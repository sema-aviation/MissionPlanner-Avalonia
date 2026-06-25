using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Joystick;

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
  private string _status =
      "Select a joystick and map each RC channel to an axis. DirectInput only enumerates devices on Windows.";

  public bool IsEnabled => _joystick != null && _joystick.enabled;

  public ConfigJoystickViewModel() {
    RefreshDevices();

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
