using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.ViewModels;

public class ConfigViewModel : BackstageViewModel {
  public ConfigViewModel() {
    Add("Flight Modes", () => new ConfigFlightModesViewModel());
    Add("Standard Params", () => new ConfigFriendlyParamsViewModel(advanced: false));
    Add("Advanced Params", () => new ConfigFriendlyParamsViewModel(advanced: true), advanced: true);
    Add("GeoFence", () => new ConfigAC_FenceViewModel());
    Add(
        "Basic Tuning",
        () => new InfoPageViewModel("Basic Tuning", "Roll/Pitch/Throttle tuning sliders — port pending.")
    );
    Add(
        "Extended Tuning",
        () => new InfoPageViewModel("Extended Tuning", "Full PID table — port pending.")
    );
    Add("Onboard OSD", () => new ConfigOSDViewModel());
    Add("User Params", () => new ConfigUserDefinedViewModel());
    Add("Full Parameter List", () => new RawParamsViewModel());
    Add("Planner", () => new ConfigPlannerViewModel());

    SelectFirst();
  }
}
