using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;

namespace MissionPlannerAvalonia.Services;

// Loads a NoFly .kml/.kmz file and exposes a Mapsui layer the map can add (mirrors MP's NoFly
// zones). Parses KML <Polygon> rings and renders each as a closed red outline. Uses only core
// Mapsui (matching the FlightPlannerMap dotted-polyline style) so it needs no extra packages.
// Does NOT touch the map controls — the caller adds the returned layer and wires any toggle.
public static class NoFlyOverlay {
  private static readonly Color NoFlyRed = new(220, 0, 0, 255);

  // Build a NoFly layer from a .kml or .kmz file. Returns null if no polygons were found.
  public static ILayer? BuildLayer(string path, string name = "NoFly") {
    var rings = LoadPolygons(path);
    return rings.Count == 0 ? null : BuildLayer(rings, name);
  }

  // Build a NoFly layer from already-parsed polygon rings (each a list of lat/lng vertices).
  public static ILayer BuildLayer(IReadOnlyList<IReadOnlyList<(double Lat, double Lng)>> rings,
      string name = "NoFly") {
    var layer = new WritableLayer { Name = name };
    foreach (var ring in rings) {
      if (ring.Count < 2) {
        continue;
      }
      var pts = new List<MPoint>(ring.Count + 1);
      foreach (var (lat, lng) in ring) {
        var (x, y) = SphericalMercator.FromLonLat(lng, lat);
        pts.Add(new MPoint(x, y));
      }
      // close the ring
      if (pts.Count > 0 && !pts[0].Equals(pts[^1])) {
        pts.Add(pts[0]);
      }
      AddOutline(layer, pts);
    }
    return layer;
  }

  // Parse all polygon outer-boundary rings from a .kml/.kmz file.
  public static List<IReadOnlyList<(double Lat, double Lng)>> LoadPolygons(string path) {
    var doc = XDocument.Parse(ReadKmlText(path));
    var rings = new List<IReadOnlyList<(double Lat, double Lng)>>();
    foreach (var poly in Descendants(doc.Root, "Polygon")) {
      // outerBoundaryIs/LinearRing/coordinates (fall back to any coordinates under the polygon)
      var coordsEl = Descendants(poly, "outerBoundaryIs")
                         .SelectMany(b => Descendants(b, "coordinates")).FirstOrDefault()
                     ?? Descendants(poly, "coordinates").FirstOrDefault();
      if (coordsEl == null) {
        continue;
      }
      var ring = ParseCoordinates(coordsEl.Value);
      if (ring.Count >= 3) {
        rings.Add(ring);
      }
    }
    return rings;
  }

  // Closed red dotted outline (same style as FlightPlannerMap polylines).
  private static void AddOutline(WritableLayer layer, IReadOnlyList<MPoint> pts) {
    if (pts.Count < 2) {
      return;
    }
    var dot = new SymbolStyle {
      SymbolType = SymbolType.Ellipse,
      Fill = new Brush(NoFlyRed),
      SymbolScale = 6.0 / 30.0,
    };
    for (int i = 1; i < pts.Count; i++) {
      var a = pts[i - 1];
      var b = pts[i];
      double dx = b.X - a.X;
      double dy = b.Y - a.Y;
      double len = Math.Sqrt(dx * dx + dy * dy);
      int steps = Math.Clamp((int)(len / 3.0), 1, 600);
      for (int s = 0; s <= steps; s++) {
        double t = (double)s / steps;
        var f = new PointFeature(new MPoint(a.X + dx * t, a.Y + dy * t));
        f.Styles.Add(dot);
        layer.Add(f);
      }
    }
  }

  // A .kmz is a zip; pull the first .kml entry. A .kml is read directly.
  private static string ReadKmlText(string path) {
    if (path.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase)) {
      using var zip = ZipFile.OpenRead(path);
      var entry = zip.Entries.FirstOrDefault(
                      e => e.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidDataException("No .kml entry inside the .kmz.");
      using var sr = new StreamReader(entry.Open());
      return sr.ReadToEnd();
    }
    return File.ReadAllText(path);
  }

  // "lon,lat[,alt] lon,lat[,alt] …" → list of (lat, lng).
  private static List<(double Lat, double Lng)> ParseCoordinates(string text) {
    var pts = new List<(double, double)>();
    foreach (var tok in text.Split(new[] { ' ', '\n', '\r', '\t' },
                 StringSplitOptions.RemoveEmptyEntries)) {
      var parts = tok.Split(',');
      if (parts.Length >= 2 &&
          double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var lng) &&
          double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat)) {
        pts.Add((lat, lng));
      }
    }
    return pts;
  }

  private static IEnumerable<XElement> Descendants(XElement? root, string localName) =>
      root == null
          ? Enumerable.Empty<XElement>()
          : root.Descendants().Where(e => e.Name.LocalName == localName);
}
