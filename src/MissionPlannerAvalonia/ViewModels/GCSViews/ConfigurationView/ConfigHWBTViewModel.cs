using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigHWBTViewModel : ViewModelBase {
  private static readonly Dictionary<int, int> _baudmap = new Dictionary<int, int>
  {
        { 57600, 7 },
        { 38400, 6 },
        { 9600, 4 },
        { 19200, 5 },
        { 115200, 8 },
        { 1200, 1 },
        { 2400, 2 },
        { 4800, 3 },
    };

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly StringBuilder _log = new();

  public ObservableCollection<string> Ports { get; } = new();
  public ObservableCollection<string> Bauds { get; } = new();

  [ObservableProperty]
  private string _selectedPort = "";

  [ObservableProperty]
  private string _name = "";

  [ObservableProperty]
  private string _selectedBaud = "57600";

  [ObservableProperty]
  private string _pin = "1234";

  [ObservableProperty]
  private string _output = "";

  [ObservableProperty]
  private bool _isBusy;

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigHWBTViewModel() {
    foreach (var b in _baudmap.Keys.OrderBy(x => x)) {
      Bauds.Add(b.ToString());
    }

    RefreshPorts();
  }

  private void RefreshPorts() {
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }

    var cur = _comPort.BaseStream?.PortName;
    SelectedPort = Ports.Contains(cur ?? "") ? cur! : Ports.FirstOrDefault() ?? "";
  }

  private void Append(string line) {
    _log.AppendLine(line);
    Dispatcher.UIThread.Post(() => Output = _log.ToString());
  }

  [RelayCommand]
  private async Task Write() {
    if (IsConnected) {
      Append("Please disconnect the main link before configuring Bluetooth.");
      return;
    }

    var portName = SelectedPort;
    if (string.IsNullOrWhiteSpace(portName)) {
      Append("No serial port selected.");
      return;
    }

    if (!int.TryParse(SelectedBaud, out var baudKey) || !_baudmap.ContainsKey(baudKey)) {
      Append("Invalid baud rate.");
      return;
    }

    _log.Clear();
    Output = "";
    IsBusy = true;

    var name = Name;
    var pin = Pin;

    var commands = new[]
    {
            "AT",
            "AT+VERSION",
            string.Format("AT+ROLE={0}\r\n", 0),
            string.Format("AT+NAME={0}\r\n", name),
            string.Format("AT+NAME{0}", name),
            string.Format("AT+BAUD={0}\r\n", SelectedBaud),
            string.Format("AT+BAUD{0}", _baudmap[baudKey]),
            string.Format("AT+PSWD={0}\r\n", pin),
            string.Format("AT+PIN{0}", pin),
            "AT+RESET",
        };

    var pass = await Task.Run(() => RunSequence(portName, commands));

    IsBusy = false;

    if (!pass) {
      Append("Error setting parameter — no device responded.");
    } else {
      Append("Programmed OK.");
    }
  }

  private bool RunSequence(string portName, string[] commands) {
    foreach (var baud in _baudmap) {
      Append("Try baud " + baud.Key);
      try {
        using var port = new SerialPort(portName, baud.Key);
        try {
          port.Open();
        } catch (Exception ex) {
          Append("Could not open port: " + ex.Message);
          return false;
        }

        port.Write("AT");

        Thread.Sleep(1100);

        port.Write("\r\n");

        Thread.Sleep(200);

        var isok = port.ReadExisting();

        if (isok.Contains("OK")) {
          Append("Valid Answer");

          foreach (var cmd in commands) {
            Append("Sending " + cmd);
            port.Write(cmd);
            Thread.Sleep(1000);
            Append("Resp " + port.ReadExisting());
          }

          return true;
        }

        Append("No Answer");
        Thread.Sleep(1100);
      } catch (Exception) {
        Append("Invalid port");
        return false;
      }
    }

    return false;
  }
}
