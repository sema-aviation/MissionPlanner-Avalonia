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

  private void OnNodeMenu(object? sender, RoutedEventArgs e) {
    if (sender is not Button btn || btn.Tag is not DroneCanNode node ||
        DataContext is not ConfigDroneCanViewModel vm) {
      return;
    }

    vm.SelectNodeCommand.Execute(node);

    var menu = new ContextMenu();
    menu.Items.Add(NodeMenuItem("Parameters", vm.GetParametersCommand));
    menu.Items.Add(NodeMenuItem("Restart", vm.RestartNodeCommand));
    menu.Items.Add(NodeMenuItem("Write", vm.WriteParametersCommand));
    menu.Items.Add(NodeMenuItem("Save to Flash", vm.SaveConfigCommand));
    menu.Items.Add(NodeMenuItem("Erase", vm.EraseConfigCommand));
    menu.Open(btn);
  }

  private static MenuItem NodeMenuItem(string header, System.Windows.Input.ICommand command) {
    var item = new MenuItem { Header = header };
    item.Click += (_, _) => {
      if (command.CanExecute(null)) {
        command.Execute(null);
      }
    };
    return item;
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
