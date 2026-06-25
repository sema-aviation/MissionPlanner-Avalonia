using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigPlannerViewModel : ViewModelBase {
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
}
