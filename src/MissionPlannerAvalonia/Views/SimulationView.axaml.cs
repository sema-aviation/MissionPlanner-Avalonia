using System;
using Avalonia.Controls;
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

namespace MissionPlannerAvalonia.Views;

public partial class SimulationView : UserControl {
  private const double HitThresholdPx = 24;
  private const double TapThresholdPx = 8;

  private readonly MapControl _map = new();
  private readonly WritableLayer _home = new() { Name = "SitlHome" };

  private bool _draggingHome;
  private Mapsui.Manipulations.ScreenPosition _pressScreen;
  private bool _centered;

  public SimulationView() {
    InitializeComponent();
    BuildMap();
    MapHost.Children.Add(_map);
    DataContextChanged += (_, _) => OnDataContextChanged();
  }

  private SimulationViewModel? Vm => DataContext as SimulationViewModel;

  private void BuildMap() {
    var map = new Map { BackColor = new Color(0x26, 0x27, 0x28) };

    var src = new HttpTileSource(
        new GlobalSphericalMercator(),
        "https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}",
        name: "GoogleSatelliteMap",
        attribution: new BruTile.Attribution("Google"));
    map.Layers.Add(new TileLayer(src) { Name = "Satellite" });
    map.Layers.Add(_home);
    _map.Map = map;

    // Marker drag is done through Mapsui's pointer events so we can mark them Handled and
    // suppress the map pan while dragging the "H" (mirrors SITL.cs onmarker handling).
    _map.MapPointerPressed += OnMapPressed;
    _map.MapPointerMoved += OnMapMoved;
    _map.MapPointerReleased += OnMapReleased;
  }

  private void OnDataContextChanged() {
    RedrawHome();
    if (Vm == null || _centered) {
      return;
    }

    if (Vm.HomeLat != 0 || Vm.HomeLng != 0) {
      var (x, y) = SphericalMercator.FromLonLat(Vm.HomeLng, Vm.HomeLat);
      double res = 156543.03392804097 / Math.Pow(2, 16);
      _map.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), res);
      _centered = true;
    }
  }

  // Green "H" home marker (mirrors GMapMarkerWP "H" in SITL.cs).
  private void RedrawHome() {
    _home.Clear();
    if (Vm != null && (Vm.HomeLat != 0 || Vm.HomeLng != 0)) {
      var (x, y) = SphericalMercator.FromLonLat(Vm.HomeLng, Vm.HomeLat);
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

  private void OnMapPressed(object? sender, MapEventArgs e) {
    _pressScreen = e.ScreenPosition;
    if (HitHome(e.ScreenPosition)) {
      _draggingHome = true;
      e.Handled = true;
    }
  }

  private void OnMapMoved(object? sender, MapEventArgs e) {
    if (!_draggingHome) {
      return;
    }

    var (lng, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
    Vm?.SetHome(lat, lng);
    RedrawHome();
    e.Handled = true;
  }

  private void OnMapReleased(object? sender, MapEventArgs e) {
    if (_draggingHome) {
      _draggingHome = false;
      e.Handled = true;
      return;
    }

    // A tap (no appreciable pan) on empty map relocates the spawn home there.
    if (e.ScreenPosition.Distance(_pressScreen) <= TapThresholdPx) {
      var (lng, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
      Vm?.SetHome(lat, lng);
      RedrawHome();
    }
  }

  private bool HitHome(Mapsui.Manipulations.ScreenPosition screen) {
    if (Vm == null || (Vm.HomeLat == 0 && Vm.HomeLng == 0)) {
      return false;
    }

    var (x, y) = SphericalMercator.FromLonLat(Vm.HomeLng, Vm.HomeLat);
    var sp = _map.Map.Navigator.Viewport.WorldToScreen(x, y);
    return sp.Distance(screen) <= HitThresholdPx;
  }
}
