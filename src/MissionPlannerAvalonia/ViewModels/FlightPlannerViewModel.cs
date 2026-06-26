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
      // ignore POI load failures (corrupt/missing file)
    }
  }

  // ---- POI map points (mirrors the FlightData "Add Poi" submenu; persisted via PoiStore). ----
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

  // ---- Drawn polygon tools (mirrors FlightPlanner's draw-polygon overlay / area readout). ----
  public event Action? DrawnPolygonChanged;

  public List<PointLatLngAlt> DrawnPolygon { get; } = new();

  // Add a vertex while the map is in polygon-draw mode.
  public void AddPolygonPoint(double lat, double lng) {
    DrawnPolygon.Add(new PointLatLngAlt(lat, lng, DefaultAlt));
    DrawnPolygonChanged?.Invoke();
  }

  public void ClearPolygon() {
    DrawnPolygon.Clear();
    DrawnPolygonChanged?.Invoke();
    Status = "Polygon cleared.";
  }

  // Build the polygon from the current waypoint lat/lngs (mirrors "polygon from current path").
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

  // Report the enclosed area (shoelace on a local equirectangular projection).
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

  // Midline "+" insert: drop a new WP between the segment whose leading waypoint has this Seq.
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
      Alt = DefaultAlt,
    });
    Renumber();
    WaypointsChanged?.Invoke();
  }

  // ---- Mission / Fence / Rally edit-type selector (mirrors cmb_missiontype). ----
  // ponytail: one grid, three backing stores — exactly like upstream processToScreen reusing the
  // same DataGridView for whichever list the selector picks. No per-type row class.
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

  // Swap the active grid contents when the edit type changes (save current rows back, load the other).
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
  private double _homeLat;

  [ObservableProperty]
  private double _homeLng;

  [ObservableProperty]
  private double _homeAlt;

  [ObservableProperty]
  private bool _showGrid;

  // Map distance readouts (mirror lbl_distance / lbl_homedist / lbl_prevdist).
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
          // Fence/Rally: mav_mission handles both the modern protocol and the legacy
          // FENCE_FETCH_POINT / RALLY_FETCH_POINT fallback for older autopilots.
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
    // Fence/Rally may legitimately upload an empty list to clear the vehicle's copy.
    if (rows.Count == 0 && type == MAVLink.MAV_MISSION_TYPE.MISSION) {
      Status = "No waypoints to write.";
      return;
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

  // Generate KML of the current mission and open it in the system viewer (mirrors MP "View KML",
  // which GENERATES the mission KML rather than loading an external one).
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

  // Push the WP-radius / loiter-radius boxes to whatever radius params the vehicle exposes
  // (mirrors MP writing WP_RADIUS / WP_LOITER_RAD on mission upload).
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

  // Append a point at a map location (click-to-add). The command matches the active edit type:
  // a fence inclusion-polygon vertex, a rally point, or a plain mission waypoint (mirrors upstream
  // FlightPlanner's per-missiontype default in Commands_DefaultValuesNeeded).
  public void AddWaypointAt(double lat, double lng) {
    switch (MissionType) {
      case "Fence":
        AddCommandRow(MAVLink.MAV_CMD.FENCE_POLYGON_VERTEX_INCLUSION, lat, lng, 0);
        break;
      case "Rally":
        AddCommandRow(MAVLink.MAV_CMD.RALLY_POINT, lat, lng, DefaultAlt);
        break;
      default:
        AddCommandRow(SplineDefault ? MAVLink.MAV_CMD.SPLINE_WAYPOINT : MAVLink.MAV_CMD.WAYPOINT,
            lat, lng, DefaultAlt);
        break;
    }
  }

  // When set, new map-added waypoints become SPLINE_WAYPOINT (mirrors CHK_splinedefault).
  [ObservableProperty]
  private bool _splineDefault;

  // "Insert Spline WP" context item — always a spline regardless of the default toggle.
  public void AddSplineWp(double lat, double lng) =>
      AddCommandRow(MAVLink.MAV_CMD.SPLINE_WAYPOINT, lat, lng, DefaultAlt);

  // "Insert at Current Position" — drop a waypoint at the vehicle's live GPS position.
  public void InsertAtCurrentPosition() {
    var cs = _comPort.MAV?.cs;
    if (cs == null || (cs.lat == 0 && cs.lng == 0)) {
      Status = "No vehicle position.";
      return;
    }
    AddWaypointAt(cs.lat, cs.lng);
  }

  // "Jump → Start" — DO_JUMP back to waypoint #1.
  public void AddJumpStart() => AddCommandRow(MAVLink.MAV_CMD.DO_JUMP, 0, 0, 0, p1: 1, p2: -1);

  // Fence "Set Return Location" — a single FENCE_RETURN_POINT; replaces any existing one.
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

  // ---- Map context menu (mirrors FlightPlanner contextMenuStrip1, Mission mode) ----
  public void InsertWaypointAt(double lat, double lng) => AddWaypointAt(lat, lng);

  // Delete the waypoint nearest the clicked location (mirrors "Delete WP").
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

  // Bound to the grid's SelectedItem so the Delete key knows which row to remove.
  [ObservableProperty]
  private WpRow? _selectedWaypoint;

  [RelayCommand]
  private void DeleteWaypoint(WpRow? row) {
    row ??= SelectedWaypoint; // Delete key passes the selected row; the grid "X" passes its own
    if (row != null) {
      Waypoints.Remove(row);
      Renumber();
      WaypointsChanged?.Invoke();
    }
  }

  // Reorder rows (mirrors the Up/Down grid columns); WaypointsChanged redraws the route.
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

  public void MoveWaypoint(int seq, double lat, double lng) {
    var row = Waypoints.FirstOrDefault(r => r.Seq == seq);
    if (row == null) {
      return;
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

  // The WP polygon + home fed to the full Survey-Grid window (Grid/GridUI.cs).
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

  // Append a generated survey grid (from GridUIWindow) onto the mission.
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
          // Skip the home→first-wp hop in the running total (matches MP's leg-sum).
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

  // Per-row alt frame: 0 GLOBAL (Absolute), 3 GLOBAL_RELATIVE_ALT (Relative), 10 GLOBAL_TERRAIN_ALT.
  [ObservableProperty]
  private byte _frame = (byte)MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT;

  // UTM / MGRS derived display columns (recomputed from Lat/Lng).
  [ObservableProperty]
  private string _zone = "";

  [ObservableProperty]
  private string _easting = "";

  [ObservableProperty]
  private string _northing = "";

  [ObservableProperty]
  private string _mgrs = "";

  // All MAV_CMD names for the editable Command dropdown.
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

  // Guards the UTM/MGRS columns against feeding their own forward recompute back into a reverse
  // conversion (and vice-versa) when Lat/Lng changes update the derived cells.
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
      // leave previous values on conversion failure (polar / edge cases)
    } finally {
      _coordGuard = false;
    }
  }

  // Editing Zone/Easting/Northing converts the trio back to Lat/Lng (mirrors convertFromUTM).
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
      // ignore malformed UTM input
    } finally {
      _coordGuard = false;
    }
  }

  // Editing the MGRS cell converts it back to Lat/Lng (mirrors convertFromMGRS).
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
      // ignore malformed MGRS input
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
