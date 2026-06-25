using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase {
  public ConnectionViewModel Connection { get; } = new();

  public FlightDataViewModel FlightData { get; } = new();
  public FlightPlannerViewModel FlightPlanner { get; } = new();
  public SetupViewModel Setup { get; } = new();
  public ConfigViewModel Config { get; } = new();
  public SimulationViewModel Simulation { get; } = new();
  public HelpViewModel Help { get; } = new();

  [ObservableProperty]
  private ViewModelBase _currentScreen;

  [ObservableProperty]
  private string _activeTab = "DATA";

  public MainWindowViewModel() => _currentScreen = FlightData;

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
}
