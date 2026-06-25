using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigPlannerViewModel : ViewModelBase {
  private const string DefaultMapIconDesc =
      "{alt}{altunit} {airspeed}{speedunit} id:{sysid} Sats:{satcount} HDOP:{gpshdop} Volts:{battery_voltage}";

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _loading;

  public ObservableCollection<string> DistUnitsOptions { get; } = new() { "Meters", "Feet" };
  public ObservableCollection<string> SpeedUnitsOptions { get; } =
      new() { "meters_per_second", "fps", "kph", "mph", "knots" };
  public ObservableCollection<string> ThemeOptions { get; } =
      new() { "BurntKermit.mpsystheme", "HighContrast", "None" };
  public ObservableCollection<string> LayoutOptions { get; } = new() { "Advanced", "Basic" };
  public ObservableCollection<string> LanguageOptions { get; } =
      new() { "English (United States)", "System" };
  public ObservableCollection<string> SpeechOptions { get; } = new() { "Warning", "Critical", "All" };

  [ObservableProperty]
  private string _distUnits = "Meters";

  [ObservableProperty]
  private string _altUnits = "Meters";

  [ObservableProperty]
  private string _speedUnits = "meters_per_second";

  [ObservableProperty]
  private string _theme = "BurntKermit.mpsystheme";

  [ObservableProperty]
  private string _layout = "Advanced";

  [ObservableProperty]
  private string _language = "English (United States)";

  [ObservableProperty]
  private string _languageNote = "";

  [ObservableProperty]
  private string _speechLevel = "Warning";

  [ObservableProperty]
  private bool _enableSpeech;

  [ObservableProperty]
  private bool _enableHudOverlay = true;

  [ObservableProperty]
  private bool _loadWaypointsOnConnect;

  [ObservableProperty]
  private bool _displayInFlightData = true;

  [ObservableProperty]
  private bool _mapFollowPlane;

  [ObservableProperty]
  private bool _resetOnUsbConnect;

  [ObservableProperty]
  private bool _displayCog = true;

  [ObservableProperty]
  private bool _displayHeading = true;

  [ObservableProperty]
  private bool _displayNavBearing = true;

  [ObservableProperty]
  private bool _displayTarget = true;

  [ObservableProperty]
  private bool _displayTooltip;

  [ObservableProperty]
  private int _telemAttitude = 4;

  [ObservableProperty]
  private int _telemPosition = 2;

  [ObservableProperty]
  private int _telemModeStatus = 2;

  [ObservableProperty]
  private int _telemRc = 2;

  [ObservableProperty]
  private int _telemSensor = 2;

  [ObservableProperty]
  private int _trackLength = 200;

  [ObservableProperty]
  private int _gcsId = 255;

  public ConfigPlannerViewModel() {
    Load();
  }

  private void Load() {
    _loading = true;
    var s = Settings.Instance;

    DistUnits = s["distunits"] ?? DistUnits;
    AltUnits = s["altunits"] ?? AltUnits;
    SpeedUnits = s["speedunits"] ?? SpeedUnits;
    Theme = s["theme"] ?? Theme;
    Layout = s["displayview"] ?? Layout;
    Language = s["language"] ?? Language;
    SpeechLevel = s["speechlevel"] ?? SpeechLevel;

    EnableSpeech = s.GetBoolean("speechenable", EnableSpeech);
    EnableHudOverlay = s.GetBoolean("CHK_hudshow", EnableHudOverlay);
    LoadWaypointsOnConnect = s.GetBoolean("loadwpsonconnect", LoadWaypointsOnConnect);
    DisplayInFlightData = s.GetBoolean("CHK_disttohomeflightdata", DisplayInFlightData);
    MapFollowPlane = s.GetBoolean("CHK_maprotation", MapFollowPlane);
    ResetOnUsbConnect = s.GetBoolean("CHK_resetapmonconnect", ResetOnUsbConnect);

    DisplayCog = s.GetBoolean("GMapMarkerBase_DisplayCOG", DisplayCog);
    DisplayHeading = s.GetBoolean("GMapMarkerBase_DisplayHeading", DisplayHeading);
    DisplayNavBearing = s.GetBoolean("GMapMarkerBase_DisplayNavBearing", DisplayNavBearing);
    DisplayTarget = s.GetBoolean("GMapMarkerBase_DisplayTarget", DisplayTarget);
    DisplayTooltip = s.GetString("mapicondesc", "") != "";

    TelemAttitude = s.GetInt32("CMB_rateattitude", TelemAttitude);
    TelemPosition = s.GetInt32("CMB_rateposition", TelemPosition);
    TelemModeStatus = s.GetInt32("CMB_ratestatus", TelemModeStatus);
    TelemRc = s.GetInt32("CMB_raterc", TelemRc);
    TelemSensor = s.GetInt32("CMB_ratesensors", TelemSensor);

    TrackLength = s.GetInt32("NUM_tracklength", TrackLength);
    GcsId = s.GetInt32("gcsid", MAVLinkInterface.gcssysid);

    _loading = false;
  }

  partial void OnDistUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["distunits"] = value;
  }

  partial void OnAltUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["altunits"] = value;
  }

  partial void OnSpeedUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["speedunits"] = value;
  }

  partial void OnThemeChanged(string value) {
    if (_loading) return;
    Settings.Instance["theme"] = value;
  }

  partial void OnLayoutChanged(string value) {
    if (_loading) return;
    Settings.Instance["displayview"] = value;
  }

  partial void OnLanguageChanged(string value) {
    if (_loading) return;
    Settings.Instance["language"] = value;
    LanguageNote = "Language change requires a restart to take effect.";
  }

  partial void OnSpeechLevelChanged(string value) {
    if (_loading) return;
    Settings.Instance["speechlevel"] = value;
  }

  // Speech enable flags are persisted as booleans; the upstream spoken-phrase
  // InputBox prompts (waypoint/battery/alt text and trigger thresholds) are omitted.
  partial void OnEnableSpeechChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechenable"] = value.ToString();
  }

  partial void OnEnableHudOverlayChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_hudshow"] = value.ToString();
  }

  partial void OnLoadWaypointsOnConnectChanged(bool value) {
    if (_loading) return;
    Settings.Instance["loadwpsonconnect"] = value.ToString();
  }

  partial void OnDisplayInFlightDataChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_disttohomeflightdata"] = value.ToString();
  }

  partial void OnMapFollowPlaneChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_maprotation"] = value.ToString();
  }

  partial void OnResetOnUsbConnectChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_resetapmonconnect"] = value.ToString();
  }

  partial void OnDisplayCogChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayCOG"] = value.ToString();
  }

  partial void OnDisplayHeadingChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayHeading"] = value.ToString();
  }

  partial void OnDisplayNavBearingChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayNavBearing"] = value.ToString();
  }

  partial void OnDisplayTargetChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayTarget"] = value.ToString();
  }

  partial void OnDisplayTooltipChanged(bool value) {
    if (_loading) return;
    Settings.Instance["mapicondesc"] = value ? DefaultMapIconDesc : "";
  }

  partial void OnTelemAttitudeChanged(int value) {
    if (_loading) return;
    Settings.Instance["CMB_rateattitude"] = value.ToString();
    _comPort.MAV.cs.rateattitude = value;
    CurrentState.rateattitudebackup = value;
    if (_comPort.BaseStream?.IsOpen == true) {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA1, value);
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA2, value);
    }
  }

  partial void OnTelemPositionChanged(int value) {
    if (_loading) return;
    Settings.Instance["CMB_rateposition"] = value.ToString();
    _comPort.MAV.cs.rateposition = value;
    CurrentState.ratepositionbackup = value;
    if (_comPort.BaseStream?.IsOpen == true) {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.POSITION, value);
    }
  }

  partial void OnTelemModeStatusChanged(int value) {
    if (_loading) return;
    Settings.Instance["CMB_ratestatus"] = value.ToString();
    _comPort.MAV.cs.ratestatus = value;
    CurrentState.ratestatusbackup = value;
    if (_comPort.BaseStream?.IsOpen == true) {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTENDED_STATUS, value);
    }
  }

  partial void OnTelemRcChanged(int value) {
    if (_loading) return;
    Settings.Instance["CMB_raterc"] = value.ToString();
    _comPort.MAV.cs.raterc = value;
    CurrentState.ratercbackup = value;
    if (_comPort.BaseStream?.IsOpen == true) {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, value);
    }
  }

  partial void OnTelemSensorChanged(int value) {
    if (_loading) return;
    Settings.Instance["CMB_ratesensors"] = value.ToString();
    _comPort.MAV.cs.ratesensors = value;
    CurrentState.ratesensorsbackup = value;
    if (_comPort.BaseStream?.IsOpen == true) {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.EXTRA3, value);
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RAW_SENSORS, value);
    }
  }

  partial void OnTrackLengthChanged(int value) {
    if (_loading) return;
    Settings.Instance["NUM_tracklength"] = value.ToString();
  }

  partial void OnGcsIdChanged(int value) {
    if (_loading) return;
    MAVLinkInterface.gcssysid = (byte)value;
    Settings.Instance["gcsid"] = value.ToString();
  }
}
