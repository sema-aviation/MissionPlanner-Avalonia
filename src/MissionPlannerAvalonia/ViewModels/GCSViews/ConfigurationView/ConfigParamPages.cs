namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;


public class ConfigFailSafeViewModel : ParamPageBase {
  public ConfigFailSafeViewModel() {
    Title = "Failsafe";
    Intro = "Throttle, battery and GCS failsafe behaviour.";
    F("FS_THR_ENABLE", "combo");
    F("FS_THR_VALUE");
    F("THR_FAILSAFE", "combo");
    F("THR_FS_VALUE");
    F("THR_FS_ACTION", "combo");
    F("FS_GCS_ENABLE", "combo");
    F("FS_SHORT_ACTN", "combo");
    F("FS_LONG_ACTN", "combo");
    F("BATT_FS_LOW_ACT", "combo");
    F("BATT_LOW_VOLT");
    F("BATT_LOW_MAH");
    F("BATT_LOW_TIMER");
    F("FS_BATT_ENABLE", "combo");
    F("FS_BATT_VOLTAGE");
    F("FS_BATT_MAH");
  }
}

public class ConfigAC_FenceViewModel : ParamPageBase {
  public ConfigAC_FenceViewModel() {
    Title = "GeoFence";
    F("FENCE_ENABLE", "combo");
    F("FENCE_TYPE", "combo");
    F("FENCE_ACTION", "combo");
    F("FENCE_ALT_MAX");
    F("FENCE_ALT_MIN");
    F("FENCE_RADIUS");
    F("RTL_ALT");
  }
}

public class ConfigADSBViewModel : ParamPageBase {
  public ConfigADSBViewModel() {
    Title = "ADSB";
    Intro = "ADS-B receiver / avoidance. Populated on connect.";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  private void Setup() {
    FByPrefix("ADSB_");
    FByPrefix("AVD_");
  }
}

public class ConfigGPSOrderViewModel : ParamPageBase {
  public ConfigGPSOrderViewModel() {
    Title = "CAN GPS Order";
    F("GPS_CAN_NODEID1");
    F("GPS_CAN_NODEID2");
    F("GPS1_CAN_OVRIDE");
    F("GPS2_CAN_OVRIDE");
  }
}

public class ConfigRangeFinderViewModel : ParamPageBase {
  public ConfigRangeFinderViewModel() {
    Title = "RangeFinder";
    F("RNGFND1_TYPE", "combo");
    F("RNGFND1_MIN_CM");
    F("RNGFND1_MAX_CM");
    F("RNGFND_TYPE", "combo");
    F("RNGFND_MAX_CM");
  }
}

public class ConfigAirspeedViewModel : ParamPageBase {
  public ConfigAirspeedViewModel() {
    Title = "Airspeed";
    F("ARSPD_TYPE", "combo");
    F("ARSPD_ENABLE", "combo");
    F("ARSPD_USE", "combo");
    F("ARSPD_PIN", "combo");
  }
}

public class ConfigOptFlowViewModel : ParamPageBase {
  public ConfigOptFlowViewModel() {
    Title = "Optical Flow";
    F("FLOW_TYPE", "combo");
    F("FLOW_FXSCALER");
    F("FLOW_FYSCALER");
    F("FLOW_ORIENT_YAW");
    F("FLOW_POS_X");
    F("FLOW_POS_Y");
    F("FLOW_POS_Z");
    F("FLOW_HGT_OVR");
  }
}

public class ConfigHWOSDViewModel : ParamPageBase {
  public ConfigHWOSDViewModel() {
    Title = "Onboard OSD (Stream Rates)";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  private void Setup() {
    FByPrefix("SR0_");
    FByPrefix("SR1_");
    FByPrefix("SR3_");
  }
}

public class ConfigOSDViewModel : ParamPageBase {
  public ConfigOSDViewModel() {
    Title = "Onboard OSD";
    Intro = "OSD panel item enable/positions. Populated on connect.";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  private void Setup() {
    FByPrefix("OSD");
  }
}

public class ConfigParachuteViewModel : ParamPageBase {
  public ConfigParachuteViewModel() {
    Title = "Parachute";
    F("CHUTE_ENABLED", "combo");
    F("CHUTE_TYPE", "combo");
    F("CHUTE_SERVO_ON");
    F("CHUTE_SERVO_OFF");
    F("CHUTE_ALT_MIN");
  }
}

public class ConfigFFTViewModel : ParamPageBase {
  public ConfigFFTViewModel() {
    Title = "FFT Setup";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  private void Setup() {
    FByPrefix("FFT_");
    F("INS_LOG_BAT_MASK");
    F("INS_LOG_BAT_CNT");
    F("LOG_BITMASK");
  }
}

public class ConfigFrameClassTypeViewModel : ParamPageBase {
  public ConfigFrameClassTypeViewModel() {
    Title = "Frame Type";
    F("FRAME_CLASS", "combo");
    F("FRAME_TYPE", "combo");
    F("Q_FRAME_CLASS", "combo");
    F("Q_FRAME_TYPE", "combo");
  }
}

