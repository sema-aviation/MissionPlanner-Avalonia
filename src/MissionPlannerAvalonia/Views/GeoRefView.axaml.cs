using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class GeoRefView : UserControl {
  public GeoRefView() {
    InitializeComponent();
    BrowseLogBtn.Click += OnBrowseLog;
    BrowseDirBtn.Click += OnBrowseDir;
    GeoTagBtn.Click += OnGeoTag;
  }

  private GeoRefViewModel? Vm => DataContext as GeoRefViewModel;

  private async void OnBrowseLog(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select flight log",
      AllowMultiple = false,
      FileTypeFilter = new[] {
        new FilePickerFileType("Logs") { Patterns = new[] { "*.bin", "*.log", "*.tlog" } },
      },
    });
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      Vm.LogPath = path;

      if (string.IsNullOrEmpty(Vm.PhotoDir)) {
        Vm.PhotoDir = System.IO.Path.GetDirectoryName(path) ?? "";
      }
    }
  }

  private async void OnBrowseDir(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }
    var dirs = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
      Title = "Select photo folder",
      AllowMultiple = false,
    });
    var path = dirs.FirstOrDefault()?.TryGetLocalPath();
    if (path != null) {
      Vm.PhotoDir = path;
    }
  }

  private async void OnGeoTag(object? sender, RoutedEventArgs e) {
    if (Vm is { } vm) {
      await vm.GeoTagAsync();
    }
  }
}
