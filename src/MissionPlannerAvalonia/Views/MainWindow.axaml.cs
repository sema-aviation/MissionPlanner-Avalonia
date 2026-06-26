using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class MainWindow : Window {
  public MainWindow() {
    InitializeComponent();
    KeyDown += OnKeyDown;
  }

  private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

  // Global hotkeys (mirrors MainV2.ProcessCmdKey). Tool-window shortcuts (Ctrl+F/P/G/X/L/W/Z/T/J)
  // are noted in AVALONIA-FEATURES.md — their target windows are not ported yet.
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

  // CTX_mainmenu "Full Screen": borderless maximized vs normal (mirrors fullScreenToolStripMenuItem).
  private void OnFullScreen(object? sender, RoutedEventArgs e) {
    if (WindowState == WindowState.FullScreen) {
      WindowState = WindowState.Normal;
      SystemDecorations = SystemDecorations.Full;
    } else {
      SystemDecorations = SystemDecorations.None;
      WindowState = WindowState.FullScreen;
    }
  }

  // CTX_mainmenu "Readonly": toggle comPort.ReadOnly via the connection VM.
  private void OnToggleReadonly(object? sender, RoutedEventArgs e) {
    if (Vm != null) {
      Vm.Connection.ReadOnly = !Vm.Connection.ReadOnly;
    }
  }
}
