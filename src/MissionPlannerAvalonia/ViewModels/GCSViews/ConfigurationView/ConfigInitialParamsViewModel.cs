using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigInitialParamsViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<string> BatteryTypes { get; } =
      new() { "LiPo", "LiPoHV", "LiIon" };

  public ObservableCollection<ParamCompareRow> Results { get; } = new();

  [ObservableProperty]
  private string _propSize = "9";

  [ObservableProperty]
  private string _cellCount = "4";

  [ObservableProperty]
  private string _cellMax = "4.2";

  [ObservableProperty]
  private string _cellMin = "3.3";

  [ObservableProperty]
  private string _batteryType = "LiPo";

  [ObservableProperty]
  private bool _tMotor;

  [ObservableProperty]
  private bool _suggested;

  [ObservableProperty]
  private bool _hasResults;

  [ObservableProperty]
  private string _status = "";

  public string DocsUrl => "https://ardupilot.org/copter/docs/tuning-process-instructions.html";

  partial void OnBatteryTypeChanged(string value) {
    switch (value) {
      case "LiPo":
        CellMax = "4.2";
        CellMin = "3.3";
        break;
      case "LiPoHV":
        CellMax = "4.35";
        CellMin = "3.3";
        break;
      case "LiIon":
        CellMax = "4.1";
        CellMin = "2.8";
        break;
      default:
        CellMax = "4.2";
        CellMin = "3.3";
        break;
    }
  }

  private static double RoundTo(double value, int precision) {
    if (precision >= 0) {
      return Math.Round(value, precision);
    }
    var p = (int)Math.Pow(10, Math.Abs(precision));
    value += 5 * p / 10;
    return Math.Round(value - value % p, 0);
  }

  private static double Parse(string s) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

  [RelayCommand]
  private void Calculate() {
    var propSize = Parse(PropSize);
    var battCells = Parse(CellCount);
    var battCellMax = Parse(CellMax);
    var battCellMin = Parse(CellMin);

    if (propSize <= 0) {
      Status = "Prop size must be larger than zero.";
      return;
    }
    if (battCells < 1) {
      Status = "Battery cell count must be at least 1.";
      return;
    }

    var atcAccelYMax = Math.Max(8000, RoundTo(-900 * propSize + 36000, -2));
    var acroYawP = 0.5 * atcAccelYMax / 4500;
    var atcAccelPMax = Math.Max(10000, RoundTo(
        -2.613267 * Math.Pow(propSize, 3) + 343.39216 * Math.Pow(propSize, 2) -
        15083.7121 * propSize + 235771, -2));
    var atcAccelRMax = atcAccelPMax;
    var insGyroFilter = Math.Max(20, Math.Round(289.22 * Math.Pow(propSize, -0.838), 0));

    var fltHalf = Math.Max(10, insGyroFilter / 2);
    var atcRatPitFltd = fltHalf;
    var atcRatPitFlte = 0.0;
    var atcRatPitFltt = fltHalf;
    var atcRatRllFltd = fltHalf;
    var atcRatRllFlte = 0.0;
    var atcRatRllFltt = fltHalf;
    var atcRatYawFltd = 0.0;
    var atcRatYawFlte = 2.0;
    var atcRatYawFltt = fltHalf;

    var atcThrMixMan = 0.1;
    var insAccelFilter = 10.0;
    var motThstExpo = Math.Min(Math.Round(0.15686 * Math.Log(propSize) + 0.23693, 2), 0.80);
    var motThstHover = 0.2;

    var battArmVolt = (battCells - 1) * 0.1 + (battCellMin + 0.3) * battCells;
    var battCrtVolt = (battCellMin + 0.2) * battCells;
    var battLowVolt = (battCellMin + 0.3) * battCells;
    var motBatVoltMax = battCellMax * battCells;
    var motBatVoltMin = battCellMin * battCells;

    if (TMotor) {
      motThstExpo = 0.2;
    }

    var atc = "ATC";
    var mot = "MOT";
    if (_comPort.MAV.cs.firmware == Firmwares.ArduPlane) {
      atc = "Q_A";
      mot = "Q_M";
    }

    var np = new Dictionary<string, double>();
    np.Add("ACRO_YAW_P", acroYawP);
    if (_comPort.MAV.param.ContainsKey(atc + "_ACCEL_P_MAX")) {
      np.Add(atc + "_ACCEL_P_MAX", atcAccelPMax);
      np.Add(atc + "_ACCEL_R_MAX", atcAccelRMax);
      np.Add(atc + "_ACCEL_Y_MAX", atcAccelYMax);
    } else {
      np.Add(atc + "_ACC_P_MAX", atcAccelPMax / 100.0);
      np.Add(atc + "_ACC_R_MAX", atcAccelRMax / 100.0);
      np.Add(atc + "_ACC_Y_MAX", atcAccelYMax / 100.0);
    }

    var major = _comPort.MAV.cs.version?.Major ?? 0;
    if (major == 4) {
      np.Add(atc + "_RAT_PIT_FLTD", atcRatPitFltd);
      np.Add(atc + "_RAT_PIT_FLTE", atcRatPitFlte);
      np.Add(atc + "_RAT_PIT_FLTT", atcRatPitFltt);
      np.Add(atc + "_RAT_RLL_FLTD", atcRatRllFltd);
      np.Add(atc + "_RAT_RLL_FLTE", atcRatRllFlte);
      np.Add(atc + "_RAT_RLL_FLTT", atcRatRllFltt);
      np.Add(atc + "_RAT_YAW_FLTD", atcRatYawFltd);
      np.Add(atc + "_RAT_YAW_FLTE", atcRatYawFlte);
      np.Add(atc + "_RAT_YAW_FLTT", atcRatYawFltt);
    } else {
      np.Add(atc + "_RAT_PIT_FILT", atcRatPitFltd);
      np.Add(atc + "_RAT_RLL_FILT", atcRatRllFltd);
      np.Add(atc + "_RAT_YAW_FILT", atcRatYawFlte);
    }

    np.Add(atc + "_THR_MIX_MAN", atcThrMixMan);
    np.Add("INS_ACCEL_FILTER", insAccelFilter);
    np.Add("INS_GYRO_FILTER", insGyroFilter);
    np.Add(mot + "_THST_EXPO", motThstExpo);
    np.Add(mot + "_THST_HOVER", motThstHover);
    np.Add("BATT_ARM_VOLT", battArmVolt);
    np.Add("BATT_CRT_VOLT", battCrtVolt);
    np.Add("BATT_LOW_VOLT", battLowVolt);
    np.Add(mot + "_BAT_VOLT_MAX", motBatVoltMax);
    np.Add(mot + "_BAT_VOLT_MIN", motBatVoltMin);

    if (TMotor) {
      np.Add(mot + "_PWM_MIN", 1100);
      np.Add(mot + "_PWM_MAX", 1940);
    }

    if (Suggested && major == 4 && _comPort.MAV.cs.firmware != Firmwares.ArduPlane) {
      np.Add("BATT_FS_CRT_ACT", 1);
      np.Add("BATT_FS_LOW_ACT", 2);
      np.Add("FENCE_ACTION", 3);
      np.Add("FENCE_ALT_MAX", 120);
      np.Add("FENCE_ENABLE", 1);
      np.Add("FENCE_RADIUS", 150);
      np.Add("FENCE_TYPE", 7);
    }

    Results.Clear();
    foreach (var kv in np) {
      var cur = _comPort.MAV.param.ContainsKey(kv.Key)
          ? _comPort.MAV.param[kv.Key].Value.ToString("0.######", CultureInfo.InvariantCulture)
          : "n/a";
      Results.Add(new ParamCompareRow {
        Name = kv.Key,
        Current = cur,
        NewValue = kv.Value.ToString("0.######", CultureInfo.InvariantCulture),
        Value = kv.Value,
      });
    }
    HasResults = Results.Count > 0;
    Status = "Review the values below, then Write to FC.";
  }

  [RelayCommand]
  [Obsolete]
  private async Task WriteToFc() {
    if (_comPort.BaseStream?.IsOpen != true) {
      Status = "Not connected.";
      return;
    }
    var fail = 0;
    foreach (var row in Results) {
      try {
        var ok = await Task.Run(() => _comPort.setParam(row.Name, row.Value, true));
        if (!ok) {
          fail++;
        }
      } catch {
        fail++;
      }
    }
    Status = fail == 0
        ? "Initial Parameters successfully updated. Check parameters before flight! " +
          "After test flight set ATC_THR_MIX_MAN to 0.5."
        : fail + " parameter(s) failed to write.";
  }
}

public partial class ParamCompareRow : ObservableObject {
  public string Name { get; set; } = "";
  public string Current { get; set; } = "";
  public string NewValue { get; set; } = "";
  public double Value { get; set; }
}
