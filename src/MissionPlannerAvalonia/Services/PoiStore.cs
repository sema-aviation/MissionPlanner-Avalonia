using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

// Points of Interest store (mirrors MP Utilities/POI.cs). Persists POIs to a tab-separated text
// file under the app data dir. Upstream stored "lat\tlng\tname"; we add an altitude column
// ("lat\tlng\talt\tname") while still loading the legacy 3-column form.
public sealed record PoiPoint(double Lat, double Lng, double Alt, string Name);

public static class PoiStore {
  // Same location/name as upstream (Settings.GetUserDataDirectory() + "poi.txt").
  public static string FilePath {
    get {
      try {
        return Path.Combine(Settings.GetUserDataDirectory(), "poi.txt");
      } catch {
        return Path.Combine(Path.GetTempPath(), "MissionPlannerAvalonia", "poi.txt");
      }
    }
  }

  private static readonly List<PoiPoint> Points = new();

  public static IReadOnlyList<PoiPoint> All => Points;

  public static void Add(PoiPoint p) {
    Points.Add(p);
    Save();
  }

  public static void Add(double lat, double lng, double alt, string name) =>
      Add(new PoiPoint(lat, lng, alt, name));

  public static bool Remove(PoiPoint p) {
    bool removed = Points.Remove(p);
    if (removed) {
      Save();
    }
    return removed;
  }

  public static void Clear() {
    Points.Clear();
    Save();
  }

  // Load from the default file (no-op if it does not exist).
  public static void Load() => Load(FilePath);

  public static void Load(string path) {
    Points.Clear();
    if (!File.Exists(path)) {
      return;
    }
    foreach (var line in File.ReadAllLines(path)) {
      if (string.IsNullOrWhiteSpace(line)) {
        continue;
      }
      var f = line.Split('\t');
      if (f.Length < 3) {
        continue;
      }
      if (!TryD(f[0], out var lat) || !TryD(f[1], out var lng)) {
        continue;
      }
      // 4-col: lat,lng,alt,name ; legacy 3-col: lat,lng,name
      if (f.Length >= 4 && TryD(f[2], out var alt)) {
        Points.Add(new PoiPoint(lat, lng, alt, f[3]));
      } else {
        Points.Add(new PoiPoint(lat, lng, 0, f[2]));
      }
    }
  }

  // Save to the default file.
  public static void Save() => Save(FilePath);

  public static void Save(string path) {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir)) {
      Directory.CreateDirectory(dir);
    }
    using var sw = new StreamWriter(path, false);
    foreach (var p in Points) {
      sw.WriteLine(string.Join("\t", new[] {
        p.Lat.ToString(CultureInfo.InvariantCulture),
        p.Lng.ToString(CultureInfo.InvariantCulture),
        p.Alt.ToString(CultureInfo.InvariantCulture),
        p.Name ?? "",
      }));
    }
  }

  private static bool TryD(string s, out double v) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
}
