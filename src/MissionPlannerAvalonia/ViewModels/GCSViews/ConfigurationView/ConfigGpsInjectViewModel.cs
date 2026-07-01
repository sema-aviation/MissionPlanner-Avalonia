using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigGpsInjectViewModel : ViewModelBase, IDisposable {
  public const string NtripOption = "NTRIP";

  private static readonly IBrush _seenBrush = Brushes.ForestGreen;
  private static readonly IBrush _idleBrush = Brushes.Firebrick;

  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private readonly rtcm3 _rtcm3 = new();
  private readonly Ubx _ubx = new();
  private readonly Dictionary<string, int> _msgSeen = new();
  private readonly object _msgLock = new();

  private ICommsSerial? _comm;
  private Thread? _worker;
  private volatile bool _running;
  private bool _autoConfigUbx;

  private long _bytesIn;
  private long _bytesUseful;
  private long _bytesTotal;

  private DateTime _baseSeen = DateTime.MinValue;
  private DateTime _gpsSeen = DateTime.MinValue;
  private DateTime _glonassSeen = DateTime.MinValue;
  private DateTime _beidouSeen = DateTime.MinValue;
  private DateTime _galileoSeen = DateTime.MinValue;

  private PointLatLngAlt _basePos = PointLatLngAlt.Zero;

  [ObservableProperty]
  private ObservableCollection<string> _ports = new();

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsNtrip))]
  [NotifyPropertyChangedFor(nameof(IsSerial))]
  private string _selectedPort = NtripOption;

  [ObservableProperty]
  private ObservableCollection<string> _baudRates =
      new() { "4800", "9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600" };

  [ObservableProperty]
  private string _selectedBaud = "115200";

  [ObservableProperty]
  private ObservableCollection<string> _receiverTypes =
      new() { "UBlox M8P/F9P", "Septentrio", "Unicore UM982" };

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsSeptentrio))]
  private string _selectedReceiverType = "UBlox M8P/F9P";

  [ObservableProperty]
  private string _host = "";

  [ObservableProperty]
  private int _port = 2101;

  [ObservableProperty]
  private string _mount = "";

  [ObservableProperty]
  private string _username = "";

  [ObservableProperty]
  private string _password = "";

  [ObservableProperty]
  private bool _ntripV1;

  [ObservableProperty]
  private bool _sendGga = true;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsSeptentrio))]
  private bool _autoConfig;

  [ObservableProperty]
  private bool _m8p130Plus = true;

  [ObservableProperty]
  private string _surveyInAcc = "2";

  [ObservableProperty]
  private string _surveyInTime = "60";

  [ObservableProperty]
  private ObservableCollection<string> _septentrioRtcmLevels =
      new() { "Lite", "Basic", "Full" };

  [ObservableProperty]
  private string _selectedSeptentrioRtcmLevel = "Basic";

  [ObservableProperty]
  private bool _septentrioGps = true;

  [ObservableProperty]
  private bool _septentrioGlonass = true;

  [ObservableProperty]
  private bool _septentrioGalileo = true;

  [ObservableProperty]
  private bool _septentrioBeidou = true;

  [ObservableProperty]
  private string _septentrioRtcmInterval = "1.0";

  [ObservableProperty]
  private bool _septentrioFixedPosition;

  [ObservableProperty]
  private string _septentrioLat = "0";

  [ObservableProperty]
  private string _septentrioLng = "0";

  [ObservableProperty]
  private string _septentrioAlt = "0";

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ConnectLabel))]
  private bool _connected;

  [ObservableProperty]
  private string _status = "Select a serial port or NTRIP and press Connect.";

  [ObservableProperty]
  private string _injected = "0 bytes";

  [ObservableProperty]
  private string _inputRate = "0 bps";

  [ObservableProperty]
  private string _outputRate = "0 bps";

  [ObservableProperty]
  private string _messagesSeen = "";

  [ObservableProperty]
  private string _rtcmBasePos = "";

  [ObservableProperty]
  private string _surveyInStatus = "Survey In: not started";

  [ObservableProperty]
  private IBrush _surveyInColor = _idleBrush;

  [ObservableProperty]
  private IBrush _baseColor = _idleBrush;

  [ObservableProperty]
  private IBrush _gpsColor = _idleBrush;

  [ObservableProperty]
  private IBrush _glonassColor = _idleBrush;

  [ObservableProperty]
  private IBrush _beidouColor = _idleBrush;

  [ObservableProperty]
  private IBrush _galileoColor = _idleBrush;

  [ObservableProperty]
  private ObservableCollection<BasePosRow> _basePositions = new();

  public bool IsNtrip => SelectedPort == NtripOption;
  public bool IsSerial => !IsNtrip;
  public bool IsSeptentrio => AutoConfig && SelectedReceiverType == "Septentrio";
  public string ConnectLabel => Connected ? "Disconnect" : "Connect";

  public ConfigGpsInjectViewModel() {
    RefreshPorts();
    LoadBasePosList();
    LoadActiveBasePos();
    LoadSeptentrioSettings();
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
    _timer.Tick += (_, _) => UpdateStats();
    _timer.Start();
  }

  [RelayCommand]
  private void RefreshPorts() {
    var existing = SelectedPort;
    var ports = new ObservableCollection<string>();
    try {
      foreach (var name in SerialPort.GetPortNames()) {
        ports.Add(name);
      }
    } catch {
    }
    ports.Add(NtripOption);
    Ports = ports;
    if (ports.Contains(existing)) {
      SelectedPort = existing;
    }
  }

  [RelayCommand]
  [Obsolete]
  private void ToggleConnect() {
    if (Connected) {
      StopWorker();
      Status = "Disconnected.";
      Connected = false;
      return;
    }

    ICommsSerial comm;
    try {
      if (IsNtrip) {
        if (string.IsNullOrWhiteSpace(Host)) {
          Status = "NTRIP host is required.";
          return;
        }
        var ntrip = new CommsNTRIP { ntrip_v1 = NtripV1 };
        if (SendGga) {
          var cs = _comPort.MAV.cs;
          ntrip.lat = cs.lat;
          ntrip.lng = cs.lng;
          ntrip.alt = cs.altasl;
        }
        ntrip.Open(BuildUrl());
        comm = ntrip;
      } else {
        if (string.IsNullOrWhiteSpace(SelectedPort)) {
          Status = "Select a serial port.";
          return;
        }
        var sp = new SerialPort { PortName = SelectedPort };
        sp.BaudRate = int.Parse(SelectedBaud, CultureInfo.InvariantCulture);
        sp.ReadBufferSize = 1024 * 64;
        sp.Open();
        comm = sp;
      }
    } catch (Exception ex) {
      Status = "Connect failed: " + ex.Message;
      return;
    }

    _comm = comm;
    _autoConfigUbx = AutoConfig && SelectedReceiverType == "UBlox M8P/F9P" && comm is SerialPort;

    if (_autoConfigUbx) {
      try {
        _ubx.SetupM8P(comm, M8p130Plus);
        if (_basePos != PointLatLngAlt.Zero) {
          _ubx.SetupBasePos(comm, _basePos, 0, 0, true);
          _ubx.SetupBasePos(comm, _basePos, 0, 0, false);
        }
        Status = "Connected — UBlox configured, injecting RTCM.";
      } catch (Exception ex) {

        Status = "Connected (auto-config failed: " + ex.Message + ").";
      }
    } else if (AutoConfig && SelectedReceiverType == "Septentrio" && comm is SerialPort) {
      ConfigureSeptentrio(comm);
    } else if (AutoConfig && SelectedReceiverType != "UBlox M8P/F9P") {
      Status = "Connected. Auto-config for " + SelectedReceiverType +
               " is not supported here; inject only.";
    } else {
      Status = "Connected — injecting RTCM to vehicle.";
    }

    PersistConnectSettings();

    _bytesTotal = 0;
    Interlocked.Exchange(ref _bytesIn, 0);
    Interlocked.Exchange(ref _bytesUseful, 0);
    lock (_msgLock) {
      _msgSeen.Clear();
    }
    _rtcm3.resetParser();
    _ubx.resetParser();
    Connected = true;
    StartWorker();
  }

  private string BuildUrl() {
    var userinfo = "";
    if (!string.IsNullOrEmpty(Username)) {
      userinfo = Username;
      if (!string.IsNullOrEmpty(Password)) {
        userinfo += ":" + Password;
      }
      userinfo += "@";
    }
    var mount = Mount?.TrimStart('/') ?? "";
    return $"http://{userinfo}{Host}:{Port}/{mount}";
  }

  [Obsolete]
  private void StartWorker() {
    _running = true;
    _worker = new Thread(Loop) { IsBackground = true, Name = "RTK inject" };
    _worker.Start();
  }

  private void StopWorker() {
    _running = false;
    try {
      _worker?.Join(1000);
    } catch {
    }
    _worker = null;
    try {
      _comm?.Close();
    } catch {
    }
    _comm = null;
  }

  [Obsolete]
  private void Loop() {
    var buffer = new byte[180];
    var lastRecv = DateTime.Now;
    var isRtcm = false;

    while (_running) {
      var comm = _comm;
      if (comm == null) {
        break;
      }

      try {
        if ((DateTime.Now - lastRecv).TotalSeconds > 10 || !comm.IsOpen) {
          if (comm is CommsNTRIP) {
            if (!comm.IsOpen) {
              Fail("NTRIP connection closed.");
              break;
            }
          } else {
            try {
              comm.Close();
              comm.Open();
            } catch {
            }
          }
          lastRecv = DateTime.Now;
        }

        while (comm.BytesToRead > 0 && _running) {
          var read = comm.Read(buffer, 0, Math.Min(buffer.Length, comm.BytesToRead));
          if (read <= 0) {
            break;
          }
          lastRecv = DateTime.Now;
          Interlocked.Add(ref _bytesIn, read);
          _bytesTotal += read;

          if (!isRtcm) {
            SendData(buffer, (ushort)read);
          }

          for (var a = 0; a < read; a++) {
            var seen = _rtcm3.Read(buffer[a]);
            if (seen > 0) {
              _ubx.resetParser();
              isRtcm = true;
              SendData(_rtcm3.packet, (ushort)_rtcm3.length);
              Interlocked.Add(ref _bytesUseful, _rtcm3.length);
              Count("Rtcm" + seen);
              ExtractBasePos(seen);
              SeenRtcm(seen);
              continue;
            }

            if (_autoConfigUbx) {
              var useen = _ubx.Read(buffer[a]);
              if (useen > 0) {
                _rtcm3.resetParser();
                ProcessUbx();
                Count("Ubx" + useen.ToString("X4"));
              }
            }
          }
        }

        Thread.Sleep(10);
      } catch (Exception ex) {
        Fail("Inject error: " + ex.Message);
        break;
      }
    }
  }

  [Obsolete]
  private void SendData(byte[] data, ushort length) {
    if (length == 0) {
      return;
    }
    _comPort.InjectGpsData(data, length);
  }

  private void Count(string name) {
    lock (_msgLock) {
      _msgSeen.TryGetValue(name, out var n);
      _msgSeen[name] = n + 1;
    }
  }

  private void SeenRtcm(int seen) {
    var now = DateTime.Now;
    switch (seen) {
      case 1001:
      case 1002:
      case 1003:
      case 1004:
      case 1071:
      case 1072:
      case 1073:
      case 1074:
      case 1075:
      case 1076:
      case 1077:
        _gpsSeen = now;
        break;
      case 1005:
      case 1006:
      case 4072:
        _baseSeen = now;
        break;
      case 1009:
      case 1010:
      case 1011:
      case 1012:
      case 1081:
      case 1082:
      case 1083:
      case 1084:
      case 1085:
      case 1086:
      case 1087:
        _glonassSeen = now;
        break;
      case 1091:
      case 1092:
      case 1093:
      case 1094:
      case 1095:
      case 1096:
      case 1097:
        _galileoSeen = now;
        break;
      case 1121:
      case 1122:
      case 1123:
      case 1124:
      case 1125:
      case 1126:
      case 1127:
        _beidouSeen = now;
        break;
    }
  }

  private void ExtractBasePos(int seen) {
    try {
      double[]? ecef = null;
      if (seen == 1005) {
        var bp = new rtcm3.type1005();
        bp.Read(_rtcm3.packet);
        ecef = bp.ecefposition;
      } else if (seen == 1006) {
        var bp = new rtcm3.type1006();
        bp.Read(_rtcm3.packet);
        ecef = bp.ecefposition;
      }

      if (ecef == null) {
        return;
      }

      var llh = new double[3];
      rtcm3.ecef2pos(ecef, ref llh);
      var lat = llh[0] * rtcm3.R2D;
      var lng = llh[1] * rtcm3.R2D;
      var alt = llh[2];

      _comPort.MAV.cs.Base = new PointLatLngAlt(lat, lng, alt);

      Dispatcher.UIThread.Post(() =>
          RtcmBasePos = string.Format(CultureInfo.InvariantCulture,
              "{0:0.0000000} {1:0.0000000} {2:0.00} - {3:HH:mm:ss}", lat, lng, alt, DateTime.Now));
    } catch {
    }
  }

  private void ProcessUbx() {
    try {
      if (_ubx.@class == 0x1 && _ubx.subclass == 0x3b) {
        var svin = _ubx.packet.ByteArrayToStructure<Ubx.ubx_nav_svin>(6);
        var acc = svin.meanAcc / 10000.0;
        var valid = svin.valid == 1;
        var active = svin.active == 1;

        string text;
        if (valid) {
          var llh = new double[3];
          rtcm3.ecef2pos(svin.getECEF(), ref llh);
          text = string.Format(CultureInfo.InvariantCulture,
              "Survey In: valid  Lat {0:0.0000000} Lng {1:0.0000000} Alt {2:0.00}  Acc {3:0.00}m",
              llh[0] * rtcm3.R2D, llh[1] * rtcm3.R2D, llh[2], acc);
        } else {
          text = string.Format(CultureInfo.InvariantCulture,
              "Survey In: {0}  Dur {1}s  Obs {2}  Acc {3:0.00}m",
              active ? "in progress" : "complete", svin.dur, svin.obs, acc);
        }

        Dispatcher.UIThread.Post(() => {
          SurveyInStatus = text;
          SurveyInColor = valid ? _seenBrush : _idleBrush;
        });
      } else if (_ubx.@class == 0x1 && _ubx.subclass == 0x7) {
        var pvt = _ubx.packet.ByteArrayToStructure<Ubx.ubx_nav_pvt>(6);
        if (pvt.fix_type >= 0x3 && (pvt.flags & 1) > 0) {
          _comPort.MAV.cs.Base =
              new PointLatLngAlt(pvt.lat / 1e7, pvt.lon / 1e7, pvt.height / 1000.0);
        }
      }
    } catch {
    }
  }

  private void UpdateStats() {
    var bin = Interlocked.Exchange(ref _bytesIn, 0);
    var buse = Interlocked.Exchange(ref _bytesUseful, 0);
    InputRate = bin + " bps";
    OutputRate = buse + " bps sent";
    Injected = _bytesTotal + " bytes";

    var sb = new StringBuilder();
    lock (_msgLock) {
      foreach (var kv in _msgSeen) {
        sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
      }
    }
    MessagesSeen = sb.ToString();

    var now = DateTime.Now;
    BaseColor = (now - _baseSeen).TotalSeconds < 20 ? _seenBrush : _idleBrush;
    GpsColor = (now - _gpsSeen).TotalSeconds < 5 ? _seenBrush : _idleBrush;
    GlonassColor = (now - _glonassSeen).TotalSeconds < 5 ? _seenBrush : _idleBrush;
    BeidouColor = (now - _beidouSeen).TotalSeconds < 5 ? _seenBrush : _idleBrush;
    GalileoColor = (now - _galileoSeen).TotalSeconds < 5 ? _seenBrush : _idleBrush;
  }

  [RelayCommand]
  private void RestartSurveyIn() {
    _basePos = PointLatLngAlt.Zero;
    SurveyInStatus = "Survey In: restarting";
    SurveyInColor = _idleBrush;
    lock (_msgLock) {
      _msgSeen.Clear();
    }

    var comm = _comm;
    if (comm == null || !comm.IsOpen || comm is not SerialPort) {
      return;
    }

    try {
      _ubx.SetupBasePos(comm, _basePos, 0, 0, true);
      _ubx.SetupM8P(comm, M8p130Plus);
      _ubx.SetupBasePos(comm, _basePos,
          ParseInt(SurveyInTime), ParseDouble(SurveyInAcc), false);
    } catch (Exception ex) {
      Status = "Restart failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private void SaveCurrentPosition() {
    var basepos = _comPort.MAV.cs.Base;
    if (basepos == null || basepos == PointLatLngAlt.Zero) {
      Status = "No valid base position determined by GPS yet.";
      return;
    }

    var name = "Base " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    BasePositions.Add(new BasePosRow {
      Lat = basepos.Lat.ToString(CultureInfo.InvariantCulture),
      Long = basepos.Lng.ToString(CultureInfo.InvariantCulture),
      Alt = basepos.Alt.ToString(CultureInfo.InvariantCulture),
      Name = name,
    });
    SaveBasePosList();
  }

  [RelayCommand]
  private void UseBasePos(BasePosRow? row) {
    if (row == null) {
      return;
    }

    try {
      _basePos = new PointLatLngAlt(ParseDouble(row.Lat), ParseDouble(row.Long),
          ParseDouble(row.Alt), row.Name);
    } catch {
      Status = "Base position row has invalid numbers.";
      return;
    }

    Settings.Instance["base_pos"] = string.Format(CultureInfo.InvariantCulture,
        "{0},{1},{2},{3}", _basePos.Lat, _basePos.Lng, _basePos.Alt, row.Name);

    var comm = _comm;
    if (comm != null && comm.IsOpen && comm is SerialPort) {
      try {
        _ubx.SetupBasePos(comm, _basePos, ParseInt(SurveyInTime), ParseDouble(SurveyInAcc), false);
        _ubx.poll_msg(comm, 0x06, 0x71);
      } catch (Exception ex) {
        Status = "Apply base position failed: " + ex.Message;
        return;
      }
    }

    Status = "Using fixed base position: " + row.Name;
  }

  [RelayCommand]
  private void DeleteBasePos(BasePosRow? row) {
    if (row == null) {
      return;
    }
    BasePositions.Remove(row);
    SaveBasePosList();
  }

  [RelayCommand]
  private void ApplySeptentrioRtcm() {
    var comm = _comm;
    if (comm == null || !comm.IsOpen || comm is not SerialPort) {
      Status = "Connect to a Septentrio receiver on a serial port first.";
      return;
    }
    try {
      ApplySeptentrioRtcmTo(comm);
      Status = "Septentrio RTCM settings updated.";
    } catch (Septentrio.FailedAckException) {
      Status = "Septentrio RTCM configuration failed (no ACK).";
    } catch (Exception ex) {
      Status = "Septentrio RTCM configuration failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private void ApplySeptentrioPosition() {
    var comm = _comm;
    if (comm == null || !comm.IsOpen || comm is not SerialPort) {
      Status = "Connect to a Septentrio receiver on a serial port first.";
      return;
    }
    try {
      ApplySeptentrioPositionTo(comm);
      Status = "Septentrio base position updated.";
    } catch (Septentrio.FailedAckException) {
      Status = "Septentrio fixed position configuration failed (no ACK).";
    } catch (Exception ex) {
      Status = "Septentrio fixed position configuration failed: " + ex.Message;
    }
  }

  private void ConfigureSeptentrio(ICommsSerial comm) {
    try {
      Septentrio.ConfigureBaseReceiver(comm).GetAwaiter().GetResult();
      ApplySeptentrioPositionTo(comm);
      ApplySeptentrioRtcmTo(comm);
      Status = "Connected — Septentrio configured, injecting RTCM.";
    } catch (Septentrio.FailedAckException) {
      Status = "Connected (Septentrio auto-config failed: no ACK).";
    } catch (Exception ex) {
      Status = "Connected (Septentrio auto-config failed: " + ex.Message + ").";
    }
  }

  private void ApplySeptentrioRtcmTo(ICommsSerial comm) {
    var signals = Septentrio.RTCMSignals.None;
    if (SeptentrioGps) {
      signals |= Septentrio.RTCMSignals.Gps;
    }
    if (SeptentrioGlonass) {
      signals |= Septentrio.RTCMSignals.Glonass;
    }
    if (SeptentrioGalileo) {
      signals |= Septentrio.RTCMSignals.Galileo;
    }
    if (SeptentrioBeidou) {
      signals |= Septentrio.RTCMSignals.Beidou;
    }

    var level = SelectedSeptentrioRtcmLevel switch {
      "Lite" => Septentrio.RTCMLevel.Lite,
      "Full" => Septentrio.RTCMLevel.Full,
      _ => Septentrio.RTCMLevel.Basic,
    };

    Septentrio.SetEnabledRTCM(comm, level, signals).GetAwaiter().GetResult();
    Septentrio.SetRTCMInterval(comm, (float)ParseDouble(SeptentrioRtcmInterval))
        .GetAwaiter().GetResult();
    PersistSeptentrioRtcm();
  }

  private void ApplySeptentrioPositionTo(ICommsSerial comm) {
    if (SeptentrioFixedPosition) {
      Septentrio.SetBasePosition(comm, (float)ParseDouble(SeptentrioLat),
          (float)ParseDouble(SeptentrioLng), (float)ParseDouble(SeptentrioAlt))
          .GetAwaiter().GetResult();
    } else {
      Septentrio.SetAutoBasePosition(comm).GetAwaiter().GetResult();
    }
    PersistSeptentrioPosition();
  }

  partial void OnSelectedSeptentrioRtcmLevelChanged(string value) => MaybeApplySeptentrioRtcm();

  partial void OnSeptentrioGpsChanged(bool value) => MaybeApplySeptentrioRtcm();

  partial void OnSeptentrioGlonassChanged(bool value) => MaybeApplySeptentrioRtcm();

  partial void OnSeptentrioGalileoChanged(bool value) => MaybeApplySeptentrioRtcm();

  partial void OnSeptentrioBeidouChanged(bool value) => MaybeApplySeptentrioRtcm();

  private void MaybeApplySeptentrioRtcm() {
    var comm = _comm;
    if (comm == null || !comm.IsOpen || comm is not SerialPort || !IsSeptentrio) {
      return;
    }
    try {
      ApplySeptentrioRtcmTo(comm);
    } catch (Exception ex) {
      Status = "Septentrio RTCM configuration failed: " + ex.Message;
    }
  }

  private void PersistConnectSettings() {
    Settings.Instance["SerialInjectGPS_port"] = SelectedPort;
    Settings.Instance["SerialInjectGPS_baud"] = SelectedBaud;
    Settings.Instance["SerialInjectGPS_autoconfig"] = AutoConfig.ToString();
    Settings.Instance["SerialInjectGPS_AutoConfigType"] = SelectedReceiverType;
    Settings.Instance["SerialInjectGPS_m8p_130p"] = M8p130Plus.ToString();
    Settings.Instance["SerialInjectGPS_SIAcc"] = SurveyInAcc;
    Settings.Instance["SerialInjectGPS_SITime"] = SurveyInTime;
  }

  private void PersistSeptentrioRtcm() {
    Settings.Instance["SerialInjectGPS_SeptentrioRTCMLevel"] =
        SeptentrioRtcmLevels.IndexOf(SelectedSeptentrioRtcmLevel).ToString(CultureInfo.InvariantCulture);
    Settings.Instance["SerialInjectGPS_SeptentrioRTCMInterval"] = SeptentrioRtcmInterval;
    Settings.Instance["SerialInjectGPS_SeptentrioGPS"] = SeptentrioGps.ToString();
    Settings.Instance["SerialInjectGPS_SeptentrioGLONASS"] = SeptentrioGlonass.ToString();
    Settings.Instance["SerialInjectGPS_SeptentrioGalileo"] = SeptentrioGalileo.ToString();
    Settings.Instance["SerialInjectGPS_SeptentrioBeiDou"] = SeptentrioBeidou.ToString();
  }

  private void PersistSeptentrioPosition() {
    Settings.Instance["SerialInjectGPS_SeptentrioFixedPosition"] = SeptentrioFixedPosition.ToString();
    Settings.Instance["SerialInjectGPS_SeptentrioFixedAtitude"] = SeptentrioLat;
    Settings.Instance["SerialInjectGPS_SeptentrioFixedLongitude"] = SeptentrioLng;
    Settings.Instance["SerialInjectGPS_SeptentrioFixedAltitude"] = SeptentrioAlt;
  }

  private void LoadSeptentrioSettings() {
    try {
      var s = Settings.Instance;
      if (s.ContainsKey("SerialInjectGPS_SeptentrioRTCMLevel") &&
          int.TryParse(s["SerialInjectGPS_SeptentrioRTCMLevel"], NumberStyles.Any,
              CultureInfo.InvariantCulture, out var idx) &&
          idx >= 0 && idx < SeptentrioRtcmLevels.Count) {
        SelectedSeptentrioRtcmLevel = SeptentrioRtcmLevels[idx];
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioRTCMInterval")) {
        SeptentrioRtcmInterval = s["SerialInjectGPS_SeptentrioRTCMInterval"];
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioGPS")) {
        SeptentrioGps = bool.Parse(s["SerialInjectGPS_SeptentrioGPS"]);
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioGLONASS")) {
        SeptentrioGlonass = bool.Parse(s["SerialInjectGPS_SeptentrioGLONASS"]);
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioGalileo")) {
        SeptentrioGalileo = bool.Parse(s["SerialInjectGPS_SeptentrioGalileo"]);
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioBeiDou")) {
        SeptentrioBeidou = bool.Parse(s["SerialInjectGPS_SeptentrioBeiDou"]);
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioFixedPosition")) {
        SeptentrioFixedPosition = bool.Parse(s["SerialInjectGPS_SeptentrioFixedPosition"]);
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioFixedAtitude")) {
        SeptentrioLat = s["SerialInjectGPS_SeptentrioFixedAtitude"];
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioFixedLongitude")) {
        SeptentrioLng = s["SerialInjectGPS_SeptentrioFixedLongitude"];
      }
      if (s.ContainsKey("SerialInjectGPS_SeptentrioFixedAltitude")) {
        SeptentrioAlt = s["SerialInjectGPS_SeptentrioFixedAltitude"];
      }
    } catch {
    }
  }

  private void LoadActiveBasePos() {
    try {
      if (Settings.Instance.ContainsKey("base_pos")) {
        var p = Settings.Instance["base_pos"].Split(',');
        _basePos = new PointLatLngAlt(
            double.Parse(p[0], CultureInfo.InvariantCulture),
            double.Parse(p[1], CultureInfo.InvariantCulture),
            double.Parse(p[2], CultureInfo.InvariantCulture),
            p.Length > 3 ? p[3] : "");
      }
    } catch {
      _basePos = PointLatLngAlt.Zero;
    }
  }

  private void LoadBasePosList() {
    try {
      if (!Settings.Instance.ContainsKey("base_pos_list")) {
        return;
      }
      var raw = Settings.Instance["base_pos_list"];
      foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries)) {
        var p = entry.Split(',');
        if (p.Length < 3) {
          continue;
        }
        BasePositions.Add(new BasePosRow {
          Lat = p[0],
          Long = p[1],
          Alt = p[2],
          Name = p.Length > 3 ? p[3] : "",
        });
      }
    } catch {
    }
  }

  private void SaveBasePosList() {
    var sb = new StringBuilder();
    foreach (var row in BasePositions) {
      sb.Append(Sanitize(row.Lat)).Append(',')
        .Append(Sanitize(row.Long)).Append(',')
        .Append(Sanitize(row.Alt)).Append(',')
        .Append(Sanitize(row.Name)).Append(';');
    }
    Settings.Instance["base_pos_list"] = sb.ToString();
  }

  private static string Sanitize(string? s) => (s ?? "").Replace(',', ' ').Replace(';', ' ');

  private static int ParseInt(string s) =>
      int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

  private static double ParseDouble(string s) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

  private void Fail(string message) {
    _running = false;
    Dispatcher.UIThread.Post(() => {
      Status = message;
      Connected = false;
    });
  }

  public void Dispose() {
    _timer.Stop();
    StopWorker();
  }
}

public partial class BasePosRow : ObservableObject {
  [ObservableProperty]
  private string _lat = "";

  [ObservableProperty]
  private string _long = "";

  [ObservableProperty]
  private string _alt = "";

  [ObservableProperty]
  private string _name = "";
}
