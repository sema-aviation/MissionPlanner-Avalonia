using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAntennaTrackerViewModel : ParamPageBase, IDisposable {
  private readonly DispatcherTimer _timer;

  public ConfigAntennaTrackerViewModel() {
    Title = "Antenna Tracker";
    Intro = "ArduTracker servo, range and PID setup. Test the yaw and pitch servos with live output.";

    F("AHRS_ORIENTATION", "combo");
    F("SERVO_YAW_TYPE", "combo");
    F("SERVO_PITCH_TYPE", "combo");
    F("ALT_SOURCE", "combo");

    F("RC1_MIN");
    F("RC1_MAX");
    F("RC1_TRIM");
    F("RC1_REV", "combo");

    F("RC2_MIN");
    F("RC2_MAX");
    F("RC2_TRIM");
    F("RC2_REV", "combo");

    F("YAW_RANGE");
    F("PITCH_MIN");
    F("PITCH_MAX");

    F("YAW2SRV_P");
    F("YAW2SRV_I");
    F("YAW2SRV_D");
    F("YAW2SRV_IMAX");
    F("YAW_SLEW_TIME");

    F("PITCH2SRV_P");
    F("PITCH2SRV_I");
    F("PITCH2SRV_D");
    F("PITCH2SRV_IMAX");
    F("PITCH_SLEW_TIME");

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => {
      YawPwm = comPort.MAV.cs.ch1out.ToString("0");
      PitchPwm = comPort.MAV.cs.ch2out.ToString("0");
    };
  }

  [ObservableProperty]
  private string _yawPwm = "0";

  [ObservableProperty]
  private string _pitchPwm = "0";

  [ObservableProperty]
  private double _yawTest = 50;

  [ObservableProperty]
  private double _pitchTest = 50;

  [ObservableProperty]
  private string _status = "";

  public void Activate() {
    if (IsConnected) {
      _timer.Start();
    }
  }

  public void Deactivate() => _timer.Stop();

  [RelayCommand]
  private Task TestYaw() => SendServo(1, "RC1_MIN", "RC1_MAX", "RC1_REV", YawTest);

  [RelayCommand]
  private Task TestPitch() => SendServo(2, "RC2_MIN", "RC2_MAX", "RC2_REV", PitchTest);

  private async Task SendServo(int channel, string minParam, string maxParam, string revParam,
      double percent) {
    if (!IsConnected) {
      Status = "Connect to a vehicle first.";
      return;
    }

    double min = GetParam(minParam, 1000);
    double max = GetParam(maxParam, 2000);
    bool reversed = GetParam(revParam, 1) < 0;

    double output = reversed
        ? Map(percent, 100, 0, min, max)
        : Map(percent, 0, 100, min, max);

    try {
      bool ok = await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.DO_SET_SERVO,
          channel, (float)output, 0, 0, 0, 0, 0));
      Status = ok ? $"Ch{channel} → {output:0} us" : "No response from the autopilot.";
    } catch {
      Status = "No response from the autopilot.";
    }
  }

  private double GetParam(string name, double fallback) =>
      comPort.MAV.param.ContainsKey(name) ? comPort.MAV.param[name].Value : fallback;

  private static double Map(double x, double inMin, double inMax, double outMin, double outMax) =>
      (x - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;

  public void Dispose() => _timer.Stop();
}
