using System;
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
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }
    foreach (var net in new[] { "TCP", "UDP", "UDPCl", "WS" }) {
      Ports.Add(net);
    }

    SelectedPort = Ports.Contains(cur ?? "") ? cur : Ports.FirstOrDefault(p => p != "AUTO");
  }

  [RelayCommand]
  private async Task ToggleConnect() {
    if (_comPort.BaseStream?.IsOpen == true) {
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
      } finally {
        _connectDialog = null;
        dlg.Close();
      }

      // User pressed Cancel: abort quietly, no error popup.
      if (dlg.CancelRequested) {
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
        IsConnected = false;
        Status = "";
        await Services.Dialogs.Alert("Connection error", openError.Message);
        return;
      }

      IsConnected = _comPort.BaseStream.IsOpen;
      ConnectText = IsConnected ? "DISCONNECT" : "CONNECT";
      if (IsConnected) {
        Status = $"Connected. {_comPort.MAV.param.Count} params.";
        // Cache params so the Full Parameter List is viewable offline next time.
        RawParamsViewModel.SaveSnapshot(_comPort);
      } else {
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
