using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class LogBrowseWindow : Window {
  private readonly LogBrowseView _view = new();

  public LogBrowseWindow() {
    Title = "Log Browser";
    Width = 1100;
    Height = 680;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    var vm = new LogBrowseViewModel();
    _view.DataContext = vm;
    Content = _view;
    DataContext = vm;
  }

  public static async Task OpenWith(string path) {
    var w = new LogBrowseWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
    if (w.DataContext is LogBrowseViewModel vm) {
      await vm.LoadFileAsync(path);
    }
  }
}
