using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigSecureApView : UserControl {
  public ConfigSecureApView() {
    InitializeComponent();
    this.FindControl<Button>("GenerateKeyBtn")!.Click += OnGenerateKey;
    this.FindControl<Button>("LoadKeyBtn")!.Click += OnLoadKey;
    this.FindControl<Button>("SignBootloaderBtn")!.Click += OnSignBootloader;
    this.FindControl<Button>("SignFirmwareBtn")!.Click += OnSignFirmware;
  }

  private void InitializeComponent() {
    AvaloniaXamlLoader.Load(this);
  }

  private async void OnGenerateKey(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not ConfigSecureApViewModel vm) {
      return;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save private key",
      DefaultExtension = "pem",
      SuggestedFileName = "private_key.pem",
      FileTypeChoices = new[] { new FilePickerFileType("PEM key") { Patterns = new[] { "*.pem" } } },
    });
    var path = file?.TryGetLocalPath();
    if (path != null) {
      vm.GenerateKey(path);
    }
  }

  private async void OnLoadKey(object? sender, RoutedEventArgs e) {
    var path = await PickFileAsync("Open private key",
        new FilePickerFileType("Private key") { Patterns = new[] { "*.pem", "*.dat" } });
    if (path != null && DataContext is ConfigSecureApViewModel vm) {
      vm.LoadPrivateKey(path);
    }
  }

  private async void OnSignBootloader(object? sender, RoutedEventArgs e) {
    var path = await PickFileAsync("Open bootloader",
        new FilePickerFileType("Bootloader") { Patterns = new[] { "*.bin" } });
    if (path != null && DataContext is ConfigSecureApViewModel vm) {
      vm.SignBootloader(path);
    }
  }

  private async void OnSignFirmware(object? sender, RoutedEventArgs e) {
    var path = await PickFileAsync("Open firmware",
        new FilePickerFileType("Firmware") { Patterns = new[] { "*.apj" } });
    if (path != null && DataContext is ConfigSecureApViewModel vm) {
      vm.SignFirmware(path);
    }
  }

  private async Task<string?> PickFileAsync(string title, FilePickerFileType filter) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return null;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = title,
      AllowMultiple = false,
      FileTypeFilter = new[] { filter, new FilePickerFileType("All files") { Patterns = new[] { "*" } } },
    });
    return files.FirstOrDefault()?.TryGetLocalPath();
  }
}
