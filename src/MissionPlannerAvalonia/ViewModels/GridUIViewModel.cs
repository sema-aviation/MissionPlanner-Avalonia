using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class GridUIViewModel : ViewModelBase {
  private const double _rad2Deg = 180 / Math.PI;

  private readonly List<PointLatLngAlt> _polygon;
  private readonly PointLatLngAlt _home;

  private readonly Dictionary<string, CameraInfo> _cameras = new();
  private readonly bool _loading = true;
  private bool _suppressRecalc;

  private static readonly HashSet<string> _outputProps = new() {
    nameof(Status), nameof(AreaText), nameof(DistanceText), nameof(SpacingText),
    nameof(GrndResText), nameof(DistBetweenLinesText), nameof(FootprintText), nameof(TurnRadText),
    nameof(PhotoCount), nameof(StripCount), nameof(WaypointCount), nameof(FlightTimeText),
    nameof(PhotoEveryText), nameof(MinShutterText), nameof(FovH), nameof(FovV), nameof(CmPixel),
    nameof(Result),
  };

  public List<PointLatLngAlt> Result { get; private set; } = new();

  public event Action<List<PointLatLngAlt>>? GridAccepted;

  public event Action? CloseRequested;

  public ObservableCollection<string> Cameras { get; } = new();

  public ObservableCollection<string> StartPositions { get; } =
      new(Enum.GetNames(typeof(Grid.StartPosition)));

  [ObservableProperty]
  private double _altitude = 100;

  [ObservableProperty]
  private double _angle;

  [ObservableProperty]
  private double _flyingSpeed = 5;

  [ObservableProperty]
  private string _selectedCamera = "";

  [ObservableProperty]
  private bool _camDirection = true;

  [ObservableProperty]
  private double _distance = 50;

  [ObservableProperty]
  private double _spacing = 30;

  [ObservableProperty]
  private double _overshoot1;

  [ObservableProperty]
  private double _overshoot2;

  [ObservableProperty]
  private double _overlap = 50;

  [ObservableProperty]
  private double _sidelap = 60;

  [ObservableProperty]
  private double _leadin;

  [ObservableProperty]
  private double _leadin2;

  [ObservableProperty]
  private bool _crossGrid;

  [ObservableProperty]
  private bool _corridor;

  [ObservableProperty]
  private double _corridorWidth = 100;

  [ObservableProperty]
  private bool _spiral;

  [ObservableProperty]
  private string _startFrom = "Home";

  [ObservableProperty]
  private double _minLaneSeparation;

  [ObservableProperty]
  private int _clockwiseLaps = 1;

  [ObservableProperty]
  private int _laps = 1;

  [ObservableProperty]
  private bool _matchSpiralPerimeter;

  [ObservableProperty]
  private double _focalLength = 5;

  [ObservableProperty]
  private string _sensorWidth = "";

  [ObservableProperty]
  private string _sensorHeight = "";

  [ObservableProperty]
  private string _imageWidth = "";

  [ObservableProperty]
  private string _imageHeight = "";

  [ObservableProperty]
  private string _fovH = "";

  [ObservableProperty]
  private string _fovV = "";

  [ObservableProperty]
  private string _cmPixel = "";

  [ObservableProperty]
  private bool _showMarkers = true;

  [ObservableProperty]
  private bool _showFootprints;

  [ObservableProperty]
  private bool _showInternals;

  [ObservableProperty]
  private bool _showGrid = true;

  [ObservableProperty]
  private bool _showBoundary = true;

  [ObservableProperty]
  private string _areaText = "";

  [ObservableProperty]
  private string _distanceText = "";

  [ObservableProperty]
  private string _spacingText = "";

  [ObservableProperty]
  private string _grndResText = "";

  [ObservableProperty]
  private string _distBetweenLinesText = "";

  [ObservableProperty]
  private string _footprintText = "";

  [ObservableProperty]
  private string _turnRadText = "";

  [ObservableProperty]
  private int _photoCount;

  [ObservableProperty]
  private int _stripCount;

  [ObservableProperty]
  private int _waypointCount;

  [ObservableProperty]
  private string _flightTimeText = "";

  [ObservableProperty]
  private string _photoEveryText = "";

  [ObservableProperty]
  private string _minShutterText = "";

  [ObservableProperty]
  private string _status = "";

  public GridUIViewModel() : this(new List<PointLatLngAlt>(), PointLatLngAlt.Zero) {
  }

  public GridUIViewModel(List<PointLatLngAlt> polygon, PointLatLngAlt home) {
    _polygon = polygon ?? new List<PointLatLngAlt>();
    _home = home ?? PointLatLngAlt.Zero;

    LoadCameras();
    LoadSettings();

    if (Angle == 0) {
      Angle = (GetAngleOfLongestSide(_polygon) + 360) % 360;
    }

    _loading = false;
    Recalc();
  }

  protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
    base.OnPropertyChanged(e);
    if (_loading || _suppressRecalc || e.PropertyName == null) {
      return;
    }

    if (!_outputProps.Contains(e.PropertyName)) {
      if (e.PropertyName == nameof(SelectedCamera)) {
        ApplyCamera();
      }

      Recalc();
    }
  }

  [RelayCommand]
  private void Accept() {
    GridAccepted?.Invoke(new List<PointLatLngAlt>(Result));
    CloseRequested?.Invoke();
  }

  [RelayCommand]
  private void Close() => CloseRequested?.Invoke();

  private void ApplyCamera() {
    if (!_cameras.TryGetValue(SelectedCamera, out var cam)) {
      return;
    }

    _suppressRecalc = true;
    FocalLength = cam.FocalLen;
    ImageHeight = cam.ImageHeight.ToString(CultureInfo.InvariantCulture);
    ImageWidth = cam.ImageWidth.ToString(CultureInfo.InvariantCulture);
    SensorHeight = cam.SensorHeight.ToString(CultureInfo.InvariantCulture);
    SensorWidth = cam.SensorWidth.ToString(CultureInfo.InvariantCulture);
    _suppressRecalc = false;
  }

  private void DoCalc() {
    try {
      double flyalt = Altitude;
      int imagewidth = int.Parse(ImageWidth, CultureInfo.InvariantCulture);
      int imageheight = int.Parse(ImageHeight, CultureInfo.InvariantCulture);

      GetFov(flyalt, out double viewwidth, out double viewheight);

      _suppressRecalc = true;
      FovH = viewwidth.ToString("#.#", CultureInfo.InvariantCulture);
      FovV = viewheight.ToString("#.#", CultureInfo.InvariantCulture);
      CmPixel = (viewheight / imageheight * 100).ToString("0.00 cm", CultureInfo.InvariantCulture);

      if (CamDirection) {
        Spacing = (1 - Overlap / 100.0) * viewheight;
        Distance = (1 - Sidelap / 100.0) * viewwidth;
      } else {
        Spacing = (1 - Overlap / 100.0) * viewwidth;
        Distance = (1 - Sidelap / 100.0) * viewheight;
      }

      _suppressRecalc = false;
    } catch {
      _suppressRecalc = false;
    }
  }

  private void GetFov(double flyalt, out double fovh, out double fovv) {
    double focallen = FocalLength;
    double sensorwidth = double.Parse(SensorWidth, CultureInfo.InvariantCulture);
    double sensorheight = double.Parse(SensorHeight, CultureInfo.InvariantCulture);

    double flscale = 1000 * flyalt / focallen;
    fovh = sensorwidth * flscale / 1000;
    fovv = sensorheight * flscale / 1000;
  }

  private void Recalc() {
    if (_polygon.Count < 3) {
      Status = "Need at least 3 polygon points to generate a survey.";
      Result = new List<PointLatLngAlt>();
      WaypointCount = 0;
      return;
    }

    if (!string.IsNullOrEmpty(SelectedCamera) && !string.IsNullOrEmpty(SensorWidth)) {
      DoCalc();
    }

    var startpos = (Grid.StartPosition)Enum.Parse(typeof(Grid.StartPosition), StartFrom);

    List<PointLatLngAlt> grid;
    try {
      if (Corridor) {
        grid = Grid.CreateCorridor(_polygon, Altitude, Distance, Spacing, Angle, Overshoot1,
            Overshoot2, startpos, false, (float)MinLaneSeparation, CorridorWidth, (float)Leadin);
      } else if (Spiral) {
        grid = Grid.CreateRotary(_polygon, Altitude, Distance, Spacing, Angle, Overshoot1,
            Overshoot2, startpos, false, (float)MinLaneSeparation, (float)Leadin, _home,
            ClockwiseLaps, MatchSpiralPerimeter, Laps);
      } else {
        grid = Grid.CreateGrid(_polygon, Altitude, Distance, Spacing, Angle, Overshoot1, Overshoot2,
            startpos, false, (float)MinLaneSeparation, (float)Leadin, (float)Leadin2, _home);
      }

      if (grid.Count > 0 && CrossGrid) {
        Grid.StartPointLatLngAlt = grid[grid.Count - 1];
        grid.AddRange(Grid.CreateGrid(_polygon, Altitude, Distance, Spacing, Angle + 90.0,
            Overshoot1, Overshoot2, Grid.StartPosition.Point, false, (float)MinLaneSeparation,
            (float)Leadin, (float)Leadin2, _home));
      }
    } catch (Exception ex) {
      Status = "Grid generation failed: " + ex.Message;
      Result = new List<PointLatLngAlt>();
      WaypointCount = 0;
      return;
    }

    Result = grid;
    ComputeStats(grid);
  }

  private void ComputeStats(List<PointLatLngAlt> grid) {
    if (grid.Count == 0) {
      Status = "Grid produced no waypoints.";
      WaypointCount = 0;
      PhotoCount = 0;
      StripCount = 0;
      return;
    }

    int strips = 0;
    int images = 0;
    int waypoints = 0;

    double routetotal = grid.First().GetDistance(_home) / 1000.0 +
                        grid.Last().GetDistance(_home) / 1000.0;

    var prev = grid[0];
    foreach (var item in grid) {
      if (item.Tag == "M") {
        images++;
      } else {
        if (item.Tag != "SM" && item.Tag != "ME") {
          strips++;
        }

        waypoints++;
        routetotal += prev.GetDistance(item) / 1000.0;
        prev = item;
      }
    }

    double area = CalcPolygonArea(_polygon);

    double v = FlyingSpeed;
    double turnrad = v * v / (9.808 * Math.Tan(45 / _rad2Deg));

    AreaText = area.ToString("#", CultureInfo.InvariantCulture) + " m^2";
    DistanceText = routetotal.ToString("0.##", CultureInfo.InvariantCulture) + " km";
    SpacingText = Spacing.ToString("0.#", CultureInfo.InvariantCulture) + " m";
    GrndResText = CmPixel;
    DistBetweenLinesText = Distance.ToString("0.##", CultureInfo.InvariantCulture) + " m";
    FootprintText = FovH + " x " + FovV + " m";
    TurnRadText = (turnrad * 2).ToString("0", CultureInfo.InvariantCulture) + " m";

    double flyspeedms = FlyingSpeed <= 0 ? 1 : FlyingSpeed;
    PhotoCount = images;
    StripCount = strips / 2;
    WaypointCount = waypoints;

    double seconds = routetotal * 1000.0 / (flyspeedms * 0.8);
    FlightTimeText = SecondsToNice(seconds);
    PhotoEveryText = SecondsToNice(Spacing / flyspeedms);

    try {
      if (!string.IsNullOrEmpty(CmPixel)) {
        double cmpix = double.Parse(CmPixel.TrimEnd('c', 'm', ' '), CultureInfo.InvariantCulture);
        double minmpix = cmpix * 0.01 / 2.0;
        double minshutter = flyspeedms / minmpix;
        MinShutterText = "1/" + (minshutter - minshutter % 1).ToString(CultureInfo.InvariantCulture);
      }
    } catch {
    }

    Status = $"Generated {grid.Count} point(s): {waypoints} waypoints, {images} photos.";
  }

  private static double CalcPolygonArea(List<PointLatLngAlt> polygon) {
    if (polygon.Count < 3) {
      return 0;
    }

    double lat0 = polygon.Average(p => p.Lat);
    double mPerDegLat = 111319.9;
    double mPerDegLng = 111319.9 * Math.Cos(lat0 * Math.PI / 180.0);

    double sum = 0;
    for (int i = 0; i < polygon.Count; i++) {
      var a = polygon[i];
      var b = polygon[(i + 1) % polygon.Count];
      double ax = a.Lng * mPerDegLng;
      double ay = a.Lat * mPerDegLat;
      double bx = b.Lng * mPerDegLng;
      double by = b.Lat * mPerDegLat;
      sum += ax * by - bx * ay;
    }

    return Math.Abs(sum) / 2.0;
  }

  private static double GetAngleOfLongestSide(List<PointLatLngAlt> list) {
    if (list.Count == 0) {
      return 0;
    }

    double angle = 0;
    double maxdist = 0;
    var last = list[list.Count - 1];
    foreach (var item in list) {
      if (item.GetDistance(last) > maxdist) {
        angle = item.GetBearing(last);
        maxdist = item.GetDistance(last);
      }

      last = item;
    }

    return (angle + 360) % 360;
  }

  private static string SecondsToNice(double seconds) {
    if (seconds < 0) {
      return "Infinity Seconds";
    }

    double secs = seconds % 60;
    int mins = (int)(seconds / 60) % 60;
    int hours = (int)(seconds / 3600);

    if (hours > 0) {
      return hours + ":" + mins.ToString("00") + ":" + secs.ToString("00") + " Hours";
    }

    if (mins > 0) {
      return mins + ":" + secs.ToString("00") + " Minutes";
    }

    return secs.ToString("0.00") + " Seconds";
  }

  private void LoadCameras() {
    ReadCameraXml(Settings.GetRunningDirectory() + "camerasBuiltin.xml");
    ReadCameraXml(Settings.GetUserDataDirectory() + "cameras.xml");

    foreach (var name in _cameras.Keys.OrderBy(n => n)) {
      if (!Cameras.Contains(name)) {
        Cameras.Add(name);
      }
    }
  }

  private void ReadCameraXml(string filename) {
    if (!File.Exists(filename)) {
      return;
    }

    try {
      using var reader = new XmlTextReader(filename);
      var ci = new CultureInfo("en-US");
      while (reader.Read()) {
        reader.MoveToElement();
        if (reader.Name != "Camera") {
          continue;
        }

        var cam = new CameraInfo();
        while (reader.Read()) {
          reader.MoveToElement();
          bool stop = false;
          switch (reader.Name) {
            case "name":
              cam.Name = reader.ReadString();
              break;
            case "imgw":
              cam.ImageWidth = float.Parse(reader.ReadString(), ci);
              break;
            case "imgh":
              cam.ImageHeight = float.Parse(reader.ReadString(), ci);
              break;
            case "senw":
              cam.SensorWidth = float.Parse(reader.ReadString(), ci);
              break;
            case "senh":
              cam.SensorHeight = float.Parse(reader.ReadString(), ci);
              break;
            case "flen":
              cam.FocalLen = float.Parse(reader.ReadString(), ci);
              break;
            case "Camera":
              if (!string.IsNullOrEmpty(cam.Name)) {
                _cameras[cam.Name] = cam;
              }

              stop = true;
              break;
          }

          if (stop) {
            break;
          }
        }
      }
    } catch {

    }
  }

  public void SaveSettings() {
    Set("grid_alt", Altitude);
    Set("grid_angle", Angle);
    Set("grid_speed", FlyingSpeed);
    Set("grid_camera", SelectedCamera);
    Set("grid_camdir", CamDirection);
    Set("grid_dist", Distance);
    Set("grid_spacing", Spacing);
    Set("grid_overshoot1", Overshoot1);
    Set("grid_overshoot2", Overshoot2);
    Set("grid_overlap", Overlap);
    Set("grid_sidelap", Sidelap);
    Set("grid_leadin1", Leadin);
    Set("grid_leadin2", Leadin2);
    Set("grid_crossgrid", CrossGrid);
    Set("grid_spiral", Spiral);
    Set("grid_startfrom", StartFrom);
    Set("grid_min_lane_separation", MinLaneSeparation);
    Set("grid_internals", ShowInternals);
    Set("grid_footprints", ShowFootprints);
    Set("grid_clockwise_laps", ClockwiseLaps);
    Set("grid_laps", Laps);
    Set("grid_match_spiral_perimeter", MatchSpiralPerimeter);
  }

  private void LoadSettings() {
    Altitude = GetD("grid_alt", Altitude);
    Angle = GetD("grid_angle", Angle);
    FlyingSpeed = GetD("grid_speed", FlyingSpeed);
    CamDirection = GetB("grid_camdir", CamDirection);
    Distance = GetD("grid_dist", Distance);
    Spacing = GetD("grid_spacing", Spacing);
    Overshoot1 = GetD("grid_overshoot1", Overshoot1);
    Overshoot2 = GetD("grid_overshoot2", Overshoot2);
    Overlap = GetD("grid_overlap", Overlap);
    Sidelap = GetD("grid_sidelap", Sidelap);
    Leadin = GetD("grid_leadin1", Leadin);
    Leadin2 = GetD("grid_leadin2", Leadin2);
    CrossGrid = GetB("grid_crossgrid", CrossGrid);
    Spiral = GetB("grid_spiral", Spiral);
    StartFrom = GetS("grid_startfrom", StartFrom);
    MinLaneSeparation = GetD("grid_min_lane_separation", MinLaneSeparation);
    ShowInternals = GetB("grid_internals", ShowInternals);
    ShowFootprints = GetB("grid_footprints", ShowFootprints);
    ClockwiseLaps = (int)GetD("grid_clockwise_laps", ClockwiseLaps);
    Laps = (int)GetD("grid_laps", Laps);
    MatchSpiralPerimeter = GetB("grid_match_spiral_perimeter", MatchSpiralPerimeter);

    SelectedCamera = GetS("grid_camera", SelectedCamera);
  }

  private static void Set(string key, double v) =>
      Settings.Instance[key] = v.ToString(CultureInfo.InvariantCulture);

  private static void Set(string key, bool v) => Settings.Instance[key] = v.ToString();

  private static void Set(string key, string v) => Settings.Instance[key] = v;

  private static double GetD(string key, double fallback) =>
      Settings.Instance.ContainsKey(key) &&
              double.TryParse(Settings.Instance[key], NumberStyles.Any, CultureInfo.InvariantCulture,
                  out var v)
          ? v
          : fallback;

  private static bool GetB(string key, bool fallback) =>
      Settings.Instance.ContainsKey(key) && bool.TryParse(Settings.Instance[key], out var v)
          ? v
          : fallback;

  private static string GetS(string key, string fallback) =>
      Settings.Instance.ContainsKey(key) && Settings.Instance[key] != null ? Settings.Instance[key]
                                                                           : fallback;

  private sealed class CameraInfo {
    public string Name = "";
    public float FocalLen;
    public float SensorWidth;
    public float SensorHeight;
    public float ImageWidth;
    public float ImageHeight;
  }
}
