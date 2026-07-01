using GeoUtility.GeoSystem;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

public static class Geo {
  public static (int Zone, double Easting, double Northing) ToUtm(double lat, double lng) {
    var p = new PointLatLngAlt(lat, lng);
    int zone = p.GetUTMZone();
    var e = p.ToUTM();
    return (zone, e[0], e[1]);
  }

  public static (double Lat, double Lng) FromUtm(double easting, double northing, int zone) {
    var lla = new utmpos(easting, northing, zone).ToLLA();
    return (lla.Lat, lla.Lng);
  }

  public static string ToMgrs(double lat, double lng) =>
      new Geographic(lng, lat).ConvertTo<MGRS>().ToString();

  public static (double Lat, double Lng) FromMgrs(string mgrs) {
    var g = new MGRS(mgrs).ConvertTo<Geographic>();
    return (g.Latitude, g.Longitude);
  }
}
