using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MissionPlannerAvalonia.Controls;

namespace MissionPlannerAvalonia.Views;

// Live raw IMU/mag/baro readout, mirrors MP "Raw Sensor View".
public class RawSensorWindow : Window {
  private readonly DispatcherTimer _timer;
  private readonly TextBlock _text = new() {
    FontFamily = new FontFamily("Consolas,Menlo,monospace"),
    FontSize = 13,
    Foreground = Brushes.WhiteSmoke,
    Margin = new Avalonia.Thickness(12),
  };

  public RawSensorWindow() {
    Title = "Raw Sensor View";
    Width = 320;
    Height = 360;
    Background = new SolidColorBrush(Color.Parse("#1F1F20"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    Content = new ScrollViewer { Content = _text };
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Refresh();
    _timer.Start();
    Closed += (_, _) => _timer.Stop();
    Refresh();
  }

  private void Refresh() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null) {
      _text.Text = "No vehicle state.";
      return;
    }
    _text.Text =
        $"Accel (mg)\n  X {cs.ax,10:0.0}\n  Y {cs.ay,10:0.0}\n  Z {cs.az,10:0.0}\n\n" +
        $"Gyro (deg/s)\n  X {cs.gx,10:0.000}\n  Y {cs.gy,10:0.000}\n  Z {cs.gz,10:0.000}\n\n" +
        $"Mag\n  X {cs.mx,10:0.0}\n  Y {cs.my,10:0.0}\n  Z {cs.mz,10:0.0}\n\n" +
        $"Baro\n  Press {cs.press_abs,8:0.00} Pa\n  Temp  {cs.press_temp,8} °C\n  Alt   {cs.alt,8:0.00} m";
  }
}

// Lists recent STATUSTEXT messages logged in cs.messages.
public class MessagesWindow : Window {
  public MessagesWindow() {
    Title = "Messages";
    Width = 520;
    Height = 360;
    Background = new SolidColorBrush(Color.Parse("#1F1F20"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    var box = new TextBox {
      AcceptsReturn = true,
      IsReadOnly = true,
      TextWrapping = TextWrapping.Wrap,
      Background = new SolidColorBrush(Color.Parse("#1F1F20")),
      Foreground = Brushes.WhiteSmoke,
    };
    var cs = AppState.comPort.MAV?.cs;
    try {
      var msgs = cs?.messages?.ToArray() ?? Array.Empty<(DateTime, string)>();
      box.Text = msgs.Length > 0
          ? string.Join(Environment.NewLine,
              msgs.TakeLast(200).Select(m => $"{m.time:HH:mm:ss}  {m.message?.TrimEnd()}"))
          : "No messages received.";
    } catch (InvalidOperationException) {
      box.Text = "No messages received.";
    }
    Content = box;
  }
}

// Hosts a VideoControl in a popup so the HUD video menu can drive it.
public class VideoPopupWindow : Window {
  public VideoControl Video { get; }
  private readonly TextBlock _status = new() {
    Foreground = Brushes.WhiteSmoke,
    Margin = new Avalonia.Thickness(6),
    VerticalAlignment = VerticalAlignment.Center,
  };

  public VideoPopupWindow(VideoControl video) {
    Video = video;
    Title = "Video";
    Width = 640;
    Height = 400;
    Background = Brushes.Black;
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    var grid = new Grid();
    grid.RowDefinitions = new RowDefinitions("*,Auto");
    Grid.SetRow(video, 0);
    Grid.SetRow(_status, 1);
    grid.Children.Add(video);
    grid.Children.Add(_status);
    Content = grid;
    UpdateStatus();
  }

  public void UpdateStatus() => _status.Text = Video.Status;
}

// Live EKF variance bars (velocity/pos-horiz/pos-vert/compass/terrain), mirrors MP EKFStatus.
// Values are variance*100; orange >50, red >80 — the standard ArduPilot warning thresholds.
public class EKFStatusWindow : Window {
  private readonly DispatcherTimer _timer;
  private readonly (string label, ProgressBar bar)[] _rows;
  private readonly TextBlock _flags = new() {
    Foreground = Brushes.WhiteSmoke,
    FontSize = 11,
    Margin = new Avalonia.Thickness(0, 8, 0, 0),
  };

  public EKFStatusWindow() {
    Title = "EKF Status";
    Width = 320;
    Height = 320;
    Background = new SolidColorBrush(Color.Parse("#1F1F20"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _rows = new[] { "Velocity", "Pos Horiz", "Pos Vert", "Compass", "Terrain" }
        .Select(l => (l, new ProgressBar { Maximum = 100, Height = 16 })).ToArray();
    var panel = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 6 };
    foreach (var (label, bar) in _rows) {
      panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.WhiteSmoke, FontSize = 12 });
      panel.Children.Add(bar);
    }
    panel.Children.Add(_flags);
    Content = new ScrollViewer { Content = panel };
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Refresh();
    _timer.Start();
    Closed += (_, _) => _timer.Stop();
    Refresh();
  }

  private void Refresh() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null) {
      return;
    }
    var vals = new[] { cs.ekfvelv, cs.ekfposhor, cs.ekfposvert, cs.ekfcompv, cs.ekfteralt };
    for (int i = 0; i < _rows.Length; i++) {
      var v = (int)(vals[i] * 100);
      _rows[i].bar.Value = v;
      _rows[i].bar.Foreground = v > 80 ? Brushes.Red : v > 50 ? Brushes.Orange : Brushes.LimeGreen;
    }
    _flags.Text = "Flags: 0x" + cs.ekfflags.ToString("X");
  }
}

// Live vibration bars + clip counters, mirrors MP Vibration. >30 m/s/s is the ArduPilot caution
// line, >60 the danger line.
public class VibrationWindow : Window {
  private readonly DispatcherTimer _timer;
  private readonly (string label, ProgressBar bar)[] _rows;
  private readonly TextBlock _clips = new() { Foreground = Brushes.WhiteSmoke, FontSize = 12 };

  public VibrationWindow() {
    Title = "Vibration";
    Width = 300;
    Height = 240;
    Background = new SolidColorBrush(Color.Parse("#1F1F20"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;
    _rows = new[] { "Vibe X", "Vibe Y", "Vibe Z" }
        .Select(l => (l, new ProgressBar { Maximum = 100, Height = 16 })).ToArray();
    var panel = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 6 };
    foreach (var (label, bar) in _rows) {
      panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.WhiteSmoke, FontSize = 12 });
      panel.Children.Add(bar);
    }
    panel.Children.Add(_clips);
    Content = panel;
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Refresh();
    _timer.Start();
    Closed += (_, _) => _timer.Stop();
    Refresh();
  }

  private void Refresh() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null) {
      return;
    }
    var vals = new[] { cs.vibex, cs.vibey, cs.vibez };
    for (int i = 0; i < _rows.Length; i++) {
      var v = (int)vals[i];
      _rows[i].bar.Value = v;
      _rows[i].bar.Foreground = v > 60 ? Brushes.Red : v > 30 ? Brushes.Orange : Brushes.LimeGreen;
    }
    _clips.Text = $"Clipping: {cs.vibeclip0} / {cs.vibeclip1} / {cs.vibeclip2}";
  }
}

// Live prearm status + last high-severity message, mirrors MP PrearmStatus.
public class PrearmStatusWindow : Window {
  private readonly DispatcherTimer _timer;
  private readonly TextBlock _state = new() { FontSize = 18, FontWeight = FontWeight.Bold };
  private readonly TextBlock _summary = new() { Foreground = Brushes.Gray, FontSize = 12 };
  private readonly ItemsControl _checks = new();

  public PrearmStatusWindow() {
    Title = "Arming Checks";
    Width = 460;
    Height = 320;
    Background = new SolidColorBrush(Color.Parse("#1F1F20"));
    WindowStartupLocation = WindowStartupLocation.CenterOwner;

    var header = new StackPanel { Spacing = 2, Children = { _state, _summary } };
    var list = new ScrollViewer {
      Content = _checks,
      VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
    };
    var root = new DockPanel { Margin = new Avalonia.Thickness(14) };
    DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
    root.Children.Add(header);
    root.Children.Add(list);
    Content = root;

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
    _timer.Tick += (_, _) => Refresh();
    _timer.Start();
    Closed += (_, _) => _timer.Stop();
    Refresh();
  }

  private void Refresh() {
    var cs = AppState.comPort.MAV?.cs;
    if (cs == null) {
      _state.Text = "No vehicle";
      _state.Foreground = Brushes.Gray;
      _summary.Text = "Not connected.";
      _checks.ItemsSource = null;
      return;
    }

    bool ok = cs.prearmstatus;
    _state.Text = ok ? "Ready to Arm" : "Not Ready to Arm";
    _state.Foreground = ok ? Brushes.LimeGreen : Brushes.OrangeRed;

    // Pull the actual failing checks out of the STATUSTEXT stream (ArduPilot emits "PreArm: ..."
    // / "Arm: ..." per failed check). Keep the latest line per distinct check, newest first.
    var failures = new List<TextBlock>();
    // Snapshot first: the reader thread mutates cs.messages (unlocked List) and enumerating it
    // live would throw on this UI thread.
    (DateTime time, string message)[] msgs;
    try {
      msgs = cs.messages?.ToArray() ?? Array.Empty<(DateTime, string)>();
    } catch (InvalidOperationException) {
      return; // contended — refresh again on the next tick
    }
    {
      var seen = new HashSet<string>();
      foreach (var m in msgs.Reverse()) {
        var text = m.message?.Trim();
        if (string.IsNullOrEmpty(text)) {
          continue;
        }

        if (text.IndexOf("arm", StringComparison.OrdinalIgnoreCase) < 0 ||
            text.IndexOf("disarm", StringComparison.OrdinalIgnoreCase) >= 0) {
          continue; // keep PreArm:/Arm: lines, drop "DISARMED" etc.
        }

        if (seen.Add(text)) {
          failures.Add(new TextBlock {
            Text = "•  " + text,
            Foreground = Brushes.OrangeRed,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Avalonia.Thickness(0, 2, 0, 2),
          });
        }
      }
    }

    if (failures.Count == 0) {
      _summary.Text = ok ? "All prearm checks passing." : "No prearm messages received yet.";
      _checks.ItemsSource = ok
          ? null
          : new[] { new TextBlock { Text = "Waiting for check results…", Foreground = Brushes.Gray } };
    } else {
      _summary.Text = $"{failures.Count} failing check(s):";
      _checks.ItemsSource = failures;
    }
  }
}
