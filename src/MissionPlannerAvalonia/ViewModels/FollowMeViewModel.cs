using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

// FollowMe — port of MissionPlanner.Controls.FollowMe (Controls/FollowMe.cs). Drives the vehicle to
// continually chase a moving position by streaming GUIDED-mode setpoints. Two position sources:
//   * a serial NMEA GPS (reads $GPGGA/$GNGGA, mirrors upstream's parser), or
//   * a manually-entered / GCS location (lat/lng typed into the form).
// Each tick it builds a Locationwp and calls comPort.setGuidedModeWP (which under the hood issues a
// SET_POSITION_TARGET_GLOBAL_INT for copter or a guided WP for plane — the same real path upstream
// uses).
public partial class FollowMeViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private ICommsSerial? _gps;
  private Thread? _thread;
  private volatile bool _run;

  private readonly PointLatLngAlt _gotoLocation = new(0, 0, 0, "Goto");
  private bool _guidedSet;

  public FollowMeViewModel() {
    RefreshPorts();
    SelectedBaud = 4800;
    UpdateRateHz = 0.5;
    RelativeAltM = 100;
  }

  public ObservableCollection<string> Ports { get; } = new();

  public ObservableCollection<int> Bauds { get; } = new() {
      4800, 9600, 19200, 38400, 57600, 115200,
  };

  public ObservableCollection<double> Rates { get; } = new() { 0.2, 0.5, 1, 2 };

  // false = manual / GCS location (typed lat/lng); true = serial NMEA GPS.
  [ObservableProperty]
  private bool _useSerialGps;

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud;

  [ObservableProperty]
  private double _updateRateHz;

  [ObservableProperty]
  private double _relativeAltM;

  [ObservableProperty]
  private double _manualLat;

  [ObservableProperty]
  private double _manualLng;

  [ObservableProperty]
  private string _status = "Stopped.";

  [ObservableProperty]
  private string _connectButtonText = "Start";

  [ObservableProperty]
  private string _locationLabel = "";

  public bool IsRunning => _run;

  partial void OnManualLatChanged(double value) => PushManual();
  partial void OnManualLngChanged(double value) => PushManual();

  private void PushManual() {
    if (!UseSerialGps) {
      _gotoLocation.Lat = ManualLat;
      _gotoLocation.Lng = ManualLng;
      _gotoLocation.Alt = RelativeAltM;
      _gotoLocation.Tag = "manual";
    }
  }

  [RelayCommand]
  private void RefreshPorts() {
    var sel = SelectedPort;
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().Distinct()) {
      Ports.Add(p);
    }

    SelectedPort = sel != null && Ports.Contains(sel) ? sel : Ports.FirstOrDefault();
  }

  // Use the current GCS map / typed position immediately as the follow target.
  [RelayCommand]
  private void UseGcsLocation() {
    UseSerialGps = false;
    PushManual();
    Status = $"Target set to {ManualLat:0.0000000}, {ManualLng:0.0000000}.";
  }

  [RelayCommand]
  private void ToggleConnect() {
    if (_run) {
      Stop();
      return;
    }

    if (_comPort.BaseStream?.IsOpen != true) {
      Status = "Connect to a vehicle first.";
      return;
    }

    if (UseSerialGps) {
      if (string.IsNullOrEmpty(SelectedPort)) {
        Status = "Pick a GPS port first.";
        return;
      }

      try {
        _gps = new SerialPort { PortName = SelectedPort, BaudRate = SelectedBaud };
        _gps.BaudRate = SelectedBaud;
        _gps.Open();
      } catch (Exception ex) {
        Stop();
        Status = "Error opening GPS port: " + ex.Message;
        return;
      }
    } else {
      PushManual();
    }

    _guidedSet = false;
    _run = true;
    _thread = new Thread(MainLoop) { IsBackground = true, Name = "followme Input" };
    _thread.Start();
    ConnectButtonText = "Stop";
    Status = UseSerialGps ? $"Following serial GPS on {SelectedPort}." : "Following manual location.";
    OnPropertyChanged(nameof(IsRunning));
  }

  [Obsolete]
  private void MainLoop() {
    DateTime nextSend = DateTime.Now;
    while (_run) {
      try {
        if (UseSerialGps && _gps != null) {
          ParseNmea(_gps.ReadLine());
        }

        if (DateTime.Now > nextSend && _gotoLocation.Lat != 0 && _gotoLocation.Lng != 0) {
          nextSend = DateTime.Now.AddMilliseconds(1000 / Math.Max(0.1, UpdateRateHz));

          var gotohere = new Locationwp {
            id = (ushort)MAVLink.MAV_CMD.WAYPOINT,
            alt = (float)RelativeAltM,
            lat = _gotoLocation.Lat,
            lng = _gotoLocation.Lng,
          };

          string label = $"{_gotoLocation.Lat:0.0000000} {_gotoLocation.Lng:0.0000000} "
              + $"{RelativeAltM:0} {_gotoLocation.Tag}";
          Dispatcher.UIThread.Post(() => LocationLabel = label);

          if (_comPort.BaseStream.IsOpen && !_comPort.giveComport) {
            try {
              // First setpoint forces GUIDED (the method only sends setMode when not already GUIDED,
              // so this does not spam); subsequent ones just update the target like upstream.
              _comPort.setGuidedModeWP(gotohere, !_guidedSet);
              _guidedSet = true;
            } catch {
            }
          }
        }

        if (!UseSerialGps) {
          Thread.Sleep(50);
        }
      } catch {
        Thread.Sleep((int)(1000 / Math.Max(0.1, UpdateRateHz)));
      }
    }
  }

  // Parse a $GPGGA/$GNGGA NMEA sentence into _gotoLocation, mirroring upstream FollowMe (including the
  // 0.60 minutes-unpacking and checksum check).
  private void ParseNmea(string line) {
    if (string.IsNullOrEmpty(line)) {
      return;
    }

    if (!line.StartsWith("$GPGGA") && !line.StartsWith("$GNGGA")) {
      return;
    }

    string[] items = line.Trim().Split(',', '*');
    if (items.Length < 9) {
      return;
    }

    if (items[items.Length - 1] != GetChecksum(line.Trim())) {
      return;
    }

    if (items[6] == "0") {
      Dispatcher.UIThread.Post(() => Status = "GPS: no fix.");
      return;
    }

    double lat = double.Parse(items[2], CultureInfo.InvariantCulture) / 100.0;
    lat = (int)lat + (lat - (int)lat) / 0.60;
    if (items[3] == "S") {
      lat *= -1;
    }

    double lng = double.Parse(items[4], CultureInfo.InvariantCulture) / 100.0;
    lng = (int)lng + (lng - (int)lng) / 0.60;
    if (items[5] == "W") {
      lng *= -1;
    }

    _gotoLocation.Lat = lat;
    _gotoLocation.Lng = lng;
    _gotoLocation.Alt = RelativeAltM;
    _gotoLocation.Tag = "Sats " + items[7] + " hdop " + items[8];
  }

  private static string GetChecksum(string sentence) {
    int checksum = 0;
    foreach (char c in sentence) {
      if (c == '$') {
        continue;
      }

      if (c == '*') {
        return checksum.ToString("X2");
      }

      checksum = checksum == 0 ? Convert.ToByte(c) : checksum ^ Convert.ToByte(c);
    }

    return checksum.ToString("X2");
  }

  private void Stop() {
    _run = false;
    try {
      _thread?.Join(500);
    } catch {
    }

    _thread = null;

    try {
      if (_gps != null && _gps.IsOpen) {
        _gps.Close();
      }
    } catch {
    }

    _gps = null;
    ConnectButtonText = "Start";
    Status = "Stopped.";
    OnPropertyChanged(nameof(IsRunning));
  }

  public void Dispose() => Stop();
}
