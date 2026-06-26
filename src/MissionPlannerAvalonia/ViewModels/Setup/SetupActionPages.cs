using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.ViewModels.Setup;

public partial class SikRadioViewModel : ViewModelBase {
  // S-register map per SiK ATI5 output (S0..S15). FORMAT (S0) is read-only and never written.
  private static readonly (int Num, string Name, string Label)[] RegMap = {
    (0, "FORMAT", "Format"),
    (1, "SERIAL_SPEED", "Baud"),
    (2, "AIR_SPEED", "Air Speed"),
    (3, "NETID", "Net ID"),
    (4, "TXPOWER", "Tx Power"),
    (5, "ECC", "ECC"),
    (6, "MAVLINK", "Mavlink"),
    (7, "OPPRESEND", "Op Resend"),
    (8, "MIN_FREQ", "Min Freq"),
    (9, "MAX_FREQ", "Max Freq"),
    (10, "NUM_CHANNELS", "# Channels"),
    (11, "DUTY_CYCLE", "Duty Cycle"),
    (12, "LBT_RSSI", "LBT RSSI"),
    (13, "MANCHESTER", "Manchester"),
    (14, "RTSCTS", "RTS CTS"),
    (15, "MAX_WINDOW", "Max Window"),
  };

  private static readonly int[] CandidateBauds = { 57600, 115200, 38400, 19200, 9600 };
  private static readonly Regex RegLine =
      new(@"S(\d+):\s*([A-Za-z0-9_/]+)\s*=\s*(\S+)", RegexOptions.Compiled);
  private static readonly Regex SikBanner = new(@"SiK|RFD", RegexOptions.Compiled);

  public partial class SikRegister : ObservableObject {
    public SikRegister(int num, string name, string label) {
      Num = num;
      Name = name;
      Label = label;
    }

    // Num is captured from the radio's ATI5 dump for dynamically-discovered extra/GPIO/
    // encryption registers, so the eventual ATS<n>= write targets the right register.
    public int Num { get; set; }
    public string Name { get; }
    public string Label { get; }
    public string OrigLocal { get; set; } = "";
    public string OrigRemote { get; set; } = "";
    public bool HasRemote { get; set; }

    // Allowed values for this register on the connected firmware (typed combo). Empty => free text.
    public ObservableCollection<string> Options { get; } = new();
    public bool HasOptions => Options.Count > 0;

    public void SetOptions(IEnumerable<string> values) {
      Options.Clear();
      foreach (var v in values) {
        Options.Add(v);
      }
      OnPropertyChanged(nameof(HasOptions));
    }

    // Keep the live value selectable even if it is outside the canned option set.
    public void EnsureOption(string value) {
      if (Options.Count > 0 && !string.IsNullOrEmpty(value) && !Options.Contains(value)) {
        Options.Insert(0, value);
      }
    }

    [ObservableProperty]
    private string _localValue = "";

    [ObservableProperty]
    private string _remoteValue = "";
  }

  public string Title { get; } = "SiK Radio";

  public string Instructions { get; } =
      "Configure a SiK telemetry radio over its raw serial port using AT commands. "
      + "Disconnect the MAVLink link first, pick the radio's port/baud, then Load Settings.";

  public ObservableCollection<SikRegister> Registers { get; } = new();
  public ObservableCollection<string> Ports { get; } = new();

  public int[] Bauds { get; } =
      { 57600, 115200, 38400, 19200, 9600, 230400, 250000 };

  [ObservableProperty]
  private string? _selectedPort;

  [ObservableProperty]
  private int _selectedBaud = 57600;

  [ObservableProperty]
  private string _localVersion = "";

  [ObservableProperty]
  private string _remoteVersion = "";

  [ObservableProperty]
  private string _status = "Idle";

  [ObservableProperty]
  private string _log = "";

  [ObservableProperty]
  private bool _isBusy;

  // ATI2 / ATI3 / ATI7 readouts (board, frequency band, live RSSI string).
  [ObservableProperty]
  private string _boardType = "";

  [ObservableProperty]
  private string _freqBand = "";

  [ObservableProperty]
  private string _rssiInfo = "";

  // AES encryption key (hex) — round-tripped via AT&E? / AT&E=.
  [ObservableProperty]
  private string _aesKey = "";

  [ObservableProperty]
  private bool _aesEnabled;

  // AT command terminal.
  [ObservableProperty]
  private string _commandText = "ATI";

  [ObservableProperty]
  private bool _terminalOpen;

  // Live RSSI streaming.
  [ObservableProperty]
  private bool _rssiRunning;

  public string TerminalButtonLabel => TerminalOpen ? "Close Terminal" : "Open Terminal";
  public string RssiButtonLabel => RssiRunning ? "Stop RSSI" : "Start RSSI";

  // Status LED: green = RSSI link streaming, amber = busy/AT session, grey = idle.
  public IBrush StatusLed =>
      RssiRunning ? Brushes.LimeGreen
      : (IsBusy || _session != null || TerminalOpen) ? Brushes.Goldenrod
      : Brushes.DimGray;

  public string StatusLedLabel =>
      RssiRunning ? "Link live" : (IsBusy || _session != null || TerminalOpen) ? "Active" : "Idle";

  // RSSI samples for the ScottPlot LivePlot (time, rssiLocal, rssiRemote, noiseLocal, noiseRemote).
  // The view code-behind subscribes and appends to the plot.
  public event Action<double, double, double, double, double>? RssiSample;
  public event Action? RssiReset;

  // Typed option sets (numeric strings) for the standard SiK S-registers; refined for RFD radios.
  private static readonly Regex RssiLine =
      new(@"RSSI:\s*([0-9]+)/([0-9]+)\s+L/R noise:\s*([0-9]+)/([0-9]+)", RegexOptions.Compiled);

  public SikRadioViewModel() {
    foreach (var (num, name, label) in RegMap) {
      var reg = new SikRegister(num, name, label);
      var opts = DefaultOptions(name, rfd: false);
      if (opts != null) {
        reg.SetOptions(opts);
      }
      Registers.Add(reg);
    }

    RefreshPorts();

    var baud = AppState.comPort.BaseStream?.BaudRate ?? 0;
    if (baud > 0 && Bauds.Contains(baud)) {
      SelectedBaud = baud;
    }
  }

  private string _origAesKey = "";

  // Persistent terminal / RSSI session.
  private SerialPort? _session;
  private System.Threading.CancellationTokenSource? _rssiCts;

  private bool LinkOpen => AppState.comPort.BaseStream?.IsOpen == true;
  private bool NotBusy => !IsBusy && _session == null;

  partial void OnIsBusyChanged(bool value) {
    OnPropertyChanged(nameof(StatusLed));
    OnPropertyChanged(nameof(StatusLedLabel));
    LoadCommand.NotifyCanExecuteChanged();
    SaveCommand.NotifyCanExecuteChanged();
    ResetDefaultsCommand.NotifyCanExecuteChanged();
    RefreshPortsCommand.NotifyCanExecuteChanged();
    UploadFirmwareCommand.NotifyCanExecuteChanged();
    OpenTerminalCommand.NotifyCanExecuteChanged();
    StartRssiCommand.NotifyCanExecuteChanged();
    SendCommandCommand.NotifyCanExecuteChanged();
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private void RefreshPorts() {
    var current = SelectedPort;
    Ports.Clear();
    foreach (var p in SerialPort.GetPortNames().OrderBy(p => p)) {
      Ports.Add(p);
    }

    var prefer = AppState.comPort.BaseStream?.PortName;
    if (current != null && Ports.Contains(current)) {
      SelectedPort = current;
    } else if (!string.IsNullOrEmpty(prefer) && Ports.Contains(prefer)) {
      SelectedPort = prefer;
    } else {
      SelectedPort = Ports.FirstOrDefault();
    }
  }

  private bool GuardLink() {
    if (LinkOpen) {
      AppendLog("MAVLink link is OPEN on " + AppState.comPort.BaseStream?.PortName
          + ". A radio cannot be in AT mode while MAVLink streams — disconnect the vehicle first.");
      Status = "Disconnect MAVLink first";
      return false;
    }
    if (string.IsNullOrEmpty(SelectedPort)) {
      AppendLog("Select a serial port first.");
      return false;
    }
    return true;
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task Load() {
    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    IsBusy = true;
    Status = "Loading…";
    AppendLog("=== Load Settings ===");

    await Task.Run(() => {
      SerialPort? sp = null;
      try {
        sp = Connect(port, baud, out var used);
        if (sp == null) {
          SetStatus("Failed to enter AT mode");
          AppendLog("Could not enter AT command mode on any candidate baud.");
          return;
        }

        AppendLog("In AT command mode @ " + used + " baud.");
        DoCommand(sp, "AT&T", false);

        var ati = DoCommand(sp, "ATI").Trim();
        Ui(() => LocalVersion = ati);

        // Per-firmware typed combos: RFD-class radios use a different value range.
        var isRfd = ati.IndexOf("RFD", StringComparison.OrdinalIgnoreCase) >= 0
            || ati.IndexOf("MP on", StringComparison.OrdinalIgnoreCase) >= 0;

        // ATI2 = board type, ATI3 = frequency band, ATI7 = live RSSI snapshot.
        var ati2 = DoCommand(sp, "ATI2").Trim();
        Ui(() => BoardType = ati2);
        var ati3 = DoCommand(sp, "ATI3").Trim();
        Ui(() => FreqBand = ati3);
        var ati7 = DoCommand(sp, "ATI7").Trim();
        Ui(() => RssiInfo = ati7);

        // AES key (AT&E?). ERROR => firmware has no encryption support.
        var aes = DoCommand(sp, "AT&E?").Trim();
        if (aes.Length == 0 || aes.Contains("ERROR")) {
          _origAesKey = "";
          Ui(() => { AesKey = ""; AesEnabled = false; });
        } else {
          _origAesKey = aes;
          Ui(() => { AesKey = aes; AesEnabled = true; });
        }

        ResetOrig(remote: false);
        ParseInto(DoCommand(sp, "ATI5", true), remote: false);
        ApplyFirmwareOptions(isRfd);

        var rti = DoCommand(sp, "RTI").Trim();
        if (SikBanner.IsMatch(rti)) {
          Ui(() => RemoteVersion = rti);
          ResetOrig(remote: true);
          ParseInto(DoCommand(sp, "RTI5", true), remote: true);
        } else {
          Ui(() => RemoteVersion = "(no remote)");
          AppendLog("No remote radio responded to RTI.");
        }

        // Leave AT mode without rebooting.
        DoCommand(sp, "ATO", false);
        SetStatus("Loaded");
      } catch (Exception ex) {
        AppendLog("Load error: " + ex.Message);
        SetStatus("Error");
      } finally {
        ClosePort(sp);
      }
    });

    IsBusy = false;
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task Save() {
    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    var snapshot = Registers.Select(r => (r.Num, r.Name, r.LocalValue, r.OrigLocal,
        r.RemoteValue, r.OrigRemote, r.HasRemote)).ToArray();
    var aesEnabled = AesEnabled;
    var aesKey = AesKey?.Trim() ?? "";
    var origAes = _origAesKey;
    IsBusy = true;
    Status = "Saving…";
    AppendLog("=== Save Settings ===");

    await Task.Run(() => {
      SerialPort? sp = null;
      try {
        sp = Connect(port, baud, out var used);
        if (sp == null) {
          SetStatus("Failed to enter AT mode");
          AppendLog("Could not enter AT command mode.");
          return;
        }

        AppendLog("In AT command mode @ " + used + " baud.");
        DoCommand(sp, "AT&T", false);

        var remoteChanged = snapshot.Any(s =>
            s.Num != 0 && s.HasRemote && s.RemoteValue != s.OrigRemote);
        if (remoteChanged) {
          DoCommand(sp, "RTI5", true);
          foreach (var s in snapshot) {
            if (s.Num != 0 && s.HasRemote && s.RemoteValue != s.OrigRemote) {
              var ans = DoCommand(sp, "RTS" + s.Num + "=" + s.RemoteValue);
              AppendLog("RTS" + s.Num + " (" + s.Name + ")=" + s.RemoteValue
                  + (ans.Contains("OK") ? " OK" : " FAILED"));
            }
          }
          DoCommand(sp, "RT&W");
          DoCommand(sp, "RTZ");
        }

        DoCommand(sp, "ATI5", true);
        foreach (var s in snapshot) {
          if (s.Num != 0 && s.LocalValue != s.OrigLocal) {
            // Validation: SiK S-registers are integers; refuse to push a non-numeric value.
            if (!int.TryParse(s.LocalValue, out _)) {
              AppendLog("ATS" + s.Num + " (" + s.Name + ")='" + s.LocalValue
                  + "' SKIPPED (not a valid integer)");
              continue;
            }
            var ans = DoCommand(sp, "ATS" + s.Num + "=" + s.LocalValue);
            AppendLog("ATS" + s.Num + " (" + s.Name + ")=" + s.LocalValue
                + (ans.Contains("OK") ? " OK" : " FAILED"));
          }
        }

        // AES key (AT&E=) — pad to the reported key width; hex only.
        if (aesEnabled && aesKey != origAes) {
          if (Regex.IsMatch(aesKey, @"\A[0-9a-fA-F]*\Z")) {
            var ans = DoCommand(sp, "AT&E=" + aesKey, true);
            AppendLog("AT&E (AES key)" + (ans.Contains("ERROR") ? " FAILED" : " OK"));
          } else {
            AppendLog("AES key SKIPPED (must be hex characters only)");
          }
        }

        var w = DoCommand(sp, "AT&W");
        AppendLog("AT&W (write eeprom)" + (w.Contains("OK") ? " OK" : " FAILED"));
        DoCommand(sp, "ATZ");
        SetStatus("Saved & rebooted");
      } catch (Exception ex) {
        AppendLog("Save error: " + ex.Message);
        SetStatus("Error");
      } finally {
        ClosePort(sp);
      }
    });

    IsBusy = false;
  }

  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task ResetDefaults() {
    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    var hasRemote = Registers.Any(r => r.HasRemote);
    IsBusy = true;
    Status = "Resetting…";
    AppendLog("=== Reset to Defaults ===");

    await Task.Run(() => {
      SerialPort? sp = null;
      try {
        sp = Connect(port, baud, out var used);
        if (sp == null) {
          SetStatus("Failed to enter AT mode");
          AppendLog("Could not enter AT command mode.");
          return;
        }

        AppendLog("In AT command mode @ " + used + " baud.");
        DoCommand(sp, "AT&T", false);

        if (hasRemote) {
          DoCommand(sp, "RT&F");
          DoCommand(sp, "RT&W");
          DoCommand(sp, "RTZ");
          AppendLog("Remote reset to factory defaults.");
        }

        DoCommand(sp, "AT&F");
        DoCommand(sp, "AT&W");
        DoCommand(sp, "ATZ");
        AppendLog("Local reset to factory defaults.");
        SetStatus("Reset & rebooted");
      } catch (Exception ex) {
        AppendLog("Reset error: " + ex.Message);
        SetStatus("Error");
      } finally {
        ClosePort(sp);
      }
    });

    IsBusy = false;
  }

  // Download stable vs beta SiK firmware.
  [ObservableProperty]
  private bool _betaFirmware;

  // Real SiK reflash via the linked upstream STK500 bootloader Uploader + IHex parser:
  // AT mode -> AT&UPDATE (reboot to bootloader) -> reopen @115200 -> sync + getDevice ->
  // download the matching radio~*.ihx -> upload+verify+reboot. Covers the .ihx SiK boards
  // (HM_TRP, RFD900/a/p/u). RFD900x/ux (.bin / bootloaderX) need the RFD vendor path — reported.
  [RelayCommand(CanExecute = nameof(NotBusy))]
  private async Task UploadFirmware() {
    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    IsBusy = true;
    Status = "Programming firmware…";
    AppendLog("=== Upload Firmware (" + (BetaFirmware ? "beta" : "stable") + ") ===");

    await Task.Run(() => {
      SerialPort? atsp = null;
      MissionPlanner.Comms.SerialPort? boot = null;
      try {
        atsp = Connect(port, baud, out var used);
        if (atsp == null) {
          SetStatus("Failed to enter AT mode");
          AppendLog("Could not enter AT command mode — cannot reflash.");
          return;
        }
        AppendLog("In AT mode @ " + used + " baud. Rebooting into bootloader (AT&UPDATE)…");
        atsp.DiscardInBuffer();
        atsp.Write("\r\n");
        Thread.Sleep(100);
        atsp.Write("AT&UPDATE\r\n");
        Thread.Sleep(700);
        ClosePort(atsp);
        atsp = null;

        boot = new MissionPlanner.Comms.SerialPort {
          PortName = port, BaudRate = 115200, ReadTimeout = 3000,
        };
        boot.Open();
        Thread.Sleep(300);

        var up = new MissionPlanner.Radio.Uploader();
        up.LogEvent += (m, _) => AppendLog(m.TrimEnd());
        up.ProgressEvent += pct => SetStatus($"Programming… {pct * 100:0}%");
        up.port = boot;
        up.connect_and_sync();

        var board = MissionPlanner.Radio.Uploader.Board.FAILED;
        var freq = MissionPlanner.Radio.Uploader.Frequency.FREQ_NONE;
        up.getDevice(ref board, ref freq);
        AppendLog($"Bootloader: board={board} freq={freq}");

        var url = FirmwareUrl(board, BetaFirmware);
        if (url == null) {
          SetStatus("Board not supported by SiK uploader");
          AppendLog($"Board {board} is not flashable via the SiK .ihx path (RFD900x/ux use the "
              + "vendor RFD tool). Aborted before erase.");
          return;
        }

        var fw = System.IO.Path.GetTempFileName();
        AppendLog("Downloading " + url);
        if (!MissionPlanner.Utilities.Download.getFilefromNet(url, fw)) {
          SetStatus("Firmware download failed");
          AppendLog("Could not download firmware — aborted before erase.");
          return;
        }

        var ihex = new MissionPlanner.Radio.IHex();
        ihex.load(fw);
        AppendLog($"Loaded {ihex.Count} hex blocks. Erasing + programming…");
        up.upload(boot, ihex);

        SetStatus("Firmware programmed");
        AppendLog("Firmware programmed + verified. Radio rebooted.");
      } catch (Exception ex) {
        AppendLog("Upload error: " + ex.Message);
        SetStatus("Upload failed — power-cycle the radio and retry");
      } finally {
        ClosePort(atsp);
        try {
          if (boot?.IsOpen == true) {
            boot.Close();
          }
        } catch {
          // ignore
        }
      }
    });

    IsBusy = false;
  }

  // Maps the bootloader-reported board to its firmware .ihx URL (mirrors upstream getFirmware).
  private static string? FirmwareUrl(MissionPlanner.Radio.Uploader.Board board, bool beta) {
    string ch = beta ? "beta" : "stable";
    return board switch {
      MissionPlanner.Radio.Uploader.Board.DEVICE_ID_HM_TRP =>
          $"https://firmware.ardupilot.org/SiK/{ch}/radio~hm_trp.ihx",
      MissionPlanner.Radio.Uploader.Board.DEVICE_ID_RFD900 =>
          $"https://firmware.ardupilot.org/SiK/{ch}/radio~rfd900.ihx",
      MissionPlanner.Radio.Uploader.Board.DEVICE_ID_RFD900A =>
          $"https://firmware.ardupilot.org/SiK/{ch}/radio~rfd900a.ihx",
      MissionPlanner.Radio.Uploader.Board.DEVICE_ID_RFD900U => beta
          ? "http://files.rfdesign.com.au/Files/firmware/MPSiK%20V2.6%20rfd900u.ihx"
          : "http://files.rfdesign.com.au/Files/firmware/RFDSiK%20V1.9%20rfd900u.ihx",
      MissionPlanner.Radio.Uploader.Board.DEVICE_ID_RFD900P => beta
          ? "http://files.rfdesign.com.au/Files/firmware/MPSiK%20V2.6%20rfd900p.ihx"
          : "http://files.rfdesign.com.au/Files/firmware/RFDSiK%20V1.9%20rfd900p.ihx",
      _ => null,
    };
  }

  public string UploadFirmwareTooltip { get; } =
      "Reflash SiK firmware (HM_TRP / RFD900/a/p/u). Disconnect MAVLink first.";

  // Fills the AES key with 32 random hex chars (16 bytes). Mirrors upstream btnRandom_Click.
  [RelayCommand]
  private void RandomAesKey() {
    Span<byte> bytes = stackalloc byte[16];
    RandomNumberGenerator.Fill(bytes);
    AesKey = Convert.ToHexString(bytes);
    AppendLog("Generated random AES key.");
  }

  // Copies every Local register value into the Remote column (in-memory). Mirrors upstream
  // btnCopyRequired — the user must still Save to push the changes to the remote radio.
  [RelayCommand]
  private void CopyRequiredToRemote() {
    foreach (var s in Registers) {
      if (s.Num == 0) {
        continue;
      }
      s.RemoteValue = s.LocalValue;
      s.EnsureOption(s.LocalValue);
      s.HasRemote = true;
    }
    Status = "Copy then Save to apply";
    AppendLog("Copied Local register values to Remote. Save to apply to the remote radio.");
  }

  // ---- AT command terminal (persistent session) ----

  private bool CanOpenTerminal => !IsBusy && !RssiRunning;
  private bool CanSendCommand => _session != null && TerminalOpen;

  partial void OnTerminalOpenChanged(bool value) {
    OnPropertyChanged(nameof(TerminalButtonLabel));
    SendCommandCommand.NotifyCanExecuteChanged();
    OnIsBusyChanged(false);
  }

  partial void OnRssiRunningChanged(bool value) {
    OnPropertyChanged(nameof(RssiButtonLabel));
    OnPropertyChanged(nameof(StatusLed));
    OnPropertyChanged(nameof(StatusLedLabel));
    OpenTerminalCommand.NotifyCanExecuteChanged();
    OnIsBusyChanged(false);
  }

  [RelayCommand(CanExecute = nameof(CanOpenTerminal))]
  private async Task OpenTerminal() {
    if (TerminalOpen) {
      await CloseSession();
      return;
    }

    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    AppendLog("=== Open AT terminal ===");
    await Task.Run(() => {
      var sp = Connect(port, baud, out var used);
      if (sp == null) {
        SetStatus("Failed to enter AT mode");
        return;
      }
      _session = sp;
      AppendLog("Terminal in AT command mode @ " + used + " baud. Type AT commands below.");
      Ui(() => { TerminalOpen = true; Status = "Terminal open"; });
    });
  }

  [RelayCommand(CanExecute = nameof(CanSendCommand))]
  private async Task SendCommand() {
    var sp = _session;
    var cmd = CommandText?.Trim() ?? "";
    if (sp == null || cmd.Length == 0) {
      return;
    }

    await Task.Run(() => {
      try {
        DoCommand(sp, cmd, true);
      } catch (Exception ex) {
        AppendLog("terminal error: " + ex.Message);
      }
    });
  }

  // ---- Live RSSI graph (ScottPlot) ----

  private bool CanStartRssi => !IsBusy && !TerminalOpen;

  [RelayCommand(CanExecute = nameof(CanStartRssi))]
  private async Task StartRssi() {
    if (RssiRunning) {
      await StopRssi();
      return;
    }

    if (!GuardLink()) {
      return;
    }

    var port = SelectedPort!;
    var baud = SelectedBaud;
    AppendLog("=== Start RSSI stream ===");
    RssiReset?.Invoke();

    // Enter AT mode + arm the RSSI debug report, then drop back to transparent mode.
    var ok = await Task.Run(() => {
      var sp = Connect(port, baud, out var used);
      if (sp == null) {
        SetStatus("Failed to enter AT mode");
        return false;
      }
      AppendLog("AT mode @ " + used + " baud. Enabling RSSI debug report.");
      DoCommand(sp, "AT&T=RSSI", true);
      DoCommand(sp, "ATO", false); // back to transparent mode; radio now streams RSSI lines
      _session = sp;
      return true;
    });

    if (!ok) {
      return;
    }

    _rssiCts = new System.Threading.CancellationTokenSource();
    var token = _rssiCts.Token;
    RssiRunning = true;
    Status = "RSSI streaming";

    // Detached reader so the command returns and the same button can Stop it.
    _ = Task.Run(() => RssiLoop(token));
  }

  private void RssiLoop(System.Threading.CancellationToken token) {
    var sp = _session;
    if (sp == null) {
      return;
    }
    var tickStart = Environment.TickCount;
    try {
      while (!token.IsCancellationRequested && sp.IsOpen) {
        try {
          sp.WriteLine("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
          if (sp.BytesToRead < 50) {
            System.Threading.Thread.Sleep(150);
            continue;
          }
          var line = ReadLine(sp);
          var match = RssiLine.Match(line);
          if (match.Success) {
            var t = (Environment.TickCount - tickStart) / 1000.0;
            RssiSample?.Invoke(t,
                double.Parse(match.Groups[1].Value), double.Parse(match.Groups[2].Value),
                double.Parse(match.Groups[3].Value), double.Parse(match.Groups[4].Value));
          }
        } catch {
          System.Threading.Thread.Sleep(150);
        }
      }
    } catch (Exception ex) {
      AppendLog("RSSI loop error: " + ex.Message);
    }
  }

  [RelayCommand]
  private async Task StopRssi() {
    _rssiCts?.Cancel();
    Ui(() => { RssiRunning = false; Status = "Idle"; });
    await Task.Run(() => {
      System.Threading.Thread.Sleep(250); // let the detached reader loop observe the cancel
      var sp = _session;
      if (sp == null) {
        return;
      }
      try {
        // leave transparent mode, disable RSSI report, resume transparent, then drop the port
        EnterCommandMode(sp);
        DoCommand(sp, "AT&T", false);
        DoCommand(sp, "ATO", false);
      } catch {
      }
      ClosePort(sp);
      _session = null;
    });
    OnIsBusyChanged(false); // _session is null again: re-enable Load/Save/etc.
    AppendLog("RSSI stream stopped.");
  }

  private async Task CloseSession() {
    _rssiCts?.Cancel();
    await Task.Run(() => {
      var sp = _session;
      if (sp != null) {
        try {
          DoCommand(sp, "ATO", false);
        } catch {
        }
        ClosePort(sp);
        _session = null;
      }
    });
    Ui(() => { TerminalOpen = false; RssiRunning = false; Status = "Idle"; });
    AppendLog("Session closed.");
  }

  private SerialPort? Connect(string port, int preferredBaud, out int usedBaud) {
    foreach (var baud in new[] { preferredBaud }.Concat(CandidateBauds).Distinct()) {
      SerialPort? sp = null;
      try {
        sp = OpenPort(port, baud);
        AppendLog("Probing " + port + " @ " + baud + "…");
        if (EnterCommandMode(sp)) {
          usedBaud = baud;
          return sp;
        }
      } catch (Exception ex) {
        AppendLog("Open " + baud + " failed: " + ex.Message);
      }
      ClosePort(sp);
    }
    usedBaud = 0;
    return null;
  }

  private static SerialPort OpenPort(string port, int baud) {
    var sp = new SerialPort(port, baud) {
      ReadTimeout = 1500,
      WriteTimeout = 1500,
      NewLine = "\r\n",
      DtrEnable = false,
      RtsEnable = false,
    };
    sp.Open();
    return sp;
  }

  private static void ClosePort(SerialPort? sp) {
    try {
      if (sp != null) {
        if (sp.IsOpen) {
          sp.Close();
        }
        sp.Dispose();
      }
    } catch {
      // ignore
    }
  }

  private bool EnterCommandMode(SerialPort sp) {
    if (ProbeAt(sp)) {
      return true;
    }

    for (var t = 0; t < 3; t++) {
      try {
        sp.DiscardInBuffer();
        // 1s guard time of silence, +++ (no CR), 1s guard time.
        Thread.Sleep(1200);
        sp.Write("+++");
        Thread.Sleep(1200);
        var resp = sp.ReadExisting();
        if (resp.Contains("OK") || ProbeAt(sp)) {
          return true;
        }
      } catch {
        // retry
      }
    }
    return false;
  }

  private bool ProbeAt(SerialPort sp) {
    var v = DoCommand(sp, "ATI").Trim();
    return SikBanner.IsMatch(v) || v.Contains(" on ");
  }

  private string DoCommand(SerialPort sp, string cmd, bool multiLine = false) {
    if (!sp.IsOpen) {
      return "";
    }

    try {
      sp.DiscardInBuffer();
      AppendLog(">> " + cmd);
      sp.Write("\r\n");
      ReadLine(sp);
      Thread.Sleep(50);
      sp.Write(cmd + "\r\n");

      var echo = ReadLine(sp);
      if (!echo.Contains(cmd)) {
        sp.DiscardInBuffer();
        sp.Write(cmd + "\r\n");
        echo = ReadLine(sp);
        if (!echo.Contains(cmd)) {
          return "";
        }
      }

      string value;
      if (multiLine) {
        var sb = new StringBuilder();
        var deadline = DateTime.Now.AddMilliseconds(1000);
        while (sp.BytesToRead > 0 || DateTime.Now < deadline) {
          var line = ReadLine(sp);
          if (line.Length > 0) {
            sb.Append(line).Append('\n');
          }
        }
        value = sb.ToString();
      } else {
        value = ReadLine(sp);
      }

      var trimmed = value.Trim();
      if (trimmed.Length > 0) {
        AppendLog("<< " + trimmed.Replace("\n", "\n   "));
      }
      return value;
    } catch (Exception ex) {
      AppendLog("cmd '" + cmd + "' error: " + ex.Message);
      return "";
    }
  }

  private static string ReadLine(SerialPort sp) {
    try {
      return sp.ReadLine();
    } catch {
      return "";
    }
  }

  // Allowed numeric values per register name. null => keep as a free-text numeric field.
  private static string[]? DefaultOptions(string name, bool rfd) {
    static string[] Range(int from, int step, int to) {
      var list = new List<string>();
      for (var v = from; v <= to; v += step) {
        list.Add(v.ToString());
      }
      return list.ToArray();
    }

    switch (name) {
      case "SERIAL_SPEED":
        return rfd
            ? new[] { "1", "2", "4", "9", "19", "38", "57", "115", "230", "460" }
            : new[] { "1", "2", "4", "9", "19", "38", "57", "115", "230" };
      case "AIR_SPEED":
        return rfd
            ? new[] { "4", "64", "125", "250", "500" }
            : new[] { "2", "4", "8", "16", "19", "24", "32", "48", "64", "96", "125", "128", "192", "250" };
      case "TXPOWER":
        return rfd ? Range(0, 1, 30) : new[] { "1", "2", "5", "8", "11", "14", "17", "20" };
      case "ECC":
      case "OPPRESEND":
      case "MANCHESTER":
      case "RTSCTS":
        return new[] { "0", "1" };
      case "MAVLINK":
        return new[] { "0", "1", "2" };
      case "DUTY_CYCLE":
        return Range(10, 10, 100);
      case "LBT_RSSI":
        return rfd ? Range(0, 25, 220) : new[] { "0" };
      default:
        return null; // FORMAT/NETID/MIN_FREQ/MAX_FREQ/NUM_CHANNELS/MAX_WINDOW => free text.
    }
  }

  // Re-key the option sets once the firmware banner tells us whether this is an RFD-class radio.
  private void ApplyFirmwareOptions(bool rfd) {
    Ui(() => {
      foreach (var r in Registers) {
        var opts = DefaultOptions(r.Name, rfd);
        if (opts != null) {
          r.SetOptions(opts);
          r.EnsureOption(r.LocalValue);
        }
      }
    });
  }

  private void ParseInto(string block, bool remote) {
    foreach (var raw in block.Split('\n')) {
      var m = RegLine.Match(raw);
      if (!m.Success) {
        continue;
      }

      var num = int.TryParse(m.Groups[1].Value, out var parsedNum) ? parsedNum : -1;
      var name = m.Groups[2].Value.Trim();
      var val = m.Groups[3].Value.Trim();
      var reg = Registers.FirstOrDefault(r => r.Name == name);

      // Surface extra/GPIO/encryption registers the radio reports beyond the standard S0..S15
      // set, capturing the real S-number so writes target the correct register.
      if (reg == null) {
        if (remote) {
          continue; // only attach remote values to registers we already know locally
        }
        var added = new SikRegister(num, name, name);
        var opts = DefaultOptions(name, rfd: false);
        if (opts != null) {
          added.SetOptions(opts);
        }
        Ui(() => Registers.Add(added));
        reg = added;
      }

      Ui(() => {
        if (remote) {
          reg.RemoteValue = val;
          reg.OrigRemote = val;
          reg.HasRemote = true;
          reg.EnsureOption(val);
        } else {
          reg.Num = num >= 0 ? num : reg.Num;
          reg.LocalValue = val;
          reg.OrigLocal = val;
          reg.EnsureOption(val);
        }
      });
    }
  }

  private void ResetOrig(bool remote) {
    Ui(() => {
      foreach (var r in Registers) {
        if (remote) {
          r.RemoteValue = "";
          r.OrigRemote = "";
          r.HasRemote = false;
        } else {
          r.LocalValue = "";
          r.OrigLocal = "";
        }
      }
    });
  }

  private void SetStatus(string s) => Ui(() => Status = s);

  private void Ui(Action a) {
    if (Dispatcher.UIThread.CheckAccess()) {
      a();
    } else {
      Dispatcher.UIThread.Post(a);
    }
  }

  private void AppendLog(string line) {
    void Do() => Log += (Log.Length > 0 ? "\n" : "") + line;
    if (Dispatcher.UIThread.CheckAccess()) {
      Do();
    } else {
      Dispatcher.UIThread.Post(Do);
    }
  }
}
