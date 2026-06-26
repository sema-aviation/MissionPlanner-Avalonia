using System;
using System.IO;
using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace MissionPlannerAvalonia;

sealed class Program {
  [STAThread]
  public static void Main(string[] args) {
    // Central crash sink (mirrors MainV2.handleException): log unhandled exceptions to a file
    // under the app data dir instead of letting them vanish.
    AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);
    // Projektanker.Icons.Avalonia 9.x: register icon providers here (the old AppBuilder.WithIcons
    // extension was removed in v9).
    IconProvider.Current.Register<FontAwesomeIconProvider>();
    try {
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    } catch (Exception ex) {
      LogCrash(ex);
      throw;
    }
  }

  private static void LogCrash(Exception? ex) {
    if (ex == null) {
      return;
    }
    try {
      var dir = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MissionPlannerAvalonia");
      Directory.CreateDirectory(dir);
      File.AppendAllText(Path.Combine(dir, "crash.log"), $"---- crash ----\n{ex}\n\n");
    } catch {
      // last-resort sink: never throw from the crash handler
    }
  }

  public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .LogToTrace();
}
