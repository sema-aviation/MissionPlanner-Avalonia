using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class RawParamsViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private readonly List<ParamRow> _all = new();

  public ObservableCollection<ParamRow> Params { get; } = new();

  public ObservableCollection<string> Categories { get; } = new() { "All" };

  [ObservableProperty]
  private string _selectedCategory = "All";

  [ObservableProperty]
  private string _searchText = "";

  [ObservableProperty]
  private bool _showModifiedOnly;

  [ObservableProperty]
  private bool _showNonDefaultOnly;

  [ObservableProperty]
  private bool _hasDefaults;

  [ObservableProperty]
  private string _status = "Not connected. Use Load from file / Load Demo to preview, or connect a vehicle.";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public RawParamsViewModel() {

    if (!IsConnected && System.IO.File.Exists(CacheFilePath)) {
      LoadSnapshotFile(CacheFilePath);
    }
  }

  partial void OnSearchTextChanged(string value) => ApplyFilter();

  partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

  partial void OnShowModifiedOnlyChanged(bool value) => ApplyFilter();

  partial void OnShowNonDefaultOnlyChanged(bool value) => ApplyFilter();

  [RelayCommand]
  private async Task Refresh() {
    if (!IsConnected) {
      await Services.Dialogs.Alert("Refresh parameters", "Not connected — cannot fetch params.");
      return;
    }

    try {

      await Task.Run(() => _comPort.getParamListMavftp(_comPort.MAV.sysid, _comPort.MAV.compid));
      LoadFromMav();
      await Services.Dialogs.Alert("Refresh parameters", $"Loaded {_all.Count} parameters.");
    } catch (Exception ex) {
      await Services.Dialogs.Alert("Refresh failed", ex.Message);
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task Write() {
    var dirty = _all.Where(r => r.IsDirty).ToList();
    if (dirty.Count == 0) {
      await Services.Dialogs.Alert("Write parameters", "No changes to write.");
      return;
    }

    var pending = new List<(ParamRow row, double val)>();
    foreach (var r in dirty) {
      if (!TryEval(r.ValueText, out var v)) {
        await Services.Dialogs.Alert("Invalid value",
            $"\"{r.ValueText}\" is not a valid value for {r.Name}.");
        return;
      }
      pending.Add((r, v));
    }

    if (!IsConnected) {

      foreach (var (row, val) in pending) {
        if (_comPort.MAV.param.ContainsKey(row.Name)) {
          _comPort.MAV.param[row.Name].Value = val;
        }
        row.SetCurrent(val);
      }
      await Services.Dialogs.Alert("Write parameters",
          $"Not connected — staged {pending.Count} change(s) into the in-memory list.");
      return;
    }

    int ok = 0;
    bool reboot = false;
    var fw = _comPort.MAV.cs.firmware.ToString();
    try {
      foreach (var (row, val) in pending) {
        var name = row.Name;
        bool wrote = await Task.Run(() => _comPort.setParam(name, val, true));
        if (wrote) {
          row.SetCurrent(val);
          ok++;
          try {
            reboot |= ParameterMetaDataRepository.GetParameterRebootRequired(name, fw);
          } catch { }
        }
      }
    } catch (Exception ex) {
      ApplyFilter();
      await Services.Dialogs.Alert("Write failed",
          $"Wrote {ok}/{pending.Count} before error: {ex.Message}");
      return;
    }

    ApplyFilter();
    if (ok == pending.Count) {
      await Services.Dialogs.Alert("Write parameters",
          $"Wrote {ok} parameter(s)."
          + (reboot ? "\n\nA reboot is required for some changes to take effect." : ""));
    } else {
      await Services.Dialogs.Alert("Write parameters",
          $"Wrote {ok} of {pending.Count}. The rest were not acknowledged — try again.");
    }
  }

  [RelayCommand]
  private async Task Commit() {
    if (!IsConnected) {
      await Services.Dialogs.Alert("Commit to flash", "Not connected — cannot commit to flash.");
      return;
    }
    try {
      _comPort.doCommand(
          _comPort.MAV.sysid, _comPort.MAV.compid, MAVLink.MAV_CMD.PREFLIGHT_STORAGE,
          1, 0, 0, 0, 0, 0, 0
      );
      await Services.Dialogs.Alert("Commit to flash", "Commit-to-flash command sent.");
    } catch (Exception ex) {
      await Services.Dialogs.Alert("Commit failed", ex.Message);
    }
  }

  [RelayCommand]
  private void LoadDemo() {
    LoadFrom(
        new[]
        {
                MakeRow("ARMING_CHECK", 1, 1),
                MakeRow("BATT_CAPACITY", 5000, 3300),
                MakeRow("FENCE_ENABLE", 0, 0),
                MakeRow("RTL_ALT", 3000, 1500),
                MakeRow("WPNAV_SPEED", 500, 1000),
                MakeRow("ATC_RAT_RLL_P", 0.135, 0.135),
        }
    );
    Status = $"Loaded {_all.Count} demo parameters (edit a value → Write).";
  }

  public void LoadParamFile(string path) => MergeFromFile(path, compareOnly: false);

  public void CompareParamFile(string path) => MergeFromFile(path, compareOnly: true);

  public void SaveParamFile(string path) {
    var table = new Hashtable();
    foreach (var r in _all) {
      table[r.Name] = r.CurrentValue;
    }
    try {
      ParamFile.SaveParamFile(path, table);
      _ = Services.Dialogs.Alert("Save parameters",
          $"Saved {table.Count} parameters to {System.IO.Path.GetFileName(path)}.");
    } catch (Exception ex) {
      _ = Services.Dialogs.Alert("Save failed", ex.Message);
    }
  }

  public static string CacheFilePath {
    get {
      var dir = System.IO.Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MissionPlannerAvalonia");
      System.IO.Directory.CreateDirectory(dir);
      return System.IO.Path.Combine(dir, "last_params.param");
    }
  }

  public static void SaveSnapshot(MAVLinkInterface comPort) {
    try {
      if (comPort?.MAV?.param == null || comPort.MAV.param.Count == 0) {
        return;
      }
      var table = new Hashtable();
      foreach (var p in comPort.MAV.param.ToArray()) {
        table[p.Name] = p.Value;
      }
      ParamFile.SaveParamFile(CacheFilePath, table);
    } catch {

    }
  }

  private void LoadSnapshotFile(string path) {
    Dictionary<string, double> fileParams;
    try {
      fileParams = ParamFile.loadParamFile(path);
    } catch (Exception ex) {
      Status = "Snapshot load failed: " + ex.Message;
      return;
    }
    if (fileParams.Count == 0) {
      return;
    }

    var fw = _comPort.MAV.cs.firmware.ToString();
    var favs = Settings.Instance.GetList("fav_params").ToHashSet();
    var rows = fileParams.OrderBy(k => k.Key).Select(p => BuildRow(p.Key, p.Value, null, fw, favs));
    LoadFrom(rows);
    Status = $"Offline: {_all.Count} parameters from the last connected session. Connect to edit/write.";
  }

  private void MergeFromFile(string path, bool compareOnly) {
    Dictionary<string, double> fileParams;
    try {
      fileParams = ParamFile.loadParamFile(path);
    } catch (Exception ex) {
      _ = Services.Dialogs.Alert("Load failed", ex.Message);
      return;
    }

    int matched = 0,
        differing = 0;
    foreach (var r in _all) {
      if (!fileParams.TryGetValue(r.Name, out var fv)) {
        continue;
      }
      matched++;
      if (fv != r.CurrentValue) {
        differing++;
        r.ValueText = fv.ToString(CultureInfo.InvariantCulture);
      }
    }

    if (compareOnly) {
      ShowModifiedOnly = true;
    }
    ApplyFilter();
    _ = Services.Dialogs.Alert(compareOnly ? "Compare parameters" : "Load parameters",
        compareOnly
            ? $"{System.IO.Path.GetFileName(path)}: {differing} differing of {matched} matched (showing modified). Write to apply."
            : $"{System.IO.Path.GetFileName(path)}: staged {differing} change(s) of {matched} matched param(s). Write to apply.");
  }

  public void PersistFavs() {
    var favs = _all.Where(r => r.Fav).Select(r => r.Name).ToList();
    Settings.Instance.SetList("fav_params", favs);
  }

  private void LoadFromMav() {
    var fw = _comPort.MAV.cs.firmware.ToString();
    var favs = Settings.Instance.GetList("fav_params").ToHashSet();

    var snapshot = _comPort.MAV.param.ToArray();
    var rows = snapshot.Select(p => BuildRow(p.Name, p.Value, p.default_value, fw, favs)).ToList();
    LoadFrom(rows);
  }

  private ParamRow BuildRow(string name, double value, double? def, string fw, HashSet<string> favs) {
    string units = Meta(name, ParameterMetaDataConstants.Units, fw);
    string range = Meta(name, ParameterMetaDataConstants.Range, fw);
    string values = Meta(name, ParameterMetaDataConstants.Values, fw);
    string desc = Meta(name, ParameterMetaDataConstants.Description, fw);
    string opts = (range + "\n" + values.Replace(",", "\n")).Trim();

    double min = double.MinValue,
        max = double.MaxValue;
    try {
      ParameterMetaDataRepository.GetParameterRange(name, ref min, ref max, fw);
    } catch {

    }

    return new ParamRow(name, value, def, units, opts, desc, min, max) { Fav = favs.Contains(name) };
  }

  private ParamRow MakeRow(string name, double value, double? def) {
    var fw = _comPort.MAV.cs.firmware.ToString();
    var favs = Settings.Instance.GetList("fav_params").ToHashSet();
    return BuildRow(name, value, def, fw, favs);
  }

  private void LoadFrom(IEnumerable<ParamRow> rows) {
    void Apply() {
      _all.Clear();
      _all.AddRange(rows);
      HasDefaults = _all.Any(r => r.DefaultValue.HasValue);
      Sort();
      RebuildCategories();
      ApplyFilter();
    }
    if (Dispatcher.UIThread.CheckAccess()) {
      Apply();
    } else {
      Dispatcher.UIThread.Post(Apply);
    }
  }

  private void Sort() {
    _all.Sort(
        (a, b) => {
          if (a.Fav != b.Fav) {
            return a.Fav ? -1 : 1;
          }
          return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }
    );
  }

  private void RebuildCategories() {
    var prefixes = _all
        .Select(r => r.Prefix)
        .Where(p => p.Length > 0)
        .Distinct()
        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
        .ToList();
    Categories.Clear();
    Categories.Add("All");
    foreach (var p in prefixes) {
      Categories.Add(p);
    }
    SelectedCategory = "All";
  }

  private void ApplyFilter() {
    var q = SearchText?.Trim() ?? "";
    var cat = SelectedCategory ?? "All";
    Params.Clear();
    foreach (var r in _all) {
      if (q.Length >= 2 && !r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) {
        continue;
      }
      if (cat != "All" && r.Prefix != cat) {
        continue;
      }
      if (ShowModifiedOnly && !r.IsDirty) {
        continue;
      }
      if (ShowNonDefaultOnly && !r.IsNonDefault) {
        continue;
      }
      Params.Add(r);
    }
  }

  private static string Meta(string name, string key, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterMetaData(name, key, fw) ?? "";
    } catch {
      return "";
    }
  }

  private static bool TryEval(string? text, out double value) {
    value = 0;
    if (string.IsNullOrWhiteSpace(text)) {
      return false;
    }
    try {
      value = new org.mariuszgromada.math.mxparser.Expression(text).calculate();
      return !double.IsNaN(value) && !double.IsInfinity(value);
    } catch {
      return false;
    }
  }
}

public partial class ParamRow : ObservableObject {
  public ParamRow(
      string name,
      double current,
      double? def,
      string units,
      string options,
      string description,
      double min,
      double max
  ) {
    Name = name;
    _currentValue = current;
    _valueText = current.ToString(CultureInfo.InvariantCulture);
    DefaultValue = def;
    Units = units;
    Options = options;
    Description = description;
    Min = min;
    Max = max;
    var us = name.IndexOf('_');
    Prefix = us > 0 ? name.Substring(0, us) : name;
  }

  public string Name { get; }
  public string Prefix { get; }
  public string Units { get; }
  public string Options { get; }
  public string Description { get; }
  public double Min { get; }
  public double Max { get; }
  public double? DefaultValue { get; }

  public string DefaultText =>
      DefaultValue.HasValue ? DefaultValue.Value.ToString(CultureInfo.InvariantCulture) : "";

  [ObservableProperty]
  private double _currentValue;

  [ObservableProperty]
  private string _valueText;

  [ObservableProperty]
  private bool _fav;

  public bool IsDirty {
    get {
      if (double.TryParse(ValueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) {
        return v != CurrentValue;
      }
      return ValueText != CurrentValue.ToString(CultureInfo.InvariantCulture);
    }
  }

  public bool IsNonDefault => DefaultValue.HasValue && DefaultValue.Value != CurrentValue;

  partial void OnValueTextChanged(string value) => OnPropertyChanged(nameof(IsDirty));

  public void SetCurrent(double v) {
    CurrentValue = v;
    ValueText = v.ToString(CultureInfo.InvariantCulture);
    OnPropertyChanged(nameof(IsDirty));
    OnPropertyChanged(nameof(IsNonDefault));
  }
}
