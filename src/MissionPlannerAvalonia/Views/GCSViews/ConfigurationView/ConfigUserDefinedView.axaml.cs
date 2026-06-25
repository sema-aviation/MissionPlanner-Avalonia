using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigUserDefinedView : UserControl {
  public ConfigUserDefinedView() => AvaloniaXamlLoader.Load(this);

  private async void OnModifyClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not ConfigUserDefinedViewModel vm) {
      return;
    }

    var owner = TopLevel.GetTopLevel(this) as Window;
    if (owner == null) {
      return;
    }

    var result = await ShowModifyDialog(owner, vm.OptionsText);
    if (result != null) {
      vm.ApplyOptions(result);
    }
  }

  private static Task<string?> ShowModifyDialog(Window owner, string initial) {
    var tcs = new TaskCompletionSource<string?>();

    var textBox = new TextBox {
      Text = initial,
      AcceptsReturn = true,
      Height = 220,
      TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
      VerticalContentAlignment = VerticalAlignment.Top,
    };

    var okButton = new Button { Content = "OK", IsDefault = true };
    var cancelButton = new Button { Content = "Cancel", IsCancel = true };

    var buttons = new StackPanel {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      HorizontalAlignment = HorizontalAlignment.Right,
    };
    buttons.Children.Add(okButton);
    buttons.Children.Add(cancelButton);

    var layout = new DockPanel { Margin = new Avalonia.Thickness(12) };
    var prompt = new TextBlock {
      Text = "Enter Param Names (comma or newline separated)",
      Margin = new Avalonia.Thickness(0, 0, 0, 8),
    };
    DockPanel.SetDock(prompt, Dock.Top);
    DockPanel.SetDock(buttons, Dock.Bottom);
    buttons.Margin = new Avalonia.Thickness(0, 8, 0, 0);
    layout.Children.Add(prompt);
    layout.Children.Add(buttons);
    layout.Children.Add(textBox);

    var dialog = new Window {
      Title = "Params",
      Width = 360,
      Height = 320,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Background = Avalonia.Media.Brush.Parse("#434445"),
      Content = layout,
    };

    okButton.Click += (_, _) => {
      tcs.TrySetResult(textBox.Text ?? "");
      dialog.Close();
    };
    cancelButton.Click += (_, _) => {
      tcs.TrySetResult(null);
      dialog.Close();
    };
    dialog.Closed += (_, _) => tcs.TrySetResult(null);

    dialog.ShowDialog(owner);
    return tcs.Task;
  }
}
