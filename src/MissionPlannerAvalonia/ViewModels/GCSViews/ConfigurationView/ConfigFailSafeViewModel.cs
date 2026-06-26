using System;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.ArduPilot;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Port of upstream GCSViews/ConfigurationView/ConfigFailSafe.cs(.Designer.cs).
// Upstream groups the failsafe params into three group boxes ("Radio", "GCS",
// "Battery") and wires each MavlinkComboBox/CheckBox/NumericUpDown to the param
// that exists for the connected vehicle (FS_THR_ENABLE/FS_GCS_ENABLE/BATT_* for
// ArduCopter & Rover, THR_FAILSAFE/FS_*_ACTN/FS_BATT_* for ArduPlane). Here the
// vehicle is selected from comPort.MAV.cs.firmware and only the matching field set
// is built into each group. The 16 RC in/out PWM bars are mirrored on the right.
public partial class ConfigFailSafeViewModel : ParamPageBase, IDisposable {
  private static readonly IBrush NormalBrush = new SolidColorBrush(Color.Parse("#94C11F"));
  private static readonly IBrush ThrottleLowBrush = Brushes.Red;

  private readonly DispatcherTimer _timer;

  public string Vehicle { get; }
  public bool IsCopter { get; }
  public bool IsPlane { get; }
  public bool IsRover { get; }
  public string WikiUrl { get; }

  // Upstream group boxes 2/3/4. Each field is also added to the base Fields
  // collection (via FG) so ParamPageBase.Refresh reloads them.
  public ObservableCollection<ParamField> RadioFields { get; } = new();
  public ObservableCollection<ParamField> GcsFields { get; } = new();
  public ObservableCollection<ParamField> BatteryFields { get; } = new();

  public FailsafeChannel[] Channels { get; }

  // Live status trio (upstream lbl_currentmode / lbl_armed / lbl_gpslock).
  [ObservableProperty]
  private string _currentMode = "—";

  [ObservableProperty]
  private string _armedText = "Disarmed";

  [ObservableProperty]
  private string _gpsText = "GPS: No GPS";

  // Mode label turns red when the throttle channel is below the failsafe threshold.
  [ObservableProperty]
  private IBrush _modeBrush = NormalBrush;

  [RelayCommand]
  private void OpenWiki() => Dialogs.OpenUrl(WikiUrl);

  public ConfigFailSafeViewModel() {
    Title = "Failsafe";
    Intro = "Throttle, battery and GCS failsafe behaviour. Ensure props are removed before testing.";

    var fw = comPort.MAV.cs.firmware;
    IsCopter = fw == Firmwares.ArduCopter2;
    IsPlane = fw == Firmwares.ArduPlane;
    IsRover = fw == Firmwares.ArduRover;
    Vehicle = fw.ToString();
    WikiUrl = IsCopter
        ? "https://ardupilot.org/copter/docs/failsafe-landing-page.html"
        : "https://ardupilot.org/plane/docs/advanced-failsafe-configuration.html";

    if (IsPlane) {
      // groupBox2 "Radio"
      FG(RadioFields, "THR_FAILSAFE", "combo");
      FG(RadioFields, "THR_FS_VALUE");
      FG(RadioFields, "THR_FS_ACTION", "combo");
      // groupBox3 "GCS"
      FG(GcsFields, "FS_GCS_ENABL", "combo");
      FG(GcsFields, "FS_SHORT_ACTN", "combo");
      FG(GcsFields, "FS_LONG_ACTN", "combo");
      // groupBox4 "Battery"
      FG(BatteryFields, "FS_BATT_ENABLE", "combo");
      FG(BatteryFields, "FS_BATT_VOLTAGE");
      FG(BatteryFields, "FS_BATT_MAH");
    } else {
      // ArduCopter / Rover / other: copter-style failsafe params.
      // groupBox2 "Radio"
      FG(RadioFields, "FS_THR_ENABLE", "combo");
      FG(RadioFields, "FS_THR_VALUE");
      // groupBox3 "GCS"
      FG(GcsFields, "FS_GCS_ENABLE", "combo");
      // groupBox4 "Battery" — upstream picks the first param that exists, in priority order.
      FGfirst(BatteryFields, "combo", "BATT_FS_LOW_ACT", "FS_BATT_ENABLE");
      FGfirst(BatteryFields, null, "LOW_VOLT", "FS_BATT_VOLTAGE", "BATT_LOW_VOLT");
      FGfirst(BatteryFields, null, "FS_BATT_MAH", "BATT_LOW_MAH");
      // Low Timer is Copter-only; upstream omits the control when the param is absent.
      if (comPort.MAV.param.ContainsKey("BATT_LOW_TIMER")) {
        FG(BatteryFields, "BATT_LOW_TIMER");
      }
    }

    Channels = new FailsafeChannel[16];
    for (int i = 0; i < 16; i++) {
      Channels[i] = new FailsafeChannel(i + 1) { InBrush = NormalBrush, OutBrush = NormalBrush };
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private ParamField FG(ObservableCollection<ParamField> group, string name, string? kind = null) {
    var f = F(name, kind);
    group.Add(f);
    return f;
  }

  // Adds the first of the candidate params that the vehicle actually exposes (mirrors upstream's
  // BATT_FS_LOW_ACT→FS_BATT_ENABLE / LOW_VOLT→FS_BATT_VOLTAGE→BATT_LOW_VOLT fallbacks).
  private void FGfirst(ObservableCollection<ParamField> group, string? kind, params string[] names) {
    foreach (var n in names) {
      if (comPort.MAV.param.ContainsKey(n)) {
        FG(group, n, kind);
        return;
      }
    }
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
    bool thrLow = fsThr > 0 && ins[2] > 0 && ins[2] < fsThr;
    Channels[2].InBrush = thrLow ? ThrottleLowBrush : NormalBrush;

    CurrentMode = cs.mode ?? "—";
    ModeBrush = thrLow ? ThrottleLowBrush : NormalBrush;
    ArmedText = cs.armed ? "Armed" : "Disarmed";
    GpsText = (int)cs.gpsstatus switch {
      0 => "GPS: No GPS",
      1 => "GPS: No Fix",
      2 => "GPS: 2D Fix",
      3 => "GPS: 3D Fix",
      _ => "GPS: 3D Fix (RTK)",
    };
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
