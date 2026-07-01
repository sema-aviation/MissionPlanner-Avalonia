using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace MissionPlannerAvalonia.Services;

public enum SitlVehicle { Plane, Copter, Rover, Heli }

public enum SitlChannel { Dev, Beta, Stable, Skip }

public sealed class SitlStartOptions {
  public SitlVehicle Vehicle { get; init; }
  public SitlChannel Channel { get; init; } = SitlChannel.Dev;

  public string Model { get; init; } = "";

  public string Home { get; init; } = "";
  public int Speed { get; init; } = 1;
  public string ExtraCmdline { get; init; } = "";
  public bool WipeEeprom { get; init; }
}

public class SitlLauncher {
  private const string _sitlBaseUrl =
      "https://firmware.ardupilot.org/Tools/MissionPlanner/sitl/";
  private const string _manifestUrl = "https://firmware.ardupilot.org/manifest.json.gz";
  private const string _defaultHome = "-35.363261,149.165230,584,353";
  private const string _host = "127.0.0.1";
  private const int _tcpPort = 5760;
  private const int _rcOverridePort = 5501;

  private static readonly string[] _cygwinDlls = {
    "cygatomic-1.dll", "cyggcc_s-1.dll", "cyggcc_s-seh-1.dll", "cyggomp-1.dll",
    "cygiconv-2.dll", "cygintl-8.dll", "cygquadmath-0.dll", "cygssp-0.dll",
    "cygstdc++-6.dll", "cygwin1.dll"
  };

  private static readonly HttpClient _http =
      new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

  private Process? _process;

  private UdpClient? _rcSend;

  private static readonly System.Collections.Generic.List<SitlLauncher> _live = new();

  public static void StopAll() {
    SitlLauncher[] all;
    lock (_live) {
      all = _live.ToArray();
    }
    foreach (var s in all) {
      s.Stop();
    }
  }

  public static bool PlatformSupported =>
      RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
      RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

  public event Action<string>? Log;

  public bool IsRunning => _process is { HasExited: false };

  public string TcpEndpoint => $"tcp:{_host}:{_tcpPort}";

  private static string CacheDir => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "MissionPlannerAvalonia", "sitl");

  public static (string ExeName, string DefaultModel) Map(SitlVehicle vehicle) => vehicle switch {
    SitlVehicle.Plane => ("ArduPlane", "plane"),
    SitlVehicle.Copter => ("ArduCopter", "+"),
    SitlVehicle.Rover => ("ArduRover", "rover"),
    SitlVehicle.Heli => ("ArduHeli", "heli"),
    _ => ("ArduCopter", "+"),
  };

  public async Task<bool> StartAsync(SitlStartOptions opts) {
    if (IsRunning) {
      Emit("SITL already running.");
      return true;
    }

    var (exeName, defaultModel) = Map(opts.Vehicle);

    var model = string.IsNullOrWhiteSpace(opts.Model) ? defaultModel : opts.Model.Trim();

    string? binary;
    try {
      binary = await EnsureBinaryAsync(exeName, opts.Channel).ConfigureAwait(false);
    } catch (Exception ex) {
      Emit($"SITL download failed: {ex.Message}");
      return false;
    }

    if (binary == null || !File.Exists(binary)) {
      Emit("Could not obtain a SITL binary for this platform/channel.");
      return false;
    }

    var workdir = Path.Combine(CacheDir, SafeDir(model));
    Directory.CreateDirectory(workdir);

    var home = string.IsNullOrWhiteSpace(opts.Home) ? _defaultHome : opts.Home.Trim();
    var speed = opts.Speed <= 0 ? 1 : opts.Speed;

    var extra = opts.ExtraCmdline?.Trim() ?? "";
    if (opts.WipeEeprom) {
      extra = (extra + " --wipe").Trim();
    }

    var args =
        $"--model {model} --home {home} -O{home} -s{speed} -I0 --serial0 tcp:0";
    if (!string.IsNullOrEmpty(extra)) {
      args += " " + extra;
    }

    var psi = new ProcessStartInfo {
      FileName = binary,
      Arguments = args,
      WorkingDirectory = workdir,
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true,
    };
    psi.EnvironmentVariables["HOME"] = workdir;

    try {
      Emit($"Starting SITL: {Path.GetFileName(binary)} {args}");
      _process = Process.Start(psi);
    } catch (Exception ex) {
      Emit($"Failed to start SITL process: {ex.Message}");
      _process = null;
      return false;
    }

    if (_process == null) {
      Emit("Failed to start SITL process.");
      return false;
    }

    lock (_live) {
      _live.Add(this);
    }
    _process.OutputDataReceived += (_, e) => { if (e.Data != null) { Emit(e.Data); } };
    _process.ErrorDataReceived += (_, e) => { if (e.Data != null) { Emit(e.Data); } };
    _process.BeginOutputReadLine();
    _process.BeginErrorReadLine();

    Emit($"Waiting for {TcpEndpoint} ...");
    if (await WaitForPortAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false)) {
      Emit($"SITL listening on {TcpEndpoint}.");
      OpenRcOverride();
      return true;
    }

    Emit("Timed out waiting for SITL to open its TCP port.");
    Stop();
    return false;
  }

  private void OpenRcOverride() {
    try {
      _rcSend?.Dispose();
      _rcSend = new UdpClient();
      _rcSend.Connect(_host, _rcOverridePort);
    } catch (Exception ex) {
      Emit($"RC override socket unavailable: {ex.Message}");
      _rcSend = null;
    }
  }

  public void SendRcInput() {
    var send = _rcSend;
    var cs = MissionPlannerAvalonia.AppState.comPort.MAV?.cs;
    if (send == null || cs == null) {
      return;
    }

    try {
      var buf = new byte[2 * 8];
      void Put(int i, int v) =>
          Array.ConstrainedCopy(BitConverter.GetBytes((ushort)v), 0, buf, i * 2, 2);
      Put(0, cs.rcoverridech1);
      Put(1, cs.rcoverridech2);
      Put(2, cs.rcoverridech3);
      Put(3, cs.rcoverridech4);
      Put(4, cs.rcoverridech5);
      Put(5, cs.rcoverridech6);
      Put(6, cs.rcoverridech7);
      Put(7, cs.rcoverridech8);
      send.Send(buf, buf.Length);
    } catch {

    }
  }

  public void Stop() {
    lock (_live) {
      _live.Remove(this);
    }
    try {
      _rcSend?.Dispose();
    } catch {

    }
    _rcSend = null;

    var proc = _process;
    _process = null;
    if (proc == null) {
      return;
    }

    try {
      if (!proc.HasExited) {
        proc.Kill(entireProcessTree: true);
      }
    } catch (Exception ex) {
      Emit($"Failed to stop SITL: {ex.Message}");
    } finally {
      proc.Dispose();
    }
  }

  private static string SafeDir(string model) {
    foreach (var c in Path.GetInvalidFileNameChars()) {
      model = model.Replace(c, '_');
    }
    return string.IsNullOrEmpty(model) ? "sitl" : model;
  }

  private async Task<string?> EnsureBinaryAsync(string exeName, SitlChannel channel) {
    Directory.CreateDirectory(CacheDir);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      var path = Path.Combine(CacheDir, exeName + ".exe");

      if (channel == SitlChannel.Skip) {
        if (File.Exists(path)) {
          Emit($"Skip download — using cached {Path.GetFileName(path)}.");
          return path;
        }
        Emit("Skip download selected but no cached SITL binary is present.");
        return null;
      }

      var baseUrl = WindowsBaseUrl(channel, exeName);
      Emit($"Downloading {exeName} ({channel}, Windows) from {baseUrl} ...");
      await DownloadAsync(baseUrl + exeName + ".elf", path).ConfigureAwait(false);
      foreach (var dll in _cygwinDlls) {
        var dllPath = Path.Combine(CacheDir, dll);
        if (!File.Exists(dllPath)) {
          try {
            await DownloadAsync(baseUrl + dll, dllPath).ConfigureAwait(false);
          } catch (Exception ex) {
            Emit($"Optional dependency {dll} failed: {ex.Message}");
          }
        }
      }
      return path;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
      var platform = (RuntimeInformation.OSArchitecture is Architecture.Arm or Architecture.Arm64)
          ? "SITL_arm_linux_gnueabihf"
          : "SITL_x86_64_linux_gnu";
      var path = Path.Combine(CacheDir, exeName);

      if (channel == SitlChannel.Skip) {
        if (File.Exists(path)) {
          Emit($"Skip download — using cached {Path.GetFileName(path)}.");
          return path;
        }
        Emit("Skip download selected but no cached SITL binary is present.");
        return null;
      }

      if (File.Exists(path)) {
        Emit($"Using cached {Path.GetFileName(path)}.");
        return path;
      }

      Emit($"Resolving {exeName} for {platform} from manifest ({channel}) ...");
      var url = await ResolveLinuxUrlAsync(platform, exeName).ConfigureAwait(false);
      if (url == null) {
        Emit($"No {platform} SITL build for {exeName} in the firmware manifest.");
        return null;
      }
      Emit($"Downloading {exeName} (Linux) ...");
      await DownloadAsync(url, path).ConfigureAwait(false);
      MakeExecutable(path);
      return path;
    }

    Emit("Prebuilt ArduPilot SITL binaries are not published for this platform " +
         "(macOS/other). Build SITL from source or run it under Linux/WSL.");
    return null;
  }

  private static string WindowsBaseUrl(SitlChannel channel, string exeName) {
    switch (channel) {
      case SitlChannel.Beta:
        return _sitlBaseUrl + "Beta/";
      case SitlChannel.Stable:
        var n = exeName.ToLowerInvariant();
        if (n.Contains("plane")) {
          return _sitlBaseUrl + "PlaneStable/";
        }
        if (n.Contains("rover")) {
          return _sitlBaseUrl + "RoverStable/";
        }

        return _sitlBaseUrl + "CopterStable/";
      default:
        return _sitlBaseUrl;
    }
  }

  private async Task<string?> ResolveLinuxUrlAsync(string platform, string exeName) {
    using var stream = await _http.GetStreamAsync(_manifestUrl).ConfigureAwait(false);
    using var gz = new GZipStream(stream, CompressionMode.Decompress);
    using var doc = await JsonDocument.ParseAsync(gz).ConfigureAwait(false);

    if (!doc.RootElement.TryGetProperty("firmware", out var firmware)) {
      return null;
    }

    string? fallback = null;
    foreach (var entry in firmware.EnumerateArray()) {
      if (!entry.TryGetProperty("platform", out var p) ||
          !string.Equals(p.GetString(), platform, StringComparison.OrdinalIgnoreCase)) {
        continue;
      }

      if (!entry.TryGetProperty("url", out var u)) {
        continue;
      }

      var url = u.GetString();
      if (url == null) {
        continue;
      }

      var leaf = url.TrimEnd('/');
      leaf = leaf.Substring(leaf.LastIndexOf('/') + 1);
      if (!string.Equals(leaf, exeName, StringComparison.OrdinalIgnoreCase)) {
        continue;
      }

      var latest = entry.TryGetProperty("latest", out var l) && l.TryGetInt64(out var lv) && lv == 1;
      if (latest) {
        return url;
      }

      fallback ??= url;
    }
    return fallback;
  }

  private async Task DownloadAsync(string url, string destination) {
    var tmp = destination + ".part";
    using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
               .ConfigureAwait(false)) {
      resp.EnsureSuccessStatusCode();
      using var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
      using var dst = File.Create(tmp);
      await src.CopyToAsync(dst).ConfigureAwait(false);
    }
    if (File.Exists(destination)) {
      File.Delete(destination);
    }

    File.Move(tmp, destination);
  }

  private void MakeExecutable(string path) {
    if (OperatingSystem.IsWindows()) {
      return;
    }
    try {
      File.SetUnixFileMode(path,
          UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
          UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
          UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    } catch (Exception ex) {
      Emit($"Could not set executable bit: {ex.Message}");
    }
  }

  private async Task<bool> WaitForPortAsync(TimeSpan timeout) {
    var deadline = DateTime.UtcNow + timeout;
    while (DateTime.UtcNow < deadline) {
      if (!IsRunning) {
        Emit("SITL process exited before opening its port.");
        return false;
      }
      try {
        using var client = new TcpClient();
        var connect = client.ConnectAsync(_host, _tcpPort);
        if (await Task.WhenAny(connect, Task.Delay(500)).ConfigureAwait(false) == connect &&
            client.Connected) {
          return true;
        }
      } catch {

      }
      await Task.Delay(500).ConfigureAwait(false);
    }
    return false;
  }

  private void Emit(string message) => Log?.Invoke(message);
}
