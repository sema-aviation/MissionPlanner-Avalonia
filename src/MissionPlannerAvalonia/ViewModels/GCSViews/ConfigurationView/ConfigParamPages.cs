namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;


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

