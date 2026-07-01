using System.Reflection;

namespace MissionPlannerAvalonia.Services;

public static class AppVersion {

  public static string Number => ParseNumber(Info());

  public static string Hash => ParseHash(Info());

  public static string Title => "Mission Planner " + Number;

  public static string Full => string.IsNullOrEmpty(Hash) ? Number : $"{Number} ({Hash})";

  private static string Info() =>
      Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
          ?.InformationalVersion
      ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
      ?? "";

  private static string ParseNumber(string s) {
    if (string.IsNullOrWhiteSpace(s)) {
      return "";
    }
    int plus = s.IndexOf('+');
    return (plus >= 0 ? s.Substring(0, plus) : s).Trim();
  }

  private static string ParseHash(string s) {
    int plus = s.IndexOf('+');
    if (plus < 0 || plus + 1 >= s.Length) {
      return "";
    }
    var h = s.Substring(plus + 1);
    return h.Length > 7 ? h.Substring(0, 7) : h;
  }
}
