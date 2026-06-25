namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;


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

public class ConfigFrameClassTypeViewModel : ParamPageBase {
  public ConfigFrameClassTypeViewModel() {
    Title = "Frame Type";
    F("FRAME_CLASS", "combo");
    F("FRAME_TYPE", "combo");
    F("Q_FRAME_CLASS", "combo");
    F("Q_FRAME_TYPE", "combo");
  }
}

