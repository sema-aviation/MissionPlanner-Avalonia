using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigMountViewModel : ViewModelBase {
  internal readonly MAVLinkInterface comPort = AppState.comPort;
  private const string ParamHead = "MNT_";

  private const string Relay = "Relay";
  private const string Transistor = "Transistor";
  private const string Disable = "Disable";

  private bool _startup = true;
  private readonly string _typeParam;

  public ConfigMountViewModel() {
    var family = comPort.MAV.param.ContainsKey("SERVO1_MIN") ? "SERVO" : "RC";
    var channels = BuildChannelNames(family);

    Tilt = new MountAxisViewModel(this, "Tilt", 7, "ANGMIN_TIL", "ANGMAX_TIL", "RC_IN_TILT",
        -90, 0, 0, 90, channels);
    Roll = new MountAxisViewModel(this, "Roll", 8, "ANGMIN_ROL", "ANGMAX_ROL", "RC_IN_ROLL",
        -90, 0, 0, 90, channels);
    Pan = new MountAxisViewModel(this, "Pan", 6, "ANGMIN_PAN", "ANGMAX_PAN", "RC_IN_PAN",
        -180, 0, 0, 180, channels);

    ShutterChannels.Add(Disable);
    foreach (var c in channels) {
      ShutterChannels.Add(c);
    }
    ShutterChannels.Add(Relay);
    ShutterChannels.Add(Transistor);

    _typeParam = HasParam(ParamHead + "TYPE") ? ParamHead + "TYPE"
        : HasParam("MNT1_TYPE") ? "MNT1_TYPE"
        : ParamHead + "TYPE";
    foreach (var kv in SafeOptions(_typeParam)) {
      MountTypes.Add(new ParamOption(kv.Key, kv.Value));
    }

    Reload();
  }

  public MountAxisViewModel Tilt { get; }
  public MountAxisViewModel Roll { get; }
  public MountAxisViewModel Pan { get; }

  public ObservableCollection<string> ShutterChannels { get; } = new();
  public ObservableCollection<ParamOption> MountTypes { get; } = new();

  [ObservableProperty]
  private string _selectedShutter = "";

  [ObservableProperty]
  private ParamOption? _selectedMountType;

  [ObservableProperty]
  private double _neutralX;

  [ObservableProperty]
  private double _neutralY;

  [ObservableProperty]
  private double _neutralZ;

  [ObservableProperty]
  private double _retractX;

  [ObservableProperty]
  private double _retractY;

  [ObservableProperty]
  private double _retractZ;

  [ObservableProperty]
  private string _status = "";

  private void Reload() {
    _startup = true;

    SelectedShutter = "";
    foreach (var name in comPort.MAV.param.Keys.ToList()) {
      if (!name.EndsWith("_FUNCTION")) {
        continue;
      }
      var ch = name.Replace("_FUNCTION", "");
      switch ((int)Math.Round(comPort.MAV.param[name].Value)) {
        case 6:
          Pan.SetSelectedFunctionSilent(ch);
          break;
        case 7:
          Tilt.SetSelectedFunctionSilent(ch);
          break;
        case 8:
          Roll.SetSelectedFunctionSilent(ch);
          break;
        case 10:
          SelectedShutter = ch;
          break;
      }
    }

    var trigg = GetParam("CAM_TRIGG_TYPE");
    if (trigg.HasValue) {
      if ((int)Math.Round(trigg.Value) == 1) {
        SelectedShutter = Relay;
      } else if ((int)Math.Round(trigg.Value) == 4) {
        SelectedShutter = Transistor;
      }
    }

    Tilt.ReloadFromParams();
    Roll.ReloadFromParams();
    Pan.ReloadFromParams();

    SelectedMountType = MountTypes.FirstOrDefault(
        o => o.Value == (int)Math.Round(GetParam(_typeParam) ?? -1));
    NeutralX = GetParam(ParamHead + "NEUTRAL_X") ?? 0;
    OnPropertyChanged(nameof(NeutralX));
    NeutralY = GetParam(ParamHead + "NEUTRAL_Y") ?? 0;
    OnPropertyChanged(nameof(NeutralY));
    NeutralZ = GetParam(ParamHead + "NEUTRAL_Z") ?? 0;
    OnPropertyChanged(nameof(NeutralZ));
    RetractX = GetParam(ParamHead + "RETRACT_X") ?? 0;
    OnPropertyChanged(nameof(RetractX));
    RetractY = GetParam(ParamHead + "RETRACT_Y") ?? 0;
    OnPropertyChanged(nameof(RetractY));
    RetractZ = GetParam(ParamHead + "RETRACT_Z") ?? 0;
    OnPropertyChanged(nameof(RetractZ));

    _startup = false;
  }

  [Obsolete]
  partial void OnSelectedMountTypeChanged(ParamOption? value) {
    if (_startup || value is null) {
      return;
    }
    SetParam(_typeParam, value.Value);
  }

  [Obsolete]
  partial void OnSelectedShutterChanged(string value) => AxisSelectionChanged();

  [Obsolete]
  partial void OnNeutralXChanged(double value) => WriteNum(ParamHead + "NEUTRAL_X", value);

  [Obsolete]
  partial void OnNeutralYChanged(double value) => WriteNum(ParamHead + "NEUTRAL_Y", value);

  [Obsolete]
  partial void OnNeutralZChanged(double value) => WriteNum(ParamHead + "NEUTRAL_Z", value);

  [Obsolete]
  partial void OnRetractXChanged(double value) => WriteNum(ParamHead + "RETRACT_X", value);

  [Obsolete]
  partial void OnRetractYChanged(double value) => WriteNum(ParamHead + "RETRACT_Y", value);

  [Obsolete]
  partial void OnRetractZChanged(double value) => WriteNum(ParamHead + "RETRACT_Z", value);

  [Obsolete]
  private void WriteNum(string name, double value) {
    if (_startup) {
      return;
    }
    SetParam(name, value);
  }

  [Obsolete]
  internal void AxisSelectionChanged() {
    if (_startup) {
      return;
    }

    try {
      EnsureDisabled(6, Pan.SelectedFunction);
      EnsureDisabled(7, Tilt.SelectedFunction);
      EnsureDisabled(8, Roll.SelectedFunction);

      if (HasParam(ParamHead + "MODE")) {
        SetParam(ParamHead + "MODE", 3);
      }

      UpdateShutter();
      Tilt.ApplyFunction();
      Roll.ApplyFunction();
      Pan.ApplyFunction();
    } catch (Exception ex) {
      Status = "Failed to set param: " + ex.Message;
    }
  }

  [Obsolete]
  private void UpdateShutter() {
    var sel = SelectedShutter;
    if (string.IsNullOrEmpty(sel)) {
      return;
    }

    if (sel == Disable) {
      SetParam("CAM_TRIGG_TYPE", 0);
      EnsureDisabled(10);
    } else if (sel == Relay) {
      EnsureDisabled(10);
      SetParam("CAM_TRIGG_TYPE", 1);
    } else if (sel == Transistor) {
      EnsureDisabled(10);
      SetParam("CAM_TRIGG_TYPE", 4);
    } else {
      EnsureDisabled(10);
      SetParam(sel + "_FUNCTION", 10);
      SetParam("CAM_TRIGG_TYPE", 0);
    }
  }

  [Obsolete]
  internal void EnsureDisabled(int role, string exclude = "") {
    foreach (var ch in ChannelNamesWithFunction()) {
      if (ch == exclude) {
        continue;
      }
      var fn = ch + "_FUNCTION";
      if ((int)Math.Round(comPort.MAV.param[fn].Value) == role) {
        SetParam(fn, 0);
      }
    }
  }

  private IEnumerable<string> ChannelNamesWithFunction() {
    foreach (var name in comPort.MAV.param.Keys.ToList()) {
      if (name.EndsWith("_FUNCTION")) {
        yield return name.Replace("_FUNCTION", "");
      }
    }
  }

  internal bool Online => comPort.BaseStream?.IsOpen == true;

  internal bool HasParam(string name) => comPort.MAV.param.ContainsKey(name);

  internal double? GetParam(string name) =>
      HasParam(name) ? comPort.MAV.param[name].Value : (double?)null;

  [Obsolete]
  internal async void SetParam(string name, double value) {
    if (!Online) {
      if (HasParam(name)) {
        comPort.MAV.param[name].Value = value;
      }
      Status = "offline";
      return;
    }
    try {
      var n = name;
      var v = value;
      var ok = await Task.Run(() => comPort.setParam(n, v, true));
      Status = ok ? name + " set" : name + " write failed";
    } catch (Exception ex) {
      Status = ex.Message;
    }
  }

  private static List<string> BuildChannelNames(string family) {
    var list = new List<string>();
    if (family == "SERVO") {
      for (var i = 1; i <= 14; i++) {
        list.Add("SERVO" + i);
      }
    } else {
      for (var i = 5; i <= 14; i++) {
        list.Add("RC" + i);
      }
    }
    return list;
  }

  private static List<KeyValuePair<int, string>> SafeOptions(string name) {
    try {
      return ParameterMetaDataRepository.GetParameterOptionsInt(
                 name, AppState.comPort.MAV.cs.firmware.ToString())
             ?? new List<KeyValuePair<int, string>>();
    } catch {
      return new List<KeyValuePair<int, string>>();
    }
  }
}

public partial class MountAxisViewModel : ObservableObject {
  private readonly ConfigMountViewModel _parent;
  private readonly int _role;
  private readonly string _angMin;
  private readonly string _angMax;
  private readonly string _rcIn;
  private bool _suppress;

  public MountAxisViewModel(ConfigMountViewModel parent, string header, int role,
      string angMinSuffix, string angMaxSuffix, string rcInSuffix,
      double angleMinLo, double angleMinHi, double angleMaxLo, double angleMaxHi,
      IEnumerable<string> channels) {
    _parent = parent;
    Header = header;
    _role = role;
    _angMin = "MNT_" + angMinSuffix;
    _angMax = "MNT_" + angMaxSuffix;
    _rcIn = "MNT_" + rcInSuffix;
    AngleMinLowerBound = angleMinLo;
    AngleMinUpperBound = angleMinHi;
    AngleMaxLowerBound = angleMaxLo;
    AngleMaxUpperBound = angleMaxHi;

    Functions.Add("Disable");
    foreach (var c in channels) {
      Functions.Add(c);
    }

    InputChannels.Add(new ParamOption(0, "Disable"));
    for (var i = 5; i <= 16; i++) {
      InputChannels.Add(new ParamOption(i, "RC" + i));
    }
  }

  public string Header { get; }
  public ObservableCollection<string> Functions { get; } = new();
  public ObservableCollection<ParamOption> InputChannels { get; } = new();

  public double AngleMinLowerBound { get; }
  public double AngleMinUpperBound { get; }
  public double AngleMaxLowerBound { get; }
  public double AngleMaxUpperBound { get; }

  [ObservableProperty]
  private string _selectedFunction = "";

  [ObservableProperty]
  private double _servoMin = 1000;

  [ObservableProperty]
  private double _servoMax = 2000;

  [ObservableProperty]
  private double _angleMin;

  [ObservableProperty]
  private double _angleMax;

  [ObservableProperty]
  private bool _reverse;

  [ObservableProperty]
  private ParamOption? _selectedInput;

  internal void SetSelectedFunctionSilent(string value) {
    _suppress = true;
    SelectedFunction = value;
    _suppress = false;
  }

  internal void ReloadFromParams() {
    _suppress = true;
    ReloadChannelNumerics();
    AngleMin = _parent.GetParam(_angMin) ?? 0;
    OnPropertyChanged(nameof(AngleMin));
    AngleMax = _parent.GetParam(_angMax) ?? 0;
    OnPropertyChanged(nameof(AngleMax));
    SelectedInput = InputChannels.FirstOrDefault(
        o => o.Value == (int)Math.Round(_parent.GetParam(_rcIn) ?? -1));
    _suppress = false;
  }

  [Obsolete]
  internal void ApplyFunction() {
    if (string.IsNullOrEmpty(SelectedFunction)) {
      return;
    }
    if (SelectedFunction != "Disable") {
      _parent.SetParam(SelectedFunction + "_FUNCTION", _role);
    } else {
      _parent.EnsureDisabled(_role);
    }
    _suppress = true;
    ReloadChannelNumerics();
    _suppress = false;
  }

  private void ReloadChannelNumerics() {
    if (string.IsNullOrEmpty(SelectedFunction) || SelectedFunction == "Disable") {
      return;
    }

    ServoMin = _parent.GetParam(SelectedFunction + "_MIN") ?? 1000;
    OnPropertyChanged(nameof(ServoMin));
    ServoMax = _parent.GetParam(SelectedFunction + "_MAX") ?? 2000;
    OnPropertyChanged(nameof(ServoMax));

    var revName = ReverseParam();
    var val = _parent.GetParam(revName) ?? 0;
    Reverse = revName.EndsWith("_REVERSED") ? val == 1 : val == -1;
    OnPropertyChanged(nameof(Reverse));
  }

  private string ReverseParam() =>
      _parent.HasParam(SelectedFunction + "_REVERSED") ? SelectedFunction + "_REVERSED"
                                                       : SelectedFunction + "_REV";

  [Obsolete]
  partial void OnSelectedFunctionChanged(string value) {
    if (_suppress) {
      return;
    }
    _parent.AxisSelectionChanged();
  }

  [Obsolete]
  partial void OnServoMinChanged(double value) {
    if (_suppress || string.IsNullOrEmpty(SelectedFunction) || SelectedFunction == "Disable") {
      return;
    }
    _parent.SetParam(SelectedFunction + "_MIN", value);
  }

  [Obsolete]
  partial void OnServoMaxChanged(double value) {
    if (_suppress || string.IsNullOrEmpty(SelectedFunction) || SelectedFunction == "Disable") {
      return;
    }
    _parent.SetParam(SelectedFunction + "_MAX", value);
  }

  [Obsolete]
  partial void OnAngleMinChanged(double value) {
    if (_suppress) {
      return;
    }
    _parent.SetParam(_angMin, value);
  }

  [Obsolete]
  partial void OnAngleMaxChanged(double value) {
    if (_suppress) {
      return;
    }
    _parent.SetParam(_angMax, value);
  }

  [Obsolete]
  partial void OnReverseChanged(bool value) {
    if (_suppress || string.IsNullOrEmpty(SelectedFunction) || SelectedFunction == "Disable") {
      return;
    }
    var revName = ReverseParam();
    var v = revName.EndsWith("_REVERSED") ? (value ? 1 : 0) : (value ? -1 : 1);
    _parent.SetParam(revName, v);
  }

  [Obsolete]
  partial void OnSelectedInputChanged(ParamOption? value) {
    if (_suppress || value is null) {
      return;
    }
    _parent.SetParam(_rcIn, value.Value);
  }
}
