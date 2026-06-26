using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigDroneCanView : UserControl {
  public ConfigDroneCanView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("FirmwareUpdateBtn")!.Click += OnFirmwareUpdate;
    this.FindControl<Button>("BusInspectorBtn")!.Click +=
        (_, _) => MissionPlannerAvalonia.Views.DroneCANInspectorWindow.OpenWindow();
  }

  private async void OnFirmwareUpdate(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not ConfigDroneCanViewModel vm) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select node firmware",
      AllowMultiple = false,
      FileTypeFilter = new[] {
        new FilePickerFileType("DroneCAN firmware") { Patterns = new[] { "*.bin", "*.apj" } },
        new FilePickerFileType("All files") { Patterns = new[] { "*" } },
      },
    });

    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      await vm.UpdateFirmwareAsync(path);
    }
  }
}
