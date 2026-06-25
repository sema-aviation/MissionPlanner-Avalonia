using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

public partial class ConnectionViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<string> Ports { get; } = new();
  public ObservableCollection<int> Bauds { get; } =
      new() { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud = 115200;

  [ObservableProperty]
  private bool _isConnected;

  [ObservableProperty]
  private string _connectText = "CONNECT";

  [ObservableProperty]
  private string _status = "";

  public ConnectionViewModel() => RefreshPorts();

  [RelayCommand]
  private void RefreshPorts() {
    var cur = SelectedPort;
    Ports.Clear();
    foreach (var p in MissionPlanner.Comms.SerialPort.GetPortNames().Where(p => p.StartsWith("/dev/cu."))) {
      Ports.Add(p);
    }

    SelectedPort = Ports.Contains(cur ?? "") ? cur : Ports.FirstOrDefault();
  }

  [RelayCommand]
  private async Task ToggleConnect() {
    if (_comPort.BaseStream?.IsOpen == true) {
      await Task.Run(() => _comPort.Close());
      IsConnected = false;
      ConnectText = "CONNECT";
      Status = "Disconnected.";
      return;
    }

    if (string.IsNullOrEmpty(SelectedPort)) {
      Status = "No port selected.";
      return;
    }

    Status = $"Connecting {SelectedPort} @ {SelectedBaud}…";
    try {
      _comPort.BaseStream = new MissionPlanner.Comms.SerialPort {
        PortName = SelectedPort,
        BaudRate = SelectedBaud,
      };
      await Task.Run(() => _comPort.Open(getparams: true, skipconnectedcheck: true, showui: false));
      IsConnected = _comPort.BaseStream.IsOpen;
      ConnectText = IsConnected ? "DISCONNECT" : "CONNECT";
      Status = IsConnected ? $"Connected. {_comPort.MAV.param.Count} params." : "Connect failed.";
    } catch (Exception ex) {
      Status = "Connect error: " + ex.Message;
      IsConnected = false;
    }
  }
}
