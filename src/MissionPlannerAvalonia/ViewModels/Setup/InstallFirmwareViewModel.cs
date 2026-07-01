using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot;
using px4uploader;

namespace MissionPlannerAvalonia.ViewModels.Setup;

public partial class FirmwareTile : ObservableObject {
  public FirmwareTile(string key, string displayName, string mavType, string glyph) {
    Key = key;
    DisplayName = displayName;
    MavType = mavType;
    Glyph = glyph;
  }

  public string Key { get; }
  public string DisplayName { get; }
  public string MavType { get; }
  public string Glyph { get; }

  [ObservableProperty]
  private string _versionLabel = "—";

  [ObservableProperty]
  private bool _available;
}

public partial class InstallFirmwareViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  private APFirmware.RELEASE_TYPES _releaseType = APFirmware.RELEASE_TYPES.OFFICIAL;

  public ObservableCollection<FirmwareTile> Tiles { get; } = new();

  [ObservableProperty]
  private string _status = "Connect your board over USB, then pick a vehicle to flash.";

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  private double _progress;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(FlashTileCommand))]
  [NotifyCanExecuteChangedFor(nameof(RefreshTilesCommand))]
  [NotifyCanExecuteChangedFor(nameof(ForceBootloaderCommand))]
  [NotifyCanExecuteChangedFor(nameof(BootloaderUpdateCommand))]
  [NotifyCanExecuteChangedFor(nameof(ToggleBetaCommand))]
  private bool _busy;

  [ObservableProperty]
  private string _channelLabel = "Channel: OFFICIAL (stable)";

  public InstallFirmwareViewModel() {
    Tiles.Add(new FirmwareTile("rover", "Rover", "GROUND_ROVER", "fa-solid fa-car"));
    Tiles.Add(new FirmwareTile("plane", "Plane", "FIXED_WING", "fa-solid fa-plane"));
    Tiles.Add(new FirmwareTile("quad", "Copter Quad", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("hexa", "Copter Hexa", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("octo", "Copter Octo", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("y6", "Copter Y6", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("tri", "Copter Tri", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("octaquad", "Copter OctaQuad", "Copter", "fa-solid fa-fan"));
    Tiles.Add(new FirmwareTile("heli", "Heli", "HELICOPTER", "fa-solid fa-helicopter"));
    Tiles.Add(new FirmwareTile("sub", "Sub", "SUBMARINE", "fa-solid fa-ship"));
    Tiles.Add(new FirmwareTile("tracker", "AntennaTracker", "ANTENNA_TRACKER", "fa-solid fa-satellite-dish"));

    _ = RefreshTiles();
  }

  private bool NotBusy => !Busy;

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task RefreshTiles() {
    Busy = true;
    Status = "Fetching firmware manifest…";
    try {
      var reltype = _releaseType;
      var newest = await Task.Run(() => {
        APFirmware.GetList();
        return APFirmware.Manifest?.Firmware == null
          ? new List<APFirmware.FirmwareInfo>()
          : APFirmware.GetReleaseNewest(reltype);
      });

      var byType = newest
        .Where(a => a != null)
        .GroupBy(a => a.MavType)
        .ToDictionary(g => g.Key, g => g.First());

      foreach (var tile in Tiles) {
        if (byType.TryGetValue(tile.MavType, out var info)) {
          var ver = string.IsNullOrEmpty(info.MavFirmwareVersionStr)
            ? info.MavFirmwareVersion?.ToString()
            : info.MavFirmwareVersionStr;
          tile.VersionLabel = $"{tile.DisplayName.Split(' ')[0]} {ver} {info.MavFirmwareVersionType}";
          tile.Available = true;
        } else {
          tile.VersionLabel = "(unavailable)";
          tile.Available = false;
        }
      }

      Status = byType.Count > 0
        ? "Select a vehicle to download and flash its firmware."
        : "No firmware found in the manifest.";
    } catch (Exception ex) {
      Status = "Failed to fetch manifest: " + ex.Message;
    } finally {
      Busy = false;
    }
  }

  [RelayCommand(CanExecute = nameof(CanFlash))]
  private async Task FlashTile(FirmwareTile? tile) {
    if (tile == null) {
      return;
    }

    Busy = true;
    Progress = 0;
    Log = "";
    var reltype = _releaseType;
    string? tempFile = null;
    try {
      var boardId = await Task.Run(DetectBoardId);
      if (boardId == null) {
        SetStatus("Could not detect a board. Plug the board in over USB and try again.");
        return;
      }

      AppendLog($"Detected board id {boardId}.");

      var info = await Task.Run(() => SelectFirmware(tile.MavType, reltype, boardId.Value));
      if (info?.Url == null) {
        SetStatus($"No {reltype} {tile.DisplayName} firmware found for this board (id {boardId}).");
        return;
      }

      tempFile = await DownloadFirmware(info.Url);
      AppendLog($"Saved firmware to {tempFile}");
      await Task.Run(() => UploadToBoard(tempFile));
    } catch (Exception ex) {
      SetStatus("Flash failed: " + ex.Message);
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

  private bool CanFlash(FirmwareTile? tile) => !Busy;

  public async Task FlashCustomFirmwareAsync(string path) {
    if (Busy) {
      return;
    }

    Busy = true;
    Progress = 0;
    Log = "";
    try {
      AppendLog($"Custom firmware: {path}");
      await Task.Run(() => UploadToBoard(path));
    } catch (Exception ex) {
      SetStatus("Flash failed: " + ex.Message);
      AppendLog(ex.ToString());
    } finally {
      Busy = false;
    }
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private void ToggleBeta() {
    _releaseType = _releaseType == APFirmware.RELEASE_TYPES.BETA
      ? APFirmware.RELEASE_TYPES.OFFICIAL
      : APFirmware.RELEASE_TYPES.BETA;
    ChannelLabel = _releaseType == APFirmware.RELEASE_TYPES.BETA
      ? "Channel: BETA"
      : "Channel: OFFICIAL (stable)";
    _ = RefreshTiles();
  }

  [RelayCommand]
  private void AllOptions() {
    OpenUrl("https://firmware.ardupilot.org/");
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task ForceBootloader() {
    Busy = true;
    try {
      await Task.Run(() => {
        SetStatus("Rebooting board into the bootloader…");
        AttemptRebootToBootloader();
        SetStatus("Board should now be in the bootloader. Pick a firmware to flash.");
      });
    } finally {
      Busy = false;
    }
  }

  [RelayCommand(CanExecute = nameof(CanBootloaderUpdate))]
  [Obsolete]
  private async Task BootloaderUpdate() {
    Busy = true;
    try {
      await Task.Run(() => {
        try {
          SetStatus("Sending bootloader update command…");

          var ok = _comPort.doCommand(MAVLink.MAV_CMD.FLASH_BOOTLOADER, 0, 0, 0, 0, 290876, 0, 0);
          SetStatus(ok ? "Bootloader upgraded." : "Bootloader upgrade failed.");
        } catch (Exception ex) {
          SetStatus("Bootloader upgrade error: " + ex.Message);
        }
      });
    } finally {
      Busy = false;
    }
  }

  private bool CanBootloaderUpdate() => !Busy && _comPort.BaseStream?.IsOpen == true;

  private APFirmware.FirmwareInfo? SelectFirmware(string mavType, APFirmware.RELEASE_TYPES reltype, int boardId) {
    if (APFirmware.Manifest?.Firmware == null) {
      return null;
    }

    return APFirmware.Manifest.Firmware
      .Where(a => a.MavType == mavType)
      .Where(a => a.MavFirmwareVersionType == reltype.ToString())
      .Where(a => a.Format == "apj")
      .Where(a => a.BoardId == boardId || (boardId == 33 && a.BoardId == 9))
      .OrderByDescending(a => a.MavFirmwareVersion)
      .FirstOrDefault();
  }

  private int? DetectBoardId() {
    AttemptRebootToBootloader();

    var deadline = DateTime.Now.AddSeconds(20);
    SetStatus("Scanning comports for the bootloader…");

    while (DateTime.Now < deadline) {
      foreach (var port in SerialPort.GetPortNames()) {
        Uploader up;
        try {
          up = new Uploader(port, 115200);
        } catch {
          continue;
        }

        try {
          up.identify();
          AppendLog($"{port}: board_type={up.board_type} bl_rev={up.bl_rev}");
          var id = up.board_type;
          up.close();
          return id;
        } catch {
          try {
            up.close();
          } catch {
          }
        }
      }
    }

    return null;
  }

  private async Task<string> DownloadFirmware(Uri url) {
    SetStatus("Downloading firmware…");
    var dest = Path.Combine(Path.GetTempPath(), "ap_install_" + Guid.NewGuid().ToString("N") + ".apj");
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

  private static void OpenUrl(string url) {
    try {
      Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    } catch {
    }
  }

  private void SetStatus(string status) => Dispatcher.UIThread.Post(() => Status = status);

  private void SetProgress(double value) => Dispatcher.UIThread.Post(() => Progress = value);

  private void AppendLog(string line) =>
    Dispatcher.UIThread.Post(() => Log += line + Environment.NewLine);
}
