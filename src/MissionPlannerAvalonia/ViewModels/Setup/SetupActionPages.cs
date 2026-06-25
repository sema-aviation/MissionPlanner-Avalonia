using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.ViewModels.Setup;

public class InstallFirmwareViewModel : ActionPageViewModel {
  public InstallFirmwareViewModel() {
    Title = "Install Firmware";
    Instructions =
        "Select your vehicle to load the latest stable ArduPilot firmware. "
        + "Connect the board over USB first. (Firmware download + bootloader upload is a deferred subsystem.)";
    foreach (var v in new[] { "Plane", "Copter", "Rover", "Sub", "Heli", "AntennaTracker", "Blimp" }) {
      var name = v;
      Action(name, () => AppendLog($"Selected {name}. Firmware fetch/flash not yet wired."));
    }
  }
}

public class SikRadioViewModel : ActionPageViewModel {
  public SikRadioViewModel() {
    Title = "SiK Radio";
    Instructions =
        "Configure a SiK telemetry radio (NetID, air speed, power, ECC). "
        + "Connect the radio's USB end, then Load. (AT-command exchange not yet wired.)";
    Action("Load Settings", () => AppendLog("Load: reads local + remote AT params — port pending."));
    Action("Save Settings", () => AppendLog("Save: writes AT params + AT&W — port pending."));
    Action("Reset to Default", () => AppendLog("Reset: AT&F — port pending."));
    Action("Upload Firmware", () => AppendLog("Upload: SiK bootloader flash — port pending."));
  }
}
