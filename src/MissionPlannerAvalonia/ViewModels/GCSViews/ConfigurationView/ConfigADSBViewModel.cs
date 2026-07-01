using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigADSBViewModel : ParamPageBase {
  private static readonly HashSet<string> _bitmaskParams = new(StringComparer.OrdinalIgnoreCase) {
    "ADSB_OPTIONS",
    "ADSB_RF_CAPABLE",
    "ADSB_RF_SELECT",
  };

  public ObservableCollection<ParamField> FilteredFields { get; } = new();

  [ObservableProperty]
  private string _search = "";

  [ObservableProperty]
  private string _status = "";

  [ObservableProperty]
  private string _flightId = "";

  [ObservableProperty]
  private string _aircraftRegistration = "";

  public ConfigADSBViewModel() {
    Title = "ADSB";
    Intro = "ADS-B receiver / avoidance. Populated on connect.";
    Setup();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
  }

  partial void OnSearchChanged(string value) {
    ApplyFilter();
  }

  private void Setup() {
    foreach (var key in comPort.MAV.param.Keys.ToList()
                 .Where(k => k.StartsWith("ADSB_", StringComparison.OrdinalIgnoreCase) ||
                             k.StartsWith("AVD_", StringComparison.OrdinalIgnoreCase))
                 .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)) {
      F(key, _bitmaskParams.Contains(key) ? "bitmask" : null);
    }
    ApplyFilter();
  }

  private void ApplyFilter() {
    FilteredFields.Clear();
    var term = Search?.Trim() ?? "";
    foreach (var f in Fields) {
      if (term.Length < 2 ||
          f.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
          f.Label.Contains(term, StringComparison.OrdinalIgnoreCase) ||
          f.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) {
        FilteredFields.Add(f);
      }
    }
  }

  [RelayCommand]
  private async Task SaveFlightId() {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      var flid = new MAVLink.mavlink_uavionix_adsb_out_cfg_flightid_t(FlightId.MakeBytesSize(9));
      await Task.Run(() => {
        comPort.sendPacket(flid, comPort.sysidcurrent, comPort.compidcurrent);
        System.Threading.Thread.Sleep(200);
        comPort.sendPacket(flid, comPort.sysidcurrent, comPort.compidcurrent);
        System.Threading.Thread.Sleep(200);
        comPort.generatePacket(MAVLink.MAVLINK_MSG_ID.UAVIONIX_ADSB_GET,
            new MAVLink.mavlink_uavionix_adsb_get_t(10005), comPort.sysidcurrent,
            comPort.compidcurrent);
      });
      Status = "Flight ID sent.";
    } catch (Exception ex) {
      Status = "send failed: " + ex.Message;
    }
  }

  [RelayCommand]
  private async Task SaveAircraftRegistration() {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      var acreg = new MAVLink.mavlink_uavionix_adsb_out_cfg_registration_t(
          AircraftRegistration.MakeBytesSize(9));
      await Task.Run(() => {
        comPort.sendPacket(acreg, comPort.sysidcurrent, comPort.compidcurrent);
        System.Threading.Thread.Sleep(200);
        comPort.sendPacket(acreg, comPort.sysidcurrent, comPort.compidcurrent);
        System.Threading.Thread.Sleep(200);
        comPort.generatePacket(MAVLink.MAVLINK_MSG_ID.UAVIONIX_ADSB_GET,
            new MAVLink.mavlink_uavionix_adsb_get_t(10004), comPort.sysidcurrent,
            comPort.compidcurrent);
      });
      Status = "Aircraft registration sent.";
    } catch (Exception ex) {
      Status = "send failed: " + ex.Message;
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task Write() {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }

    var ordered = Fields
        .OrderBy(f => f.Name.Contains("ENABLE", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
        .ToList();

    bool error = false;
    foreach (var f in ordered) {
      if (!f.Exists) {
        continue;
      }
      try {
        var name = f.Name;
        var value = f.Value;
        if (!await Task.Run(() => comPort.setParam(name, value))) {
          error = true;
        }
      } catch {
        error = true;
      }
    }

    Status = error ? "write failed" : "Parameters successfully saved.";
  }
}
