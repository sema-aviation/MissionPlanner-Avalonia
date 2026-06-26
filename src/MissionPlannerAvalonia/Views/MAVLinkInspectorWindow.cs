using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

// Standalone tool window hosting the live MAVLink inspector (mirrors MP opening the
// Mavlink Inspector form). Same pattern as LogBrowseWindow: Window owns View + VM, with a
// static OpenWindow() entry point. The VM detaches its OnPacketReceived/OnPacketSent
// handlers and stops its timer on Dispose, which we call from Closed.
public class MAVLinkInspectorWindow : Window {
  private readonly MAVLinkInspectorView _view = new();
  private readonly MAVLinkInspectorViewModel _vm = new();

  public MAVLinkInspectorWindow() {
    Title = "Mavlink Inspector";
    Width = 640;
    Height = 520;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;
    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new MAVLinkInspectorWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
