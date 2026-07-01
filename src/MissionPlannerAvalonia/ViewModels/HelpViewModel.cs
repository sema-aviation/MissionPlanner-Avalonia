using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class HelpViewModel : ViewModelBase {
  // Running version + git hash, e.g. "Version 2026.6.2 (f7427a5)".
  public string AppVersionDisplay => "Version " + Services.AppVersion.Full;

  [ObservableProperty]
  private string _updateStatus = "";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
  private bool _isChecking;

  [RelayCommand(CanExecute = nameof(CanCheck))]
  private async Task CheckForUpdates() {
    IsChecking = true;
    UpdateStatus = "Checking for updates…";
    try {
      await Services.Updater.CheckNowAsync();
      UpdateStatus = "";
    } catch (Exception ex) {
      UpdateStatus = "Update check failed: " + ex.Message;
    } finally {
      IsChecking = false;
    }
  }

  private bool CanCheck() => !IsChecking;
}
