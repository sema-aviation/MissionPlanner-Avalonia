using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class LinkStatsWindow : Window {
  private readonly LinkStatsViewModel _vm = new();

  public LinkStatsWindow() {
    AvaloniaXamlLoader.Load(this);
    DataContext = _vm;
    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new LinkStatsWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
