using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class FlightPlannerViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<WpRow> Waypoints { get; } = new();

  public ObservableCollection<string> MapTypes { get; } =
      new()
      {
            "GoogleSatelliteMap",
            "GoogleHybridMap",
            "BingSatelliteMap",
            "OpenStreetMap",
            "EsriWorldImagery",
      };

  [ObservableProperty]
  private string _mapType = "GoogleSatelliteMap";

  [ObservableProperty]
  private string _status = "Connect, then Read. Or Load File to preview a mission.";

  [ObservableProperty]
  private double _defaultAlt = 100;

  [ObservableProperty]
  private double _wpRadius = 90;

  [ObservableProperty]
  private double _loiterRadius = 100;

  [ObservableProperty]
  private double _homeLat;

  [ObservableProperty]
  private double _homeLng;

  [ObservableProperty]
  private double _homeAlt;

  [ObservableProperty]
  private bool _showGrid;

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  [RelayCommand]
  [Obsolete]
  private async Task ReadWaypoints() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }
    Status = "Reading mission…";
    try {
      var rows = await Task.Run(() => {
        var list = new List<WpRow>();
        ushort count = _comPort.getWPCount(MAVLink.MAV_MISSION_TYPE.MISSION);
        for (ushort i = 0; i < count; i++) {
          var wp = _comPort.getWP(i, MAVLink.MAV_MISSION_TYPE.MISSION);
          list.Add(WpRow.From(i, wp));
        }
        return list;
      });
      Replace(rows);
      Status = $"Read {rows.Count} waypoint(s).";
    } catch (Exception ex) {
      Status = "Read failed: " + ex.Message;
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task WriteWaypoints() {
    if (!IsConnected) {
      Status = "Not connected — cannot write.";
      return;
    }
    var rows = Waypoints.ToList();
    if (rows.Count == 0) {
      Status = "No waypoints to write.";
      return;
    }
    Status = $"Writing {rows.Count} waypoint(s)…";
    try {
      await Task.Run(() => {
        _comPort.setWPTotal((ushort)rows.Count, MAVLink.MAV_MISSION_TYPE.MISSION);
        for (int i = 0; i < rows.Count; i++) {
          var loc = rows[i].ToLocationwp();
          _comPort.setWP(
              loc,
              (ushort)i,
              MAVLink.MAV_FRAME.GLOBAL_RELATIVE_ALT,
              (byte)(i == 0 ? 1 : 0)
          );
        }
        _comPort.setWPACK(MAVLink.MAV_MISSION_TYPE.MISSION);
      });
      Status = $"Wrote {rows.Count} waypoint(s).";
    } catch (Exception ex) {
      Status = "Write failed: " + ex.Message;
    }
  }

  public async Task SaveFileAsync(string path) {
    try {
      var lines = new List<string> { "QGC WPL 110" };
      for (int i = 0; i < Waypoints.Count; i++) {
        var w = Waypoints[i];
        lines.Add(
            string.Join(
                "\t",
                new[]
                {
                            i.ToString(),
                            i == 0 ? "1" : "0",
                            "3",
                            ((int)w.Command).ToString(),
                            F(w.P1),
                            F(w.P2),
                            F(w.P3),
                            F(w.P4),
                            F(w.Lat),
                            F(w.Lng),
                            F(w.Alt),
                            "1",
                }
            )
        );
      }
      await File.WriteAllLinesAsync(path, lines);
      Status = $"Saved {Waypoints.Count} waypoint(s) to {Path.GetFileName(path)}.";
    } catch (Exception ex) {
      Status = "Save failed: " + ex.Message;
    }
  }

  public async Task LoadFileAsync(string path) {
    try {
      var lines = await File.ReadAllLinesAsync(path);
      var rows = new List<WpRow>();
      foreach (var line in lines.Skip(1)) {
        var t = line.Split('\t');
        if (t.Length < 12) {
          continue;
        }

        rows.Add(
            new WpRow {
              Seq = int.Parse(t[0], CultureInfo.InvariantCulture),
              Command = (ushort)int.Parse(t[3], CultureInfo.InvariantCulture),
              P1 = D(t[4]),
              P2 = D(t[5]),
              P3 = D(t[6]),
              P4 = D(t[7]),
              Lat = D(t[8]),
              Lng = D(t[9]),
              Alt = D(t[10]),
            }
        );
      }
      Replace(rows);
      Status = $"Loaded {rows.Count} waypoint(s) from {Path.GetFileName(path)}.";
    } catch (Exception ex) {
      Status = "Load failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private void AddWaypoint() {
    Waypoints.Add(
        new WpRow {
          Seq = Waypoints.Count,
          Command = (ushort)MAVLink.MAV_CMD.WAYPOINT,
          Alt = DefaultAlt,
          Lat = HomeLat,
          Lng = HomeLng,
        }
    );
    Renumber();
  }

  [RelayCommand]
  private void DeleteWaypoint(WpRow? row) {
    if (row != null) {
      Waypoints.Remove(row);
      Renumber();
    }
  }

  [RelayCommand]
  private async Task SetHomeFromVehicle() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }
    await Task.Yield();
    var cs = _comPort.MAV.cs;
    HomeLat = cs.lat;
    HomeLng = cs.lng;
    HomeAlt = cs.altasl;
    Status = "Home set from vehicle position.";
  }

  private void Replace(IEnumerable<WpRow> rows) {
    void Apply() {
      Waypoints.Clear();
      foreach (var r in rows) {
        Waypoints.Add(r);
      }

      Renumber();
    }
    if (Dispatcher.UIThread.CheckAccess()) {
      Apply();
    } else {
      Dispatcher.UIThread.Post(Apply);
    }
  }

  private void Renumber() {
    for (int i = 0; i < Waypoints.Count; i++) {
      Waypoints[i].Seq = i;
    }
  }

  private static string F(double v) => v.ToString("0.000000", CultureInfo.InvariantCulture);

  private static double D(string s) =>
      double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public partial class WpRow : ObservableObject {
  [ObservableProperty]
  private int _seq;

  [ObservableProperty]
  private ushort _command;

  [ObservableProperty]
  private double _p1;

  [ObservableProperty]
  private double _p2;

  [ObservableProperty]
  private double _p3;

  [ObservableProperty]
  private double _p4;

  [ObservableProperty]
  private double _lat;

  [ObservableProperty]
  private double _lng;

  [ObservableProperty]
  private double _alt;

  public string CommandName => ((MAVLink.MAV_CMD)Command).ToString();

  public static WpRow From(int seq, Locationwp wp) =>
      new() {
        Seq = seq,
        Command = wp.id,
        P1 = wp.p1,
        P2 = wp.p2,
        P3 = wp.p3,
        P4 = wp.p4,
        Lat = wp.lat,
        Lng = wp.lng,
        Alt = wp.alt,
      };

  public Locationwp ToLocationwp() =>
      new() {
        id = Command,
        p1 = (float)P1,
        p2 = (float)P2,
        p3 = (float)P3,
        p4 = (float)P4,
        lat = Lat,
        lng = Lng,
        alt = (float)Alt,
      };
}
