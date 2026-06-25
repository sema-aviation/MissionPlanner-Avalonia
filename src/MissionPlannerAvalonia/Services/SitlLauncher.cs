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

public class SitlLauncher {
  private const string SitlBaseUrl =
      "https://firmware.ardupilot.org/Tools/MissionPlanner/sitl/";
  private const string ManifestUrl = "https://firmware.ardupilot.org/manifest.json.gz";
  private const string DefaultHome = "-35.363261,149.165230,584,353";
  private const string Host = "127.0.0.1";
  private const int TcpPort = 5760;

  // Cygwin runtime shipped alongside the Windows SITL builds.
  private static readonly string[] CygwinDlls = {
    "cygatomic-1.dll", "cyggcc_s-1.dll", "cyggcc_s-seh-1.dll", "cyggomp-1.dll",
    "cygiconv-2.dll", "cygintl-8.dll", "cygquadmath-0.dll", "cygssp-0.dll",
    "cygstdc++-6.dll", "cygwin1.dll"
  };

  private static readonly HttpClient _http =
      new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

  private Process? _process;

  public event Action<string>? Log;

  public bool IsRunning => _process is { HasExited: false };

  public string TcpEndpoint => $"tcp:{Host}:{TcpPort}";

  private static string CacheDir => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "MissionPlannerAvalonia", "sitl");

  public async Task<bool> StartAsync(SitlVehicle vehicle) {
    if (IsRunning) {
      Emit("SITL already running.");
      return true;
    }

    var (exeName, frame) = Map(vehicle);

    string? binary;
    try {
      binary = await EnsureBinaryAsync(exeName).ConfigureAwait(false);
    } catch (Exception ex) {
      Emit($"SITL download failed: {ex.Message}");
      return false;
    }

    if (binary == null || !File.Exists(binary)) {
      Emit("Could not obtain a SITL binary for this platform.");
      return false;
    }

    var workdir = Path.Combine(CacheDir, frame);
    Directory.CreateDirectory(workdir);

    var args = $"--model {frame} --home {DefaultHome} -I0 --serial0 tcp:0";
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

    _process.OutputDataReceived += (_, e) => { if (e.Data != null) { Emit(e.Data); } };
    _process.ErrorDataReceived += (_, e) => { if (e.Data != null) { Emit(e.Data); } };
    _process.BeginOutputReadLine();
    _process.BeginErrorReadLine();

    Emit($"Waiting for {TcpEndpoint} ...");
    if (await WaitForPortAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false)) {
      Emit($"SITL listening on {TcpEndpoint}.");
      return true;
    }

    Emit("Timed out waiting for SITL to open its TCP port.");
    Stop();
    return false;
  }

  public void Stop() {
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

  private static (string exeName, string frame) Map(SitlVehicle vehicle) => vehicle switch {
    SitlVehicle.Plane => ("ArduPlane", "plane"),
    SitlVehicle.Copter => ("ArduCopter", "quad"),
    SitlVehicle.Rover => ("ArduRover", "rover"),
    SitlVehicle.Heli => ("ArduHeli", "heli"),
    _ => ("ArduCopter", "quad"),
  };

  private async Task<string?> EnsureBinaryAsync(string exeName) {
    Directory.CreateDirectory(CacheDir);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      var path = Path.Combine(CacheDir, exeName + ".exe");
      if (File.Exists(path)) {
        Emit($"Using cached {Path.GetFileName(path)}.");
        return path;
      }
      Emit($"Downloading {exeName} (Windows) ...");
      await DownloadAsync(SitlBaseUrl + exeName + ".elf", path).ConfigureAwait(false);
      foreach (var dll in CygwinDlls) {
        var dllPath = Path.Combine(CacheDir, dll);
        if (!File.Exists(dllPath)) {
          try {
            await DownloadAsync(SitlBaseUrl + dll, dllPath).ConfigureAwait(false);
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
      if (File.Exists(path)) {
        Emit($"Using cached {Path.GetFileName(path)}.");
        return path;
      }
      Emit($"Resolving {exeName} for {platform} from manifest ...");
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

  private async Task<string?> ResolveLinuxUrlAsync(string platform, string exeName) {
    using var stream = await _http.GetStreamAsync(ManifestUrl).ConfigureAwait(false);
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
        var connect = client.ConnectAsync(Host, TcpPort);
        if (await Task.WhenAny(connect, Task.Delay(500)).ConfigureAwait(false) == connect &&
            client.Connected) {
          return true;
        }
      } catch {
        // not listening yet
      }
      await Task.Delay(500).ConfigureAwait(false);
    }
    return false;
  }

  private void Emit(string message) => Log?.Invoke(message);
}
