using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels;

// A message type with its fields, for the 3-level check tree (MsgType -> field).
public partial class LogTypeNode : ObservableObject {
  public string Type { get; }
  public ObservableCollection<LogFieldNode> Fields { get; } = new();
  public LogTypeNode(string type) => Type = type;
}

public partial class LogFieldNode : ObservableObject {
  public string Type { get; }
  public string Field { get; }
  public string Display => Field;
  public LogFieldNode(string type, string field) {
    Type = type;
    Field = field;
  }
}

public partial class LogBrowseViewModel : ViewModelBase {
  private readonly Dictionary<string, string[]> _formats = new(StringComparer.OrdinalIgnoreCase);

  [ObservableProperty]
  private string _info = "Open a .tlog or .bin dataflash log.";

  [ObservableProperty]
  private string _status = string.Empty;

  [ObservableProperty]
  private string? _currentPath;

  [ObservableProperty]
  private string? _selectedType;

  [ObservableProperty]
  private string? _selectedField;

  [ObservableProperty]
  private string _fieldExpression = string.Empty;

  [ObservableProperty]
  private GraphPreset? _selectedPreset;

  [ObservableProperty]
  private bool _busy;

  public ObservableCollection<string> MessageTypes { get; } = new();
  public ObservableCollection<string> Fields { get; } = new();
  public ObservableCollection<LogTypeNode> Tree { get; } = new();
  public ObservableCollection<GraphPreset> Presets { get; } = new();

  // GPS track for the map panel; raised after a load so the view can draw it.
  public IReadOnlyList<(double lat, double lng)> Track { get; private set; } =
      Array.Empty<(double, double)>();
  public event Action? TrackChanged;

  public LogBrowseViewModel() {
    foreach (var p in LoadPresets()) {
      Presets.Add(p);
    }
  }

  partial void OnSelectedTypeChanged(string? value) {
    Fields.Clear();
    if (value != null && _formats.TryGetValue(value, out var fields)) {
      foreach (var f in fields) {
        Fields.Add(f);
      }
    }
    SelectedField = Fields.FirstOrDefault();
  }

  public async Task LoadFileAsync(string path) {
    CurrentPath = path;
    Busy = true;
    Status = "Parsing log…";
    try {
      var (summary, formats, types, track) = await Task.Run(() => Parse(path));
      _formats.Clear();
      foreach (var kv in formats) {
        _formats[kv.Key] = kv.Value;
      }
      MessageTypes.Clear();
      Tree.Clear();
      foreach (var t in types) {
        MessageTypes.Add(t);
        var node = new LogTypeNode(t);
        foreach (var f in formats[t]) {
          node.Fields.Add(new LogFieldNode(t, f));
        }
        Tree.Add(node);
      }
      Track = track;
      Info = summary;
      SelectedType = MessageTypes.FirstOrDefault();
      Status = $"Loaded {types.Count} message types.";
      TrackChanged?.Invoke();
    } catch (Exception ex) {
      Info = $"Failed to read log:\n{ex.Message}";
      Status = "Parse failed.";
    } finally {
      Busy = false;
    }
  }

  // Read one literal TYPE.FIELD curve from the current log.
  public (IReadOnlyList<double> xs, IReadOnlyList<double> ys)? ReadCurve(string type, string field) {
    if (CurrentPath == null) {
      return null;
    }
    var series = DataFlashLog.ReadField(CurrentPath, type, field);
    if (series.Count == 0) {
      return null;
    }
    return (series.Select(s => s.time).ToList(), series.Select(s => s.value).ToList());
  }

  public (string type, string field)? ResolveField() {
    var expr = FieldExpression?.Trim();
    if (!string.IsNullOrEmpty(expr) && expr.Contains('.')) {
      var parts = expr.Split('.', 2);
      var t = parts[0].Trim();
      var f = parts[1].Trim();
      if (t.Length > 0 && f.Length > 0) {
        return (t, f);
      }
    }
    if (!string.IsNullOrEmpty(SelectedType) && !string.IsNullOrEmpty(SelectedField)) {
      return (SelectedType!, SelectedField!);
    }
    return null;
  }

  // Mode/Error/Event overlay markers at the sample times of MODE/ERR/EV messages.
  public IReadOnlyList<(double x, string label)> ReadOverlay(string type, string labelField) {
    if (CurrentPath == null || !_formats.ContainsKey(type)) {
      return Array.Empty<(double, string)>();
    }
    var series = DataFlashLog.ReadField(CurrentPath, type, labelField);
    return series.Select(s => (s.time, $"{type} {s.value:0}")).ToList();
  }

  // Rows of one message type for the data grid (time + each field value).
  public (IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<string>> rows) ReadRows(
      string type, int maxRows = 5000) {
    if (CurrentPath == null || !_formats.TryGetValue(type, out var fields) || fields.Length == 0) {
      return (Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
    }
    var columns = new[] { "time" }.Concat(fields).ToList();
    var perField = fields.Select(f => DataFlashLog.ReadField(CurrentPath, type, f)).ToList();
    int n = Math.Min(maxRows, perField.Count > 0 ? perField.Min(s => s.Count) : 0);
    var rows = new List<IReadOnlyList<string>>(n);
    for (int i = 0; i < n; i++) {
      var row = new List<string> { perField[0][i].time.ToString("0.000") };
      foreach (var s in perField) {
        row.Add(s[i].value.ToString("0.###"));
      }
      rows.Add(row);
    }
    return (columns, rows);
  }

  private static (string summary, Dictionary<string, string[]> formats, List<string> types,
      IReadOnlyList<(double lat, double lng)> track) Parse(string path) {
    var formats = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    List<string> types;

    using (var log = new DFLogBuffer(path)) {
      types = log.SeenMessageTypes
          .Where(t => !string.IsNullOrEmpty(t))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
          .ToList();

      foreach (var t in types) {
        formats[t] = log.dflog.logformat.TryGetValue(t, out var lbl) && lbl.FieldNames != null
            ? lbl.FieldNames.ToArray()
            : Array.Empty<string>();
      }
    }

    var fullTrack = DataFlashLog.ReadTrack(path);
    var track = fullTrack.Select(p => (p.lat, p.lng)).ToList();
    var summary = BuildSummary(path, types.Count, fullTrack);
    return (summary, formats, types, track);
  }

  private static string BuildSummary(
      string path, int typeCount,
      IReadOnlyList<(double lat, double lng, double alt, DateTime time)> track) {
    var fi = new FileInfo(path);
    string trackInfo;
    if (track.Count > 0) {
      var duration = track[^1].time - track[0].time;
      var maxAlt = track.Max(p => p.alt);
      trackInfo = $"GPS: {track.Count} pts, {duration:hh\\:mm\\:ss}, max alt {maxAlt:0.#} m";
    } else {
      trackInfo = "GPS: no 3D-fix track";
    }
    return $"{fi.Name}\n{fi.Length / 1024.0 / 1024.0:0.0} MB\n{typeCount} msg types\n{trackInfo}";
  }

  // Resolve and parse the bundled graphs/*.xml presets from the upstream submodule (best-effort).
  private static List<GraphPreset> LoadPresets() {
    foreach (var dir in PresetDirs()) {
      var list = GraphPresets.LoadDirectory(dir);
      if (list.Count > 0) {
        return list;
      }
    }
    return new List<GraphPreset>();
  }

  private static IEnumerable<string> PresetDirs() {
    var baseDir = AppContext.BaseDirectory;
    yield return Path.Combine(baseDir, "graphs");
    var dir = new DirectoryInfo(baseDir);
    for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent) {
      yield return Path.Combine(dir.FullName, "external", "MissionPlanner", "graphs");
    }
  }
}
