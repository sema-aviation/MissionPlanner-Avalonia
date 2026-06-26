using System.Linq;
using Avalonia.Headless.XUnit;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Tests;

// The Mission/Fence/Rally selector reuses one grid backed by three stores (mirrors upstream
// cmb_missiontype + processToScreen). Switching type must preserve each list independently.
public class MissionTypeSwapTests {
  [AvaloniaFact]
  public void Switching_type_preserves_each_lists_points() {
    var vm = new FlightPlannerViewModel();

    // Mission: add a waypoint.
    vm.AddWaypointAt(40.0, 28.0);
    Assert.Single(vm.Waypoints);
    Assert.Equal((ushort)MAVLink.MAV_CMD.WAYPOINT, vm.Waypoints[0].Command);

    // Switch to Fence: grid clears, a fence vertex is added.
    vm.MissionType = "Fence";
    Assert.Empty(vm.Waypoints);
    vm.AddWaypointAt(41.0, 29.0);
    vm.AddWaypointAt(41.1, 29.1);
    Assert.Equal(2, vm.Waypoints.Count);
    Assert.Equal((ushort)MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION, vm.Waypoints[0].Command);

    // Switch to Rally: independent store, rally command.
    vm.MissionType = "Rally";
    Assert.Empty(vm.Waypoints);
    vm.AddWaypointAt(42.0, 30.0);
    Assert.Equal((ushort)MAVLink.MAV_CMD.RALLY_POINT, vm.Waypoints[0].Command);

    // Back to Mission: original waypoint is restored.
    vm.MissionType = "Mission";
    Assert.Single(vm.Waypoints);
    Assert.Equal(40.0, vm.Waypoints[0].Lat, 6);

    // Fence list still has both vertices.
    vm.MissionType = "Fence";
    Assert.Equal(2, vm.Waypoints.Count);
    Assert.Equal(29.1, vm.Waypoints[1].Lng, 6);
  }
}
