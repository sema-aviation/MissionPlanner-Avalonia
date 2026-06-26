using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels;
using SharpKml.Dom;
using SharpKml.Engine;

namespace MissionPlannerAvalonia.Views;

public partial class FlightPlannerView : UserControl {
  private FlightPlannerViewModel? _wired;

  public FlightPlannerView() {
    InitializeComponent();
    Map.WaypointDragMoved += OnWaypointDragged;
    Map.WaypointDragCommitted += OnWaypointDragged;
    Map.MapClicked += (lat, lng) => Vm?.AddWaypointAt(lat, lng);
    Map.ContextMenu = BuildMapMenu();
    DataContextChanged += (_, _) => WireViewModel();
    WireViewModel();
  }

  // Mirrors FlightPlanner contextMenuStrip1 (Mission mode). Acts at Map.LastClickLatLng.
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
    menu.Items.Add(Item("Insert WP", (vm, lat, lng) => vm.InsertWaypointAt(lat, lng)));
    menu.Items.Add(Item("Delete WP", (vm, lat, lng) => vm.DeleteNearest(lat, lng)));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Takeoff", (vm, lat, lng) => _ = vm.AddTakeoff(lat, lng)));
    menu.Items.Add(Item("Land", (vm, lat, lng) => vm.AddLand(lat, lng)));
    menu.Items.Add(Item("RTL", (vm, _, _) => vm.AddRtl()));
    menu.Items.Add(Item("DO_SET_ROI", (vm, lat, lng) => vm.AddRoi(lat, lng)));
    var loiter = new MenuItem { Header = "Loiter" };
    loiter.Items.Add(Item("Forever", (vm, lat, lng) => vm.AddLoiterForever(lat, lng)));
    loiter.Items.Add(Item("Time", (vm, lat, lng) => _ = vm.AddLoiterTime(lat, lng)));
    loiter.Items.Add(Item("Circles", (vm, lat, lng) => _ = vm.AddLoiterCircles(lat, lng)));
    menu.Items.Add(loiter);
    menu.Items.Add(Item("Jump", (vm, _, _) => _ = vm.AddJump()));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Clear Mission", (vm, _, _) => vm.ClearMissionCommand.Execute(null)));
    menu.Items.Add(Item("Reverse WPs", (vm, _, _) => vm.ReverseWaypointsCommand.Execute(null)));
    menu.Items.Add(Item("Modify Alt", (vm, _, _) => _ = vm.ModifyAllAlt()));
    menu.Items.Add(new Separator());
    var nofly = new MenuItem { Header = "Load NoFly Overlay…" };
    nofly.Click += OnLoadNoFly;
    menu.Items.Add(nofly);
    var noflyClear = new MenuItem { Header = "Clear NoFly Overlay" };
    noflyClear.Click += (_, _) => Map.SetNoFlyLayer(null);
    menu.Items.Add(noflyClear);
    return menu;
  }

  private static readonly FilePickerFileType NoFlyType = new("NoFly KML/KMZ") {
    Patterns = new[] { "*.kml", "*.kmz" },
  };

  // Load a NoFly KML/KMZ and draw its polygons over the map (mirrors MP's NoFly overlay).
  private async void OnLoadNoFly(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top == null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Load NoFly Overlay",
      AllowMultiple = false,
      FileTypeFilter = new[] { NoFlyType },
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

  private static readonly FilePickerFileType WpType = new("Waypoints") {
    Patterns = new[] { "*.waypoints", "*.txt" },
  };

  private static readonly FilePickerFileType KmlType = new("KML") {
    Patterns = new[] { "*.kml" },
  };

  private void WireViewModel() {
    if (ReferenceEquals(_wired, Vm)) {
      return;
    }

    if (_wired != null) {
      _wired.WaypointsChanged -= OnWaypointsChanged;
      _wired.PropertyChanged -= OnVmPropertyChanged;
    }

    _wired = Vm;
    if (_wired == null) {
      return;
    }

    _wired.WaypointsChanged += OnWaypointsChanged;
    _wired.PropertyChanged += OnVmPropertyChanged;
    OnWaypointsChanged();
    Map.SetGraticuleVisible(_wired.ShowGrid);
    Map.SetMapType(_wired.MapType);
  }

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
    }
  }

  private void OnWaypointsChanged() {
    if (Vm == null) {
      return;
    }

    Map.SetWaypoints(Vm.Waypoints.Select(w => (w.Seq, w.Lat, w.Lng)).ToList());
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
          FileTypeFilter = new[] { WpType },
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
          FileTypeChoices = new[] { WpType },
        }
    );
    if (file?.TryGetLocalPath() is { } path) {
      await Vm.SaveFileAsync(path);
    }
  }

  // "View KML" generates the current mission as KML and opens it (mirrors MP lnk_kml).
  private void OnViewKml(object? sender, RoutedEventArgs e) {
    if (Vm != null) {
      Vm.Status = Vm.GenerateMissionKmlAndOpen();
    }
  }

  // "Load KML File" / "KML Overlay": load an external KML and draw it on the map.
  private async void OnLoadKmlOverlay(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Load KML Overlay",
          AllowMultiple = false,
          FileTypeFilter = new[] { KmlType },
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

    // Open the full Survey-Grid window (Grid/GridUI.cs); accepted grid is appended to the mission.
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
