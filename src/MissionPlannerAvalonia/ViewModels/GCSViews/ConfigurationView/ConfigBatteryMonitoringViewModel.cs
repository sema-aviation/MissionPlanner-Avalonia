using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigBatteryMonitoringViewModel : BatteryMonitorPageBase {
  public string[] SensorTypes { get; } =
  {
        "0: Other",
        "1: AttoPilot 45A",
        "2: AttoPilot 90A",
        "3: AttoPilot 180A",
        "4: 3DR Power Module",
        "5: 3DR 4 in 1 ESC",
        "6: 3DR HV Power Module APM",
        "7: Cube HV Power Module",
        "8: CUAV HV PM",
        "9: Holybro Power Module",
    };

  public string[] HwVersions { get; } =
  {
        "0: CUAV V5/Pixhawk4 or APM1",
        "1: APM2 - 2.5 non 3DR",
        "2: APM2.5+/ZealotF427 - 3DR Power Module",
        "3: PX4",
        "4: The Cube or Pixhawk",
        "5: VR Brain 4.5 - 5",
        "6: VR Micro Brain 5",
        "7: VR Brain 4",
        "8: Cube Orange",
        "9: Durandal/ZealotH743",
        "10: Pixhawk 6C/Pix32 v6",
    };

  [ObservableProperty]
  private int _selectedSensorIndex = -1;

  [ObservableProperty]
  private int _selectedHwIndex = -1;

  public ConfigBatteryMonitoringViewModel() : base("BATT") { }

  protected override double LiveVoltageValue() => comPort.MAV.cs.battery_voltage;

  protected override double LiveCurrentValue() => comPort.MAV.cs.current;

  partial void OnSelectedSensorIndexChanged(int value) {
    if (value < 0) {
      return;
    }

    ApplySensorType(value);
  }

  partial void OnSelectedHwIndexChanged(int value) {
    if (value < 0) {
      return;
    }

    ApplyHwVersion(value);
  }

  private void ApplySensorType(int selection) {
    switch (selection) {
      case 1: {
          var maxvolt = 13.6f;
          var maxamps = 44.7f;
          var topvolt = maxvolt * 242.3f / 1000;
          var topamps = maxamps * 73.20f / 1000;
          VoltMult.Value = maxvolt / topvolt;
          AmpPerVolt.Value = maxamps / topamps;
          break;
        }
      case 2: {
          var maxvolt = 50f;
          var maxamps = 89.4f;
          var topvolt = maxvolt * 63.69f / 1000;
          var topamps = maxamps * 36.60f / 1000;
          VoltMult.Value = maxvolt / topvolt;
          AmpPerVolt.Value = maxamps / topamps;
          break;
        }
      case 3: {
          var maxvolt = 50f;
          var maxamps = 178.8f;
          var topvolt = maxvolt * 63.69f / 1000;
          var topamps = maxamps * 18.30f / 1000;
          VoltMult.Value = maxvolt / topvolt;
          AmpPerVolt.Value = maxamps / topamps;
          break;
        }
      case 4: {
          var maxvolt = 50f;
          var maxamps = 90f;
          var topvolt = maxvolt * 99f / 1000;
          var topamps = maxamps * 55.55f / 1000;
          VoltMult.Value = maxvolt / topvolt;
          AmpPerVolt.Value = maxamps / topamps;
          break;
        }
      case 5:
        VoltMult.Value = 12.02;
        AmpPerVolt.Value = 17;
        break;
      case 6:
        VoltMult.Value = 12.02;
        AmpPerVolt.Value = 24;
        break;
      case 7:
        VoltMult.Value = 12.02;
        AmpPerVolt.Value = 39.877;
        break;
      case 8:
        VoltMult.Value = 18;
        AmpPerVolt.Value = 24;
        break;
      case 9:
        VoltMult.Value = 18.182;
        AmpPerVolt.Value = 36.364;
        break;
    }
  }

  private void ApplyHwVersion(int selection) {
    switch (selection) {
      case 0:
        VoltPin.Value = 0;
        CurrPin.Value = 1;
        break;
      case 1:
        VoltPin.Value = 1;
        CurrPin.Value = 2;
        break;
      case 2:
        VoltPin.Value = 13;
        CurrPin.Value = 12;
        break;
      case 3:
        VoltPin.Value = 100;
        CurrPin.Value = 101;
        break;
      case 4:
        VoltPin.Value = 2;
        CurrPin.Value = 3;
        break;
      case 5:
        VoltPin.Value = 10;
        CurrPin.Value = 11;
        break;
      case 6:
        VoltPin.Value = 10;
        CurrPin.Value = -1;
        break;
      case 7:
        VoltPin.Value = 6;
        CurrPin.Value = 7;
        break;
      case 8:
        VoltPin.Value = 14;
        CurrPin.Value = 15;
        break;
      case 9:
        VoltPin.Value = 16;
        CurrPin.Value = 17;
        break;
      case 10:
        VoltPin.Value = 8;
        CurrPin.Value = 4;
        break;
    }
  }
}
