using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

// Port of the app-wide dialog providers (upstream Controls/InputBox, CustomMessageBox,
// Common.OpenUrl, Common.MessageShowAgain). Callable from view-models without plumbing a
// TopLevel: the owner is resolved from the desktop lifetime's MainWindow.
public static class Dialogs {
  private const string Bg = "#262728";

  public static Window? Owner =>
      (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

  // Single-line text prompt. Returns null on cancel.
  // (Command handlers run on the UI thread, so no marshaling is needed.)
  public static Task<string?> InputBox(string title, string prompt, string value = "") =>
      ShowInput(title, prompt, value);

  // Yes/No confirmation. Returns true for Yes.
  public static Task<bool> Confirm(string title, string text) =>
      ShowButtons(title, text, ("Yes", true), ("No", false));

  // OK acknowledgement.
  public static Task Alert(string title, string text) =>
      ShowButtons(title, text, ("OK", true));

  // Multi-button chooser. Returns the clicked label, or null if the window is closed without a pick.
  public static Task<string?> Choice(string title, string text, params string[] labels) {
    var panel = Shell(title);
    panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
    var row = new StackPanel {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
      Spacing = 8,
      Margin = new Thickness(0, 6, 0, 0),
    };
    var w = Frame(title, panel);
    foreach (var label in labels) {
      var b = new Button { Content = label, MinWidth = 80 };
      b.Click += (_, _) => w.Close(label);
      row.Children.Add(b);
    }
    panel.Children.Add(row);
    return ShowOwned<string?>(w);
  }

  // Altitude + frame prompt (mirrors MP AltInputBox). Returns (alt, frame) or null on cancel.
  public static Task<(double Alt, string Frame)?> AltInputBox(
      string title = "Altitude", double defaultAlt = 50, string defaultFrame = "Relative") =>
      ShowAltInput(title, defaultAlt, defaultFrame);

  // Confirmation suppressed once the user ticks "Don't show again" (persisted per tag).
  // Returns the user's choice; suppressed prompts auto-return true.
  public static async Task<bool> MessageShowAgain(string title, string text, string tag) {
    var key = "SHOWAGAIN_" + tag;
    if (Settings.Instance.GetBoolean(key)) {
      return true;
    }
    var (ok, dontShow) = await ShowShowAgain(title, text);
    if (dontShow) {
      Settings.Instance[key] = true.ToString();
      Settings.Instance.Save();
    }
    return ok;
  }

  // Cross-platform URL launcher (mirrors Common.OpenUrl).
  public static void OpenUrl(string url) {
    try {
      Process.Start(url);
    } catch {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        Process.Start(new ProcessStartInfo(url.Replace("&", "^&")) { UseShellExecute = true });
      } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
        Process.Start("xdg-open", url);
      } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
        Process.Start("open", url);
      } else {
        throw;
      }
    }
  }

  private static StackPanel Shell(string title) => new() {
    Margin = new Thickness(16),
    Spacing = 10,
    Children = { new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 14 } },
  };

  private static Window Frame(string title, Control body) => new() {
    Title = title,
    Width = 380,
    SizeToContent = SizeToContent.Height,
    CanResize = false,
    WindowStartupLocation = WindowStartupLocation.CenterOwner,
    Background = new SolidColorBrush(Color.Parse(Bg)),
    Content = body,
  };

  private static Task<string?> ShowInput(string title, string prompt, string value) {
    var box = new TextBox { Text = value };
    var panel = Shell(title);
    panel.Children.Add(new TextBlock { Text = prompt });
    panel.Children.Add(box);
    var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
    var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
    panel.Children.Add(Buttons(ok, cancel));
    var w = Frame(title, panel);
    ok.Click += (_, _) => w.Close(box.Text);
    cancel.Click += (_, _) => w.Close((string?)null);
    return ShowOwned<string?>(w);
  }

  private static async Task<(double, string)?> ShowAltInput(string title, double alt, string frame) {
    var box = new TextBox { Text = alt.ToString(System.Globalization.CultureInfo.InvariantCulture) };
    var combo = new ComboBox {
      ItemsSource = new[] { "Relative", "Absolute", "Terrain" },
      SelectedItem = frame,
      HorizontalAlignment = HorizontalAlignment.Stretch,
    };
    var panel = Shell(title);
    panel.Children.Add(new TextBlock { Text = "Altitude" });
    panel.Children.Add(box);
    panel.Children.Add(new TextBlock { Text = "Frame" });
    panel.Children.Add(combo);
    var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
    var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
    panel.Children.Add(Buttons(ok, cancel));
    var w = Frame(title, panel);
    ok.Click += (_, _) => w.Close(true);
    cancel.Click += (_, _) => w.Close(false);
    if (!await ShowOwned<bool>(w)) {
      return null;
    }
    if (!double.TryParse(box.Text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var a)) {
      return null;
    }
    return (a, combo.SelectedItem as string ?? "Relative");
  }

  private static Task<bool> ShowButtons(string title, string text, params (string label, bool result)[] btns) {
    var panel = Shell(title);
    panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
    var row = new StackPanel {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
      Spacing = 8,
      Margin = new Thickness(0, 6, 0, 0),
    };
    var w = Frame(title, panel);
    foreach (var (label, result) in btns) {
      var b = new Button { Content = label, MinWidth = 80, IsDefault = result };
      b.Click += (_, _) => w.Close(result);
      row.Children.Add(b);
    }
    panel.Children.Add(row);
    return ShowOwned<bool>(w);
  }

  private static async Task<(bool ok, bool dontShow)> ShowShowAgain(string title, string text) {
    var chk = new CheckBox { Content = "Don't show this again" };
    var panel = Shell(title);
    panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
    panel.Children.Add(chk);
    var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
    var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
    var w = Frame(title, panel);
    ok.Click += (_, _) => w.Close(true);
    cancel.Click += (_, _) => w.Close(false);
    panel.Children.Add(Buttons(ok, cancel));
    var ok2 = await ShowOwned<bool>(w);
    return (ok2, chk.IsChecked == true);
  }

  private static StackPanel Buttons(params Control[] controls) {
    var row = new StackPanel {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
      Spacing = 8,
      Margin = new Thickness(0, 6, 0, 0),
    };
    foreach (var c in controls) {
      row.Children.Add(c);
    }
    return row;
  }

  private static Task<T> ShowOwned<T>(Window w) {
    var owner = Owner;
    return owner != null ? w.ShowDialog<T>(owner) : w.ShowDialog<T>(w);
  }
}
