using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;
using SharpKml.Dom;
using SharpKml.Engine;

namespace MissionPlannerAvalonia.Views;

public partial class FlightPlannerView : UserControl {
  private FlightPlannerViewModel? _wired;
  private bool _polygonDrawMode;

  private const int _pColIndex = 2;

  [Obsolete]
  public FlightPlannerView() {
    InitializeComponent();
    Map.WaypointDragMoved += OnWaypointDragged;
    Map.WaypointDragCommitted += OnWaypointDragged;
    Map.MapClicked += OnMapClicked;
    Map.MidpointInsertRequested += (afterSeq, lat, lng) =>
        Vm?.InsertWaypointAfterSeq(afterSeq, lat, lng);
    Map.ContextMenu = BuildMapMenu();
    DataContextChanged += (_, _) => WireViewModel();
    WireViewModel();
  }

  private void OnMapClicked(double lat, double lng) {
    if (Vm == null) {
      return;
    }
    if (_polygonDrawMode) {
      Vm.AddPolygonPoint(lat, lng);
    } else {
      Vm.AddWaypointAt(lat, lng);
    }
  }

  private static readonly string[] _defaultParamLabels = { "P1", "P2", "P3", "P4" };
  [Obsolete]
  private static readonly Dictionary<MAVLink.MAV_CMD, string[]> _paramLabels = new() {
    [MAVLink.MAV_CMD.WAYPOINT] = new[] { "Delay", "—", "—", "Yaw" },
    [MAVLink.MAV_CMD.SPLINE_WAYPOINT] = new[] { "Delay", "—", "—", "—" },
    [MAVLink.MAV_CMD.LOITER_UNLIM] = new[] { "—", "—", "Radius", "Yaw" },
    [MAVLink.MAV_CMD.LOITER_TURNS] = new[] { "Turns", "—", "Radius", "—" },
    [MAVLink.MAV_CMD.LOITER_TIME] = new[] { "Time", "—", "Radius", "—" },
    [MAVLink.MAV_CMD.RETURN_TO_LAUNCH] = new[] { "—", "—", "—", "—" },
    [MAVLink.MAV_CMD.LAND] = new[] { "Abort", "—", "—", "Yaw" },
    [MAVLink.MAV_CMD.TAKEOFF] = new[] { "—", "—", "—", "Yaw" },
    [MAVLink.MAV_CMD.DO_JUMP] = new[] { "WP#", "Repeat", "—", "—" },
    [MAVLink.MAV_CMD.DO_CHANGE_SPEED] = new[] { "Type", "Speed", "Throttle", "—" },
    [MAVLink.MAV_CMD.DO_SET_ROI] = new[] { "—", "—", "—", "—" },
    [MAVLink.MAV_CMD.DO_DIGICAM_CONTROL] = new[] { "Shoot", "—", "—", "—" },
    [MAVLink.MAV_CMD.DO_SET_SERVO] = new[] { "Ch", "PWM", "—", "—" },
    [MAVLink.MAV_CMD.DO_SET_RELAY] = new[] { "Relay", "On/Off", "—", "—" },
    [MAVLink.MAV_CMD.CONDITION_DELAY] = new[] { "Time", "—", "—", "—" },
  };

  [Obsolete]
  private void OnWpSelectionChanged(object? sender, SelectionChangedEventArgs e) {
    if (sender is not DataGrid grid || grid.SelectedItem is not WpRow row) {
      return;
    }
    var cmd = (MAVLink.MAV_CMD)row.Command;
    Services.MavCmdInfo.EnsureLoaded(Services.MavCmdInfo.CurrentSubtree());
    var xml = Services.MavCmdInfo.Get(cmd.ToString());
    var labels = _paramLabels.TryGetValue(cmd, out var l) ? l : _defaultParamLabels;
    for (int i = 0; i < 4; i++) {
      int col = _pColIndex + i;
      if (col < grid.Columns.Count) {
        string name = xml != null && !string.IsNullOrEmpty(xml[i]) ? xml[i] : labels[i];
        grid.Columns[col].Header = string.IsNullOrEmpty(name) ? "—" : name;
      }
    }
  }

  [Obsolete]
  private ContextMenu BuildMapMenu() {
    MenuItem Item(string header, Action<FlightPlannerViewModel, double, double> action) {
      var mi = new MenuItem { Header = header };
      mi.Click += (_, _) => {
        var (lat, lng) = Map.LastClickLatLng;
        if (Vm != null) {
          action(Vm, lat, lng);
        }
      };
      return mi;
    }
    var menu = new ContextMenu();
    menu.Items.Add(Item("Insert Point", (vm, lat, lng) => vm.InsertWaypointAt(lat, lng)));
    menu.Items.Add(Item("Delete Point", (vm, lat, lng) => vm.DeleteNearest(lat, lng)));
    menu.Items.Add(Item("Set Home Here", (vm, lat, lng) => vm.SetHome(lat, lng)));
    menu.Items.Add(Item("Measure Distance", (vm, lat, lng) => _ = vm.MeasureClick(lat, lng)));
    var zoomto = new MenuItem { Header = "Zoom To" };
    var zh = new MenuItem { Header = "Home" };
    zh.Click += (_, _) => Map.ZoomToHome();
    var zm = new MenuItem { Header = "Mission" };
    zm.Click += (_, _) => Map.ZoomToMission();
    var zv = new MenuItem { Header = "Vehicle" };
    zv.Click += (_, _) => Map.ZoomToVehicle();
    zoomto.Items.Add(zh);
    zoomto.Items.Add(zm);
    zoomto.Items.Add(zv);
    menu.Items.Add(zoomto);
    var rotate = new MenuItem { Header = "Rotate" };
    var rcw = new MenuItem { Header = "Clockwise 15°" };
    rcw.Click += (_, _) => Map.RotateBy(15);
    var rccw = new MenuItem { Header = "Counter-clockwise 15°" };
    rccw.Click += (_, _) => Map.RotateBy(-15);
    var rreset = new MenuItem { Header = "Reset (North Up)" };
    rreset.Click += (_, _) => Map.ResetRotation();
    rotate.Items.Add(rcw);
    rotate.Items.Add(rccw);
    rotate.Items.Add(rreset);
    menu.Items.Add(rotate);

    var missionOnly = new List<Control>();
    void AddMissionOnly(Control c) {
      missionOnly.Add(c);
      menu.Items.Add(c);
    }

    var fenceOnly = new List<Control>();
    void AddFenceOnly(Control c) {
      fenceOnly.Add(c);
      menu.Items.Add(c);
    }
    AddFenceOnly(Item("Set Return Location", (vm, lat, lng) => vm.SetFenceReturn(lat, lng)));
    AddMissionOnly(new Separator());
    AddMissionOnly(Item("Insert at Current Position", (vm, _, _) => vm.InsertAtCurrentPosition()));
    AddMissionOnly(Item("Insert Spline WP", (vm, lat, lng) => vm.AddSplineWp(lat, lng)));
    AddMissionOnly(Item("Takeoff", (vm, lat, lng) => _ = vm.AddTakeoff(lat, lng)));
    AddMissionOnly(Item("Land", (vm, lat, lng) => vm.AddLand(lat, lng)));
    AddMissionOnly(Item("RTL", (vm, _, _) => vm.AddRtl()));
    AddMissionOnly(Item("DO_SET_ROI", (vm, lat, lng) => vm.AddRoi(lat, lng)));
    var loiter = new MenuItem { Header = "Loiter" };
    loiter.Items.Add(Item("Forever", (vm, lat, lng) => vm.AddLoiterForever(lat, lng)));
    loiter.Items.Add(Item("Time", (vm, lat, lng) => _ = vm.AddLoiterTime(lat, lng)));
    loiter.Items.Add(Item("Circles", (vm, lat, lng) => _ = vm.AddLoiterCircles(lat, lng)));
    AddMissionOnly(loiter);
    AddMissionOnly(Item("Jump", (vm, _, _) => _ = vm.AddJump()));
    AddMissionOnly(Item("Jump to Start", (vm, _, _) => vm.AddJumpStart()));
    var autowp = new MenuItem { Header = "Auto WP" };
    autowp.Items.Add(Item("Circle", (vm, lat, lng) => _ = vm.CreateWpCircle(lat, lng)));
    autowp.Items.Add(Item("Spline Circle", (vm, lat, lng) => _ = vm.CreateSplineCircle(lat, lng)));
    autowp.Items.Add(Item("Circle Survey", (vm, lat, lng) => _ = vm.CreateCircleSurvey(lat, lng)));
    AddMissionOnly(autowp);
    AddMissionOnly(Item("Elevation Graph", (_, _, _) => ShowElevationGraph()));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Clear", (vm, _, _) => vm.ClearMissionCommand.Execute(null)));
    AddMissionOnly(Item("Reverse WPs", (vm, _, _) => vm.ReverseWaypointsCommand.Execute(null)));
    AddMissionOnly(Item("Modify Alt", (vm, _, _) => _ = vm.ModifyAllAlt()));
    menu.Opening += (_, _) => {
      bool mission = Vm?.MissionType is null or "Mission";
      foreach (var c in missionOnly) {
        c.IsVisible = mission;
      }
      foreach (var c in fenceOnly) {
        c.IsVisible = Vm?.MissionType == "Fence";
      }
    };
    menu.Items.Add(new Separator());
    var poi = new MenuItem { Header = "POI" };
    poi.Items.Add(Item("Add POI", (vm, lat, lng) => _ = vm.AddPoi(lat, lng)));
    poi.Items.Add(Item("Delete POI", (vm, lat, lng) => vm.DeleteNearestPoi(lat, lng)));
    poi.Items.Add(Item("POI at Coords", (vm, _, _) => _ = vm.AddPoiAtCoords()));
    poi.Items.Add(Item("Clear POIs", (vm, _, _) => vm.ClearPois()));
    menu.Items.Add(poi);
    menu.Items.Add(new Separator());
    var nofly = new MenuItem { Header = "Load NoFly Overlay…" };
    nofly.Click += OnLoadNoFly;
    menu.Items.Add(nofly);
    var noflyClear = new MenuItem { Header = "Clear NoFly Overlay" };
    noflyClear.Click += (_, _) => Map.SetNoFlyLayer(null);
    menu.Items.Add(noflyClear);
    menu.Items.Add(new Separator());
    var poly = new MenuItem { Header = "Polygon" };
    var draw = new MenuItem { Header = "Draw" };
    draw.Click += (_, _) => {
      _polygonDrawMode = !_polygonDrawMode;
      draw.Header = _polygonDrawMode ? "Draw (on)" : "Draw";
      if (Vm != null) {
        Vm.Status = _polygonDrawMode
            ? "Polygon draw: click the map to add vertices."
            : "Polygon draw off.";
      }
    };
    poly.Items.Add(draw);
    poly.Items.Add(Item("Clear", (vm, _, _) => vm.ClearPolygon()));
    poly.Items.Add(Item("From Current Waypoints", (vm, _, _) => vm.BuildPolygonFromWaypoints()));
    poly.Items.Add(Item("Area", (vm, _, _) => vm.PolygonArea()));
    menu.Items.Add(poly);
    return menu;
  }

  private void OnZoomSliderChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e) {
    Map?.SetZoomLevel(e.NewValue);
  }

  private void ShowElevationGraph() {
    if (Vm?.BuildElevationProfile() is not { } p) {
      if (Vm != null) {
        Vm.Status = "Need at least 2 waypoints for elevation graph.";
      }
      return;
    }
    var win = new ElevationGraphWindow(p.Dist, p.Terrain, p.Planned,
        MissionPlanner.CurrentState.AltUnit, MissionPlanner.CurrentState.DistanceUnit);
    if (TopLevel.GetTopLevel(this) is Window owner) {
      win.Show(owner);
    } else {
      win.Show();
    }
  }

  private static readonly FilePickerFileType _noFlyType = new("NoFly KML/KMZ") {
    Patterns = new[] { "*.kml", "*.kmz" },
  };

  private async void OnLoadNoFly(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top == null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Load NoFly Overlay",
      AllowMultiple = false,
      FileTypeFilter = new[] { _noFlyType },
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path == null) {
      return;
    }

    var layer = Services.NoFlyOverlay.BuildLayer(path);
    Map.SetNoFlyLayer(layer);
    if (Vm != null) {
      Vm.Status = layer == null ? "No NoFly polygons found." : "NoFly overlay loaded.";
    }
  }

  private FlightPlannerViewModel? Vm => DataContext as FlightPlannerViewModel;

  private static readonly FilePickerFileType _wpType = new("Waypoints") {
    Patterns = new[] { "*.waypoints", "*.txt" },
  };

  private static readonly FilePickerFileType _kmlType = new("KML") {
    Patterns = new[] { "*.kml" },
  };

  private void WireViewModel() {
    if (ReferenceEquals(_wired, Vm)) {
      return;
    }

    if (_wired != null) {
      _wired.WaypointsChanged -= OnWaypointsChanged;
      _wired.PropertyChanged -= OnVmPropertyChanged;
      _wired.PoiChanged -= OnPoiChanged;
      _wired.DrawnPolygonChanged -= OnDrawnPolygonChanged;
    }

    _wired = Vm;
    if (_wired == null) {
      return;
    }

    _wired.WaypointsChanged += OnWaypointsChanged;
    _wired.PropertyChanged += OnVmPropertyChanged;
    _wired.PoiChanged += OnPoiChanged;
    _wired.DrawnPolygonChanged += OnDrawnPolygonChanged;
    OnWaypointsChanged();
    OnPoiChanged();
    OnDrawnPolygonChanged();
    Map.SetGraticuleVisible(_wired.ShowGrid);
    Map.SetMapType(_wired.MapType);
  }

  private void OnPoiChanged() =>
      Map.ShowPois(Services.PoiStore.All.Select(p => (p.Lat, p.Lng, p.Name)).ToList());

  private void OnDrawnPolygonChanged() =>
      Map.ShowDrawnPolygon(Vm == null
          ? new List<(double, double)>()
          : Vm.DrawnPolygon.Select(p => (p.Lat, p.Lng)).ToList());

  private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e) {
    if (Vm == null) {
      return;
    }

    if (e.PropertyName == nameof(FlightPlannerViewModel.ShowGrid)) {
      Map.SetGraticuleVisible(Vm.ShowGrid);
    } else if (e.PropertyName == nameof(FlightPlannerViewModel.MapType)) {
      Map.SetMapType(Vm.MapType);
    } else if (e.PropertyName == nameof(FlightPlannerViewModel.HomeLat)
               || e.PropertyName == nameof(FlightPlannerViewModel.HomeLng)) {
      Map.SetHome(Vm.HomeLat, Vm.HomeLng);
    } else if (e.PropertyName == nameof(FlightPlannerViewModel.MissionType)) {
      Map.SetRenderMode(Vm.MissionType);
    }
  }

  private void OnWaypointsChanged() {
    if (Vm == null) {
      return;
    }

    Map.SetWaypoints(
        Vm.Waypoints.Select(w => (w.Seq, w.Lat, w.Lng, w.Command, w.P1, w.P2, w.P3, w.P4)).ToList(),
        Vm.WpRadius, Vm.LoiterRadius);
  }

  private void OnWaypointDragged(int seq, double lat, double lng) => Vm?.MoveWaypoint(seq, lat, lng);

  private void OnMapTypeChanged(object? sender, SelectionChangedEventArgs e) {
    if (Vm != null) {
      Map.SetMapType(Vm.MapType);
    }
  }

  private async void OnLoadFile(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Load Mission",
          AllowMultiple = false,
          FileTypeFilter = new[] { _wpType },
        }
    );
    var file = files.FirstOrDefault();
    if (file?.TryGetLocalPath() is { } path) {
      await Vm.LoadFileAsync(path);
    }
  }

  private async void OnSaveFile(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }

    var file = await top.StorageProvider.SaveFilePickerAsync(
        new FilePickerSaveOptions {
          Title = "Save Mission",
          DefaultExtension = "waypoints",
          SuggestedFileName = "mission.waypoints",
          FileTypeChoices = new[] { _wpType },
        }
    );
    if (file?.TryGetLocalPath() is { } path) {
      await Vm.SaveFileAsync(path);
    }
  }

  private void OnViewKml(object? sender, RoutedEventArgs e) {
    if (Vm != null) {
      Vm.Status = Vm.GenerateMissionKmlAndOpen();
    }
  }

  private async void OnLoadKmlOverlay(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Load KML Overlay",
          AllowMultiple = false,
          FileTypeFilter = new[] { _kmlType },
        }
    );
    if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) {
      return;
    }

    try {
      var track = ParseKmlTrack(path);
      if (track.Count >= 2) {
        Map.ShowKmlTrack(track);
      }
    } catch (Exception ex) {
      if (Vm != null) {
        Vm.Status = "KML load failed: " + ex.Message;
      }
    }
  }

  private static List<(double Lat, double Lng)> ParseKmlTrack(string path) {
    var track = new List<(double, double)>();
    KmlFile kml;
    using (var fs = File.OpenRead(path)) {
      kml = KmlFile.Load(fs);
    }

    if (kml.Root == null) {
      return track;
    }

    foreach (var el in kml.Root.Flatten()) {
      switch (el) {
        case LineString ls when ls.Coordinates != null:
          foreach (var v in ls.Coordinates) {
            track.Add((v.Latitude, v.Longitude));
          }

          break;
        case LinearRing lr when lr.Coordinates != null:
          foreach (var v in lr.Coordinates) {
            track.Add((v.Latitude, v.Longitude));
          }

          break;
        case SharpKml.Dom.Point pt when pt.Coordinate != null:
          track.Add((pt.Coordinate.Latitude, pt.Coordinate.Longitude));
          break;
      }
    }

    return track;
  }

  private async void OnInjectCustomMap(object? sender, RoutedEventArgs e) {
    var url = await PromptAsync("Inject Custom Map",
        "Tile URL template (use {x} {y} {z}):",
        "https://tile.openstreetmap.org/{z}/{x}/{y}.png");
    if (!string.IsNullOrWhiteSpace(url)) {
      Map.SetCustomTileSource(url);
      if (Vm != null) {
        Vm.Status = "Custom tile source applied.";
      }
    }
  }

  private void OnSurveyGrid(object? sender, RoutedEventArgs e) {
    if (Vm == null) {
      return;
    }

    if (Vm.BuildSurveyArea() is not { } area) {
      Vm.Status = "Need at least 3 waypoints to outline the survey area.";
      return;
    }

    GridUIWindow.OpenForPolygon(area.polygon, area.home,
        grid => Vm.Status = Vm.AppendSurveyGrid(grid));
  }

  private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

  private async Task<string?> PromptAsync(string title, string label, string initial) {
    var owner = OwnerWindow;
    if (owner == null) {
      return null;
    }

    var box = new TextBox { Text = initial };
    var ok = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
    var dlg = new Window {
      Title = title,
      Width = 460,
      SizeToContent = SizeToContent.Height,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel {
        Margin = new Avalonia.Thickness(12),
        Spacing = 8,
        Children = {
          new TextBlock { Text = label },
          box,
          ok,
        },
      },
    };
    string? answer = null;
    ok.Click += (_, _) => {
      answer = box.Text;
      dlg.Close();
    };
    await dlg.ShowDialog(owner);
    return answer;
  }

}
