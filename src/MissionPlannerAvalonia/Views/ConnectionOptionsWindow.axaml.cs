using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

// Connection Options dialog (mirrors MP's CTX "Connection Options"). Edits a few real comms
// settings persisted via Settings.Instance.
public partial class ConnectionOptionsWindow : Window {
  public ConnectionOptionsWindow() {
    AvaloniaXamlLoader.Load(this);
    DataContext = new ConnectionOptionsViewModel();
  }

  private void OnClose(object? sender, RoutedEventArgs e) => Close();

  public static void OpenWindow() {
    var w = new ConnectionOptionsWindow();
    var owner = Services.Dialogs.Owner;
    if (owner != null) {
      w.Show(owner);
    } else {
      w.Show();
    }
  }
}
