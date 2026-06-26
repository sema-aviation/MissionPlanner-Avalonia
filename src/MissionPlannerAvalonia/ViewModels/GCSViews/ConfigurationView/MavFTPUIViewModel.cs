using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.ArduPilot.Mavlink;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// MAVFTP remote file browser. Mirrors MissionPlanner/Controls/MavFTPUI.cs, using the
// real MAVFtp transport (kCmdListDirectory / GetFile / UploadFile / kCmdCreateDirectory /
// kCmdRemoveFile / kCmdRemoveDirectory / kCmdCalcFileCRC32) against AppState.comPort.
public partial class MavFTPUIViewModel : ViewModelBase {
  private readonly MAVLinkInterface _mav = AppState.comPort;
  private readonly MAVFtp _mavftp;

  public ObservableCollection<FtpDirNode> Roots { get; } = new();

  // Contents (sub-directories + files) of the currently selected tree directory.
  public ObservableCollection<FtpEntry> Entries { get; } = new();

  [ObservableProperty]
  private FtpDirNode? _selectedDir;

  [ObservableProperty]
  private FtpEntry? _selectedEntry;

  [ObservableProperty]
  private string _status = "Connect over MAVLink, then Refresh to browse the remote filesystem.";

  [ObservableProperty]
  private int _progress;

  [ObservableProperty]
  private string _newFolderName = "";

  [ObservableProperty]
  private bool _isBusy;

  public MavFTPUIViewModel() {
    _mavftp = new MAVFtp(_mav, (byte)_mav.sysidcurrent, (byte)_mav.compidcurrent);
    var next = DateTime.UtcNow;
    _mavftp.Progress += (message, percent) => {
      if (next > DateTime.UtcNow) {
        return;
      }

      next = DateTime.UtcNow.AddMilliseconds(100);
      Dispatcher.UIThread.Post(() => {
        if (percent >= 0) {
          Progress = percent;
        }

        Status = message;
      });
    };
  }

  // Blocking directory listing — always called from a background task.
  internal List<MAVFtp.FtpFileInfo> ListDir(string path) {
    lock (_mavftp) {
      return _mavftp.kCmdListDirectory(path, new CancellationTokenSource());
    }
  }

  [RelayCommand]
  private async Task Refresh() {
    if (_mav.BaseStream?.IsOpen != true) {
      Status = "Not connected — open the MAVLink link first.";
      return;
    }

    IsBusy = true;
    Status = "Updating folders…";
    Entries.Clear();
    Roots.Clear();

    var root = new FtpDirNode("/", "/", this);
    var sys = new FtpDirNode("@SYS", "@SYS/", this);
    Roots.Add(root);
    Roots.Add(sys);

    await root.LoadChildrenAsync();
    await sys.LoadChildrenAsync();

    SelectedDir = root;
    Status = "Ready.";
    IsBusy = false;
  }

  partial void OnSelectedDirChanged(FtpDirNode? value) {
    if (value != null) {
      _ = ListEntriesAsync(value);
    }
  }

  private async Task ListEntriesAsync(FtpDirNode dir) {
    IsBusy = true;
    Status = "Listing " + dir.FullPath;
    Entries.Clear();

    List<MAVFtp.FtpFileInfo> list = new();
    try {
      await Task.Run(() => list = ListDir(dir.FullPath));
    } catch (Exception ex) {
      Status = "List failed: " + ex.Message;
      IsBusy = false;
      return;
    }

    foreach (var info in list) {
      if (info.Name is "." or "..") {
        continue;
      }

      Entries.Add(new FtpEntry {
        Name = info.Name,
        FullName = info.FullName,
        IsDirectory = info.isDirectory,
        Type = info.isDirectory ? "Directory" : "File",
        SizeText = info.isDirectory ? "" : info.Size.ToString(),
      });
    }

    Status = "Ready.";
    IsBusy = false;
  }

  // Called from the view with a chosen destination folder.
  public async Task DownloadAsync(string destFolder) {
    var entry = SelectedEntry;
    if (entry == null || entry.IsDirectory) {
      Status = "Select a file to download.";
      return;
    }

    IsBusy = true;
    Status = "Download " + entry.Name;
    var cancel = new CancellationTokenSource();
    try {
      await Task.Run(() => {
        var ms = _mavftp.GetFile(entry.FullName, cancel, false);
        var file = Path.Combine(destFolder, entry.Name);
        int a = 0;
        while (File.Exists(file)) {
          file = Path.Combine(destFolder, entry.Name) + a++;
        }

        File.WriteAllBytes(file, ms.ToArray());
      });
      Status = "Downloaded " + entry.Name;
    } catch (Exception ex) {
      Status = "Download failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }
  }

  // Called from the view with a chosen local file; uploads into the selected directory.
  public async Task UploadAsync(string localFile) {
    var dir = SelectedDir;
    if (dir == null) {
      Status = "Select a destination directory first.";
      return;
    }

    IsBusy = true;
    var remote = Combine(dir.FullPath, Path.GetFileName(localFile));
    Status = "Upload " + Path.GetFileName(localFile);
    var cancel = new CancellationTokenSource();
    try {
      bool crcOk = true;
      await Task.Run(() => {
        _mavftp.UploadFile(remote, localFile, cancel);
        uint crc = 0;
        _mavftp.kCmdCalcFileCRC32(remote, ref crc, cancel);
        var local = MAVFtp.crc_crc32(0, File.ReadAllBytes(localFile));
        crcOk = local == crc;
      });
      Status = crcOk ? "Uploaded " + Path.GetFileName(localFile) : "Uploaded with CRC mismatch.";
    } catch (Exception ex) {
      Status = "Upload failed: " + ex.Message;
    } finally {
      IsBusy = false;
    }

    await ListEntriesAsync(dir);
  }

  [RelayCommand]
  private async Task MakeDirectory() {
    var dir = SelectedDir;
    if (dir == null || string.IsNullOrWhiteSpace(NewFolderName)) {
      Status = "Select a parent directory and enter a folder name.";
      return;
    }

    IsBusy = true;
    var path = Combine(dir.FullPath, NewFolderName.Trim());
    var cancel = new CancellationTokenSource();
    bool ok = false;
    try {
      await Task.Run(() => ok = _mavftp.kCmdCreateDirectory(path, cancel));
    } catch (Exception ex) {
      Status = "Mkdir failed: " + ex.Message;
      IsBusy = false;
      return;
    }

    Status = ok ? "Created " + path : "Failed to create directory.";
    NewFolderName = "";
    IsBusy = false;
    await ListEntriesAsync(dir);
  }

  [RelayCommand]
  private async Task Delete() {
    var dir = SelectedDir;
    var entry = SelectedEntry;
    if (dir == null || entry == null) {
      Status = "Select an item to delete.";
      return;
    }

    IsBusy = true;
    Status = "Delete " + entry.Name;
    var cancel = new CancellationTokenSource();
    bool ok = false;
    try {
      await Task.Run(() => {
        ok = entry.IsDirectory
            ? _mavftp.kCmdRemoveDirectory(entry.FullName, cancel)
            : _mavftp.kCmdRemoveFile(entry.FullName, cancel);
      });
    } catch (Exception ex) {
      Status = "Delete failed: " + ex.Message;
      IsBusy = false;
      return;
    }

    Status = ok ? "Deleted " + entry.Name : "Failed to delete " + entry.Name;
    IsBusy = false;
    await ListEntriesAsync(dir);
  }

  // Double-clicking a directory in the list drills into it (keeps tree + list in sync).
  public async Task OpenSelectedEntryAsync() {
    var dir = SelectedDir;
    var entry = SelectedEntry;
    if (dir == null || entry == null || !entry.IsDirectory) {
      return;
    }

    await dir.LoadChildrenAsync();
    dir.IsExpanded = true;
    foreach (var child in dir.Children) {
      if (child.FullPath == entry.FullName) {
        SelectedDir = child;
        return;
      }
    }
  }

  internal static string Combine(string dir, string name) {
    if (string.IsNullOrEmpty(dir) || dir == "/") {
      return "/" + name;
    }

    return (dir.EndsWith("/") ? dir : dir + "/") + name;
  }
}

// Lazy-loaded directory node for the tree (sub-directories only).
public partial class FtpDirNode : ObservableObject {
  private readonly MavFTPUIViewModel? _vm;
  private readonly bool _isPlaceholder;
  private bool _loaded;

  public FtpDirNode(string name, string fullPath, MavFTPUIViewModel? vm, bool isPlaceholder = false) {
    Name = name;
    FullPath = fullPath;
    _vm = vm;
    _isPlaceholder = isPlaceholder;
    if (!isPlaceholder) {
      // A non-null placeholder so the expander arrow shows before children are fetched.
      Children.Add(new FtpDirNode("…", "", null, true));
    }
  }

  public string Name { get; }

  public string FullPath { get; }

  public ObservableCollection<FtpDirNode> Children { get; } = new();

  [ObservableProperty]
  private bool _isExpanded;

  partial void OnIsExpandedChanged(bool value) {
    if (value) {
      _ = LoadChildrenAsync();
    }
  }

  public async Task LoadChildrenAsync() {
    if (_loaded || _isPlaceholder || _vm == null) {
      return;
    }

    _loaded = true;
    List<MAVFtp.FtpFileInfo> list = new();
    try {
      await Task.Run(() => list = _vm.ListDir(FullPath));
    } catch {
      Children.Clear();
      return;
    }

    Children.Clear();
    foreach (var info in list) {
      if (!info.isDirectory || info.Name is "." or "..") {
        continue;
      }

      Children.Add(new FtpDirNode(info.Name, info.FullName, _vm));
    }
  }
}

public partial class FtpEntry : ObservableObject {
  [ObservableProperty]
  private string _name = "";

  [ObservableProperty]
  private string _fullName = "";

  [ObservableProperty]
  private bool _isDirectory;

  [ObservableProperty]
  private string _type = "";

  [ObservableProperty]
  private string _sizeText = "";
}
