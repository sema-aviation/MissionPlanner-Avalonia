namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigAdvancedViewModel : ActionPageViewModel {
  public ConfigAdvancedViewModel() {
    Title = "Advanced";
    Instructions = "The following tools are for advanced configuration only — use with caution.";
    foreach (var (name, desc) in Tools) {
      var d = desc;
      Action(name, () => AppendLog($"{name}: {d} (port pending)"));
    }
  }

  private static readonly (string, string)[] Tools =
  {
        ("Warning Manager", "Enable custom warnings based on a set of conditions"),
        ("MAVLink Inspector", "View decoded mavlink data being sent and received"),
        ("Proximity", "View the data from a 360 lidar"),
        ("Mavlink Signing", "Enable mavlink signing to secure communication"),
        ("Mavlink Mirror", "Mavlink mirror to an external location"),
        ("NMEA", "Output the MAV location as a NMEA string"),
        ("Follow Me", "Use an external NMEA gps to send guided waypoints"),
        ("Param gen", "Regenerate the param info used inside MP"),
        ("Moving Base", "Show an extra icon on the map of your current location"),
        ("Anon Log", "Scramble lat/lng in bin or tlog"),
        ("FFT", "Plot an FFT from a log"),
        ("Spectrogram", "Plot a spectrogram from a log"),
        ("Support Proxy", "Share connection with support engineer"),
    };
}
