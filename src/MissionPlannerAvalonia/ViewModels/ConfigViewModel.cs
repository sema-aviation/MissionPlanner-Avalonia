using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.ViewModels;

public class ConfigViewModel : BackstageViewModel {
  // Vehicle gating mirrors MP's display* flags (e.g. heli pages only for ArduCopter heli frames,
  // plane tuning only for ArduPlane). firmware lives on the connected vehicle's CurrentState.
  private static MissionPlanner.ArduPilot.Firmwares Fw =>
      AppState.comPort.MAV.cs.firmware;

  private static bool IsCopter => Fw == MissionPlanner.ArduPilot.Firmwares.ArduCopter2;
  private static bool IsPlane => Fw == MissionPlanner.ArduPilot.Firmwares.ArduPlane;
  private static bool IsRover => Fw == MissionPlanner.ArduPilot.Firmwares.ArduRover;

  // Heli pages show only when the connected copter is a traditional-heli frame (H_* params present).
  private static bool IsHeli =>
      IsCopter && AppState.comPort.MAV.param.ContainsKey("H_SWASH_TYPE");

  [System.Obsolete]
  public ConfigViewModel() : base(persistKey: "config_lastpage") {
    Add("Flight Modes", () => new ConfigFlightModesViewModel(), requiresConnection: true);
    Add("Standard Params", () => new ConfigFriendlyParamsViewModel(advanced: false), requiresConnection: true);
    Add("Advanced Params", () => new ConfigFriendlyParamsViewModel(advanced: true), advanced: true, requiresConnection: true);
    Add("GeoFence", () => new ConfigAC_FenceViewModel(), requiresConnection: true);
    Add("Basic Tuning", () => new ConfigBasicTuningViewModel(), requiresConnection: true,
        visibleWhen: () => IsCopter && !IsHeli);
    Add("Heli Setup", () => new ConfigTradHeliViewModel(), requiresConnection: true,
        visibleWhen: () => IsHeli);
    Add("Heli Setup (4.0+)", () => new ConfigTradHeli4ViewModel(), advanced: true,
        requiresConnection: true, visibleWhen: () => IsHeli);
    Add("Basic Tuning (Plane)", () => new ConfigArduplaneViewModel(), requiresConnection: true,
        visibleWhen: () => IsPlane);
    Add("Basic Tuning (Rover)", () => new ConfigArduroverViewModel(), requiresConnection: true,
        visibleWhen: () => IsRover);
    Add(IsPlane ? "QP Extended Tuning" : "Extended Tuning",
        () => new ConfigExtendedTuningViewModel(), advanced: true, requiresConnection: true);
    Add("Initial Parameters", () => new ConfigInitialParamsViewModel(), requiresConnection: true);
    Add("Onboard OSD", () => new ConfigOSDViewModel(), requiresConnection: true);
    Add("MAVFtp", () => new MavFTPUIViewModel(), requiresConnection: true);
    Add("User Params", () => new ConfigUserDefinedViewModel(), requiresConnection: true);
    Add("Full Parameter List", () => new RawParamsViewModel());
    Add("Planner", () => new ConfigPlannerViewModel());
    Add("Planner (Advanced)", () => new ConfigPlannerAdvViewModel(), advanced: true);

    SelectFirst();
  }
}
