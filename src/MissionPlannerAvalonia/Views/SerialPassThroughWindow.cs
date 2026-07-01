using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class SerialPassThroughWindow : Window {
  private readonly SerialPassThroughView _view = new();
  private readonly SerialPassThroughViewModel _vm = new();

  public SerialPassThroughWindow() {
    Title = "Mavlink Mirror";
    Width = 480;
    Height = 380;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;

    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new SerialPassThroughWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
