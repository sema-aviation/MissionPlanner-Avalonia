using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Joystick;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigJoystickViewModel : ViewModelBase, IDisposable {
  private const int MaxAxis = 16;

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private JoystickBase? _joystick;

  public ObservableCollection<string> Devices { get; } = new();
  public ObservableCollection<JoyAxisRow> Axes { get; } = new();
  public ObservableCollection<JoyButtonRow> Buttons { get; } = new();

  public IReadOnlyList<string> AxisOptions { get; } =
      Enum.GetNames(typeof(joystickaxis));

  public IReadOnlyList<string> ButtonFunctions { get; } =
      Enum.GetNames(typeof(buttonfunction));

  [ObservableProperty]
  private string _selectedDevice = "";

  [ObservableProperty]
  private bool _elevons;

  [ObservableProperty]
  private bool _manualControl;

  [ObservableProperty]
  private string _enableLabel = "Enable";

  [ObservableProperty]
  private string _loadedConfig = "Loaded config: (default xml)";

  [ObservableProperty]
  private string _status =
      "Select a joystick and map each RC channel to an axis. DirectInput only enumerates devices on Windows.";

  public bool IsEnabled => _joystick != null && _joystick.enabled;

  public ConfigJoystickViewModel() {
    RefreshDevices();
    LoadedConfig = "Loaded Config for " + _comPort.MAV.cs.firmware;

    // build a config-only joystick so the axis rows reflect any saved xml mapping
    var temp = JoystickBase.Create(() => _comPort);
    for (int a = 1; a <= MaxAxis; a++) {
      var cfg = temp.getChannel(a);
      Axes.Add(new JoyAxisRow(a) {
        Axis = (cfg.axis).ToString(),
        Expo = cfg.expo,
        Reverse = cfg.reverse,
      });
    }
    temp.Dispose();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  [RelayCommand]
  private void RefreshDevices() {
    var selected = SelectedDevice;
    Devices.Clear();
    try {
      foreach (var d in JoystickBase.getDevices()) {
        Devices.Add(d);
      }
    } catch (Exception ex) {
      Status = "Error getting joystick list: " + ex.Message;
      return;
    }

    if (!string.IsNullOrEmpty(selected) && Devices.Contains(selected)) {
      SelectedDevice = selected;
    } else if (Devices.Count > 0) {
      SelectedDevice = Devices[0];
    } else {
      Status = "No joysticks detected (DirectInput finds devices on Windows only).";
    }
  }

  [RelayCommand]
  private void ToggleEnable() {
    if (_joystick == null || !_joystick.enabled) {
      if (string.IsNullOrEmpty(SelectedDevice)) {
        Status = "Please select a joystick.";
        return;
      }

      try {
        _joystick?.UnAcquireJoyStick();
      } catch {
      }

      var joy = JoystickBase.Create(() => _comPort);
      ApplyConfigTo(joy);
      joy.elevons = Elevons;
      joy.manual_control = ManualControl;

      if (!joy.start(SelectedDevice)) {
        Status = "Please connect a joystick.";
        joy.Dispose();
        return;
      }

      joy.name = SelectedDevice;
      joy.enabled = true;
      _joystick = joy;

      BuildButtonRows();

      EnableLabel = "Disable";
      Status = "Joystick enabled: " + SelectedDevice;
    } else {
      _joystick.enabled = false;
      _joystick.clearRCOverride();
      _joystick.Dispose();
      _joystick = null;
      EnableLabel = "Enable";
      Status = "Joystick disabled.";
    }

    OnPropertyChanged(nameof(IsEnabled));
  }

  [RelayCommand]
  private void Save() {
    if (_joystick == null) {
      Status = "Please enable a joystick before saving.";
      return;
    }

    ApplyConfigTo(_joystick);
    _joystick.saveconfig();
    Status = "Joystick configuration saved.";
  }

  // Mirrors JoystickSetup.but_settings_Click: each button function has a small dialog that
  // edits that action's Joy_* params (mode / p1..p4) directly on the live JoyButton config.
  [RelayCommand]
  private async Task ButtonSettings(JoyButtonRow row) {
    if (row == null) {
      return;
    }

    if (_joystick == null) {
      Status = "Enable a joystick before configuring button actions.";
      return;
    }

    // Sync this row's chosen function/buttonno into the live config first so the dialog
    // reads/writes the JoyButton entry it is about to edit.
    ApplyButtonToJoystick(row);

    var fn = ParseFunction(row.Function);
    switch (fn) {
      case buttonfunction.ChangeMode:
        await ShowModeDialog(row.Index);
        break;
      case buttonfunction.Mount_Mode:
        await ShowMountModeDialog(row.Index);
        break;
      case buttonfunction.Do_Set_Relay:
        await ShowParamDialog(row.Index, "Do_Set_Relay", ("Relay No#", "p1"));
        break;
      case buttonfunction.Do_Set_Servo:
        await ShowParamDialog(row.Index, "Do_Set_Servo", ("Servo No#", "p1"), ("PWM", "p2"));
        break;
      case buttonfunction.Do_Repeat_Relay:
        await ShowParamDialog(row.Index, "Do_Repeat_Relay",
            ("Relay No#", "p1"), ("Repeat #", "p2"), ("Time", "p3"));
        break;
      case buttonfunction.Do_Repeat_Servo:
        await ShowParamDialog(row.Index, "Do_Repeat_Servo",
            ("Servo No#", "p1"), ("Pwm Value", "p2"), ("Rep Time", "p3"), ("Delay (ms)", "p4"));
        break;
      case buttonfunction.Button_axis0:
      case buttonfunction.Button_axis1:
        await ShowParamDialog(row.Index, "Button_axis", ("PWM 1", "p1"), ("PWM 2", "p2"));
        break;
      default:
        Status = "No settings to set for " + fn + ".";
        break;
    }
  }

  // Export every joystickbutton*/joystickaxis* xml into a single .joycfg zip (JoystickBase.ExportConfig).
  [RelayCommand]
  private async Task ExportConfig() {
    if (_joystick == null) {
      Status = "Enable a joystick before exporting.";
      return;
    }

    var path = await PickSaveAsync("Export joystick config", "joycfg");
    if (string.IsNullOrEmpty(path)) {
      return;
    }

    try {
      ApplyConfigTo(_joystick);
      _joystick.saveconfig();
      _joystick.ExportConfig(path);
      LoadedConfig = "Loaded config: " + System.IO.Path.GetFileName(path);
      Status = "Exported joystick config to " + path;
    } catch (Exception ex) {
      Status = "Export failed: " + ex.Message;
    }
  }

  // Import a .joycfg zip back into the user data dir and reload the axis rows (JoystickBase.ImportConfig).
  [RelayCommand]
  private async Task ImportConfig() {
    if (!await Dialogs.Confirm("Import Joystick Config",
            "NOTE: this will replace any existing joystick configuration.\n"
            + "Please make sure you have saved your current configuration if needed.")) {
      return;
    }

    var path = await PickFileAsync("Import joystick config", "*.joycfg", "Joystick config");
    if (string.IsNullOrEmpty(path)) {
      return;
    }

    // A live, enabled joystick holds the old config in memory; drop it so the reload wins.
    if (_joystick != null && _joystick.enabled) {
      ToggleEnable();
    }

    try {
      var temp = JoystickBase.Create(() => _comPort);
      temp.ImportConfig(path);
      temp.loadconfig();
      for (int a = 1; a <= MaxAxis && a - 1 < Axes.Count; a++) {
        var cfg = temp.getChannel(a);
        var rowidx = a - 1;
        Axes[rowidx].Axis = cfg.axis.ToString();
        Axes[rowidx].Expo = cfg.expo;
        Axes[rowidx].Reverse = cfg.reverse;
      }
      temp.Dispose();
      LoadedConfig = "Loaded config: " + System.IO.Path.GetFileName(path);
      Status = "Imported. Re-enable the joystick for changes to take effect.";
    } catch (Exception ex) {
      Status = "Import failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task DetectAxis(JoyAxisRow row) {
    if (row == null || string.IsNullOrEmpty(SelectedDevice)) {
      return;
    }

    Status = "Move the axis you want assigned to RC " + row.ChannelNo + "...";
    var axis = await Task.Run(() => JoystickBase.getMovingAxis(SelectedDevice, 16000));
    row.Axis = axis.ToString();
    ApplyAxisToJoystick(row);
    Status = "RC " + row.ChannelNo + " mapped to " + row.Axis;
  }

  [RelayCommand]
  private async Task DetectButton(JoyButtonRow row) {
    if (row == null || string.IsNullOrEmpty(SelectedDevice)) {
      return;
    }

    Status = "Press the button you want assigned...";
    var no = await Task.Run(() => JoystickBase.getPressedButton(SelectedDevice));
    row.ButtonNo = no;
    ApplyButtonToJoystick(row);
    Status = "Button assigned: " + no;
  }

  private void ApplyConfigTo(JoystickBase joy) {
    foreach (var row in Axes) {
      joy.setChannel(row.ChannelNo, ParseAxis(row.Axis), row.Reverse, row.Expo);
    }

    int idx = 0;
    foreach (var row in Buttons) {
      var cfg = joy.getButton(idx);
      cfg.buttonno = row.ButtonNo;
      cfg.function = ParseFunction(row.Function);
      joy.setButton(idx, cfg);
      idx++;
    }
  }

  private void ApplyAxisToJoystick(JoyAxisRow row) {
    if (_joystick == null) {
      return;
    }

    _joystick.setChannel(row.ChannelNo, ParseAxis(row.Axis), row.Reverse, row.Expo);
  }

  private void ApplyButtonToJoystick(JoyButtonRow row) {
    if (_joystick == null) {
      return;
    }

    var cfg = _joystick.getButton(row.Index);
    cfg.buttonno = row.ButtonNo;
    cfg.function = ParseFunction(row.Function);
    _joystick.setButton(row.Index, cfg);
  }

  private void BuildButtonRows() {
    Buttons.Clear();
    if (_joystick == null) {
      return;
    }

    int count;
    try {
      count = Math.Min(16, _joystick.getNumButtons());
    } catch {
      count = 0;
    }

    for (int f = 0; f < count; f++) {
      var cfg = _joystick.getButton(f);
      Buttons.Add(new JoyButtonRow(f) {
        ButtonNo = cfg.buttonno,
        Function = cfg.function.ToString(),
      });
    }
  }

  private void Pump() {
    if (_joystick == null || !_joystick.enabled) {
      return;
    }

    _joystick.elevons = Elevons;
    _joystick.manual_control = ManualControl;

    try {
      foreach (var row in Axes) {
        row.Value = _joystick.getValueForChannel(row.ChannelNo);
      }

      foreach (var row in Buttons) {
        row.Pressed = _joystick.isButtonPressed(row.Index);
      }
    } catch {
    }
  }

  private static joystickaxis ParseAxis(string value) {
    if (Enum.TryParse(value, out joystickaxis axis)) {
      return axis;
    }

    return joystickaxis.None;
  }

  private static buttonfunction ParseFunction(string value) {
    if (Enum.TryParse(value, out buttonfunction fn)) {
      return fn;
    }

    return buttonfunction.ChangeMode;
  }

  // ChangeMode dialog (mirrors Joy_ChangeMode): pick a flight mode string -> config.mode.
  private async Task ShowModeDialog(int index) {
    if (_joystick == null) {
      return;
    }

    var cfg = _joystick.getButton(index);
    List<KeyValuePair<int, string>> modes;
    try {
      modes = Common.getModesList(_comPort.MAV.cs.firmware);
    } catch {
      modes = new List<KeyValuePair<int, string>>();
    }

    var names = modes.Select(m => m.Value).ToList();
    var combo = new ComboBox {
      ItemsSource = names,
      SelectedItem = names.FirstOrDefault(n => n == cfg.mode) ?? names.FirstOrDefault(),
      HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    if (await ShowDialog("Joy_ChangeMode", new Control[] {
      new TextBlock { Text = "Mode" }, combo,
    })) {
      cfg.function = buttonfunction.ChangeMode;
      cfg.mode = combo.SelectedItem as string ?? cfg.mode;
      _joystick.setButton(index, cfg);
      Status = "Button " + (index + 1) + " -> ChangeMode " + cfg.mode;
    }
  }

  // Mount_Mode dialog (mirrors Joy_Mount_Mode): pick MNT*_DEFLT_MODE option -> config.p1.
  private async Task ShowMountModeDialog(int index) {
    if (_joystick == null) {
      return;
    }

    var cfg = _joystick.getButton(index);
    List<KeyValuePair<int, string>> opts = new();
    foreach (var name in new[] { "MNT1_DEFLT_MODE", "MNT_DEFLT_MODE", "MNT_MODE" }) {
      try {
        var item = ParameterMetaDataRepository.GetParameterOptionsInt(
            name, _comPort.MAV.cs.firmware.ToString());
        if (item != null && item.Count > 0) {
          opts = item;
          break;
        }
      } catch {
      }
    }

    var names = opts.Select(o => o.Value).ToList();
    var keys = opts.Select(o => o.Key).ToList();
    var combo = new ComboBox {
      ItemsSource = names,
      SelectedIndex = Math.Max(0, keys.IndexOf((int)cfg.p1)),
      HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    if (await ShowDialog("Joy_Mount_Mode", new Control[] {
      new TextBlock { Text = "Mount Mode" }, combo,
    })) {
      cfg.function = buttonfunction.Mount_Mode;
      if (combo.SelectedIndex >= 0 && combo.SelectedIndex < keys.Count) {
        cfg.p1 = keys[combo.SelectedIndex];
      }
      _joystick.setButton(index, cfg);
      Status = "Button " + (index + 1) + " -> Mount_Mode " + cfg.p1;
    }
  }

  // Generic numeric-param dialog (mirrors the Joy_Do_* / Joy_Button_axis NumericUpDown forms).
  private async Task ShowParamDialog(int index, string title,
      params (string Label, string Field)[] fields) {
    if (_joystick == null) {
      return;
    }

    var cfg = _joystick.getButton(index);
    var boxes = new List<(string Field, NumericUpDown Box)>();
    var body = new List<Control>();
    foreach (var (label, field) in fields) {
      var box = new NumericUpDown {
        Minimum = 0,
        Maximum = 65535,
        Value = (decimal)GetParam(cfg, field),
        HorizontalAlignment = HorizontalAlignment.Stretch,
      };
      body.Add(new TextBlock { Text = label });
      body.Add(box);
      boxes.Add((field, box));
    }

    if (await ShowDialog("Joy_" + title, body.ToArray())) {
      foreach (var (field, box) in boxes) {
        cfg = SetParam(cfg, field, (float)(box.Value ?? 0));
      }
      _joystick.setButton(index, cfg);
      Status = "Button " + (index + 1) + " -> " + title + " saved.";
    }
  }

  private static float GetParam(JoyButton cfg, string field) => field switch {
    "p1" => cfg.p1,
    "p2" => cfg.p2,
    "p3" => cfg.p3,
    "p4" => cfg.p4,
    _ => 0,
  };

  private static JoyButton SetParam(JoyButton cfg, string field, float value) {
    switch (field) {
      case "p1": cfg.p1 = value; break;
      case "p2": cfg.p2 = value; break;
      case "p3": cfg.p3 = value; break;
      case "p4": cfg.p4 = value; break;
    }
    return cfg;
  }

  // Minimal modal dialog (OK/Cancel) built in code, in the spirit of Services/Dialogs.cs.
  private static Task<bool> ShowDialog(string title, Control[] body) {
    var panel = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
    panel.Children.Add(new TextBlock {
      Text = title,
      FontWeight = FontWeight.Bold,
      FontSize = 14,
    });
    foreach (var c in body) {
      panel.Children.Add(c);
    }

    var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
    var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
    var row = new StackPanel {
      Orientation = Orientation.Horizontal,
      HorizontalAlignment = HorizontalAlignment.Right,
      Spacing = 8,
      Margin = new Thickness(0, 6, 0, 0),
    };
    row.Children.Add(ok);
    row.Children.Add(cancel);
    panel.Children.Add(row);

    var w = new Window {
      Title = title,
      Width = 320,
      SizeToContent = SizeToContent.Height,
      CanResize = false,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Background = new SolidColorBrush(Color.Parse("#262728")),
      Content = panel,
    };
    ok.Click += (_, _) => w.Close(true);
    cancel.Click += (_, _) => w.Close(false);

    var owner = (Application.Current?.ApplicationLifetime
                 as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    return owner != null ? w.ShowDialog<bool>(owner) : w.ShowDialog<bool>(w);
  }

  private static async Task<string?> PickFileAsync(string title, string pattern, string desc) {
    var top = (Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = title,
      AllowMultiple = false,
      FileTypeFilter = new[] {
        new FilePickerFileType(desc) { Patterns = new[] { pattern } },
      },
    });
    return files.Count > 0 ? files[0].TryGetLocalPath() : null;
  }

  private static async Task<string?> PickSaveAsync(string title, string ext) {
    var top = (Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = title,
      DefaultExtension = ext,
      FileTypeChoices = new[] {
        new FilePickerFileType("Joystick config") { Patterns = new[] { "*.joycfg" } },
      },
    });
    return file?.TryGetLocalPath();
  }

  public void Dispose() {
    _timer.Stop();
    try {
      _joystick?.UnAcquireJoyStick();
    } catch {
    }

    _joystick?.Dispose();
    _joystick = null;
  }
}

public partial class JoyAxisRow : ObservableObject {
  public JoyAxisRow(int channelNo) {
    ChannelNo = channelNo;
  }

  public int ChannelNo { get; }

  public string Label => "RC " + ChannelNo;

  [ObservableProperty]
  private string _axis = "None";

  [ObservableProperty]
  private int _expo;

  [ObservableProperty]
  private bool _reverse;

  [ObservableProperty]
  private int _value;
}

public partial class JoyButtonRow : ObservableObject {
  public JoyButtonRow(int index) {
    Index = index;
  }

  public int Index { get; }

  public string Label => "But " + (Index + 1);

  [ObservableProperty]
  private int _buttonNo = -1;

  [ObservableProperty]
  private string _function = "ChangeMode";

  [ObservableProperty]
  private bool _pressed;
}

public class JoyPressedConverter : IValueConverter {
  public static readonly JoyPressedConverter Instance = new();

  private static readonly IBrush On = new SolidColorBrush(Color.FromRgb(0x94, 0xC1, 0x1F));
  private static readonly IBrush Off = new SolidColorBrush(Color.FromRgb(0x26, 0x27, 0x28));

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
    return value is true ? On : Off;
  }

  public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
    throw new NotSupportedException();
  }
}
