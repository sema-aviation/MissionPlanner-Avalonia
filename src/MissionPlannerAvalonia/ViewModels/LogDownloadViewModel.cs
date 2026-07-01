using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels;

public partial class LogDownloadRow : ObservableObject {
  public ushort Id { get; init; }
  public uint SizeBytes { get; init; }
  public DateTime TimeUtc { get; init; }

  public string SizeText =>
      SizeBytes >= 1024 * 1024 ? (SizeBytes / 1024.0 / 1024.0).ToString("0.0") + " MB"
                               : (SizeBytes / 1024.0).ToString("0.0") + " KB";

  public string TimeText => TimeUtc == DateTime.MinValue ? "—"
                                                         : TimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

public partial class LogDownloadViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private static readonly DateTime _epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

  public ObservableCollection<LogDownloadRow> Logs { get; } = new();

  [ObservableProperty]
  private LogDownloadRow? _selectedLog;

  [ObservableProperty]
  private bool _isBusy;

  [ObservableProperty]
  private double _progress;

  [ObservableProperty]
  private string _status = "";

  [RelayCommand]
  [Obsolete]
  private async Task Refresh() {
    if (!AppState.IsConnected) {
      Status = "Not connected.";
      return;
    }
    if (IsBusy) {
      return;
    }

    IsBusy = true;
    Status = "Requesting log list…";
    Logs.Clear();
    try {
      var list = await Task.Run(() => _comPort.GetLogEntry());
      foreach (var e in list.Values.OrderBy(a => a.id)) {
        if (e.size == 0) {
          continue;
        }
        Logs.Add(new LogDownloadRow {
          Id = e.id,
          SizeBytes = e.size,
          TimeUtc = e.time_utc == 0 ? DateTime.MinValue : _epoch.AddSeconds(e.time_utc),
        });
      }
      Status = $"{Logs.Count} log(s) on board.";
    } catch (Exception ex) {
      Status = "List failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task Download() {
    var sel = SelectedLog;
    if (sel == null) {
      Status = "Select a log first.";
      return;
    }
    if (!AppState.IsConnected) {
      Status = "Not connected.";
      return;
    }
    if (IsBusy) {
      return;
    }

    var dest = await PickSaveAsync($"log_{sel.Id}.bin");
    if (dest == null) {
      return;
    }

    IsBusy = true;
    Progress = 0;
    Status = $"Downloading log {sel.Id}…";

    void OnProgress(int offset, string _) {
      double pct = sel.SizeBytes > 0 ? Math.Min(100.0, 100.0 * offset / sel.SizeBytes) : 0;
      Dispatcher.UIThread.Post(() => Progress = pct);
    }

    _comPort.Progress += OnProgress;
    try {
      await Task.Run(async () => {
        using var temp = await _comPort.GetLog(sel.Id);
        var tempPath = temp.Name;
        temp.Position = 0;
        using (var outFs = new FileStream(dest, FileMode.Create, FileAccess.Write)) {
          await temp.CopyToAsync(outFs);
        }
        temp.Close();
        try {
          File.Delete(tempPath);
        } catch {

        }
      });
      Progress = 100;
      Status = $"Saved log {sel.Id} to {dest}";
    } catch (Exception ex) {
      Status = "Download failed: " + ex.Message;
    } finally {
      _comPort.Progress -= OnProgress;
      IsBusy = false;
    }
  }

  private static async Task<string?> PickSaveAsync(string suggested) {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return null;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save dataflash log",
      SuggestedFileName = suggested,
      DefaultExtension = "bin",
      FileTypeChoices = new[] {
        new FilePickerFileType("Dataflash log") { Patterns = new[] { "*.bin" } },
      },
    });
    return file?.TryGetLocalPath();
  }
}
