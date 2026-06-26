using System;
using Avalonia.Input;
using Avalonia.Threading;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Controls;

public class MapView : MapControl {
  private readonly WritableLayer _track = new() { Name = "Track" };
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly DispatcherTimer _timer;
  private bool _centered;
  private MPoint? _lastTrackPt;

  // Lat/lng under the last pointer interaction — the FlightData map context menu reads this so
  // its commands (Fly To Here, Set Home, TakeOff…) act at the clicked location (mirrors MP
  // MouseDownStart). Updated on every pointer press/release.
  public (double Lat, double Lng) LastClickLatLng { get; private set; }

  // Raised on a left click with the clicked lat/lng — for Ctrl+click Fly-To etc.
  public event Action<double, double>? MapLeftClicked;

  // Continuously re-center on the vehicle (mirrors MP "Auto Pan").
  public bool AutoPan { get; set; }

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

    AppendTrack(pt);

    if (!_centered) {
      double res = 156543.03392804097 / Math.Pow(2, 16);
      Map.Navigator.CenterOnAndZoomTo(pt, res);
      _centered = true;
    } else if (AutoPan) {
      Map.Navigator.CenterOn(pt);
    }
  }

  private static readonly SymbolStyle TrackDot = new() {
    SymbolType = SymbolType.Ellipse,
    Fill = new Brush(Color.Black),
    SymbolScale = 0.12,
  };

  // Append black GPS-track dots for the segment since the last sample (incremental — cheap to grow).
  private void AppendTrack(MPoint pt) {
    if (_lastTrackPt is { } prev) {
      double dx = pt.X - prev.X, dy = pt.Y - prev.Y;
      double len = Math.Sqrt(dx * dx + dy * dy);
      if (len < 0.5) {
        return;
      }
      int steps = Math.Clamp((int)(len / 3.0), 1, 200);
      for (int s = 1; s <= steps; s++) {
        double t = (double)s / steps;
        var f = new PointFeature(new MPoint(prev.X + dx * t, prev.Y + dy * t));
        f.Styles.Add(TrackDot);
        _track.Add(f);
      }
      _track.DataHasChanged();
    }
    _lastTrackPt = pt;
  }

  // Clear the recorded GPS track (mirrors MP "Clear Track").
  public void ClearTrack() {
    _track.Clear();
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

  // Re-center the map on a lat/lng (used by Auto-Pan follow and zoom-to actions).
  public void CenterOn(double lat, double lng) {
    var (x, y) = SphericalMercator.FromLonLat(lng, lat);
    Map.Navigator.CenterOn(new MPoint(x, y));
  }
}
