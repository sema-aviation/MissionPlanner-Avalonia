using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAccelCalibrationViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _inCalibrate;
  private MAVLink.ACCELCAL_VEHICLE_POS _pos;
  private int _sub1 = -1;
  private int _sub2 = -1;

  public string Title => "Accelerometer Calibration";

  public string Instructions =>
      "Full calibration: press Calibrate Accel, then place the vehicle in each orientation as "
      + "prompted (level, on its LEFT side, on its RIGHT side, nose DOWN, nose UP, on its BACK), "
      + "pressing Click when Done after each. Calibrate Level trims the level position only. "
      + "Simple Accel performs a single-position calibration.";

  [ObservableProperty]
  private string _accelButtonText = "Calibrate Accel";

  [ObservableProperty]
  private bool _accelButtonEnabled = true;

  [ObservableProperty]
  private string _levelButtonText = "Calibrate Level";

  [ObservableProperty]
  private string _simpleButtonText = "Simple Accel Cal";

  [ObservableProperty]
  private string _userMessage = "";

  [ObservableProperty]
  private string _log = "";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  private async Task CalibrateAccel() {
    if (_inCalibrate) {
      try {
        await Task.Run(() => _comPort.sendPacket(
            new MAVLink.mavlink_command_long_t {
              param1 = (float)_pos,
              command = (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS,
            },
            _comPort.sysidcurrent,
            _comPort.compidcurrent));
      } catch {
        AppendLog("Command failed.");
      }

      return;
    }

    if (!IsConnected) {
      AppendLog("Not connected — connect a vehicle first.");
      return;
    }

    UserMessage = "";
    Log = "";
    try {
      AppendLog("Sending accel calibration command…");
      bool ok = await Task.Run(() => _comPort.doCommand(
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
          0, 0, 0, 0, 1, 0, 0));
      if (!ok) {
        AppendLog("Command failed.");
        return;
      }

      _inCalibrate = true;
      _sub1 = _comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.STATUSTEXT,
          ReceivedPacket,
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent);
      _sub2 = _comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.COMMAND_LONG,
          ReceivedPacket,
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent);
      AccelButtonText = "Click when Done";
    } catch (Exception ex) {
      _inCalibrate = false;
      AppendLog("Failed to start: " + ex.Message);
    }
  }

  private bool ReceivedPacket(MAVLink.MAVLinkMessage arg) {
    if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.STATUSTEXT) {
      var message = Encoding.ASCII.GetString(arg.ToStructure<MAVLink.mavlink_statustext_t>().text)
          .TrimEnd('\0');
      UpdateUserMessage(message);

      if (message.ToLower().Contains("calibration successful")
          || message.ToLower().Contains("calibration failed")) {
        Dispatcher.UIThread.Post(() => {
          AccelButtonText = "Done";
          AccelButtonEnabled = false;
        });
        StopCalibration();
      }
    }

    if (arg.msgid == (uint)MAVLink.MAVLINK_MSG_ID.COMMAND_LONG) {
      var message = arg.ToStructure<MAVLink.mavlink_command_long_t>();
      if (message.command == (ushort)MAVLink.MAV_CMD.ACCELCAL_VEHICLE_POS) {
        _pos = (MAVLink.ACCELCAL_VEHICLE_POS)message.param1;
        UpdateUserMessage("Please place vehicle " + _pos);
      }
    }

    return true;
  }

  private void UpdateUserMessage(string message) {
    if (message.ToLower().Contains("place vehicle") || message.ToLower().Contains("calibration")) {
      Dispatcher.UIThread.Post(() => {
        UserMessage = message;
        AppendLog(message);
      });
    }
  }

  [RelayCommand]
  private async Task CalibrateLevel() {
    if (!IsConnected) {
      AppendLog("Not connected — connect a vehicle first.");
      return;
    }

    try {
      AppendLog("Sending level command…");
      bool ok = await Task.Run(() => _comPort.doCommand(
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
          0, 0, 0, 0, 2, 0, 0));
      LevelButtonText = ok ? "Completed" : "Calibrate Level";
      AppendLog(ok ? "Level calibration accepted." : "Command failed.");
    } catch (Exception ex) {
      AppendLog("Failed to level: " + ex.Message);
    }
  }

  [RelayCommand]
  private async Task SimpleAccel() {
    if (!IsConnected) {
      AppendLog("Not connected — connect a vehicle first.");
      return;
    }

    try {
      AppendLog("Sending simple accelerometer calibration command…");
      bool ok = await Task.Run(() => _comPort.doCommand(
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
          0, 0, 0, 0, 4, 0, 0));
      SimpleButtonText = ok ? "Completed" : "Simple Accel Cal";
      AppendLog(ok ? "Simple calibration accepted." : "Command failed.");
    } catch (Exception ex) {
      AppendLog("Failed: " + ex.Message);
    }
  }

  private void StopCalibration() {
    _inCalibrate = false;
    if (_sub1 != -1) {
      _comPort.UnSubscribeToPacketType(_sub1);
      _sub1 = -1;
    }
    if (_sub2 != -1) {
      _comPort.UnSubscribeToPacketType(_sub2);
      _sub2 = -1;
    }
  }

  public void Deactivate() => StopCalibration();

  private void AppendLog(string line) {
    void Do() => Log += (Log.Length > 0 ? "\n" : "") + line;
    if (Dispatcher.UIThread.CheckAccess()) {
      Do();
    } else {
      Dispatcher.UIThread.Post(Do);
    }
  }

  public void Dispose() => StopCalibration();
}

public partial class ConfigCompassViewModel : ParamPageBase, IDisposable {
  private int _sub1 = -1;
  private int _sub2 = -1;

  // Live mag-sample feed for the MagCalSphere visualisation (wired up by ConfigCompassView).
  // OnMagSample carries the body-frame direction vector streamed in MAG_CAL_PROGRESS; the view
  // forwards each sample to the orthographic point cloud. OnMagSphereClear resets it on Start.
  public event Action<double, double, double>? OnMagSample;
  public event Action? OnMagSphereClear;

  public ConfigCompassViewModel() {
    Title = "Compass";
    Intro = "Compass configuration and onboard magnetometer calibration.";

    F("COMPASS_EXTERNAL", "combo");
    F("COMPASS_EXTERN2", "combo");
    F("COMPASS_EXTERN3", "combo");

    F("COMPASS_ORIENT", "combo");
    F("COMPASS_ORIENT2", "combo");
    F("COMPASS_ORIENT3", "combo");

    F("COMPASS_PRIMARY", "combo");
    F("COMPASS_AUTODEC", "bool");
    F("COMPASS_CAL_FIT", "combo");

    ReloadDeclination();
    LoadCompassFlags();
    BuildCompassList();
  }

  public ObservableCollection<CompassPrioRow> Compasses { get; } = new();

  [ObservableProperty]
  private bool _useCompass1;

  [ObservableProperty]
  private bool _useCompass2;

  [ObservableProperty]
  private bool _useCompass3;

  [ObservableProperty]
  private bool _learnOffsets;

  [ObservableProperty]
  private string _compassStatus = "";

  private bool _loadingFlags;

  partial void OnUseCompass1Changed(bool value) =>
      WriteFlag(new[] { "COMPASS_USE", "COMPASS1_USE" }, value);

  partial void OnUseCompass2Changed(bool value) =>
      WriteFlag(new[] { "COMPASS_USE2", "COMPASS2_USE" }, value);

  partial void OnUseCompass3Changed(bool value) =>
      WriteFlag(new[] { "COMPASS_USE3", "COMPASS3_USE" }, value);

  partial void OnLearnOffsetsChanged(bool value) =>
      WriteFlag(new[] { "COMPASS_LEARN" }, value);

  private async void WriteFlag(string[] names, bool value) {
    if (_loadingFlags || !IsConnected) {
      return;
    }

    var p = comPort.MAV.param[names];
    if (p == null) {
      return;
    }

    try {
      await Task.Run(() => comPort.setParam(
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent, p.Name, value ? 1 : 0));
    } catch {
    }
  }

  private void LoadCompassFlags() {
    var p = comPort.MAV?.param;
    if (p == null) {
      return;
    }

    _loadingFlags = true;
    UseCompass1 = p[new[] { "COMPASS_USE", "COMPASS1_USE" }]?.Value > 0;
    UseCompass2 = p[new[] { "COMPASS_USE2", "COMPASS2_USE" }]?.Value > 0;
    UseCompass3 = p[new[] { "COMPASS_USE3", "COMPASS3_USE" }]?.Value > 0;
    LearnOffsets = p[new[] { "COMPASS_LEARN" }]?.Value > 0;
    _loadingFlags = false;
  }

  private void BuildCompassList() {
    Compasses.Clear();

    var param = comPort.MAV?.param;
    if (param == null) {
      return;
    }

    var orientOptions = OrientOptions(param);

    var list = param
        .Where(a => a.Name.StartsWith("COMPASS") && a.Name.Contains("DEV_ID") && a.Value != 0)
        .OrderBy(a => a.Name)
        .Select(a => new CompassDevice(a.Name, (uint)a.Value, param))
        .ToList();

    var prio = param
        .Where(a => a.Name.StartsWith("COMPASS_PRIO") && a.Value != 0)
        .OrderBy(a => a.Name)
        .Select(a => new CompassDevice(a.Name, (uint)a.Value, param))
        .ToList();

    bool anyMissing = false;
    foreach (var p in prio) {
      if (p.DevID == 0 || list.Any(b => b.DevID == p.DevID)) {
        p.Missing = false;
      } else {
        p.Missing = true;
        anyMissing = true;
      }
    }

    list = list.Where(a => !prio.Any(b => b.DevID == a.DevID)).ToList();
    list.InsertRange(0, prio);

    int i = 1;
    foreach (var d in list) {
      Compasses.Add(new CompassPrioRow {
        Priority = i++,
        DevID = d.DevID,
        BusType = d.BusType,
        Bus = d.Bus,
        Address = d.Address,
        DevType = d.DevType,
        Missing = d.Missing,
        External = d.External,
        Orientation = FormatOrient(d.OrientValue, orientOptions),
      });
    }

    CompassStatus = anyMissing
        ? "Your compass configuration has changed, please review the missing compass."
        : "";
  }

  private static List<KeyValuePair<int, string>>? OrientOptions(MAVLink.MAVLinkParamList param) {
    try {
      var op = param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT" }];
      if (op == null) {
        return null;
      }

      return ParameterMetaDataRepository.GetParameterOptionsInt(
          op.Name, AppState.comPort.MAV.cs.firmware.ToString());
    } catch {
      return null;
    }
  }

  private static string FormatOrient(int? value, List<KeyValuePair<int, string>>? options) {
    if (value == null) {
      return "";
    }

    var hit = options?.FirstOrDefault(o => o.Key == value.Value);
    return hit is { Value: { } text } && text.Length > 0 ? text : value.Value.ToString();
  }

  [RelayCommand]
  private async Task MoveUp(CompassPrioRow? row) {
    if (row == null) {
      return;
    }

    int idx = Compasses.IndexOf(row);
    if (idx <= 0) {
      return;
    }

    Compasses.Move(idx, idx - 1);
    Renumber();
    await UpdateFirst3();
  }

  [RelayCommand]
  private async Task MoveDown(CompassPrioRow? row) {
    if (row == null) {
      return;
    }

    int idx = Compasses.IndexOf(row);
    if (idx < 0 || idx >= Compasses.Count - 1) {
      return;
    }

    Compasses.Move(idx, idx + 1);
    Renumber();
    await UpdateFirst3();
  }

  [RelayCommand]
  private async Task RemoveMissing() {
    for (int i = Compasses.Count - 1; i >= 0; i--) {
      if (Compasses[i].Missing) {
        Compasses.RemoveAt(i);
      }
    }

    Renumber();
    await UpdateFirst3();
  }

  [RelayCommand]
  private async Task Reboot() {
    if (!IsConnected) {
      return;
    }

    try {
      await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN,
          1, 0, 0, 0, 0, 0, 0));
    } catch (Exception ex) {
      MagResult = ex.Message;
    }
  }

  private void Renumber() {
    for (int i = 0; i < Compasses.Count; i++) {
      Compasses[i].Priority = i + 1;
    }
  }

  private async Task UpdateFirst3() {
    if (!IsConnected) {
      return;
    }

    await SetParamSafe("COMPASS_PRIO1_ID", Compasses.Count >= 1 ? Compasses[0].DevID : 0);
    await SetParamSafe("COMPASS_PRIO2_ID", Compasses.Count >= 2 ? Compasses[1].DevID : 0);
    await SetParamSafe("COMPASS_PRIO3_ID", Compasses.Count >= 3 ? Compasses[2].DevID : 0);

    MagResult = "Compass priority updated. A reboot is required for it to take effect.";
    BuildCompassList();
  }

  [ObservableProperty]
  private double _prog1;

  [ObservableProperty]
  private double _prog2;

  [ObservableProperty]
  private double _prog3;

  [ObservableProperty]
  private string _magResult = "";

  [ObservableProperty]
  private bool _calRunning;

  [ObservableProperty]
  private double _declinationDeg;

  [ObservableProperty]
  private double _largeVehicleHeading;

  [RelayCommand]
  private async Task StartMagCal() {
    if (!IsConnected) {
      MagResult = "Connect to a vehicle first.";
      return;
    }

    try {
      await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_START_MAG_CAL,
          0, 1, 1, 0, 0, 0, 0));
    } catch (Exception ex) {
      MagResult = "Failed to start MAG CAL: " + ex.Message;
      return;
    }

    Prog1 = 0;
    Prog2 = 0;
    Prog3 = 0;
    MagResult = "";
    OnMagSphereClear?.Invoke();

    if (_sub1 == -1) {
      _sub1 = comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS,
          ReceivedPacket,
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent);
    }
    if (_sub2 == -1) {
      _sub2 = comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT,
          ReceivedPacket,
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent);
    }

    CalRunning = true;
  }

  [RelayCommand]
  private async Task AcceptMagCal() {
    if (!IsConnected) {
      return;
    }

    try {
      await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL,
          0, 0, 1, 0, 0, 0, 0));
    } catch (Exception ex) {
      MagResult = ex.Message;
    }

    StopCal();
  }

  [RelayCommand]
  private async Task CancelMagCal() {
    if (!IsConnected) {
      return;
    }

    try {
      await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL,
          0, 0, 1, 0, 0, 0, 0));
    } catch (Exception ex) {
      MagResult = ex.Message;
    }

    StopCal();
  }

  [RelayCommand]
  private async Task LargeVehicleMagCal() {
    if (!IsConnected) {
      MagResult = "Connect to a vehicle first.";
      return;
    }

    try {
      bool ok = await Task.Run(() => comPort.doCommand(
          comPort.MAV.sysid,
          comPort.MAV.compid,
          MAVLink.MAV_CMD.FIXED_MAG_CAL_YAW,
          (float)LargeVehicleHeading, 0, 0, 0, 0, 0, 0));
      MagResult = ok
          ? "Large-vehicle (fixed yaw) calibration completed."
          : "Command failed. GPS lock is required.";
    } catch (Exception ex) {
      MagResult = ex.Message;
    }
  }

  [RelayCommand]
  private async Task QuickPixhawk() {
    await SetParamSafe("COMPASS_EXTERNAL", 0);
    await SetParamSafe("COMPASS_ORIENT", 0);
    MagResult = "Set internal compass orientation for Pixhawk/Cube.";
  }

  [RelayCommand]
  private async Task WriteDeclination() {
    if (!IsConnected) {
      return;
    }

    double rad = DeclinationDeg * Math.PI / 180.0;
    bool ok = await SetParamSafe("COMPASS_DEC", rad);
    MagResult = ok ? "Declination written." : "Failed to write declination.";
  }

  private async Task<bool> SetParamSafe(string name, double value) {
    if (!IsConnected || !comPort.MAV.param.ContainsKey(name)) {
      return false;
    }

    try {
      return await Task.Run(() => comPort.setParam(
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent, name, value));
    } catch {
      return false;
    }
  }

  private bool ReceivedPacket(MAVLink.MAVLinkMessage packet) {
    if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS) {
      var obj = (MAVLink.mavlink_mag_cal_progress_t)packet.data;
      Dispatcher.UIThread.Post(() => {
        // Feed the primary compass's body-frame direction vector into the sphere visualisation so
        // the operator can see sample coverage build up as the vehicle is rotated (mirrors upstream
        // ProgressReporterSphere.sphere1.AddPoint).
        if (obj.compass_id == 0
            && (obj.direction_x != 0 || obj.direction_y != 0 || obj.direction_z != 0)) {
          OnMagSample?.Invoke(obj.direction_x, obj.direction_y, obj.direction_z);
        }

        if (obj.compass_id == 0) {
          Prog1 = obj.completion_pct;
        }

        if (obj.compass_id == 1) {
          Prog2 = obj.completion_pct;
        }

        if (obj.compass_id == 2) {
          Prog3 = obj.completion_pct;
        }
      });
    } else if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT) {
      var obj = (MAVLink.mavlink_mag_cal_report_t)packet.data;
      if (obj.compass_id == 0 && obj.ofs_x == 0) {
        return true;
      }

      Dispatcher.UIThread.Post(() => {
        if (obj.compass_id == 0) {
          Prog1 = 100;
        }

        if (obj.compass_id == 1) {
          Prog2 = 100;
        }

        if (obj.compass_id == 2) {
          Prog3 = 100;
        }

        MagResult +=
            $"id:{obj.compass_id} x:{obj.ofs_x:0.0} y:{obj.ofs_y:0.0} z:{obj.ofs_z:0.0} "
            + $"fit:{obj.fitness:0.0} {(MAVLink.MAG_CAL_STATUS)obj.cal_status}\n";

        if (obj.autosaved == 1) {
          MagResult += "Calibration saved. Please reboot the autopilot.\n";
          StopCal();
        }
      });
    }

    return true;
  }

  private void ReloadDeclination() {
    if (comPort.MAV.param.ContainsKey("COMPASS_DEC")) {
      DeclinationDeg = comPort.MAV.param["COMPASS_DEC"].Value * 180.0 / Math.PI;
    }
  }

  protected override void OnRefreshed() {
    ReloadDeclination();
    LoadCompassFlags();
    BuildCompassList();
  }

  private void StopCal() {
    CalRunning = false;
    if (_sub1 != -1) {
      comPort.UnSubscribeToPacketType(_sub1);
      _sub1 = -1;
    }
    if (_sub2 != -1) {
      comPort.UnSubscribeToPacketType(_sub2);
      _sub2 = -1;
    }
  }

  public void Deactivate() => StopCal();

  public void Dispose() => StopCal();
}

public partial class CompassPrioRow : ObservableObject {
  [ObservableProperty]
  private int _priority;

  public int DevID { get; init; }
  public string BusType { get; init; } = "";
  public int Bus { get; init; }
  public int Address { get; init; }
  public string DevType { get; init; } = "";
  public bool Missing { get; init; }
  public bool External { get; init; }
  public string Orientation { get; init; } = "";
}

internal sealed class CompassDevice {
  private readonly Device.DeviceStructure _devid;

  public CompassDevice(string paramName, uint id, MAVLink.MAVLinkParamList param) {
    ParamName = paramName;
    _devid = new Device.DeviceStructure(paramName, id);

    var id1 = param[new[] { "COMPASS_DEV_ID", "COMPASS1_DEV_ID" }];
    var id2 = param[new[] { "COMPASS_DEV_ID2", "COMPASS2_DEV_ID" }];
    var id3 = param[new[] { "COMPASS_DEV_ID3", "COMPASS3_DEV_ID" }];

    if (id1 != null && (uint)id1.Value == id) {
      OrientValue = (int?)param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT" }]?.Value;
      External = param[new[] { "COMPASS_EXTERNAL", "COMPASS1_EXTERN" }]?.Value > 0;
    }
    if (id2 != null && (uint)id2.Value == id) {
      OrientValue = (int?)param[new[] { "COMPASS_ORIENT2", "COMPASS2_ORIENT" }]?.Value;
      External = param[new[] { "COMPASS_EXTERN2", "COMPASS2_EXTERN" }]?.Value > 0;
    }
    if (id3 != null && (uint)id3.Value == id) {
      OrientValue = (int?)param[new[] { "COMPASS_ORIENT3", "COMPASS3_ORIENT" }]?.Value;
      External = param[new[] { "COMPASS_EXTERN3", "COMPASS3_EXTERN" }]?.Value > 0;
    }
  }

  public string ParamName { get; }
  public int DevID => (int)_devid.devid;
  public string BusType => _devid.bus_type.ToString().Replace("BUS_TYPE_", "");
  public int Bus => (int)_devid.bus;
  public int Address => (int)_devid.address;
  public bool External { get; }
  public bool Missing { get; set; }
  public int? OrientValue { get; }

  public string DevType {
    get {
      if (_devid.bus_type == Device.BusType.BUS_TYPE_UAVCAN) {
        return "SENSOR_ID#" + _devid.devtype;
      }

      if (ParamName.Contains("COMP")) {
        return _devid.devtypecompass.ToString().Replace("DEVTYPE_", "");
      }

      if (ParamName.Contains("BARO")) {
        return _devid.devtypebaro.ToString().Replace("DEVTYPE_", "");
      }

      if (ParamName.Contains("ASP")) {
        return _devid.devtypeairspd.ToString().Replace("DEVTYPE_", "");
      }

      return _devid.devtypeimu.ToString().Replace("DEVTYPE_", "");
    }
  }
}

public partial class ConfigESCCalibrationViewModel : ParamPageBase {
  public ConfigESCCalibrationViewModel() {
    Title = "ESC Calibration (AC3.3+)";
    Intro = "Configure motor PWM output and run the all-at-once ESC calibration.";

    F("MOT_PWM_TYPE", "combo");
    F("MOT_PWM_MIN");
    F("MOT_PWM_MAX");
    F("MOT_SPIN_ARM");
    F("MOT_SPIN_MIN");
    F("MOT_SPIN_MAX");
  }

  public string Instructions =>
      "DANGER: REMOVE ALL PROPELLERS FIRST.\n"
      + "1. Press Calibrate ESCs (sets ESC_CALIBRATION = 3). Requires AC 3.3+.\n"
      + "2. Disconnect the battery and USB.\n"
      + "3. Re-connect the battery — the autopilot enters the ESC calibration sequence and passes "
      + "the throttle range through to the ESCs.\n"
      + "4. Listen for the ESC confirmation tones, then disconnect and reconnect power normally.";

  [ObservableProperty]
  private string _calButtonText = "Calibrate ESCs";

  [ObservableProperty]
  private string _status = "";

  [RelayCommand]
  private async Task CalibrateEsc() {
    if (!IsConnected) {
      Status = "Connect to a vehicle first.";
      return;
    }

    try {
      bool ok = await Task.Run(() => comPort.setParam(
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent, "ESC_CALIBRATION", 3));
      if (!ok) {
        Status = "Set param error. Please ensure your version is AC 3.3+.";
        return;
      }

      CalButtonText = "Done";
      Status = "ESC_CALIBRATION set. Now power-cycle the vehicle to run the sequence.";
    } catch {
      Status = "Set param error. Please ensure your version is AC 3.3+.";
    }
  }
}

public partial class MotorTestItem : ObservableObject {
  public MotorTestItem(int testOrder, string label, string rotation) {
    TestOrder = testOrder;
    Label = label;
    Rotation = rotation;
  }

  public int TestOrder { get; }
  public string Label { get; }
  public string Rotation { get; }
}

public partial class ConfigMotorTestViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private int _motorMax;

  public ConfigMotorTestViewModel() {
    Title = "Motor Test";
    BuildMotors();
  }

  public string Title { get; }

  public string Instructions =>
      "DANGER: REMOVE ALL PROPELLERS. Verifies motor order and direction. Each test spins one "
      + "motor at the set throttle % for the set duration. Test all in sequence steps through "
      + "every motor (A, B, C…) one after another.";

  public ObservableCollection<MotorTestItem> Motors { get; } = new();

  [ObservableProperty]
  private string _frameClass = "";

  [ObservableProperty]
  private string _frameType = "";

  [ObservableProperty]
  private int _throttlePercent = 8;

  [ObservableProperty]
  private int _durationSec = 2;

  [ObservableProperty]
  private string _status = "";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  private void TestMotor(MotorTestItem? item) {
    if (item != null) {
      RunTest(item.TestOrder, ThrottlePercent, DurationSec);
    }
  }

  [RelayCommand]
  private void TestAllSequence() {
    RunTest(1, ThrottlePercent, DurationSec, _motorMax);
  }

  [RelayCommand]
  private void StopAll() {
    for (int i = 1; i <= _motorMax; i++) {
      RunTest(i, 0, 0);
    }
  }

  [RelayCommand]
  private async Task SetSpinArm() {
    if (!Require("MOT_SPIN_ARM")) {
      return;
    }

    if (ThrottlePercent >= 20) {
      Status = "Throttle percent above 20, too high.";
      return;
    }

    double value = (ThrottlePercent + 2) / 100.0;
    bool ok = await SetParam("MOT_SPIN_ARM", value);
    Status = ok ? $"MOT_SPIN_ARM set to {value:0.00}." : "Failed to set MOT_SPIN_ARM.";
  }

  [RelayCommand]
  private async Task SetSpinMin() {
    if (!Require("MOT_SPIN_MIN")) {
      return;
    }

    if (ThrottlePercent >= 20) {
      Status = "Throttle percent above 20, too high.";
      return;
    }

    double value = ((int)(_comPort.MAV.param["MOT_SPIN_MIN"].Value * 100) + 3) / 100.0;
    bool ok = await SetParam("MOT_SPIN_MIN", value);
    Status = ok ? $"MOT_SPIN_MIN set to {value:0.00}." : "Failed to set MOT_SPIN_MIN.";
  }

  private async void RunTest(int motor, int speed, int time, int motorCount = 0) {
    if (!IsConnected) {
      Status = "Connect to a vehicle first.";
      return;
    }

    try {
#pragma warning disable CS0612
      bool ok = await Task.Run(() => _comPort.doMotorTest(
          motor,
          MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PERCENT,
          speed,
          time,
          motorCount));
#pragma warning restore CS0612
      Status = ok ? "" : "Command was denied by the autopilot.";
    } catch (Exception ex) {
      Status = "Failed to test motor: " + ex.Message;
    }
  }

  private bool Require(string name) {
    if (!IsConnected) {
      Status = "Connect to a vehicle first.";
      return false;
    }

    if (!_comPort.MAV.param.ContainsKey(name)) {
      Status = $"param {name} missing.";
      return false;
    }

    return true;
  }

  private async Task<bool> SetParam(string name, double value) {
    try {
      return await Task.Run(() => _comPort.setParam(
          (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, name, value));
    } catch {
      return false;
    }
  }

  private void BuildMotors() {
    Motors.Clear();
    _motorMax = GetMotorMax(out var layout);

    for (int a = 1; a <= _motorMax; a++) {
      string label = "Test motor " + (char)((a - 1) + 'A');
      string rotation = "";
      if (layout != null) {
        foreach (var motor in layout) {
          if (motor.TestOrder == a) {
            label = "Test motor " + (char)((a - 1) + 'A') + "  (Motor " + motor.Number + ")";
            if (motor.Rotation != "?" && motor.Rotation.Length > 0) {
              rotation = motor.Rotation;
            }
          }
        }
      }

      Motors.Add(new MotorTestItem(a, label, rotation));
    }
  }

  private int GetMotorMax(out List<LayoutMotor>? layout) {
    layout = null;
    int motorMax = 8;

    if (_comPort.MAV.aptype == MAVLink.MAV_TYPE.GROUND_ROVER
        || _comPort.MAV.aptype == MAVLink.MAV_TYPE.SURFACE_BOAT) {
      return 4;
    }

    bool enable = _comPort.MAV.param.ContainsKey("FRAME")
        || _comPort.MAV.param.ContainsKey("Q_FRAME_TYPE")
        || _comPort.MAV.param.ContainsKey("FRAME_TYPE");
    if (!enable) {
      return motorMax;
    }

    if (TrySetFrame("FRAME_CLASS", "FRAME_TYPE", ref layout)
        || TrySetFrame("Q_FRAME_CLASS", "Q_FRAME_TYPE", ref layout)) {
      if (layout != null && layout.Count > 0) {
        return layout.Count;
      }
    }

    var type = MAVLink.MAV_TYPE.QUADROTOR;
    if (_comPort.MAV.param.ContainsKey("Q_FRAME_CLASS")) {
      var value = (int)_comPort.MAV.param["Q_FRAME_CLASS"].Value;
      type = value switch {
        2 or 5 => MAVLink.MAV_TYPE.HEXAROTOR,
        3 or 4 => MAVLink.MAV_TYPE.OCTOROTOR,
        6 => MAVLink.MAV_TYPE.HELICOPTER,
        7 => MAVLink.MAV_TYPE.TRICOPTER,
        _ => MAVLink.MAV_TYPE.QUADROTOR,
      };
    } else if (_comPort.MAV.param.ContainsKey("FRAME")
        || _comPort.MAV.param.ContainsKey("FRAME_TYPE")) {
      type = _comPort.MAV.aptype;
    }

    motorMax = type switch {
      MAVLink.MAV_TYPE.TRICOPTER => 4,
      MAVLink.MAV_TYPE.QUADROTOR => 4,
      MAVLink.MAV_TYPE.HEXAROTOR => 6,
      MAVLink.MAV_TYPE.OCTOROTOR => 8,
      MAVLink.MAV_TYPE.HELICOPTER => 0,
      MAVLink.MAV_TYPE.DODECAROTOR => 12,
      _ => motorMax,
    };

    return motorMax;
  }

  private bool TrySetFrame(string classParam, string typeParam, ref List<LayoutMotor>? layout) {
    if (!_comPort.MAV.param.ContainsKey(classParam) || !_comPort.MAV.param.ContainsKey(typeParam)) {
      return false;
    }

    var fw = _comPort.MAV.cs.firmware.ToString();
    int frameClass = (int)_comPort.MAV.param[classParam].Value;
    int frameType = (int)_comPort.MAV.param[typeParam].Value;

    try {
      var list = ParameterMetaDataRepository.GetParameterOptionsInt(classParam, fw);
      var hit = list?.FirstOrDefault(i => i.Key == frameClass);
      if (hit is { Value: { } cv }) {
        FrameClass = "Class: " + cv;
      }
    } catch {
    }

    try {
      var list = ParameterMetaDataRepository.GetParameterOptionsInt(typeParam, fw);
      var hit = list?.FirstOrDefault(i => i.Key == frameType);
      if (hit is { Value: { } tv }) {
        FrameType = "Type: " + tv;
      }
    } catch {
    }

    layout = LookupLayout(frameClass, frameType);
    return true;
  }

  private static List<LayoutMotor>? LookupLayout(int frameClass, int frameType) {
    try {
      string? file = FindLayoutFile();
      if (file == null) {
        return null;
      }

      using var doc = JsonDocument.Parse(File.ReadAllText(file));
      var root = doc.RootElement;
      if (!root.TryGetProperty("layouts", out var layouts)) {
        return null;
      }

      foreach (var layout in layouts.EnumerateArray()) {
        if (layout.GetProperty("Class").GetInt32() == frameClass
            && layout.GetProperty("Type").GetInt32() == frameType) {
          var motors = new List<LayoutMotor>();
          foreach (var m in layout.GetProperty("motors").EnumerateArray()) {
            motors.Add(new LayoutMotor {
              Number = m.GetProperty("Number").GetInt32(),
              TestOrder = m.GetProperty("TestOrder").GetInt32(),
              Rotation = m.TryGetProperty("Rotation", out var r) ? r.GetString() ?? "?" : "?",
            });
          }

          return motors;
        }
      }
    } catch {
    }

    return null;
  }

  private static string? FindLayoutFile() {
    foreach (var dir in new[] {
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory(),
    }) {
      var p = Path.Combine(dir, "APMotorLayout.json");
      if (File.Exists(p)) {
        return p;
      }
    }

    return null;
  }

  internal class LayoutMotor {
    public int Number { get; set; }
    public int TestOrder { get; set; }
    public string Rotation { get; set; } = "?";
  }
}
