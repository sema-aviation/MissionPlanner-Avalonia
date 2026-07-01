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

public partial class BitOption : ObservableObject {
  private readonly ParamField _owner;

  public int Bit { get; }
  public long Mask => 1L << Bit;
  public string Label { get; }

  [ObservableProperty]
  private bool _isSet;

  public BitOption(ParamField owner, int bit, string label) {
    _owner = owner;
    Bit = bit;
    Label = $"{bit}: {label}";
  }

  [Obsolete]
  partial void OnIsSetChanged(bool value) => _owner.OnBitToggled();
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
  public bool IsBitmask { get; }
  public bool IsNumeric => !IsCombo && !IsBool && !IsBitmask;

  public double Min { get; } = double.MinValue;
  public double Max { get; } = double.MaxValue;
  public double Increment { get; } = 0.01;
  private readonly bool _hasRange;

  public ObservableCollection<ParamOption> Options { get; } = new();
  public ObservableCollection<BitOption> BitOptions { get; } = new();

  public bool Exists => _comPort.MAV.param.ContainsKey(Name);

  public bool IsOutOfRange => Exists && _hasRange && (Value < Min || Value > Max);

  [ObservableProperty]
  private bool _fav;

  [ObservableProperty]
  private double _value;

  [ObservableProperty]
  private bool _checked;

  [ObservableProperty]
  private ParamOption? _selectedOption;

  [ObservableProperty]
  private string _status = "";

  public string BitmaskSummary {
    get {
      var on = BitOptions.Where(b => b.IsSet).Select(b => b.Label).ToList();
      if (on.Count == 0) {
        return "(none)";
      }
      return on.Count <= 3 ? string.Join(", ", on) : $"{on.Count} bits set";
    }
  }

  public ParamField(string name, string? kind = null) {
    Name = name;
    var fw = _comPort.MAV.cs.firmware.ToString();

    Label = Meta(ParameterMetaDataConstants.DisplayName, fw) is { Length: > 0 } dn ? dn : name;
    Units = Meta(ParameterMetaDataConstants.Units, fw);
    Description = Meta(ParameterMetaDataConstants.Description, fw);

    var opts = SafeOptions(name, fw);
    var bits = SafeBitmask(name, fw);

    if (kind == "bitmask" && bits.Count > 0) {
      IsBitmask = true;
      foreach (var kv in bits) {
        BitOptions.Add(new BitOption(this, kv.Key, kv.Value));
      }
    } else if (kind == "combo" || (kind == null && opts.Count > 0)) {
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
      _hasRange = true;
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
      if (IsBitmask) {
        long iv = (long)Math.Round(v);
        foreach (var b in BitOptions) {
          b.IsSet = (iv & b.Mask) != 0;
        }
        OnPropertyChanged(nameof(BitmaskSummary));
      }
      Status = "";
    } else {
      Status = "n/a";
    }
    _suppress = false;
    OnPropertyChanged(nameof(IsOutOfRange));
  }

  [Obsolete]
  internal void OnBitToggled() {
    if (_suppress) {
      return;
    }
    long v = 0;
    foreach (var b in BitOptions) {
      if (b.IsSet) {
        v |= b.Mask;
      }
    }
    _suppress = true;
    Value = v;
    _suppress = false;
    OnPropertyChanged(nameof(BitmaskSummary));
    OnPropertyChanged(nameof(IsOutOfRange));
    Push(v);
  }

  [Obsolete]
  partial void OnValueChanged(double value) {
    OnPropertyChanged(nameof(IsOutOfRange));
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

  private static List<KeyValuePair<int, string>> SafeBitmask(string name, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterBitMaskInt(name, fw) ?? new();
    } catch {
      return new();
    }
  }

  public static bool HasBitmask(string name, string fw) => SafeBitmask(name, fw).Count > 0;
}
