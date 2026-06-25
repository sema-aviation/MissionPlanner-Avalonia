using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.Setup;

namespace MissionPlannerAvalonia.Views.Setup;

public partial class InstallFirmwareView : UserControl {
  public InstallFirmwareView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("CustomFwBtn")!.Click += OnLoadCustomFirmware;
  }

  private async void OnLoadCustomFirmware(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not InstallFirmwareViewModel vm) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Load custom firmware",
          AllowMultiple = false,
          FileTypeFilter = new[]
            {
                    new FilePickerFileType("ArduPilot firmware") { Patterns = new[] { "*.apj", "*.px4" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        }
    );

    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      await vm.FlashCustomFirmwareAsync(path);
    }
  }
}
