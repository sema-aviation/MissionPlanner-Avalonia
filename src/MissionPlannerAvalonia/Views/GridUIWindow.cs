using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class GridUIWindow : Window {
  private readonly GridUIView _view = new();

  public GridUIWindow(GridUIViewModel vm) {
    Title = "Survey (Grid)";
    Width = 800;
    Height = 660;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = vm;
    Content = _view;
    DataContext = vm;

    vm.CloseRequested += Close;
    Closed += (_, _) => {
      vm.SaveSettings();
      vm.CloseRequested -= Close;
    };
  }

  public static GridUIViewModel OpenForPolygon(List<PointLatLngAlt> polygon, PointLatLngAlt home,
      Action<List<PointLatLngAlt>> onAccept) {
    var vm = new GridUIViewModel(polygon, home);
    if (onAccept != null) {
      vm.GridAccepted += onAccept;
    }

    var w = new GridUIWindow(vm);
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }

    return vm;
  }
}
