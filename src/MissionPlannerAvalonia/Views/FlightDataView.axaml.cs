using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Controls;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class FlightDataView : UserControl {
  [Obsolete]
  public FlightDataView() {
    InitializeComponent();
    var hud = this.FindControl<HudControl>("Hud");
    if (hud != null) {
      hud.IndicatorClicked += OnHudIndicatorClicked;
    }
    _fdMap = this.FindControl<MapView>("FdMap");
    _tuningPlot = this.FindControl<LivePlot>("TuningPlot");
    if (_fdMap != null) {
      _fdMap.ContextMenu = BuildMapMenu(_fdMap);

      _fdMap.CursorMoved += (lat, lng) => {
        if (DataContext is FlightDataViewModel vm) {
          vm.CursorLat = lat;
          vm.CursorLng = lng;
        }
      };
      BindMap();
      DataContextChanged += (_, _) => BindMap();
    }
    _fdTabs = this.FindControl<TabControl>("FdTabs");
    if (_fdTabs != null) {
      _defaultTabPanel = _fdTabs.ItemsPanel;
      _fdTabs.ContextMenu = BuildTabMenu(_fdTabs);
      ApplyTabSettings(_fdTabs);
    }
    ApplyGaugeSettings();
  }

  private static readonly Dictionary<string, string> _gaugeKeys = new() {
    ["GVsi"] = "GaugeVSI",
    ["GSpeed"] = "GaugeSpeed",
    ["GAlt"] = "GaugeAlt",
  };

  private void ApplyGaugeSettings() {
    foreach (var (name, key) in _gaugeKeys) {
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

  private async void OnGaugeDoubleTapped(object? sender, TappedEventArgs e) {
    if (sender is not Gauge g || g.Name == null || !_gaugeKeys.TryGetValue(g.Name, out var key)) {
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

  private async void OnQuickViewDoubleTapped(object? sender, TappedEventArgs e) {
    if (DataContext is not FlightDataViewModel vm) {
      return;
    }

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

  private readonly MapView? _fdMap;
  private readonly LivePlot? _tuningPlot;
  private readonly TabControl? _fdTabs;
  private FlightDataViewModel? _mapVm;

  private void BindMap() {
    if (_fdMap == null || ReferenceEquals(_mapVm, DataContext)) {
      return;
    }
    if (_mapVm != null) {
      _mapVm.TrackClearRequested -= _fdMap.ClearTrack;
      _mapVm.PropertyChanged -= OnMapVmChanged;
      _mapVm.TuningSampled -= OnTuningSampled;
      _mapVm.TuningFieldsChanged -= OnTuningFieldsChanged;
    }
    _mapVm = DataContext as FlightDataViewModel;
    if (_mapVm != null) {
      _fdMap.AutoPan = _mapVm.AutoPan;
      _mapVm.TrackClearRequested += _fdMap.ClearTrack;
      _mapVm.PropertyChanged += OnMapVmChanged;
      _mapVm.TuningSampled += OnTuningSampled;
      _mapVm.TuningFieldsChanged += OnTuningFieldsChanged;
    }
  }

  private void OnMapVmChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e) {
    if (_fdMap != null && _mapVm != null
        && e.PropertyName == nameof(FlightDataViewModel.AutoPan)) {
      _fdMap.AutoPan = _mapVm.AutoPan;
    }
    if (e.PropertyName == nameof(FlightDataViewModel.Tuning) && _mapVm?.Tuning != true) {
      ResetTuningPlot();
    }
  }

  private readonly Dictionary<string, (List<double> Xs, List<double> Ys)> _tuningBuffers = new();

  private static readonly ScottPlot.Color[] _tuningPalette = {
    ScottPlot.Colors.Yellow, ScottPlot.Colors.Cyan, ScottPlot.Colors.OrangeRed,
    ScottPlot.Colors.LightGreen, ScottPlot.Colors.Magenta, ScottPlot.Colors.DeepSkyBlue,
  };
  private readonly Dictionary<string, ScottPlot.Color> _tuningColors = new();

  private ScottPlot.Color ColorFor(string label) {
    if (!_tuningColors.TryGetValue(label, out var c)) {
      c = _tuningPalette[_tuningColors.Count % _tuningPalette.Length];
      _tuningColors[label] = c;
    }
    return c;
  }

  private void ResetTuningPlot() {
    _tuningBuffers.Clear();
    _tuningPlot?.ClearAll();
  }

  private void OnTuningFieldsChanged() => ResetTuningPlot();

  private void OnTuningSampled(double t,
      System.Collections.Generic.IReadOnlyDictionary<string, double> sample) {
    if (_tuningPlot == null) {
      return;
    }
    double cutoff = t - FlightDataViewModel.TuningWindowSeconds;
    foreach (var (label, value) in sample) {
      if (!_tuningBuffers.TryGetValue(label, out var buf)) {
        buf = (new List<double>(), new List<double>());
        _tuningBuffers[label] = buf;
      }
      buf.Xs.Add(t);
      buf.Ys.Add(value);

      while (buf.Xs.Count > 0 && buf.Xs[0] < cutoff) {
        buf.Xs.RemoveAt(0);
        buf.Ys.RemoveAt(0);
      }
      _tuningPlot.SetSeries(label, buf.Xs, buf.Ys, ColorFor(label));
    }
  }

  private async void OnTuningPickClick(object? sender, RoutedEventArgs e) {
    if (DataContext is not FlightDataViewModel vm
        || TopLevel.GetTopLevel(this) is not Window owner) {
      return;
    }
    var fields = vm.TuningFieldList();
    var panel = new WrapPanel { Orientation = Orientation.Vertical, MaxHeight = 520 };
    var boxes = new List<CheckBox>();
    foreach (var (name, desc) in fields) {
      var cb = new CheckBox {
        Content = desc,
        Tag = name,
        IsChecked = vm.IsTuningField(name),
        Width = 200,
        FontSize = 11,
      };
      boxes.Add(cb);
      panel.Children.Add(cb);
    }
    var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
    var dlg = new Window {
      Title = "Tuning — pick fields",
      Width = 680,
      Height = 600,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new DockPanel {
        Margin = new Avalonia.Thickness(10),
        Children = {
          ok,
          new ScrollViewer {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = panel,
          },
        },
      },
    };
    DockPanel.SetDock(ok, Dock.Bottom);
    ok.Click += (_, _) => dlg.Close(true);
    if (await dlg.ShowDialog<bool>(owner)) {
      vm.SetTuningFields(boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag!));
      vm.Tuning = true;
    }
  }

  private readonly Dictionary<string, Window> _indicatorWindows = new();

  private void OnHudIndicatorClicked(string which) {
    string key = which switch { "ekf" => "ekf", "vibe" => "vibe", _ => "prearm" };
    if (_indicatorWindows.TryGetValue(key, out var existing)) {
      existing.Activate();
      return;
    }

    var owner = TopLevel.GetTopLevel(this) as Window;
    Window win = key switch {
      "ekf" => new EKFStatusWindow(),
      "vibe" => new VibrationWindow(),
      _ => new PrearmStatusWindow(),
    };
    _indicatorWindows[key] = win;
    win.Closed += (_, _) => _indicatorWindows.Remove(key);
    if (owner != null) {
      win.Show(owner);
    } else {
      win.Show();
    }
  }

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

  private readonly ITemplate<Panel?>? _defaultTabPanel;

  private static IEnumerable<TabItem> TabItemsOf(TabControl tabs) => tabs.Items.OfType<TabItem>();

  private static string HeaderOf(TabItem ti) => ti.Header?.ToString() ?? "";

  private void ApplyTabSettings(TabControl tabs) {
    var hidden = HiddenTabSet();
    foreach (var ti in TabItemsOf(tabs)) {
      ti.IsVisible = !hidden.Contains(HeaderOf(ti));
    }
    if (TabMultiLineSetting()) {
      SetTabMultiLine(tabs, true);
    }
  }

  private static HashSet<string> HiddenTabSet() {
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (Settings.Instance.ContainsKey("tabcontrolactions")) {
      foreach (var h in (Settings.Instance["tabcontrolactions"] ?? "")
                   .Split(';', StringSplitOptions.RemoveEmptyEntries)) {
        set.Add(h.Trim());
      }
    }
    return set;
  }

  private static bool TabMultiLineSetting() =>
      Settings.Instance.ContainsKey("tabcontrolmultiline")
      && bool.TryParse(Settings.Instance["tabcontrolmultiline"], out var b) && b;

  private void SetTabMultiLine(TabControl tabs, bool on) {
    tabs.ItemsPanel = on ? new FuncTemplate<Panel?>(() => new WrapPanel()) : _defaultTabPanel;
  }

  private ContextMenu BuildTabMenu(TabControl tabs) {
    var customize = new MenuItem { Header = "Customize" };
    customize.Click += async (_, _) => await CustomizeTabsAsync(tabs);
    var multiline = new MenuItem {
      Header = "MultiLine",
      ToggleType = MenuItemToggleType.CheckBox,
      IsChecked = TabMultiLineSetting(),
    };
    multiline.Click += (_, _) => {
      SetTabMultiLine(tabs, multiline.IsChecked);
      Settings.Instance["tabcontrolmultiline"] = multiline.IsChecked.ToString();
    };
    var menu = new ContextMenu();
    menu.Items.Add(customize);
    menu.Items.Add(multiline);
    return menu;
  }

  private async Task CustomizeTabsAsync(TabControl tabs) {
    if (TopLevel.GetTopLevel(this) is not Window owner) {
      return;
    }
    var panel = new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(4) };
    var map = new List<(TabItem Ti, CheckBox Cb)>();
    foreach (var ti in TabItemsOf(tabs)) {
      var cb = new CheckBox { Content = HeaderOf(ti), IsChecked = ti.IsVisible };
      map.Add((ti, cb));
      panel.Children.Add(cb);
    }
    var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right };
    var dlg = new Window {
      Title = "Customize Tabs",
      Width = 280,
      Height = 480,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new DockPanel {
        Margin = new Avalonia.Thickness(10),
        Children = { ok, new ScrollViewer { Content = panel } },
      },
    };
    DockPanel.SetDock(ok, Dock.Bottom);
    ok.Click += (_, _) => dlg.Close(true);
    if (!await dlg.ShowDialog<bool>(owner)) {
      return;
    }
    var hidden = new List<string>();
    foreach (var (ti, cb) in map) {
      bool vis = cb.IsChecked == true;
      ti.IsVisible = vis;
      if (!vis) {
        hidden.Add(HeaderOf(ti));
      }
    }
    Settings.Instance["tabcontrolactions"] = string.Join(";", hidden);
  }
}
