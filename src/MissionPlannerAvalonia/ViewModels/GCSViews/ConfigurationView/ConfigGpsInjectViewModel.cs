using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigGpsInjectViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private CommsNTRIP _ntrip;
  private Thread _worker;
  private volatile bool _running;
  private long _bytesTotal;
  private long _bytesLastSecond;
  private long _bytesThisSecond;
  private DateTime _rateMark = DateTime.Now;

  [ObservableProperty]
  private string _host = "";

  [ObservableProperty]
  private int _port = 2101;

  [ObservableProperty]
  private string _mount = "";

  [ObservableProperty]
  private string _username = "";

  [ObservableProperty]
  private string _password = "";

  [ObservableProperty]
  private bool _ntripV1;

  [ObservableProperty]
  private bool _sendGga = true;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ConnectLabel))]
  private bool _connected;

  [ObservableProperty]
  private string _status = "Enter NTRIP caster details and press Connect.";

  [ObservableProperty]
  private string _injected = "0 bytes";

  public string ConnectLabel => Connected ? "Disconnect" : "Connect";

  public ConfigGpsInjectViewModel() {
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
    _timer.Tick += (_, _) => UpdateStats();
    _timer.Start();
  }

  private void UpdateStats() {
    var now = DateTime.Now;
    if ((now - _rateMark).TotalSeconds >= 1) {
      _bytesLastSecond = Interlocked.Exchange(ref _bytesThisSecond, 0);
      _rateMark = now;
    }
    Injected = $"{_bytesTotal} bytes ({_bytesLastSecond} B/s)";
  }

  [RelayCommand]
  [Obsolete]
  private void ToggleConnect() {
    if (Connected) {
      StopWorker();
      Status = "Disconnected.";
      Connected = false;
      return;
    }

    if (string.IsNullOrWhiteSpace(Host)) {
      Status = "Host is required.";
      return;
    }

    var url = BuildUrl();

    try {
      var ntrip = new CommsNTRIP { ntrip_v1 = NtripV1 };
      if (SendGga) {
        var cs = _comPort.MAV.cs;
        ntrip.lat = cs.lat;
        ntrip.lng = cs.lng;
        ntrip.alt = cs.altasl;
      }
      ntrip.Open(url);
      _ntrip = ntrip;
    } catch (Exception ex) {
      Status = "Connect failed: " + ex.Message;
      _ntrip = null;
      return;
    }

    _bytesTotal = 0;
    _bytesThisSecond = 0;
    _bytesLastSecond = 0;
    _rateMark = DateTime.Now;
    Connected = true;
    Status = "Connected — injecting RTCM to vehicle.";
    StartWorker();
  }

  private string BuildUrl() {
    var userinfo = "";
    if (!string.IsNullOrEmpty(Username)) {
      userinfo = Username;
      if (!string.IsNullOrEmpty(Password)) {
        userinfo += ":" + Password;
      }
      userinfo += "@";
    }
    var mount = Mount?.TrimStart('/') ?? "";
    return $"http://{userinfo}{Host}:{Port}/{mount}";
  }

  [Obsolete]
  private void StartWorker() {
    _running = true;
    _worker = new Thread(Loop) { IsBackground = true, Name = "NTRIP inject" };
    _worker.Start();
  }

  private void StopWorker() {
    _running = false;
    try {
      _worker?.Join(1000);
    } catch {
    }
    _worker = null;
    try {
      _ntrip?.Close();
    } catch {
    }
    _ntrip = null;
  }

  [Obsolete]
  private void Loop() {
    var buffer = new byte[1024 * 4];
    while (_running) {
      var ntrip = _ntrip;
      if (ntrip == null) {
        break;
      }

      try {
        if (!ntrip.IsOpen) {
          Fail("NTRIP connection closed.");
          break;
        }

        var avail = ntrip.BytesToRead;
        if (avail <= 0) {
          Thread.Sleep(20);
          continue;
        }

        var toread = Math.Min(avail, buffer.Length);
        var read = ntrip.Read(buffer, 0, toread);
        if (read <= 0) {
          Thread.Sleep(20);
          continue;
        }

        var packet = new byte[read];
        Array.Copy(buffer, 0, packet, 0, read);
        _comPort.InjectGpsData(packet, (ushort)read);

        Interlocked.Add(ref _bytesThisSecond, read);
        _bytesTotal += read;
      } catch (Exception ex) {
        Fail("Inject error: " + ex.Message);
        break;
      }
    }
  }

  private void Fail(string message) {
    _running = false;
    Dispatcher.UIThread.Post(() => {
      Status = message;
      Connected = false;
    });
  }

  public void Dispose() {
    _timer.Stop();
    StopWorker();
  }
}
