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
using NetTopologySuite.Geometries;

namespace MissionPlannerAvalonia.Controls;

public class FlightPlannerMap : MapControl {
  private const double _hitThresholdPx = 16;

  private readonly WritableLayer _route = new() { Name = "Route" };
  private readonly WritableLayer _drawnPoly = new() { Name = "DrawnPolygon" };
  private readonly WritableLayer _distLabels = new() { Name = "DistLabels" };
  private readonly WritableLayer _midpoints = new() { Name = "Midpoints" };
  private readonly WritableLayer _rings = new() { Name = "WpRings" };
  private readonly WritableLayer _waypoints = new() { Name = "Waypoints" };
  private readonly WritableLayer _selection = new() { Name = "Selection" };
  private readonly WritableLayer _kml = new() { Name = "KmlTrack" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly GridLayer _graticule = new("Graticule");
  private readonly DispatcherTimer _timer;

  private readonly
      List<(int Seq, double Lat, double Lng, ushort Cmd, double P1, double P2, double P3, double P4)>
      _wps = new();
  private double _wpRadius;
  private double _loiterRadius;
  private (double Lat, double Lng) _homeLatLng;
  private readonly List<(MPoint Pt, int AfterSeq)> _midSegs = new();
  private string _renderMode = "Mission";
  private ILayer _baseLayer;
  private bool _graticuleOn;
  private bool _centered;
  private int _dragIndex = -1;

  private readonly HashSet<int> _groupSet = new();
  private readonly Dictionary<int, (double Lat, double Lng)> _groupSnapshot = new();
  private bool _ctrlHeld;
  private bool _rubberbanding;
  private MPoint? _rubberStart;
  private bool _groupDragging;
  private double _groupOriginLat, _groupOriginLng;

  public event Action<int, double, double>? WaypointDragMoved;
  public event Action<int, double, double>? WaypointDragCommitted;

  public event Action<double, double>? MapClicked;

  public event Action<int, double, double>? MidpointInsertRequested;

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
    map.Layers.Add(_rings);
    map.Layers.Add(_waypoints);

    map.Layers.Add(_poi);
    _vehicle.Style = MavMarker.Vehicle(0);
    map.Layers.Add(_vehicle);
    map.Layers.Add(_selection);

    map.Navigator.Limiter = new Mapsui.Limiting.ViewportLimiterKeepWithinExtent();
    Map = map;

    MapPointerPressed += OnMapPointerPressed;
    MapPointerMoved += OnMapPointerMoved;
    MapPointerReleased += OnMapPointerReleased;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    _timer.Tick += (_, _) => UpdateVehicle();
    _timer.Start();
  }

  public void SetWaypoints(
      IReadOnlyList<(int Seq, double Lat, double Lng, ushort Cmd, double P1, double P2, double P3,
          double P4)> wps,
      double wpRadius, double loiterRadius) {
    _wpRadius = wpRadius;
    _loiterRadius = loiterRadius;
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

  public void SetHome(double lat, double lng) {
    _homeLatLng = (lat, lng);
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
    RedrawWaypoints();
  }

  public void SetRenderMode(string mode) {
    _renderMode = mode;
    RedrawWaypoints();
  }

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
    _rings.Clear();
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
      var (radius, ringColor, ringFill) = RingFor(w.Cmd, w.P1, w.P2, w.P3);
      if (radius > 0) {
        _rings.Add(BuildRing(w.Lng, w.Lat, radius, ringColor, ringFill));
      }
      _waypoints.Add(BuildMarker(pt, w.Seq, markerColor, _groupSet.Contains(i)));
    }

    if (!rally) {
      if (fence && line.Count >= 3) {
        line.Add(line[0]);
        AddPolyline(_route, line, markerColor, 3);
      } else {
        AddRoute(line, markerColor, 4);
      }
      AddDistanceLabels();
    }

    if (mission && line.Count > 2 && (_homeLatLng.Lat != 0 || _homeLatLng.Lng != 0)) {
      var (hx, hy) = SphericalMercator.FromLonLat(_homeLatLng.Lng, _homeLatLng.Lat);
      var hp = new MPoint(hx, hy);
      double dLast = Haversine(_homeLatLng.Lat, _homeLatLng.Lng, _wps[^1].Lat, _wps[^1].Lng);
      double dFirst = Haversine(_homeLatLng.Lat, _homeLatLng.Lng, _wps[0].Lat, _wps[0].Lng);
      bool dash = dLast < 5000 && dFirst < 5000;
      AddPolyline(_route, new[] { line[^1], hp, line[0] }, new Color(255, 255, 0), 2, dash);
    }

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
    _rings.DataHasChanged();
    _route.DataHasChanged();
    _distLabels.DataHasChanged();
    _midpoints.DataHasChanged();
    RefreshGraphics();
  }

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

  private (double Radius, Color Color, Color? Fill) RingFor(ushort cmd, double p1, double p2,
      double p3) {
    var lightBlue = new Color(173, 216, 230);
    switch ((MAVLink.MAV_CMD)cmd) {
      case MAVLink.MAV_CMD.SPLINE_WAYPOINT:
        return (_wpRadius, new Color(0, 128, 0), null);
      case MAVLink.MAV_CMD.LOITER_TURNS:
      case MAVLink.MAV_CMD.LOITER_UNLIM:
        return (Math.Abs(p3 != 0 ? p3 : _loiterRadius), lightBlue, null);
      case MAVLink.MAV_CMD.LOITER_TO_ALT:
        return (Math.Abs(p2 != 0 ? p2 : _loiterRadius), lightBlue, null);
      case MAVLink.MAV_CMD.LOITER_TIME:
        return (Math.Abs(_loiterRadius), lightBlue, null);
      case MAVLink.MAV_CMD.FENCE_CIRCLE_INCLUSION:
        return (p1, new Color(0, 0, 255), null);
      case MAVLink.MAV_CMD.FENCE_CIRCLE_EXCLUSION:
        return (p1, new Color(255, 0, 0), new Color(255, 0, 0, 30));
      case MAVLink.MAV_CMD.WAYPOINT:
      case MAVLink.MAV_CMD.TAKEOFF:
      case MAVLink.MAV_CMD.VTOL_TAKEOFF:
      case MAVLink.MAV_CMD.LAND:
      case MAVLink.MAV_CMD.VTOL_LAND:
      case MAVLink.MAV_CMD.DO_LAND_START:
        return (_wpRadius, Color.White, null);
      default:
        return (0, Color.White, null);
    }
  }

  private static GeometryFeature BuildRing(double lng, double lat, double radiusM, Color color,
      Color? fill) {
    var (cx, cy) = SphericalMercator.FromLonLat(lng, lat);
    double mr = radiusM / Math.Cos(lat * Math.PI / 180.0);
    const int seg = 48;
    var coords = new Coordinate[seg + 1];
    for (int i = 0; i <= seg; i++) {
      double t = 2 * Math.PI * i / seg;
      coords[i] = new Coordinate(cx + Math.Cos(t) * mr, cy + Math.Sin(t) * mr);
    }

    if (fill.HasValue) {
      var poly = new GeometryFeature { Geometry = new Polygon(new LinearRing(coords)) };
      poly.Styles.Add(new VectorStyle {
        Outline = new Pen(color, 2) { PenStyle = PenStyle.Dash },
        Line = new Pen(color, 2) { PenStyle = PenStyle.Dash },
        Fill = new Brush(fill.Value),
      });
      return poly;
    }

    var f = new GeometryFeature { Geometry = new LineString(coords) };
    f.Styles.Add(new VectorStyle { Line = new Pen(color, 2) { PenStyle = PenStyle.Dash } });
    return f;
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
      double widthPx, bool dash = false) {
    if (pts.Count < 2) {
      return;
    }

    var coords = new Coordinate[pts.Count];
    for (int i = 0; i < pts.Count; i++) {
      coords[i] = new Coordinate(pts[i].X, pts[i].Y);
    }

    var feature = new GeometryFeature { Geometry = new LineString(coords) };
    var pen = new Pen(color, widthPx);
    if (dash) {
      pen.PenStyle = PenStyle.Dash;
    }
    feature.Styles.Add(new VectorStyle { Line = pen });
    layer.Add(feature);
  }

  // ponytail: centripetal Catmull-Rom is a rendering stand-in for upstream Spline2 physics
  private void AddRoute(IReadOnlyList<MPoint> pts, Color color, double widthPx) {
    if (pts.Count < 2) {
      return;
    }

    var outc = new List<Coordinate> { new(pts[0].X, pts[0].Y) };
    for (int i = 0; i < pts.Count - 1; i++) {
      bool spline = (MAVLink.MAV_CMD)_wps[i + 1].Cmd == MAVLink.MAV_CMD.SPLINE_WAYPOINT;
      if (spline) {
        var p0 = pts[Math.Max(0, i - 1)];
        var p1 = pts[i];
        var p2 = pts[i + 1];
        var p3 = pts[Math.Min(pts.Count - 1, i + 2)];
        const int sub = 12;
        for (int s = 1; s <= sub; s++) {
          outc.Add(CatmullRom(p0, p1, p2, p3, (double)s / sub));
        }
      } else {
        outc.Add(new Coordinate(pts[i + 1].X, pts[i + 1].Y));
      }
    }

    var feature = new GeometryFeature { Geometry = new LineString(outc.ToArray()) };
    feature.Styles.Add(new VectorStyle { Line = new Pen(color, widthPx) });
    _route.Add(feature);
  }

  private static Coordinate CatmullRom(MPoint p0, MPoint p1, MPoint p2, MPoint p3, double t) {
    double t2 = t * t;
    double t3 = t2 * t;
    double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * t
        + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2
        + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
    double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * t
        + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2
        + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);
    return new Coordinate(x, y);
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

      if (_groupSet.Count > 0) {
        _groupSet.Clear();
        RedrawWaypoints();
      }
      _dragIndex = idx;
      e.Handled = true;
      return;
    }

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
        gw.Lat = nlat;
        gw.Lng = nlng;
        _wps[gi] = gw;
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
    w.Lat = lat;
    w.Lng = lng;
    _wps[_dragIndex] = w;
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
        && Distance(e.GetPosition(this), _pressPoint) < _hitThresholdPx) {

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
    double best = _hitThresholdPx;
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
    double best = _hitThresholdPx;
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

  public void ZoomToHome() {
    if (_homeLatLng.Lat == 0 && _homeLatLng.Lng == 0) {
      return;
    }
    var (x, y) = SphericalMercator.FromLonLat(_homeLatLng.Lng, _homeLatLng.Lat);
    Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 156543.03392804097 / Math.Pow(2, 17));
  }

  public void ZoomToVehicle() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null || (cs.lat == 0 && cs.lng == 0)) {
      return;
    }
    var (x, y) = SphericalMercator.FromLonLat(cs.lng, cs.lat);
    Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 156543.03392804097 / Math.Pow(2, 17));
  }

  public void ZoomToMission() {
    if (_wps.Count == 0) {
      return;
    }
    double minX = double.MaxValue, minY = double.MaxValue;
    double maxX = double.MinValue, maxY = double.MinValue;
    foreach (var w in _wps) {
      var (x, y) = SphericalMercator.FromLonLat(w.Lng, w.Lat);
      minX = Math.Min(minX, x);
      minY = Math.Min(minY, y);
      maxX = Math.Max(maxX, x);
      maxY = Math.Max(maxY, y);
    }
    if (maxX - minX < 1 && maxY - minY < 1) {
      Map.Navigator.CenterOnAndZoomTo(new MPoint(minX, minY), 156543.03392804097 / Math.Pow(2, 17));
      return;
    }
    double padX = (maxX - minX) * 0.1, padY = (maxY - minY) * 0.1;
    Map.Navigator.ZoomToBox(new MRect(minX - padX, minY - padY, maxX + padX, maxY + padY));
  }

  public void RotateBy(double deg) => Map.Navigator.RotateTo(Map.Navigator.Viewport.Rotation + deg);

  public void ResetRotation() => Map.Navigator.RotateTo(0);

  public void SetZoomLevel(double level) =>
      Map.Navigator.ZoomTo(156543.03392804097 / Math.Pow(2, level));
}
