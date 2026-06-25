using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public abstract partial class BatteryMonitorPageBase : ViewModelBase, IDisposable {
  protected readonly MAVLinkInterface comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;

  protected string Prefix { get; }

  public ParamField Monitor { get; }
  public ParamField Capacity { get; }
  public ParamField VoltPin { get; }
  public ParamField CurrPin { get; }
  public ParamField VoltMult { get; }
  public ParamField AmpPerVolt { get; }
  public ParamField AmpOffset { get; }

  [ObservableProperty]
  private string _liveVoltage = "0";

  [ObservableProperty]
  private string _liveCurrent = "0";

  [ObservableProperty]
  private double _measuredVoltage;

  [ObservableProperty]
  private string _calibrationStatus = "";

  public bool IsConnected => comPort.BaseStream?.IsOpen == true;

  protected BatteryMonitorPageBase(string prefix) {
    Prefix = prefix;
    Monitor = new ParamField($"{prefix}_MONITOR", "combo");
    Capacity = new ParamField($"{prefix}_CAPACITY");
    VoltPin = new ParamField($"{prefix}_VOLT_PIN", "combo");
    CurrPin = new ParamField($"{prefix}_CURR_PIN", "combo");
    VoltMult = new ParamField($"{prefix}_VOLT_MULT");
    AmpPerVolt = new ParamField($"{prefix}_AMP_PERVLT");
    AmpOffset = new ParamField($"{prefix}_AMP_OFFSET");

    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _timer.Tick += (_, _) => UpdateLive();
    _timer.Start();
    UpdateLive();
  }

  protected abstract double LiveVoltageValue();
  protected abstract double LiveCurrentValue();

  private void UpdateLive() {
    LiveVoltage = LiveVoltageValue().ToString("0.00");
    LiveCurrent = LiveCurrentValue().ToString("0.00");
  }

  [RelayCommand]
  private void ApplyCalibration() {
    var voltage = LiveVoltageValue();
    if (voltage == 0) {
      CalibrationStatus = "No live voltage — cannot calibrate.";
      return;
    }

    var newMult = MeasuredVoltage * VoltMult.Value / voltage;
    VoltMult.Value = newMult;
    CalibrationStatus = $"{Prefix}_VOLT_MULT = {newMult:0.000000}";
  }

  public void Dispose() => _timer.Stop();
}

public partial class ConfigBatteryMonitoring2ViewModel : BatteryMonitorPageBase {
  public ConfigBatteryMonitoring2ViewModel() : base("BATT2") { }

  protected override double LiveVoltageValue() => comPort.MAV.cs.battery_voltage2;

  protected override double LiveCurrentValue() => comPort.MAV.cs.current2;
}
