using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class GeoRefWindow : Window {
  private readonly GeoRefView _view = new();

  public GeoRefWindow() {
    Title = "Geo Ref Images";
    Width = 920;
    Height = 660;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    var vm = new GeoRefViewModel();
    _view.DataContext = vm;
    Content = _view;
    DataContext = vm;
  }

  public static Task OpenWith(string? logPath = null) {
    var w = new GeoRefWindow();
    if (!string.IsNullOrEmpty(logPath) && w.DataContext is GeoRefViewModel vm) {
      vm.LogPath = logPath;
    }
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
    return Task.CompletedTask;
  }
}
