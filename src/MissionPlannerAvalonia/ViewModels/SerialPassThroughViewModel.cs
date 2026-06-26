using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels;

// Mavlink Mirror — port of MissionPlanner.Controls.SerialOutputPass (Controls/SerialOutputPass.cs).
// Opens a second link (serial / TCP host / UDP host) and forwards the MAVLink byte stream both ways
// between it and the vehicle link. The forwarding itself is done inside MAVLinkInterface: setting
// comPort.MirrorStream makes the read loop copy every received buffer out to the mirror, and (when
// "allow write back to vehicle" is ticked) MirrorStreamWrite copies bytes read from the mirror back
// into the vehicle link (MAVLinkInterface.ProcessMirrorStream). We only manage the stream lifecycle
// and surface live status + byte counters via a CountingCommsSerial wrapper.
public partial class SerialPassThroughViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _poll;

  private TcpListener? _listener;
  private CountingCommsSerial? _stream;

  public SerialPassThroughViewModel() {
    RefreshPorts();
    SelectedBaud = 115200;

    _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    _poll.Tick += (_, _) => UpdateStatus();
    _poll.Start();

    UpdateStatus();
  }

  public ObservableCollection<string> Ports { get; } = new();

  public ObservableCollection<int> Bauds { get; } = new() {
      1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 921600,
  };

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud;

  // When true, bytes received from the mirror are written back into the vehicle link.
  [ObservableProperty]
  private bool _allowWriteBack;

  [ObservableProperty]
  private string _status = "Stopped.";

  [ObservableProperty]
  private string _connectButtonText = "Start";

  [ObservableProperty]
  private long _txBytes;

  [ObservableProperty]
  private long _rxBytes;

  public bool IsRunning =>
      _stream != null && _stream.IsOpen || _listener != null;

  partial void OnAllowWriteBackChanged(bool value) {
    _comPort.MirrorStreamWrite = value;
    if (_stream != null) {
      // keep the active mirror entry in sync
      foreach (var m in _comPort.Mirrors.Where(m => ReferenceEquals(m.MirrorStream, _stream))) {
        m.MirrorStreamWrite = value;
      }
    }
  }

  [RelayCommand]
  private void RefreshPorts() {
    var sel = SelectedPort;
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }

    Ports.Add("TCP Host - 14550");
    Ports.Add("UDP Host - 14550");
    SelectedPort = sel != null && Ports.Contains(sel) ? sel : Ports.FirstOrDefault();
  }

  [RelayCommand]
  private void ToggleConnect() {
    if (IsRunning) {
      Stop();
      return;
    }

    if (string.IsNullOrEmpty(SelectedPort)) {
      Status = "Pick a port first.";
      return;
    }

    try {
      ICommsSerial inner;
      switch (SelectedPort) {
        case "TCP Host - 14550": {
            var tcp = new TcpSerial();
            _stream = new CountingCommsSerial(tcp);
            _comPort.MirrorStream = _stream;
            _comPort.MirrorStreamWrite = AllowWriteBack;
            _listener = new TcpListener(IPAddress.Any, 14550);
            _listener.Start(0);
            _listener.BeginAcceptTcpClient(OnAcceptTcpClient, (_listener, tcp));
            ConnectButtonText = "Stop";
            Status = "Listening on TCP 14550 — waiting for a client…";
            return;
          }

        case "UDP Host - 14550": {
            var udp = new UdpSerial { Port = "14550" };
            udp.client = new UdpClient(14550);
            inner = udp;
            break;
          }

        default:
          inner = new SerialPort { PortName = SelectedPort, BaudRate = SelectedBaud };
          break;
      }

      inner.BaudRate = SelectedBaud;
      _stream = new CountingCommsSerial(inner);
      _stream.Open();

      _comPort.MirrorStream = _stream;
      _comPort.MirrorStreamWrite = AllowWriteBack;

      ConnectButtonText = "Stop";
      Status = $"Mirroring on {SelectedPort}.";
    } catch (Exception ex) {
      Stop();
      Status = "Error connecting: " + ex.Message;
    }
  }

  private void OnAcceptTcpClient(IAsyncResult ar) {
    var (listener, tcp) = ((TcpListener, TcpSerial))ar.AsyncState!;
    try {
      var client = listener.EndAcceptTcpClient(ar);
      tcp.client = client;
      Dispatcher.UIThread.Post(() => Status = "Mirroring on TCP 14550 (client connected).");
      listener.BeginAcceptTcpClient(OnAcceptTcpClient, (listener, tcp));
    } catch {
      // listener stopped
    }
  }

  private void Stop() {
    try {
      _listener?.Stop();
    } catch {
    }

    _listener = null;

    try {
      if (_stream != null) {
        _comPort.Mirrors.RemoveAll(m => ReferenceEquals(m.MirrorStream, _stream));
        if (_stream.IsOpen) {
          _stream.Close();
        }
      }
    } catch {
    }

    _stream = null;
    ConnectButtonText = "Start";
    Status = "Stopped.";
  }

  private void UpdateStatus() {
    if (_stream != null) {
      TxBytes = _stream.TxCount;
      RxBytes = _stream.RxCount;
    }

    OnPropertyChanged(nameof(IsRunning));
  }

  public void Dispose() {
    _poll.Stop();
    Stop();
  }

  // ICommsSerial decorator that counts bytes flowing both ways through the mirror link so the UI can
  // show live Tx/Rx counters. All other members delegate straight to the wrapped stream.
  private sealed class CountingCommsSerial : ICommsSerial {
    private readonly ICommsSerial _inner;

    public CountingCommsSerial(ICommsSerial inner) => _inner = inner;

    public long TxCount { get; private set; }
    public long RxCount { get; private set; }

    public Stream BaseStream => _inner.BaseStream;
    public int BaudRate { get => _inner.BaudRate; set => _inner.BaudRate = value; }
    public int BytesToRead => _inner.BytesToRead;
    public int BytesToWrite => _inner.BytesToWrite;
    public int DataBits { get => _inner.DataBits; set => _inner.DataBits = value; }
    public bool DtrEnable { get => _inner.DtrEnable; set => _inner.DtrEnable = value; }
    public bool IsOpen => _inner.IsOpen;
    public string PortName { get => _inner.PortName; set => _inner.PortName = value; }
    public int ReadBufferSize { get => _inner.ReadBufferSize; set => _inner.ReadBufferSize = value; }
    public int ReadTimeout { get => _inner.ReadTimeout; set => _inner.ReadTimeout = value; }
    public bool RtsEnable { get => _inner.RtsEnable; set => _inner.RtsEnable = value; }
    public int WriteBufferSize {
      get => _inner.WriteBufferSize;
      set => _inner.WriteBufferSize = value;
    }
    public int WriteTimeout { get => _inner.WriteTimeout; set => _inner.WriteTimeout = value; }

    public void Close() => _inner.Close();
    public void DiscardInBuffer() => _inner.DiscardInBuffer();
    public void Open() => _inner.Open();

    public int Read(byte[] buffer, int offset, int count) {
      int n = _inner.Read(buffer, offset, count);
      RxCount += n;
      return n;
    }

    public int ReadByte() {
      int b = _inner.ReadByte();
      if (b >= 0) {
        RxCount++;
      }

      return b;
    }

    public int ReadChar() {
      int c = _inner.ReadChar();
      if (c >= 0) {
        RxCount++;
      }

      return c;
    }

    public string ReadExisting() {
      var s = _inner.ReadExisting();
      RxCount += s?.Length ?? 0;
      return s;
    }

    public string ReadLine() {
      var s = _inner.ReadLine();
      RxCount += s?.Length ?? 0;
      return s;
    }

    public void Write(string text) {
      _inner.Write(text);
      TxCount += text?.Length ?? 0;
    }

    public void Write(byte[] buffer, int offset, int count) {
      _inner.Write(buffer, offset, count);
      TxCount += count;
    }

    public void WriteLine(string text) {
      _inner.WriteLine(text);
      TxCount += (text?.Length ?? 0) + 1;
    }

    public void toggleDTR() => _inner.toggleDTR();
    public void Dispose() => _inner.Dispose();
  }
}
