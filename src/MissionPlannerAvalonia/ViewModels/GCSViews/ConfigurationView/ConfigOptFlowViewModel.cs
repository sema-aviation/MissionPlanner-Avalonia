using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigOptFlowViewModel : ParamPageBase {
  [ObservableProperty]
  private ParamField _enable = null!;

  [ObservableProperty]
  private ParamField _type = null!;

  [ObservableProperty]
  private ParamField _yaw = null!;

  [ObservableProperty]
  private ParamField _fxScaler = null!;

  [ObservableProperty]
  private ParamField _fyScaler = null!;

  [ObservableProperty]
  private ParamField _posX = null!;

  [ObservableProperty]
  private ParamField _posY = null!;

  [ObservableProperty]
  private ParamField _posZ = null!;

  [ObservableProperty]
  private ParamField _heightOverride = null!;

  // FLOW_ENABLE only exists on older firmwares; its presence selects the legacy panel.
  [ObservableProperty]
  private bool _legacyPanelVisible;

  [ObservableProperty]
  private bool _newPanelVisible;

  // FLOW_HGT_OVR is ArduRover-only.
  [ObservableProperty]
  private bool _heightOverrideVisible;

  public ConfigOptFlowViewModel() {
    Title = "Optical Flow";
    Setup();
  }

  protected override void OnRefreshed() {
    Setup();
  }

  private void Setup() {
    Enable = new ParamField("FLOW_ENABLE", "bool");
    Type = new ParamField("FLOW_TYPE", "combo");
    Yaw = new ParamField("FLOW_ORIENT_YAW");
    FxScaler = new ParamField("FLOW_FXSCALER");
    FyScaler = new ParamField("FLOW_FYSCALER");
    PosX = new ParamField("FLOW_POS_X");
    PosY = new ParamField("FLOW_POS_Y");
    PosZ = new ParamField("FLOW_POS_Z");
    HeightOverride = new ParamField("FLOW_HGT_OVR");

    LegacyPanelVisible = comPort.MAV.param.ContainsKey("FLOW_ENABLE");
    NewPanelVisible = !LegacyPanelVisible;
    HeightOverrideVisible =
        NewPanelVisible && comPort.MAV.cs.firmware == Firmwares.ArduRover;
  }
}
