using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MissionPlannerAvalonia.Services;

public class TlogPlayer {
  readonly object _locker = new();

  List<MAVLink.MAVLinkMessage> _packets = new();
  DateTime _start = DateTime.MinValue;
  DateTime _end = DateTime.MinValue;

  Thread? _thread;
  volatile bool _running;
  volatile bool _paused;
  volatile bool _stop;
  int _index;
  double _speed = 1.0;

  public event Action<MAVLink.MAVLinkMessage>? Packet;
  public event Action<double>? Progress;

  public bool IsOpen { get; private set; }

  public bool IsPlaying => _running && !_paused;

  public double Speed {
    get => _speed;
    set => _speed = Math.Clamp(value, 0.1, 10.0);
  }

  public TimeSpan Duration {
    get {
      if (!IsOpen || _packets.Count == 0) {
        return TimeSpan.Zero;
      }

      var d = _end - _start;
      return d < TimeSpan.Zero ? TimeSpan.Zero : d;
    }
  }

  public TimeSpan Position {
    get {
      if (!IsOpen || _packets.Count == 0) {
        return TimeSpan.Zero;
      }

      var i = Math.Min(_index, _packets.Count - 1);
      var t = _packets[i].rxtime - _start;
      return t < TimeSpan.Zero ? TimeSpan.Zero : t;
    }
  }

  public void Open(string tlogPath) {
    Close();

    var packets = new List<MAVLink.MAVLinkMessage>();

    using (var stream = File.Open(tlogPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
      var parse = new MAVLink.MavlinkParse(true);

      while (stream.Position < stream.Length) {
        MAVLink.MAVLinkMessage? msg;
        try {
          msg = parse.ReadPacket(stream);
        } catch (EndOfStreamException) {
          break;
        } catch {
          continue;
        }

        if (msg == null) {
          continue;
        }

        packets.Add(msg);
      }
    }

    _packets = packets;
    _index = 0;
    _stop = false;
    _paused = false;

    if (packets.Count > 0) {
      _start = packets[0].rxtime;
      _end = packets[packets.Count - 1].rxtime;
      IsOpen = true;
    }
  }

  public void Play() {
    if (!IsOpen) {
      return;
    }

    _paused = false;

    lock (_locker) {
      if (_running) {
        return;
      }

      _stop = false;
      _running = true;
      _thread = new Thread(PlaybackLoop) { IsBackground = true, Name = "TlogPlayer" };
      _thread.Start();
    }
  }

  public void Pause() {
    _paused = true;
  }

  public void Seek(double fraction) {
    if (!IsOpen) {
      return;
    }

    fraction = Math.Clamp(fraction, 0.0, 1.0);
    var target = TimeSpan.FromSeconds(Duration.TotalSeconds * fraction);

    var idx = _packets.Count - 1;
    for (var i = 0; i < _packets.Count; i++) {
      if (_packets[i].rxtime - _start >= target) {
        idx = i;
        break;
      }
    }

    _index = idx;
  }

  public void Close() {
    _stop = true;
    _paused = false;

    var t = _thread;
    if (t != null && t.IsAlive && t != Thread.CurrentThread) {
      t.Join(500);
    }

    _thread = null;
    _running = false;
    IsOpen = false;
    _index = 0;
    _packets = new List<MAVLink.MAVLinkMessage>();
  }

  void PlaybackLoop() {
    try {
      while (!_stop && _index < _packets.Count) {
        while (_paused && !_stop) {
          Thread.Sleep(20);
        }

        if (_stop) {
          break;
        }

        var msg = _packets[_index];
        Packet?.Invoke(msg);

        var duration = Duration.TotalSeconds;
        if (duration > 0) {
          Progress?.Invoke(Math.Clamp((msg.rxtime - _start).TotalSeconds / duration, 0.0, 1.0));
        }

        var current = msg.rxtime;
        _index++;
        if (_index >= _packets.Count) {
          break;
        }

        var ms = (_packets[_index].rxtime - current).TotalMilliseconds / _speed;
        SleepChunked(ms);
      }

      if (!_stop && _index >= _packets.Count) {
        Progress?.Invoke(1.0);
      }
    } finally {
      _running = false;
    }
  }

  void SleepChunked(double milliseconds) {
    var remaining = milliseconds;
    while (remaining > 0 && !_stop && !_paused) {
      var chunk = Math.Min(20.0, remaining);
      Thread.Sleep((int)Math.Ceiling(chunk));
      remaining -= chunk;
    }
  }

  public static void ExportKml(string tlogPath, string outKmlPath) {
    var track = new List<(double lat, double lng, double alt, DateTime time)>();

    using (var stream = File.Open(tlogPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
      var parse = new MAVLink.MavlinkParse(true);

      while (stream.Position < stream.Length) {
        MAVLink.MAVLinkMessage? msg;
        try {
          msg = parse.ReadPacket(stream);
        } catch (EndOfStreamException) {
          break;
        } catch {
          continue;
        }

        if (msg == null || msg.msgid != (uint)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT) {
          continue;
        }

        if (msg.data is not MAVLink.mavlink_global_position_int_t gpi) {
          continue;
        }

        var lat = gpi.lat / 1e7;
        var lng = gpi.lon / 1e7;
        var alt = gpi.alt / 1000.0;

        if (lat == 0 && lng == 0) {
          continue;
        }

        track.Add((lat, lng, alt, msg.rxtime));
      }
    }

    DataFlashLog.WriteKmlTrack(track, outKmlPath);
  }
}
