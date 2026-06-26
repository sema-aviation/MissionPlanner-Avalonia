using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.Services;

// Modern auto-update check (mirrors MP Utilities/Update.cs intent). Queries the GitHub releases
// API for the latest release, compares to the running version, downloads the chosen asset into a
// local cache, and notifies via Dialogs.
//
// BLOCKED / platform-specific: the actual in-place install (upstream's ".new" file swap +
// Updater.exe handoff on Windows) is installer-specific and is NOT implemented here. CheckAsync /
// DownloadAsync / CheckAndNotifyAsync provide check + download + notify only; the downloaded
// asset path is returned for an external/platform installer to apply.
public static class Updater {
  // Avalonia port repo (override if the release feed moves).
  public const string DefaultOwnerRepo = "sema-aviation/MissionPlanner-Avalonia";

  private static readonly HttpClient Http = CreateClient();

  private static HttpClient CreateClient() {
    var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    // GitHub API requires a User-Agent.
    c.DefaultRequestHeaders.UserAgent.ParseAdd("MissionPlannerAvalonia-Updater");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    return c;
  }

  public sealed class UpdateInfo {
    public string TagName { get; init; } = "";
    public string Name { get; init; } = "";
    public Version? RemoteVersion { get; init; }
    public Version? LocalVersion { get; init; }
    public bool IsNewer { get; init; }
    public string? AssetName { get; init; }
    public string? AssetUrl { get; init; }
    public long AssetSize { get; init; }
    public string? HtmlUrl { get; init; }
  }

  // Query the latest release and compare to the running assembly version.
  public static async Task<UpdateInfo?> CheckAsync(string ownerRepo = DefaultOwnerRepo,
      CancellationToken ct = default) {
    var url = $"https://api.github.com/repos/{ownerRepo}/releases/latest";
    using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
    if (!resp.IsSuccessStatusCode) {
      return null;
    }

    await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
    var root = doc.RootElement;

    string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
    string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
    string? html = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

    var remote = ParseVersion(tag) ?? ParseVersion(name);
    var local = RunningVersion();

    // Prefer a platform asset; fall back to the first asset.
    string? assetName = null, assetUrl = null;
    long assetSize = 0;
    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array) {
      var list = assets.EnumerateArray().ToList();
      var pick = list.FirstOrDefault(MatchesPlatform);
      if (pick.ValueKind != JsonValueKind.Undefined ||
          (list.Count > 0 && (pick = list[0]).ValueKind != JsonValueKind.Undefined)) {
        assetName = pick.TryGetProperty("name", out var an) ? an.GetString() : null;
        assetUrl = pick.TryGetProperty("browser_download_url", out var au) ? au.GetString() : null;
        assetSize = pick.TryGetProperty("size", out var asz) ? asz.GetInt64() : 0;
      }
    }

    return new UpdateInfo {
      TagName = tag,
      Name = name,
      RemoteVersion = remote,
      LocalVersion = local,
      IsNewer = remote != null && local != null && remote > local,
      AssetName = assetName,
      AssetUrl = assetUrl,
      AssetSize = assetSize,
      HtmlUrl = html,
    };
  }

  // Download the release asset into the local update cache. Returns the saved file path.
  public static async Task<string?> DownloadAsync(UpdateInfo info,
      IProgress<double>? progress = null, CancellationToken ct = default) {
    if (string.IsNullOrEmpty(info.AssetUrl) || string.IsNullOrEmpty(info.AssetName)) {
      return null;
    }

    string dir = CacheDir();
    Directory.CreateDirectory(dir);
    string outPath = Path.Combine(dir, info.AssetName);

    using var resp = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct)
        .ConfigureAwait(false);
    resp.EnsureSuccessStatusCode();

    long total = resp.Content.Headers.ContentLength ?? info.AssetSize;
    await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    await using var dst = File.Create(outPath);

    var buffer = new byte[81920];
    long read = 0;
    int n;
    while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0) {
      await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
      read += n;
      if (total > 0) {
        progress?.Report(Math.Clamp(read * 100.0 / total, 0, 100));
      }
    }
    return outPath;
  }

  // Check, and if a newer release exists, prompt the user (Dialogs) to download it.
  public static async Task CheckAndNotifyAsync(string ownerRepo = DefaultOwnerRepo) {
    UpdateInfo? info;
    try {
      info = await CheckAsync(ownerRepo).ConfigureAwait(true);
    } catch (Exception ex) {
      await Dialogs.Alert("Update", "Update check failed: " + ex.Message);
      return;
    }

    if (info == null) {
      await Dialogs.Alert("Update", "Could not reach the update server.");
      return;
    }
    if (!info.IsNewer) {
      await Dialogs.Alert("Update",
          $"You are up to date (running {info.LocalVersion?.ToString() ?? "?"}).");
      return;
    }

    bool download = await Dialogs.Confirm("Update available",
        $"A new version is available: {info.TagName}\n" +
        $"(you have {info.LocalVersion?.ToString() ?? "?"}).\n\nDownload it now?");
    if (!download) {
      return;
    }

    if (string.IsNullOrEmpty(info.AssetUrl)) {
      // No downloadable asset — open the release page instead.
      if (!string.IsNullOrEmpty(info.HtmlUrl)) {
        Dialogs.OpenUrl(info.HtmlUrl);
      }
      return;
    }

    var progress = new ProgressReporter("Downloading update");
    progress.Show2();
    try {
      var path = await DownloadAsync(info, new Progress<double>(p => progress.Set(p, "Downloading…")))
          .ConfigureAwait(true);
      progress.Close();
      if (path != null) {
        // Installing in place is platform-specific (see class note) — just reveal the download.
        await Dialogs.Alert("Update downloaded",
            "Saved to:\n" + path + "\n\nInstall it manually (in-place auto-install is " +
            "platform-specific and not available in this build).");
      }
    } catch (Exception ex) {
      progress.Close();
      await Dialogs.Alert("Update", "Download failed: " + ex.Message);
    }
  }

  // --- helpers ----------------------------------------------------------------------------------

  private static string CacheDir() {
    try {
      return Path.Combine(Settings.GetUserDataDirectory(), "updatecache");
    } catch {
      return Path.Combine(Path.GetTempPath(), "MissionPlannerAvalonia", "updatecache");
    }
  }

  public static Version? RunningVersion() {
    var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
    return asm.GetName().Version;
  }

  // Parse a CalVer/SemVer tag like "v2026.06.25" or "2026.6.25-beta" into a Version.
  private static Version? ParseVersion(string? tag) {
    if (string.IsNullOrWhiteSpace(tag)) {
      return null;
    }
    var s = tag.Trim();
    if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
      s = s.Substring(1);
    }
    // strip any pre-release / build suffix
    int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
    if (cut >= 0) {
      s = s.Substring(0, cut);
    }
    return Version.TryParse(s, out var v) ? v : null;
  }

  private static bool MatchesPlatform(JsonElement asset) {
    if (!asset.TryGetProperty("name", out var n)) {
      return false;
    }
    var name = (n.GetString() ?? "").ToLowerInvariant();
    if (OperatingSystem.IsWindows()) {
      return name.Contains("win") || name.EndsWith(".exe") || name.EndsWith(".msi");
    }
    if (OperatingSystem.IsMacOS()) {
      return name.Contains("mac") || name.Contains("osx") || name.EndsWith(".dmg") ||
             name.EndsWith(".pkg");
    }
    if (OperatingSystem.IsLinux()) {
      return name.Contains("linux") || name.EndsWith(".appimage") || name.EndsWith(".deb") ||
             name.EndsWith(".tar.gz");
    }
    return false;
  }
}
