using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.Tests;

public class GeoTests {
  // Canberra-ish point used across MP/SITL defaults.
  private const double Lat = -35.363261;
  private const double Lng = 149.165230;

  [Fact]
  public void Utm_round_trips_within_a_metre() {
    var (zone, e, n) = Geo.ToUtm(Lat, Lng);
    var (lat, lng) = Geo.FromUtm(e, n, zone);
    Assert.Equal(Lat, lat, 4);
    Assert.Equal(Lng, lng, 4);
  }

  [Fact]
  public void Mgrs_round_trips_within_tolerance() {
    var mgrs = Geo.ToMgrs(Lat, Lng);
    Assert.False(string.IsNullOrWhiteSpace(mgrs));
    var (lat, lng) = Geo.FromMgrs(mgrs);
    Assert.Equal(Lat, lat, 3);
    Assert.Equal(Lng, lng, 3);
  }
}
