using System;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigFailSafeViewModel : ParamPageBase, IDisposable {
  private static readonly IBrush NormalBrush = new SolidColorBrush(Color.Parse("#94C11F"));
  private static readonly IBrush ThrottleLowBrush = Brushes.Red;

  private readonly DispatcherTimer _timer;

  public FailsafeChannel[] Channels { get; }

  public ConfigFailSafeViewModel() {
    Title = "Failsafe";
    Intro = "Throttle, battery and GCS failsafe behaviour. Ensure props are removed before testing.";
    F("FS_THR_ENABLE", "combo");
    F("FS_THR_VALUE");
    F("THR_FAILSAFE", "combo");
    F("THR_FS_VALUE");
    F("THR_FS_ACTION", "combo");
    F("FS_GCS_ENABLE", "combo");
    F("FS_SHORT_ACTN", "combo");
    F("FS_LONG_ACTN", "combo");
    F("BATT_FS_LOW_ACT", "combo");
    F("BATT_LOW_VOLT");
    F("BATT_LOW_MAH");
    F("BATT_LOW_TIMER");
    F("FS_BATT_ENABLE", "combo");
    F("FS_BATT_VOLTAGE");
    F("FS_BATT_MAH");

    Channels = new FailsafeChannel[16];
    for (int i = 0; i < 16; i++) {
      Channels[i] = new FailsafeChannel(i + 1) { InBrush = NormalBrush, OutBrush = NormalBrush };
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private void Pump() {
    var cs = comPort.MAV.cs;
    float[] ins = {
        cs.ch1in, cs.ch2in, cs.ch3in, cs.ch4in, cs.ch5in, cs.ch6in, cs.ch7in, cs.ch8in,
        cs.ch9in, cs.ch10in, cs.ch11in, cs.ch12in, cs.ch13in, cs.ch14in, cs.ch15in, cs.ch16in,
    };
    float[] outs = {
        cs.ch1out, cs.ch2out, cs.ch3out, cs.ch4out, cs.ch5out, cs.ch6out, cs.ch7out, cs.ch8out,
        cs.ch9out, cs.ch10out, cs.ch11out, cs.ch12out, cs.ch13out, cs.ch14out, cs.ch15out, cs.ch16out,
    };

    double fsThr = comPort.MAV.param.ContainsKey("FS_THR_VALUE")
        ? comPort.MAV.param["FS_THR_VALUE"].Value
        : 0;

    for (int i = 0; i < 16; i++) {
      Channels[i].In = ins[i];
      Channels[i].Out = outs[i];
    }

    // ch3 is the throttle channel: recolor red when below the failsafe threshold.
    Channels[2].InBrush = (fsThr > 0 && ins[2] > 0 && ins[2] < fsThr) ? ThrottleLowBrush : NormalBrush;
  }

  public void Dispose() => _timer.Stop();
}

public partial class FailsafeChannel : ObservableObject {
  public FailsafeChannel(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private double _in;

  [ObservableProperty]
  private double _out;

  [ObservableProperty]
  private IBrush _inBrush = Brushes.Gray;

  [ObservableProperty]
  private IBrush _outBrush = Brushes.Gray;
}
