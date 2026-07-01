using System;
using System.IO;
using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace MissionPlannerAvalonia;

sealed class Program {
  [STAThread]
  public static void Main(string[] args) {

    AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject as Exception);

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

    }
  }

  public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()
          .LogToTrace();
}
