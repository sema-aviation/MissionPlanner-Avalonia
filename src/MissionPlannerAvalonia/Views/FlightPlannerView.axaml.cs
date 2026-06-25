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
    DataContextChanged += (_, _) => WireViewModel();
    WireViewModel();
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

  private async void OnViewKml(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "View KML",
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

  private async void OnSurveyGrid(object? sender, RoutedEventArgs e) {
    if (Vm == null) {
      return;
    }

    if (await SurveyDialogAsync(Vm.DefaultAlt) is { } r) {
      Vm.Status = Vm.GenerateSurveyGrid(r.Alt, r.Spacing, r.Angle);
    }
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

  private async Task<(double Alt, double Spacing, double Angle)?> SurveyDialogAsync(double defaultAlt) {
    var owner = OwnerWindow;
    if (owner == null) {
      return null;
    }

    var altBox = new NumericUpDown { Value = (decimal)defaultAlt, Minimum = 1 };
    var spacingBox = new NumericUpDown { Value = 30, Minimum = 1 };
    var angleBox = new NumericUpDown { Value = 0, Minimum = 0, Maximum = 359 };
    var ok = new Button { Content = "Generate", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
    var dlg = new Window {
      Title = "Survey (Grid)",
      Width = 360,
      SizeToContent = SizeToContent.Height,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel {
        Margin = new Avalonia.Thickness(12),
        Spacing = 8,
        Children = {
          new TextBlock { Text = "Altitude (m)" },
          altBox,
          new TextBlock { Text = "Line spacing (m)" },
          spacingBox,
          new TextBlock { Text = "Angle (deg)" },
          angleBox,
          ok,
        },
      },
    };
    bool confirmed = false;
    ok.Click += (_, _) => {
      confirmed = true;
      dlg.Close();
    };
    await dlg.ShowDialog(owner);
    if (!confirmed) {
      return null;
    }

    return ((double)(altBox.Value ?? 0), (double)(spacingBox.Value ?? 0), (double)(angleBox.Value ?? 0));
  }
}
