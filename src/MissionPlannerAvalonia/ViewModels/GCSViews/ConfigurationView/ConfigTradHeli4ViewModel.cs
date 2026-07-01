using System.Collections.ObjectModel;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigTradHeli4ViewModel : ParamPageBase {
  public ObservableCollection<HeliServoRow> Servos { get; } = new();
  public ObservableCollection<ParamField> Swashplate { get; } = new();
  public ObservableCollection<ParamField> Throttle { get; } = new();
  public ObservableCollection<ParamField> Governor { get; } = new();
  public ObservableCollection<ParamField> Misc { get; } = new();

  public ConfigTradHeli4ViewModel() {
    Title = "Heli Setup";
    Intro = "Traditional helicopter setup (ArduPilot 4.0+). Remove blades before testing servos.";
    Build();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Servos.Clear();
    Swashplate.Clear();
    Throttle.Clear();
    Governor.Clear();
    Misc.Clear();
    Build();
  }

  private void Build() {
    for (var i = 1; i <= 8; i++) {
      var servo = "SERVO" + i;
      Servos.Add(new HeliServoRow {
        Index = i,
        Reversed = F(servo + "_REVERSED", "bool"),
        Function = F(servo + "_FUNCTION", "combo"),
        Min = F(servo + "_MIN"),
        Trim = F(servo + "_TRIM"),
        Max = F(servo + "_MAX"),
      });
    }

    foreach (var p in new[] {
        "H_SV_MAN", "H_SW_TYPE", "H_SW_COL_DIR", "H_SW_LIN_SVO", "H_FLYBAR_MODE",
        "H_CYC_MAX", "H_COL_MAX", "H_COL_MID", "H_COL_MIN", "H_COL_ANG_MIN",
        "H_COL_ANG_MAX", "H_COL_ZERO_THRST", "H_COL_LAND_MIN" }) {
      Swashplate.Add(F(p));
    }

    foreach (var p in new[] {
        "H_RSC_MODE", "H_RSC_CRITICAL", "H_RSC_RAMP_TIME", "H_RSC_RUNUP_TIME",
        "H_RSC_CLDWN_TIME", "H_RSC_SETPOINT", "H_RSC_IDLE", "H_RSC_THRCRV_0",
        "H_RSC_THRCRV_25", "H_RSC_THRCRV_50", "H_RSC_THRCRV_75", "H_RSC_THRCRV_100" }) {
      Throttle.Add(F(p));
    }

    foreach (var p in new[] {
        "H_RSC_GOV_COMP", "H_RSC_GOV_SETPNT", "H_RSC_GOV_DISGAG", "H_RSC_GOV_DROOP",
        "H_RSC_GOV_FF", "H_RSC_GOV_TCGAIN", "H_RSC_GOV_RANGE", "H_RSC_GOV_RPM",
        "H_RSC_GOV_TORQUE" }) {
      Governor.Add(F(p));
    }

    foreach (var p in new[] {
        "IM_STB_COL_1", "IM_STB_COL_2", "IM_STB_COL_3", "IM_STB_COL_4",
        "H_TAIL_TYPE", "H_TAIL_SPEED", "H_GYR_GAIN", "H_GYR_GAIN_ACRO", "H_COLYAW" }) {
      Misc.Add(F(p));
    }
  }
}

public class HeliServoRow {
  public int Index { get; set; }
  public ParamField Reversed { get; set; } = null!;
  public ParamField Function { get; set; } = null!;
  public ParamField Min { get; set; } = null!;
  public ParamField Trim { get; set; } = null!;
  public ParamField Max { get; set; } = null!;
}
