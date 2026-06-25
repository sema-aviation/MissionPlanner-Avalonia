using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels;

public partial class SimulationViewModel : ViewModelBase {
  [ObservableProperty]
  private string _status = "Select a firmware to simulate (SITL launch = TODO).";
}
