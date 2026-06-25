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

public partial class LogBrowseViewModel : ViewModelBase {
  private readonly Dictionary<string, string[]> _formats =
      new(StringComparer.OrdinalIgnoreCase);

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
  private bool _busy;

  public ObservableCollection<string> MessageTypes { get; } = new();
  public ObservableCollection<string> Fields { get; } = new();

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
      var (summary, formats, types) = await Task.Run(() => Parse(path));
      _formats.Clear();
      foreach (var kv in formats) {
        _formats[kv.Key] = kv.Value;
      }
      MessageTypes.Clear();
      foreach (var t in types) {
        MessageTypes.Add(t);
      }
      Info = summary;
      SelectedType = MessageTypes.FirstOrDefault();
      Status = $"Loaded {types.Count} message types.";
    } catch (Exception ex) {
      Info = $"Failed to read log:\n{ex.Message}";
      Status = "Parse failed.";
    } finally {
      Busy = false;
    }
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

  private static (string summary, Dictionary<string, string[]> formats, List<string> types)
      Parse(string path) {
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

    var summary = BuildSummary(path, types.Count);
    return (summary, formats, types);
  }

  private static string BuildSummary(string path, int typeCount) {
    var fi = new FileInfo(path);
    string trackInfo;
    try {
      var track = DataFlashLog.ReadTrack(path);
      if (track.Count > 0) {
        var duration = track[^1].time - track[0].time;
        var maxAlt = track.Max(p => p.alt);
        trackInfo = $"GPS: {track.Count} pts, {duration:hh\\:mm\\:ss}, max alt {maxAlt:0.#} m";
      } else {
        trackInfo = "GPS: no 3D-fix track";
      }
    } catch (Exception ex) {
      trackInfo = $"GPS: {ex.Message}";
    }

    return $"{fi.Name}\n{fi.Length / 1024.0 / 1024.0:0.0} MB\n{typeCount} msg types\n{trackInfo}";
  }
}
