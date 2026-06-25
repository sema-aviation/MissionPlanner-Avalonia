using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class FlightPlannerView : UserControl {
  public FlightPlannerView() => InitializeComponent();

  private FlightPlannerViewModel? Vm => DataContext as FlightPlannerViewModel;

  private static readonly FilePickerFileType WpType = new("Waypoints") {
    Patterns = new[] { "*.waypoints", "*.txt" },
  };

  private async void OnLoadFile(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Load Mission",
          AllowMultiple = false,
          FileTypeFilter = new[] { WpType },
        }
    );
    var file = files.FirstOrDefault();
    if (file?.TryGetLocalPath() is { } path) {
      await Vm.LoadFileAsync(path);
    }
  }

  private async void OnSaveFile(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }

    var file = await top.StorageProvider.SaveFilePickerAsync(
        new FilePickerSaveOptions {
          Title = "Save Mission",
          DefaultExtension = "waypoints",
          SuggestedFileName = "mission.waypoints",
          FileTypeChoices = new[] { WpType },
        }
    );
    if (file?.TryGetLocalPath() is { } path) {
      await Vm.SaveFileAsync(path);
    }
  }
}
