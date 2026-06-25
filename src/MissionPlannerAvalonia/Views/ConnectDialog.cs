using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Views;

// Built in code (no XAML) so it has no named-field / compiled-binding fragility.
// label2 == null hides the second field (single-value prompts like WS url / UDP port).
public class ConnectDialog : Window {
  private ConnectDialog(string title, string label1, string value1, string? label2, string value2) {
    Title = "Connection";
    Width = 360;
    SizeToContent = SizeToContent.Height;
    CanResize = false;
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    Background = new SolidColorBrush(Color.FromRgb(0x26, 0x27, 0x28));

    var box1 = new TextBox { Text = value1 };
    var panel = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
    panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 14 });
    panel.Children.Add(new StackPanel {
      Spacing = 3,
      Children = { new TextBlock { Text = label1 }, box1 },
    });

    TextBox? box2 = null;
    if (label2 != null) {
      box2 = new TextBox { Text = value2 };
      panel.Children.Add(new StackPanel {
        Spacing = 3,
        Children = { new TextBlock { Text = label2 }, box2 },
      });
    }

    var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
    var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
    ok.Click += (_, _) => Close(new[] { box1.Text, box2?.Text });
    cancel.Click += (_, _) => Close(null);
    panel.Children.Add(new StackPanel {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
      Spacing = 8,
      Margin = new Thickness(0, 6, 0, 0),
      Children = { ok, cancel },
    });

    Content = panel;
  }

  public static Task<string?[]?> Show(
      Window owner, string title, string label1, string value1, string? label2, string value2) =>
      new ConnectDialog(title, label1, value1, label2, value2).ShowDialog<string?[]?>(owner);
}
