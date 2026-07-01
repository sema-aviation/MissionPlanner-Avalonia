using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class DroneCANParamsView : UserControl {
  private static readonly FilePickerFileType _paramFilter =
      new("Parameter File") { Patterns = new[] { "*.param", "*.parm" } };

  public DroneCANParamsView() {
    AvaloniaXamlLoader.Load(this);
    this.FindControl<Button>("LoadBtn")!.Click += OnLoad;
    this.FindControl<Button>("SaveBtn")!.Click += OnSave;
  }

  private DroneCANParamsViewModel? Vm => DataContext as DroneCANParamsViewModel;

  private void OnFavClicked(object? sender, RoutedEventArgs e) {
    if (sender is CheckBox { DataContext: DroneCanParamRow row }) {
      Vm?.ToggleFav(row);
    }
  }

  private async void OnLoad(object? sender, RoutedEventArgs e) {
    var path = await PickOpen("Load parameters");
    if (path != null) {
      Vm?.LoadParamFile(path);
    }
  }

  private async void OnSave(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }

    var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save parameters",
      DefaultExtension = "param",
      FileTypeChoices = new[] { _paramFilter },
    });

    var path = file?.TryGetLocalPath();
    if (path != null) {
      Vm.SaveParamFile(path);
    }
  }

  private async Task<string?> PickOpen(string title) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return null;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = title,
      AllowMultiple = false,
      FileTypeFilter = new[] {
        _paramFilter, new FilePickerFileType("All files") { Patterns = new[] { "*" } },
      },
    });

    return files.FirstOrDefault()?.TryGetLocalPath();
  }
}
