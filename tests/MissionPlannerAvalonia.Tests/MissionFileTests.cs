using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Tests;

public class MissionFileTests {
  [AvaloniaFact]
  public async Task Save_then_Load_round_trips_waypoints() {
    var vm = new FlightPlannerViewModel();
    vm.Waypoints.Add(
        new WpRow {
          Command = (ushort)MAVLink.MAV_CMD.WAYPOINT,
          Lat = 40.1,
          Lng = 28.2,
          Alt = 120,
        }
    );
    vm.Waypoints.Add(
        new WpRow {
          Command = (ushort)MAVLink.MAV_CMD.RETURN_TO_LAUNCH,
          Lat = 0,
          Lng = 0,
          Alt = 0,
        }
    );

    var path = Path.Combine(Path.GetTempPath(), $"mp_test_{System.Guid.NewGuid():N}.waypoints");
    try {
      await vm.SaveFileAsync(path);
      Assert.True(File.Exists(path));
      Assert.StartsWith("QGC WPL 110", File.ReadAllLines(path)[0]);

      var loaded = new FlightPlannerViewModel();
      await loaded.LoadFileAsync(path);

      Assert.Equal(2, loaded.Waypoints.Count);
      Assert.Equal((ushort)MAVLink.MAV_CMD.WAYPOINT, loaded.Waypoints[0].Command);
      Assert.Equal(40.1, loaded.Waypoints[0].Lat, 6);
      Assert.Equal(28.2, loaded.Waypoints[0].Lng, 6);
      Assert.Equal(120, loaded.Waypoints[0].Alt, 3);
    } finally {
      if (File.Exists(path)) {
        File.Delete(path);
      }
    }
  }
}
