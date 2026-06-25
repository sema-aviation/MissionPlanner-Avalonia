using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    public int Num { get; }
    public string Name { get; }
    public string Label { get; }
    public string OrigLocal { get; set; } = "";
    public string OrigRemote { get; set; } = "";
    public bool HasRemote { get; set; }

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

  public SikRadioViewModel() {
    foreach (var (num, name, label) in RegMap) {
      Registers.Add(new SikRegister(num, name, label));
    }

    RefreshPorts();

    var baud = AppState.comPort.BaseStream?.BaudRate ?? 0;
    if (baud > 0 && Bauds.Contains(baud)) {
      SelectedBaud = baud;
    }
  }

  private bool LinkOpen => AppState.comPort.BaseStream?.IsOpen == true;
  private bool NotBusy => !IsBusy;

  partial void OnIsBusyChanged(bool value) {
    LoadCommand.NotifyCanExecuteChanged();
    SaveCommand.NotifyCanExecuteChanged();
    ResetDefaultsCommand.NotifyCanExecuteChanged();
    RefreshPortsCommand.NotifyCanExecuteChanged();
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

        ResetOrig(remote: false);
        ParseInto(DoCommand(sp, "ATI5", true), remote: false);

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
            var ans = DoCommand(sp, "ATS" + s.Num + "=" + s.LocalValue);
            AppendLog("ATS" + s.Num + " (" + s.Name + ")=" + s.LocalValue
                + (ans.Contains("OK") ? " OK" : " FAILED"));
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

  // Firmware upload over the SiK bootloader (Download .ihx/.bin + STK500-style flash) is a
  // deferred subsystem; the Upload button stays disabled until it is wired.
  [RelayCommand(CanExecute = nameof(NotBusy))]
  private void UploadFirmware() {
    AppendLog("Firmware upload is not yet wired (deferred).");
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

  private void ParseInto(string block, bool remote) {
    foreach (var raw in block.Split('\n')) {
      var m = RegLine.Match(raw);
      if (!m.Success) {
        continue;
      }

      var name = m.Groups[2].Value.Trim();
      var val = m.Groups[3].Value.Trim();
      var reg = Registers.FirstOrDefault(r => r.Name == name);
      if (reg == null) {
        continue;
      }

      Ui(() => {
        if (remote) {
          reg.RemoteValue = val;
          reg.OrigRemote = val;
          reg.HasRemote = true;
        } else {
          reg.LocalValue = val;
          reg.OrigLocal = val;
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
