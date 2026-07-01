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
using px4uploader;

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
    SetStatus("Reading firmware file…");
    px4uploader.Firmware fw;
    try {
      fw = px4uploader.Firmware.ProcessFirmware(filename);
    } catch (Exception ex) {
      SetStatus("Invalid firmware file: " + ex.Message);
      return;
    }

    AppendLog($"Loaded firmware board_id={fw.board_id} rev={fw.board_revision}");

    AttemptRebootToBootloader();

    var deadline = DateTime.Now.AddSeconds(30);
    SetStatus("Scanning comports for bootloader…");

    while (DateTime.Now < deadline) {
      var ports = SerialPort.GetPortNames();
      Uploader? found = null;

      foreach (var port in ports) {
        Uploader up;
        try {
          up = new Uploader(port, 115200);
        } catch (Exception ex) {
          AppendLog($"{port}: {ex.Message}");
          continue;
        }

        try {
          up.identify();
          AppendLog($"{port}: board_type={up.board_type} bl_rev={up.bl_rev} fw_maxsize={up.fw_maxsize}");

          if (up.board_type != fw.board_id && !(up.board_type == 33 && fw.board_id == 9)) {
            AppendLog($"{port}: board mismatch (detected {up.board_type}, fw {fw.board_id}) - skipping");
            up.close();
            continue;
          }

          found = up;
          break;
        } catch (Exception ex) {
          AppendLog($"{port}: not a bootloader ({ex.Message})");
          try {
            up.close();
          } catch {
          }
        }
      }

      if (found == null) {
        continue;
      }

      SetStatus("Connecting…");
      System.Threading.Thread.Sleep(500);

      found.ProgressEvent += OnUploaderProgress;
      found.LogEvent += OnUploaderLog;
      found.ConfirmEvent += _ => true;

      try {
        found.currentChecksum(fw);
        AppendLog("Firmware already on the board. No upload required.");
        SetStatus("No upload required — firmware already present.");
        try {
          found.__reboot();
        } catch {
        }
        return;
      } catch (IOException) {
        SetStatus("Lost communication with the board.");
        found.close();
        return;
      } catch (TimeoutException) {
        SetStatus("Communication timeout with the board.");
        found.close();
        return;
      } catch {

      }

      try {
        SetStatus("Uploading firmware…");
        SetProgress(0);
        found.upload(fw);
        SetProgress(100);
        SetStatus("Upload complete.");
      } catch (Exception ex) {
        SetStatus("ERROR: " + ex.Message);
        AppendLog(ex.ToString());
      } finally {
        found.close();
      }

      return;
    }

    SetStatus("ERROR: No response from board.");
  }

  private void AttemptRebootToBootloader() {
    var ports = SerialPort.GetPortNames();
    var tasks = new List<Task<bool>>();

    foreach (var port in ports) {
      try {
        var task = Task.Run(() => {
          using var up = new Uploader(port, 115200);
          up.identify();
          return true;
        });
        tasks.Add(task);
      } catch {
      }
    }

    foreach (var task in tasks) {
      try {
        if (task.Wait(TimeSpan.FromSeconds(3)) && task.GetAwaiter().GetResult()) {
          return;
        }
      } catch {
      }
    }

    if (_comPort.BaseStream is SerialPort) {
      try {
        SetStatus("Looking for heartbeat…");
        var task = Task.Run(() => {
          _comPort.BaseStream.Open();
          _comPort.giveComport = true;
          if (_comPort.getHeartBeat().Length > 0) {
            _comPort.doReboot(true, false);
            _comPort.Close();
          } else {
            _comPort.BaseStream.Close();
            throw new Exception("No heartbeat found");
          }
        });
        if (task.Wait(TimeSpan.FromSeconds(5))) {
          SetStatus("Rebooting to bootloader…");
        } else {
          SetStatus("Please unplug the board and plug it back in.");
        }
      } catch (Exception ex) {
        AppendLog(ex.Message);
        SetStatus("Please unplug the board and plug it back in.");
      }
    }
  }

  private void OnUploaderProgress(double completed) => SetProgress(completed);

  private void OnUploaderLog(string message, int level) => AppendLog(message);

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
