using Avalonia;
using Avalonia.Headless;
using MissionPlannerAvalonia;
using MissionPlannerAvalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace MissionPlannerAvalonia.Tests;

public static class TestAppBuilder {
  public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
