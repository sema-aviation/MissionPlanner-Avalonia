using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class DroneCANParamsWindow : Window {
  private readonly DroneCANParamsView _view = new();
  private readonly DroneCANParamsViewModel _vm = new();

  public DroneCANParamsWindow() {
    Title = "UAVCAN Params";
    Width = 720;
    Height = 560;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;
    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() => Show(null);

  public static void OpenForNode(byte nodeId) {
    var w = Show(nodeId);

    _ = w._vm.InitForNodeAsync(nodeId);
  }

  private static DroneCANParamsWindow Show(byte? nodeId) {
    var w = new DroneCANParamsWindow();
    if (nodeId.HasValue) {
      w.Title = "UAVCAN Params - " + nodeId.Value;
    }

    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }

    return w;
  }
}
