using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

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
    // MP flowLayoutPanelServos = servoOptions ch5..16 then relayOptions 0..3,
    // wrapped into 2 columns (8 rows each). Single list + vertical wrap reproduces it.
    for (int ch = 5; ch <= 16; ch++) {
      ServoRelayItems.Add(new ServoChannel(ch));
    }
    for (int r = 0; r < 4; r++) {
      ServoRelayItems.Add(new RelayChannel(r));
    }
    // MP tabAuxFunction = 7 auxOptions (RCx_OPTION combos)
    for (int ch = 7; ch <= 13; ch++) {
      AuxOptions.Add(new AuxRow(ch, new ParamField($"RC{ch}_OPTION")));
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
    BatCurrent = cs.current;
    NavBearing = cs.nav_bearing;

    if (_hudUserFields.Count > 0) {
      var sb = new System.Text.StringBuilder();
      foreach (var p in StatusProps) {
        if (!_hudUserFields.Contains(p.Name)) {
          continue;
        }
        object? v;
        try {
          v = p.GetValue(cs);
        } catch {
          v = null;
        }
        sb.AppendLine(v is float or double ? $"{p.Name}: {v:0.00}" : $"{p.Name}: {v}");
      }
      HudCustomText = sb.ToString().TrimEnd();
    } else if (HudCustomText.Length > 0) {
      HudCustomText = "";
    }

    RefreshStatus(cs);
    RefreshPreflight(cs);
  }

  // ---- Quick tab ----
  [ObservableProperty]
  private double _batCurrent;

  // ---- Status tab: full cs.GetProperties() reflection dump (mirrors MP tabStatus_Paint) ----
  public ObservableCollection<StatusItem> Statuses { get; } = new();

  // MP: cs.GetItemList(true) -> all public instance props, alpha-sorted.
  private static readonly System.Reflection.PropertyInfo[] StatusProps =
      typeof(MissionPlanner.CurrentState)
          .GetProperties()
          .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead)
          .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
          .ToArray();

  private void RefreshStatus(MissionPlanner.CurrentState cs) {
    if (Statuses.Count != StatusProps.Length) {
      Statuses.Clear();
      foreach (var p in StatusProps) {
        Statuses.Add(new StatusItem(p.Name));
      }
    }
    for (int i = 0; i < StatusProps.Length; i++) {
      object? v;
      try {
        v = StatusProps[i].GetValue(cs);
      } catch {
        v = null;
      }
      Statuses[i].Value = v is float or double ? $"{v:0.000}" : v?.ToString() ?? "";
    }
  }

  // ---- PreFlight tab (checklist with live status) ----
  public ObservableCollection<CheckItem> PreflightChecks { get; } = new();

  // first N are auto (telemetry-driven); the rest are manual user items the Edit dialog manages.
  private const int AutoCheckCount = 6;

  private void RefreshPreflight(MissionPlanner.CurrentState cs) {
    if (PreflightChecks.Count == 0) {
      PreflightChecks.Add(new CheckItem("Ready GPS"));
      PreflightChecks.Add(new CheckItem("Gps Sat Count"));
      PreflightChecks.Add(new CheckItem("Telemetry Signal"));
      PreflightChecks.Add(new CheckItem("Battery Level"));
      PreflightChecks.Add(new CheckItem("Mode"));
      PreflightChecks.Add(new CheckItem("Check Altitude"));
      PreflightChecks.Add(new CheckItem("Tail and wings secured?", manual: true));
      PreflightChecks.Add(new CheckItem("All servos respond to input?", manual: true));
    }
    PreflightChecks[0].Set($"{cs.satcount} >= 3", cs.satcount >= 3);
    PreflightChecks[1].Set($"{cs.satcount} Sats", cs.satcount >= 3);
    PreflightChecks[2].Set($"{cs.linkqualitygcs}%", cs.linkqualitygcs > 0);
    PreflightChecks[3].Set($"{cs.battery_voltage:0.0} V", cs.battery_voltage > 1);
    PreflightChecks[4].Set(cs.mode ?? "Unknown", !string.IsNullOrEmpty(cs.mode));
    PreflightChecks[5].Set($"{cs.alt:0.0} m", cs.alt < 5);
  }

  [RelayCommand]
  private async Task EditPreflight() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var current = string.Join(
        Environment.NewLine,
        PreflightChecks.Skip(AutoCheckCount).Select(c => c.Name));
    var box = new Avalonia.Controls.TextBox {
      Text = current,
      AcceptsReturn = true,
      MinWidth = 360,
      MinHeight = 180,
    };
    var ok = new Avalonia.Controls.Button {
      Content = "Save",
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    };
    var dlg = new Avalonia.Controls.Window {
      Title = "Edit Checklist Items",
      Width = 400,
      Height = 280,
      WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
      Content = new Avalonia.Controls.StackPanel {
        Margin = new Avalonia.Thickness(10),
        Spacing = 8,
        Children = {
          new Avalonia.Controls.TextBlock { Text = "One checklist item per line:" },
          box,
          ok,
        },
      },
    };
    ok.Click += (_, _) => dlg.Close(box.Text);
    var result = await dlg.ShowDialog<string?>(top);
    if (result == null) {
      return;
    }
    for (int i = PreflightChecks.Count - 1; i >= AutoCheckCount; i--) {
      PreflightChecks.RemoveAt(i);
    }
    foreach (var line in result.Split('\n')) {
      var name = line.Trim();
      if (name.Length > 0) {
        PreflightChecks.Add(new CheckItem(name, manual: true));
      }
    }
  }

  // ---- Servo/Relay tab (servoOptions ch5-16 + relayOptions 0-3) ----
  public ObservableCollection<object> ServoRelayItems { get; } = new();

  // ---- Aux Function tab (RCx_OPTION combos) ----
  public ObservableCollection<AuxRow> AuxOptions { get; } = new();

  [RelayCommand]
  [Obsolete]
  private async Task SetServoChannel(string spec) {
    if (!Connected || spec == null) {
      return;
    }
    var parts = spec.Split(':');
    if (parts.Length != 2 || !int.TryParse(parts[0], out int ch)) {
      return;
    }
    var chan = ServoRelayItems.OfType<ServoChannel>().FirstOrDefault(s => s.Number == ch);
    int pwm = parts[1] switch {
      "low" => chan?.Min ?? 1100,
      "high" => chan?.Max ?? 1900,
      "toggle" => (chan?.Toggled ?? false) ? (chan!.Min) : (chan!.Max),
      _ => (int)(((chan?.Min ?? 1100) + (chan?.Max ?? 1900)) / 2),
    };
    if (parts[1] == "toggle" && chan != null) {
      chan.Toggled = !chan.Toggled;
    }
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_SET_SERVO,
            ch, pwm, 0, 0, 0, 0, 0));
    Log($"Servo {ch} -> {pwm}");
  }

  // ---- Scripts tab ----
  [ObservableProperty]
  private string _scriptStatus = "No Script Running";

  [ObservableProperty]
  private string _selectedScript = "None";

  [ObservableProperty]
  private bool _redirectOutput = true;

  // MP shows Edit/Run/Abort only after a script is selected.
  [ObservableProperty]
  private bool _scriptSelected;

  [RelayCommand]
  private async Task SelectScript() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(
        new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Select script", AllowMultiple = false });
    if (files.Count > 0) {
      SelectedScript = files[0].Name;
      ScriptSelected = true;
      ScriptStatus = "Python scripting engine is not bundled in this cross-platform build.";
    }
  }

  [RelayCommand]
  private void RunScript() =>
      ScriptStatus = "Python scripting engine is not bundled in this cross-platform build.";

  [RelayCommand]
  private void AbortScript() => ScriptStatus = "No Script Running";

  [RelayCommand]
  private void EditScript() => Log("Edit selected script: external editor not bundled.");

  private static async Task<string?> PickFileAsync(string title, string ext, string desc) {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(
        new Avalonia.Platform.Storage.FilePickerOpenOptions {
          Title = title,
          AllowMultiple = false,
          FileTypeFilter = new[] {
            new Avalonia.Platform.Storage.FilePickerFileType(desc) { Patterns = new[] { ext } },
          },
        });
    return files.Count > 0 ? files[0].Name : null;
  }

  [RelayCommand]
  private async Task LoadTlog() {
    var name = await PickFileAsync("Open telemetry log", "*.tlog", "Telemetry log");
    if (name != null) {
      LogStatus = $"Loaded {name}. Tlog playback is not yet implemented.";
    }
  }

  [RelayCommand]
  private async Task ReviewLog() {
    var name = await PickFileAsync("Open dataflash log", "*.bin", "DataFlash log");
    if (name != null) {
      LogStatus = $"Opened {name}. Log review/graphing is not yet implemented.";
    }
  }

  // ---- Payload Control tab ----
  [ObservableProperty]
  private double _tilt;

  [ObservableProperty]
  private double _pan;

  [ObservableProperty]
  private double _roll2;

  private long _lastMountSend;

  [Obsolete]
  partial void OnTiltChanged(double value) => NudgeMount();

  [Obsolete]
  partial void OnPanChanged(double value) => NudgeMount();

  [Obsolete]
  partial void OnRoll2Changed(double value) => NudgeMount();

  // throttle continuous slider drags to ~10 Hz
  [Obsolete]
  private void NudgeMount() {
    if (!Connected) {
      return;
    }
    long now = Environment.TickCount64;
    if (now - _lastMountSend < 100) {
      return;
    }
    _lastMountSend = now;
    _ = PointMount();
  }

  [RelayCommand]
  [Obsolete]
  private async Task PointMount() {
    if (!Connected) {
      return;
    }
    double t = Tilt, p = Pan, r = Roll2;
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_MOUNT_CONTROL,
            (float)t, (float)r, (float)p, 0, 0, 0, 2));
    Log($"Mount tilt {t} pan {p} roll {r}");
  }

  [RelayCommand]
  private void ResetPosition() {
    Tilt = 0;
    Pan = 0;
    Roll2 = 0;
  }

  // ---- Log tabs ----
  [ObservableProperty]
  private string _logStatus = "";

  [RelayCommand]
  [Obsolete]
  private async Task DownloadDataflashLog() {
    if (!Connected) {
      LogStatus = "Not connected.";
      return;
    }
    LogStatus = "Requesting on-board log list…";
    try {
      var list = await _comPort.GetLogEntry();
      LogStatus = $"{list.Count} logs on the vehicle. Use the list to download.";
    } catch (Exception ex) {
      LogStatus = "Log list failed: " + ex.Message;
    }
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

  public ObservableCollection<int> WpNumbers { get; } =
      new(System.Linq.Enumerable.Range(0, 31));

  [ObservableProperty]
  private int _setWpIndex;

  [ObservableProperty]
  private double _homeAlt;

  // ModifyandSet controls (Change Speed / Change Alt / Set Loiter Rad)
  [ObservableProperty]
  private double _changeSpeedValue;

  [ObservableProperty]
  private double _changeAltValue = 100;

  [ObservableProperty]
  private double _loiterRadValue;

  [RelayCommand]
  [Obsolete]
  private async Task ChangeSpeed() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    double v = ChangeSpeedValue;
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_CHANGE_SPEED,
            0, (float)v, 0, 0, 0, 0, 0));
    Log($"Change speed {v}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task ChangeAlt() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    int newalt = (int)ChangeAltValue;
    await Task.Run(() =>
        _comPort.setNewWPAlt(new MissionPlanner.Utilities.Locationwp {
          alt = newalt / MissionPlanner.CurrentState.multiplieralt,
        }));
    Log($"Change alt {newalt}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task SetLoiterRad() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    int newrad = (int)LoiterRadValue;
    await Task.Run(() =>
        _comPort.setParam(new[] { "LOITER_RAD", "WP_LOITER_RAD" },
            newrad / MissionPlanner.CurrentState.multiplierdist));
    Log($"Set loiter rad {newrad}");
  }

  public ObservableCollection<string> MountModes { get; } =
      new() { "Retract", "Neutral", "MavLink Targeting", "RC Targeting", "GPS Point" };

  [ObservableProperty]
  private string _selectedMountMode = "Retract";

  [RelayCommand]
  [Obsolete]
  private async Task SetMount() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    int mode = MountModes.IndexOf(SelectedMountMode);
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_MOUNT_CONFIGURE,
            mode, 0, 0, 0, 0, 0, 0));
    Log($"Set mount mode {SelectedMountMode}");
  }

  [RelayCommand]
  [Obsolete]
  private async Task RestartMission() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    await Task.Run(() => {
      _comPort.setWPCurrent(_comPort.MAV.sysid, _comPort.MAV.compid, 0);
      _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.MISSION_START,
          0, 0, 0, 0, 0, 0, 0);
    });
    Log("Restart mission");
  }

  [RelayCommand]
  [Obsolete]
  private async Task ResumeMission() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.MISSION_START,
            0, 0, 0, 0, 0, 0, 0));
    Log("Resume mission");
  }

  [RelayCommand]
  private void RawSensorView() => Log("Raw sensor view toggled.");

  [RelayCommand]
  private void Joystick() => Log("Joystick mapping is under Setup > Joystick.");

  [RelayCommand]
  private void ShowMessage() => Log("Message");

  [RelayCommand]
  private void ClearTrack() => Log("Track cleared.");

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

  // RelayOptions Low/High/Toggle. spec = "idx:low|high|toggle"
  [RelayCommand]
  [Obsolete]
  private async Task SetRelay(string spec) {
    if (!Connected || spec == null) {
      return;
    }
    var parts = spec.Split(':');
    if (parts.Length != 2 || !int.TryParse(parts[0], out int idx) || idx >= _relayState.Length) {
      return;
    }
    bool on = parts[1] switch {
      "low" => false,
      "high" => true,
      _ => !_relayState[idx],
    };
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

  // ---- HUD right-click options (mirror MP contextMenuStripHud) ----
  [ObservableProperty]
  private bool _hudShowIcons = true;

  [ObservableProperty]
  private bool _hudRussian;

  [ObservableProperty]
  private int _hudBatteryCells;

  [ObservableProperty]
  private string _hudGroundColor = "";

  [ObservableProperty]
  private int _hudColumn;

  [ObservableProperty]
  private int _mapColumn = 2;

  [ObservableProperty]
  private double _navBearing;

  [ObservableProperty]
  private string _hudCustomText = "";

  // HUD Items submenu toggles
  [ObservableProperty]
  private bool _hudHeading = true;

  [ObservableProperty]
  private bool _hudSpeed = true;

  [ObservableProperty]
  private bool _hudAlt = true;

  [ObservableProperty]
  private bool _hudConnection = true;

  [ObservableProperty]
  private bool _hudXTrack = true;

  [ObservableProperty]
  private bool _hudRollPitch = true;

  [ObservableProperty]
  private bool _hudGps = true;

  [ObservableProperty]
  private bool _hudBattery = true;

  [ObservableProperty]
  private bool _hudBattery2 = true;

  [ObservableProperty]
  private bool _hudEkf = true;

  [ObservableProperty]
  private bool _hudVibe = true;

  [ObservableProperty]
  private bool _hudPrearm = true;

  [ObservableProperty]
  private bool _hudAoa = true;

  private readonly System.Collections.Generic.HashSet<string> _hudUserFields = new();

  [RelayCommand]
  private void ToggleHudIcons() => HudShowIcons = !HudShowIcons;

  [RelayCommand]
  private void ToggleRussianHud() => HudRussian = !HudRussian;

  [RelayCommand]
  private async Task SetGroundColor() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var picker = new Avalonia.Controls.ColorPicker {
      Color = string.IsNullOrEmpty(HudGroundColor)
          ? Avalonia.Media.Color.Parse("#9BB824")
          : Avalonia.Media.Color.Parse(HudGroundColor),
    };
    var ok = new Avalonia.Controls.Button {
      Content = "OK",
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    };
    var dlg = new Avalonia.Controls.Window {
      Title = "Ground Color",
      Width = 340,
      Height = 420,
      WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
      Content = new Avalonia.Controls.StackPanel {
        Margin = new Avalonia.Thickness(10),
        Spacing = 8,
        Children = { picker, ok },
      },
    };
    ok.Click += (_, _) => dlg.Close(true);
    if (await dlg.ShowDialog<bool>(top)) {
      var c = picker.Color;
      HudGroundColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
  }

  [RelayCommand]
  private void SwapHudMap() => (HudColumn, MapColumn) = (MapColumn, HudColumn);

  [RelayCommand]
  private void SetAspectRatio() =>
      Log("Set Aspect Ratio: HUD video aspect (used with video overlay; video not ported).");

  [RelayCommand]
  private void HudVideoNotPorted() =>
      Log("HUD video/GStreamer capture is not ported in this cross-platform build.");

  // MP "User Items": checkbox per numeric CurrentState field; checked ones render on the HUD.
  [RelayCommand]
  private async Task HudUserItems() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var panel = new Avalonia.Controls.WrapPanel {
      Orientation = Avalonia.Layout.Orientation.Vertical,
      MaxHeight = 520,
    };
    foreach (var p in StatusProps) {
      if (!IsNumber(p.PropertyType)) {
        continue;
      }
      var cb = new Avalonia.Controls.CheckBox {
        Content = p.Name,
        IsChecked = _hudUserFields.Contains(p.Name),
        Width = 180,
        FontSize = 11,
      };
      var name = p.Name;
      cb.IsCheckedChanged += (_, _) => {
        if (cb.IsChecked == true) {
          _hudUserFields.Add(name);
        } else {
          _hudUserFields.Remove(name);
        }
      };
      panel.Children.Add(cb);
    }
    var dlg = new Avalonia.Controls.Window {
      Title = "HUD User Items",
      Width = 900,
      Height = 600,
      WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
      Content = new Avalonia.Controls.ScrollViewer {
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        Content = panel,
      },
    };
    await dlg.ShowDialog(top);
  }

  private static bool IsNumber(Type t) {
    t = Nullable.GetUnderlyingType(t) ?? t;
    return t == typeof(float) || t == typeof(double) || t == typeof(int) || t == typeof(uint)
        || t == typeof(short) || t == typeof(ushort) || t == typeof(long) || t == typeof(ulong)
        || t == typeof(byte) || t == typeof(sbyte) || t == typeof(decimal);
  }

  [RelayCommand]
  private async Task SetBatteryCells() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var box = new Avalonia.Controls.NumericUpDown {
      Minimum = 0,
      Maximum = 14,
      Value = HudBatteryCells,
      Width = 120,
    };
    var ok = new Avalonia.Controls.Button {
      Content = "OK",
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    };
    var dlg = new Avalonia.Controls.Window {
      Title = "Battery Cell Count",
      Width = 220,
      Height = 140,
      WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
      Content = new Avalonia.Controls.StackPanel {
        Margin = new Avalonia.Thickness(10),
        Spacing = 8,
        Children = {
          new Avalonia.Controls.TextBlock { Text = "Cells (0 = auto):" },
          box,
          ok,
        },
      },
    };
    ok.Click += (_, _) => dlg.Close(true);
    var res = await dlg.ShowDialog<bool>(top);
    if (res) {
      HudBatteryCells = (int)(box.Value ?? 0);
    }
  }

}

public record AuxRow(int Channel, ParamField Field);

public class RelayChannel {
  public RelayChannel(int index) {
    Index = index;
  }

  public int Index { get; }
  public string Label => $"Relay {Index + 1}";
}

public partial class ServoOut : ObservableObject {
  public ServoOut(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private int _value;
}

public partial class StatusItem : ObservableObject {
  public StatusItem(string name) {
    Name = name;
  }

  public string Name { get; }

  [ObservableProperty]
  private string _value = "";
}

public partial class CheckItem : ObservableObject {
  public CheckItem(string name, bool manual = false) {
    Name = name;
    Manual = manual;
  }

  [ObservableProperty]
  private string _name;

  // Manual items have no telemetry value; the user toggles the checkbox themselves.
  public bool Manual { get; }

  [ObservableProperty]
  private string _value = "";

  [ObservableProperty]
  private bool _ok;

  public void Set(string value, bool ok) {
    Value = value;
    Ok = ok;
  }
}

public partial class ServoChannel : ObservableObject {
  public ServoChannel(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private int _min = 1100;

  [ObservableProperty]
  private int _max = 1900;

  public bool Toggled { get; set; }
}
