using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using MissionPlanner.Utilities;
using MissionPlannerAvalonia.Services;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Port of GCSViews/ConfigurationView/ConfigAdvanced. Upstream lists 13 advanced tools, each
// opening a dedicated WinForms window. Tools whose target is a runnable backend op in the port
// are wired for real; tools whose target window is not yet ported open a clear "not yet ported"
// notice via Services/Dialogs (never a silent no-op).
public class ConfigAdvancedViewModel : ActionPageViewModel {
  public ConfigAdvancedViewModel() {
    Title = "Advanced";
    Instructions = "The following tools are for advanced configuration only — use with caution.";

    // Anon Log -> Privacy.anonymise: pure backend op (no WinForms window), wired for real.
    Action("Anon Log", () => _ = AnonLogAsync());

    // Tools now ported to standalone Avalonia windows (Phase 6) — wired to the real window.
    Action("MAVLink Inspector", () => Views.MAVLinkInspectorWindow.OpenWindow());
    Action("Mavlink Mirror", () => Views.SerialPassThroughWindow.OpenWindow());
    Action("NMEA", () => Views.SerialOutputNMEAWindow.OpenWindow());
    Action("Follow Me", () => Views.FollowMeWindow.OpenWindow());

    foreach (var (name, desc, opens) in NotPorted) {
      var n = name;
      var d = desc;
      var o = opens;
      Action(n, () => _ = NoticeAsync(n, d, o));
    }
  }

  // Upstream button -> (name, description, window it opens). These open a separate WinForms window
  // that has no Avalonia equivalent in the port yet (the remaining unported tools).
  private static readonly (string Name, string Desc, string Opens)[] NotPorted =
  {
        ("Warning Manager", "Enable custom warnings based on a set of conditions", "WarningsManager"),
        ("Proximity", "View the data from a 360 lidar", "ProximityControl"),
        ("Mavlink Signing", "Enable mavlink signing to secure communication", "AuthKeys"),
        ("Param gen", "Regenerate the param info used inside MP", "ParameterMetaDataParser"),
        ("Moving Base", "Show an extra icon on the map of your current location", "MovingBase"),
        ("FFT", "Plot an FFT from a log", "fftui"),
        ("Spectrogram", "Plot a spectrogram from a log", "SpectrogramUI"),
        ("Support Proxy", "Share connection with support engineer", "SerialSupportProxy"),
    };

  private async Task NoticeAsync(string name, string desc, string opens) {
    AppendLog($"{name}: not yet ported (upstream opens {opens}).");
    await Dialogs.Alert(
        $"{name} — not yet ported",
        $"{desc}\n\nIn Mission Planner this opens the \"{opens}\" window, which has not been " +
        "ported to the Avalonia GCS yet.");
  }

  // Mirror of ConfigAdvanced.but_anonlog_Click: confirm, pick a tlog/bin/log, pick an output,
  // then scramble lat/lng via Privacy.anonymise.
  private async Task AnonLogAsync() {
    var sp = Dialogs.Owner?.StorageProvider;
    if (sp == null) {
      AppendLog("Anon Log: no window available.");
      return;
    }

    await Dialogs.Alert("Anon Log", "This is beta, please confirm the output file.");

    var picked = await sp.OpenFilePickerAsync(new FilePickerOpenOptions {
      Title = "Select a log to anonymise",
      AllowMultiple = false,
      FileTypeFilter = new[]
        {
                new FilePickerFileType("tlog or bin/log") { Patterns = new[] { "*.tlog", "*.bin", "*.log" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
    });

    var input = picked.FirstOrDefault()?.TryGetLocalPath();
    if (input == null) {
      return;
    }

    var ext = Path.GetExtension(input).ToLowerInvariant();
    if (ext == ".bin") {
      ext = ".log";
    }

    var suggested = Path.GetFileNameWithoutExtension(input) + "-anon" + ext;
    var save = await sp.SaveFilePickerAsync(new FilePickerSaveOptions {
      Title = "Save anonymised log",
      SuggestedFileName = suggested,
      DefaultExtension = ext.TrimStart('.'),
    });

    var output = save?.TryGetLocalPath();
    if (output == null) {
      return;
    }

    AppendLog($"Anonymising {input} -> {output} …");
    try {
      await Task.Run(() => Privacy.anonymise(input, output));
      AppendLog("Anon Log: done.");
    } catch (Exception ex) {
      AppendLog("Anon Log failed: " + ex.Message);
    }
  }
}
