namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;


public class ConfigExtendedTuningViewModel : ParamPageBase {
  public ConfigExtendedTuningViewModel() {
    Title = "Extended Tuning";
    Intro = "Copter PID gains and tuning. Modern ArduPilot param names; fields show n/a if not present on this firmware.";

    F("CH6_OPT", "combo");
    F("CH7_OPT", "combo");
    F("CH8_OPT", "combo");
    F("CH9_OPT", "combo");
    F("CH10_OPT", "combo");

    F("TUNE", "combo");
    F("TUNE_MIN");
    F("TUNE_MAX");

    F("PSC_POSXY_P");
    F("PSC_VELXY_D");
    F("PSC_VELXY_I");
    F("PSC_VELXY_IMAX");
    F("PSC_VELXY_P");

    F("ATC_RAT_PIT_P");
    F("ATC_RAT_PIT_I");
    F("ATC_RAT_PIT_D");
    F("ATC_RAT_PIT_IMAX");
    F("ATC_RAT_PIT_FLTE");
    F("ATC_RAT_PIT_FLTD");
    F("ATC_RAT_PIT_FLTT");

    F("ATC_RAT_RLL_P");
    F("ATC_RAT_RLL_I");
    F("ATC_RAT_RLL_D");
    F("ATC_RAT_RLL_IMAX");
    F("ATC_RAT_RLL_FLTE");
    F("ATC_RAT_RLL_FLTD");
    F("ATC_RAT_RLL_FLTT");

    F("ATC_RAT_YAW_P");
    F("ATC_RAT_YAW_I");
    F("ATC_RAT_YAW_D");
    F("ATC_RAT_YAW_IMAX");
    F("ATC_RAT_YAW_FLTE");
    F("ATC_RAT_YAW_FLTD");
    F("ATC_RAT_YAW_FLTT");

    F("ATC_ANG_PIT_P");
    F("ATC_ANG_RLL_P");
    F("ATC_ANG_YAW_P");

    F("PSC_ACCZ_P");
    F("PSC_ACCZ_I");
    F("PSC_ACCZ_D");
    F("PSC_ACCZ_IMAX");

    F("PSC_POSZ_P");
    F("PSC_VELZ_P");

    F("LOIT_SPEED");
    F("WPNAV_RADIUS");
    F("WPNAV_SPEED");
    F("WPNAV_SPEED_DN");
    F("WPNAV_SPEED_UP");

    F("INS_GYRO_FILTER");
    F("INS_ACCEL_FILTER");

    F("INS_LOG_BAT_MASK");
    F("INS_LOG_BAT_OPT");

    F("INS_NOTCH_ENABLE", "combo");
    F("INS_NOTCH_FREQ");
    F("INS_NOTCH_BW");
    F("INS_NOTCH_ATT");

    F("INS_HNTCH_ENABLE", "combo");
    F("INS_HNTCH_MODE", "combo");
    F("INS_HNTCH_REF");
    F("INS_HNTCH_FREQ");
    F("INS_HNTCH_ATT");
    F("INS_HNTCH_BW");
    F("INS_HNTCH_OPTS");
    F("INS_HNTCH_HMNCS");

    F("ATC_ACCEL_R_MAX");
    F("ATC_ACCEL_P_MAX");
    F("ATC_ACCEL_Y_MAX");
    F("ATC_INPUT_TC");
  }
}
