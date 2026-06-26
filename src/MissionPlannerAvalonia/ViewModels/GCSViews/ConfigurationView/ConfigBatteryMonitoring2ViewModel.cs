using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public abstract partial class BatteryMonitorPageBase : ViewModelBase, IDisposable {
  private const string AlertEnabledKey = "speechbatteryenabled";
  private const string SpeechEnabledKey = "speechenable";

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
  private double _measuredCurrent;

  [ObservableProperty]
  private string _calibrationStatus = "";

  [ObservableProperty]
  private bool _alertOnLowBattery;

  public bool IsConnected => comPort.BaseStream?.IsOpen == true;

  protected BatteryMonitorPageBase(string prefix) {
    Prefix = prefix;
    Monitor = new ParamField($"{prefix}_MONITOR", "combo");
    Capacity = new ParamField($"{prefix}_CAPACITY");
    VoltPin = new ParamField($"{prefix}_VOLT_PIN", "combo");
    CurrPin = new ParamField($"{prefix}_CURR_PIN", "combo");
    VoltMult = new ParamField($"{prefix}_VOLT_MULT");
    AmpPerVolt = new ParamField(ResolveAmpPerVoltName(prefix));
    AmpOffset = new ParamField($"{prefix}_AMP_OFFSET");

    _alertOnLowBattery = Settings.Instance.GetBoolean(AlertEnabledKey)
        && Settings.Instance.GetBoolean(SpeechEnabledKey);

    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
    _timer.Tick += (_, _) => UpdateLive();
    _timer.Start();
    UpdateLive();
  }

  // Modern ArduPilot uses {prefix}_AMP_PERVLT; legacy firmware exposed {prefix}_AMP_PERVOL(T).
  // Prefer the modern name, but fall back to whichever legacy name the vehicle actually has.
  private static string ResolveAmpPerVoltName(string prefix) {
    var param = AppState.comPort.MAV.param;
    string[] candidates = {
      $"{prefix}_AMP_PERVLT", $"{prefix}_AMP_PERVOL", $"{prefix}_AMP_PERVOLT",
    };
    foreach (var name in candidates) {
      if (param.ContainsKey(name)) {
        return name;
      }
    }
    return $"{prefix}_AMP_PERVLT";
  }

  protected abstract double LiveVoltageValue();
  protected abstract double LiveCurrentValue();

  private void UpdateLive() {
    LiveVoltage = LiveVoltageValue().ToString("0.00");
    LiveCurrent = LiveCurrentValue().ToString("0.00");
  }

  // Row 1-3: new_mult = old_mult * measured / live (upstream TXT_measuredvoltage_Validated).
  [RelayCommand]
  private void ApplyVoltageCalibration() {
    var voltage = LiveVoltageValue();
    if (voltage == 0) {
      CalibrationStatus = "No live voltage — cannot calibrate.";
      return;
    }

    var newMult = MeasuredVoltage * VoltMult.Value / voltage;
    VoltMult.Value = newMult;
    CalibrationStatus = $"{VoltMult.Name} = {newMult:0.000000}";
  }

  // Row 4-6: new_pervlt = old_pervlt * measured / live (upstream txt_meascurrent_Validated).
  [RelayCommand]
  private void ApplyCurrentCalibration() {
    var current = LiveCurrentValue();
    if (current == 0) {
      CalibrationStatus = "No live current — cannot calibrate.";
      return;
    }

    var newPerVolt = MeasuredCurrent * AmpPerVolt.Value / current;
    AmpPerVolt.Value = newPerVolt;
    CalibrationStatus = $"{AmpPerVolt.Name} = {newPerVolt:0.000000}";
  }

  // Backed by app Settings (CHK_speechbattery upstream), NOT a vehicle param.
  partial void OnAlertOnLowBatteryChanged(bool value) {
    Settings.Instance[AlertEnabledKey] = value.ToString();
    Settings.Instance[SpeechEnabledKey] = true.ToString();
    if (value) {
      Settings.Instance["speechbattery"] ??= "WARNING, Battery at {batv} Volt, {batp} percent";
      Settings.Instance["speechbatteryvolt"] ??= "9.6";
      Settings.Instance["speechbatterypercent"] ??= "20";
    }
  }

  public void Dispose() => _timer.Stop();
}

public partial class ConfigBatteryMonitoring2ViewModel : BatteryMonitorPageBase {
  public ConfigBatteryMonitoring2ViewModel() : base("BATT2") { }

  protected override double LiveVoltageValue() => comPort.MAV.cs.battery_voltage2;

  protected override double LiveCurrentValue() => comPort.MAV.cs.current2;
}
