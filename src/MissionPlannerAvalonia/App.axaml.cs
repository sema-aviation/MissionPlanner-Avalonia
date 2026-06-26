using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;
using MissionPlannerAvalonia.Views;

namespace MissionPlannerAvalonia;

public partial class App : Application {
  public override void Initialize() {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted() {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
      // Kill any running SITL when the app exits (mirrors MP killing simulator procs on shutdown).
      desktop.Exit += (_, _) => Services.SitlLauncher.StopAll();
    }

    base.OnFrameworkInitializationCompleted();
  }
}
