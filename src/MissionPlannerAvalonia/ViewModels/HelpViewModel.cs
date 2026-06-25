using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class HelpViewModel : ViewModelBase {
  [RelayCommand]
  private void CheckForUpdates() =>
      OpenUrl("https://github.com/sema-aviation/MissionPlanner-Avalonia/releases/latest");

  [RelayCommand]
  private void CheckForBetaUpdates() =>
      OpenUrl("https://github.com/sema-aviation/MissionPlanner-Avalonia/releases");

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
