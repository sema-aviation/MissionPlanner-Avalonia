using MissionPlanner;

namespace MissionPlannerAvalonia;

public static class AppState {
  public static MAVLinkInterface comPort { get; } = new MAVLinkInterface();
}
