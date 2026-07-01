using Avalonia.Controls;
using Avalonia.Media;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public class FollowMeWindow : Window {
  private readonly FollowMeView _view = new();
  private readonly FollowMeViewModel _vm = new();

  public FollowMeWindow() {
    Title = "Follow Me";
    Width = 480;
    Height = 420;
    Background = new SolidColorBrush(Color.Parse("#434445"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _view.DataContext = _vm;
    Content = _view;
    DataContext = _vm;

    Closed += (_, _) => _vm.Dispose();
  }

  public static void OpenWindow() {
    var w = new FollowMeWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
