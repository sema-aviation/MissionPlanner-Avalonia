using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels;

public partial class SimulationViewModel : ViewModelBase {
  private readonly SitlLauncher _sitl = new();
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  // Raised after SITL starts and the link connects, asking the shell to switch to FlightData.
  // The shell subscribes; this VM never touches MainWindowViewModel directly.
  public event Action? RequestFlightData;

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

  // Home point (set by dragging/clicking the "H" marker on the SITL map). Alt comes from SRTM.
  [ObservableProperty]
  private double _homeLat;

  [ObservableProperty]
  private double _homeLng;

  [ObservableProperty]
  private double _homeAlt;

  // -O / --home heading and -s speed.
  [ObservableProperty]
  private int _heading;

  [ObservableProperty]
  private int _simSpeed = 1;

  // cmb_model override; empty => vehicle default frame.
  [ObservableProperty]
  private string _selectedModel = "";

  // Free-text extra args (txt_cmdline) and --wipe toggle.
  [ObservableProperty]
  private string _extraCmdline = "";

  [ObservableProperty]
  private bool _wipeEeprom;

  // cmb_version index: 0 Dev, 1 Beta, 2 Stable, 3 Skip (persisted as sitl_download_version).
  [ObservableProperty]
  private int _selectedChannelIndex;

  // Full 34-entry cmb_model list from SITL.resx (leading blank = "use vehicle default").
  public IReadOnlyList<string> Models { get; } = new[] {
    "", "quadplane", "xplane", "xplane-heli", "firefly", "+", "quad", "copter", "x",
    "hexa", "octa", "tri", "y6", "heli", "heli-dual", "heli-compound", "singlecopter",
    "coaxcopter", "rover", "crrcsim", "jsbsim", "flightaxis", "gazebo", "last_letter",
    "tracker", "balloon", "plane", "calibration", "plane-jet", "sailboat", "motorboat",
    "morse-rover", "rover-skid", "plane-3d",
  };

  // cmb_version display strings (SITL.cs versionSelect).
  public IReadOnlyList<string> Channels { get; } = new[] {
    "Latest (Dev)", "Beta", "Stable", "Skip Download",
  };

  public SimulationViewModel() {
    _sitl.Log += OnLog;

    try {
      SelectedChannelIndex = Settings.Instance.GetInt32("sitl_download_version");
    } catch {
      SelectedChannelIndex = 0;
    }

    if (SelectedChannelIndex < 0 || SelectedChannelIndex >= Channels.Count) {
      SelectedChannelIndex = 0;
    }

    InitHome();
  }

  // Seed the home marker from the planned home location, else ArduPilot's CMAC default.
  private void InitHome() {
    var planned = _comPort.MAV?.cs?.PlannedHomeLocation;
    if (planned != null && (planned.Lat != 0 || planned.Lng != 0)) {
      SetHome(planned.Lat, planned.Lng);
    } else {
      SetHome(-35.3633515, 149.1652412);
    }
  }

  // Update the home point and look up its SRTM altitude (mirrors BuildHomeLocation's srtm call).
  public void SetHome(double lat, double lng) {
    HomeLat = lat;
    HomeLng = lng;
    try {
      HomeAlt = srtm.getAltitude(lat, lng).alt;
    } catch {
      HomeAlt = 0;
    }
  }

  private SitlVehicle SelectedVehicle =>
      IsCopter ? SitlVehicle.Copter :
      IsRover ? SitlVehicle.Rover :
      IsHeli ? SitlVehicle.Heli :
      SitlVehicle.Plane;

  private SitlChannel SelectedChannel => SelectedChannelIndex switch {
    1 => SitlChannel.Beta,
    2 => SitlChannel.Stable,
    3 => SitlChannel.Skip,
    _ => SitlChannel.Dev,
  };

  // "lat,lng,alt,heading" for -O / --home (BuildHomeLocation).
  private string BuildHome() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}",
      HomeLat, HomeLng, HomeAlt, Heading);

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

    // Persist the channel choice for next launch (Settings sitl_download_version).
    try {
      Settings.Instance["sitl_download_version"] = SelectedChannelIndex.ToString();
    } catch {
      // settings store unavailable — non-fatal
    }

    IsBusy = true;
    StartStopText = "Starting…";
    try {
      var opts = new SitlStartOptions {
        Vehicle = SelectedVehicle,
        Channel = SelectedChannel,
        Model = SelectedModel,
        Home = BuildHome(),
        Speed = SimSpeed,
        ExtraCmdline = ExtraCmdline,
        WipeEeprom = WipeEeprom,
      };

      // macOS has no prebuilt ArduPilot SITL binary, so StartAsync returns false there.
      bool ok = await _sitl.StartAsync(opts);
      if (!ok) {
        Status = "SITL did not start (no prebuilt binary on this platform/channel?). See log.";
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

      // Mirrors StartSITL's ShowScreen(screens[0]) — jump to FlightData once linked.
      if (connected) {
        RequestFlightData?.Invoke();
      }
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
      bool open = _comPort.BaseStream.IsOpen;
      if (open) {
        AppState.RaiseConnectionChanged();
      }
      return open;
    } catch (Exception ex) {
      OnLog("Connect error: " + ex.Message);
      return false;
    }
  }

  private async Task DisconnectAsync() {
    if (_comPort.BaseStream?.IsOpen == true) {
      await Task.Run(() => _comPort.Close());
      AppState.RaiseConnectionChanged();
    }
  }
}
