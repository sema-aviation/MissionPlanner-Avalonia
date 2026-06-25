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

  // ---- Actions tab ----
  public ObservableCollection<string> Actions { get; } =
      new()
      {
            "Loiter_Unlim",
            "Return_To_Launch",
            "Preflight_Calibration",
            "Mission_Start",
            "Preflight_Reboot_Shutdown",
            "Trigger_Camera",
            "Battery_Reset",
            "Toggle_Safety_Switch",
            "Do_Parachute",
            "Engine_Start",
            "Engine_Stop",
            "Terminate_Flight",
      };

  [ObservableProperty]
  private string _selectedAction = "Return_To_Launch";

  [ObservableProperty]
  private int _setWpIndex;

  [ObservableProperty]
  private double _homeAlt;

  [RelayCommand]
  [Obsolete]
  private async Task DoAction() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    var a = SelectedAction;
    await Task.Run(() => RunAction(a));
    Log($"Action {a} sent");
  }

  [Obsolete]
  private void RunAction(string a) {
    byte s = _comPort.MAV.sysid;
    byte c = _comPort.MAV.compid;
    switch (a) {
      case "Loiter_Unlim":
        _comPort.setMode("Loiter");
        break;
      case "Return_To_Launch":
        _comPort.setMode("RTL");
        break;
      case "Preflight_Calibration":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 1, 1, 0, 0, 0, 0, 0);
        break;
      case "Mission_Start":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.MISSION_START, 0, 0, 0, 0, 0, 0, 0);
        break;
      case "Preflight_Reboot_Shutdown":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN, 1, 0, 0, 0, 0, 0, 0);
        break;
      case "Trigger_Camera":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_DIGICAM_CONTROL, 0, 0, 0, 0, 1, 0, 0);
        break;
      case "Battery_Reset":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.BATTERY_RESET, 255, 100, 0, 0, 0, 0, 0);
        break;
      case "Toggle_Safety_Switch":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_SET_SAFETY_SWITCH_STATE, 0, 0, 0, 0, 0, 0, 0);
        break;
      case "Do_Parachute":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_PARACHUTE, 2, 0, 0, 0, 0, 0, 0);
        break;
      case "Engine_Start":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_ENGINE_CONTROL, 1, 0, 0, 0, 0, 0, 0);
        break;
      case "Engine_Stop":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_ENGINE_CONTROL, 0, 0, 0, 0, 0, 0, 0);
        break;
      case "Terminate_Flight":
        _comPort.doCommand(s, c, MAVLink.MAV_CMD.DO_FLIGHTTERMINATION, 1, 0, 0, 0, 0, 0, 0);
        break;
    }
  }

  [RelayCommand]
  private async Task SetWp() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    ushort idx = (ushort)SetWpIndex;
    await Task.Run(() => _comPort.setWPCurrent(_comPort.MAV.sysid, _comPort.MAV.compid, idx));
    Log($"Set current WP {idx}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task SetHome() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_SET_HOME,
            0, 0, 0, 0, 0, 0, (float)HomeAlt));
    Log($"Set home alt {HomeAlt}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task AbortLand() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_GO_AROUND,
            0, 0, 0, 0, 0, 0, 0));
    Log("Abort landing (go-around) sent");
  }

  // ---- Servo / Relay tab ----
  [ObservableProperty]
  private int _servoNumber = 1;

  [ObservableProperty]
  private int _servoPwm = 1500;

  private readonly bool[] _relayState = new bool[6];

  [RelayCommand]
  [Obsolete]
  private async Task SetServo() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    int n = ServoNumber;
    int pwm = ServoPwm;
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_SET_SERVO,
            n, pwm, 0, 0, 0, 0, 0));
    Log($"Servo {n} -> {pwm} us");
  }

  [RelayCommand]
  [Obsolete]
  private async Task ToggleRelay(string number) {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    if (!int.TryParse(number, out int idx)) {
      return;
    }
    bool on = !_relayState[idx];
    _relayState[idx] = on;
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_SET_RELAY,
            idx, on ? 1 : 0, 0, 0, 0, 0, 0));
    Log($"Relay {idx} {(on ? "ON" : "OFF")}");
  }

  // ---- Payload Control tab ----
  [ObservableProperty]
  private double _gimbalPitch;

  [ObservableProperty]
  private double _gimbalYaw;

  [RelayCommand]
  [Obsolete]
  private async Task PointGimbal() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    double pitch = GimbalPitch;
    double yaw = GimbalYaw;
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_MOUNT_CONTROL,
            (float)pitch, 0, (float)yaw, 0, 0, 0, 2));
    Log($"Gimbal pitch {pitch} yaw {yaw}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task TriggerCamera() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_DIGICAM_CONTROL,
            0, 0, 0, 0, 1, 0, 0));
    Log("Camera trigger sent");
  }

  private void Log(string m) => Messages += m + "\n";
}

public partial class ServoOut : ObservableObject {
  public ServoOut(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private int _value;
}
