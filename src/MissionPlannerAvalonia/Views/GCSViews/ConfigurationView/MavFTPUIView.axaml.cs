using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class MavFTPUIView : UserControl {
  public MavFTPUIView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("DownloadBtn")!.Click += OnDownload;
    this.FindControl<Button>("UploadBtn")!.Click += OnUpload;
    this.FindControl<DataGrid>("EntriesGrid")!.DoubleTapped += OnEntryDoubleTapped;
  }

  private async void OnDownload(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not MavFTPUIViewModel vm) {
      return;
    }

    var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
      Title = "Select download folder",
      AllowMultiple = false,
    });

    var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    if (path != null) {
      await vm.DownloadAsync(path);
    }
  }

  private async void OnUpload(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || DataContext is not MavFTPUIViewModel vm) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select file to upload",
      AllowMultiple = false,
    });

    if (files.Count > 0) {
      var path = files[0].TryGetLocalPath();
      if (path != null) {
        await vm.UploadAsync(path);
      }
    }
  }

  private async void OnEntryDoubleTapped(object? sender, TappedEventArgs e) {
    if (DataContext is MavFTPUIViewModel vm) {
      await vm.OpenSelectedEntryAsync();
    }
  }
}
