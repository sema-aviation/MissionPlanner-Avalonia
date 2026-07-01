using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigOSDViewModel : ParamPageBase {
  private static readonly Regex _enRegex =
      new("^OSD(\\d+)_(.+)_EN$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

  public int Columns { get; } = 30;
  public int Rows { get; } = 16;
  public double CellWidth { get; } = 26;
  public double CellHeight { get; } = 24;

  public double CanvasWidth => Columns * CellWidth;
  public double CanvasHeight => Rows * CellHeight;

  public ObservableCollection<int> Screens { get; } = new();
  public ObservableCollection<OsdItemVm> Items { get; } = new();

  [ObservableProperty]
  private int _selectedScreen;

  [ObservableProperty]
  private string _status = "";

  public ConfigOSDViewModel() {
    Title = "Onboard OSD";
    Intro = "Drag items on the screen preview, or edit X/Y directly. Tick Enable to show an item. "
        + "Changes write live to the OSD parameters.";
    BuildScreens();
  }

  protected override void OnRefreshed() {
    BuildScreens();
  }

  partial void OnSelectedScreenChanged(int value) => LoadItems();

  private void BuildScreens() {
    var screens = new SortedSet<int>();
    foreach (var key in comPort.MAV.param.Keys) {
      var m = _enRegex.Match(key);
      if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) {
        screens.Add(n);
      }
    }

    Screens.Clear();
    foreach (var s in screens) {
      Screens.Add(s);
    }

    if (Screens.Count == 0) {
      Items.Clear();
      Status = "No OSD parameters found. Connect and Refresh.";
      return;
    }

    if (!Screens.Contains(SelectedScreen)) {
      SelectedScreen = Screens.First();
    } else {
      LoadItems();
    }
  }

  private void LoadItems() {
    Items.Clear();
    if (SelectedScreen <= 0) {
      return;
    }

    var prefix = $"OSD{SelectedScreen}_";
    var items = new List<OsdItemVm>();
    foreach (var key in comPort.MAV.param.Keys) {
      var m = _enRegex.Match(key);
      if (!m.Success || m.Groups[1].Value != SelectedScreen.ToString()) {
        continue;
      }

      var item = m.Groups[2].Value;
      var xName = prefix + item + "_X";
      var yName = prefix + item + "_Y";
      if (!comPort.MAV.param.ContainsKey(xName) || !comPort.MAV.param.ContainsKey(yName)) {
        continue;
      }

      items.Add(new OsdItemVm(this, item, key, xName, yName));
    }

    foreach (var it in items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)) {
      Items.Add(it);
    }

    Status = $"Screen {SelectedScreen}: {Items.Count} items.";
  }

  internal void ReportWrite(string name, bool ok) {
    Status = ok ? $"{name} ✓" : $"{name} write failed";
  }

  [RelayCommand]
  private void EnableAll() => SetAllEnabled(true);

  [RelayCommand]
  private void DisableAll() => SetAllEnabled(false);

  private void SetAllEnabled(bool on) {
    foreach (var it in Items) {
      it.Enabled = on;
    }
  }
}

public partial class OsdItemVm : ObservableObject {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly ConfigOSDViewModel _owner;
  private readonly bool _suppress;

  public string Name { get; }
  public string EnName { get; }
  public string XName { get; }
  public string YName { get; }

  [ObservableProperty]
  private bool _enabled;

  [ObservableProperty]
  private int _x;

  [ObservableProperty]
  private int _y;

  public double CanvasLeft => X * _owner.CellWidth;
  public double CanvasTop => Y * _owner.CellHeight;
  public double Opacity => Enabled ? 1.0 : 0.35;

  public OsdItemVm(ConfigOSDViewModel owner, string name, string enName, string xName, string yName) {
    _owner = owner;
    Name = name;
    EnName = enName;
    XName = xName;
    YName = yName;

    _suppress = true;
    Enabled = Read(enName) != 0;
    X = (int)Math.Round(Read(xName));
    Y = (int)Math.Round(Read(yName));
    _suppress = false;
  }

  public void SetCell(int col, int row) {
    X = Math.Clamp(col, 0, _owner.Columns - 1);
    Y = Math.Clamp(row, 0, _owner.Rows - 1);
  }

  [Obsolete]
  partial void OnEnabledChanged(bool value) {
    OnPropertyChanged(nameof(Opacity));
    Push(EnName, value ? 1 : 0);
  }

  [Obsolete]
  partial void OnXChanged(int value) {
    OnPropertyChanged(nameof(CanvasLeft));
    Push(XName, value);
  }

  [Obsolete]
  partial void OnYChanged(int value) {
    OnPropertyChanged(nameof(CanvasTop));
    Push(YName, value);
  }

  private double Read(string name) =>
      _comPort.MAV.param.ContainsKey(name) ? _comPort.MAV.param[name].Value : 0;

  [Obsolete]
  private async void Push(string name, double v) {
    if (_suppress) {
      return;
    }

    if (_comPort.BaseStream?.IsOpen != true) {
      if (_comPort.MAV.param.ContainsKey(name)) {
        _comPort.MAV.param[name].Value = v;
      }
      _owner.ReportWrite(name + " (offline)", true);
      return;
    }

    try {
      bool ok = await Task.Run(() => _comPort.setParam(name, v, true));
      _owner.ReportWrite(name, ok);
    } catch (Exception ex) {
      _owner.Status = ex.Message;
    }
  }
}
