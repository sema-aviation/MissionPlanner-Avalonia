using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

public partial class RawParamsViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private readonly List<ParamRow> _all = new();

  public ObservableCollection<ParamRow> Params { get; } = new();

  [ObservableProperty]
  private string _searchText = "";

  [ObservableProperty]
  private string _status = "Not connected. Use Load Demo to preview, or connect a vehicle.";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  partial void OnSearchTextChanged(string value) => ApplyFilter();

  [RelayCommand]
  private async Task Refresh() {
    if (!IsConnected) {
      Status = "Not connected — cannot fetch params.";
      return;
    }

    Status = "Requesting parameters…";
    try {
      await Task.Run(() => _comPort.getParamList());
      LoadFrom(
          _comPort.MAV.param.Select(p => new ParamRow(p.Name, p.Value, p.default_value, (int)p.Type))
      );
      Status = $"Loaded {_all.Count} parameters.";
    } catch (Exception ex) {
      Status = "Fetch failed: " + ex.Message;
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task Write() {
    var dirty = _all.Where(r => r.IsDirty).ToList();
    if (dirty.Count == 0) {
      Status = "No changes to write.";
      return;
    }
    if (!IsConnected) {
      Status = $"Not connected — {dirty.Count} pending change(s) not written.";
      return;
    }

    Status = $"Writing {dirty.Count} parameter(s)…";
    int ok = 0;
    foreach (var r in dirty) {
      var name = r.Name;
      var val = r.EditValue!.Value;
      bool wrote = await Task.Run(() => _comPort.setParam(name, val));
      if (wrote) {
        r.CurrentValue = val;
        r.EditValue = null;
        ok++;
      }
    }
    Status = $"Wrote {ok}/{dirty.Count} parameter(s).";
  }

  [RelayCommand]
  private void LoadDemo() {
    LoadFrom(
        new[]
        {
                new ParamRow("ARMING_CHECK", 1, 1, 2),
                new ParamRow("BATT_CAPACITY", 5000, 3300, 2),
                new ParamRow("FENCE_ENABLE", 0, 0, 2),
                new ParamRow("RTL_ALT", 3000, 1500, 2),
                new ParamRow("WPNAV_SPEED", 500, 1000, 2),
                new ParamRow("ATC_RAT_RLL_P", 0.135, 0.135, 9),
        }
    );
    Status = $"Loaded {_all.Count} demo parameters (edit a value → Write).";
  }

  private void LoadFrom(IEnumerable<ParamRow> rows) {
    void Apply() {
      _all.Clear();
      _all.AddRange(rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase));
      ApplyFilter();
    }
    if (Dispatcher.UIThread.CheckAccess()) {
      Apply();
    } else {
      Dispatcher.UIThread.Post(Apply);
    }
  }

  private void ApplyFilter() {
    var q = SearchText?.Trim() ?? "";
    Params.Clear();
    foreach (
        var r in _all.Where(r => q.Length == 0 || r.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
    ) {
      Params.Add(r);
    }
  }
}

public partial class ParamRow : ObservableObject {
  public ParamRow(string name, double current, double? def, int type) {
    Name = name;
    _currentValue = current;
    DefaultValue = def;
    Type = type;
  }

  public string Name { get; }
  public double? DefaultValue { get; }
  public int Type { get; }

  [ObservableProperty]
  private double _currentValue;

  [ObservableProperty]
  private double? _editValue;

  public bool IsDirty => EditValue.HasValue && EditValue.Value != CurrentValue;
}
