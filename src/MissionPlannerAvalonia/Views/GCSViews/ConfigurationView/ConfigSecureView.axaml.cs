using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigSecureView : UserControl {
  public ConfigSecureView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("SetKeyBtn")!.Click += OnSetKey;
  }

  private async void OnSetKey(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not ConfigSecureViewModel vm) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Open public key",
          AllowMultiple = false,
          FileTypeFilter = new[]
            {
                    new FilePickerFileType("Public keys") { Patterns = new[] { "*.dat", "*.pem", "*.pub" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        }
    );

    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      await vm.SetKeyFromFileAsync(path);
    }
  }
}
