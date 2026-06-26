using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels;

// Conversion hub for telemetry logs (mirrors MP's MavlinkLog form): pick a .tlog and convert it
// to KML / GPX / Matlab. KML & GPX reuse the same KMLib/GPX writers as the dataflash exporter
// (DataFlashLog.WriteKmlTrack/WriteGpxTrack) — we just parse the tlog's GLOBAL_POSITION_INT stream
// into a track first. Matlab uses DataFlashLog.ExportMatlab, whose tlog branch calls MatLab.tlog.
public partial class MavlinkLogConvertViewModel : ViewModelBase {
  private string? _tlogPath;

  [ObservableProperty]
  private string _tlogName = "(no file selected)";

  [ObservableProperty]
  private bool _hasFile;

  [ObservableProperty]
  private bool _isBusy;

  [ObservableProperty]
  private string _status = "Pick a .tlog to convert.";

  [RelayCommand]
  private async Task PickTlog() {
    var path = await PickOpenAsync();
    if (path == null) {
      return;
    }
    _tlogPath = path;
    TlogName = Path.GetFileName(path);
    HasFile = true;
    Status = "Ready. Choose a conversion.";
  }

  [RelayCommand]
  private Task ConvertKml() => Convert("kml", "KML", (track, outPath) =>
      DataFlashLog.WriteKmlTrack(track, outPath));

  [RelayCommand]
  private Task ConvertGpx() => Convert("gpx", "GPX", (track, outPath) =>
      DataFlashLog.WriteGpxTrack(track, outPath));

  private async Task Convert(string ext, string label,
      Action<IReadOnlyList<(double lat, double lng, double alt, DateTime time)>, string> write) {
    if (_tlogPath == null || IsBusy) {
      return;
    }
    var dest = await PickSaveAsync(Path.GetFileNameWithoutExtension(_tlogPath) + "." + ext, ext);
    if (dest == null) {
      return;
    }

    IsBusy = true;
    Status = $"Converting to {label}…";
    try {
      var src = _tlogPath;
      await Task.Run(() => {
        var track = ReadTlogTrack(src);
        if (track.Count == 0) {
          throw new InvalidOperationException("No GPS positions found in the tlog.");
        }
        write(track, dest);
      });
      Status = $"Wrote {label}: {dest}";
    } catch (Exception ex) {
      Status = $"{label} conversion failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }

  [RelayCommand]
  private async Task ConvertMatlab() {
    if (_tlogPath == null || IsBusy) {
      return;
    }
    IsBusy = true;
    Status = "Converting to Matlab…";
    try {
      var src = _tlogPath;
      await Task.Run(() => DataFlashLog.ExportMatlab(src));
      Status = "Wrote Matlab .mat next to the tlog.";
    } catch (Exception ex) {
      Status = "Matlab conversion failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }

  // Parse a tlog (8-byte big-endian timestamp prefix + mavlink packet) and pull the GPS track
  // out of GLOBAL_POSITION_INT messages. MavlinkParse(hasTimestamp:true) handles the prefix and
  // stamps each message's rxtime.
  private static List<(double lat, double lng, double alt, DateTime time)> ReadTlogTrack(string path) {
    var track = new List<(double lat, double lng, double alt, DateTime time)>();
    var parser = new MAVLink.MavlinkParse(true);

    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    while (fs.Position < fs.Length) {
      MAVLink.MAVLinkMessage? msg;
      try {
        msg = parser.ReadPacket(fs);
      } catch (EndOfStreamException) {
        break;
      } catch {
        continue;
      }
      if (msg == null) {
        continue;
      }
      if (msg.msgid != (uint)MAVLink.MAVLINK_MSG_ID.GLOBAL_POSITION_INT) {
        continue;
      }

      var gpi = msg.ToStructure<MAVLink.mavlink_global_position_int_t>();
      double lat = gpi.lat / 1e7;
      double lng = gpi.lon / 1e7;
      double alt = gpi.alt / 1000.0;
      if (lat is < -90 or > 90 || lng is < -180 or > 180 || (lat == 0 && lng == 0)) {
        continue;
      }
      track.Add((lat, lng, alt, msg.rxtime));
    }
    return track;
  }

  private static async Task<string?> PickOpenAsync() {
    var top = TopWindow();
    if (top == null) {
      return null;
    }
    var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select telemetry log",
      AllowMultiple = false,
      FileTypeFilter = new[] {
        new FilePickerFileType("Telemetry log") { Patterns = new[] { "*.tlog" } },
      },
    });
    return files.Count > 0 ? files[0].TryGetLocalPath() : null;
  }

  private static async Task<string?> PickSaveAsync(string suggested, string ext) {
    var top = TopWindow();
    if (top == null) {
      return null;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save converted log",
      SuggestedFileName = suggested,
      DefaultExtension = ext,
    });
    return file?.TryGetLocalPath();
  }

  private static Avalonia.Controls.Window? TopWindow() =>
      (Avalonia.Application.Current?.ApplicationLifetime
       as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
