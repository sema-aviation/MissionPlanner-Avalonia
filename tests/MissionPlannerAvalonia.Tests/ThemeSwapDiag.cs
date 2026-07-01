using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MissionPlannerAvalonia.Services;
using Xunit;
using Xunit.Abstractions;

namespace MissionPlannerAvalonia.Tests;

public class ThemeSwapDiag {
  private readonly ITestOutputHelper _o;
  public ThemeSwapDiag(ITestOutputHelper o) => _o = o;

  private static Color? Accent() {
    Application.Current!.Resources.TryGetResource("MpAccentBrush", null, out var v);
    return (v as ISolidColorBrush)?.Color;
  }

  [AvaloniaFact]
  public void SwapChangesAccent() {
    var mdCount = Application.Current!.Resources.MergedDictionaries.Count;
    ThemeService.Apply("Classic");
    var classic = Accent();
    ThemeService.Apply("Emerald");
    var emerald = Accent();
    _o.WriteLine($"mdCount={mdCount} classic={classic} emerald={emerald}");
    Assert.NotEqual(classic, emerald);
  }
}
