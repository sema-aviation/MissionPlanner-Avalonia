using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

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
    Enable = new ParamField("ARSPD_ENABLE", "combo");
    Use = new ParamField("ARSPD_USE", "combo");

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
  }

  private void Tick() {
    Airspeed = comPort.MAV.cs.airspeed.ToString("0.0");
  }

  public void Dispose() {
    _timer.Stop();
  }
}
