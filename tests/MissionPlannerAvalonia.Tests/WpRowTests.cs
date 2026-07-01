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

  [Fact]
  public void CommandName_setter_updates_command() {
    var row = new WpRow();
    row.CommandName = "LAND";
    Assert.Equal((ushort)MAVLink.MAV_CMD.LAND, row.Command);
  }

  [Fact]
  public void FrameName_maps_both_ways_and_round_trips() {
    var row = new WpRow { FrameName = "Terrain" };
    Assert.Equal((byte)MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT, row.Frame);
    var back = WpRow.From(0, row.ToLocationwp());
    Assert.Equal("Terrain", back.FrameName);
  }

  [Fact]
  public void Setting_lat_lng_populates_utm_and_mgrs() {
    var row = new WpRow { Lat = -35.363261, Lng = 149.165230 };
    Assert.False(string.IsNullOrWhiteSpace(row.Zone));
    Assert.False(string.IsNullOrWhiteSpace(row.Mgrs));
  }

  [Fact]
  public void Editing_utm_cells_converts_back_to_lat_lng() {
    var row = new WpRow { Lat = -35.363261, Lng = 149.165230 };
    string zone = row.Zone, easting = row.Easting, northing = row.Northing;

    row.Lat = 0;
    row.Lng = 0;
    row.Zone = zone;
    row.Easting = easting;
    row.Northing = northing;

    Assert.Equal(-35.363261, row.Lat, 4);
    Assert.Equal(149.165230, row.Lng, 4);
  }

  [Fact]
  public void Editing_mgrs_cell_converts_back_to_lat_lng() {
    var row = new WpRow { Lat = -35.363261, Lng = 149.165230 };
    string mgrs = row.Mgrs;

    row.Lat = 0;
    row.Lng = 0;
    row.Mgrs = mgrs;

    Assert.Equal(-35.363261, row.Lat, 3);
    Assert.Equal(149.165230, row.Lng, 3);
  }
}
