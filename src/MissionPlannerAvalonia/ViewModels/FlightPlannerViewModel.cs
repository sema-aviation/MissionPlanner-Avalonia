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
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class FlightPlannerViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _recomputing;

  public event Action? WaypointsChanged;

  public ObservableCollection<WpRow> Waypoints { get; } = new();

  public FlightPlannerViewModel() {
    Waypoints.CollectionChanged += OnWaypointsCollectionChanged;
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

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  [Obsolete]
  private async Task ReadWaypoints() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }
    Status = "Reading mission…";
    try {
      var rows = await Task.Run(() => {
        var list = new List<WpRow>();
        ushort count = _comPort.getWPCount(MAVLink.MAV_MISSION_TYPE.MISSION);
        for (ushort i = 0; i < count; i++) {
          var wp = _comPort.getWP(i, MAVLink.MAV_MISSION_TYPE.MISSION);
          list.Add(WpRow.From(i, wp));
        }
        return list;
      });
      Replace(rows);
      Status = $"Read {rows.Count} waypoint(s).";
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
    if (rows.Count == 0) {
      Status = "No waypoints to write.";
      return;
    }
    Status = $"Writing {rows.Count} waypoint(s)…";
    try {
      await Task.Run(() => {
        WriteRadiusParams();
        _comPort.setWPTotal((ushort)rows.Count, MAVLink.MAV_MISSION_TYPE.MISSION);
        for (int i = 0; i < rows.Count; i++) {
          var loc = rows[i].ToLocationwp();
          _comPort.setWP(
              loc,
              (ushort)i,
              (MAVLink.MAV_FRAME)rows[i].Frame,
              (byte)(i == 0 ? 1 : 0)
          );
        }
        _comPort.setWPACK(MAVLink.MAV_MISSION_TYPE.MISSION);
      });
      Status = $"Wrote {rows.Count} waypoint(s).";
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

  // Append a waypoint at a map location (click-to-add) using the default altitude.
  public void AddWaypointAt(double lat, double lng) {
    AddCommandRow(MAVLink.MAV_CMD.WAYPOINT, lat, lng, DefaultAlt);
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

  [RelayCommand]
  private void DeleteWaypoint(WpRow? row) {
    if (row != null) {
      Waypoints.Remove(row);
      Renumber();
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
      PointLatLngAlt? last = (HomeLat == 0 && HomeLng == 0)
                                 ? null
                                 : new PointLatLngAlt(HomeLat, HomeLng, HomeAlt);
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
          double grad = distance > 0 ? height / distance : 0;
          w.Grad = (grad * 100).ToString("0.0", CultureInfo.InvariantCulture);
          w.Angle = ((180.0 / Math.PI) * Math.Atan(grad)).ToString("0.0",
              CultureInfo.InvariantCulture);
          w.Dist = Math.Sqrt(distance * distance + height * height)
                       .ToString("0.0", CultureInfo.InvariantCulture);
          w.Az = ((cur.GetBearing(last) + 180) % 360).ToString("0", CultureInfo.InvariantCulture);
        }

        last = cur;
      }
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

  partial void OnLatChanged(double value) => RecomputeCoords();

  partial void OnLngChanged(double value) => RecomputeCoords();

  private void RecomputeCoords() {
    if (Lat == 0 && Lng == 0) {
      Zone = Easting = Northing = Mgrs = "";
      return;
    }
    try {
      var (zone, e, n) = Services.Geo.ToUtm(Lat, Lng);
      Zone = zone.ToString();
      Easting = e.ToString("0.0", CultureInfo.InvariantCulture);
      Northing = n.ToString("0.0", CultureInfo.InvariantCulture);
      Mgrs = Services.Geo.ToMgrs(Lat, Lng);
    } catch {
      // leave previous values on conversion failure (polar / edge cases)
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
