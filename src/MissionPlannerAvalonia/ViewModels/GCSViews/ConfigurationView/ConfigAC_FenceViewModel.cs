using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAC_FenceViewModel : ParamPageBase {
  [ObservableProperty]
  private ParamField _enable = null!;

  [ObservableProperty]
  private ParamField _type = null!;

  [ObservableProperty]
  private ParamField _action = null!;

  [ObservableProperty]
  private ParamField _maxAlt = null!;

  [ObservableProperty]
  private ParamField _minAlt = null!;

  [ObservableProperty]
  private ParamField _maxRadius = null!;

  [ObservableProperty]
  private ParamField _rtlAlt = null!;

  public string MaxAltLabel => "Max Alt[" + CurrentState.DistanceUnit + "]";
  public string MinAltLabel => "Min Alt[" + CurrentState.DistanceUnit + "]";
  public string MaxRadiusLabel => "Max Radius[" + CurrentState.DistanceUnit + "]";
  public string RtlAltLabel => "RTL Altitude[" + CurrentState.DistanceUnit + "]";

  public ConfigAC_FenceViewModel() {
    Title = "GeoFence";
    Setup();
  }

  protected override void OnRefreshed() {
    Setup();
  }

  private void Setup() {
    Enable = new ParamField("FENCE_ENABLE", "bool");
    Type = new ParamField("FENCE_TYPE", "combo");
    Action = new ParamField("FENCE_ACTION", "combo");
    MaxAlt = new ParamField("FENCE_ALT_MAX");
    MinAlt = new ParamField("FENCE_ALT_MIN");
    MaxRadius = new ParamField("FENCE_RADIUS");
    RtlAlt = new ParamField(
        comPort.MAV.param.ContainsKey("RTL_ALT_M") ? "RTL_ALT_M" : "RTL_ALT");
  }
}
