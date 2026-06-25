using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigFirmwareLegacyViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<string> Vehicles { get; } = new();
  public ObservableCollection<string> ReleaseTypes { get; } = new();
  public ObservableCollection<FirmwareItem> Firmwares { get; } = new();

  [ObservableProperty]
  private string? _selectedVehicle;

  [ObservableProperty]
  private string? _selectedReleaseType = "OFFICIAL";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
  private FirmwareItem? _selectedFirmware;

  [ObservableProperty]
  private string _status = "Select a vehicle type and firmware, then click Upload Firmware.";

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  private double _progress;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(UploadCommand))]
  [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
  private bool _busy;

  public ConfigFirmwareLegacyViewModel() {
    foreach (var rt in Enum.GetNames(typeof(APFirmware.RELEASE_TYPES))) {
      ReleaseTypes.Add(rt);
    }
    foreach (var mt in Enum.GetNames(typeof(APFirmware.MAV_TYPE))) {
      Vehicles.Add(mt);
    }
    SelectedVehicle = Vehicles.FirstOrDefault();
    _ = RefreshList();
  }

  partial void OnSelectedVehicleChanged(string? value) => _ = RefreshList();

  partial void OnSelectedReleaseTypeChanged(string? value) => _ = RefreshList();

  [RelayCommand(CanExecute = nameof(CanRefresh))]
  private async Task Refresh() => await RefreshList(true);

  private bool CanRefresh() => !Busy;

  private async Task RefreshList(bool force = false) {
    if (SelectedVehicle == null || SelectedReleaseType == null) {
      return;
    }

    Busy = true;
    Status = "Fetching firmware manifest…";
    try {
      var vehicle = SelectedVehicle;
      var reltype = SelectedReleaseType;
      var list = await Task.Run(() => {
        APFirmware.GetList(force: force);
        if (APFirmware.Manifest?.Firmware == null) {
          return new List<APFirmware.FirmwareInfo>();
        }

        return APFirmware.Manifest.Firmware
          .Where(a => a.MavType == vehicle)
          .Where(a => a.MavFirmwareVersionType == reltype)
          .Where(a => a.Format == "apj")
          .OrderBy(a => a.Platform)
          .ToList();
      });

      Firmwares.Clear();
      foreach (var fw in list) {
        Firmwares.Add(new FirmwareItem(fw));
      }

      Status = Firmwares.Count > 0
        ? $"{Firmwares.Count} firmware images available for {vehicle} ({reltype})."
        : "No firmware found for the selected vehicle/release type.";
      SelectedFirmware = Firmwares.FirstOrDefault();
    } catch (Exception ex) {
      Status = "Failed to fetch manifest: " + ex.Message;
    } finally {
      Busy = false;
    }
  }

  [RelayCommand(CanExecute = nameof(CanUpload))]
  private async Task Upload() {
    var item = SelectedFirmware;
    if (item?.Info.Url == null) {
      return;
    }

    Busy = true;
    Progress = 0;
    Log = "";
    string? tempFile = null;
    try {
      tempFile = await DownloadFirmware(item.Info.Url);
      AppendLog($"Saved firmware to {tempFile}");
      await Task.Run(() => UploadToBoard(tempFile));
    } catch (Exception ex) {
      Status = "Upload failed: " + ex.Message;
      AppendLog(ex.ToString());
    } finally {
      if (tempFile != null && File.Exists(tempFile)) {
        try {
          File.Delete(tempFile);
        } catch {
        }
      }
      Busy = false;
    }
  }

  private bool CanUpload() => !Busy && SelectedFirmware != null;

  private async Task<string> DownloadFirmware(Uri url) {
    SetStatus("Downloading firmware…");
    var dest = Path.Combine(Path.GetTempPath(), "ap_legacy_" + Guid.NewGuid().ToString("N") + ".apj");
    using var client = new HttpClient();
    var bytes = await client.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(dest, bytes);
    return dest;
  }

  private void UploadToBoard(string filename) {
    SetProgress(100);
    AppendLog($"Firmware downloaded to {filename}");
    SetStatus(
        "Firmware downloaded. On-board bootloader flashing is not bundled in this cross-platform "
        + "build yet — flash the .apj with upstream Mission Planner or ArduPilot's uploader tools.");
  }

  private void SetStatus(string status) => Dispatcher.UIThread.Post(() => Status = status);

  private void SetProgress(double value) => Dispatcher.UIThread.Post(() => Progress = value);

  private void AppendLog(string line) =>
    Dispatcher.UIThread.Post(() => Log = (Log + line + Environment.NewLine));
}

public class FirmwareItem {
  public FirmwareItem(APFirmware.FirmwareInfo info) {
    Info = info;
  }

  public APFirmware.FirmwareInfo Info { get; }

  public string Display {
    get {
      var ver = string.IsNullOrEmpty(Info.MavFirmwareVersionStr)
        ? Info.MavFirmwareVersion?.ToString()
        : Info.MavFirmwareVersionStr;
      return $"{Info.Platform}  {ver}  ({Info.MavFirmwareVersionType})";
    }
  }
}
