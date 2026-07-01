using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigCompassLegacyViewModel : ParamPageBase, IDisposable {
  private const int _thresholdOfsRed = 600;
  private const int _thresholdOfsYellow = 400;

  private int _sub1 = -1;
  private int _sub2 = -1;

  public ConfigCompassLegacyViewModel() {
    Title = "Compass";
    Intro = "Legacy fixed-offset compass configuration and onboard magnetometer calibration.";

    F("COMPASS_USE", "bool");
    F("COMPASS_EXTERNAL", "bool");
    F("COMPASS_ORIENT", "combo");

    F("COMPASS_USE2", "bool");
    F("COMPASS_EXTERN2", "bool");
    F("COMPASS_ORIENT2", "combo");

    F("COMPASS_USE3", "bool");
    F("COMPASS_EXTERN3", "bool");
    F("COMPASS_ORIENT3", "combo");

    F("COMPASS_PRIMARY", "combo");
    F("COMPASS_AUTODEC", "bool");
    F("COMPASS_LEARN", "bool");
    F("COMPASS_CAL_FIT", "combo");

    LoadOffsets();
    LoadDeclination();
  }

  public ParamField FieldUse1 => Fields[0];
  public ParamField FieldExternal1 => Fields[1];
  public ParamField FieldOrient1 => Fields[2];
  public ParamField FieldUse2 => Fields[3];
  public ParamField FieldExternal2 => Fields[4];
  public ParamField FieldOrient2 => Fields[5];
  public ParamField FieldUse3 => Fields[6];
  public ParamField FieldExternal3 => Fields[7];
  public ParamField FieldOrient3 => Fields[8];
  public ParamField FieldPrimary => Fields[9];
  public ParamField FieldAutoDec => Fields[10];
  public ParamField FieldLearn => Fields[11];
  public ParamField FieldFitness => Fields[12];

  public bool Compass2Visible => comPort.MAV?.param?.ContainsKey("COMPASS_EXTERN2") == true;
  public bool Compass3Visible => comPort.MAV?.param?.ContainsKey("COMPASS_EXTERN3") == true;
  public bool HasPrimary => comPort.MAV?.param?.ContainsKey("COMPASS_PRIMARY") == true;

  [ObservableProperty]
  private string _compass1Offset = "";

  [ObservableProperty]
  private string _compass1Mot = "";

  [ObservableProperty]
  private IBrush _compass1Brush = Brushes.Gray;

  [ObservableProperty]
  private string _compass2Offset = "";

  [ObservableProperty]
  private string _compass2Mot = "";

  [ObservableProperty]
  private IBrush _compass2Brush = Brushes.Gray;

  [ObservableProperty]
  private string _compass3Offset = "";

  [ObservableProperty]
  private string _compass3Mot = "";

  [ObservableProperty]
  private IBrush _compass3Brush = Brushes.Gray;

  [ObservableProperty]
  private double _declinationDeg;

  [ObservableProperty]
  private double _declinationMin;

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
  private double _largeVehicleHeading;

  private static int Absmax(int a, int b, int c) =>
      Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), Math.Abs(c));

  private static IBrush OffsetBrush(int x, int y, int z) {
    if (Absmax(x, y, z) > _thresholdOfsRed) {
      return Brushes.Red;
    }

    if (Absmax(x, y, z) > _thresholdOfsYellow) {
      return Brushes.Gold;
    }

    if (x == 0 && y == 0 && z == 0) {
      return Brushes.Red;
    }

    return Brushes.LimeGreen;
  }

  private void LoadOffsets() {
    var p = comPort.MAV?.param;
    OnPropertyChanged(nameof(Compass2Visible));
    OnPropertyChanged(nameof(Compass3Visible));
    OnPropertyChanged(nameof(HasPrimary));

    if (p == null || !p.ContainsKey("COMPASS_OFS_X")) {
      return;
    }

    LoadOne(p, "", out var o1, out var b1, out var m1);
    Compass1Offset = o1;
    Compass1Brush = b1;
    Compass1Mot = m1;

    if (Compass2Visible) {
      LoadOne(p, "2", out var o2, out var b2, out var m2);
      Compass2Offset = o2;
      Compass2Brush = b2;
      Compass2Mot = m2;
    }

    if (Compass3Visible) {
      LoadOne(p, "3", out var o3, out var b3, out var m3);
      Compass3Offset = o3;
      Compass3Brush = b3;
      Compass3Mot = m3;
    }
  }

  private static void LoadOne(
      MAVLink.MAVLinkParamList p, string idx, out string offset, out IBrush brush, out string mot) {
    int x = (int)p["COMPASS_OFS" + idx + "_X"];
    int y = (int)p["COMPASS_OFS" + idx + "_Y"];
    int z = (int)p["COMPASS_OFS" + idx + "_Z"];

    offset = $"OFFSETS  X: {x},   Y: {y},   Z: {z}";
    brush = OffsetBrush(x, y, z);

    string motKey = "COMPASS_MOT" + idx + "_X";
    if (p.ContainsKey(motKey)) {
      int mx = (int)p["COMPASS_MOT" + idx + "_X"];
      int my = (int)p["COMPASS_MOT" + idx + "_Y"];
      int mz = (int)p["COMPASS_MOT" + idx + "_Z"];
      mot = $"MOT          X: {mx},   Y: {my},   Z: {mz}";
    } else {
      mot = "";
    }
  }

  private void LoadDeclination() {
    var p = comPort.MAV?.param;
    if (p == null || !p.ContainsKey("COMPASS_DEC")) {
      return;
    }

    double dec = p["COMPASS_DEC"].Value * MathHelper.rad2deg;
    DeclinationDeg = (int)dec;
    DeclinationMin = Math.Abs((dec - (int)dec) * 60);
  }

  [RelayCommand]
  private async Task WriteDeclination() {
    if (!IsConnected) {
      return;
    }

    if (!comPort.MAV.param.ContainsKey("COMPASS_DEC")) {
      await Dialogs.Alert(Strings.ERROR, Strings.ErrorFeatureNotEnabled);
      return;
    }

    double dec = DeclinationDeg;
    if (dec < 0) {
      dec -= DeclinationMin / 60.0;
    } else {
      dec += DeclinationMin / 60.0;
    }

    bool ok = await SetParamSafe("COMPASS_DEC", dec * MathHelper.deg2rad);
    if (!ok) {
      await Dialogs.Alert(Strings.ERROR, string.Format(Strings.ErrorSetValueFailed, "COMPASS_DEC"));
    }
  }

  [RelayCommand]
  private async Task StartMagCal() {
    if (!IsConnected) {
      await Dialogs.Alert("Error", "Connect to a vehicle first.");
      return;
    }

    try {
      await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_START_MAG_CAL, 0, 1, 1, 0, 0, 0, 0));
    } catch (Exception ex) {
      await Dialogs.Alert(
          "Error", "Failed to start MAG CAL, check the autopilot is still responding.\n" + ex);
      return;
    }

    Prog1 = 0;
    Prog2 = 0;
    Prog3 = 0;
    MagResult = "";

    if (_sub1 == -1) {
      _sub1 = comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS, ReceivedPacket,
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent);
    }
    if (_sub2 == -1) {
      _sub2 = comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.MAG_CAL_REPORT, ReceivedPacket,
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent);
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
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, 0, 0, 1, 0, 0, 0, 0));
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
          (byte)comPort.sysidcurrent, (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, 0, 0, 1, 0, 0, 0, 0));
    } catch (Exception ex) {
      MagResult = ex.Message;
    }

    StopCal();
  }

  [RelayCommand]
  private async Task LargeVehicleMagCal() {
    if (!IsConnected) {
      await Dialogs.Alert("Error", "Connect to a vehicle first.");
      return;
    }

    var entered = await Dialogs.InputBox(
        "MagCal Yaw",
        "Enter current heading in degrees\nNOTE: gps lock is required. Heading is true, not magnetic",
        LargeVehicleHeading.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if (entered == null
        || !double.TryParse(entered, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double value)) {
      return;
    }

    LargeVehicleHeading = value;
    try {
      bool ok = await Task.Run(() => comPort.doCommand(
          comPort.MAV.sysid, comPort.MAV.compid,
          MAVLink.MAV_CMD.FIXED_MAG_CAL_YAW, (float)value, 0, 0, 0, 0, 0, 0));
      await Dialogs.Alert(
          ok ? Strings.Completed : "Error", ok ? Strings.Completed : Strings.CommandFailed);
    } catch (Exception) {
      await Dialogs.Alert("Error", Strings.CommandFailed);
    }
  }

  [RelayCommand]
  private async Task QuickPixhawk() {
    if (!IsConnected) {
      await Dialogs.Alert("Error", Strings.ErrorNotConnected);
      return;
    }

    try {
      await SetParamSafe("COMPASS_USE", 1);
      await SetParamSafe("COMPASS_USE2", 1);
      await SetParamSafe("COMPASS_USE3", 0);
      await SetParamSafe("COMPASS_EXTERNAL", 1);
      await SetParamSafe("COMPASS_EXTERN2", 0);
      await SetParamSafe("COMPASS_EXTERN3", 0);
      await SetParamSafe("COMPASS_PRIMARY", 0);
      await SetParamSafe("COMPASS_LEARN", 1);

      if (await Dialogs.Confirm(
          "", "is the FW version greater than APM:copter 3.01 or APM:Plane 2.74?")) {
        await SetParamSafe("COMPASS_ORIENT", (int)Rotation.ROTATION_NONE);
      } else {
        await SetParamSafe("COMPASS_ORIENT", (int)Rotation.ROTATION_ROLL_180);
        await SetParamSafe("COMPASS_EXTERNAL", 0);
      }
    } catch (Exception) {
      await Dialogs.Alert(Strings.ERROR, Strings.ErrorSettingParameter);
    }

    ReloadFields();
  }

  [RelayCommand]
  private async Task QuickApm25() {
    if (!IsConnected) {
      await Dialogs.Alert("Error", Strings.ErrorNotConnected);
      return;
    }

    try {
      await SetParamSafe("COMPASS_ORIENT", (int)Rotation.ROTATION_NONE);
      await SetParamSafe("COMPASS_USE", 1);
      await SetParamSafe("COMPASS_USE2", 0);
      await SetParamSafe("COMPASS_USE3", 0);
      await SetParamSafe("COMPASS_EXTERNAL", 0);
      await SetParamSafe("COMPASS_EXTERN2", 0);
      await SetParamSafe("COMPASS_EXTERN3", 0);
      await SetParamSafe("COMPASS_PRIMARY", 0);
      await SetParamSafe("COMPASS_LEARN", 1);
    } catch (Exception) {
      await Dialogs.Alert(Strings.ERROR, Strings.ErrorSettingParameter);
    }

    ReloadFields();
  }

  [RelayCommand]
  private async Task ApmExternal() {
    if (!IsConnected) {
      await Dialogs.Alert("Error", Strings.ErrorNotConnected);
      return;
    }

    try {
      await SetParamSafe("COMPASS_ORIENT", (int)Rotation.ROTATION_ROLL_180);
      await SetParamSafe("COMPASS_EXTERNAL", 1);
      await SetParamSafe("COMPASS_EXTERN2", 0);
      await SetParamSafe("COMPASS_EXTERN3", 0);
      await SetParamSafe("COMPASS_USE", 1);
      await SetParamSafe("COMPASS_USE2", 0);
      await SetParamSafe("COMPASS_USE3", 0);
      await SetParamSafe("COMPASS_PRIMARY", 0);
      await SetParamSafe("COMPASS_LEARN", 1);
    } catch (Exception) {
      await Dialogs.Alert(Strings.ERROR, Strings.ErrorSettingParameter);
    }

    ReloadFields();
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

  private void ReloadFields() {
    foreach (var f in Fields) {
      f.Reload();
    }

    LoadOffsets();
  }

  private bool ReceivedPacket(MAVLink.MAVLinkMessage packet) {
    if (packet.msgid == (byte)MAVLink.MAVLINK_MSG_ID.MAG_CAL_PROGRESS) {
      var obj = (MAVLink.mavlink_mag_cal_progress_t)packet.data;
      Dispatcher.UIThread.Post(() => {
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
          _ = Dialogs.Alert("Mag Cal", "Please reboot the autopilot");
        }
      });
    }

    return true;
  }

  protected override void OnRefreshed() {
    LoadOffsets();
    LoadDeclination();
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
