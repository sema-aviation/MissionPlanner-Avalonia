using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigCubeIDView : UserControl {
  [Obsolete]
  public ConfigCubeIDView() {
    InitializeComponent();
    this.FindControl<Button>("CustomBtn")!.Click += OnCustom;
  }

  private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

  [Obsolete]
  private async void OnCustom(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not ConfigCubeIDViewModel vm) {
      return;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select firmware",
      AllowMultiple = false,
      FileTypeFilter = new[] { new FilePickerFileType("Firmware") { Patterns = new[] { "*.bin" } } },
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      vm.UploadCustom(path);
    }
  }
}
