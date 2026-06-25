using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.ViewModels;

public class ConfigViewModel : BackstageViewModel {
  public ConfigViewModel() {
    Add("Flight Modes", () => new ConfigFlightModesViewModel(), requiresConnection: true);
    Add("Standard Params", () => new ConfigFriendlyParamsViewModel(advanced: false), requiresConnection: true);
    Add("Advanced Params", () => new ConfigFriendlyParamsViewModel(advanced: true), advanced: true, requiresConnection: true);
    Add("GeoFence", () => new ConfigAC_FenceViewModel(), requiresConnection: true);
    Add("Basic Tuning", () => new ConfigBasicTuningViewModel(), requiresConnection: true);
    Add("Basic Tuning (Plane)", () => new ConfigArduplaneViewModel(), requiresConnection: true);
    Add("Basic Tuning (Rover)", () => new ConfigArduroverViewModel(), requiresConnection: true);
    Add("Extended Tuning", () => new ConfigExtendedTuningViewModel(), advanced: true, requiresConnection: true);
    Add("Onboard OSD", () => new ConfigOSDViewModel(), requiresConnection: true);
    Add("User Params", () => new ConfigUserDefinedViewModel(), requiresConnection: true);
    Add("Full Parameter List", () => new RawParamsViewModel());
    Add("Planner", () => new ConfigPlannerViewModel());
    Add("Planner (Advanced)", () => new ConfigPlannerAdvViewModel(), advanced: true);

    SelectFirst();
  }
}
