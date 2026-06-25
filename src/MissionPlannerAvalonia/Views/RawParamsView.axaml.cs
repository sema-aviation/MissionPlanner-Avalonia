using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class RawParamsView : UserControl {
  private static readonly FilePickerFileType ParamFilter =
      new("Parameter File") { Patterns = new[] { "*.param", "*.parm" } };

  public RawParamsView() {
    InitializeComponent();
    this.FindControl<Button>("LoadBtn")!.Click += OnLoad;
    this.FindControl<Button>("SaveBtn")!.Click += OnSave;
    this.FindControl<Button>("CompareBtn")!.Click += OnCompare;
    this.FindControl<DataGrid>("ParamsGrid")!.CellEditEnded += (_, _) => Vm?.PersistFavs();
  }

  private RawParamsViewModel? Vm => DataContext as RawParamsViewModel;

  private async void OnLoad(object? sender, RoutedEventArgs e) {
    var path = await PickOpen("Load parameters");
    if (path != null) {
      Vm?.LoadParamFile(path);
    }
  }

  private async void OnCompare(object? sender, RoutedEventArgs e) {
    var path = await PickOpen("Compare parameters");
    if (path != null) {
      Vm?.CompareParamFile(path);
    }
  }

  private async void OnSave(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null || Vm is null) {
      return;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(
        new FilePickerSaveOptions {
          Title = "Save parameters",
          DefaultExtension = "param",
          FileTypeChoices = new[] { ParamFilter },
        }
    );
    var path = file?.TryGetLocalPath();
    if (path != null) {
      Vm.SaveParamFile(path);
    }
  }

  private async System.Threading.Tasks.Task<string?> PickOpen(string title) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return null;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = title,
          AllowMultiple = false,
          FileTypeFilter = new[] { ParamFilter, new FilePickerFileType("All files") { Patterns = new[] { "*" } } },
        }
    );
    return files.FirstOrDefault()?.TryGetLocalPath();
  }
}
