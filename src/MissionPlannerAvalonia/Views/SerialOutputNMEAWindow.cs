using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class SerialOutputNMEAWindow : Window {
  private readonly SerialOutputNMEAView _view = new();
  private readonly SerialOutputNMEAViewModel _vm = new();

  public SerialOutputNMEAWindow() {
    Title = "NMEA Output";
    Width = 480;
    Height = 400;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;

    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new SerialOutputNMEAWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
