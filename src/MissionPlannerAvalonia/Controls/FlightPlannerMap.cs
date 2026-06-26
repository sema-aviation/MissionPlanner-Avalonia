using System;
using System.Collections.Generic;
using Avalonia.Threading;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using MissionPlannerAvalonia.ViewModels;
using NetTopologySuite.Geometries;

namespace MissionPlannerAvalonia.Controls;

public class FlightPlannerMap : MapControl {
  private const double HitThresholdPx = 16;

  private readonly WritableLayer _route = new() { Name = "Route" };
  private readonly WritableLayer _drawnPoly = new() { Name = "DrawnPolygon" };
  private readonly WritableLayer _distLabels = new() { Name = "DistLabels" };
  private readonly WritableLayer _midpoints = new() { Name = "Midpoints" };
  private readonly WritableLayer _waypoints = new() { Name = "Waypoints" };
  private readonly WritableLayer _selection = new() { Name = "Selection" };
  private readonly WritableLayer _kml = new() { Name = "KmlTrack" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly GridLayer _graticule = new("Graticule");
  private readonly DispatcherTimer _timer;

  private readonly List<(int Seq, double Lat, double Lng)> _wps = new();
  private readonly List<(MPoint Pt, int AfterSeq)> _midSegs = new();
  private string _renderMode = "Mission";
  private ILayer _baseLayer;
  private bool _graticuleOn;
  private bool _centered;
  private int _dragIndex = -1;

  // Group select/move state (Ctrl+drag a rectangle, then drag any selected marker).
  private readonly HashSet<int> _groupSet = new();
  private readonly Dictionary<int, (double Lat, double Lng)> _groupSnapshot = new();
  private bool _ctrlHeld;
  private bool _rubberbanding;
  private MPoint? _rubberStart;
  private bool _groupDragging;
  private double _groupOriginLat, _groupOriginLng;

  public event Action<int, double, double>? WaypointDragMoved;
  public event Action<int, double, double>? WaypointDragCommitted;

  // Raised on a left click on empty map (no marker hit, no drag) — adds a waypoint there.
  public event Action<double, double>? MapClicked;

  // Raised when a midline "+" half-marker is clicked: insert a WP after this Seq.
  public event Action<int, double, double>? MidpointInsertRequested;

  // Lat/lng under the last pointer press — the map context menu (Mission/Fence/Rally) acts here.
  public (double Lat, double Lng) LastClickLatLng { get; private set; }

  private Avalonia.Point _pressPoint;
  private bool _didDrag;

  private readonly WritableLayer _home = new() { Name = "Home" };
  private readonly WritableLayer _poi = new() { Name = "POI" };

  public FlightPlannerMap() {
    var map = new Map { BackColor = new Color(0x26, 0x27, 0x28) };
    _baseLayer = BuildTileLayer("GoogleSatelliteMap");
    map.Layers.Add(_baseLayer);
    map.Layers.Add(_drawnPoly);
    map.Layers.Add(_route);
    map.Layers.Add(_distLabels);
    map.Layers.Add(_midpoints);
    map.Layers.Add(_kml);
    map.Layers.Add(_home);
    map.Layers.Add(_waypoints);

    map.Layers.Add(_poi);
    _vehicle.Style = MavMarker.Vehicle(0);
    map.Layers.Add(_vehicle);
    map.Layers.Add(_selection);
    // Keep the viewport inside the world extent so you can't zoom/pan past the tiles into gray.
    map.Navigator.Limiter = new Mapsui.Limiting.ViewportLimiterKeepWithinExtent();
    Map = map;

    MapPointerPressed += OnMapPointerPressed;
    MapPointerMoved += OnMapPointerMoved;
    MapPointerReleased += OnMapPointerReleased;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    _timer.Tick += (_, _) => UpdateVehicle();
    _timer.Start();
  }

  public void SetWaypoints(IReadOnlyList<(int Seq, double Lat, double Lng)> wps) {
    _wps.Clear();
    foreach (var w in wps) {
      if (w.Lat == 0 && w.Lng == 0) {
        continue;
      }

      _wps.Add(w);
    }

    RedrawWaypoints();

    if (!_centered && _wps.Count > 0) {
      var (x, y) = SphericalMercator.FromLonLat(_wps[0].Lng, _wps[0].Lat);
      double res = 156543.03392804097 / Math.Pow(2, 17);
      Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), res);
      _centered = true;
    }
  }

  // Draw/move the green "H" home marker (mirrors MP home marker on the planner map).
  public void SetHome(double lat, double lng) {
    _home.Clear();
    if (lat != 0 || lng != 0) {
      var (x, y) = SphericalMercator.FromLonLat(lng, lat);
      var f = new PointFeature(new MPoint(x, y));
      f.Styles.Add(new SymbolStyle {
        SymbolType = SymbolType.Ellipse,
        Fill = new Brush(Color.FromArgb(255, 0, 200, 0)),
        Outline = new Pen(Color.White, 2),
        SymbolScale = 0.7,
      });
      f.Styles.Add(new LabelStyle {
        Text = "H",
        ForeColor = Color.White,
        Font = new Font { Size = 11, Bold = true },
      });
      _home.Add(f);
    }
    _home.DataHasChanged();
  }

  // Mission = yellow open route; Fence = red closed polygon; Rally = lime markers, no connecting line.
  public void SetRenderMode(string mode) {
    _renderMode = mode;
    RedrawWaypoints();
  }

  // Draw the persisted POI markers (magenta dots + name labels), mirroring MP's POI overlay.
  public void ShowPois(IReadOnlyList<(double Lat, double Lng, string Name)> pois) {
    _poi.Clear();
    foreach (var p in pois) {
      if (p.Lat == 0 && p.Lng == 0) {
        continue;
      }
      var (x, y) = SphericalMercator.FromLonLat(p.Lng, p.Lat);
      var f = new PointFeature(new MPoint(x, y));
      f.Styles.Add(new SymbolStyle {
        SymbolType = SymbolType.Ellipse,
        Fill = new Brush(new Color(0xFF, 0x40, 0xFF)),
        Outline = new Pen(Color.White, 1),
        SymbolScale = 0.6,
      });
      f.Styles.Add(new LabelStyle {
        Text = p.Name ?? "",
        ForeColor = Color.White,
        BackColor = new Brush(new Color(0, 0, 0, 140)),
        Font = new Font { Size = 10 },
        Offset = new Offset(0, 14),
      });
      _poi.Add(f);
    }
    _poi.DataHasChanged();
    RefreshGraphics();
  }

  // Draw the user-drawn polygon: vertex dots, plus a semi-transparent closed fill once it has 3+.
  public void ShowDrawnPolygon(IReadOnlyList<(double Lat, double Lng)> pts) {
    _drawnPoly.Clear();
    var coords = new List<Coordinate>(pts.Count + 1);
    foreach (var (lat, lng) in pts) {
      var (x, y) = SphericalMercator.FromLonLat(lng, lat);
      coords.Add(new Coordinate(x, y));
    }

    if (coords.Count >= 3) {
      coords.Add(coords[0]);
      var poly = new Polygon(new LinearRing(coords.ToArray()));
      var f = new GeometryFeature { Geometry = poly };
      f.Styles.Add(new VectorStyle {
        Fill = new Brush(new Color(0x40, 0xA0, 0xFF, 70)),
        Line = new Pen(new Color(0x40, 0xA0, 0xFF), 2),
      });
      _drawnPoly.Add(f);
    } else if (coords.Count == 2) {
      var f = new GeometryFeature { Geometry = new LineString(coords.ToArray()) };
      f.Styles.Add(new VectorStyle { Line = new Pen(new Color(0x40, 0xA0, 0xFF), 2) });
      _drawnPoly.Add(f);
    }

    foreach (var (lat, lng) in pts) {
      var (x, y) = SphericalMercator.FromLonLat(lng, lat);
      var dot = new PointFeature(new MPoint(x, y));
      dot.Styles.Add(new SymbolStyle {
        SymbolType = SymbolType.Ellipse,
        Fill = new Brush(new Color(0x40, 0xA0, 0xFF)),
        Outline = new Pen(Color.White, 1),
        SymbolScale = 0.45,
      });
      _drawnPoly.Add(dot);
    }

    _drawnPoly.DataHasChanged();
    RefreshGraphics();
  }

  public void SetMapType(string type) => ReplaceBase(BuildTileLayer(type));

  public void SetCustomTileSource(string urlTemplate) {
    if (string.IsNullOrWhiteSpace(urlTemplate)) {
      return;
    }

    var src = new HttpTileSource(
        new GlobalSphericalMercator(),
        urlTemplate,
        name: "Custom",
        attribution: new BruTile.Attribution("Custom"));
    ReplaceBase(new TileLayer(src) { Name = "Custom" });
  }

  private void ReplaceBase(TileLayer layer) {
    Map.Layers.Remove(_baseLayer);
    _baseLayer = layer;
    Map.Layers.Add(layer);
    Map.Layers.MoveToBottom(layer);
    RefreshGraphics();
  }

  public void ShowKmlTrack(IReadOnlyList<(double Lat, double Lng)> track) {
    _kml.Clear();
    var pts = new List<MPoint>(track.Count);
    foreach (var (lat, lng) in track) {
      var (x, y) = SphericalMercator.FromLonLat(lng, lat);
      pts.Add(new MPoint(x, y));
    }

    AddPolyline(_kml, pts, new Color(0x00, 0xC8, 0xFF), 3);
    _kml.DataHasChanged();
    RefreshGraphics();
  }

  private Mapsui.Layers.ILayer? _nofly;

  // Add/replace (or clear, when null) the NoFly KMZ overlay layer (mirrors MP's NoFly overlay).
  public void SetNoFlyLayer(Mapsui.Layers.ILayer? layer) {
    if (_nofly != null) {
      Map.Layers.Remove(_nofly);
      _nofly = null;
    }
    if (layer != null) {
      _nofly = layer;
      Map.Layers.Add(layer);
    }
    RefreshGraphics();
  }

  public void SetGraticuleVisible(bool visible) {
    if (visible && !_graticuleOn) {
      Map.Layers.Add(_graticule);
      _graticuleOn = true;
    } else if (!visible && _graticuleOn) {
      Map.Layers.Remove(_graticule);
      _graticuleOn = false;
    }

    RefreshGraphics();
  }

  private void RedrawWaypoints() {
    _waypoints.Clear();
    _route.Clear();
    _distLabels.Clear();
    _midpoints.Clear();
    _midSegs.Clear();

    bool fence = _renderMode == "Fence";
    bool rally = _renderMode == "Rally";
    bool mission = !fence && !rally;
    var markerColor = fence ? new Color(0xFF, 0x40, 0x40)
        : rally ? new Color(0x40, 0xE0, 0x40)
        : new Color(0xFF, 0xCC, 0x00);

    var line = new List<MPoint>(_wps.Count);
    for (int i = 0; i < _wps.Count; i++) {
      var w = _wps[i];
      var (x, y) = SphericalMercator.FromLonLat(w.Lng, w.Lat);
      var pt = new MPoint(x, y);
      line.Add(pt);
      _waypoints.Add(BuildMarker(pt, w.Seq, markerColor, _groupSet.Contains(i)));
    }

    // Rally points are standalone — no connecting line. A fence polygon is closed back to its start.
    if (!rally) {
      if (fence && line.Count >= 3) {
        line.Add(line[0]);
      }
      AddPolyline(_route, line, markerColor, fence ? 3 : 4);
      AddDistanceLabels();
    }

    // Midline "+" half-markers at each leg midpoint (mission mode only).
    if (mission && _wps.Count >= 2) {
      for (int i = 0; i < _wps.Count - 1; i++) {
        var a = _wps[i];
        var b = _wps[i + 1];
        var (mx, my) = SphericalMercator.FromLonLat((a.Lng + b.Lng) / 2, (a.Lat + b.Lat) / 2);
        var mp = new MPoint(mx, my);
        _midSegs.Add((mp, a.Seq));
        var f = new PointFeature(mp);
        f.Styles.Add(new LabelStyle {
          Text = "+",
          ForeColor = Color.White,
          BackColor = new Brush(new Color(0, 120, 0, 180)),
          Font = new Font { Size = 13, Bold = true },
        });
        _midpoints.Add(f);
      }
    }

    _waypoints.DataHasChanged();
    _route.DataHasChanged();
    _distLabels.DataHasChanged();
    _midpoints.DataHasChanged();
    RefreshGraphics();
  }

  // Per-leg distance labels at segment midpoints (mirrors MP's on-map distance overlay).
  private void AddDistanceLabels() {
    for (int i = 1; i < _wps.Count; i++) {
      var a = _wps[i - 1];
      var b = _wps[i];
      double d = Haversine(a.Lat, a.Lng, b.Lat, b.Lng);
      var (x, y) = SphericalMercator.FromLonLat((a.Lng + b.Lng) / 2, (a.Lat + b.Lat) / 2);
      var f = new PointFeature(new MPoint(x, y));
      f.Styles.Add(new LabelStyle {
        Text = d >= 1000 ? (d / 1000).ToString("0.0") + "km" : d.ToString("0") + "m",
        ForeColor = Color.White,
        BackColor = new Brush(new Color(0, 0, 0, 140)),
        Font = new Font { Size = 10 },
      });
      _distLabels.Add(f);
    }
  }

  private static double Haversine(double lat1, double lng1, double lat2, double lng2) {
    const double r = 6378137.0;
    double dLat = (lat2 - lat1) * Math.PI / 180;
    double dLng = (lng2 - lng1) * Math.PI / 180;
    double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
        + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
        * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
    return r * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
  }

  private static PointFeature BuildMarker(MPoint pt, int seq, Color fill, bool selected) {
    var f = new PointFeature(pt);
    f.Styles.Add(new SymbolStyle {
      SymbolType = SymbolType.Ellipse,
      Fill = new Brush(fill),
      Outline = new Pen(selected ? new Color(0x00, 0xB0, 0xFF) : Color.Black, selected ? 3 : 1),
      SymbolScale = selected ? 0.8 : 0.6,
    });
    f.Styles.Add(new LabelStyle {
      Text = seq.ToString(),
      ForeColor = Color.Black,
      BackColor = new Brush(Color.Transparent),
      Font = new Font { Size = 11, Bold = true },
    });
    return f;
  }

  private static void AddPolyline(WritableLayer layer, IReadOnlyList<MPoint> pts, Color color,
      double widthPx) {
    if (pts.Count < 2) {
      return;
    }

    var coords = new Coordinate[pts.Count];
    for (int i = 0; i < pts.Count; i++) {
      coords[i] = new Coordinate(pts[i].X, pts[i].Y);
    }

    var feature = new GeometryFeature { Geometry = new LineString(coords) };
    feature.Styles.Add(new VectorStyle { Line = new Pen(color, widthPx) });
    layer.Add(feature);
  }

  private static TileLayer BuildTileLayer(string type) {
    string url = type switch {
      "GoogleSatelliteMap" => "https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}",
      "GoogleHybridMap" => "https://mt1.google.com/vt/lyrs=y&x={x}&y={y}&z={z}",
      "OpenStreetMap" => "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
      _ =>
          "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    };
    var src = new HttpTileSource(
        new GlobalSphericalMercator(),
        url,
        name: type,
        attribution: new BruTile.Attribution(type));
    return new TileLayer(src) { Name = type };
  }

  private void OnMapPointerPressed(object? sender, MapEventArgs e) {
    var (lng, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
    LastClickLatLng = (lat, lng);
    int idx = HitTest(e.ScreenPosition);
    if (idx >= 0) {
      // Grabbing a marker that's part of the current group selection moves the whole group.
      if (_groupSet.Count > 0 && _groupSet.Contains(idx)) {
        _groupDragging = true;
        _groupOriginLat = lat;
        _groupOriginLng = lng;
        _groupSnapshot.Clear();
        foreach (var gi in _groupSet) {
          _groupSnapshot[gi] = (_wps[gi].Lat, _wps[gi].Lng);
        }
        e.Handled = true;
        return;
      }

      // Grabbing any other marker clears the selection and starts a single-marker drag.
      if (_groupSet.Count > 0) {
        _groupSet.Clear();
        RedrawWaypoints();
      }
      _dragIndex = idx;
      e.Handled = true;
      return;
    }

    // Ctrl+drag on empty map starts a rubber-band group selection.
    if (_ctrlHeld) {
      _rubberbanding = true;
      _rubberStart = e.WorldPosition;
      e.Handled = true;
    }
  }

  private void OnMapPointerMoved(object? sender, MapEventArgs e) {
    if (_groupDragging) {
      var (glng, glat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
      double dLat = glat - _groupOriginLat;
      double dLng = glng - _groupOriginLng;
      _didDrag = true;
      foreach (var kv in _groupSnapshot) {
        int gi = kv.Key;
        double nlat = kv.Value.Lat + dLat;
        double nlng = kv.Value.Lng + dLng;
        var gw = _wps[gi];
        _wps[gi] = (gw.Seq, nlat, nlng);
        WaypointDragMoved?.Invoke(gw.Seq, nlat, nlng);
      }
      RedrawWaypoints();
      e.Handled = true;
      return;
    }

    if (_rubberbanding && _rubberStart is { } rs) {
      _didDrag = true;
      DrawRubberBand(rs, e.WorldPosition);
      e.Handled = true;
      return;
    }

    if (_dragIndex < 0) {
      return;
    }

    var (lng, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
    _didDrag = true;
    var w = _wps[_dragIndex];
    _wps[_dragIndex] = (w.Seq, lat, lng);
    RedrawWaypoints();
    WaypointDragMoved?.Invoke(w.Seq, lat, lng);
    e.Handled = true;
  }

  private void OnMapPointerReleased(object? sender, MapEventArgs e) {
    if (_groupDragging) {
      foreach (var kv in _groupSnapshot) {
        var gw = _wps[kv.Key];
        WaypointDragCommitted?.Invoke(gw.Seq, gw.Lat, gw.Lng);
      }
      _groupDragging = false;
      e.Handled = true;
      return;
    }

    if (_rubberbanding && _rubberStart is { } rs) {
      SelectInRect(rs, e.WorldPosition);
      _rubberbanding = false;
      _rubberStart = null;
      _selection.Clear();
      _selection.DataHasChanged();
      RedrawWaypoints();
      e.Handled = true;
      return;
    }

    if (_dragIndex < 0) {
      return;
    }

    var w = _wps[_dragIndex];
    WaypointDragCommitted?.Invoke(w.Seq, w.Lat, w.Lng);
    _dragIndex = -1;
    e.Handled = true;
  }

  private void DrawRubberBand(MPoint a, MPoint b) {
    _selection.Clear();
    double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
    double minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
    var ring = new LinearRing(new[] {
      new Coordinate(minX, minY),
      new Coordinate(maxX, minY),
      new Coordinate(maxX, maxY),
      new Coordinate(minX, maxY),
      new Coordinate(minX, minY),
    });
    var f = new GeometryFeature { Geometry = new Polygon(ring) };
    f.Styles.Add(new VectorStyle {
      Fill = new Brush(new Color(0x00, 0xB0, 0xFF, 50)),
      Line = new Pen(new Color(0x00, 0xB0, 0xFF), 1),
    });
    _selection.Add(f);
    _selection.DataHasChanged();
    RefreshGraphics();
  }

  private void SelectInRect(MPoint a, MPoint b) {
    double minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
    double minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
    _groupSet.Clear();
    for (int i = 0; i < _wps.Count; i++) {
      var (x, y) = SphericalMercator.FromLonLat(_wps[i].Lng, _wps[i].Lat);
      if (x >= minX && x <= maxX && y >= minY && y <= maxY) {
        _groupSet.Add(i);
      }
    }
  }

  // Click-to-add is detected on the Avalonia pointer route so we can gate on the left button
  // (Mapsui's MapEventArgs doesn't expose which button) and skip it after a marker drag or pan.
  protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e) {
    _pressPoint = e.GetPosition(this);
    _didDrag = false;
    _ctrlHeld = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
    base.OnPointerPressed(e);
  }

  protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e) {
    base.OnPointerReleased(e);
    if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left
        && _dragIndex < 0 && !_didDrag && !_ctrlHeld
        && Distance(e.GetPosition(this), _pressPoint) < HitThresholdPx) {
      // A click on a midline "+" inserts a WP there; otherwise it adds one at the click.
      int after = HitTestMidpoint(e.GetPosition(this));
      if (after >= 0) {
        MidpointInsertRequested?.Invoke(after, LastClickLatLng.Lat, LastClickLatLng.Lng);
      } else {
        MapClicked?.Invoke(LastClickLatLng.Lat, LastClickLatLng.Lng);
      }
    }
  }

  private static double Distance(Avalonia.Point a, Avalonia.Point b) {
    double dx = a.X - b.X, dy = a.Y - b.Y;
    return System.Math.Sqrt(dx * dx + dy * dy);
  }

  private int HitTest(Mapsui.Manipulations.ScreenPosition screen) {
    var vp = Map.Navigator.Viewport;
    double best = HitThresholdPx;
    int found = -1;
    for (int i = 0; i < _wps.Count; i++) {
      var (x, y) = SphericalMercator.FromLonLat(_wps[i].Lng, _wps[i].Lat);
      var sp = vp.WorldToScreen(x, y);
      double d = sp.Distance(screen);
      if (d < best) {
        best = d;
        found = i;
      }
    }

    return found;
  }

  private int HitTestMidpoint(Avalonia.Point screen) {
    var vp = Map.Navigator.Viewport;
    var sp = new Mapsui.Manipulations.ScreenPosition(screen.X, screen.Y);
    double best = HitThresholdPx;
    int after = -1;
    foreach (var (pt, seq) in _midSegs) {
      var s = vp.WorldToScreen(pt.X, pt.Y);
      double d = s.Distance(sp);
      if (d < best) {
        best = d;
        after = seq;
      }
    }

    return after;
  }

  private void UpdateVehicle() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null || (cs.lat == 0 && cs.lng == 0)) {
      return;
    }

    var (x, y) = SphericalMercator.FromLonLat(cs.lng, cs.lat);
    var pt = new MPoint(x, y);
    _vehicle.Style = MavMarker.Vehicle(cs.yaw);
    _vehicle.Clear();
    _vehicle.Add(new PointFeature(pt));
    _vehicle.DataHasChanged();

    if (!_centered) {
      double res = 156543.03392804097 / Math.Pow(2, 16);
      Map.Navigator.CenterOnAndZoomTo(pt, res);
      _centered = true;
    }
  }
}
