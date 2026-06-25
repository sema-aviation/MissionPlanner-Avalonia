namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigBasicTuningViewModel : ParamPageBase {
  public ConfigBasicTuningViewModel() {
    Title = "Basic Tuning";
    Intro = "Simple sliders for the most common copter tuning parameters.";
    F("RC_FEEL_RP");
    F("RATE_RLL_P");
    F("RATE_RLL_I");
    F("RATE_PIT_P");
    F("RATE_PIT_I");
    F("ATC_RAT_RLL_P");
    F("ATC_RAT_RLL_I");
    F("ATC_RAT_PIT_P");
    F("ATC_RAT_PIT_I");
    F("THR_MID");
    F("THR_ACCEL_P");
    F("THR_ACCEL_I");
    F("ACCEL_Z_P");
    F("ACCEL_Z_I");
  }
}
