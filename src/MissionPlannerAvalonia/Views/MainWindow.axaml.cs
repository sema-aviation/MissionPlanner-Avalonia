using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class MainWindow : Window {
  public MainWindow() {
    InitializeComponent();

    Title = Services.AppVersion.Title;
    KeyDown += OnKeyDown;
  }

  private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

  private void OnKeyDown(object? sender, KeyEventArgs e) {
    var vm = Vm;
    if (vm == null) {
      return;
    }
    bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
    switch (e.Key) {
      case Key.F2:
        vm.NavigateCommand.Execute("DATA");
        break;
      case Key.F3:
        vm.NavigateCommand.Execute("PLAN");
        break;
      case Key.F4:
        vm.NavigateCommand.Execute("CONFIG");
        break;
      case Key.F5:
        vm.GetParamsCommand.Execute(null);
        break;
      case Key.F12:
        vm.Connection.ToggleConnectCommand.Execute(null);
        break;
      case Key.Y when ctrl:
        vm.SaveToEepromCommand.Execute(null);
        break;
      default:
        return;
    }
    e.Handled = true;
  }

  private void OnFullScreen(object? sender, RoutedEventArgs e) {
    if (WindowState == WindowState.FullScreen) {
      WindowState = WindowState.Normal;
      SystemDecorations = SystemDecorations.Full;
    } else {
      SystemDecorations = SystemDecorations.None;
      WindowState = WindowState.FullScreen;
    }
  }

  private void OnToggleReadonly(object? sender, RoutedEventArgs e) {
    if (Vm != null) {
      Vm.Connection.ReadOnly = !Vm.Connection.ReadOnly;
    }
  }

  private void OnLinkStats(object? sender, RoutedEventArgs e) => LinkStatsWindow.OpenWindow();

  private void OnConnectionOptions(object? sender, RoutedEventArgs e) =>
      ConnectionOptionsWindow.OpenWindow();

  private void OnDownloadLogs(object? sender, RoutedEventArgs e) => LogDownloadWindow.OpenWindow();

  private void OnMavlinkLogConvert(object? sender, RoutedEventArgs e) => MavlinkLogWindow.OpenWindow();
}
