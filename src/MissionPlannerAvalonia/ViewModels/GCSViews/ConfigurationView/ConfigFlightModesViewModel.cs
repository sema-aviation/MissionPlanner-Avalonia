using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class FlightModeRow : ObservableObject {
  public int Number { get; }
  public string Band { get; }
  public bool ShowSimple { get; }
  public ObservableCollection<ParamOption> ModeOptions { get; }

  [ObservableProperty]
  private ParamOption? _selectedMode;

  [ObservableProperty]
  private bool _simple;

  [ObservableProperty]
  private bool _superSimple;

  [ObservableProperty]
  private bool _active;

  public FlightModeRow(int number, string band, bool showSimple,
      ObservableCollection<ParamOption> modeOptions) {
    Number = number;
    Band = band;
    ShowSimple = showSimple;
    ModeOptions = modeOptions;
  }
}

public partial class ConfigFlightModesViewModel : ViewModelBase, IDisposable {
  private static readonly string[] Bands = {
    "PWM 0 - 1230", "1231 - 1360", "1361 - 1490", "1491 - 1620", "1621 - 1749", "1750 +",
  };

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private readonly string _prefix;
  private readonly bool _isCopter;

  public ObservableCollection<ParamOption> ModeOptions { get; } = new();
  public ObservableCollection<FlightModeRow> Rows { get; } = new();
  public bool ShowSimple { get; }

  [ObservableProperty]
  private string _currentMode = "";

  [ObservableProperty]
  private string _currentPwm = "";

  [ObservableProperty]
  private string _saveModesText = "Save Modes";

  public ConfigFlightModesViewModel() {
    var fw = _comPort.MAV.cs.firmware;
    _isCopter = fw == Firmwares.ArduCopter2;
    ShowSimple = _isCopter;

    _prefix = fw switch {
      Firmwares.ArduRover => "MODE",
      Firmwares.PX4 => "COM_FLTMODE",
      _ => "FLTMODE",
    };

    LoadOptions(fw);

    for (int i = 1; i <= 6; i++) {
      Rows.Add(new FlightModeRow(i, Bands[i - 1], ShowSimple, ModeOptions));
    }

    LoadValues();

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
  }

  private void LoadOptions(Firmwares fw) {
    List<KeyValuePair<int, string>>? modes = null;
    try {
      modes = Common.getModesList(fw);
    } catch {
    }

    if (modes == null || modes.Count == 0) {
      try {
        modes = ParameterMetaDataRepository.GetParameterOptionsInt(_prefix + "1", fw.ToString());
      } catch {
      }
    }

    if (modes == null) {
      return;
    }

    foreach (var kv in modes) {
      ModeOptions.Add(new ParamOption(kv.Key, kv.Value));
    }
  }

  private void LoadValues() {
    for (int i = 0; i < Rows.Count; i++) {
      var name = _prefix + (i + 1);
      if (_comPort.MAV.param.ContainsKey(name)) {
        var v = (int)Math.Round(_comPort.MAV.param[name].Value);
        Rows[i].SelectedMode = ModeOptions.FirstOrDefault(o => o.Value == v);
      }
    }

    if (_isCopter) {
      LoadBitmask("SIMPLE", (row, set) => row.Simple = set);
      LoadBitmask("SUPER_SIMPLE", (row, set) => row.SuperSimple = set);
    }
  }

  private void LoadBitmask(string param, Action<FlightModeRow, bool> apply) {
    if (!_comPort.MAV.param.ContainsKey(param)) {
      return;
    }

    var mask = (int)Math.Round(_comPort.MAV.param[param].Value);
    for (int i = 0; i < Rows.Count; i++) {
      apply(Rows[i], ((mask >> i) & 1) == 1);
    }
  }

  private void Tick() {
    CurrentMode = _comPort.MAV.cs.mode ?? "";

    float pwm = 0;
    var ch = ActiveChannel();
    if (ch > 0) {
      pwm = ChannelIn(ch);
      CurrentPwm = ch + ": " + pwm;
    }

    var active = ActiveIndex(pwm);
    for (int i = 0; i < Rows.Count; i++) {
      Rows[i].Active = i == active;
    }
  }

  private int ActiveChannel() {
    if (_comPort.MAV.param.ContainsKey("FLTMODE_CH")) {
      return (int)_comPort.MAV.param["FLTMODE_CH"].Value;
    }
    if (_comPort.MAV.param.ContainsKey("MODE_CH")) {
      return (int)_comPort.MAV.param["MODE_CH"].Value;
    }
    return 0;
  }

  private float ChannelIn(int ch) {
    var cs = _comPort.MAV.cs;
    return ch switch {
      5 => cs.ch5in,
      6 => cs.ch6in,
      7 => cs.ch7in,
      8 => cs.ch8in,
      9 => cs.ch9in,
      10 => cs.ch10in,
      11 => cs.ch11in,
      12 => cs.ch12in,
      13 => cs.ch13in,
      14 => cs.ch14in,
      15 => cs.ch15in,
      16 => cs.ch16in,
      _ => 0,
    };
  }

  private static int ActiveIndex(float pwm) {
    var p = (int)pwm;
    if (p <= 1230) {
      return 0;
    }
    if (p <= 1360) {
      return 1;
    }
    if (p <= 1490) {
      return 2;
    }
    if (p <= 1620) {
      return 3;
    }
    if (p <= 1749) {
      return 4;
    }
    return 5;
  }

  [RelayCommand]
  private async Task SaveModes() {
    if (_comPort.BaseStream?.IsOpen != true) {
      SaveModesText = "Offline";
      return;
    }

    try {
      var sysid = (byte)_comPort.sysidcurrent;
      var compid = (byte)_comPort.compidcurrent;

      for (int i = 0; i < Rows.Count; i++) {
        var mode = Rows[i].SelectedMode;
        if (mode == null) {
          continue;
        }

        var name = _prefix + (i + 1);
        var v = mode.Value;
        await Task.Run(() => _comPort.setParam(sysid, compid, name, v));
      }

      if (_isCopter) {
        await WriteBitmask(sysid, compid, "SIMPLE", r => r.Simple);
        await WriteBitmask(sysid, compid, "SUPER_SIMPLE", r => r.SuperSimple);
      }

      SaveModesText = "Complete";
    } catch (Exception ex) {
      SaveModesText = "Error: " + ex.Message;
    }
  }

  private async Task WriteBitmask(byte sysid, byte compid, string param, Func<FlightModeRow, bool> get) {
    if (!_comPort.MAV.param.ContainsKey(param)) {
      return;
    }

    var mask = 0;
    for (int i = 0; i < Rows.Count; i++) {
      if (get(Rows[i])) {
        mask |= 1 << i;
      }
    }

    await Task.Run(() => _comPort.setParam(sysid, compid, param, mask));
  }

  public void Dispose() {
    _timer.Stop();
  }
}
