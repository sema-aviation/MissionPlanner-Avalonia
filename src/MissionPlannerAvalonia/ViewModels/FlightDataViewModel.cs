using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

public partial class FlightDataViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;

  [ObservableProperty]
  private double _roll;

  [ObservableProperty]
  private double _pitch;

  [ObservableProperty]
  private double _yaw;

  [ObservableProperty]
  private double _alt;

  [ObservableProperty]
  private double _groundSpeed;

  [ObservableProperty]
  private double _airSpeed;

  [ObservableProperty]
  private double _wpDist;

  [ObservableProperty]
  private double _verticalSpeed;

  [ObservableProperty]
  private double _distToHome;

  [ObservableProperty]
  private double _batteryVoltage;

  [ObservableProperty]
  private int _batteryRemaining;

  [ObservableProperty]
  private double _satCount;

  [ObservableProperty]
  private double _gpsHdop;

  [ObservableProperty]
  private string _mode = "—";

  [ObservableProperty]
  private bool _armed;

  [ObservableProperty]
  private string _armText = "ARM";

  [ObservableProperty]
  private string _messages = "";

  public ObservableCollection<ServoOut> Servos { get; } = new();

  public LogBrowseViewModel TelemetryLogs { get; } = new();
  public LogBrowseViewModel DataFlashLogs { get; } = new();

  [ObservableProperty]
  private bool _prearmOk;

  [ObservableProperty]
  private string _prearmText = "—";

  [ObservableProperty]
  private double _ekfStatus;

  public ObservableCollection<string> Modes { get; } =
      new()
      {
            "STABILIZE",
            "ALT_HOLD",
            "LOITER",
            "AUTO",
            "GUIDED",
            "RTL",
            "LAND",
            "POSHOLD",
            "BRAKE",
            "ACRO",
            "MANUAL",
            "FBWA",
            "FBWB",
            "CRUISE",
            "CIRCLE",
      };

  [ObservableProperty]
  private string _selectedMode = "STABILIZE";

  public FlightDataViewModel() {
    for (int i = 1; i <= 8; i++) {
      Servos.Add(new ServoOut(i));
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private void Pump() {
    var cs = _comPort.MAV?.cs;
    if (cs == null) {
      return;
    }

    Roll = cs.roll;
    Pitch = cs.pitch;
    Yaw = cs.yaw;
    Alt = cs.alt;
    GroundSpeed = cs.groundspeed;
    AirSpeed = cs.airspeed;
    WpDist = cs.wp_dist;
    VerticalSpeed = cs.verticalspeed;
    DistToHome = cs.DistToHome;
    BatteryVoltage = cs.battery_voltage;
    BatteryRemaining = cs.battery_remaining;
    SatCount = cs.satcount;
    GpsHdop = cs.gpshdop;
    Mode = cs.mode ?? "—";
    Armed = cs.armed;
    ArmText = cs.armed ? "DISARM" : "ARM";

    float[] outs =
    {
            cs.ch1out,
            cs.ch2out,
            cs.ch3out,
            cs.ch4out,
            cs.ch5out,
            cs.ch6out,
            cs.ch7out,
            cs.ch8out,
        };
    for (int i = 0; i < 8; i++) {
      Servos[i].Value = (int)outs[i];
    }

    PrearmOk = cs.prearmstatus;
    PrearmText = PrearmOk ? "Ready to Arm" : "Not Ready to Arm";
    EkfStatus = cs.ekfstatus;
  }

  private bool Connected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  [Obsolete]
  private async Task ToggleArm() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    bool target = !Armed;
    bool ok = await Task.Run(() => _comPort.doARM(target, false));
    Messages += $"{(target ? "Arm" : "Disarm")}: {(ok ? "ok" : "rejected")}\n";
  }

  [RelayCommand]
  [Obsolete]
  private async Task SetMode() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    var m = SelectedMode;
    await Task.Run(() => _comPort.setMode(m));
    Messages += $"Set mode {m}\n";
  }

  [RelayCommand]
  [Obsolete]
  private Task QuickAuto() {
    SelectedMode = "AUTO";
    return SetMode();
  }

  [RelayCommand]
  [Obsolete]
  private Task QuickLoiter() {
    SelectedMode = "LOITER";
    return SetMode();
  }

  [RelayCommand]
  [Obsolete]
  private Task QuickRtl() {
    SelectedMode = "RTL";
    return SetMode();
  }
}

public partial class ServoOut : ObservableObject {
  public ServoOut(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private int _value;
}
