using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.ViewModels;

public class ConfigViewModel : BackstageViewModel {
  public ConfigViewModel() {
    Add("Flight Modes", () => new ConfigFlightModesViewModel());
    Add("Standard Params", () => new ConfigFriendlyParamsViewModel(advanced: false));
    Add("Advanced Params", () => new ConfigFriendlyParamsViewModel(advanced: true), advanced: true);
    Add("GeoFence", () => new ConfigAC_FenceViewModel());
    Add("Basic Tuning", () => new ConfigBasicTuningViewModel());
    Add("Basic Tuning (Plane)", () => new ConfigArduplaneViewModel());
    Add("Basic Tuning (Rover)", () => new ConfigArduroverViewModel());
    Add("Extended Tuning", () => new ConfigExtendedTuningViewModel(), advanced: true);
    Add("Onboard OSD", () => new ConfigOSDViewModel());
    Add("User Params", () => new ConfigUserDefinedViewModel());
    Add("Full Parameter List", () => new RawParamsViewModel());
    Add("Planner", () => new ConfigPlannerViewModel());
    Add("Planner (Advanced)", () => new ConfigPlannerAdvViewModel(), advanced: true);

    SelectFirst();
  }
}
