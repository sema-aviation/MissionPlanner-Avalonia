using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// ponytail: unportable controls (DirectShow video, joystick setup, audio vario, theme editor, Windows map-cache dir, spoken-phrase InputBox prompts) are marked inline below.
public partial class ConfigPlannerViewModel : ViewModelBase {
  private const string _defaultMapIconDesc =
      "{alt}{altunit} {airspeed}{speedunit} id:{sysid} Sats:{satcount} HDOP:{gpshdop} Volts:{battery_voltage}";

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _loading;

  public ObservableCollection<string> DistUnitsOptions { get; } = new() { "Meters", "Feet" };
  public ObservableCollection<string> SpeedUnitsOptions { get; } =
      new() { "meters_per_second", "fps", "kph", "mph", "knots" };
  public ObservableCollection<string> ThemeOptions { get; } =
      new(MissionPlannerAvalonia.Services.ThemeService.Names);
  public ObservableCollection<string> LayoutOptions { get; } = new() { "Basic", "Advanced", "Custom" };
  public ObservableCollection<string> LanguageOptions { get; } =
      new() { "English (United States)", "System" };
  public ObservableCollection<string> SpeechOptions { get; } = new() { "Warning", "Critical", "All" };

  public ObservableCollection<string> SeverityOptions { get; } = new() {
    "Emergency", "Alert", "Critical", "Error", "Warning", "Notice", "Info", "Debug"
  };

  public ObservableCollection<string> MapCacheOptions { get; } =
      new() { "ServerOnly", "ServerAndCache", "CacheOnly" };

  public ObservableCollection<string> SecondaryDisplayStyleOptions { get; } =
      new() { "Normal", "Transparent", "Hidden" };

  // ponytail: upstream's CMB_osdcolor_SelectedIndexChanged body is fully commented out; this only persists "hudcolor" and applies nothing live.
  public ObservableCollection<string> OsdColorOptions { get; } = new() {
    "White", "Black", "Red", "Green", "Blue", "Yellow", "Orange", "Cyan",
    "Magenta", "Gray", "LightGray", "Lime", "Pink", "Purple"
  };

  [ObservableProperty]
  private string _distUnits = "Meters";

  [ObservableProperty]
  private string _altUnits = "Meters";

  [ObservableProperty]
  private string _speedUnits = "meters_per_second";

  [ObservableProperty]
  private string _theme = "Emerald";

  [ObservableProperty]
  private string _layout = "Advanced";

  [ObservableProperty]
  private string _language = "English (United States)";

  [ObservableProperty]
  private string _languageNote = "";

  [ObservableProperty]
  private string _speechLevel = "Warning";

  [ObservableProperty]
  private string _severity = "Warning";

  [ObservableProperty]
  private string _mapCache = "ServerAndCache";

  [ObservableProperty]
  private string _secondaryDisplayStyle = "Normal";

  [ObservableProperty]
  private string _osdColor = "White";

  [ObservableProperty]
  private string _logDir = "";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(SpeechSubOptionsVisible))]
  private bool _enableSpeech;

  public bool SpeechSubOptionsVisible => EnableSpeech;

  [ObservableProperty]
  private bool _speechArmedOnly;

  [ObservableProperty]
  private bool _speechWaypoint;

  [ObservableProperty]
  private bool _speechMode;

  [ObservableProperty]
  private bool _speechCustom;

  [ObservableProperty]
  private bool _speechBattery;

  [ObservableProperty]
  private bool _speechAltWarning;

  [ObservableProperty]
  private bool _speechArmDisarm;

  [ObservableProperty]
  private bool _speechLowSpeed;

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
  private bool _rtsResetEsp32;

  [ObservableProperty]
  private bool _displayCog = true;

  [ObservableProperty]
  private bool _displayHeading = true;

  [ObservableProperty]
  private bool _displayNavBearing = true;

  [ObservableProperty]
  private bool _displayRadius = true;

  [ObservableProperty]
  private bool _displayTarget = true;

  [ObservableProperty]
  private bool _displayTooltip;

  [ObservableProperty]
  private bool _betaUpdates;

  [ObservableProperty]
  private bool _passwordProtect;

  [ObservableProperty]
  private bool _showAirports;

  [ObservableProperty]
  private bool _enableAdsb;

  [ObservableProperty]
  private bool _noRcReceiver;

  [ObservableProperty]
  private bool _showTfr;

  [ObservableProperty]
  private bool _autoParamCommit;

  [ObservableProperty]
  private bool _showNoFly;

  [ObservableProperty]
  private bool _paramsBg;

  [ObservableProperty]
  private bool _slowMachine;

  [ObservableProperty]
  private bool _gdiPlus;

  [ObservableProperty]
  private bool _analyticsOptOut;

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
  private int _lineLength = 500;

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
    Theme = MissionPlannerAvalonia.Services.ThemeService.Current;
    Layout = s["displayview"] ?? Layout;
    Language = s["language"] ?? Language;
    SpeechLevel = s["speechlevel"] ?? SpeechLevel;

    int sev = s.GetInt32("severity", 4);
    if (sev >= 0 && sev < SeverityOptions.Count) {
      Severity = SeverityOptions[sev];
    }

    MapCache = s["mapCache"] ?? MapCache;
    SecondaryDisplayStyle = s.GetString("GMapMarkerBase_InactiveDisplayStyle", SecondaryDisplayStyle);
    OsdColor = s["hudcolor"] ?? OsdColor;
    LogDir = s.LogDir;

    EnableSpeech = s.GetBoolean("speechenable", EnableSpeech);
    SpeechArmedOnly = s.GetBoolean("speech_armed_only", SpeechArmedOnly);
    SpeechWaypoint = s.GetBoolean("speechwaypointenabled", SpeechWaypoint);
    SpeechMode = s.GetBoolean("speechmodeenabled", SpeechMode);
    SpeechCustom = s.GetBoolean("speechcustomenabled", SpeechCustom);
    SpeechBattery = s.GetBoolean("speechbatteryenabled", SpeechBattery);
    SpeechAltWarning = s.GetBoolean("speechaltenabled", SpeechAltWarning);
    SpeechArmDisarm = s.GetBoolean("speecharmenabled", SpeechArmDisarm);
    SpeechLowSpeed = s.GetBoolean("speechlowspeedenabled", SpeechLowSpeed);

    EnableHudOverlay = s.GetBoolean("CHK_hudshow", EnableHudOverlay);
    LoadWaypointsOnConnect = s.GetBoolean("loadwpsonconnect", LoadWaypointsOnConnect);
    DisplayInFlightData = s.GetBoolean("CHK_disttohomeflightdata", DisplayInFlightData);
    MapFollowPlane = s.GetBoolean("CHK_maprotation", MapFollowPlane);
    ResetOnUsbConnect = s.GetBoolean("CHK_resetapmonconnect", ResetOnUsbConnect);
    RtsResetEsp32 = s.GetBoolean("CHK_rtsresetesp32", RtsResetEsp32);

    DisplayCog = s.GetBoolean("GMapMarkerBase_DisplayCOG", DisplayCog);
    DisplayHeading = s.GetBoolean("GMapMarkerBase_DisplayHeading", DisplayHeading);
    DisplayNavBearing = s.GetBoolean("GMapMarkerBase_DisplayNavBearing", DisplayNavBearing);
    DisplayRadius = s.GetBoolean("GMapMarkerBase_DisplayRadius", DisplayRadius);
    DisplayTarget = s.GetBoolean("GMapMarkerBase_DisplayTarget", DisplayTarget);
    DisplayTooltip = s.GetString("mapicondesc", "") != "";

    BetaUpdates = s.GetBoolean("beta_updates", BetaUpdates);
    PasswordProtect = s.GetBoolean("password_protect", PasswordProtect);
    ShowAirports = s.GetBoolean("showairports", ShowAirports);
    EnableAdsb = s.GetBoolean("enableadsb", EnableAdsb);
    NoRcReceiver = s.GetBoolean("norcreceiver", NoRcReceiver);
    ShowTfr = s.GetBoolean("showtfr", ShowTfr);
    AutoParamCommit = s.GetBoolean("autoParamCommit", AutoParamCommit);
    ShowNoFly = s.GetBoolean("ShowNoFly", ShowNoFly);
    ParamsBg = s.GetBoolean("Params_BG", ParamsBg);
    SlowMachine = s.GetBoolean("SlowMachine", SlowMachine);
    GdiPlus = s.GetBoolean("CHK_GDIPlus", GdiPlus);
    AnalyticsOptOut = s.GetBoolean("analyticsoptout", AnalyticsOptOut);

    TelemAttitude = s.GetInt32("CMB_rateattitude", TelemAttitude);
    TelemPosition = s.GetInt32("CMB_rateposition", TelemPosition);
    TelemModeStatus = s.GetInt32("CMB_ratestatus", TelemModeStatus);
    TelemRc = s.GetInt32("CMB_raterc", TelemRc);
    TelemSensor = s.GetInt32("CMB_ratesensors", TelemSensor);

    TrackLength = s.GetInt32("NUM_tracklength", TrackLength);
    LineLength = s.GetInt32("GMapMarkerBase_Length", LineLength);
    GcsId = s.GetInt32("gcsid", MAVLinkInterface.gcssysid);

    _loading = false;
  }

  [RelayCommand]
  private void RerequestParams() {
    if (_comPort.BaseStream?.IsOpen != true) {
      return;
    }

    try {
      _comPort.getParamList();
    } catch {

    }
  }

  // ponytail: BUT_Joystick/themecustom/Vario/logdirbrowse/mapCacheDir + DirectShow video combos have no cross-platform equivalent wired up yet and are omitted.
  partial void OnDistUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["distunits"] = value;
    AppState.ApplyUnits();
  }

  partial void OnAltUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["altunits"] = value;
    AppState.ApplyUnits();
  }

  partial void OnSpeedUnitsChanged(string value) {
    if (_loading) return;
    Settings.Instance["speedunits"] = value;
    AppState.ApplyUnits();
  }

  partial void OnThemeChanged(string value) {
    if (_loading) return;
    MissionPlannerAvalonia.Services.ThemeService.Apply(value);
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

  partial void OnSeverityChanged(string value) {
    if (_loading) return;
    int idx = SeverityOptions.IndexOf(value);
    if (idx < 0) idx = 4;
    Settings.Instance["severity"] = idx.ToString();
  }

  partial void OnMapCacheChanged(string value) {
    if (_loading) return;
    Settings.Instance["mapCache"] = value;
  }

  partial void OnSecondaryDisplayStyleChanged(string value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_InactiveDisplayStyle"] = value;
  }

  partial void OnOsdColorChanged(string value) {
    if (_loading) return;
    Settings.Instance["hudcolor"] = value;
  }

  partial void OnLogDirChanged(string value) {
    if (_loading) return;
    if (!string.IsNullOrEmpty(value) && System.IO.Directory.Exists(value)) {
      Settings.Instance.LogDir = value;
    }
  }

  partial void OnEnableSpeechChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechenable"] = value.ToString();
  }

  partial void OnSpeechArmedOnlyChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speech_armed_only"] = value.ToString();
  }

  partial void OnSpeechWaypointChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechwaypointenabled"] = value.ToString();
  }

  partial void OnSpeechModeChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechmodeenabled"] = value.ToString();
  }

  partial void OnSpeechCustomChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechcustomenabled"] = value.ToString();
  }

  partial void OnSpeechBatteryChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechbatteryenabled"] = value.ToString();
  }

  partial void OnSpeechAltWarningChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechaltenabled"] = value.ToString();
  }

  partial void OnSpeechArmDisarmChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speecharmenabled"] = value.ToString();
  }

  partial void OnSpeechLowSpeedChanged(bool value) {
    if (_loading) return;
    Settings.Instance["speechlowspeedenabled"] = value.ToString();
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

    if (value) ShowNoFly = false;
  }

  partial void OnResetOnUsbConnectChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_resetapmonconnect"] = value.ToString();
  }

  partial void OnRtsResetEsp32Changed(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_rtsresetesp32"] = value.ToString();
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

  partial void OnDisplayRadiusChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayRadius"] = value.ToString();
  }

  partial void OnDisplayTargetChanged(bool value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_DisplayTarget"] = value.ToString();
  }

  partial void OnDisplayTooltipChanged(bool value) {
    if (_loading) return;
    Settings.Instance["mapicondesc"] = value ? _defaultMapIconDesc : "";
  }

  partial void OnBetaUpdatesChanged(bool value) {
    if (_loading) return;
    Settings.Instance["beta_updates"] = value.ToString();
  }

  partial void OnPasswordProtectChanged(bool value) {
    if (_loading) return;

    // ponytail: upstream also prompts for the password via InputBox; only the flag is stored here.
    Settings.Instance["password_protect"] = value.ToString();
  }

  partial void OnShowAirportsChanged(bool value) {
    if (_loading) return;
    Settings.Instance["showairports"] = value.ToString();
  }

  partial void OnEnableAdsbChanged(bool value) {
    if (_loading) return;

    // ponytail: upstream prompts for the ADSB server/port via InputBox; only the flag is stored here.
    Settings.Instance["enableadsb"] = value.ToString();
  }

  partial void OnNoRcReceiverChanged(bool value) {
    if (_loading) return;
    Settings.Instance["norcreceiver"] = value.ToString();
  }

  partial void OnShowTfrChanged(bool value) {
    if (_loading) return;
    Settings.Instance["showtfr"] = value.ToString();
  }

  partial void OnAutoParamCommitChanged(bool value) {
    if (_loading) return;
    Settings.Instance["autoParamCommit"] = value.ToString();
  }

  partial void OnShowNoFlyChanged(bool value) {
    if (_loading) return;
    Settings.Instance["ShowNoFly"] = value.ToString();

    if (value) MapFollowPlane = false;
  }

  partial void OnParamsBgChanged(bool value) {
    if (_loading) return;
    Settings.Instance["Params_BG"] = value.ToString();
  }

  partial void OnSlowMachineChanged(bool value) {
    if (_loading) return;
    Settings.Instance["SlowMachine"] = value.ToString();
  }

  partial void OnGdiPlusChanged(bool value) {
    if (_loading) return;
    Settings.Instance["CHK_GDIPlus"] = value.ToString();
  }

  partial void OnAnalyticsOptOutChanged(bool value) {
    if (_loading) return;
    Settings.Instance["analyticsoptout"] = value.ToString();
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

  partial void OnLineLengthChanged(int value) {
    if (_loading) return;
    Settings.Instance["GMapMarkerBase_Length"] = value.ToString();
  }

  partial void OnGcsIdChanged(int value) {
    if (_loading) return;
    MAVLinkInterface.gcssysid = (byte)value;
    Settings.Instance["gcsid"] = value.ToString();
  }
}
