using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

public static class ThemeService {
  public static readonly string[] Names = { "Classic", "Emerald", "Lime Refined", "Deep Forest" };

  private static readonly Uri _base = new("avares://MissionPlannerAvalonia/App.axaml");
  private static ResourceInclude? _current;

  public static string Current { get; private set; } = "Classic";

  private static string PathFor(string name) => name switch {
    "Emerald" => "avares://MissionPlannerAvalonia/Theme/Palettes/Emerald.axaml",
    "Lime Refined" => "avares://MissionPlannerAvalonia/Theme/Palettes/LimeRefined.axaml",
    "Deep Forest" => "avares://MissionPlannerAvalonia/Theme/Palettes/DeepForest.axaml",
    _ => "avares://MissionPlannerAvalonia/Theme/Palettes/Classic.axaml",
  };

  public static void Apply(string name) {
    var app = Application.Current;
    if (app is null) {
      return;
    }

    if (!Names.Contains(name)) {
      name = "Classic";
    }

    var md = app.Resources.MergedDictionaries;
    if (_current != null) {
      md.Remove(_current);
    }

    _current = new ResourceInclude(_base) { Source = new Uri(PathFor(name)) };
    md.Add(_current);
    Current = name;
    Settings.Instance["colortheme"] = name;
  }

  public static void ApplySaved() {
    var saved = Settings.Instance["colortheme"];
    Apply(string.IsNullOrWhiteSpace(saved) ? "Emerald" : saved);
  }
}
