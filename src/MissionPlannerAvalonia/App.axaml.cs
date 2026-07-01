using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MissionPlannerAvalonia.ViewModels;
using MissionPlannerAvalonia.Views;

namespace MissionPlannerAvalonia;

public partial class App : Application {
  public override void Initialize() {
    AvaloniaXamlLoader.Load(this);
  }

  public override void OnFrameworkInitializationCompleted() {
    Services.ThemeService.ApplySaved();

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
      desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };

      desktop.Exit += (_, _) => Services.SitlLauncher.StopAll();

      _ = Services.Updater.CheckOnStartupAsync();
    }

    base.OnFrameworkInitializationCompleted();
  }
}
