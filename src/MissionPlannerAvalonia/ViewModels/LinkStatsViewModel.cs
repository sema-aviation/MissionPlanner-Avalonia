using System;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

// Live link-statistics popup (mirrors MP's CTRL+L "Link Stats" / mavlink link quality readout).
// Subscribes to the active link's byte/packet observables and recomputes a 1 Hz snapshot of
// throughput, packet count, packet loss and link quality. All counters are written from the
// MAVLink read thread (the Subjects fire off-UI-thread) and read back on the UI-thread timer,
// so accumulation uses Interlocked. Every subscription is torn down in Dispose (window Closed).
public partial class LinkStatsViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _mav = AppState.comPort;
  private readonly DispatcherTimer _timer;

  // Cross-thread accumulators (incremented on the link thread, drained on the UI thread).
  private long _rxBytes;
  private long _txBytes;
  private long _packetsLost;
  private long _packetsReceived;

  private readonly IDisposable? _rxSub;
  private readonly IDisposable? _txSub;
  private readonly IDisposable? _lostSub;
  private readonly IDisposable? _recvSub;

  private readonly DateTime _start = DateTime.Now;

  [ObservableProperty]
  private string _rxRate = "0 B/s";

  [ObservableProperty]
  private string _txRate = "0 B/s";

  [ObservableProperty]
  private string _packetCount = "0";

  [ObservableProperty]
  private string _packetsLostText = "0";

  [ObservableProperty]
  private string _linkQuality = "—";

  [ObservableProperty]
  private string _timeConnected = "00:00:00";

  [ObservableProperty]
  private string _status = "";

  public LinkStatsViewModel() {
    // IObservable<int>.Subscribe(IObserver<int>) lives in mscorlib, so we don't depend on the
    // System.Reactive extension methods being visible to this project.
    _rxSub = _mav.BytesReceived.Subscribe(new ActionObserver(n => Interlocked.Add(ref _rxBytes, n)));
    _txSub = _mav.BytesSent.Subscribe(new ActionObserver(n => Interlocked.Add(ref _txBytes, n)));
    _lostSub = _mav.WhenPacketLost.Subscribe(new ActionObserver(n => Interlocked.Add(ref _packetsLost, n)));
    _recvSub = _mav.WhenPacketReceived.Subscribe(new ActionObserver(n => Interlocked.Add(ref _packetsReceived, n)));

    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
    Tick();
  }

  private void Tick() {
    // Drain the per-second byte counters; whatever accumulated since the last tick is the rate.
    long rx = Interlocked.Exchange(ref _rxBytes, 0);
    long tx = Interlocked.Exchange(ref _txBytes, 0);
    RxRate = Format(rx);
    TxRate = Format(tx);

    PacketCount = _mav.packetcount.ToString();

    long lost = Interlocked.Read(ref _packetsLost);
    long recv = Interlocked.Read(ref _packetsReceived);
    PacketsLostText = lost.ToString();

    long total = lost + recv;
    LinkQuality = total > 0 ? (100.0 * recv / total).ToString("0.0") + " %" : "—";

    bool connected = _mav.BaseStream?.IsOpen == true;
    Status = connected ? "Connected" : "Disconnected";
    TimeConnected = (DateTime.Now - _start).ToString(@"hh\:mm\:ss");
  }

  private static string Format(long bytesPerSec) {
    if (bytesPerSec >= 1024 * 1024) {
      return (bytesPerSec / 1024.0 / 1024.0).ToString("0.0") + " MB/s";
    }
    if (bytesPerSec >= 1024) {
      return (bytesPerSec / 1024.0).ToString("0.0") + " KB/s";
    }
    return bytesPerSec + " B/s";
  }

  public void Dispose() {
    _timer.Stop();
    _rxSub?.Dispose();
    _txSub?.Dispose();
    _lostSub?.Dispose();
    _recvSub?.Dispose();
  }

  // Minimal IObserver<int> so we can subscribe to the link's Subjects without taking a hard
  // dependency on the System.Reactive Subscribe(Action<T>) extension method.
  private sealed class ActionObserver : IObserver<int> {
    private readonly Action<int> _onNext;
    public ActionObserver(Action<int> onNext) => _onNext = onNext;
    public void OnNext(int value) => _onNext(value);
    public void OnError(Exception error) { }
    public void OnCompleted() { }
  }
}
