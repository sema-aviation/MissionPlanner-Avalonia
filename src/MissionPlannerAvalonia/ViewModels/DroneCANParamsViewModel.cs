using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DroneCAN;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

// Standalone per-node DroneCAN parameter editor — port of upstream Controls/DroneCANParams.cs.
// Distinct from the inline editor in the config page: this is its own window that connects a
// DroneCanBridge over the active MAVLink link, picks a node, and does get/edit/write/save.
// Columns mirror upstream: Command(name) | Value | Min | Max | Default | Fav, plus the search
// box, modified-only filter, re-request, write+save-to-flash, erase, and .param load/save.
public partial class DroneCANParamsViewModel : ViewModelBase, IDisposable {
  private readonly DroneCanBridge _bridge = new();
  private byte? _node;

  // Full row set; Params is the filtered view bound to the grid.
  private readonly List<DroneCanParamRow> _all = new();

  public ObservableCollection<DroneCanParamRow> Params { get; } = new();

  public string[] BusOptions { get; } = { "MAVLink CAN1", "MAVLink CAN2" };

  [ObservableProperty]
  private int _selectedBusIndex;

  [ObservableProperty]
  private int _nodeId;

  [ObservableProperty]
  private bool _isConnected;

  [ObservableProperty]
  private bool _isBusy;

  [ObservableProperty]
  private string _status = "Pick a node ID and Connect, then Get Parameters.";

  [ObservableProperty]
  private string _search = "";

  [ObservableProperty]
  private bool _showModifiedOnly;

  public string ConnectLabel => IsConnected ? "Disconnect" : "Connect";

  partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectLabel));

  partial void OnSearchChanged(string value) => ApplyFilter();

  partial void OnShowModifiedOnlyChanged(bool value) => ApplyFilter();

  [RelayCommand]
  private void ToggleConnect() {
    if (IsConnected) {
      _bridge.Stop();
      IsConnected = false;
      Status = "Disconnected.";
      return;
    }

    byte bus = (byte)(SelectedBusIndex == 1 ? 2 : 1);
    if (!_bridge.Start(bus)) {
      Status = "Not connected — open the MAVLink link first.";
      return;
    }

    IsConnected = true;
    Status = $"Connected on MAVLink CAN{bus}. Set node ID and Get Parameters.";
  }

  // Entry point used by DroneCANParamsWindow.OpenForNode — connect (bus 1) and load.
  public async Task InitForNodeAsync(byte node) {
    NodeId = node;
    byte bus = (byte)(SelectedBusIndex == 1 ? 2 : 1);
    if (!_bridge.Start(bus)) {
      Status = "Not connected — open the MAVLink link first.";
      return;
    }

    IsConnected = true;
    await GetParameters();
  }

  [RelayCommand]
  private async Task GetParameters() {
    var can = _bridge.Can;
    if (can == null) {
      Status = "Connect first.";
      return;
    }

    var node = (byte)NodeId;
    _node = node;
    IsBusy = true;
    Status = $"Requesting parameters from node {node}…";

    List<DroneCAN.DroneCAN.uavcan_protocol_param_GetSet_res> list = new();
    try {
      await Task.Run(() => list = can.GetParameters(node));
    } catch (Exception ex) {
      Status = "Error getting parameters: " + ex.Message;
      IsBusy = false;
      return;
    }

    var favs = Settings.Instance.GetList("fav_params").ToHashSet();

    _all.Clear();
    foreach (var p in list) {
      var name = Encoding.ASCII.GetString(p.name, 0, p.name_len);
      if (string.IsNullOrEmpty(name)) {
        continue;
      }

      _all.Add(new DroneCanParamRow {
        Name = name,
        Value = Convert.ToString(p.value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        OriginalValue = Convert.ToString(p.value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Min = Convert.ToString(p.min_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Max = Convert.ToString(p.max_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Default = Convert.ToString(p.default_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        IsFav = favs.Contains(name),
      });
    }

    // Favourites first, then alphabetical (mirrors upstream OnParamsOnSortCompare).
    _all.Sort((a, b) => a.IsFav != b.IsFav
        ? b.IsFav.CompareTo(a.IsFav)
        : string.CompareOrdinal(a.Name, b.Name));

    ApplyFilter();
    Status = $"Loaded {_all.Count} parameters from node {node}.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task Write() {
    var can = _bridge.Can;
    if (can == null || _node == null) {
      Status = "Connect and Get Parameters first.";
      return;
    }

    var changed = _all.Where(p => p.IsDirty).ToList();
    if (changed.Count == 0) {
      Status = "No modified parameters to write.";
      return;
    }

    IsBusy = true;
    var node = _node.Value;
    int failed = 0;

    await Task.Run(() => {
      foreach (var p in changed) {
        try {
          object value = double.TryParse(p.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
              ? d
              : p.Value;
          if (!can.SetParameter(node, p.Name, value)) {
            failed++;
          }
        } catch {
          failed++;
        }
      }

      try {
        can.SaveConfig(node);
      } catch {
      }
    });

    foreach (var p in changed) {
      p.OriginalValue = p.Value;
    }

    Status = failed == 0
        ? $"Wrote {changed.Count} parameter(s) and saved to flash."
        : $"Wrote parameters with {failed} failure(s); saved to flash.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task CommitToFlash() {
    var can = _bridge.Can;
    if (can == null || _node == null) {
      Status = "Connect and Get Parameters first.";
      return;
    }

    IsBusy = true;
    var node = _node.Value;
    bool ok = false;
    await Task.Run(() => {
      try {
        ok = can.SaveConfig(node);
      } catch {
      }
    });
    Status = ok ? "Parameters committed to non-volatile memory." : "Failed to save.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task ReRequest() => await GetParameters();

  [RelayCommand]
  private async Task Erase() {
    var can = _bridge.Can;
    if (can == null || _node == null) {
      Status = "Connect and Get Parameters first.";
      return;
    }

    IsBusy = true;
    var node = _node.Value;
    bool ok = false;
    await Task.Run(() => {
      try {
        ok = can.ExecuteOpCode(node, (byte)DroneCAN.DroneCAN.uavcan_protocol_param_ExecuteOpcode_req
            .UAVCAN_PROTOCOL_PARAM_EXECUTEOPCODE_REQ_OPCODE_ERASE);
      } catch {
      }
    });
    Status = ok ? "Erased parameters to defaults (node restart may be required)." : "Failed to erase.";
    IsBusy = false;
  }

  // Toggle a row's favourite flag and persist to Settings (mirrors Params_CellContentClick).
  public void ToggleFav(DroneCanParamRow row) {
    if (row.IsFav) {
      Settings.Instance.AppendList("fav_params", row.Name);
    } else {
      var list = Settings.Instance.GetList("fav_params");
      Settings.Instance.SetList("fav_params", list.Where(s => s != row.Name));
    }

    _all.Sort((a, b) => a.IsFav != b.IsFav
        ? b.IsFav.CompareTo(a.IsFav)
        : string.CompareOrdinal(a.Name, b.Name));
    ApplyFilter();
  }

  public void LoadParamFile(string path) {
    Dictionary<string, double> fileParams;
    try {
      fileParams = ParamFile.loadParamFile(path);
    } catch (Exception ex) {
      Status = "Failed to load: " + ex.Message;
      return;
    }

    int loaded = 0;
    foreach (var row in _all) {
      if (fileParams.TryGetValue(row.Name, out var v)) {
        row.Value = v.ToString(CultureInfo.InvariantCulture);
        loaded++;
      }
    }

    ApplyFilter();
    Status = $"Loaded {loaded} matching parameter value(s) from file (not yet written).";
  }

  public void SaveParamFile(string path) {
    var table = new Hashtable();
    foreach (var row in _all) {
      if (double.TryParse(row.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) {
        table[row.Name] = v;
      }
    }

    try {
      ParamFile.SaveParamFile(path, table);
      Status = $"Saved {table.Count} parameter(s) to file.";
    } catch (Exception ex) {
      Status = "Failed to save: " + ex.Message;
    }
  }

  private void ApplyFilter() {
    var search = Search?.Trim() ?? "";
    Regex? rx = null;
    if (search.Length >= 2) {
      try {
        rx = new Regex(search.Replace("*", ".*").Replace("..*", ".*"),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
      } catch {
        rx = null;
      }
    }

    Params.Clear();
    foreach (var row in _all) {
      if (ShowModifiedOnly && !row.IsDirty) {
        continue;
      }

      if (rx != null &&
          !rx.IsMatch(row.Name) && !rx.IsMatch(row.Value) && !rx.IsMatch(row.Min) &&
          !rx.IsMatch(row.Max) && !rx.IsMatch(row.Default)) {
        continue;
      }

      Params.Add(row);
    }
  }

  public void Dispose() => _bridge.Stop();
}

public partial class DroneCanParamRow : ObservableObject {
  [ObservableProperty]
  private string _name = "";

  [ObservableProperty]
  private string _value = "";

  [ObservableProperty]
  private string _min = "";

  [ObservableProperty]
  private string _max = "";

  [ObservableProperty]
  private string _default = "";

  [ObservableProperty]
  private bool _isFav;

  public string OriginalValue { get; set; } = "";

  public bool IsDirty => !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
}
