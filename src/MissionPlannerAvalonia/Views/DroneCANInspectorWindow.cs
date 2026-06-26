using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

// Standalone tool window hosting the live DroneCAN/UAVCAN bus inspector (mirrors MP's
// UAVCAN Inspector form). LogBrowseWindow pattern with a static OpenWindow(). The VM owns a
// DroneCanBridge it tears down on Dispose, invoked from Closed (detaches MessageReceived,
// stops the timer + bridge — no leaks).
public class DroneCANInspectorWindow : Window {
  private readonly DroneCANInspectorView _view = new();
  private readonly DroneCANInspectorViewModel _vm = new();

  public DroneCANInspectorWindow() {
    Title = "UAVCAN Inspector";
    Width = 700;
    Height = 520;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;
    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new DroneCANInspectorWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
