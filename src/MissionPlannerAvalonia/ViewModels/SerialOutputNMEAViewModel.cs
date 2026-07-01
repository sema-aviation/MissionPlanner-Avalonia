using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels;

public partial class SerialOutputNMEAViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private TcpListener? _listener;
  private ICommsSerial? _stream;
  private Thread? _thread;
  private volatile bool _run;

  public SerialOutputNMEAViewModel() {
    RefreshPorts();
    SelectedBaud = 4800;
    UpdateRateHz = 5;
  }

  public ObservableCollection<string> Ports { get; } = new();

  public ObservableCollection<int> Bauds { get; } = new() {
      4800, 9600, 19200, 38400, 57600, 115200,
  };

  public ObservableCollection<double> Rates { get; } = new() { 1, 2, 5, 10 };

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud;

  [ObservableProperty]
  private double _updateRateHz;

  [ObservableProperty]
  private string _status = "Stopped.";

  [ObservableProperty]
  private string _connectButtonText = "Connect";

  [ObservableProperty]
  private string _lastSentence = "";

  public bool IsRunning => _run;

  [RelayCommand]
  private void RefreshPorts() {
    var sel = SelectedPort;
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }

    Ports.Add("TCP Host - 14551");
    Ports.Add("UDP Host - 14551");
    SelectedPort = sel != null && Ports.Contains(sel) ? sel : Ports.FirstOrDefault();
  }

  [RelayCommand]
  private void ToggleConnect() {
    if (_run || _listener != null) {
      Stop();
      return;
    }

    if (string.IsNullOrEmpty(SelectedPort)) {
      Status = "Pick a port first.";
      return;
    }

    try {
      switch (SelectedPort) {
        case "TCP Host - 14551": {
            var tcp = new TcpSerial();
            _stream = tcp;
            _listener = new TcpListener(IPAddress.Any, 14551);
            _listener.Start(0);
            _listener.BeginAcceptTcpClient(OnAcceptTcpClient, (_listener, tcp));
            break;
          }

        case "UDP Host - 14551": {
            var udp = new UdpSerial { Port = "14551" };
            udp.client = new UdpClient(14551);
            _stream = udp;
            _stream.Open();
            break;
          }

        default:
          _stream = new SerialPort { PortName = SelectedPort, BaudRate = SelectedBaud };
          _stream.BaudRate = SelectedBaud;
          _stream.Open();
          break;
      }
    } catch (Exception ex) {
      Stop();
      Status = "Error connecting: " + ex.Message;
      return;
    }

    _run = true;
    _thread = new Thread(MainLoop) { IsBackground = true, Name = "Nmea output" };
    _thread.Start();
    ConnectButtonText = "Stop";
    Status = $"Emitting NMEA on {SelectedPort}.";
    OnPropertyChanged(nameof(IsRunning));
  }

  private void OnAcceptTcpClient(IAsyncResult ar) {
    var (listener, tcp) = ((TcpListener, TcpSerial))ar.AsyncState!;
    try {
      var client = listener.EndAcceptTcpClient(ar);
      tcp.client = client;
      listener.BeginAcceptTcpClient(OnAcceptTcpClient, (listener, tcp));
    } catch {

    }
  }

  private void MainLoop() {
    while (_run) {
      try {
        if (_stream == null || !_stream.IsOpen) {
          Thread.Sleep(20);
          continue;
        }

        var cs = _comPort.MAV.cs;

        double lat = (int)cs.lat + (cs.lat - (int)cs.lat) * .6f;
        double lng = (int)cs.lng + (cs.lng - (int)cs.lng) * .6f;
        var now = DateTime.Now.ToUniversalTime();

        string line = string.Format(CultureInfo.InvariantCulture,
            "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},", "GGA",
            now, Math.Abs(lat * 100).ToString("0000.00000", CultureInfo.InvariantCulture),
            cs.lat < 0 ? "S" : "N",
            Math.Abs(lng * 100).ToString("00000.00000", CultureInfo.InvariantCulture),
            cs.lng < 0 ? "W" : "E",
            cs.gpsstatus >= 3 ? 1 : 0, cs.satcount, cs.gpshdop,
            cs.altasl / CurrentState.multiplieralt, "M", "0.0", "M", "");
        Send(line);

        line = string.Format(CultureInfo.InvariantCulture,
            "$GP{0},{1},{2},{3},{4},{5:HHmmss.fff},{6},{7}", "GLL",
            Math.Abs(lat * 100).ToString("0000.00", CultureInfo.InvariantCulture),
            cs.lat < 0 ? "S" : "N",
            Math.Abs(lng * 100).ToString("00000.00", CultureInfo.InvariantCulture),
            cs.lng < 0 ? "W" : "E", now, "A", "A");
        Send(line);

        line = string.Format(CultureInfo.InvariantCulture,
            "$GP{0},{1:0.0},{2},{3},{4},{5}", "HDG", cs.yaw, 0, "E", 0, "E");
        Send(line);

        line = string.Format(CultureInfo.InvariantCulture,
            "$GP{0},{1},{2},{3},{4}", "VTG",
            cs.groundcourse.ToString("000"), cs.yaw.ToString("000"),
            (cs.groundspeed * 1.943844).ToString("00.0", CultureInfo.InvariantCulture),
            (cs.groundspeed * 3.6).ToString("00.0", CultureInfo.InvariantCulture));
        Send(line);

        line = string.Format(CultureInfo.InvariantCulture,
            "$GP{0},{1:HHmmss.fff},{2},{3},{4},{5},{6},{7},{8},{9:ddMMyy},{10},{11},{12}", "RMC",
            now, "A", Math.Abs(lat * 100).ToString("0.00000", CultureInfo.InvariantCulture),
            cs.lat < 0 ? "S" : "N",
            Math.Abs(lng * 100).ToString("0.00000", CultureInfo.InvariantCulture),
            cs.lng < 0 ? "W" : "E",
            (cs.groundspeed * 1.943844).ToString("0.0", CultureInfo.InvariantCulture),
            cs.groundcourse.ToString("0.0", CultureInfo.InvariantCulture), DateTime.Now, 0, "E", "A");
        Send(line);

        var nextSend = DateTime.Now.AddMilliseconds(1000 / Math.Max(0.1, UpdateRateHz));
        var sleepFor = Math.Min((int)Math.Abs((nextSend - DateTime.Now).TotalMilliseconds), 4000);
        Thread.Sleep(Math.Max(1, sleepFor));
      } catch {
        Thread.Sleep(50);
      }
    }
  }

  private void Send(string line) {
    string checksum = GetChecksum(line);
    string full = line + "*" + checksum + "\r";
    _stream?.WriteLine(full);
    Dispatcher.UIThread.Post(() => LastSentence = full.TrimEnd());
  }

  public static string GetChecksum(string sentence) {
    int checksum = 0;
    foreach (char c in sentence) {
      if (c == '$') {
        continue;
      }

      if (c == '*') {
        break;
      }

      checksum = checksum == 0 ? Convert.ToByte(c) : checksum ^ Convert.ToByte(c);
    }

    return checksum.ToString("X2");
  }

  private void Stop() {
    _run = false;
    try {
      _listener?.Stop();
    } catch {
    }

    _listener = null;

    try {
      _thread?.Join(500);
    } catch {
    }

    _thread = null;

    try {
      if (_stream != null && _stream.IsOpen) {
        _stream.Close();
      }
    } catch {
    }

    _stream = null;
    ConnectButtonText = "Connect";
    Status = "Stopped.";
    OnPropertyChanged(nameof(IsRunning));
  }

  public void Dispose() => Stop();
}
