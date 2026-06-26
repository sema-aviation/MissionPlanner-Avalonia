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

namespace MissionPlannerAvalonia.ViewModels;

// Standalone Antenna Tracker tool (mirrors MissionPlanner.Antenna.TrackerUI + TrackerGeneric).
// Distinct from the Config > Antenna Tracker page: this is a free-floating window that connects
// to a pan/tilt tracker over serial with three selectable backends (Maestro / ArduTracker /
// DegreeTracker), shows live azimuth/elevation, and supports auto point-at-vehicle, manual slew,
// home/center, and the Find Trim Pan SiK sweep.
public partial class AntennaTrackerUIViewModel : ViewModelBase, IDisposable {
  private const string KeyPrefix = "Tracker_";

  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private ITrackerOutput? _tracker;
  private CancellationTokenSource? _loopCts;

  // interfaces enum mirrors TrackerGeneric.interfaces
  public ObservableCollection<string> Interfaces { get; } =
      new() { "Maestro", "ArduTracker", "DegreeTracker" };

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

  // Maestro speed/accel fields only apply to the Maestro backend.
  [ObservableProperty]
  private bool _speedAccelEnabled = true;

  // Pan group
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

  // Tilt group
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

  // Manual slew vs. auto point-at-vehicle.
  [ObservableProperty]
  private bool _manualMode;

  [ObservableProperty]
  private double _manualAzimuth;

  [ObservableProperty]
  private double _manualElevation;

  // Live readout (degrees).
  [ObservableProperty]
  private string _vehicleAzimuth = "--";

  [ObservableProperty]
  private string _vehicleElevation = "--";

  [ObservableProperty]
  private string _commandedAzimuth = "--";

  [ObservableProperty]
  private string _commandedElevation = "--";

  public bool IsRunning => _loopCts is { IsCancellationRequested: false };

  public AntennaTrackerUIViewModel() {
    LoadSettings();
    RefreshPorts();
    UpdatePanTrimRange();
    UpdateTiltTrimRange();
    UpdateSpeedAccelEnabled();
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

  partial void OnSelectedInterfaceChanged(string value) => UpdateSpeedAccelEnabled();

  // Mirrors CMB_interface_SelectedIndexChanged: speed/accel are Maestro-only.
  private void UpdateSpeedAccelEnabled() => SpeedAccelEnabled = SelectedInterface == "Maestro";

  partial void OnPanRangeChanged(string value) => UpdatePanTrimRange();

  partial void OnTiltRangeChanged(string value) => UpdateTiltTrimRange();

  private void UpdatePanTrimRange() {
    // Upstream forces the pan trim slider to a full 360 sweep.
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

    if (string.IsNullOrWhiteSpace(SelectedPort)) {
      Status = "No serial port selected.";
      return;
    }

    ITrackerOutput driver = SelectedInterface switch {
      "ArduTracker" => new ArduTrackerDriver(),
      "DegreeTracker" => new DegreeTrackerDriver(),
      _ => new MaestroDriver(),
    };

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

    // reflect any center the driver clamped to
    PanCenter = driver.PanPWMCenter.ToString(CultureInfo.InvariantCulture);
    TiltCenter = driver.TiltPWMCenter.ToString(CultureInfo.InvariantCulture);

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
    Status = "Connected (" + SelectedInterface + ").";
    StartLoop();
  }

  // Point the dish straight at the center / home position.
  [RelayCommand]
  private void HomeCenter() {
    ManualAzimuth = 0;
    ManualElevation = 0;
    if (_tracker != null) {
      try {
        _tracker.PanAndTilt(0, 0);
      } catch (Exception ex) {
        Status = "Center failed: " + ex.Message;
      }
    }
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
          double az;
          double el;
          if (ManualMode) {
            az = ManualAzimuth;
            el = ManualElevation;
          } else {
            // 10 hz - position updates default to 3 hz on the stream rate
            az = _comPort.MAV.cs.AZToMAV;
            el = _comPort.MAV.cs.ELToMAV;
          }

          _tracker?.PanAndTilt(az, el);

          Dispatcher.UIThread.Post(() => {
            VehicleAzimuth = _comPort.MAV.cs.AZToMAV.ToString("0.0", CultureInfo.InvariantCulture);
            VehicleElevation =
                _comPort.MAV.cs.ELToMAV.ToString("0.0", CultureInfo.InvariantCulture);
            CommandedAzimuth = az.ToString("0.0", CultureInfo.InvariantCulture);
            CommandedElevation = el.ToString("0.0", CultureInfo.InvariantCulture);
          });
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

    PanTrim = Settings.Instance.GetInt32(KeyPrefix + "TRK_pantrim", 0);
    TiltTrim = Settings.Instance.GetInt32(KeyPrefix + "TRK_tilttrim", 0);
    PanReverse = Settings.Instance.GetBoolean(KeyPrefix + "CHK_revpan", false);
    TiltReverse = Settings.Instance.GetBoolean(KeyPrefix + "CHK_revtilt", false);
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
    var key = KeyPrefix + name;
    return Settings.Instance.ContainsKey(key) && Settings.Instance[key] != null
        ? Settings.Instance[key]
        : fallback;
  }

  private static void Set(string name, string value) =>
      Settings.Instance[KeyPrefix + name] = value;

  private static int ParseInt(string value, int fallback) =>
      int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v
                                                                                          : fallback;

  public void Dispose() {
    SaveSettings();
    StopLoop();
    _tracker?.Close();
    _tracker = null;
  }

  // ----- Tracker backends (reimplemented from MissionPlanner.Antenna.* because the Antenna
  // ExtLib is not referenced and the project files may not be edited). -----

  // Common output surface (mirrors MissionPlanner.Antenna.ITrackerOutput).
  private interface ITrackerOutput {
    SerialPort ComPort { get; set; }
    double TrimPan { get; set; }
    double TrimTilt { get; set; }
    int PanStartRange { get; set; }
    int TiltStartRange { get; set; }
    int PanEndRange { get; set; }
    int TiltEndRange { get; set; }
    int PanPWMRange { get; set; }
    int TiltPWMRange { get; set; }
    int PanPWMCenter { get; set; }
    int TiltPWMCenter { get; set; }
    int PanSpeed { get; set; }
    int TiltSpeed { get; set; }
    int PanAccel { get; set; }
    int TiltAccel { get; set; }
    bool PanReverse { get; set; }
    bool TiltReverse { get; set; }
    bool Init(out string error);
    bool Setup();
    bool PanAndTilt(double pan, double tilt);
    void Close();
  }

  private abstract class TrackerBase : ITrackerOutput {
    protected int _panReverse = 1;
    protected int _tiltReverse = 1;

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

    public abstract bool PanReverse { get; set; }
    public abstract bool TiltReverse { get; set; }

    public virtual bool Init(out string error) {
      error = "";
      try {
        ComPort.Open();
      } catch (Exception ex) {
        error = "Error connecting: " + ex.Message;
        return false;
      }

      return true;
    }

    public virtual bool Setup() => true;

    public abstract bool PanAndTilt(double pan, double tilt);

    public void Close() {
      try {
        ComPort?.Close();
      } catch {
      }
    }

    protected static double Wrap180(double input) {
      if (input > 180) {
        return input - 360;
      }

      if (input < -180) {
        return input + 360;
      }

      return input;
    }

    protected static short Constrain(double input, double min, double max) {
      if (input < min) {
        return (short)min;
      }

      if (input > max) {
        return (short)max;
      }

      return (short)input;
    }
  }

  // Pololu Maestro compact-protocol backend (ported from MissionPlanner.Antenna.Maestro).
  private sealed class MaestroDriver : TrackerBase {
    private const byte SetTarget = 0x84;
    private const byte SetSpeed = 0x87;
    private const byte SetAccel = 0x89;
    private const byte PanAddress = 0;
    private const byte TiltAddress = 1;

    // Maestro reverse uses 1 == true (matches upstream where -1 flips the output).
    public override bool PanReverse {
      get => _panReverse == -1;
      set => _panReverse = value ? -1 : 1;
    }

    public override bool TiltReverse {
      get => _tiltReverse == -1;
      set => _tiltReverse = value ? -1 : 1;
    }

    public override bool Init(out string error) {
      error = "";
      if (PanStartRange - PanEndRange == 0) {
        error = "Invalid pan range.";
        return false;
      }

      if (TiltStartRange - TiltEndRange == 0) {
        error = "Invalid tilt range.";
        return false;
      }

      return base.Init(out error);
    }

    public override bool Setup() {
      SendCompactCommand(SetSpeed, PanAddress, PanSpeed);
      SendCompactCommand(SetSpeed, TiltAddress, TiltSpeed);
      SendCompactCommand(SetAccel, PanAddress, PanAccel);
      SendCompactCommand(SetAccel, TiltAddress, TiltAccel);
      return true;
    }

    private bool Pan(double angle) {
      double angleRange = Math.Abs(PanStartRange - PanEndRange);
      double pulseWidth =
          PanPWMRange / angleRange * Wrap180(angle - TrimPan) * _panReverse + PanPWMCenter;
      short target =
          Constrain(pulseWidth, PanPWMCenter - PanPWMRange / 2.0, PanPWMCenter + PanPWMRange / 2.0);
      target *= 4;
      SendCompactCommand(SetTarget, PanAddress, target);
      return true;
    }

    private bool Tilt(double angle) {
      double angleRange = Math.Abs(TiltStartRange - TiltEndRange);
      double pulseWidth =
          TiltPWMRange / angleRange * (angle - TrimTilt) * _tiltReverse + TiltPWMCenter;
      short target = Constrain(pulseWidth, TiltPWMCenter - TiltPWMRange / 2.0,
          TiltPWMCenter + TiltPWMRange / 2.0);
      target *= 4;
      SendCompactCommand(SetTarget, TiltAddress, target);
      return true;
    }

    public override bool PanAndTilt(double pan, double tilt) {
      if (Math.Abs(TiltStartRange - TiltEndRange) > 120) {
        double target = Wrap180(pan - TrimPan);
        if (Math.Abs(target) > 90) {
          return Tilt(180 - tilt) && Pan(target);
        }

        return Tilt(tilt) && Pan(pan);
      }

      return Tilt(tilt) && Pan(pan);
    }

    private void SendCompactCommand(byte cmd, byte addr, int data) {
      byte[] buffer = { cmd, addr, (byte)(data & 0x7F), (byte)((data >> 7) & 0x7F) };
      ComPort.DiscardInBuffer();
      ComPort.Write(buffer, 0, buffer.Length);
    }
  }

  // ASCII "!!!PAN:nnnn,TLT:nnnn" PWM backend (ported from MissionPlanner.Antenna.ArduTracker).
  private sealed class ArduTrackerDriver : TrackerBase {
    private int _currentpan = 1500;
    private int _currenttilt = 1500;

    // Note: upstream ArduTracker/Degree use 1 == reverse (opposite of Maestro).
    public override bool PanReverse {
      get => _panReverse == 1;
      set => _panReverse = value ? -1 : 1;
    }

    public override bool TiltReverse {
      get => _tiltReverse == 1;
      set => _tiltReverse = value ? -1 : 1;
    }

    public override bool Init(out string error) {
      error = "";
      if (PanStartRange - PanEndRange == 0) {
        error = "Invalid pan range.";
        return false;
      }

      if (TiltStartRange - TiltEndRange == 0) {
        error = "Invalid tilt range.";
        return false;
      }

      return base.Init(out error);
    }

    private void Pan(double angle) {
      double range = Math.Abs(PanStartRange - PanEndRange);
      short pointAt = Constrain(Wrap180(angle - TrimPan), PanStartRange, PanEndRange);
      _currentpan = (int)(pointAt / range * 2.0 * (PanPWMRange / 2) * _panReverse + PanPWMCenter);
    }

    private void Tilt(double angle) {
      double range = Math.Abs(TiltStartRange - TiltEndRange);
      short pointAt = Constrain(angle - TrimTilt, TiltStartRange, TiltEndRange);
      _currenttilt =
          (int)(pointAt / range * 2.0 * (TiltPWMRange / 2) * _tiltReverse + TiltPWMCenter);
    }

    public override bool PanAndTilt(double pan, double tilt) {
      Tilt(tilt);
      Pan(pan);
      ComPort.Write(string.Format(CultureInfo.InvariantCulture, "!!!PAN:{0:0000},TLT:{1:0000}\n",
          _currentpan, _currenttilt));
      return true;
    }
  }

  // ASCII degree*10 backend (ported from MissionPlanner.Antenna.DegreeTracker).
  private sealed class DegreeTrackerDriver : TrackerBase {
    private int _currentpan = 1500;
    private int _currenttilt = 1500;

    public override bool PanReverse {
      get => _panReverse == 1;
      set => _panReverse = value ? -1 : 1;
    }

    public override bool TiltReverse {
      get => _tiltReverse == 1;
      set => _tiltReverse = value ? -1 : 1;
    }

    public override bool PanAndTilt(double pan, double tilt) {
      _currenttilt = (int)(tilt * 10);
      _currentpan = (int)(pan * 10);
      ComPort.Write(string.Format(CultureInfo.InvariantCulture, "!!!PAN:{0:0000},TLT:{1:0000}\n",
          _currentpan, _currenttilt));
      return true;
    }
  }
}
