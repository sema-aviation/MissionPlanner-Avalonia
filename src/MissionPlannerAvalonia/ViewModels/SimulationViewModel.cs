using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels;

public partial class SimulationViewModel : ViewModelBase {
  private readonly SitlLauncher _sitl = new();
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  [ObservableProperty]
  private string _status = "Select a firmware to simulate, then press Start.";

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartStopCommand))]
  private bool _isBusy;

  [ObservableProperty]
  private bool _isRunning;

  [ObservableProperty]
  private string _startStopText = "Start";

  [ObservableProperty]
  private bool _isPlane = true;

  [ObservableProperty]
  private bool _isRover;

  [ObservableProperty]
  private bool _isCopter;

  [ObservableProperty]
  private bool _isHeli;

  public SimulationViewModel() {
    _sitl.Log += OnLog;
  }

  private SitlVehicle SelectedVehicle =>
      IsCopter ? SitlVehicle.Copter :
      IsRover ? SitlVehicle.Rover :
      IsHeli ? SitlVehicle.Heli :
      SitlVehicle.Plane;

  private void OnLog(string line) => Dispatcher.UIThread.Post(() => {
    Log += line + "\n";
    Status = line;
  });

  [RelayCommand(CanExecute = nameof(CanStartStop))]
  private async Task StartStop() {
    if (IsRunning) {
      _sitl.Stop();
      await DisconnectAsync();
      IsRunning = false;
      StartStopText = "Start";
      Status = "SITL stopped.";
      return;
    }

    IsBusy = true;
    StartStopText = "Starting…";
    try {
      // macOS has no prebuilt ArduPilot SITL binary, so StartAsync returns false there.
      bool ok = await _sitl.StartAsync(SelectedVehicle);
      if (!ok) {
        Status = "SITL did not start (no prebuilt binary on this platform?). See log.";
        StartStopText = "Start";
        return;
      }

      Status = $"Connecting to {_sitl.TcpEndpoint} …";
      bool connected = await ConnectAsync();
      IsRunning = true;
      StartStopText = "Stop";
      Status = connected
          ? $"SITL running and connected on {_sitl.TcpEndpoint}."
          : $"SITL running on {_sitl.TcpEndpoint}; auto-connect failed — connect manually.";
    } catch (Exception ex) {
      Status = "Start error: " + ex.Message;
      StartStopText = "Start";
    } finally {
      IsBusy = false;
    }
  }

  private bool CanStartStop() => !IsBusy;

  // Mirrors ConnectionViewModel's TCP path: prefill CommsSettings, hand the
  // MAVLinkInterface a TcpSerial transport pointed at the SITL endpoint, then Open.
  private async Task<bool> ConnectAsync() {
    if (_comPort.BaseStream?.IsOpen == true) {
      return true;
    }

    AppState.CommsSettings["TCP_host"] = "127.0.0.1";
    AppState.CommsSettings["TCP_port"] = "5760";
    _comPort.BaseStream = new TcpSerial();
    try {
      await Task.Run(() => _comPort.Open(getparams: true, skipconnectedcheck: true, showui: false));
      return _comPort.BaseStream.IsOpen;
    } catch (Exception ex) {
      OnLog("Connect error: " + ex.Message);
      return false;
    }
  }

  private async Task DisconnectAsync() {
    if (_comPort.BaseStream?.IsOpen == true) {
      await Task.Run(() => _comPort.Close());
    }
  }
}
