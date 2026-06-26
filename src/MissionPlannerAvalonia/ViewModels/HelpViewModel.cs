using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class HelpViewModel : ViewModelBase {
  private const string ReleasesApi =
      "https://api.github.com/repos/sema-aviation/MissionPlanner-Avalonia/releases";
  private const string ReleasesPage =
      "https://github.com/sema-aviation/MissionPlanner-Avalonia/releases";

  private static readonly HttpClient _http = CreateClient();

  // Running version + git hash, e.g. "Version 2026.6.2 (f7427a5)".
  public string AppVersionDisplay => "Version " + Services.AppVersion.Full;

  [ObservableProperty]
  private string _updateStatus = "";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
  [NotifyCanExecuteChangedFor(nameof(CheckForBetaUpdatesCommand))]
  [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
  private bool _isChecking;

  [RelayCommand(CanExecute = nameof(CanCheck))]
  private Task CheckForUpdates() => CheckAsync(beta: false);

  [RelayCommand(CanExecute = nameof(CanCheck))]
  private Task CheckForBetaUpdates() => CheckAsync(beta: true);

  // Full modern-equivalent of the upstream auto-installer: check + download the asset + notify.
  // (The in-place .new swap / Updater.exe handoff is Windows-installer-specific — Updater leaves the
  // downloaded asset for an external installer.)
  [RelayCommand(CanExecute = nameof(CanCheck))]
  private async Task DownloadUpdate() {
    IsChecking = true;
    UpdateStatus = "Checking + downloading latest release…";
    try {
      await Services.Updater.CheckAndNotifyAsync();
      UpdateStatus = "Update check complete.";
    } catch (Exception ex) {
      UpdateStatus = "Update failed: " + ex.Message;
    } finally {
      IsChecking = false;
    }
  }

  private bool CanCheck() => !IsChecking;

  // The in-app auto-update installer from upstream Mission Planner is not ported;
  // this checks the GitHub releases API and opens the download page when newer.
  private async Task CheckAsync(bool beta) {
    IsChecking = true;
    UpdateStatus = beta ? "Checking for beta updates…" : "Checking for updates…";
    try {
      var (tag, url, prerelease) = await FetchLatestAsync(beta);
      if (tag == null) {
        UpdateStatus = "No matching release found on GitHub.";
        return;
      }

      var local = LocalVersion();
      var remote = ParseVersion(tag);
      string channel = (beta || prerelease) ? "beta " : "";
      if (remote != null && local != null && remote > local) {
        UpdateStatus = $"Update available: {channel}{tag} (you have {local}). Opening download page…";
        OpenUrl(url ?? ReleasesPage);
      } else {
        UpdateStatus =
            $"You are up to date ({local?.ToString() ?? "unknown"}). Latest {channel}release: {tag}.";
      }
    } catch (Exception ex) {
      UpdateStatus = "Update check failed: " + ex.Message;
    } finally {
      IsChecking = false;
    }
  }

  private static async Task<(string? tag, string? url, bool prerelease)> FetchLatestAsync(bool beta) {
    using var resp = await _http.GetAsync(ReleasesApi);
    resp.EnsureSuccessStatusCode();
    await using var stream = await resp.Content.ReadAsStreamAsync();
    using var doc = await JsonDocument.ParseAsync(stream);

    // GitHub returns releases newest-first; stable channel skips prereleases.
    foreach (var rel in doc.RootElement.EnumerateArray()) {
      if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) {
        continue;
      }

      bool pre = rel.TryGetProperty("prerelease", out var p) && p.GetBoolean();
      if (!beta && pre) {
        continue;
      }

      var tag = rel.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
      var url = rel.TryGetProperty("html_url", out var h) ? h.GetString() : null;
      return (tag, url, pre);
    }

    return (null, null, false);
  }

  private static Version? LocalVersion() {
    var asm = Assembly.GetExecutingAssembly();
    var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    return ParseVersion(info) ?? asm.GetName().Version;
  }

  private static Version? ParseVersion(string? s) {
    if (string.IsNullOrWhiteSpace(s)) {
      return null;
    }

    s = s.Trim();
    if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
      s = s.Substring(1);
    }

    int i = 0;
    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) {
      i++;
    }

    s = s.Substring(0, i).Trim('.');
    return Version.TryParse(s, out var v) ? v : null;
  }

  private static HttpClient CreateClient() {
    var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    c.DefaultRequestHeaders.UserAgent.ParseAdd("MissionPlannerAvalonia");
    c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    return c;
  }

  private static void OpenUrl(string url) {
    try {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
      } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
        Process.Start("open", url);
      } else {
        Process.Start("xdg-open", url);
      }
    } catch {
      // best-effort; no browser available
    }
  }
}
