using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {
  public ConnectionViewModel Connection { get; } = new();

  public FlightDataViewModel FlightData { get; } = new();
  public FlightPlannerViewModel FlightPlanner { get; } = new();
  public SetupViewModel Setup { get; } = new();
  [System.Obsolete]
  public ConfigViewModel Config { get; } = new();
  public SimulationViewModel Simulation { get; } = new();
  public HelpViewModel Help { get; } = new();

  [ObservableProperty]
  private ViewModelBase _currentScreen;

  [ObservableProperty]
  private string _activeTab = "DATA";

  public MainWindowViewModel() {
    _currentScreen = FlightData;

    Simulation.RequestFlightData += () =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Navigate("DATA"));
  }

  [RelayCommand]
  private void Navigate(string target) {
    ActiveTab = target;
    CurrentScreen = target switch {
      "DATA" => FlightData,
      "PLAN" => FlightPlanner,
      "SETUP" => Setup,
      "CONFIG" => Config,
      "SIMULATION" => Simulation,
      "HELP" => Help,
      _ => CurrentScreen,
    };
  }

  [RelayCommand]
  private void OpenArduPilotSite() =>
      Services.Dialogs.OpenUrl("https://ardupilot.org/?utm_source=Menu&utm_campaign=MP");

  [RelayCommand]
  private async System.Threading.Tasks.Task GetParams() {
    if (AppState.comPort.BaseStream?.IsOpen != true) {
      return;
    }
    await System.Threading.Tasks.Task.Run(() => AppState.comPort.getParamList());
  }

  [RelayCommand]
  private async System.Threading.Tasks.Task SaveToEeprom() {
    if (AppState.comPort.BaseStream?.IsOpen != true) {
      return;
    }
    await System.Threading.Tasks.Task.Run(() =>
        AppState.comPort.doCommand(AppState.comPort.MAV.sysid, AppState.comPort.MAV.compid,
            MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1, 0, 0, 0, 0, 0, 0));
  }
}
