using System;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigTerminalViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;

  [ObservableProperty]
  private string _output = "";

  [ObservableProperty]
  private string _input = "";

  [ObservableProperty]
  private string _status = "Open the link, then type a command and press Send.";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigTerminalViewModel() {
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private void Pump() {
    ICommsSerial port = _comPort.BaseStream;
    if (port == null || !port.IsOpen) {
      return;
    }

    try {
      if (port.BytesToRead > 0) {
        string data = port.ReadExisting();
        if (!string.IsNullOrEmpty(data)) {
          Append(data);
        }
      }
    } catch {
      // ignore transient read errors (matches upstream)
    }
  }

  private void Append(string data) {
    data = data.TrimEnd('\r');
    data = data.Replace("\0", " ");

    string text = Output + data;
    int back = text.IndexOf('\b');
    while (back >= 0) {
      text = text.Remove(back == 0 ? 0 : back - 1, back == 0 ? 1 : 2);
      back = text.IndexOf('\b');
    }

    const int max = 64 * 1024;
    if (text.Length > max) {
      text = text.Substring(text.Length - max);
    }

    Output = text;
  }

  [RelayCommand]
  private void Send() {
    ICommsSerial port = _comPort.BaseStream;
    if (port == null || !port.IsOpen) {
      Status = "Not connected — open the serial/MAVLink link first.";
      return;
    }

    string line = Input ?? "";
    try {
      if (line == "+++") {
        port.Write(line);
      } else {
        port.Write(line + "\r");
      }

      Append("\n" + line + "\n");
      Status = "Sent.";
    } catch {
      Status = "Error writing to com port.";
    }

    Input = "";
  }

  [RelayCommand]
  private void Clear() {
    Output = "";
  }

  public void Dispose() => _timer.Stop();
}
