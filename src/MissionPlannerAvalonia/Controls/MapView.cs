using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
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

public class MapView : MapControl {
  private readonly WritableLayer _track = new() { Name = "Track" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly DispatcherTimer _timer;
  private bool _centered;
  private MPoint? _lastTrackPt;
  private readonly List<Coordinate> _trackPts = new();

  public (double Lat, double Lng) LastClickLatLng { get; private set; }

  public event Action<double, double>? MapLeftClicked;

  public bool AutoPan { get; set; }

  public static readonly StyledProperty<int> ZoomLevelProperty =
      AvaloniaProperty.Register<MapView, int>(nameof(ZoomLevel), 16);

  public int ZoomLevel {
    get => GetValue(ZoomLevelProperty);
    set => SetValue(ZoomLevelProperty, value);
  }

  public event Action<double, double>? CursorMoved;

  static MapView() {
    ZoomLevelProperty.Changed.AddClassHandler<MapView>((m, e) => m.SetZoomLevel((int)e.NewValue!));
  }

  private static double ResolutionForLevel(int level) =>
      156543.03392804097 / Math.Pow(2, level);

  public void SetZoomLevel(int level) {
    level = Math.Clamp(level, 1, 21);
    try {
      Map?.Navigator.ZoomTo(ResolutionForLevel(level));
    } catch {

    }
  }

  public MapView() {

    var map = new Map { BackColor = new Color(0x26, 0x27, 0x28) };
    var esri = new HttpTileSource(
        new GlobalSphericalMercator(),
        "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
        name: "Esri World Imagery",
        attribution: new BruTile.Attribution("© Esri")
    );
    map.Layers.Add(new TileLayer(esri) { Name = "Satellite" });

    map.Layers.Add(_track);
    _vehicle.Style = MavMarker.Vehicle(0);
    map.Layers.Add(_vehicle);

    map.Navigator.Limiter = new Mapsui.Limiting.ViewportLimiterKeepWithinExtent();
    Map = map;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    _timer.Tick += (_, _) => UpdateVehicle();
    _timer.Start();
  }

  public bool LiveVehicle { get; set; } = true;

  private void UpdateVehicle() {
    if (!LiveVehicle) {
      return;
    }
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null || (cs.lat == 0 && cs.lng == 0)) {
      return;
    }

    var (x, y) = SphericalMercator.FromLonLat(cs.lng, cs.lat);
    var pt = new MPoint(x, y);
    _vehicle.Style = MavMarker.Vehicle(cs.yaw);
    _vehicle.Clear();
    _vehicle.Add(new PointFeature(pt));

    DrawBearingOverlays(cs, pt);
    _vehicle.DataHasChanged();

    AppendTrack(pt);

    if (!_centered) {
      double res = 156543.03392804097 / Math.Pow(2, 16);
      Map.Navigator.CenterOnAndZoomTo(pt, res);
      _centered = true;
    } else if (AutoPan) {
      Map.Navigator.CenterOn(pt);
    }
  }

  private static readonly VectorStyle _trackStyle = new() {
    Line = new Pen(new Color(255, 220, 30), 2),
  };

  private static readonly VectorStyle _headingStyle = new() {
    Line = new Pen(new Color(255, 0, 0), 2),
  };
  private static readonly VectorStyle _cogStyle = new() {
    Line = new Pen(new Color(0, 0, 0), 2),
  };
  private static readonly VectorStyle _navBearingStyle = new() {
    Line = new Pen(new Color(0, 128, 0), 2),
  };
  private static readonly VectorStyle _targetStyle = new() {
    Line = new Pen(new Color(255, 165, 0), 2),
  };
  private static readonly VectorStyle _radiusStyle = new() {
    Line = new Pen(new Color(255, 105, 180), 2),
  };

  private void DrawBearingOverlays(MissionPlanner.CurrentState cs, MPoint pt) {
    double resMpp = Map.Navigator.Viewport.Resolution;
    if (resMpp <= 0) {
      return;
    }

    var s = MissionPlanner.Utilities.Settings.Instance;
    double lenPx = s.GetInt32("GMapMarkerBase_Length", 500);
    double len = lenPx * resMpp;

    if (s.GetBoolean("GMapMarkerBase_DisplayHeading", true)) {
      AddBearingLine(pt, cs.yaw, len, _headingStyle);
    }
    if (s.GetBoolean("GMapMarkerBase_DisplayNavBearing", true)) {
      AddBearingLine(pt, cs.nav_bearing, len, _navBearingStyle);
    }
    if (s.GetBoolean("GMapMarkerBase_DisplayCOG", true)) {
      AddBearingLine(pt, cs.groundcourse, len, _cogStyle);
    }
    if (s.GetBoolean("GMapMarkerBase_DisplayTarget", true)) {
      AddBearingLine(pt, cs.target_bearing, len, _targetStyle);
    }
    if (s.GetBoolean("GMapMarkerBase_DisplayRadius", true)) {
      AddRadiusArc(pt, cs.groundcourse, cs.radius, resMpp);
    }
  }

  private void AddBearingLine(MPoint pt, double bearingDeg, double len, VectorStyle style) {
    double rad = bearingDeg * Math.PI / 180.0;
    var end = new MPoint(pt.X + Math.Sin(rad) * len, pt.Y + Math.Cos(rad) * len);
    var line = new GeometryFeature {
      Geometry = new LineString(new[] { new Coordinate(pt.X, pt.Y), new Coordinate(end.X, end.Y) }),
    };
    line.Styles.Add(style);
    _vehicle.Add(line);
  }

  private void AddRadiusArc(MPoint pt, double cogDeg, double radius, double resMpp) {
    if (Math.Abs(radius) <= 1) {
      return;
    }

    const double desiredLeadDist = 100.0;
    double m2pixelwidth = 1.0 / resMpp;
    double alpha = desiredLeadDist * m2pixelwidth / radius * (180.0 / Math.PI);
    if (Math.Abs(alpha) <= 1) {
      return;
    }
    alpha = Math.Clamp(alpha, -360.0, 360.0);

    double radiusM = radius;
    double cog = cogDeg * Math.PI / 180.0;
    double cx = pt.X + Math.Cos(cog) * radiusM;
    double cy = pt.Y + Math.Sin(cog) * radiusM;
    double start = (cogDeg - 180.0) * Math.PI / 180.0;

    var coords = new List<Coordinate>();
    const int steps = 24;
    for (int i = 0; i <= steps; i++) {
      double theta = start + alpha * (Math.PI / 180.0) * i / steps;
      coords.Add(new Coordinate(cx + Math.Cos(theta) * radiusM, cy + Math.Sin(theta) * radiusM));
    }
    var arc = new GeometryFeature { Geometry = new LineString(coords.ToArray()) };
    arc.Styles.Add(_radiusStyle);
    _vehicle.Add(arc);
  }

  private void AppendTrack(MPoint pt) {
    if (_lastTrackPt is { } prev) {
      double dx = pt.X - prev.X, dy = pt.Y - prev.Y;
      if (Math.Sqrt(dx * dx + dy * dy) < 0.5) {
        return;
      }
    }
    _lastTrackPt = pt;
    _trackPts.Add(new Coordinate(pt.X, pt.Y));
    if (_trackPts.Count > 5000) {
      _trackPts.RemoveAt(0);
    }
    if (_trackPts.Count < 2) {
      return;
    }

    var line = new GeometryFeature { Geometry = new LineString(_trackPts.ToArray()) };
    line.Styles.Add(_trackStyle);
    _track.Clear();
    _track.Add(line);
    _track.DataHasChanged();
  }

  public void ShowStaticTrack(IReadOnlyList<(double Lat, double Lng)> pts) {
    _track.Clear();
    _trackPts.Clear();
    if (pts.Count == 0) {
      _track.DataHasChanged();
      return;
    }
    foreach (var (lat, lng) in pts) {
      if (lat == 0 && lng == 0) {
        continue;
      }
      var (x, y) = SphericalMercator.FromLonLat(lng, lat);
      _trackPts.Add(new Coordinate(x, y));
    }
    if (_trackPts.Count >= 2) {
      var line = new GeometryFeature { Geometry = new LineString(_trackPts.ToArray()) };
      line.Styles.Add(_trackStyle);
      _track.Add(line);
    }
    _track.DataHasChanged();
    if (_trackPts.Count > 0) {
      double res = 156543.03392804097 / Math.Pow(2, 15);
      Map.Navigator.CenterOnAndZoomTo(new MPoint(_trackPts[0].X, _trackPts[0].Y), res);
      _centered = true;
    }
  }

  public void ShowSampleMarker(double lat, double lng) {
    if (lat == 0 && lng == 0) {
      return;
    }
    var (x, y) = SphericalMercator.FromLonLat(lng, lat);
    var pt = new MPoint(x, y);
    _vehicle.Style = MavMarker.Vehicle(0);
    _vehicle.Clear();
    _vehicle.Add(new PointFeature(pt));
    _vehicle.DataHasChanged();
    Map.Navigator.CenterOn(pt);
  }

  public void ClearTrack() {
    _track.Clear();
    _trackPts.Clear();
    _lastTrackPt = null;
    _track.DataHasChanged();
  }

  private (double Lat, double Lng) ToLatLng(Avalonia.Point screen) {
    var w = Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y);
    var (lng, lat) = SphericalMercator.ToLonLat(w.X, w.Y);
    return (lat, lng);
  }

  protected override void OnPointerPressed(PointerPressedEventArgs e) {
    base.OnPointerPressed(e);
    LastClickLatLng = ToLatLng(e.GetPosition(this));
  }

  protected override void OnPointerReleased(PointerReleasedEventArgs e) {
    base.OnPointerReleased(e);
    var ll = ToLatLng(e.GetPosition(this));
    LastClickLatLng = ll;
    if (e.InitialPressMouseButton == MouseButton.Left) {
      MapLeftClicked?.Invoke(ll.Lat, ll.Lng);
    }
  }

  protected override void OnPointerMoved(PointerEventArgs e) {
    base.OnPointerMoved(e);
    if (CursorMoved == null) {
      return;
    }
    var (lat, lng) = ToLatLng(e.GetPosition(this));
    CursorMoved.Invoke(lat, lng);
  }

  public void CenterOn(double lat, double lng) {
    var (x, y) = SphericalMercator.FromLonLat(lng, lat);
    Map.Navigator.CenterOn(new MPoint(x, y));
  }
}
