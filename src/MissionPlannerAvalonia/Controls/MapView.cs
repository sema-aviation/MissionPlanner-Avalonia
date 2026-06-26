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
using MissionPlannerAvalonia.ViewModels;
using NetTopologySuite.Geometries;

namespace MissionPlannerAvalonia.Controls;

public class MapView : MapControl {
  private readonly WritableLayer _track = new() { Name = "Track" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly DispatcherTimer _timer;
  private bool _centered;
  private MPoint? _lastTrackPt;
  private readonly List<Coordinate> _trackPts = new();

  // Lat/lng under the last pointer interaction — the FlightData map context menu reads this so
  // its commands (Fly To Here, Set Home, TakeOff…) act at the clicked location (mirrors MP
  // MouseDownStart). Updated on every pointer press/release.
  public (double Lat, double Lng) LastClickLatLng { get; private set; }

  // Raised on a left click with the clicked lat/lng — for Ctrl+click Fly-To etc.
  public event Action<double, double>? MapLeftClicked;

  // Continuously re-center on the vehicle (mirrors MP "Auto Pan").
  public bool AutoPan { get; set; }

  // Web-mercator zoom level (Slippy-map z) bound to the on-map zoom widget. Changing it re-zooms
  // the viewport keeping the current centre (mirrors MP MainMap.Zoom / trackBar1).
  public static readonly StyledProperty<int> ZoomLevelProperty =
      AvaloniaProperty.Register<MapView, int>(nameof(ZoomLevel), 16);

  public int ZoomLevel {
    get => GetValue(ZoomLevelProperty);
    set => SetValue(ZoomLevelProperty, value);
  }

  // Raised on pointer move with the lat/lng under the cursor (live coord readout over the map).
  public event Action<double, double>? CursorMoved;

  static MapView() {
    ZoomLevelProperty.Changed.AddClassHandler<MapView>((m, e) => m.SetZoomLevel((int)e.NewValue!));
  }

  // Mapsui resolution (metres/pixel at the equator) for a slippy-map zoom level.
  private static double ResolutionForLevel(int level) =>
      156543.03392804097 / Math.Pow(2, level);

  // Public zoom API: re-zoom to a slippy-map level keeping the current centre. Guarded so an early
  // call (before the viewport has a size) can't throw.
  public void SetZoomLevel(int level) {
    level = Math.Clamp(level, 1, 21);
    try {
      Map?.Navigator.ZoomTo(ResolutionForLevel(level));
    } catch {
      // viewport not ready yet — the next vehicle update / user gesture will settle it
    }
  }

  public MapView() {
    // dark map background so no white shows behind tiles during resize
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
    // Keep the viewport inside the world extent so you can't zoom/pan past the tiles into gray.
    map.Navigator.Limiter = new Mapsui.Limiting.ViewportLimiterKeepWithinExtent();
    Map = map;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    _timer.Tick += (_, _) => UpdateVehicle();
    _timer.Start();
  }

  // When false, the live-vehicle poll is suppressed — used by LogBrowse, which shows a static
  // recorded track from a log rather than a live link.
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

    // Heading line from the vehicle — fixed on-screen length, pointing along yaw (clockwise from N).
    double resMpp = Map.Navigator.Viewport.Resolution;
    if (resMpp > 0) {
      double len = 60 * resMpp;
      double rad = cs.yaw * Math.PI / 180.0;
      var end = new MPoint(pt.X + Math.Sin(rad) * len, pt.Y + Math.Cos(rad) * len);
      var heading = new GeometryFeature {
        Geometry = new LineString(new[] { new Coordinate(pt.X, pt.Y), new Coordinate(end.X, end.Y) }),
      };
      heading.Styles.Add(HeadingLine);
      _vehicle.Add(heading);
    }
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

  private static readonly VectorStyle TrackStyle = new() {
    Line = new Pen(new Color(255, 220, 30), 2), // amber GPS track, like MP
  };
  private static readonly VectorStyle HeadingLine = new() {
    Line = new Pen(new Color(0, 200, 255), 3), // cyan heading vector
  };

  // Grow the GPS track as a single polyline (one moved-enough point per sample) — no per-sample
  // dots/circles cluttering the map.
  private void AppendTrack(MPoint pt) {
    if (_lastTrackPt is { } prev) {
      double dx = pt.X - prev.X, dy = pt.Y - prev.Y;
      if (Math.Sqrt(dx * dx + dy * dy) < 0.5) {
        return; // hasn't moved enough to bother
      }
    }
    _lastTrackPt = pt;
    _trackPts.Add(new Coordinate(pt.X, pt.Y));
    if (_trackPts.Count > 5000) {
      _trackPts.RemoveAt(0); // cap memory on long flights
    }
    if (_trackPts.Count < 2) {
      return;
    }

    var line = new GeometryFeature { Geometry = new LineString(_trackPts.ToArray()) };
    line.Styles.Add(TrackStyle);
    _track.Clear();
    _track.Add(line);
    _track.DataHasChanged();
  }

  // Draw a complete recorded GPS track from a log and frame it (LogBrowse). Centers on the first
  // fix at a sensible zoom.
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
      line.Styles.Add(TrackStyle);
      _track.Add(line);
    }
    _track.DataHasChanged();
    if (_trackPts.Count > 0) {
      double res = 156543.03392804097 / Math.Pow(2, 15);
      Map.Navigator.CenterOnAndZoomTo(new MPoint(_trackPts[0].X, _trackPts[0].Y), res);
      _centered = true;
    }
  }

  // Place/move a marker at a sample point (LogBrowse GoToSample — plot/grid → map sync).
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

  // Clear the recorded GPS track (mirrors MP "Clear Track").
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

  // Re-center the map on a lat/lng (used by Auto-Pan follow and zoom-to actions).
  public void CenterOn(double lat, double lng) {
    var (x, y) = SphericalMercator.FromLonLat(lng, lat);
    Map.Navigator.CenterOn(new MPoint(x, y));
  }
}
