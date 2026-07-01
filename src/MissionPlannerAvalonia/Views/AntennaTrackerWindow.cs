using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class AntennaTrackerWindow : Window {
  private readonly AntennaTrackerView _view = new();
  private readonly AntennaTrackerUIViewModel _vm = new();

  public AntennaTrackerWindow() {
    Title = "Antenna Tracker";
    Width = 800;
    Height = 660;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;

    Opened += (_, _) => _vm.Activate();
    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new AntennaTrackerWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
