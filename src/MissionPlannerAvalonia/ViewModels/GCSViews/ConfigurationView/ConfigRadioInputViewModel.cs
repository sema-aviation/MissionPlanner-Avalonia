using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigRadioInputViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private readonly float[] _min = new float[16];
  private readonly float[] _max = new float[16];
  private readonly float[] _trim = new float[16];
  private bool _calibrating;
  private readonly bool _startup = true;

  private int _chRoll = 1, _chPitch = 2, _chThro = 3, _chYaw = 4;

  public RcChannel[] Channels { get; }
  public RcChannel Roll { get; }
  public RcChannel Pitch { get; }
  public RcChannel Throttle { get; }
  public RcChannel Yaw { get; }
  public RcChannel[] Aux { get; }

  [ObservableProperty] private string _status = "Move all transmitter sticks/switches to view live input.";
  [ObservableProperty] private string _calibrateLabel = "Calibrate Radio";

  [ObservableProperty] private bool _revRoll;
  [ObservableProperty] private bool _revPitch;
  [ObservableProperty] private bool _revThrottle;
  [ObservableProperty] private bool _revYaw;
  [ObservableProperty] private bool _showReverse = true;

  [ObservableProperty] private bool _isPlane;
  [ObservableProperty] private bool _elevonMixing;
  [ObservableProperty] private bool _elevonReverse;
  [ObservableProperty] private bool _elevonCh1Rev;
  [ObservableProperty] private bool _elevonCh2Rev;

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigRadioInputViewModel() {
    Channels = new RcChannel[16];
    for (int i = 0; i < 16; i++) {
      Channels[i] = new RcChannel(i + 1);
      _min[i] = 3000;
      _max[i] = 0;
      _trim[i] = 1500;
    }

    Setup();

    Roll = Channels[_chRoll - 1];
    Pitch = Channels[_chPitch - 1];
    Throttle = Channels[_chThro - 1];
    Yaw = Channels[_chYaw - 1];
    Roll.Label = $"Roll (rc{_chRoll})";
    Pitch.Label = $"Pitch (rc{_chPitch})";
    Throttle.Label = $"Throttle (rc{_chThro})";
    Yaw.Label = $"Yaw (rc{_chYaw})";

    Aux = new RcChannel[12];
    for (int i = 0; i < 12; i++) {
      Aux[i] = Channels[i + 4];
      Aux[i].Label = $"Radio {i + 5}";
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
    _startup = false;
  }

  private void Setup() {
    var fw = _comPort.MAV.cs.firmware;
    IsPlane = fw == Firmwares.ArduPlane || fw == Firmwares.Ateryx;
    ShowReverse = fw != Firmwares.ArduCopter2;

    if (Has("RCMAP_ROLL")) {
      _chRoll = (int)P("RCMAP_ROLL");
      _chPitch = (int)P("RCMAP_PITCH");
      _chThro = (int)P("RCMAP_THROTTLE");
      _chYaw = (int)P("RCMAP_YAW");
    }

    RevRoll = P($"RC{_chRoll}_REVERSED") == 1;
    RevPitch = P($"RC{_chPitch}_REVERSED") == 1;
    RevThrottle = P($"RC{_chThro}_REVERSED") == 1;
    RevYaw = P($"RC{_chYaw}_REVERSED") == 1;

    if (IsPlane) {
      ElevonMixing = P("ELEVON_MIXING") == 1;
      ElevonReverse = P("ELEVON_REVERSE") == 1;
      ElevonCh1Rev = P("ELEVON_CH1_REV") == 1;
      ElevonCh2Rev = P("ELEVON_CH2_REV") == 1;
    }

    try {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 2);
    } catch { }
  }

  private bool Has(string name) => _comPort.MAV.param.ContainsKey(name);
  private double P(string name) => Has(name) ? _comPort.MAV.param[name].Value : double.NaN;

  private void Pump() {
    var cs = _comPort.MAV.cs;
    float[] vals = {
      cs.ch1in, cs.ch2in, cs.ch3in, cs.ch4in, cs.ch5in, cs.ch6in, cs.ch7in, cs.ch8in,
      cs.ch9in, cs.ch10in, cs.ch11in, cs.ch12in, cs.ch13in, cs.ch14in, cs.ch15in, cs.ch16in,
    };
    for (int i = 0; i < 16; i++) {
      Channels[i].Value = (int)vals[i];
      if (_calibrating && vals[i] > 800 && vals[i] < 2200) {
        _min[i] = Math.Min(_min[i], vals[i]);
        _max[i] = Math.Max(_max[i], vals[i]);
        Channels[i].Min = (int)_min[i];
        Channels[i].Max = (int)_max[i];
      }
    }
  }

  partial void OnRevRollChanged(bool value) => WriteReverse(_chRoll, value);
  partial void OnRevPitchChanged(bool value) => WriteReverse(_chPitch, value);
  partial void OnRevThrottleChanged(bool value) => WriteReverse(_chThro, value);
  partial void OnRevYawChanged(bool value) => WriteReverse(_chYaw, value);
  partial void OnElevonMixingChanged(bool value) => WriteParam("ELEVON_MIXING", value ? 1 : 0);
  partial void OnElevonReverseChanged(bool value) => WriteParam("ELEVON_REVERSE", value ? 1 : 0);
  partial void OnElevonCh1RevChanged(bool value) => WriteParam("ELEVON_CH1_REV", value ? 1 : 0);
  partial void OnElevonCh2RevChanged(bool value) => WriteParam("ELEVON_CH2_REV", value ? 1 : 0);

  private void WriteReverse(int ch, bool reversed) {
    if (_startup || !IsConnected) {
      return;
    }
    WriteParam($"RC{ch}_REVERSED", reversed ? 1 : 0);
  }

  [Obsolete]
  private void WriteParam(string name, double value) {
    if (_startup || !IsConnected || !Has(name)) {
      return;
    }
    _ = Task.Run(() => _comPort.setParam(name, value));
  }

  [RelayCommand]
  private void BindDsm2() => Bind(0);
  [RelayCommand]
  private void BindDsmX() => Bind(1);
  [RelayCommand]
  private void BindDsm8() => Bind(2);

  private void Bind(int dsmType) {
    if (!IsConnected) {
      Status = "Not connected — cannot bind.";
      return;
    }
    try {
      _comPort.doCommand(_comPort.MAV.sysid, _comPort.MAV.compid,
          MAVLink.MAV_CMD.START_RX_PAIR, 0, dsmType, 0, 0, 0, 0, 0);
      Status = "Put the transmitter in bind mode — receiver is waiting.";
    } catch {
      Status = "Error binding receiver.";
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task ToggleCalibrate() {
    if (!IsConnected) {
      Status = "Not connected — cannot calibrate.";
      return;
    }

    if (!_calibrating) {
      Status = "Move ALL sticks and switches to their extremes, then click Done.";
      for (int i = 0; i < 16; i++) {
        _min[i] = 3000;
        _max[i] = 0;
      }
      try {
        _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 10);
      } catch { }
      _calibrating = true;
      CalibrateLabel = "Click when Done";
      return;
    }

    _calibrating = false;
    CalibrateLabel = "Calibrate Radio";

    if (!(_min[0] > 800 && _min[0] < 2200)) {
      Status = "Bad channel 1 input — calibration cancelled.";
      return;
    }

    Status = "Ensure sticks centered + throttle down, writing RCn_MIN/MAX/TRIM…";
    var cs = _comPort.MAV.cs;
    float[] center = {
      cs.ch1in, cs.ch2in, cs.ch3in, cs.ch4in, cs.ch5in, cs.ch6in, cs.ch7in, cs.ch8in,
      cs.ch9in, cs.ch10in, cs.ch11in, cs.ch12in, cs.ch13in, cs.ch14in, cs.ch15in, cs.ch16in,
    };
    int ok = 0;
    for (int a = 0; a < 16; a++) {
      _trim[a] = Math.Min(Math.Max(center[a], _min[a]), _max[a]);
      bool valid = _min[a] < _max[a] && _min[a] != 0 && _max[a] != 0 &&
                   _trim[a] <= _max[a] && _trim[a] >= _min[a] && _trim[a] != 0;
      if (!valid) {
        continue;
      }
      int n = a + 1;
      float mn = _min[a], mx = _max[a], tr = _trim[a];
      if (await Task.Run(() => _comPort.setParam($"RC{n}_MIN", mn, true))) {
        ok++;
      }
      await Task.Run(() => _comPort.setParam($"RC{n}_MAX", mx, true));
      await Task.Run(() => _comPort.setParam($"RC{n}_TRIM", tr, true));
    }

    try {
      _comPort.requestDatastream(MAVLink.MAV_DATA_STREAM.RC_CHANNELS, 2);
    } catch { }
    Status = $"Calibration written ({ok} channels). Normal values ~1100 | 1900.";
  }

  public void Dispose() => _timer.Stop();
}

public partial class RcChannel : ObservableObject {
  public RcChannel(int number) {
    Number = number;
    Label = $"Ch {number}";
  }

  public int Number { get; }

  [ObservableProperty] private string _label;
  [ObservableProperty] private int _value;
  [ObservableProperty] private int _min = 1500;
  [ObservableProperty] private int _max = 1500;
}
