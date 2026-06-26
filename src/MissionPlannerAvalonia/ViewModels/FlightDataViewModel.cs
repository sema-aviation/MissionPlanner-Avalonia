using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.ViewModels;

public partial class FlightDataViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private readonly TlogPlayer _tlog = new();
  private readonly LuaScriptHost _lua = new();

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

  [ObservableProperty]
  private string _statusText = "";

  private int _lastMsgCount = -1;

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
    for (int r = 0; r < 16; r++) {
      ServoRelayItems.Add(new RelayChannel(r));
    }
    // MP tabAuxFunction = 7 auxOptions (RCx_OPTION combos)
    for (int ch = 7; ch <= 13; ch++) {
      AuxOptions.Add(new AuxRow(ch, new ParamField($"RC{ch}_OPTION")));
    }

    InitQuickItems();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();

    _tlog.Packet += OnTlogPacket;
    _tlog.Progress += OnTlogProgress;
    _lua.Output += OnLuaOutput;
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

    // extra HUD draw items
    WindDir = cs.wind_dir;
    WindVel = cs.wind_vel;
    Aoa = cs.AOA;
    Ssa = cs.SSA;
    XTrackError = cs.xtrack_error;
    TurnRate = cs.turnrate;
    BatteryVoltage2 = cs.battery_voltage2;
    BatteryRemaining2 = cs.battery_remaining2;
    BatCurrent2 = cs.current2;
    ThrottlePercent = cs.ch3percent;
    Failsafe = cs.failsafe;
    SafetyActive = cs.safetyactive;
    LinkQuality = cs.linkqualitygcs;
    TargetAlt = cs.targetalt;
    TargetSpeed = cs.targetairspeed;

    UpdateQuickItems(cs);

    // Live STATUSTEXT for the Messages tab (mirrors Messagetabtimer; last 200, newest at bottom).
    if (cs.messages != null && cs.messages.Count != _lastMsgCount) {
      _lastMsgCount = cs.messages.Count;
      StatusText = string.Join("\n",
          cs.messages.TakeLast(200).Select(m => $"{m.time:HH:mm:ss}  {m.message?.TrimEnd()}"));
    }

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

  // ---- extra HUD telemetry (new upstream draw items) ----
  [ObservableProperty]
  private double _windDir;

  [ObservableProperty]
  private double _windVel;

  [ObservableProperty]
  private double _aoa;

  [ObservableProperty]
  private double _ssa;

  [ObservableProperty]
  private double _xTrackError;

  [ObservableProperty]
  private double _turnRate;

  [ObservableProperty]
  private double _batteryVoltage2;

  [ObservableProperty]
  private int _batteryRemaining2;

  [ObservableProperty]
  private double _batCurrent2;

  [ObservableProperty]
  private double _throttlePercent;

  [ObservableProperty]
  private bool _failsafe;

  [ObservableProperty]
  private bool _safetyActive;

  [ObservableProperty]
  private double _linkQuality;

  [ObservableProperty]
  private double _targetAlt;

  [ObservableProperty]
  private double _targetSpeed;

  // ---- Quick tab: grid of QuickView cells (mirrors MP quickView controls) ----
  public ObservableCollection<QuickItem> QuickItems { get; } = new();

  // Default fields + colours mirror the previous static Quick tab layout.
  private static readonly (string field, string color)[] QuickDefaults = {
    ("alt", "#D197F8"),
    ("groundspeed", "#FE842E"),
    ("current", "#FF605B"),
    ("airspeed", "#00FF53"),
    ("verticalspeed", "#FEFE56"),
    ("DistToHome", "#00FFFC"),
  };

  private void InitQuickItems() {
    var cs = _comPort.MAV?.cs;
    for (int i = 0; i < QuickDefaults.Length; i++) {
      var key = $"quickView{i + 1}";
      string field = Settings.Instance.ContainsKey(key) && !string.IsNullOrEmpty(Settings.Instance[key])
          ? Settings.Instance[key]
          : QuickDefaults[i].field;
      var item = new QuickItem(field, QuickDefaults[i].color);
      item.Desc = DescFor(cs, field);
      QuickItems.Add(item);
    }
  }

  private static string DescFor(MissionPlanner.CurrentState? cs, string field) {
    try {
      return cs?.GetNameandUnit(field) ?? field;
    } catch {
      return field;
    }
  }

  private void UpdateQuickItems(MissionPlanner.CurrentState cs) {
    foreach (var item in QuickItems) {
      var pi = StatusProps.FirstOrDefault(p => p.Name == item.Field);
      if (pi == null) {
        continue;
      }
      try {
        object? v = pi.GetValue(cs);
        item.Number = v switch {
          bool b => b ? 1 : 0,
          IConvertible c => Convert.ToDouble(c, CultureInfo.InvariantCulture),
          _ => item.Number,
        };
      } catch {
        // leave the previous value if this field can't be read/converted this tick.
      }
    }
  }

  // Called by the view after the field picker; persists the chosen field via Settings.
  public void SetQuickField(QuickItem item, string field) {
    item.Field = field;
    item.Desc = DescFor(_comPort.MAV?.cs, field);
    int idx = QuickItems.IndexOf(item);
    if (idx >= 0) {
      Settings.Instance[$"quickView{idx + 1}"] = field;
    }
  }

  // Numeric (or bool) CurrentState fields the picker offers, sorted by description.
  public System.Collections.Generic.List<(string name, string desc)> QuickFieldList() {
    var cs = _comPort.MAV?.cs;
    var list = new System.Collections.Generic.List<(string, string)>();
    foreach (var p in StatusProps) {
      var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
      if (!IsNumber(t) && t != typeof(bool)) {
        continue;
      }
      list.Add((p.Name, DescFor(cs, p.Name)));
    }
    list.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
    return list;
  }

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

  // ---- Scripts tab (Lua via MoonSharp) ----
  [ObservableProperty]
  private string _scriptStatus = "No Script Running";

  [ObservableProperty]
  private string _selectedScript = "None";

  [ObservableProperty]
  private bool _redirectOutput = true;

  // MP shows Edit/Run/Abort only after a script is selected.
  [ObservableProperty]
  private bool _scriptSelected;

  [ObservableProperty]
  private string _scriptEditorText = "";

  [ObservableProperty]
  private string _scriptOutput = "";

  private string? _selectedScriptPath;

  private void OnLuaOutput(string text) {
    Dispatcher.UIThread.Post(() => {
      ScriptOutput += text + "\n";
      if (!_lua.IsRunning) {
        ScriptStatus = "No Script Running";
      }
    });
  }

  [RelayCommand]
  private async Task SelectScript() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(
        new Avalonia.Platform.Storage.FilePickerOpenOptions {
          Title = "Select Lua script",
          AllowMultiple = false,
          FileTypeFilter = new[] {
            new Avalonia.Platform.Storage.FilePickerFileType("Lua script") { Patterns = new[] { "*.lua" } },
          },
        });
    if (files.Count > 0) {
      _selectedScriptPath = files[0].TryGetLocalPath();
      SelectedScript = files[0].Name;
      ScriptSelected = true;
      ScriptStatus = "Selected " + files[0].Name;
    }
  }

  [RelayCommand]
  private async Task RunScript() {
    if (_lua.IsRunning) {
      ScriptStatus = "Script already running";
      return;
    }
    ScriptOutput = "";
    ScriptStatus = "Running";
    var code = ScriptEditorText;
    if (!string.IsNullOrWhiteSpace(code)) {
      await _lua.RunAsync(code);
    } else if (_selectedScriptPath != null) {
      await _lua.RunFileAsync(_selectedScriptPath);
    } else {
      ScriptStatus = "No script selected";
    }
  }

  [RelayCommand]
  private void AbortScript() {
    _lua.Abort();
    ScriptStatus = "Aborting…";
  }

  [RelayCommand]
  private void EditScript() {
    if (_selectedScriptPath != null && System.IO.File.Exists(_selectedScriptPath)) {
      ScriptEditorText = System.IO.File.ReadAllText(_selectedScriptPath);
      ScriptStatus = "Editing " + SelectedScript;
    } else {
      ScriptStatus = "No script file to edit";
    }
  }

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
    return files.Count > 0 ? files[0].TryGetLocalPath() : null;
  }

  private static async Task<string?> PickSaveAsync(string title, string ext) {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(
        new Avalonia.Platform.Storage.FilePickerSaveOptions {
          Title = title,
          DefaultExtension = ext,
        });
    return file?.TryGetLocalPath();
  }

  // ---- Telemetry Logs tab (TlogPlayer) ----
  private string? _tlogPath;

  [ObservableProperty]
  private double _tlogProgress;

  [ObservableProperty]
  private string _tlogPositionText = "0.00 %";

  [ObservableProperty]
  private string _playbackSpeedText = "x 1.0";

  [ObservableProperty]
  private bool _tlogPlaying;

  private bool _seekFromUi = true;

  [RelayCommand]
  private async Task LoadTlog() {
    var path = await PickFileAsync("Open telemetry log", "*.tlog", "Telemetry log");
    if (path == null) {
      return;
    }
    try {
      await Task.Run(() => _tlog.Open(path));
      _tlogPath = path;
      LogStatus = $"Loaded {System.IO.Path.GetFileName(path)} ({_tlog.Duration:hh\\:mm\\:ss}).";
    } catch (Exception ex) {
      LogStatus = "Open failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private void PlayPauseTlog() {
    if (!_tlog.IsOpen) {
      LogStatus = "Load a telemetry log first.";
      return;
    }
    if (_tlog.IsPlaying) {
      _tlog.Pause();
      TlogPlaying = false;
    } else {
      _tlog.Play();
      TlogPlaying = true;
    }
  }

  [RelayCommand]
  private void SetTlogSpeed(string factor) {
    if (double.TryParse(factor, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var s)) {
      _tlog.Speed = s;
      PlaybackSpeedText = $"x {_tlog.Speed:0.0##}";
    }
  }

  // Slider drag -> seek (guarded so playback-driven Progress updates don't loop back).
  partial void OnTlogProgressChanged(double value) {
    if (_seekFromUi && _tlog.IsOpen) {
      _tlog.Seek(value / 100.0);
    }
  }

  private void OnTlogProgress(double fraction) {
    Dispatcher.UIThread.Post(() => {
      _seekFromUi = false;
      TlogProgress = fraction * 100.0;
      _seekFromUi = true;
      TlogPositionText = $"{fraction * 100.0:0.00} %";
      if (fraction >= 1.0) {
        TlogPlaying = false;
      }
    });
  }

  // Apply tlog packets onto cs so the HUD + map replay the flight (mirrors the live path).
  private void OnTlogPacket(MAVLink.MAVLinkMessage msg) {
    var cs = _comPort.MAV?.cs;
    if (cs == null) {
      return;
    }
    switch (msg.msgid) {
      case (uint)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT
          when msg.data is MAVLink.mavlink_global_position_int_t gpi:
        if (gpi.lat != 0 || gpi.lon != 0) {
          cs.lat = gpi.lat / 1e7;
          cs.lng = gpi.lon / 1e7;
        }
        cs.alt = gpi.alt / 1000.0f;
        break;
      case (uint)MAVLink.MAVLINK_MSG_ID.ATTITUDE
          when msg.data is MAVLink.mavlink_attitude_t att:
        cs.roll = att.roll * 180f / (float)Math.PI;
        cs.pitch = att.pitch * 180f / (float)Math.PI;
        cs.yaw = att.yaw * 180f / (float)Math.PI;
        break;
      case (uint)MAVLink.MAVLINK_MSG_ID.VFR_HUD
          when msg.data is MAVLink.mavlink_vfr_hud_t hud:
        cs.groundspeed = hud.groundspeed;
        cs.airspeed = hud.airspeed;
        break;
    }
  }

  [RelayCommand]
  private async Task TlogToKml() {
    if (_tlogPath == null) {
      LogStatus = "Load a telemetry log first.";
      return;
    }
    var outPath = await PickSaveAsync("Save KML", "kml");
    if (outPath == null) {
      return;
    }
    try {
      await Task.Run(() => TlogPlayer.ExportKml(_tlogPath, outPath));
      LogStatus = "Wrote " + System.IO.Path.GetFileName(outPath);
    } catch (Exception ex) {
      LogStatus = "Export failed: " + ex.Message;
    }
  }

  // ---- DataFlash Logs tab (DataFlashLog) ----
  private string? _binPath;

  [RelayCommand]
  private async Task ReviewLog() {
    var path = await PickFileAsync("Open dataflash log", "*.bin", "DataFlash log");
    if (path == null) {
      return;
    }
    _binPath = path;
    try {
      await Views.LogBrowseWindow.OpenWith(path);
      LogStatus = $"Opened {System.IO.Path.GetFileName(path)} in Log Browser.";
    } catch (Exception ex) {
      LogStatus = "Open failed: " + ex.Message;
    }
  }

  private async Task<string?> EnsureBinAsync() {
    if (_binPath != null) {
      return _binPath;
    }
    _binPath = await PickFileAsync("Open dataflash log", "*.bin", "DataFlash log");
    return _binPath;
  }

  [RelayCommand]
  private async Task CreateKmlGpx() {
    var bin = await EnsureBinAsync();
    if (bin == null) {
      return;
    }
    try {
      await Task.Run(() => {
        var baseName = System.IO.Path.ChangeExtension(bin, null);
        DataFlashLog.ExportKml(bin, baseName + ".kml");
        DataFlashLog.ExportGpx(bin, baseName + ".gpx");
      });
      LogStatus = "Wrote KML + GPX next to the log.";
    } catch (Exception ex) {
      LogStatus = "Export failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task ConvertBinToLog() {
    var bin = await EnsureBinAsync();
    if (bin == null) {
      return;
    }
    var outPath = await PickSaveAsync("Save text log", "log");
    if (outPath == null) {
      return;
    }
    try {
      await Task.Run(() => DataFlashLog.ConvertBinToLog(bin, outPath));
      LogStatus = "Wrote " + System.IO.Path.GetFileName(outPath);
    } catch (Exception ex) {
      LogStatus = "Convert failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task CreateMatlab() {
    var bin = await EnsureBinAsync();
    if (bin == null) {
      return;
    }
    try {
      await Task.Run(() => DataFlashLog.ExportMatlab(bin, s => Dispatcher.UIThread.Post(() => LogStatus = s)));
      LogStatus = "Matlab export complete.";
    } catch (Exception ex) {
      LogStatus = "Matlab export failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task AutoAnalysis() {
    var bin = await EnsureBinAsync();
    if (bin == null) {
      return;
    }
    LogStatus = "Analyzing…";
    try {
      var summary = await Task.Run(() =>
          Services.LogAnalyzer.Format(Services.LogAnalyzer.Analyze(bin)));
      LogStatus = summary;
    } catch (Exception ex) {
      LogStatus = "Analysis failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task GeoReferenceImages() {
    // Geo-tag photos against the flight log's CAM/GPS messages (mirrors GeoRef/georefimage.cs).
    LogStatus = "Geo Reference: matching images to GPS by EXIF timestamp...";
    await Views.GeoRefWindow.OpenWith();
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
  [Obsolete]
  private async Task ResetPosition() {
    Tilt = 0;
    Pan = 0;
    Roll2 = 0;
    if (!Connected) {
      return;
    }
    // Mount-neutral: explicitly command (0,0,0) rather than only zeroing local fields.
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.DO_MOUNT_CONTROL,
            0, 0, 0, 0, 0, 0, 2));
    Log("Mount reset to neutral");
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

  // Full upstream BUT_resumemis: reprogram the mission keeping home + do-commands, skipping the
  // already-flown nav waypoints before the resume point; for copter, GUIDED+arm+takeoff then AUTO.
  [RelayCommand]
  [Obsolete]
  private async Task ResumeMission() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    if (!await Services.Dialogs.MessageShowAgain("Resume Mission",
        "Warning: this will reprogram your mission, arm and issue a takeoff command (copter).",
        "resumemission")) {
      return;
    }
    var cs = _comPort.MAV.cs;
    int last = cs.lastautowp == -1 ? 1 : cs.lastautowp;
    var s = await Services.Dialogs.InputBox("Resume at", "Resume mission at waypoint #", last.ToString());
    if (!int.TryParse(s, out int lastwpno)) {
      return;
    }
    Log("Resuming mission…");
    await Task.Run(() => {
      var lastwpdata = _comPort.getWP((ushort)lastwpno);
      var cmds = new System.Collections.Generic.List<MissionPlanner.Utilities.Locationwp>();
      var wpcount = _comPort.getWPCount();
      for (ushort a = 0; a < wpcount; a++) {
        var wp = _comPort.getWP(a);
        if (a < lastwpno && a != 0) {
          if (wp.id != (ushort)MAVLink.MAV_CMD.TAKEOFF && wp.id < (ushort)MAVLink.MAV_CMD.LAST) {
            continue;
          }
          if (wp.id > (ushort)MAVLink.MAV_CMD.DO_LAST) {
            continue;
          }
        }
        cmds.Add(wp);
      }
      ushort wpno = 0;
      _comPort.setWPTotal((ushort)cmds.Count);
      foreach (var loc in cmds) {
        if (_comPort.setWP(loc, wpno, (MAVLink.MAV_FRAME)loc.frame)
            != MAVLink.MAV_MISSION_RESULT.MAV_MISSION_ACCEPTED) {
          return;
        }
        wpno++;
      }
      _comPort.setWPACK();
      _comPort.setWPCurrent(_comPort.MAV.sysid, _comPort.MAV.compid, 1);

      if (cs.firmware == MissionPlanner.ArduPilot.Firmwares.ArduCopter2) {
        if (!SpinUntil(() => { _comPort.setMode("GUIDED"); return cs.mode.Equals("Guided", StringComparison.OrdinalIgnoreCase); })) {
          return;
        }
        if (!SpinUntil(() => { _comPort.doARM(true); return cs.armed; })) {
          return;
        }
        if (!SpinUntil(() => {
          _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.TAKEOFF,
              0, 0, 0, 0, 0, 0, lastwpdata.alt);
          return cs.alt >= lastwpdata.alt - 2;
        })) {
          return;
        }
      }
      _comPort.setMode("AUTO");
    });
    Log("Resume mission");
  }

  // Retry an action once a second until it reports success, giving up after 30 s (mirrors the
  // upstream timeout loops in BUT_resumemis).
  private static bool SpinUntil(Func<bool> tryStep) {
    for (int i = 0; i < 30; i++) {
      if (tryStep()) {
        return true;
      }
      System.Threading.Thread.Sleep(1000);
    }
    return false;
  }

  [RelayCommand]
  private void RawSensorView() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    var win = new Views.RawSensorWindow();
    if (top != null) {
      win.Show(top);
    } else {
      win.Show();
    }
  }

  [RelayCommand]
  private void Joystick() => Log("Joystick mapping is under Setup > Joystick.");

  // Upstream BUT_SendMSG: prompt for text and send_text(severity 5) so it lands in the vehicle log.
  [RelayCommand]
  private async Task ShowMessage() {
    if (!_comPort.BaseStream.IsOpen) {
      return;
    }
    var txt = await Services.Dialogs.InputBox("Enter Message", "Enter Message to be logged");
    if (string.IsNullOrEmpty(txt)) {
      return;
    }
    try {
      _comPort.send_text(5, txt);
    } catch {
      await Services.Dialogs.Alert("Error", "No response from vehicle.");
    }
  }

  // Auto-pan follow + clear-track are applied on the MapView by the view code-behind.
  [ObservableProperty]
  private bool _autoPan;

  public event System.Action? TrackClearRequested;

  [RelayCommand]
  private void ClearTrack() {
    TrackClearRequested?.Invoke();
    Log("Track cleared.");
  }

  // ---- Map context menu (mirrors FlightData contextMenuStripMap) ----
  // Called from FlightDataView code-behind with the lat/lng under the cursor (MapView.LastClickLatLng).

  private byte Sysid => _comPort.MAV.sysid;
  private byte Compid => _comPort.MAV.compid;

  private static byte FrameByte(string frame) => frame switch {
    "Absolute" => (byte)MAVLink.MAV_FRAME.GLOBAL,
    "Terrain" => (byte)MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT,
    _ => (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
  };

  [Obsolete]
  private async Task GuidedGoto(double lat, double lng, double alt, string frame) {
    var wp = new MissionPlanner.Utilities.Locationwp {
      id = (ushort)MAVLink.MAV_CMD.WAYPOINT,
      lat = lat,
      lng = lng,
      alt = (float)alt,
      frame = FrameByte(frame),
    };
    await Task.Run(() => _comPort.setGuidedModeWP(wp));
    Log($"Fly to {lat:0.000000},{lng:0.000000} @ {alt} m ({frame})");
  }

  [Obsolete]
  public async Task FlyToHere(double lat, double lng) {
    if (!Connected) {
      return;
    }
    var r = await Services.Dialogs.AltInputBox("Fly To Here", 50, "Relative");
    if (r != null) {
      await GuidedGoto(lat, lng, r.Value.Alt, r.Value.Frame);
    }
  }

  [Obsolete]
  public async Task FlyToCoords() {
    if (!Connected) {
      return;
    }
    var s = await Services.Dialogs.InputBox("Fly To Coords", "Enter lat;lng;alt");
    var p = s?.Split(';');
    if (p is not { Length: >= 3 }
        || !double.TryParse(p[0], out var lat)
        || !double.TryParse(p[1], out var lng)
        || !double.TryParse(p[2], out var alt)) {
      return;
    }
    await GuidedGoto(lat, lng, alt, "Relative");
  }

  [Obsolete]
  public async Task PointCameraHere(double lat, double lng) {
    if (!Connected) {
      return;
    }
    var s = await Services.Dialogs.InputBox("Point Camera Here", "Enter Target Alt (relative to home)", "0");
    if (!float.TryParse(s, out var alt)) {
      return;
    }
    await Task.Run(() => _comPort.doCommandInt(Sysid, Compid, MAVLink.MAV_CMD.DO_SET_ROI,
        0, 0, 0, 0, (int)(lat * 1e7), (int)(lng * 1e7), alt,
        frame: MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT));
    Log($"Camera ROI -> {lat:0.000000},{lng:0.000000}");
  }

  [Obsolete]
  public async Task TriggerCameraNow() {
    if (!Connected) {
      return;
    }
    await Task.Run(() => _comPort.setDigicamControl(true));
    Log("Camera triggered");
  }

  public async Task SetHomeHere(double lat, double lng) {
    if (!Connected) {
      return;
    }
    if (!await Services.Dialogs.Confirm("Set Home", "Set home to the clicked location?")) {
      return;
    }
    await Task.Run(() => _comPort.doCommand(Sysid, Compid, MAVLink.MAV_CMD.DO_SET_HOME,
        0, 0, 0, 0, (float)lat, (float)lng, 0));
    Log($"Home set -> {lat:0.000000},{lng:0.000000}");
  }

  [Obsolete]
  public async Task SetEkfOriginHere(double lat, double lng) {
    if (!Connected) {
      return;
    }
    var s = MissionPlanner.Utilities.srtm.getAltitude(lat, lng);
    float alt = s.currenttype == MissionPlanner.Utilities.srtm.tiletype.valid
        ? (float)s.alt
        : _comPort.MAV.cs.altasl;
    var go = new MAVLink.mavlink_set_gps_global_origin_t {
      latitude = (int)(lat * 1e7),
      longitude = (int)(lng * 1e7),
      altitude = (int)(alt * 1000),
      target_system = Sysid,
    };
    await Task.Run(() => _comPort.sendPacket(go, Sysid, Compid));
    Log($"EKF origin set -> {lat:0.000000},{lng:0.000000}");
  }

  [Obsolete]
  public async Task TakeOffHere() {
    if (!Connected) {
      return;
    }
    var s = await Services.Dialogs.InputBox("Takeoff", "Enter Takeoff Alt (m)", "5");
    if (!float.TryParse(s, out var alt)) {
      return;
    }
    await Task.Run(() => {
      _comPort.setMode("GUIDED");
      _comPort.doCommand(Sysid, Compid, MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, alt);
    });
    Log($"Takeoff to {alt} m");
  }

  public async Task JumpToTag() {
    if (!Connected) {
      return;
    }
    var s = await Services.Dialogs.InputBox("Jump To Tag", "Tag Id (0-65535)");
    if (!ushort.TryParse(s, out var tag)) {
      return;
    }
    await Task.Run(() => _comPort.doCommand(Sysid, Compid, MAVLink.MAV_CMD.DO_JUMP_TAG,
        tag, 0, 0, 0, 0, 0, 0));
    Log($"Jump to tag {tag}");
  }

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

  // Upstream BUT_Homealt: toggle the HUD home-altitude display offset (NOT a DO_SET_HOME command).
  // Off → show altitude relative to home; pressing again clears the offset.
  [RelayCommand]
  private void SetHome() {
    if (!Connected) {
      Messages += "Not connected.\n";
      return;
    }
    var cs = _comPort.MAV.cs;
    cs.altoffsethome = cs.altoffsethome != 0 ? 0 : -(float)(cs.HomeAlt / CurrentState.multiplieralt);
    Log(cs.altoffsethome != 0 ? "Home-alt offset on" : "Home-alt offset off");
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

  private readonly bool[] _relayState = new bool[16];

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

  // ---- HUD video (VideoControl in a popup) ----
  private MissionPlannerAvalonia.Controls.VideoControl? _video;
  private Views.VideoPopupWindow? _videoWindow;

  private Views.VideoPopupWindow EnsureVideoWindow() {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (_videoWindow == null) {
      // Fresh control per window: an Avalonia control can't be re-parented after the
      // previous popup closed, so we don't cache it across windows.
      _video = new MissionPlannerAvalonia.Controls.VideoControl();
      _videoWindow = new Views.VideoPopupWindow(_video);
      _videoWindow.Closed += (_, _) => { _video?.Stop(); _videoWindow = null; };
      if (top != null) {
        _videoWindow.Show(top);
      } else {
        _videoWindow.Show();
      }
    } else {
      _videoWindow.Activate();
    }
    return _videoWindow;
  }

  private static async Task<string?> PromptTextAsync(string title, string label, string initial) {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var box = new Avalonia.Controls.TextBox { Text = initial, MinWidth = 360 };
    var ok = new Avalonia.Controls.Button {
      Content = "OK",
      HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
    };
    var dlg = new Avalonia.Controls.Window {
      Title = title,
      Width = 420,
      Height = 150,
      WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
      Content = new Avalonia.Controls.StackPanel {
        Margin = new Avalonia.Thickness(10),
        Spacing = 8,
        Children = {
          new Avalonia.Controls.TextBlock { Text = label },
          box,
          ok,
        },
      },
    };
    ok.Click += (_, _) => dlg.Close(box.Text);
    return await dlg.ShowDialog<string?>(top);
  }

  [RelayCommand]
  private async Task SetVideoSource(string preset) {
    var win = EnsureVideoWindow();
    if (!win.Video.IsAvailable) {
      Log(win.Video.Status);
      win.UpdateStatus();
      return;
    }
    string initial = preset switch {
      "mjpeg" => "http://192.168.0.1:8080/?action=stream",
      "gstreamer" => "rtsp://192.168.144.25:8554/main.264",
      "herelink" => "rtsp://192.168.43.1:8554/fpv_stream",
      "camera" => "v4l2:///dev/video0",
      _ => "",
    };
    var mrl = await PromptTextAsync("Video source", "Stream URL / MRL:", initial);
    if (string.IsNullOrWhiteSpace(mrl)) {
      return;
    }
    win.Video.Play(mrl);
    win.UpdateStatus();
    Log(win.Video.Status);
  }

  [RelayCommand]
  private async Task RecordVideo() {
    var win = EnsureVideoWindow();
    var outPath = await PickSaveAsync("Record video to", "ts");
    if (outPath == null) {
      return;
    }
    win.Video.TryRecord(outPath);
    win.UpdateStatus();
    Log(win.Video.Status);
  }

  [RelayCommand]
  private void StopVideo() {
    if (_videoWindow == null) {
      return;
    }
    _videoWindow.Video.Stop();
    _videoWindow.UpdateStatus();
    Log(_videoWindow.Video.Status);
  }

  [RelayCommand]
  private void SetAspectRatio() =>
      Log("Set Aspect Ratio: VideoControl stretches to fit the popup; no fixed aspect override.");

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

  // ---- Transponder tab (uAvionix ADS-B out) ----
  // Upstream MP drives this through MAVLinkInterface.uAvionixADSBControl (squawk + mode
  // state bits + flight id + ident), not individual params, so we mirror that here.
  [ObservableProperty]
  private int _squawk = 1200;

  [ObservableProperty]
  private string _flightId = "";

  [ObservableProperty]
  private bool _modeA;

  [ObservableProperty]
  private bool _modeC;

  [ObservableProperty]
  private bool _modeS;

  [ObservableProperty]
  private bool _modeEs;

  [ObservableProperty]
  private string _transponderStatus = "Not connected";

  // UAVIONIX_ADSB_OUT_CONTROL_STATE bits: 8=IDENT, 16=ModeA, 32=ModeC, 64=ModeS, 128=1090ES.
  private void SendTransponder(byte extraState) {
    if (!Connected) {
      TransponderStatus = "Not connected";
      return;
    }
    byte state = (byte)(extraState
        | (ModeA ? 16 : 0)
        | (ModeC ? 32 : 0)
        | (ModeS ? 64 : 0)
        | (ModeEs ? 128 : 0));
    var id = System.Text.Encoding.ASCII.GetBytes((FlightId ?? "").PadRight(8).Substring(0, 8));
    _comPort.uAvionixADSBControl(int.MaxValue, (ushort)Squawk, state, 0, id, 0);
  }

  [RelayCommand]
  private async Task ConnectTransponder() {
    if (!Connected) {
      TransponderStatus = "Not connected";
      return;
    }
    TransponderStatus = "Connecting…";
    await Task.Run(() =>
        _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.SET_MESSAGE_INTERVAL,
            (float)MAVLink.MAVLINK_MSG_ID.UAVIONIX_ADSB_OUT_STATUS, 1000000.0f, 0, 0, 0, 0, 0));
    TransponderStatus = "Requested transponder status stream";
  }

  [RelayCommand]
  private void TransponderStandby() {
    ModeA = ModeC = ModeS = ModeEs = false;
    SendTransponder(0);
    TransponderStatus = "STBY";
  }

  [RelayCommand]
  private void TransponderOn() {
    ModeA = true;
    ModeC = false;
    ModeS = true;
    ModeEs = true;
    SendTransponder(0);
    TransponderStatus = "ON";
  }

  [RelayCommand]
  private void TransponderAlt() {
    ModeA = ModeC = ModeS = ModeEs = true;
    SendTransponder(0);
    TransponderStatus = "ALT";
  }

  [RelayCommand]
  private void TransponderIdent() {
    SendTransponder(8);
    TransponderStatus = "IDENT";
  }

  partial void OnSquawkChanged(int value) => SendTransponder(0);

}

// One Quick-tab cell: a live cs field shown as a big value + small label (mirrors MP QuickView).
public partial class QuickItem : ObservableObject {
  public QuickItem(string field, string color) {
    _field = field;
    Color = color;
  }

  [ObservableProperty]
  private string _field;

  [ObservableProperty]
  private string _desc = "";

  [ObservableProperty]
  private double _number;

  public string Color { get; }

  public Avalonia.Media.IBrush Brush =>
      new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(Color));
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
