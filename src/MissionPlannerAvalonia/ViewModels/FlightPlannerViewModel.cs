using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class FlightPlannerViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _recomputing;

  public event Action? WaypointsChanged;

  public ObservableCollection<WpRow> Waypoints { get; } = new();

  public event Action? PoiChanged;

  public FlightPlannerViewModel() {
    Waypoints.CollectionChanged += OnWaypointsCollectionChanged;
    try {
      Services.PoiStore.Load();
    } catch {

    }
    try {
      var s = Settings.Instance;
      if (s["fpminaltwarning"] != null
          && double.TryParse(s["fpminaltwarning"], NumberStyles.Any, CultureInfo.InvariantCulture,
              out var aw)) {
        _altWarn = aw;
      }
      _verifyHeight = s.GetBoolean("fpverifyheight", false);
    } catch {

    }
  }

  public async Task AddPoi(double lat, double lng) {
    var name = await Services.Dialogs.InputBox("Add POI", "Name", "POI");
    if (name == null) {
      return;
    }
    Services.PoiStore.Add(lat, lng, DefaultAlt, name);
    PoiChanged?.Invoke();
  }

  public async Task AddPoiAtCoords() {
    var s = await Services.Dialogs.InputBox("POI at Coords", "lat;lng", "0;0");
    var parts = s?.Split(';');
    if (parts is { Length: >= 2 }
        && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat)
        && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var lng)) {
      await AddPoi(lat, lng);
    }
  }

  public void DeleteNearestPoi(double lat, double lng) {
    Services.PoiPoint? best = null;
    double bestD = double.MaxValue;
    foreach (var p in Services.PoiStore.All) {
      double d = (p.Lat - lat) * (p.Lat - lat) + (p.Lng - lng) * (p.Lng - lng);
      if (d < bestD) {
        bestD = d;
        best = p;
      }
    }
    if (best != null) {
      Services.PoiStore.Remove(best);
      PoiChanged?.Invoke();
    }
  }

  public void ClearPois() {
    Services.PoiStore.Clear();
    PoiChanged?.Invoke();
  }

  public event Action? DrawnPolygonChanged;

  public List<PointLatLngAlt> DrawnPolygon { get; } = new();

  public void AddPolygonPoint(double lat, double lng) {
    DrawnPolygon.Add(new PointLatLngAlt(lat, lng, DefaultAlt));
    DrawnPolygonChanged?.Invoke();
  }

  public void ClearPolygon() {
    DrawnPolygon.Clear();
    DrawnPolygonChanged?.Invoke();
    Status = "Polygon cleared.";
  }

  public void BuildPolygonFromWaypoints() {
    DrawnPolygon.Clear();
    foreach (var w in Waypoints) {
      if (w.Lat == 0 && w.Lng == 0) {
        continue;
      }
      DrawnPolygon.Add(new PointLatLngAlt(w.Lat, w.Lng, w.Alt));
    }
    DrawnPolygonChanged?.Invoke();
    Status = $"Polygon built from {DrawnPolygon.Count} point(s).";
  }

  public string PolygonArea() {
    if (DrawnPolygon.Count < 3) {
      Status = "Need at least 3 polygon points for area.";
      return Status;
    }
    double area = PolygonAreaM2(DrawnPolygon);
    Status = $"Polygon area: {area:0} m² ({area / 10000:0.00} ha).";
    return Status;
  }

  private static double PolygonAreaM2(IReadOnlyList<PointLatLngAlt> pts) {
    double lat0 = pts.Average(p => p.Lat) * Math.PI / 180;
    const double mPerDeg = 111320.0;
    double sum = 0;
    for (int i = 0; i < pts.Count; i++) {
      var a = pts[i];
      var b = pts[(i + 1) % pts.Count];
      double ax = a.Lng * mPerDeg * Math.Cos(lat0), ay = a.Lat * mPerDeg;
      double bx = b.Lng * mPerDeg * Math.Cos(lat0), by = b.Lat * mPerDeg;
      sum += ax * by - bx * ay;
    }
    return Math.Abs(sum) / 2;
  }

  public void InsertWaypointAfterSeq(int afterSeq, double lat, double lng) {
    int idx = -1;
    for (int i = 0; i < Waypoints.Count; i++) {
      if (Waypoints[i].Seq == afterSeq) {
        idx = i;
        break;
      }
    }
    if (idx < 0) {
      AddWaypointAt(lat, lng);
      return;
    }
    var cmd = SplineDefault ? MAVLink.MAV_CMD.SPLINE_WAYPOINT : MAVLink.MAV_CMD.WAYPOINT;
    Waypoints.Insert(idx + 1, new WpRow {
      Command = (ushort)cmd,
      Lat = lat,
      Lng = lng,
      Alt = VerifyPlaceAlt(lat, lng, DefaultAlt, (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT),
    });
    Renumber();
    WaypointsChanged?.Invoke();
  }

  // ponytail: one grid + three backing stores for Mission/Fence/Rally, like upstream reusing one DataGridView per list; no per-type row class.
  public string[] MissionTypes { get; } = { "Mission", "Fence", "Rally" };

  [ObservableProperty]
  private string _missionType = "Mission";

  private readonly List<WpRow> _missionStore = new();
  private readonly List<WpRow> _fenceStore = new();
  private readonly List<WpRow> _rallyStore = new();

  private MAVLink.MAV_MISSION_TYPE CurrentMissionType => MissionType switch {
    "Fence" => MAVLink.MAV_MISSION_TYPE.FENCE,
    "Rally" => MAVLink.MAV_MISSION_TYPE.RALLY,
    _ => MAVLink.MAV_MISSION_TYPE.MISSION,
  };

  private List<WpRow> StoreFor(string type) => type switch {
    "Fence" => _fenceStore,
    "Rally" => _rallyStore,
    _ => _missionStore,
  };

  partial void OnMissionTypeChanged(string oldValue, string newValue) {
    var prev = StoreFor(oldValue);
    prev.Clear();
    prev.AddRange(Waypoints);
    Replace(StoreFor(newValue).ToList());
    WaypointsChanged?.Invoke();
  }

  public ObservableCollection<string> MapTypes { get; } =
      new()
      {
            "GoogleSatelliteMap",
            "GoogleHybridMap",
            "BingSatelliteMap",
            "OpenStreetMap",
            "EsriWorldImagery",
      };

  [ObservableProperty]
  private string _mapType = "GoogleSatelliteMap";

  [ObservableProperty]
  private string _status = "Connect, then Read. Or Load File to preview a mission.";

  [ObservableProperty]
  private double _defaultAlt = 100;

  [ObservableProperty]
  private double _wpRadius = 90;

  [ObservableProperty]
  private double _loiterRadius = 100;

  [ObservableProperty]
  private double _altWarn;

  [ObservableProperty]
  private bool _verifyHeight;

  partial void OnAltWarnChanged(double value) {
    try {
      Settings.Instance["fpminaltwarning"] = value.ToString(CultureInfo.InvariantCulture);
    } catch {

    }
  }

  partial void OnVerifyHeightChanged(bool value) {
    try {
      Settings.Instance["fpverifyheight"] = value.ToString();
    } catch {

    }
  }

  [ObservableProperty]
  private double _homeLat;

  [ObservableProperty]
  private double _homeLng;

  [ObservableProperty]
  private double _homeAlt;

  [ObservableProperty]
  private bool _showGrid;

  [ObservableProperty]
  private string _totalDist = "0";

  [ObservableProperty]
  private string _homeDist = "0";

  [ObservableProperty]
  private string _prevDist = "0";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  [Obsolete]
  private async Task ReadWaypoints() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }
    var type = CurrentMissionType;
    Status = $"Reading {MissionType.ToLowerInvariant()}…";
    try {
      var rows = await Task.Run(async () => {
        var list = new List<WpRow>();
        if (type == MAVLink.MAV_MISSION_TYPE.MISSION) {
          ushort count = _comPort.getWPCount(type);
          for (ushort i = 0; i < count; i++) {
            list.Add(WpRow.From(i, _comPort.getWP(i, type)));
          }
        } else {

          var locs = await mav_mission.download(_comPort, _comPort.MAV.sysid, _comPort.MAV.compid, type);
          for (int i = 0; i < locs.Count; i++) {
            list.Add(WpRow.From(i, locs[i]));
          }
        }
        return list;
      });
      Replace(rows);
      Status = $"Read {rows.Count} {MissionType.ToLowerInvariant()} point(s).";
    } catch (Exception ex) {
      Status = "Read failed: " + ex.Message;
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task WriteWaypoints() {
    if (!IsConnected) {
      Status = "Not connected — cannot write.";
      return;
    }
    var rows = Waypoints.ToList();
    var type = CurrentMissionType;

    if (rows.Count == 0 && type == MAVLink.MAV_MISSION_TYPE.MISSION) {
      Status = "No waypoints to write.";
      return;
    }
    if (type == MAVLink.MAV_MISSION_TYPE.MISSION) {
      for (int a = 0; a < rows.Count; a++) {
        var cmd = (MAVLink.MAV_CMD)rows[a].Command;
        if (rows[a].Command < (ushort)MAVLink.MAV_CMD.LAST
            && cmd != MAVLink.MAV_CMD.TAKEOFF && cmd != MAVLink.MAV_CMD.LAND
            && cmd != MAVLink.MAV_CMD.RETURN_TO_LAUNCH
            && rows[a].Alt < AltWarn) {
          await Services.Dialogs.Alert("Low alt",
              "Low alt on WP#" + (a + 1)
              + "\nPlease reduce the alt warning, or increase the altitude");
          Status = "Write aborted: low alt on WP#" + (a + 1);
          return;
        }
      }
    }
    Status = $"Writing {rows.Count} {MissionType.ToLowerInvariant()} point(s)…";
    try {
      await Task.Run(async () => {
        if (type == MAVLink.MAV_MISSION_TYPE.MISSION) {
          WriteRadiusParams();
          _comPort.setWPTotal((ushort)rows.Count, type);
          for (int i = 0; i < rows.Count; i++) {
            _comPort.setWP(rows[i].ToLocationwp(), (ushort)i,
                (MAVLink.MAV_FRAME)rows[i].Frame, (byte)(i == 0 ? 1 : 0));
          }
          _comPort.setWPACK(type);
        } else {
          await mav_mission.upload(_comPort, _comPort.MAV.sysid, _comPort.MAV.compid, type,
              rows.Select(r => r.ToLocationwp()).ToList());
        }
      });
      Status = $"Wrote {rows.Count} {MissionType.ToLowerInvariant()} point(s).";
    } catch (Exception ex) {
      Status = "Write failed: " + ex.Message;
    }
  }

  public string GenerateMissionKmlAndOpen() {
    var pts = Waypoints.Where(w => w.Lat != 0 || w.Lng != 0).ToList();
    if (pts.Count == 0) {
      return "No waypoints to export.";
    }
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\"><Document><name>Mission</name>");
    sb.AppendLine("<Placemark><name>Route</name><LineString><tessellate>1</tessellate><coordinates>");
    foreach (var w in pts) {
      sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", w.Lng, w.Lat, w.Alt));
    }
    sb.AppendLine("</coordinates></LineString></Placemark></Document></kml>");
    var path = Path.Combine(Path.GetTempPath(), "mission.kml");
    File.WriteAllText(path, sb.ToString());
    Services.Dialogs.OpenUrl(path);
    return "Opened mission KML.";
  }

  [Obsolete]
  private void WriteRadiusParams() {
    var param = _comPort.MAV.param;
    void Set(string name, double value) {
      if (param.ContainsKey(name)) {
        _comPort.setParam(name, (float)(value / CurrentState.multiplierdist));
      }
    }
    Set("WP_RADIUS", WpRadius);
    Set("WP_RADIUS_M", WpRadius);
    Set("WP_LOITER_RAD", LoiterRadius);
    Set("LOITER_RAD", LoiterRadius);
  }

  public async Task SaveFileAsync(string path) {
    try {
      var lines = new List<string> { "QGC WPL 110" };
      for (int i = 0; i < Waypoints.Count; i++) {
        var w = Waypoints[i];
        lines.Add(
            string.Join(
                "\t",
                new[]
                {
                            i.ToString(),
                            i == 0 ? "1" : "0",
                            "3",
                            ((int)w.Command).ToString(),
                            F(w.P1),
                            F(w.P2),
                            F(w.P3),
                            F(w.P4),
                            F(w.Lat),
                            F(w.Lng),
                            F(w.Alt),
                            "1",
                }
            )
        );
      }
      await File.WriteAllLinesAsync(path, lines);
      Status = $"Saved {Waypoints.Count} waypoint(s) to {Path.GetFileName(path)}.";
    } catch (Exception ex) {
      Status = "Save failed: " + ex.Message;
    }
  }

  public async Task LoadFileAsync(string path) {
    try {
      var lines = await File.ReadAllLinesAsync(path);
      var rows = new List<WpRow>();
      foreach (var line in lines.Skip(1)) {
        var t = line.Split('\t');
        if (t.Length < 12) {
          continue;
        }

        rows.Add(
            new WpRow {
              Seq = int.Parse(t[0], CultureInfo.InvariantCulture),
              Command = (ushort)int.Parse(t[3], CultureInfo.InvariantCulture),
              P1 = D(t[4]),
              P2 = D(t[5]),
              P3 = D(t[6]),
              P4 = D(t[7]),
              Lat = D(t[8]),
              Lng = D(t[9]),
              Alt = D(t[10]),
            }
        );
      }
      Replace(rows);
      Status = $"Loaded {rows.Count} waypoint(s) from {Path.GetFileName(path)}.";
    } catch (Exception ex) {
      Status = "Load failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private void AddWaypoint() => AddWaypointAt(HomeLat, HomeLng);

  public void AddWaypointAt(double lat, double lng) {
    switch (MissionType) {
      case "Fence":
        AddCommandRow(MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION, lat, lng, 0);
        break;
      case "Rally":
        AddCommandRow(MAVLink.MAV_CMD.RALLY_POINT, lat, lng,
            VerifyPlaceAlt(lat, lng, DefaultAlt, (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT));
        break;
      default:
        AddCommandRow(SplineDefault ? MAVLink.MAV_CMD.SPLINE_WAYPOINT : MAVLink.MAV_CMD.WAYPOINT,
            lat, lng,
            VerifyPlaceAlt(lat, lng, DefaultAlt, (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT));
        break;
    }
  }

  [ObservableProperty]
  private bool _splineDefault;

  public void AddSplineWp(double lat, double lng) =>
      AddCommandRow(MAVLink.MAV_CMD.SPLINE_WAYPOINT, lat, lng,
          VerifyPlaceAlt(lat, lng, DefaultAlt, (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT));

  public void InsertAtCurrentPosition() {
    var cs = _comPort.MAV?.cs;
    if (cs == null || (cs.lat == 0 && cs.lng == 0)) {
      Status = "No vehicle position.";
      return;
    }
    AddWaypointAt(cs.lat, cs.lng);
  }

  public void AddJumpStart() => AddCommandRow(MAVLink.MAV_CMD.DO_JUMP, 0, 0, 0, p1: 1, p2: -1);

  public async Task CreateWpCircle(double lat, double lng) {
    if (await Ask("Radius", "Radius", "50") is not { } s1 || !int.TryParse(s1, out var radius)) {
      return;
    }
    if (await Ask("Points", "Number of points to generate Circle", "20") is not { } s2
        || !int.TryParse(s2, out var points) || points == 0) {
      return;
    }
    if (await Ask("Points", "Direction of circle (-1 or 1)", "1") is not { } s3
        || !int.TryParse(s3, out var direction)) {
      return;
    }
    if (await Ask("angle", "Angle of first point (whole degrees)", "0") is not { } s4
        || !int.TryParse(s4, out var startangle)) {
      return;
    }
    double rad = radius / CurrentState.multiplierdist;
    double a = startangle;
    double step = 360.0 / points;
    if (direction == -1) {
      a += 360;
      step *= -1;
    }
    var center = new PointLatLngAlt(lat, lng);
    int n = 0;
    for (; a <= startangle + 360 && a >= 0; a += step) {
      var p = center.newpos(a, rad);
      AddCommandRow(MAVLink.MAV_CMD.WAYPOINT, p.Lat, p.Lng,
          VerifyPlaceAlt(p.Lat, p.Lng, DefaultAlt, (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT));
      n++;
    }
    Status = $"WP circle: {n} point(s).";
  }

  public async Task CreateSplineCircle(double lat, double lng) {
    if (await Ask("Radius", "Radius", "50") is not { } s1 || !int.TryParse(s1, out var radius)) {
      return;
    }
    if (await Ask("min alt", "Min Alt", "5") is not { } s2 || !int.TryParse(s2, out var minalt)) {
      return;
    }
    if (await Ask("max alt", "Max Alt", "20") is not { } s3 || !int.TryParse(s3, out var maxalt)) {
      return;
    }
    if (await Ask("alt step", "alt step", "5") is not { } s4 || !int.TryParse(s4, out var altstep)) {
      return;
    }
    if (await Ask("angle", "Angle of first point (whole degrees)", "0") is not { } s5
        || !int.TryParse(s5, out var startangle)) {
      return;
    }
    int points = 4;
    double step = 360.0 / points;
    AddCommandRow(MAVLink.MAV_CMD.DO_SET_ROI, lat, lng, 0);
    var center = new PointLatLngAlt(lat, lng);
    bool startup = true;
    for (int stepalt = minalt; stepalt <= maxalt;) {
      for (double a = 0; a <= startangle + 360 && a >= 0; a += step) {
        var p = center.newpos(a, radius);
        AddCommandRow(MAVLink.MAV_CMD.SPLINE_WAYPOINT, p.Lat, p.Lng, stepalt);
        if (!startup) {
          stepalt += altstep / points;
        }
      }
      if (startup) {
        stepalt = minalt;
      }
      startup = false;
    }
    Status = "Spline circle added.";
  }

  public async Task CreateCircleSurvey(double lat, double lng) {
    if (await Ask("", "startalt", "10") is not { } s1 || !int.TryParse(s1, out var startalt)) {
      return;
    }
    if (await Ask("", "endalt", "20") is not { } s2 || !int.TryParse(s2, out var endalt)) {
      return;
    }
    if (await Ask("", "seperation", "2") is not { } s3 || !int.TryParse(s3, out var seperation)
        || seperation == 0) {
      return;
    }
    if (await Ask("", "radius", "5") is not { } s4 || !int.TryParse(s4, out var radius)) {
      return;
    }
    if (await Ask("", "photos", "50") is not { } s5 || !int.TryParse(s5, out var photos)
        || photos == 0) {
      return;
    }
    if (await Ask("", "start heading", "0") is not { } s6
        || !int.TryParse(s6, out var startheading)) {
      return;
    }
    var center = new PointLatLngAlt(lat, lng);
    AddCommandRow(MAVLink.MAV_CMD.DO_SET_ROI, lat, lng, 0);
    for (int alt = startalt; alt <= endalt; alt += seperation) {
      for (int heading = startheading; heading <= startheading + 360; heading += 360 / photos) {
        var np = center.newpos(heading, radius);
        AddCommandRow(MAVLink.MAV_CMD.WAYPOINT, np.Lat, np.Lng, alt, p1: 2);
        AddCommandRow(MAVLink.MAV_CMD.DO_DIGICAM_CONTROL, 0, 0, 0, p2: 1);
      }
    }
    Status = "Circle survey added.";
  }

  private static Task<string?> Ask(string title, string prompt, string value) =>
      Services.Dialogs.InputBox(title, prompt, value);

  public (double[] Dist, double[] Terrain, double[] Planned)? BuildElevationProfile() {
    var pts = Waypoints.Where(w => w.Lat != 0 || w.Lng != 0).ToList();
    if (pts.Count < 2) {
      return null;
    }
    double m = CurrentState.multiplieralt;
    double md = CurrentState.multiplierdist;
    double homeTerr = HomeTerrainAlt();
    var dist = new List<double>();
    var terr = new List<double>();
    var plan = new List<double>();
    double cum = 0;
    var first = new PointLatLngAlt(pts[0].Lat, pts[0].Lng);
    AddSample(dist, terr, plan, 0, pts[0].Lat, pts[0].Lng, pts[0].Alt, pts[0].Frame, homeTerr, m);
    for (int i = 1; i < pts.Count; i++) {
      var a = pts[i - 1];
      var b = pts[i];
      var pa = new PointLatLngAlt(a.Lat, a.Lng);
      var pb = new PointLatLngAlt(b.Lat, b.Lng);
      double leg = pb.GetDistance(pa);
      int segs = Math.Max(1, (int)(leg / 100));
      for (int s = 1; s <= segs; s++) {
        double f = (double)s / segs;
        double la = a.Lat + (b.Lat - a.Lat) * f;
        double lo = a.Lng + (b.Lng - a.Lng) * f;
        double alt = a.Alt + (b.Alt - a.Alt) * f;
        cum += leg / segs * md;
        AddSample(dist, terr, plan, cum, la, lo, alt, b.Frame, homeTerr, m);
      }
    }
    return (dist.ToArray(), terr.ToArray(), plan.ToArray());
  }

  private static void AddSample(List<double> dist, List<double> terr, List<double> plan, double x,
      double lat, double lng, double alt, byte frame, double homeTerr, double m) {
    var t = srtm.getAltitude(lat, lng);
    double terrain = t.currenttype == srtm.tiletype.invalid ? 0 : t.alt * m;
    double planned = (MAVLink.MAV_FRAME)frame switch {
      MAVLink.MAV_FRAME.GLOBAL => alt,
      MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT => alt + terrain,
      _ => alt + homeTerr,
    };
    dist.Add(x);
    terr.Add(terrain);
    plan.Add(planned);
  }

  private PointLatLngAlt? _measureStart;

  public async Task MeasureClick(double lat, double lng) {
    if (_measureStart == null) {
      _measureStart = new PointLatLngAlt(lat, lng);
      Status = "Measure: start set — click Measure again at the end point.";
      return;
    }
    var end = new PointLatLngAlt(lat, lng);
    double dist = end.GetDistance(_measureStart) * CurrentState.multiplierdist;
    double az = (end.GetBearing(_measureStart) + 180) % 360;
    _measureStart = null;
    Status = $"Distance: {dist:0.0} {CurrentState.DistanceUnit}  AZ: {az:0}";
    await Services.Dialogs.Alert("Measure",
        $"Distance: {dist:0.0} {CurrentState.DistanceUnit}  AZ: {az:0}");
  }

  public void SetFenceReturn(double lat, double lng) {
    var existing = Waypoints.Where(w => w.Command == (ushort)MAVLink.MAV_CMD.FENCE_RETURN_POINT)
                       .ToList();
    foreach (var r in existing) {
      Waypoints.Remove(r);
    }
    AddCommandRow(MAVLink.MAV_CMD.FENCE_RETURN_POINT, lat, lng, 0);
  }

  private WpRow AddCommandRow(MAVLink.MAV_CMD cmd, double lat, double lng, double alt,
      double p1 = 0, double p2 = 0, double p3 = 0, double p4 = 0) {
    var row = new WpRow {
      Seq = Waypoints.Count,
      Command = (ushort)cmd,
      Alt = alt,
      Lat = lat,
      Lng = lng,
      P1 = p1,
      P2 = p2,
      P3 = p3,
      P4 = p4,
    };
    Waypoints.Add(row);
    Renumber();
    WaypointsChanged?.Invoke();
    return row;
  }

  public void InsertWaypointAt(double lat, double lng) => AddWaypointAt(lat, lng);

  public void DeleteNearest(double lat, double lng) {
    WpRow? best = null;
    double bestD = double.MaxValue;
    foreach (var w in Waypoints) {
      double d = (w.Lat - lat) * (w.Lat - lat) + (w.Lng - lng) * (w.Lng - lng);
      if (d < bestD) {
        bestD = d;
        best = w;
      }
    }
    if (best != null) {
      Waypoints.Remove(best);
      Renumber();
      WaypointsChanged?.Invoke();
    }
  }

  public void AddRtl() => AddCommandRow(MAVLink.MAV_CMD.RETURN_TO_LAUNCH, 0, 0, 0);

  public void AddLand(double lat, double lng) => AddCommandRow(MAVLink.MAV_CMD.LAND, lat, lng, 0);

  public async Task AddTakeoff(double lat, double lng) {
    var s = await Services.Dialogs.InputBox("Takeoff", "Takeoff alt (m)", DefaultAlt.ToString("0", CultureInfo.InvariantCulture));
    if (double.TryParse(s, out var alt)) {
      AddCommandRow(MAVLink.MAV_CMD.TAKEOFF, lat, lng, alt);
    }
  }

  [Obsolete]
  public void AddRoi(double lat, double lng) => AddCommandRow(MAVLink.MAV_CMD.DO_SET_ROI, lat, lng, DefaultAlt);

  public void AddLoiterForever(double lat, double lng) =>
      AddCommandRow(MAVLink.MAV_CMD.LOITER_UNLIM, lat, lng, DefaultAlt);

  public async Task AddLoiterTime(double lat, double lng) {
    var s = await Services.Dialogs.InputBox("Loiter Time", "Seconds", "60");
    if (double.TryParse(s, out var sec)) {
      AddCommandRow(MAVLink.MAV_CMD.LOITER_TIME, lat, lng, DefaultAlt, p1: sec);
    }
  }

  public async Task AddLoiterCircles(double lat, double lng) {
    var s = await Services.Dialogs.InputBox("Loiter Circles", "Turns", "3");
    if (double.TryParse(s, out var turns)) {
      AddCommandRow(MAVLink.MAV_CMD.LOITER_TURNS, lat, lng, DefaultAlt, p1: turns);
    }
  }

  public async Task AddJump() {
    var s = await Services.Dialogs.InputBox("Jump (DO_JUMP)", "Target WP #", "0");
    if (double.TryParse(s, out var wp)) {
      AddCommandRow(MAVLink.MAV_CMD.DO_JUMP, 0, 0, 0, p1: wp, p2: -1);
    }
  }

  [RelayCommand]
  private void ClearMission() {
    Waypoints.Clear();
    WaypointsChanged?.Invoke();
  }

  [RelayCommand]
  private void ReverseWaypoints() {
    var rev = Waypoints.Reverse().ToList();
    Waypoints.Clear();
    foreach (var w in rev) {
      Waypoints.Add(w);
    }
    Renumber();
    WaypointsChanged?.Invoke();
  }

  public async Task ModifyAllAlt() {
    var s = await Services.Dialogs.InputBox("Modify Alt", "New altitude for all waypoints (m)",
        DefaultAlt.ToString("0", CultureInfo.InvariantCulture));
    if (double.TryParse(s, out var alt)) {
      foreach (var w in Waypoints) {
        w.Alt = alt;
      }
      WaypointsChanged?.Invoke();
    }
  }

  [ObservableProperty]
  private WpRow? _selectedWaypoint;

  [RelayCommand]
  private void DeleteWaypoint(WpRow? row) {
    row ??= SelectedWaypoint;
    if (row != null) {
      Waypoints.Remove(row);
      Renumber();
      WaypointsChanged?.Invoke();
    }
  }

  [RelayCommand]
  private void MoveWaypointUp(WpRow? row) {
    int i = row == null ? -1 : Waypoints.IndexOf(row);
    if (i > 0) {
      Waypoints.Move(i, i - 1);
      Renumber();
      WaypointsChanged?.Invoke();
    }
  }

  [RelayCommand]
  private void MoveWaypointDown(WpRow? row) {
    int i = row == null ? -1 : Waypoints.IndexOf(row);
    if (i >= 0 && i < Waypoints.Count - 1) {
      Waypoints.Move(i, i + 1);
      Renumber();
      WaypointsChanged?.Invoke();
    }
  }

  [RelayCommand]
  private async Task SetHomeFromVehicle() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }
    await Task.Yield();
    var cs = _comPort.MAV.cs;
    HomeLat = cs.lat;
    HomeLng = cs.lng;
    HomeAlt = cs.altasl;
    Status = "Home set from vehicle position.";
  }

  public void SetHome(double lat, double lng) {
    HomeLat = lat;
    HomeLng = lng;
    var t = srtm.getAltitude(lat, lng);
    if (t.currenttype != srtm.tiletype.invalid) {
      HomeAlt = Math.Round(t.alt * CurrentState.multiplieralt, 2);
    }
    Status = "Home set to clicked location.";
  }

  private double VerifyPlaceAlt(double lat, double lng, double baseAlt, byte frame) {
    if (!VerifyHeight) {
      return baseAlt;
    }
    var t = srtm.getAltitude(lat, lng);
    if (t.currenttype == srtm.tiletype.invalid) {
      return baseAlt;
    }
    double terr = t.alt * CurrentState.multiplieralt;
    return (MAVLink.MAV_FRAME)frame switch {
      MAVLink.MAV_FRAME.GLOBAL => terr + baseAlt,
      MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT => baseAlt,
      _ => terr + baseAlt - HomeTerrainAlt(),
    };
  }

  private double HomeTerrainAlt() {
    var h = srtm.getAltitude(HomeLat, HomeLng);
    return h.currenttype == srtm.tiletype.invalid ? 0 : h.alt * CurrentState.multiplieralt;
  }

  public void MoveWaypoint(int seq, double lat, double lng) {
    var row = Waypoints.FirstOrDefault(r => r.Seq == seq);
    if (row == null) {
      return;
    }

    if (VerifyHeight
        && (MAVLink.MAV_FRAME)row.Frame != MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT) {
      var oldT = srtm.getAltitude(row.Lat, row.Lng);
      var newT = srtm.getAltitude(lat, lng);
      if (oldT.currenttype != srtm.tiletype.invalid
          && newT.currenttype != srtm.tiletype.invalid) {
        double m = CurrentState.multiplieralt;
        row.Alt = row.Alt + newT.alt * m - oldT.alt * m;
      }
    }

    row.Lat = lat;
    row.Lng = lng;
  }

  public string GenerateSurveyGrid(double altitude, double spacing, double angle) {
    var polygon = Waypoints.Where(w => !(w.Lat == 0 && w.Lng == 0))
                      .Select(w => new PointLatLngAlt(w.Lat, w.Lng, altitude))
                      .ToList();
    if (polygon.Count < 3) {
      return "Need at least 3 waypoints to outline the survey area.";
    }

    var home = (HomeLat == 0 && HomeLng == 0)
                   ? polygon[0]
                   : new PointLatLngAlt(HomeLat, HomeLng, HomeAlt);
    try {
      var grid = Grid.CreateGrid(polygon, altitude, spacing, 0, angle, 0, 0,
          Grid.StartPosition.Home, false, 0, 0, 0, home);
      if (grid.Count == 0) {
        return "Grid generation produced no waypoints.";
      }

      foreach (var p in grid) {
        Waypoints.Add(new WpRow {
          Seq = Waypoints.Count,
          Command = (ushort)MAVLink.MAV_CMD.WAYPOINT,
          Lat = p.Lat,
          Lng = p.Lng,
          Alt = p.Alt,
        });
      }

      Renumber();
      RecomputeGrid();
      WaypointsChanged?.Invoke();
      return $"Survey grid added {grid.Count} waypoint(s).";
    } catch (Exception ex) {
      return "Survey failed: " + ex.Message;
    }
  }

  public (System.Collections.Generic.List<PointLatLngAlt> polygon, PointLatLngAlt home)? BuildSurveyArea() {
    var polygon = Waypoints.Where(w => !(w.Lat == 0 && w.Lng == 0))
                      .Select(w => new PointLatLngAlt(w.Lat, w.Lng, DefaultAlt))
                      .ToList();
    if (polygon.Count < 3) {
      return null;
    }

    var home = (HomeLat == 0 && HomeLng == 0)
                   ? polygon[0]
                   : new PointLatLngAlt(HomeLat, HomeLng, HomeAlt);
    return (polygon, home);
  }

  public string AppendSurveyGrid(System.Collections.Generic.List<PointLatLngAlt> grid) {
    if (grid == null || grid.Count == 0) {
      return "Grid produced no waypoints.";
    }

    foreach (var p in grid) {
      Waypoints.Add(new WpRow {
        Seq = Waypoints.Count,
        Command = (ushort)MAVLink.MAV_CMD.WAYPOINT,
        Lat = p.Lat,
        Lng = p.Lng,
        Alt = p.Alt,
      });
    }

    Renumber();
    RecomputeGrid();
    WaypointsChanged?.Invoke();
    return $"Survey grid added {grid.Count} waypoint(s).";
  }

  private void OnWaypointsCollectionChanged(object? sender,
      System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
    if (e.OldItems != null) {
      foreach (WpRow r in e.OldItems) {
        r.PropertyChanged -= OnRowChanged;
      }
    }

    if (e.NewItems != null) {
      foreach (WpRow r in e.NewItems) {
        r.PropertyChanged -= OnRowChanged;
        r.PropertyChanged += OnRowChanged;
      }
    }

    RecomputeGrid();
    WaypointsChanged?.Invoke();
  }

  private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
    if (_recomputing) {
      return;
    }

    if (e.PropertyName is nameof(WpRow.Lat) or nameof(WpRow.Lng) or nameof(WpRow.Alt)) {
      RecomputeGrid();
      WaypointsChanged?.Invoke();
    }
  }

  private void RecomputeGrid() {
    if (_recomputing) {
      return;
    }

    _recomputing = true;
    try {
      var home = (HomeLat == 0 && HomeLng == 0)
                     ? null
                     : new PointLatLngAlt(HomeLat, HomeLng, HomeAlt);
      PointLatLngAlt? last = home;
      double total = 0, prev = 0;
      bool first = true;
      foreach (var w in Waypoints) {
        if (w.Lat == 0 && w.Lng == 0) {
          w.Grad = w.Angle = w.Dist = w.Az = "";
          continue;
        }

        var cur = new PointLatLngAlt(w.Lat, w.Lng, w.Alt);
        if (last == null) {
          w.Grad = w.Angle = w.Dist = w.Az = "";
        } else {
          double height = w.Alt - last.Alt;
          double distance = cur.GetDistance(last);
          double leg = Math.Sqrt(distance * distance + height * height);
          double grad = distance > 0 ? height / distance : 0;
          w.Grad = (grad * 100).ToString("0.0", CultureInfo.InvariantCulture);
          w.Angle = ((180.0 / Math.PI) * Math.Atan(grad)).ToString("0.0",
              CultureInfo.InvariantCulture);
          w.Dist = leg.ToString("0.0", CultureInfo.InvariantCulture);
          w.Az = ((cur.GetBearing(last) + 180) % 360).ToString("0", CultureInfo.InvariantCulture);

          if (!first) {
            total += leg;
          }
          prev = leg;
        }

        last = cur;
        first = false;
      }

      TotalDist = total.ToString("0", CultureInfo.InvariantCulture);
      PrevDist = prev.ToString("0", CultureInfo.InvariantCulture);
      HomeDist = (home != null && last != null && last != home)
                     ? last.GetDistance(home).ToString("0", CultureInfo.InvariantCulture)
                     : "0";
    } finally {
      _recomputing = false;
    }
  }

  partial void OnHomeLatChanged(double value) => RecomputeGrid();

  partial void OnHomeLngChanged(double value) => RecomputeGrid();

  partial void OnHomeAltChanged(double value) => RecomputeGrid();

  private void Replace(IEnumerable<WpRow> rows) {
    void Apply() {
      Waypoints.Clear();
      foreach (var r in rows) {
        Waypoints.Add(r);
      }

      Renumber();
    }
    if (Dispatcher.UIThread.CheckAccess()) {
      Apply();
    } else {
      Dispatcher.UIThread.Post(Apply);
    }
  }

  private void Renumber() {
    for (int i = 0; i < Waypoints.Count; i++) {
      Waypoints[i].Seq = i;
    }
  }

  private static string F(double v) => v.ToString("0.000000", CultureInfo.InvariantCulture);

  private static double D(string s) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public partial class WpRow : ObservableObject {
  [ObservableProperty]
  private int _seq;

  [ObservableProperty]
  private ushort _command;

  [ObservableProperty]
  private double _p1;

  [ObservableProperty]
  private double _p2;

  [ObservableProperty]
  private double _p3;

  [ObservableProperty]
  private double _p4;

  [ObservableProperty]
  private double _lat;

  [ObservableProperty]
  private double _lng;

  [ObservableProperty]
  private double _alt;

  [ObservableProperty]
  private string _grad = "";

  [ObservableProperty]
  private string _angle = "";

  [ObservableProperty]
  private string _dist = "";

  [ObservableProperty]
  private string _az = "";

  [ObservableProperty]
  private byte _frame = (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT;

  [ObservableProperty]
  private string _zone = "";

  [ObservableProperty]
  private string _easting = "";

  [ObservableProperty]
  private string _northing = "";

  [ObservableProperty]
  private string _mgrs = "";

  public static readonly string[] CommandList = System.Enum.GetNames(typeof(MAVLink.MAV_CMD));

  public string CommandName {
    get => ((MAVLink.MAV_CMD)Command).ToString();
    set {
      if (System.Enum.TryParse<MAVLink.MAV_CMD>(value, out var cmd)) {
        Command = (ushort)cmd;
        OnPropertyChanged(nameof(CommandName));
      }
    }
  }

  public static readonly string[] FrameList = { "Relative", "Absolute", "Terrain" };

  public string FrameName {
    get => Frame switch {
      (byte)MAVLink.MAV_FRAME.GLOBAL => "Absolute",
      (byte)MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT => "Terrain",
      _ => "Relative",
    };
    set {
      Frame = value switch {
        "Absolute" => (byte)MAVLink.MAV_FRAME.GLOBAL,
        "Terrain" => (byte)MAVLink.MAV_FRAME.GLOBAL_TERRAIN_ALT,
        _ => (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
      };
      OnPropertyChanged(nameof(FrameName));
    }
  }

  private bool _coordGuard;

  partial void OnLatChanged(double value) => RecomputeCoords();

  partial void OnLngChanged(double value) => RecomputeCoords();

  private void RecomputeCoords() {
    if (_coordGuard) {
      return;
    }
    if (Lat == 0 && Lng == 0) {
      _coordGuard = true;
      Zone = Easting = Northing = Mgrs = "";
      _coordGuard = false;
      return;
    }
    try {
      var (zone, e, n) = Services.Geo.ToUtm(Lat, Lng);
      _coordGuard = true;
      Zone = zone.ToString(CultureInfo.InvariantCulture);
      Easting = e.ToString("0.0", CultureInfo.InvariantCulture);
      Northing = n.ToString("0.0", CultureInfo.InvariantCulture);
      Mgrs = Services.Geo.ToMgrs(Lat, Lng);
    } catch {

    } finally {
      _coordGuard = false;
    }
  }

  partial void OnZoneChanged(string value) => ReverseFromUtm();

  partial void OnEastingChanged(string value) => ReverseFromUtm();

  partial void OnNorthingChanged(string value) => ReverseFromUtm();

  private void ReverseFromUtm() {
    if (_coordGuard) {
      return;
    }
    if (!int.TryParse(Zone, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zone)
        || !double.TryParse(Easting, NumberStyles.Any, CultureInfo.InvariantCulture, out var e)
        || !double.TryParse(Northing, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) {
      return;
    }
    try {
      _coordGuard = true;
      var (lat, lng) = Services.Geo.FromUtm(e, n, zone);
      Lat = lat;
      Lng = lng;
    } catch {

    } finally {
      _coordGuard = false;
    }
  }

  partial void OnMgrsChanged(string value) {
    if (_coordGuard || string.IsNullOrWhiteSpace(value)) {
      return;
    }
    try {
      _coordGuard = true;
      var (lat, lng) = Services.Geo.FromMgrs(value.Trim());
      Lat = lat;
      Lng = lng;
    } catch {

    } finally {
      _coordGuard = false;
    }
  }

  public static WpRow From(int seq, Locationwp wp) =>
      new() {
        Seq = seq,
        Command = wp.id,
        Frame = wp.frame,
        P1 = wp.p1,
        P2 = wp.p2,
        P3 = wp.p3,
        P4 = wp.p4,
        Lat = wp.lat,
        Lng = wp.lng,
        Alt = wp.alt,
      };

  public Locationwp ToLocationwp() =>
      new() {
        id = Command,
        frame = Frame,
        p1 = (float)P1,
        p2 = (float)P2,
        p3 = (float)P3,
        p4 = (float)P4,
        lat = Lat,
        lng = Lng,
        alt = (float)Alt,
      };
}
