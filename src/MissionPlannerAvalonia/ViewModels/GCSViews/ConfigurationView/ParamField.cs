using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public record ParamOption(int Value, string Text) {
  public override string ToString() => Text;
}

public partial class ParamField : ObservableObject {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _suppress;

  public string Name { get; }
  public string Label { get; }
  public string Units { get; }
  public string Description { get; }

  public bool IsCombo { get; }
  public bool IsBool { get; }
  public bool IsNumeric => !IsCombo && !IsBool;

  public double Min { get; } = double.MinValue;
  public double Max { get; } = double.MaxValue;
  public double Increment { get; } = 0.01;

  public ObservableCollection<ParamOption> Options { get; } = new();

  public bool Exists => _comPort.MAV.param.ContainsKey(Name);

  [ObservableProperty]
  private double _value;

  [ObservableProperty]
  private bool _checked;

  [ObservableProperty]
  private ParamOption? _selectedOption;

  [ObservableProperty]
  private string _status = "";

  public ParamField(string name, string? kind = null) {
    Name = name;
    var fw = _comPort.MAV.cs.firmware.ToString();

    Label = Meta(ParameterMetaDataConstants.DisplayName, fw) is { Length: > 0 } dn ? dn : name;
    Units = Meta(ParameterMetaDataConstants.Units, fw);
    Description = Meta(ParameterMetaDataConstants.Description, fw);

    var opts = SafeOptions(name, fw);
    if (kind == "combo" || (kind == null && opts.Count > 0)) {
      IsCombo = true;
      foreach (var kv in opts) {
        Options.Add(new ParamOption(kv.Key, kv.Value));
      }
    } else if (kind == "bool") {
      IsBool = true;
    }

    double min = Min,
        max = Max,
        inc = Increment;
    if (ParameterMetaDataRepository.GetParameterRange(name, ref min, ref max, fw)) {
      Min = min;
      Max = max;
    }
    if (ParameterMetaDataRepository.GetParameterIncrement(name, ref inc, fw)) {
      Increment = inc;
    }

    Reload();
  }

  public void Reload() {
    _suppress = true;
    if (Exists) {
      var v = _comPort.MAV.param[Name].Value;
      Value = v;
      Checked = v != 0;
      SelectedOption = Options.FirstOrDefault(o => o.Value == (int)Math.Round(v));
      Status = "";
    } else {
      Status = "n/a";
    }
    _suppress = false;
  }

  [Obsolete]
  partial void OnValueChanged(double value) {
    if (IsNumeric)
      Push(value);
  }

  [Obsolete]
  partial void OnCheckedChanged(bool value) {
    if (IsBool)
      Push(value ? 1 : 0);
  }

  [Obsolete]
  partial void OnSelectedOptionChanged(ParamOption? value) {
    if (IsCombo && value != null)
      Push(value.Value);
  }

  [Obsolete]
  private async void Push(double v) {
    if (_suppress) {
      return;
    }

    if (_comPort.BaseStream?.IsOpen != true) {
      if (Exists) {
        _comPort.MAV.param[Name].Value = v;
      }

      Status = "offline";
      return;
    }
    try {
      var name = Name;
      bool ok = await Task.Run(() => _comPort.setParam(name, v, true));
      Status = ok ? "✓" : "write failed";
    } catch (Exception ex) {
      Status = ex.Message;
    }
  }

  private string Meta(string key, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterMetaData(Name, key, fw) ?? "";
    } catch {
      return "";
    }
  }

  private static List<KeyValuePair<int, string>> SafeOptions(string name, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterOptionsInt(name, fw) ?? new();
    } catch {
      return new();
    }
  }
}
