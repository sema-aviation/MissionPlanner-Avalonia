using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAirspeedViewModel : ParamPageBase, IDisposable {
  private readonly DispatcherTimer _timer;

  [ObservableProperty]
  private ParamField _type = null!;

  [ObservableProperty]
  private ParamField _enable = null!;

  [ObservableProperty]
  private ParamField _use = null!;

  [ObservableProperty]
  private ParamField _pin = null!;

  [ObservableProperty]
  private ParamField _ratio = null!;

  [ObservableProperty]
  private string _calStatus = "";

  [ObservableProperty]
  private string _airspeed = "0.0";

  public ConfigAirspeedViewModel() {
    Title = "Airspeed";
    Setup();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
  }

  protected override void OnRefreshed() {
    Setup();
  }

  private void Setup() {
    Type = new ParamField("ARSPD_TYPE", "combo");
    Enable = new ParamField("ARSPD_ENABLE", "bool");
    Use = new ParamField("ARSPD_USE", "bool");

    var pin = new ParamField("ARSPD_PIN", "combo");
    pin.Options.Clear();
    pin.Options.Add(new ParamOption(0, "APM 2 analog pin 0"));
    pin.Options.Add(new ParamOption(1, "APM 2 analog pin 1"));
    pin.Options.Add(new ParamOption(2, "APM 2 analog pin 2"));
    pin.Options.Add(new ParamOption(3, "APM 2 analog pin 3"));
    pin.Options.Add(new ParamOption(4, "APM 2 analog pin 4"));
    pin.Options.Add(new ParamOption(5, "APM 2 analog pin 5"));
    pin.Options.Add(new ParamOption(6, "APM 2 analog pin 6"));
    pin.Options.Add(new ParamOption(7, "APM 2 analog pin 7"));
    pin.Options.Add(new ParamOption(8, "APM 2 analog pin 8"));
    pin.Options.Add(new ParamOption(9, "APM 2 analog pin 9"));
    pin.Options.Add(new ParamOption(64, "APM 1 AS Port"));
    pin.Options.Add(new ParamOption(11, "PX4 Analog AS Port"));
    pin.Options.Add(new ParamOption(15, "Pixhawk Analog AS Port"));
    pin.Options.Add(new ParamOption(65, "PX4/Pixhawk EagleTree or MEAS I2C AS Sensor"));
    pin.Reload();
    Pin = pin;

    Ratio = new ParamField("ARSPD_RATIO");
  }

  private void Tick() {
    Airspeed = comPort.MAV.cs.airspeed.ToString("0.0");
  }

  [RelayCommand]
  private async Task GroundCalibration() {
    if (comPort.BaseStream?.IsOpen != true) {
      CalStatus = "offline";
      return;
    }

    if (comPort.MAV.cs.airspeed > 7.0 || comPort.MAV.cs.groundspeed > 10.0) {
      CalStatus = "Unable - UAV airborne";
      return;
    }

    try {
      CalStatus = "Calibrating…";
      bool ok = await Task.Run(() => comPort.doCommand(
          (byte)comPort.sysidcurrent,
          (byte)comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
          0, 0, 1, 0, 0, 0, 0));
      CalStatus = ok ? "✓" : "calibration failed";
    } catch (Exception ex) {
      CalStatus = ex.Message;
    }
  }

  public void Dispose() {
    _timer.Stop();
  }
}
