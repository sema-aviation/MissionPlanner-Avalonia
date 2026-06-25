using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigADSBViewModel : ParamPageBase {
  public ObservableCollection<ParamField> FilteredFields { get; } = new();

  [ObservableProperty]
  private string _search = "";

  [ObservableProperty]
  private string _status = "";

  public ConfigADSBViewModel() {
    Title = "ADSB";
    Intro = "ADS-B receiver / avoidance. Populated on connect.";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  partial void OnSearchChanged(string value) {
    ApplyFilter();
  }

  private void Setup() {
    foreach (var key in comPort.MAV.param.Keys.ToList()
                 .Where(k => k.StartsWith("ADSB_", StringComparison.OrdinalIgnoreCase) ||
                             k.StartsWith("AVD_", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)) {
      F(key);
    }
    ApplyFilter();
  }

  private void ApplyFilter() {
    FilteredFields.Clear();
    var term = Search?.Trim() ?? "";
    foreach (var f in Fields) {
      if (term.Length < 2 ||
          f.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
          f.Label.Contains(term, StringComparison.OrdinalIgnoreCase) ||
          f.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) {
        FilteredFields.Add(f);
      }
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task Write() {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }

    // set ENABLE params last
    var ordered = Fields
        .OrderBy(f => f.Name.Contains("ENABLE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
        .ToList();

    bool error = false;
    foreach (var f in ordered) {
      if (!f.Exists) {
        continue;
      }
      try {
        var name = f.Name;
        var value = f.Value;
        if (!await Task.Run(() => comPort.setParam(name, value))) {
          error = true;
        }
      } catch {
        error = true;
      }
    }

    Status = error ? "write failed" : "Parameters successfully saved.";
  }
}
