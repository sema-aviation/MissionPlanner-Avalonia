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
  private readonly WritableLayer _waypoints = new() { Name = "Waypoints" };
  private readonly WritableLayer _kml = new() { Name = "KmlTrack" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly GridLayer _graticule = new("Graticule");
  private readonly DispatcherTimer _timer;

  private readonly List<(int Seq, double Lat, double Lng)> _wps = new();
  private ILayer _baseLayer;
  private bool _graticuleOn;
  private bool _centered;
  private int _dragIndex = -1;

  public event Action<int, double, double>? WaypointDragMoved;
  public event Action<int, double, double>? WaypointDragCommitted;

  // Raised on a left click on empty map (no marker hit, no drag) — adds a waypoint there.
  public event Action<double, double>? MapClicked;

  // Lat/lng under the last pointer press — the map context menu (Mission/Fence/Rally) acts here.
  public (double Lat, double Lng) LastClickLatLng { get; private set; }

  private Avalonia.Point _pressPoint;
  private bool _didDrag;

  private readonly WritableLayer _home = new() { Name = "Home" };

  public FlightPlannerMap() {
    var map = new Map { BackColor = new Color(0x26, 0x27, 0x28) };
    _baseLayer = BuildTileLayer("GoogleSatelliteMap");
    map.Layers.Add(_baseLayer);
    map.Layers.Add(_route);
    map.Layers.Add(_kml);
    map.Layers.Add(_home);
    map.Layers.Add(_waypoints);

    _vehicle.Style = MavMarker.Vehicle(0);
    map.Layers.Add(_vehicle);
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

    var line = new List<MPoint>(_wps.Count);
    foreach (var w in _wps) {
      var (x, y) = SphericalMercator.FromLonLat(w.Lng, w.Lat);
      var pt = new MPoint(x, y);
      line.Add(pt);
      _waypoints.Add(BuildMarker(pt, w.Seq));
    }

    AddPolyline(_route, line, new Color(0xFF, 0xCC, 0x00), 4);

    _waypoints.DataHasChanged();
    _route.DataHasChanged();
    RefreshGraphics();
  }

  private static PointFeature BuildMarker(MPoint pt, int seq) {
    var f = new PointFeature(pt);
    f.Styles.Add(new SymbolStyle {
      SymbolType = SymbolType.Ellipse,
      Fill = new Brush(new Color(0xFF, 0xCC, 0x00)),
      Outline = new Pen(Color.Black, 1),
      SymbolScale = 0.6,
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
      _dragIndex = idx;
      e.Handled = true;
    }
  }

  private void OnMapPointerMoved(object? sender, MapEventArgs e) {
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
    if (_dragIndex < 0) {
      return;
    }

    var w = _wps[_dragIndex];
    WaypointDragCommitted?.Invoke(w.Seq, w.Lat, w.Lng);
    _dragIndex = -1;
    e.Handled = true;
  }

  // Click-to-add is detected on the Avalonia pointer route so we can gate on the left button
  // (Mapsui's MapEventArgs doesn't expose which button) and skip it after a marker drag or pan.
  protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e) {
    _pressPoint = e.GetPosition(this);
    _didDrag = false;
    base.OnPointerPressed(e);
  }

  protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e) {
    base.OnPointerReleased(e);
    if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left
        && _dragIndex < 0 && !_didDrag
        && Distance(e.GetPosition(this), _pressPoint) < HitThresholdPx) {
      MapClicked?.Invoke(LastClickLatLng.Lat, LastClickLatLng.Lng);
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
