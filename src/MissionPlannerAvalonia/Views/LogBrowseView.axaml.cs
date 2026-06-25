using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class LogBrowseView : UserControl {
  public LogBrowseView() {
    InitializeComponent();
    OpenBtn.Click += OnOpen;
  }

  private async void OnOpen(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Open log",
          AllowMultiple = false,
          FileTypeFilter = new[]
            {
                    new FilePickerFileType("Logs") { Patterns = new[] { "*.tlog", "*.bin", "*.log" } },
            },
        }
    );
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null && DataContext is LogBrowseViewModel vm) {
      vm.LoadFile(path);
    }
  }
}
