using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Views;

namespace MissionPlannerAvalonia.ViewModels;

public partial class ConnectionViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<string> Ports { get; } = new();
  public ObservableCollection<int> Bauds { get; } =
      new() { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud = 115200;

  [ObservableProperty]
  private bool _isConnected;

  [ObservableProperty]
  private string _connectText = "CONNECT";

  [ObservableProperty]
  private string _status = "";

  // 0–100 telemetry/param-download progress; -1 hides the bar (mirrors MP status1.Percent).
  [ObservableProperty]
  private int _progress = -1;

  // Block writes to the vehicle (mirrors CTX readonlyToolStripMenuItem -> comPort.ReadOnly).
  [ObservableProperty]
  private bool _readOnly;

  partial void OnReadOnlyChanged(bool value) => _comPort.ReadOnly = value;

  public ConnectionViewModel() {
    _comPort.Progress += OnProgress;
    RefreshPorts();
  }

  private Services.ProgressReporter? _connectDialog;

  // Background packet reader. MP runs this as MainV2.SerialReader; MAVLinkInterface does not
  // self-read, so without it the stream is never drained after Open() -> cs stays frozen
  // (HUD/telemetry all zero) and unread heartbeats desync the link (param refresh times out).
  private CancellationTokenSource? _readerCts;

  private void StartReader() {
    StopReader();
    // Stream rates default to 0 on a fresh CurrentState; UpdateCurrentSettings re-requests streams
    // at these rates, so 0 tells the autopilot to STOP streaming (telemetry froze after the initial
    // burst). MP loads these from Settings — seed sane Hz here and request the streams up front.
    foreach (var mav in _comPort.MAVlist) {
      mav.cs.rateattitude = 4;
      mav.cs.rateposition = 2;
      mav.cs.ratestatus = 2;
      mav.cs.ratesensors = 2;
      mav.cs.raterc = 2;
    }
    RequestStreams();

    var cts = new CancellationTokenSource();
    _readerCts = cts;
    _ = Task.Run(() => SerialReaderLoop(cts));
  }

  private void RequestStreams() {
    try {
      foreach (var mav in _comPort.MAVlist) {
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTENDED_STATUS, mav.cs.ratestatus, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.POSITION, mav.cs.rateposition, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA1, mav.cs.rateattitude, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA2, mav.cs.rateattitude, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA3, mav.cs.ratesensors, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, mav.cs.ratesensors, mav.sysid, mav.compid);
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, mav.cs.raterc, mav.sysid, mav.compid);
      }
    } catch {
      // ignore: streams get re-requested by UpdateCurrentSettings if this initial request is lost
    }
  }

  private void StopReader() {
    _readerCts?.Cancel();
    _readerCts = null;
  }

  private async Task SerialReaderLoop(CancellationTokenSource cts) {
    var ct = cts.Token;
    var lastHeartbeat = DateTime.MinValue;
    int consecutiveErrors = 0;
    while (!ct.IsCancellationRequested) {
      // Stream closed without a user disconnect => link lost (remote drop). Surface it instead
      // of freezing the HUD behind a stale "DISCONNECT" button.
      if (_comPort.BaseStream?.IsOpen != true) {
        HandleLinkLost(cts);
        break;
      }

      try {
        // Don't read while a foreground op owns the port (param/wp/command/calibration); otherwise
        // the reader steals the PARAM_VALUE/ACK that op is waiting for. Mirrors MP's giveComport gate.
        if (_comPort.giveComport == false) {
          var start = DateTime.UtcNow;
          while (_comPort.giveComport == false && _comPort.BaseStream?.IsOpen == true &&
                 _comPort.BaseStream.BytesToRead > 10 && !ct.IsCancellationRequested &&
                 start.AddSeconds(1) > DateTime.UtcNow) {
            await _comPort.readPacketAsync().ConfigureAwait(false);
          }

          foreach (var mav in _comPort.MAVlist) {
            mav.cs.UpdateCurrentSettings(null, false, _comPort, mav);
          }

          // 1 Hz GCS heartbeat keeps the autopilot streaming to us (it stops to an inactive GCS).
          if ((DateTime.UtcNow - lastHeartbeat).TotalSeconds >= 1) {
            lastHeartbeat = DateTime.UtcNow;
            SendHeartbeat();
          }
        }

        consecutiveErrors = 0;
        await Task.Delay(_comPort.giveComport ? 50 : 1, ct).ConfigureAwait(false);
      } catch (OperationCanceledException) {
        break;
      } catch {
        // A read fault on a still-"open" port usually means the device vanished (USB unplug). A
        // SerialPort keeps IsOpen==true until Close(), so without bailing we'd spin at 20Hz forever.
        if (++consecutiveErrors >= 5) {
          HandleLinkLost(cts);
          break;
        }
        try {
          await Task.Delay(50, ct).ConfigureAwait(false);
        } catch (OperationCanceledException) {
          break;
        }
      }
    }
  }

  // GCS heartbeat per MAV, addressed to that MAV so generatePacket frames it with the link's
  // mavlink version + signing (a single gcssysid heartbeat is always mavlink1/unsigned).
  private void SendHeartbeat() {
    foreach (var mav in _comPort.MAVlist) {
      try {
        _comPort.sendPacket(
            new MAVLink.mavlink_heartbeat_t {
              type = (byte)MAVLink.MAV_TYPE.GCS,
              autopilot = (byte)MAVLink.MAV_AUTOPILOT.INVALID,
              mavlink_version = 3,
            },
            mav.sysid, mav.compid);
      } catch {
        // ignore: a dropped heartbeat is recovered on the next tick
      }
    }
  }

  // Unexpected link loss (not a user disconnect). Close the port and reset the UI on the UI thread.
  private void HandleLinkLost(CancellationTokenSource self) {
    try {
      _comPort.Close();
    } catch {
      // already down
    }

    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
      if (_readerCts != self) {
        return; // a user disconnect or a newer connection already took over
      }

      _readerCts = null;
      IsConnected = false;
      ConnectText = "CONNECT";
      Status = "Connection lost.";
      AppState.RaiseConnectionChanged();
    });
  }

  private void OnProgress(int percent, string status) =>
      Avalonia.Threading.Dispatcher.UIThread.Post(() => {
        Progress = percent;
        if (!string.IsNullOrEmpty(status)) {
          Status = status;
        }
        _connectDialog?.Set(percent < 0 ? 0 : percent, status);
      });

  [RelayCommand]
  private void RefreshPorts() {
    var cur = SelectedPort;
    Ports.Clear();
    Ports.Add("AUTO");
    foreach (var p in DedupePorts(SerialPort.GetPortNames())) {
      Ports.Add(p);
    }
    foreach (var net in new[] { "TCP", "UDP", "UDPCl", "WS" }) {
      Ports.Add(net);
    }

    SelectedPort = Ports.Contains(cur ?? "") ? cur : Ports.FirstOrDefault(p => p != "AUTO");
  }

  // macOS virtual ports that are never an autopilot — pure dropdown noise.
  private static readonly string[] InternalPorts = { "Bluetooth-Incoming-Port", "debug-console" };

  // macOS exposes each serial device as both /dev/cu.X and /dev/tty.X — the dropdown showed
  // every port twice. Keep cu.* (the dial-out node MP uses), drop the matching tty.*, and hide
  // the built-in virtual ports.
  private static IEnumerable<string> DedupePorts(string[] names) {
    var all = names.Distinct()
        .Where(n => !InternalPorts.Any(p => n.Contains(p, StringComparison.OrdinalIgnoreCase)))
        .ToList();
    var cuDevices = new HashSet<string>(
        all.Where(n => n.Contains("/cu.")).Select(n => n.Replace("/cu.", "/tty.")));
    return all.Where(n => !cuDevices.Contains(n)).OrderBy(n => n);
  }

  [RelayCommand]
  private async Task ToggleConnect() {
    if (_comPort.BaseStream?.IsOpen == true) {
      StopReader();
      await Task.Run(() => _comPort.Close());
      IsConnected = false;
      ConnectText = "CONNECT";
      Status = "Disconnected.";
      AppState.RaiseConnectionChanged();
      return;
    }

    var sel = SelectedPort;
    if (string.IsNullOrEmpty(sel)) {
      await Services.Dialogs.Alert("Connect", "No port selected.");
      return;
    }

    try {
      ICommsSerial? stream = await BuildStreamAsync(sel);
      if (stream == null) {
        Status = "";
        return;
      }

      _comPort.BaseStream = stream;

      var dlg = new Services.ProgressReporter("Connecting Mavlink");
      _connectDialog = dlg;
      dlg.Set(0, $"Connecting {sel}…");
      // Cancel closes the stream, which unblocks the blocking Open() call so the user can abort.
      dlg.Token.Register(() => {
        try {
          _comPort.Close();
        } catch {
          // ignore: just trying to abort a stuck connect
        }
      });
      dlg.Show2();

      Exception? openError = null;
      try {
        await Task.Run(() => _comPort.Open(getparams: true, skipconnectedcheck: true, showui: false));
      } catch (Exception ex) {
        openError = ex;
      }
      _connectDialog = null;

      // User pressed Cancel: abort quietly, no error popup.
      if (dlg.CancelRequested) {
        dlg.Close();
        try {
          _comPort.Close();
        } catch {
          // already closing
        }
        IsConnected = false;
        ConnectText = "CONNECT";
        Status = "";
        AppState.RaiseConnectionChanged();
        return;
      }

      if (openError != null) {
        dlg.Close();
        IsConnected = false;
        Status = "";
        await Services.Dialogs.Alert("Connection error", openError.Message);
        return;
      }

      IsConnected = _comPort.BaseStream.IsOpen;
      ConnectText = IsConnected ? "DISCONNECT" : "CONNECT";
      if (IsConnected) {
        Status = $"Connected. {_comPort.MAV.param.Count} params.";
        StartReader();
        // Cache params so the Full Parameter List is viewable offline next time.
        RawParamsViewModel.SaveSnapshot(_comPort);
        // Show success in the connect dialog briefly, then close it (info bar is gone).
        dlg.Set(100, Status);
        await Task.Delay(1200);
        dlg.Close();
      } else {
        dlg.Close();
        Status = "";
        await Services.Dialogs.Alert("Connection failed", $"Could not connect on {sel}.");
      }
      AppState.RaiseConnectionChanged();
    } catch (Exception ex) {
      Status = "";
      IsConnected = false;
      await Services.Dialogs.Alert("Connection error", ex.Message);
    }
  }

  // Returns the transport for the selected entry, prompting for host/port where
  // needed and prefilling AppState.CommsSettings so the transport reads them back.
  private async Task<ICommsSerial?> BuildStreamAsync(string sel) {
    switch (sel) {
      case "AUTO":
        return await Task.Run(ScanForPort);

      case "TCP": {
          var v = await PromptAsync("TCP client", "Host / IP", Setting("TCP_host", "127.0.0.1"),
                                     "Remote port", Setting("TCP_port", "5760"));
          if (v == null) {
            return null;
          }

          Store("TCP_host", v[0]);
          Store("TCP_port", v[1]);
          return new TcpSerial();
        }

      case "UDPCl": {
          var v = await PromptAsync("UDP client", "Host / IP", Setting("UDP_host", "127.0.0.1"),
                                     "Remote port", Setting("UDP_port", "14550"));
          if (v == null) {
            return null;
          }

          Store("UDP_host", v[0]);
          Store("UDP_port", v[1]);
          return new UdpSerialConnect();
        }

      case "UDP": {
          var v = await PromptAsync("UDP listener", "Local port", Setting("UDP_port", "14550"), null, "");
          if (v == null) {
            return null;
          }

          Store("UDP_port", v[0]);
          return new UdpSerial();
        }

      case "WS": {
          var v = await PromptAsync("WebSocket", "URL", Setting("WS_url", "ws://127.0.0.1:8080"), null, "");
          if (v == null) {
            return null;
          }

          Store("WS_url", v[0]);
          return new WebSocket();
        }

      default:
        return new SerialPort { PortName = sel, BaudRate = SelectedBaud };
    }
  }

  private ICommsSerial? ScanForPort() {
    CommsSerialScan.Scan(false);
    var deadline = DateTime.Now.AddSeconds(20);
    while (!CommsSerialScan.foundport && CommsSerialScan.run == 1 && DateTime.Now < deadline) {
      Thread.Sleep(300);
    }
    return CommsSerialScan.portinterface?.FirstOrDefault();
  }

  private static string Setting(string key, string fallback) {
    var v = AppState.CommsSettings.TryGetValue(key, out var s) ? s : "";
    return string.IsNullOrEmpty(v) ? fallback : v;
  }

  private static void Store(string key, string? value) =>
      AppState.CommsSettings[key] = value ?? "";

  private static async Task<string[]?> PromptAsync(
      string title, string l1, string v1, string? l2, string v2) {
    var owner = (Avalonia.Application.Current?.ApplicationLifetime
                 as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (owner == null) {
      return new[] { v1, v2 };
    }

    var r = await ConnectDialog.Show(owner, title, l1, v1, l2, v2);
    if (r == null) {
      return null;
    }

    return new[] { r[0] ?? "", r[1] ?? "" };
  }
}
