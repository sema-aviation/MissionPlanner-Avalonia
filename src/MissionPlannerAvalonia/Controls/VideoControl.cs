using System;

using Avalonia.Controls;

using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;

namespace MissionPlannerAvalonia.Controls;

public class VideoControl : UserControl {
  private readonly VideoView _videoView;
  private LibVLCSharp.Shared.LibVLC? _libVlc;
  private MediaPlayer? _mediaPlayer;
  private bool _isAvailable;
  private string _status = "video not started";

  public VideoControl() {
    _videoView = new VideoView();
    Content = _videoView;
    InitializeCore();
  }

  public bool IsAvailable => _isAvailable;

  public string Status => _status;

  public void Play(string mrl) {
    if (!_isAvailable || _libVlc is null || _mediaPlayer is null) {
      return;
    }

    if (string.IsNullOrWhiteSpace(mrl)) {
      _status = "no source set";
      return;
    }

    try {
      using var media = new Media(_libVlc, mrl, FromType.FromLocation);
      _mediaPlayer.Play(media);
      _currentMrl = mrl;
      _status = $"playing: {mrl}";
    } catch (Exception ex) {
      _status = $"play failed: {ex.Message}";
    }
  }

  public void Stop() {
    if (!_isAvailable || _mediaPlayer is null) {
      return;
    }

    try {
      _mediaPlayer.Stop();
      _status = "stopped";
    } catch (Exception ex) {
      _status = $"stop failed: {ex.Message}";
    }
  }

  public bool TryRecord(string outPath) {
    if (!_isAvailable || _libVlc is null || _mediaPlayer is null) {
      return false;
    }

    if (string.IsNullOrWhiteSpace(outPath) || string.IsNullOrWhiteSpace(_currentMrl)) {
      _status = "record unavailable: no active source";
      return false;
    }

    try {
      var sout = $":sout=#duplicate{{dst=display,dst=std{{access=file,mux=ts,dst={outPath}}}}}";
      var media = new Media(_libVlc, _currentMrl, FromType.FromLocation);
      media.AddOption(sout);
      media.AddOption(":sout-keep");
      _mediaPlayer.Play(media);
      media.Dispose();
      _status = $"recording: {outPath}";
      return true;
    } catch (Exception ex) {
      _status = $"record failed: {ex.Message}";
      return false;
    }
  }

  public void Snapshot(string outPath) {
    if (!_isAvailable || _mediaPlayer is null) {
      return;
    }

    try {
      _mediaPlayer.TakeSnapshot(0, outPath, 0, 0);
      _status = $"snapshot: {outPath}";
    } catch (Exception ex) {
      _status = $"snapshot failed: {ex.Message}";
    }
  }

  private string? _currentMrl;

  private void InitializeCore() {
    // Load-bearing guard: native libvlc binaries may be absent in this
    // environment; Core.Initialize / new LibVLC throw if so. Degrade to an
    // unavailable status instead of crashing the whole FlightData view.
    try {
      LibVLCSharp.Shared.Core.Initialize();
      _libVlc = new LibVLCSharp.Shared.LibVLC();
      _mediaPlayer = new MediaPlayer(_libVlc);
      _videoView.MediaPlayer = _mediaPlayer;
      _isAvailable = true;
      _status = "video ready";
    } catch (Exception ex) {
      _isAvailable = false;
      _status = $"video unavailable: libvlc not found ({ex.Message})";
    }
  }
}
