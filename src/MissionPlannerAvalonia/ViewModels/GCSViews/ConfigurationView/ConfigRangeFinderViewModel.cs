using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigRangeFinderViewModel : ParamPageBase, IDisposable {
  private readonly DispatcherTimer _timer;

  [ObservableProperty]
  private ParamField _type = null!;

  [ObservableProperty]
  private ParamField _min = null!;

  [ObservableProperty]
  private ParamField _max = null!;

  [ObservableProperty]
  private ParamField _pin = null!;

  [ObservableProperty]
  private ParamField _scaling = null!;

  [ObservableProperty]
  private ParamField _function = null!;

  [ObservableProperty]
  private ParamField _offset = null!;

  [ObservableProperty]
  private ParamField _rmetric = null!;

  [ObservableProperty]
  private string _distance = "0.0";

  [ObservableProperty]
  private string _voltage = "0.0";

  [ObservableProperty]
  private string _presetStatus = "";

  public ConfigRangeFinderViewModel() {
    Title = "RangeFinder";
    Setup();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
  }

  protected override void OnRefreshed() {
    Setup();
  }

  private void Setup() {
    Type = new ParamField("RNGFND1_TYPE", "combo");
    Min = new ParamField("RNGFND1_MIN_CM");
    Max = new ParamField("RNGFND1_MAX_CM");
    Pin = new ParamField("RNGFND1_PIN", "combo");
    Scaling = new ParamField("RNGFND1_SCALING");
    Function = new ParamField("RNGFND1_FUNCTION", "combo");
    Offset = new ParamField("RNGFND1_OFFSET");
    Rmetric = new ParamField("RNGFND1_RMETRIC", "combo");
  }

  private void Tick() {
    var cs = comPort.MAV.cs;
    Distance = cs.sonarrange.ToString("0.0");
    Voltage = cs.sonarvoltage.ToString("0.0");
  }

  [RelayCommand]
  [Obsolete]
  private void TeraRangerPreset() {
    if (comPort.BaseStream?.IsOpen != true) {
      PresetStatus = "offline";
      return;
    }
    try {
      // TeraRangerOne-I2C: set min and max to 20cm - 1m
      comPort.setParam("RNGFND1_MAX_CM", 100);
      comPort.setParam("RNGFND1_MIN_CM", 20);
      Min.Reload();
      Max.Reload();
      PresetStatus = "✓";
    } catch (Exception ex) {
      PresetStatus = ex.Message;
    }
  }

  public void Dispose() {
    _timer.Stop();
  }
}
