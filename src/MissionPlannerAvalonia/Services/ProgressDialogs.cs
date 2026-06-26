using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MissionPlannerAvalonia.Services;

// Modeless "please wait" box (mirrors Common.LoadingBox). Caller shows it, then calls Close().
public class LoadingBox : Window {
  private readonly TextBlock _label;

  public LoadingBox(string title, string prompt) {
    Title = title;
    Width = 300;
    SizeToContent = SizeToContent.Height;
    CanResize = false;
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    Background = new SolidColorBrush(Color.Parse("#262728"));
    _label = new TextBlock {
      Text = prompt,
      Foreground = Brushes.WhiteSmoke,
      Margin = new Thickness(20),
      HorizontalAlignment = HorizontalAlignment.Center,
    };
    Content = _label;
  }

  public void SetText(string text) => Dispatcher.UIThread.Post(() => _label.Text = text);

  public void Show2() {
    var owner = Dialogs.Owner;
    if (owner != null) {
      Show(owner);
    } else {
      Show();
    }
  }
}

// Determinate progress window with status + cancel (mirrors ProgressReporterDialogue). Long ops
// report via Set(pct, status); CancelRequested flips when the user cancels.
public class ProgressReporter : Window {
  private readonly ProgressBar _bar = new() { Maximum = 100, Height = 18, Margin = new Thickness(0, 8, 0, 8) };
  private readonly TextBlock _status = new() { Foreground = Brushes.WhiteSmoke };
  private readonly CancellationTokenSource _cts = new();

  public CancellationToken Token => _cts.Token;
  public bool CancelRequested => _cts.IsCancellationRequested;

  public ProgressReporter(string title) {
    Title = title;
    Width = 360;
    SizeToContent = SizeToContent.Height;
    CanResize = false;
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    Background = new SolidColorBrush(Color.Parse("#262728"));
    var cancel = new Button {
      Content = "Cancel",
      MinWidth = 80,
      HorizontalAlignment = HorizontalAlignment.Right,
    };
    cancel.Click += (_, _) => _cts.Cancel();
    Content = new StackPanel {
      Margin = new Thickness(16),
      Children = { _status, _bar, cancel },
    };
  }

  public void Set(double percent, string status) => Dispatcher.UIThread.Post(() => {
    _bar.Value = Math.Clamp(percent, 0, 100);
    _status.Text = status;
  });

  public void Show2() {
    var owner = Dialogs.Owner;
    if (owner != null) {
      Show(owner);
    } else {
      Show();
    }
  }
}
