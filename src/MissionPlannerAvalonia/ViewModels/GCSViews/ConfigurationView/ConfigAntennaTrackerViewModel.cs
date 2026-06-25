namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigAntennaTrackerViewModel : ParamPageBase {
  public ConfigAntennaTrackerViewModel() {
    Title = "Antenna Tracker";
    Intro = "ArduTracker servo, range and PID setup.";

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
  }
}
