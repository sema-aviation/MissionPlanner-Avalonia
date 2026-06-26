using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAntennaTrackerParamViewModel : ParamPageBase, IDisposable {
  private const double SliderMin = 0;
  private const double SliderMax = 100;

  private readonly DispatcherTimer _timer;

  // combos
  [ObservableProperty]
  private ParamField _orientation = null!;

  [ObservableProperty]
  private ParamField _yawType = null!;

  [ObservableProperty]
  private ParamField _pitchType = null!;

  [ObservableProperty]
  private ParamField _altSource = null!;

  // yaw rc
  [ObservableProperty]
  private ParamField _yawMin = null!;

  [ObservableProperty]
  private ParamField _yawMax = null!;

  [ObservableProperty]
  private ParamField _yawTrim = null!;

  [ObservableProperty]
  private ParamField _yawRev = null!;

  // pitch rc
  [ObservableProperty]
  private ParamField _pitchRcMin = null!;

  [ObservableProperty]
  private ParamField _pitchRcMax = null!;

  [ObservableProperty]
  private ParamField _pitchTrim = null!;

  [ObservableProperty]
  private ParamField _pitchRev = null!;

  // ranges
  [ObservableProperty]
  private ParamField _yawRange = null!;

  [ObservableProperty]
  private ParamField _pitchMin = null!;

  [ObservableProperty]
  private ParamField _pitchMax = null!;

  // yaw pid
  [ObservableProperty]
  private ParamField _yawP = null!;

  [ObservableProperty]
  private ParamField _yawI = null!;

  [ObservableProperty]
  private ParamField _yawD = null!;

  [ObservableProperty]
  private ParamField _yawImax = null!;

  [ObservableProperty]
  private ParamField _yawSlewTime = null!;

  // pitch pid
  [ObservableProperty]
  private ParamField _pitchP = null!;

  [ObservableProperty]
  private ParamField _pitchI = null!;

  [ObservableProperty]
  private ParamField _pitchD = null!;

  [ObservableProperty]
  private ParamField _pitchImax = null!;

  [ObservableProperty]
  private ParamField _pitchSlewTime = null!;

  [ObservableProperty]
  private double _yawTest;

  [ObservableProperty]
  private double _pitchTest;

  [ObservableProperty]
  private string _yawPwm = "0";

  [ObservableProperty]
  private string _pitchPwm = "0";

  [ObservableProperty]
  private bool _isArduTracker;

  [ObservableProperty]
  private string _writeStatus = "";

  public ConfigAntennaTrackerParamViewModel() {
    Title = "Antenna Tracker";
    Setup();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
  }

  protected override void OnRefreshed() {
    Setup();
  }

  private void Setup() {
    IsArduTracker = comPort.MAV.cs.firmware == Firmwares.ArduTracker;

    Orientation = new ParamField("AHRS_ORIENTATION", "combo");
    YawType = new ParamField("SERVO_YAW_TYPE", "combo");
    PitchType = new ParamField("SERVO_PITCH_TYPE", "combo");
    AltSource = new ParamField("ALT_SOURCE", "combo");

    YawMin = new ParamField("RC1_MIN");
    YawMax = new ParamField("RC1_MAX");
    YawTrim = new ParamField("RC1_TRIM");
    YawRev = new ParamField("RC1_REV", "bool");

    PitchRcMin = new ParamField("RC2_MIN");
    PitchRcMax = new ParamField("RC2_MAX");
    PitchTrim = new ParamField("RC2_TRIM");
    PitchRev = new ParamField("RC2_REV", "bool");

    YawRange = new ParamField("YAW_RANGE");
    PitchMin = new ParamField("PITCH_MIN");
    PitchMax = new ParamField("PITCH_MAX");

    YawP = new ParamField("YAW2SRV_P");
    YawI = new ParamField("YAW2SRV_I");
    YawD = new ParamField("YAW2SRV_D");
    YawImax = new ParamField("YAW2SRV_IMAX");
    YawSlewTime = new ParamField("YAW_SLEW_TIME");

    PitchP = new ParamField("PITCH2SRV_P");
    PitchI = new ParamField("PITCH2SRV_I");
    PitchD = new ParamField("PITCH2SRV_D");
    PitchImax = new ParamField("PITCH2SRV_IMAX");
    PitchSlewTime = new ParamField("PITCH_SLEW_TIME");
  }

  private void Tick() {
    var cs = comPort.MAV.cs;
    YawPwm = ((int)cs.ch1out).ToString();
    PitchPwm = ((int)cs.ch2out).ToString();
  }

  private static double Map(double x, double inMin, double inMax, double outMin, double outMax) {
    return (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
  }

  [RelayCommand]
  private async Task TestYaw() {
    double output = YawRev.Checked
        ? Map(YawTest, SliderMin, SliderMax, YawMin.Value, YawMax.Value)
        : Map(YawTest, SliderMax, SliderMin, YawMin.Value, YawMax.Value);
    await SetServo(1, output);
  }

  [RelayCommand]
  private async Task TestPitch() {
    double output = PitchRev.Checked
        ? Map(PitchTest, SliderMax, SliderMin, PitchRcMin.Value, PitchRcMax.Value)
        : Map(PitchTest, SliderMin, SliderMax, PitchRcMin.Value, PitchRcMax.Value);
    await SetServo(2, output);
  }

  private async Task SetServo(int servo, double output) {
    if (comPort.BaseStream?.IsOpen != true) {
      WriteStatus = "offline";
      return;
    }
    try {
      await Task.Run(() => comPort.doCommand(comPort.MAV.sysid, comPort.MAV.compid,
          MAVLink.MAV_CMD.DO_SET_SERVO, servo, (float)output, 0, 0, 0, 0, 0));
    } catch (Exception ex) {
      WriteStatus = ex.Message;
    }
  }

  [RelayCommand]
  private async Task WritePids() {
    if (comPort.BaseStream?.IsOpen != true) {
      WriteStatus = "offline";
      return;
    }
    var fields = new[] {
      Orientation, YawType, PitchType, AltSource,
      YawMin, YawMax, YawTrim, YawRev,
      PitchRcMin, PitchRcMax, PitchTrim, PitchRev,
      YawRange, PitchMin, PitchMax,
      YawP, YawI, YawD, YawImax, YawSlewTime,
      PitchP, PitchI, PitchD, PitchImax, PitchSlewTime,
    };
    try {
      foreach (var f in fields) {
        if (!f.Exists) {
          continue;
        }
        var name = f.Name;
        var value = f.Value;
        await Task.Run(() => comPort.setParam(name, value, true));
      }
      WriteStatus = "✓";
    } catch (Exception ex) {
      WriteStatus = ex.Message;
    }
  }

  public void Dispose() {
    _timer.Stop();
  }
}
