using System;
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
    if (cs?.messages != null && cs.messages.Count > 0) {
      box.Text = string.Join(Environment.NewLine,
          cs.messages.TakeLast(200)
              .Select(m => $"{m.time:HH:mm:ss}  {m.message?.TrimEnd()}"));
    } else {
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
