using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public record SimpleRelation(string Name, double Multiplier);

public partial class SimplePidItem : ObservableObject {
  private readonly Action<SimplePidItem>? _onChanged;
  private readonly bool _suppress;

  public string Title { get; }
  public string Description { get; }
  public string Name { get; }
  public double Min { get; }
  public double Max { get; }
  public double Increment { get; }
  public IReadOnlyList<SimpleRelation> Relations { get; }

  [ObservableProperty]
  private double _value;

  public SimplePidItem(string title, string desc, string name, double min, double max,
                       double increment, double value, IReadOnlyList<SimpleRelation> relations,
                       Action<SimplePidItem>? onChanged) {
    Title = title;
    Description = desc;
    Name = name;
    Min = min;
    Max = max;
    Increment = increment;
    Relations = relations;
    _onChanged = onChanged;
    _suppress = true;
    Value = value;
    _suppress = false;
  }

  partial void OnValueChanged(double value) {
    if (!_suppress) {
      _onChanged?.Invoke(this);
    }
  }
}

public partial class ConfigBasicTuningViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public string Title => "Basic Tuning";

  public string Note => "NOTE: using this interface may reset some off your custom pids.";

  public ObservableCollection<SimplePidItem> Items { get; } = new();

  [ObservableProperty]
  private string _info = "";

  private bool Connected => _comPort.BaseStream?.IsOpen == true;

  [Obsolete]
  public ConfigBasicTuningViewModel() {
    Load();
  }

  private static readonly (string Title, string Desc, string Name, double Min, double Max,
      SimpleRelation[] Relations)[] _template = {
    ("RC Feel Roll/Pitch",
     "Slide to the left for softer response to user RC input or slide to the right for crisper response.",
     "RC_FEEL_RP", 0, 100, new SimpleRelation[0]),
    ("Roll/Pitch Sensitivity:",
     "Slide to the right if the copter is sluggish or slide to the left if the copter is twitchy.",
     "RATE_RLL_P", 0.08, 0.4,
     new[] {
       new SimpleRelation("RATE_RLL_I", 1),
       new SimpleRelation("RATE_PIT_P", 1),
       new SimpleRelation("RATE_PIT_I", 1),
     }),
    ("Roll/Pitch Sensitivity:",
     "Slide to the right if the copter is sluggish or slide to the left if the copter is twitchy.",
     "ATC_RAT_RLL_P", 0.08, 0.4,
     new[] {
       new SimpleRelation("ATC_RAT_RLL_I", 1),
       new SimpleRelation("ATC_RAT_PIT_P", 1),
       new SimpleRelation("ATC_RAT_PIT_I", 1),
     }),
    ("Throttle Hover:",
     "How much throttle is needed to maintain a steady hover.",
     "THR_MID", 200, 800, new SimpleRelation[0]),
    ("Climb Sensitivity:",
     "Slide to the right to climb more aggressively or slide to the left to climb more gently.",
     "THR_ACCEL_P", 0.3, 1,
     new[] { new SimpleRelation("THR_ACCEL_I", 2) }),
    ("Climb Sensitivity:",
     "Slide to the right to climb more aggressively or slide to the left to climb more gently.",
     "ACCEL_Z_P", 0.3, 1,
     new[] { new SimpleRelation("ACCEL_Z_I", 2) }),
  };

  [Obsolete]
  private void Load() {
    Items.Clear();
    var fw = _comPort.MAV.cs.firmware.ToString();

    foreach (var t in _template) {
      if (!_comPort.MAV.param.ContainsKey(t.Name)) {
        continue;
      }

      double value = _comPort.MAV.param[t.Name].Value;
      double min = t.Min;
      double max = t.Max;
      if (value < min) {
        min = value;
      }
      if (value > max) {
        max = value;
      }

      double rmin = min, rmax = max;
      if (ParameterMetaDataRepository.GetParameterRange(t.Name, ref rmin, ref rmax, fw)) {
        min = rmin;
        max = rmax;
      }

      double increment = 0.01;
      ParameterMetaDataRepository.GetParameterIncrement(t.Name, ref increment, fw);

      var relations = new List<SimpleRelation>(t.Relations);
      Items.Add(new SimplePidItem(t.Title, t.Desc, t.Name, min, max, increment, value,
                                  relations, OnItemChanged));
    }
  }

  [Obsolete]
  private async void OnItemChanged(SimplePidItem item) {
    Info = "";
    await WriteOne(item.Name, item.Value);
    foreach (var rel in item.Relations) {
      await WriteOne(rel.Name, item.Value * rel.Multiplier);
    }
  }

  [Obsolete]
  private async Task WriteOne(string name, double value) {
    if (!Connected) {
      if (_comPort.MAV.param.ContainsKey(name)) {
        _comPort.MAV.param[name].Value = value;
      }

      Append("offline " + name + " "
             + value.ToString("0.######", CultureInfo.InvariantCulture));
      return;
    }

    try {
      bool ok = await Task.Run(() => _comPort.setParam(name, value, true));
      Append((ok ? "set " : "failed ") + name + " "
             + value.ToString("0.######", CultureInfo.InvariantCulture));
    } catch (Exception ex) {
      Append("error " + name + " " + ex.Message);
    }
  }

  private void Append(string line) => Info += line + "\r\n";

  [RelayCommand]
  [Obsolete]
  private async Task Refresh() {
    if (Connected) {
      await Task.Run(() => _comPort.getParamList());
    }

    Load();
  }
}
