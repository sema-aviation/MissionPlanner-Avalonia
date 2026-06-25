using System;
using Avalonia.Threading;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Controls;

public class MapView : MapControl {
  private readonly WritableLayer _vehicle = new() { Name = "Vehicle" };
  private readonly DispatcherTimer _timer;
  private bool _centered;

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

    _vehicle.Style = new SymbolStyle {
      Fill = new Brush(Color.Red),
      SymbolScale = 0.8,
      Outline = new Pen(Color.White, 2),
    };
    map.Layers.Add(_vehicle);
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
