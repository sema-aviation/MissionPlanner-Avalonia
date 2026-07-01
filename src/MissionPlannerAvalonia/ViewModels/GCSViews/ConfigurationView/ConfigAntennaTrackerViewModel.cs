using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigAntennaTrackerViewModel : ViewModelBase, IDisposable {
  private const string _keyPrefix = "Tracker_";

  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private MaestroDriver? _tracker;
  private CancellationTokenSource? _loopCts;

  public ObservableCollection<string> Interfaces { get; } = new() { "Maestro", "ArduTracker" };
  public ObservableCollection<string> Ports { get; } = new();
  public ObservableCollection<string> Bauds { get; } =
      new() { "4800", "9600", "14400", "19200", "28800", "38400", "57600", "115200" };

  [ObservableProperty]
  private string _selectedInterface = "Maestro";

  [ObservableProperty]
  private string _selectedPort = "";

  [ObservableProperty]
  private string _selectedBaud = "9600";

  [ObservableProperty]
  private string _connectText = "Connect";

  [ObservableProperty]
  private bool _controlsEnabled = true;

  [ObservableProperty]
  private string _status = "";

  [ObservableProperty]
  private string _panRange = "360";

  [ObservableProperty]
  private string _panPwmRange = "1000";

  [ObservableProperty]
  private string _panCenter = "1500";

  [ObservableProperty]
  private string _panSpeed = "100";

  [ObservableProperty]
  private string _panAccel = "5";

  [ObservableProperty]
  private double _panTrim;

  [ObservableProperty]
  private double _panTrimMin = -180;

  [ObservableProperty]
  private double _panTrimMax = 180;

  [ObservableProperty]
  private bool _panReverse;

  [ObservableProperty]
  private string _tiltRange = "90";

  [ObservableProperty]
  private string _tiltPwmRange = "1000";

  [ObservableProperty]
  private string _tiltCenter = "1500";

  [ObservableProperty]
  private string _tiltSpeed = "100";

  [ObservableProperty]
  private string _tiltAccel = "5";

  [ObservableProperty]
  private double _tiltTrim;

  [ObservableProperty]
  private double _tiltTrimMin = -45;

  [ObservableProperty]
  private double _tiltTrimMax = 45;

  [ObservableProperty]
  private bool _tiltReverse;

  public bool IsRunning => _loopCts is { IsCancellationRequested: false };

  public ConfigAntennaTrackerViewModel() {
    LoadSettings();
    RefreshPorts();
    UpdatePanTrimRange();
    UpdateTiltTrimRange();
  }

  public void Activate() {
    RefreshPorts();
    if (IsRunning) {
      ConnectText = "Disconnect";
    }
  }

  public void Deactivate() => SaveSettings();

  private void RefreshPorts() {
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }

    if (!Ports.Contains(SelectedPort)) {
      SelectedPort = Ports.FirstOrDefault() ?? "";
    }
  }

  partial void OnPanRangeChanged(string value) => UpdatePanTrimRange();

  partial void OnTiltRangeChanged(string value) => UpdateTiltTrimRange();

  private void UpdatePanTrimRange() {

    PanTrimMin = -180;
    PanTrimMax = 180;
  }

  private void UpdateTiltTrimRange() {
    int range = ParseInt(TiltRange, 90);
    TiltTrimMin = range / 2 * -1;
    TiltTrimMax = range / 2;
  }

  partial void OnPanTrimChanged(double value) {
    if (_tracker != null) {
      _tracker.TrimPan = value;
    }
  }

  partial void OnTiltTrimChanged(double value) {
    if (_tracker != null) {
      _tracker.TrimTilt = value;
    }
  }

  partial void OnPanSpeedChanged(string value) {
    if (_tracker != null) {
      _tracker.PanSpeed = ParseInt(value, 0);
    }
  }

  partial void OnPanAccelChanged(string value) {
    if (_tracker != null) {
      _tracker.PanAccel = ParseInt(value, 0);
    }
  }

  partial void OnTiltSpeedChanged(string value) {
    if (_tracker != null) {
      _tracker.TiltSpeed = ParseInt(value, 0);
    }
  }

  partial void OnTiltAccelChanged(string value) {
    if (_tracker != null) {
      _tracker.TiltAccel = ParseInt(value, 0);
    }
  }

  [RelayCommand]
  private void Connect() {
    SaveSettings();

    if (IsRunning) {
      StopLoop();
      _tracker?.Close();
      _tracker = null;
      ControlsEnabled = true;
      ConnectText = "Connect";
      Status = "Disconnected.";
      return;
    }

    if (SelectedInterface != "Maestro") {
      Status = SelectedInterface + " is not supported by this build. Use Maestro.";
      return;
    }

    if (string.IsNullOrWhiteSpace(SelectedPort)) {
      Status = "No serial port selected.";
      return;
    }

    var driver = new MaestroDriver();

    try {
      driver.ComPort = new SerialPort {
        PortName = SelectedPort,
        BaudRate = ParseInt(SelectedBaud, 9600),
      };
    } catch (Exception ex) {
      Status = "Error connecting: " + ex.Message;
      return;
    }

    try {
      int panRange = ParseInt(PanRange, 360);
      driver.PanStartRange = panRange / 2 * -1;
      driver.PanEndRange = panRange / 2;
      driver.TrimPan = PanTrim;

      int tiltRange = ParseInt(TiltRange, 90);
      driver.TiltStartRange = tiltRange / 2 * -1;
      driver.TiltEndRange = tiltRange / 2;
      driver.TrimTilt = TiltTrim;

      driver.PanReverse = PanReverse;
      driver.TiltReverse = TiltReverse;

      driver.PanPWMRange = ParseInt(PanPwmRange, 1000);
      driver.TiltPWMRange = ParseInt(TiltPwmRange, 1000);

      driver.PanPWMCenter = ParseInt(PanCenter, 1500);
      driver.TiltPWMCenter = ParseInt(TiltCenter, 1500);

      driver.PanSpeed = ParseInt(PanSpeed, 100);
      driver.PanAccel = ParseInt(PanAccel, 5);
      driver.TiltSpeed = ParseInt(TiltSpeed, 100);
      driver.TiltAccel = ParseInt(TiltAccel, 5);
    } catch (Exception ex) {
      Status = "Invalid number entered: " + ex.Message;
      return;
    }

    if (!driver.Init(out var err)) {
      Status = err;
      return;
    }

    driver.Setup();

    try {
      driver.PanAndTilt(0, 0);
    } catch (Exception ex) {
      Status = "Failed to set initial pan and tilt: " + ex.Message;
      driver.Close();
      return;
    }

    _tracker = driver;
    ControlsEnabled = false;
    ConnectText = "Disconnect";
    Status = "Connected.";
    StartLoop();
  }

  [RelayCommand]
  private async Task FindTrimPan() {
    if (!IsRunning) {
      Status = "Connect to the tracker first.";
      return;
    }

    float snr = _comPort.MAV.cs.localsnrdb;
    if (snr == 0) {
      Status = "No valid SiK radio detected.";
      return;
    }

    Status = "Searching for best pan trim...";

    await Task.Run(() => {
      float pan = (float)PanTrim;
      float panRange = ParseInt(PanRange, 360);

      float ans = CheckPos(pan - panRange / 4, pan + panRange / 4 - 1, 30);
      ans = CheckPos(-30 + ans, 30 + ans, 5);
      ans = CheckPos(-5 + ans, 5 + ans, 1);

      SetPan(ans);
    });

    Status = "Pan trim search complete.";
  }

  private float CheckPos(float start, float end, float scale) {
    float lastsnr = 0;
    float best = 0;

    SetPan(start);
    Thread.Sleep(4000);

    for (float n = start; n < end; n += scale) {
      SetPan(n);
      Thread.Sleep(2000);

      float snr = _comPort.MAV.cs.localsnrdb;
      if (snr > lastsnr) {
        best = n;
        lastsnr = snr;
      }
    }

    return best;
  }

  private void SetPan(float angle) =>
      Dispatcher.UIThread.Post(() => PanTrim = angle);

  private void StartLoop() {
    _loopCts = new CancellationTokenSource();
    var token = _loopCts.Token;
    _ = Task.Run(() => {
      while (!token.IsCancellationRequested) {
        try {

          _tracker?.PanAndTilt(_comPort.MAV.cs.AZToMAV, _comPort.MAV.cs.ELToMAV);
        } catch {
        }

        Thread.Sleep(100);
      }
    }, token);
  }

  private void StopLoop() {
    _loopCts?.Cancel();
    _loopCts = null;
  }

  private void LoadSettings() {
    SelectedInterface = Get("CMB_interface", SelectedInterface);
    SelectedPort = Get("CMB_serialport", SelectedPort);
    SelectedBaud = Get("CMB_baudrate", SelectedBaud);

    PanRange = Get("TXT_panrange", PanRange);
    PanPwmRange = Get("TXT_pwmrangepan", PanPwmRange);
    PanCenter = Get("TXT_centerpan", PanCenter);
    PanSpeed = Get("TXT_panspeed", PanSpeed);
    PanAccel = Get("TXT_panaccel", PanAccel);

    TiltRange = Get("TXT_tiltrange", TiltRange);
    TiltPwmRange = Get("TXT_pwmrangetilt", TiltPwmRange);
    TiltCenter = Get("TXT_centertilt", TiltCenter);
    TiltSpeed = Get("TXT_tiltspeed", TiltSpeed);
    TiltAccel = Get("TXT_tiltaccel", TiltAccel);

    PanTrim = Settings.Instance.GetInt32(_keyPrefix + "TRK_pantrim", 0);
    TiltTrim = Settings.Instance.GetInt32(_keyPrefix + "TRK_tilttrim", 0);
    PanReverse = Settings.Instance.GetBoolean(_keyPrefix + "CHK_revpan", false);
    TiltReverse = Settings.Instance.GetBoolean(_keyPrefix + "CHK_revtilt", false);
  }

  private void SaveSettings() {
    Set("CMB_interface", SelectedInterface);
    Set("CMB_serialport", SelectedPort);
    Set("CMB_baudrate", SelectedBaud);

    Set("TXT_panrange", PanRange);
    Set("TXT_pwmrangepan", PanPwmRange);
    Set("TXT_centerpan", PanCenter);
    Set("TXT_panspeed", PanSpeed);
    Set("TXT_panaccel", PanAccel);

    Set("TXT_tiltrange", TiltRange);
    Set("TXT_pwmrangetilt", TiltPwmRange);
    Set("TXT_centertilt", TiltCenter);
    Set("TXT_tiltspeed", TiltSpeed);
    Set("TXT_tiltaccel", TiltAccel);

    Set("TRK_pantrim", ((int)PanTrim).ToString(CultureInfo.InvariantCulture));
    Set("TRK_tilttrim", ((int)TiltTrim).ToString(CultureInfo.InvariantCulture));
    Set("CHK_revpan", PanReverse.ToString());
    Set("CHK_revtilt", TiltReverse.ToString());
  }

  private static string Get(string name, string fallback) {
    var key = _keyPrefix + name;
    return Settings.Instance.ContainsKey(key) && Settings.Instance[key] != null
        ? Settings.Instance[key]
        : fallback;
  }

  private static void Set(string name, string value) =>
      Settings.Instance[_keyPrefix + name] = value;

  private static int ParseInt(string value, int fallback) =>
      int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

  public void Dispose() {
    SaveSettings();
    StopLoop();
    _tracker?.Close();
    _tracker = null;
  }

  private sealed class MaestroDriver {
    private const byte _setTarget = 0x84;
    private const byte _setSpeed = 0x87;
    private const byte _setAccel = 0x89;

    private const byte _panAddress = 0;
    private const byte _tiltAddress = 1;

    private int _panReverse = 1;
    private int _tiltReverse = 1;

    public SerialPort ComPort { get; set; } = null!;

    public double TrimPan { get; set; }
    public double TrimTilt { get; set; }

    public int PanStartRange { get; set; }
    public int TiltStartRange { get; set; }
    public int PanEndRange { get; set; }
    public int TiltEndRange { get; set; }
    public int PanPWMRange { get; set; }
    public int TiltPWMRange { get; set; }
    public int PanPWMCenter { get; set; }
    public int TiltPWMCenter { get; set; }
    public int PanSpeed { get; set; }
    public int TiltSpeed { get; set; }
    public int PanAccel { get; set; }
    public int TiltAccel { get; set; }

    public bool PanReverse {
      get => _panReverse == -1;
      set => _panReverse = value ? -1 : 1;
    }

    public bool TiltReverse {
      get => _tiltReverse == -1;
      set => _tiltReverse = value ? -1 : 1;
    }

    public bool Init(out string error) {
      error = "";

      if (PanStartRange - PanEndRange == 0) {
        error = "Invalid pan range.";
        return false;
      }

      if (TiltStartRange - TiltEndRange == 0) {
        error = "Invalid tilt range.";
        return false;
      }

      try {
        ComPort.Open();
      } catch (Exception ex) {
        error = "Error connecting: " + ex.Message;
        return false;
      }

      return true;
    }

    public bool Setup() {
      SendCompactCommand(_setSpeed, _panAddress, PanSpeed);
      SendCompactCommand(_setSpeed, _tiltAddress, TiltSpeed);
      SendCompactCommand(_setAccel, _panAddress, PanAccel);
      SendCompactCommand(_setAccel, _tiltAddress, TiltAccel);
      return true;
    }

    public bool Pan(double angle) {
      double angleRange = Math.Abs(PanStartRange - PanEndRange);
      double pulseWidth =
          PanPWMRange / angleRange * Wrap180(angle - TrimPan) * _panReverse + PanPWMCenter;
      short target =
          Constrain(pulseWidth, PanPWMCenter - PanPWMRange / 2.0, PanPWMCenter + PanPWMRange / 2.0);
      target *= 4;
      SendCompactCommand(_setTarget, _panAddress, target);
      return true;
    }

    public bool Tilt(double angle) {
      double angleRange = Math.Abs(TiltStartRange - TiltEndRange);
      double pulseWidth =
          TiltPWMRange / angleRange * (angle - TrimTilt) * _tiltReverse + TiltPWMCenter;
      short target = Constrain(pulseWidth, TiltPWMCenter - TiltPWMRange / 2.0,
          TiltPWMCenter + TiltPWMRange / 2.0);
      target *= 4;
      SendCompactCommand(_setTarget, _tiltAddress, target);
      return true;
    }

    public bool PanAndTilt(double pan, double tilt) {
      if (Math.Abs(TiltStartRange - TiltEndRange) > 120) {
        double target = Wrap180(pan - TrimPan);
        if (Math.Abs(target) > 90) {
          return Tilt(180 - tilt) && Pan(target);
        }

        return Tilt(tilt) && Pan(pan);
      }

      return Tilt(tilt) && Pan(pan);
    }

    public void Close() {
      try {
        ComPort?.Close();
      } catch {
      }
    }

    private static double Wrap180(double input) {
      if (input > 180) {
        return input - 360;
      }

      if (input < -180) {
        return input + 360;
      }

      return input;
    }

    private static short Constrain(double input, double min, double max) {
      if (input < min) {
        return (short)min;
      }

      if (input > max) {
        return (short)max;
      }

      return (short)input;
    }

    private void SendCompactCommand(byte cmd, byte addr, int data) {
      byte[] buffer = { cmd, addr, (byte)(data & 0x7F), (byte)((data >> 7) & 0x7F) };
      ComPort.DiscardInBuffer();
      ComPort.Write(buffer, 0, buffer.Length);
    }
  }
}
