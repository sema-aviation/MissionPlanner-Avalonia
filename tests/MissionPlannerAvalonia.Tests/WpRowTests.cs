using MissionPlanner.Utilities;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Tests;

public class WpRowTests {
  [Fact]
  public void From_then_ToLocationwp_round_trips_fields() {
    var src = new Locationwp {
      id = (ushort)MAVLink.MAV_CMD.WAYPOINT,
      p1 = 1,
      p2 = 2,
      p3 = 3,
      p4 = 4,
      lat = 40.123456,
      lng = 28.654321,
      alt = 100f,
    };

    var row = WpRow.From(7, src);
    Assert.Equal(7, row.Seq);
    Assert.Equal(src.id, row.Command);

    var back = row.ToLocationwp();
    Assert.Equal(src.id, back.id);
    Assert.Equal(src.p1, back.p1);
    Assert.Equal(src.p4, back.p4);
    Assert.Equal(src.lat, back.lat, 6);
    Assert.Equal(src.lng, back.lng, 6);
    Assert.Equal(src.alt, back.alt, 3);
  }

  [Fact]
  public void CommandName_resolves_enum() {
    var row = new WpRow { Command = (ushort)MAVLink.MAV_CMD.RETURN_TO_LAUNCH };
    Assert.Equal("RETURN_TO_LAUNCH", row.CommandName);
  }
}
