using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

// Lists and downloads on-board dataflash logs over the active MAVLink link
// (mirrors MP's LogDownloadMavlink form).
public partial class LogDownloadWindow : Window {
  public LogDownloadWindow() {
    AvaloniaXamlLoader.Load(this);
    DataContext = new LogDownloadViewModel();
  }

  public static void OpenWindow() {
    var w = new LogDownloadWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
