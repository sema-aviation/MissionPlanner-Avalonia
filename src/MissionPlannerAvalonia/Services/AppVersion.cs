using System;
using System.Reflection;

namespace MissionPlannerAvalonia.Services;

// Single source for the app version string at runtime, parsed from the build-embedded
// AssemblyInformationalVersion ("2026.6.2+<githash>"). Used by the window title and Help.
public static class AppVersion {
  // e.g. "2026.6.2"
  public static string Number => ParseNumber(Info());

  // short git hash (7 chars) from the "+<hash>" suffix, or "" if absent.
  public static string Hash => ParseHash(Info());

  // window title, e.g. "Mission Planner 2026.6.2"
  public static string Title => "Mission Planner " + Number;

  // e.g. "2026.6.2 (f7427a5)"
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
