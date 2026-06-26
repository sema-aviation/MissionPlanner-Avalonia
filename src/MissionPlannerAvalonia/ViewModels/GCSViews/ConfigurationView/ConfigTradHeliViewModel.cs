using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Port of MissionPlanner.GCSViews.ConfigurationView.ConfigTradHeli (legacy 3.x heli page).
// Full parameter set + swashplate type radios + H_SV_MAN manual servo-test buttons.
// ponytail: the live ZedGraph collective curve and the HS3/HS4 live servo-input bars are
// telemetry visualizations only and are not ported here; all writable params are present.
public partial class ConfigTradHeliViewModel : ParamPageBase {
  private bool _suppressSwash;

  [ObservableProperty]
  private bool _swashIsCcpm = true;

  [ObservableProperty]
  private string _servoStatus = "";

  public ConfigTradHeliViewModel() {
    Title = "Heli Setup";
    Intro = "Traditional helicopter swashplate and rotor speed setup. Remove blades before testing servos.";
    Setup();
    ReadSwash();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
    ReadSwash();
  }

  private string Pick(params string[] names) {
    foreach (var n in names) {
      if (comPort.MAV.param.ContainsKey(n)) {
        return n;
      }
    }
    return names[0];
  }

  private void Setup() {
    F("H_PHANG");
    F("ATC_PIRO_COMP", "bool");
    F("H_SV_TEST");
    F("ATC_HOVR_ROL_TRM");
    F("H_CYC_MAX");

    F("H_RSC_CRITICAL");
    F(Pick("H_RSC_MAX", "H_RSC_PWM_MAX"));
    F(Pick("H_RSC_MIN", "H_RSC_PWM_MIN"));
    F(Pick("H_RSC_REV", "H_RSC_PWM_REV"));
    F("H_RSC_POWER_HIGH");
    F("H_RSC_POWER_LOW");
    F("H_RSC_IDLE");

    F(Pick("IM_STAB_COL_1", "IM_STB_COL_1"));
    F(Pick("IM_STAB_COL_2", "IM_STB_COL_2"));
    F(Pick("IM_STAB_COL_3", "IM_STB_COL_3"));
    F(Pick("IM_STAB_COL_4", "IM_STB_COL_4"));
    F("IM_ACRO_COL_EXP");

    F("H_TAIL_TYPE", "combo");
    F("H_TAIL_SPEED");
    F("H_LAND_COL_MIN");
    F("H_COLYAW");
    F("H_RSC_RAMP_TIME");
    F("H_RSC_RUNUP_TIME");
    F("H_RSC_MODE", "combo");
    F("H_RSC_SETPOINT");
    F("H_GYR_GAIN");

    F("H_COL_MIN");
    F("H_COL_MID");
    F("H_COL_MAX");
    F(Pick("HS4_MIN", "SERVO4_MIN"));
    F(Pick("HS4_MAX", "SERVO4_MAX"));

    F("H_SV1_POS");
    F("H_SV2_POS");
    F("H_SV3_POS");

    F(Pick("HS1_REV", "H_SV1_REV", "SERVO1_REVERSED"));
    F(Pick("HS2_REV", "H_SV2_REV", "SERVO2_REVERSED"));
    F(Pick("HS3_REV", "H_SV3_REV", "SERVO3_REVERSED"));
    F(Pick("HS4_REV", "H_SV4_REV", "SERVO4_REVERSED"));
    F("H_FLYBAR_MODE", "combo");

    F(Pick("HS1_TRIM", "H_SV1_TRIM", "SERVO1_TRIM"));
    F(Pick("HS2_TRIM", "H_SV2_TRIM", "SERVO2_TRIM"));
    F(Pick("HS3_TRIM", "H_SV3_TRIM", "SERVO3_TRIM"));
    F(Pick("HS4_TRIM", "H_SV4_TRIM", "SERVO4_TRIM"));
  }

  private void ReadSwash() {
    _suppressSwash = true;
    if (comPort.MAV.param.ContainsKey("H_SWASH_TYPE")) {
      SwashIsCcpm = (int)Math.Round(comPort.MAV.param["H_SWASH_TYPE"].Value) == 0;
    }
    _suppressSwash = false;
  }

  [System.Obsolete]
  partial void OnSwashIsCcpmChanged(bool value) {
    if (_suppressSwash) {
      return;
    }
    WriteSwash(value ? 0 : 1);
  }

  [System.Obsolete]
  private async void WriteSwash(double value) {
    if (comPort.BaseStream?.IsOpen != true) {
      ServoStatus = "offline";
      return;
    }
    try {
      var ok = await Task.Run(() => comPort.setParam("H_SWASH_TYPE", value));
      ServoStatus = ok ? "H_SWASH_TYPE set" : "Set H_SWASH_TYPE Failed";
    } catch {
      ServoStatus = "Set H_SWASH_TYPE Failed";
    }
  }

  // H_SV_MAN: 0=stop/disable, 1=manual, 2=max, 3=center, 4=min, 5=test (mirrors upstream buttons).
  [RelayCommand]
  [System.Obsolete]
  private async Task SetServoMan(string mode) {
    if (!int.TryParse(mode, out var v)) {
      return;
    }
    if (comPort.BaseStream?.IsOpen != true) {
      ServoStatus = "offline";
      return;
    }
    try {
      var ok = await Task.Run(() => comPort.setParam("H_SV_MAN", v));
      ServoStatus = ok ? "H_SV_MAN=" + v : "Set H_SV_MAN Failed";
    } catch {
      ServoStatus = "Set H_SV_MAN Failed";
    }
  }
}
