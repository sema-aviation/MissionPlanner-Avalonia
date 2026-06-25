using Avalonia.Headless.XUnit;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Tests;

public class ShellSmokeTests {
  [AvaloniaFact]
  public void Shell_defaults_to_FlightData() {
    var vm = new MainWindowViewModel();
    Assert.IsType<FlightDataViewModel>(vm.CurrentScreen);
    Assert.Equal("DATA", vm.ActiveTab);
  }

  [AvaloniaFact]
  public void Navigate_switches_hosted_screen() {
    var vm = new MainWindowViewModel();

    vm.NavigateCommand.Execute("PLAN");
    Assert.IsType<FlightPlannerViewModel>(vm.CurrentScreen);

    vm.NavigateCommand.Execute("CONFIG");
    Assert.IsType<ConfigViewModel>(vm.CurrentScreen);
    Assert.Equal("CONFIG", vm.ActiveTab);
  }
}
