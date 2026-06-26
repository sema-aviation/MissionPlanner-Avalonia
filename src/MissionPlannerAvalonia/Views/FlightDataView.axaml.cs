using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class FlightDataView : UserControl {
  public FlightDataView() {
    InitializeComponent();
    var hud = this.FindControl<HudControl>("Hud");
    if (hud != null) {
      hud.IndicatorClicked += OnHudIndicatorClicked;
    }
    _fdMap = this.FindControl<MapView>("FdMap");
    if (_fdMap != null) {
      _fdMap.ContextMenu = BuildMapMenu(_fdMap);
      BindMap();
      DataContextChanged += (_, _) => BindMap();
    }
    ApplyGaugeSettings();
  }

  // Gauge x:Name -> Settings key prefix; reload persisted Min/Max and add range bands.
  private static readonly Dictionary<string, string> GaugeKeys = new() {
    ["GVsi"] = "GaugeVSI",
    ["GSpeed"] = "GaugeSpeed",
    ["GAlt"] = "GaugeAlt",
  };

  private void ApplyGaugeSettings() {
    foreach (var (name, key) in GaugeKeys) {
      var g = this.FindControl<Gauge>(name);
      if (g == null) {
        continue;
      }
      if (TryGetDouble(key + "MIN", out var mn)) {
        g.Min = mn;
      }
      if (TryGetDouble(key + "MAX", out var mx)) {
        g.Max = mx;
      }
      // green (lower) / red (upper) bands as a quick visual cue (mirrors AGauge ranges).
      double span = g.Max - g.Min;
      g.Ranges = new List<GaugeRange> {
        new() { Start = g.Min, End = g.Min + span * 0.75,
                Color = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.FromArgb(120, 0, 200, 0)) },
        new() { Start = g.Min + span * 0.75, End = g.Max,
                Color = new Avalonia.Media.SolidColorBrush(
                    Avalonia.Media.Color.FromArgb(150, 220, 40, 40)) },
      };
    }
  }

  private static bool TryGetDouble(string key, out double value) {
    value = 0;
    return Settings.Instance.ContainsKey(key)
        && double.TryParse(Settings.Instance[key], NumberStyles.Any, CultureInfo.InvariantCulture,
            out value);
  }

  // Double-click a gauge -> prompt Min/Max and persist (mirrors Gspeed_DoubleClick).
  private async void OnGaugeDoubleTapped(object? sender, TappedEventArgs e) {
    if (sender is not Gauge g || g.Name == null || !GaugeKeys.TryGetValue(g.Name, out var key)) {
      return;
    }
    var minStr = await Services.Dialogs.InputBox("Set Min", "Enter Min value",
        g.Min.ToString(CultureInfo.InvariantCulture));
    if (minStr != null
        && double.TryParse(minStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var mn)) {
      g.Min = mn;
      Settings.Instance[key + "MIN"] = mn.ToString(CultureInfo.InvariantCulture);
    }
    var maxStr = await Services.Dialogs.InputBox("Set Max", "Enter Max value",
        g.Max.ToString(CultureInfo.InvariantCulture));
    if (maxStr != null
        && double.TryParse(maxStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var mx)) {
      g.Max = mx;
      Settings.Instance[key + "MAX"] = mx.ToString(CultureInfo.InvariantCulture);
    }
    ApplyGaugeSettings();
  }

  // Double-click a QuickView cell -> field picker over numeric cs properties (mirrors
  // quickView_DoubleClick). The chosen field is applied + persisted by the view model.
  private async void OnQuickViewDoubleTapped(object? sender, TappedEventArgs e) {
    if (DataContext is not FlightDataViewModel vm) {
      return;
    }
    // resolve which QuickItem cell was double-clicked from the event source's data context.
    QuickItem? item = (e.Source as Control)?.DataContext as QuickItem;
    if (item == null) {
      return;
    }
    var owner = TopLevel.GetTopLevel(this) as Window;
    if (owner == null) {
      return;
    }
    var fields = vm.QuickFieldList();
    var list = new ListBox {
      ItemsSource = fields.ConvertAll(f => f.desc),
      Height = 480,
      MinWidth = 360,
    };
    int cur = fields.FindIndex(f => f.name == item.Field);
    if (cur >= 0) {
      list.SelectedIndex = cur;
    }
    var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
    var dlg = new Window {
      Title = "Display This",
      Width = 420,
      Height = 560,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel {
        Margin = new Avalonia.Thickness(10),
        Spacing = 8,
        Children = { new ScrollViewer { Content = list, Height = 480 }, ok },
      },
    };
    ok.Click += (_, _) => dlg.Close(true);
    list.DoubleTapped += (_, _) => dlg.Close(true);
    if (await dlg.ShowDialog<bool>(owner) && list.SelectedIndex >= 0) {
      vm.SetQuickField(item, fields[list.SelectedIndex].name);
    }
  }

  private MapView? _fdMap;
  private FlightDataViewModel? _mapVm;

  // Push AutoPan + Clear-Track from the VM onto the MapView (mirrors MP CHK_autopan / Clear Track).
  private void BindMap() {
    if (_fdMap == null || ReferenceEquals(_mapVm, DataContext)) {
      return;
    }
    if (_mapVm != null) {
      _mapVm.TrackClearRequested -= _fdMap.ClearTrack;
      _mapVm.PropertyChanged -= OnMapVmChanged;
    }
    _mapVm = DataContext as FlightDataViewModel;
    if (_mapVm != null) {
      _fdMap.AutoPan = _mapVm.AutoPan;
      _mapVm.TrackClearRequested += _fdMap.ClearTrack;
      _mapVm.PropertyChanged += OnMapVmChanged;
    }
  }

  private void OnMapVmChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e) {
    if (_fdMap != null && _mapVm != null
        && e.PropertyName == nameof(FlightDataViewModel.AutoPan)) {
      _fdMap.AutoPan = _mapVm.AutoPan;
    }
  }

  // Mirrors MP hud1_ekfclick / hud1_vibeclick / hud1_prearmclick.
  private void OnHudIndicatorClicked(string which) {
    var owner = TopLevel.GetTopLevel(this) as Window;
    Window win = which switch {
      "ekf" => new EKFStatusWindow(),
      "vibe" => new VibrationWindow(),
      _ => new PrearmStatusWindow(),
    };
    if (owner != null) {
      win.Show(owner);
    } else {
      win.Show();
    }
  }

  // Mirrors FlightData contextMenuStripMap. Each item acts at the clicked map location.
  [Obsolete]
  private ContextMenu BuildMapMenu(MapView map) {
    FlightDataViewModel? Vm() => DataContext as FlightDataViewModel;
    MenuItem Item(string header, Func<FlightDataViewModel, Task> action) {
      var mi = new MenuItem { Header = header };
      mi.Click += async (_, _) => {
        var vm = Vm();
        if (vm != null) {
          await action(vm);
        }
      };
      return mi;
    }
    var menu = new ContextMenu();
    menu.Items.Add(Item("Fly To Here", vm => vm.FlyToHere(map.LastClickLatLng.Lat, map.LastClickLatLng.Lng)));
    menu.Items.Add(Item("Fly To Coords", vm => vm.FlyToCoords()));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Point Camera Here", vm => vm.PointCameraHere(map.LastClickLatLng.Lat, map.LastClickLatLng.Lng)));
    menu.Items.Add(Item("Trigger Camera NOW", vm => vm.TriggerCameraNow()));
    menu.Items.Add(new Separator());
    menu.Items.Add(Item("Set Home Here", vm => vm.SetHomeHere(map.LastClickLatLng.Lat, map.LastClickLatLng.Lng)));
    menu.Items.Add(Item("Set EKF Origin Here", vm => vm.SetEkfOriginHere(map.LastClickLatLng.Lat, map.LastClickLatLng.Lng)));
    menu.Items.Add(Item("TakeOff", vm => vm.TakeOffHere()));
    menu.Items.Add(Item("Jump To Tag", vm => vm.JumpToTag()));
    return menu;
  }
}
