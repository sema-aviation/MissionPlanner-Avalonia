using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class MavlinkLogWindow : Window {
  public MavlinkLogWindow() {
    AvaloniaXamlLoader.Load(this);
    DataContext = new MavlinkLogConvertViewModel();
  }

  public static void OpenWindow() {
    var w = new MavlinkLogWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
