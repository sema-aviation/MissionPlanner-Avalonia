using System;
using System.Threading.Tasks;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigCompassMotViewModel : ActionPageViewModel {
  public ConfigCompassMotViewModel() {
    Title = "Compass/Motor Calibration";
    Instructions =
        "Measures interference on the compass from the motors. REMOVE ALL PROPELLERS, secure the "
        + "vehicle, then press Start and slowly raise the throttle to maximum over a few seconds. "
        + "Press Finish to stop and store the result. Requires AC 3.2+.";
    Action("Start", Start);
    Action("Finish", Finish);
  }

  private async void Start() {
    if (!RequireConnection()) {
      return;
    }

    AppendLog("Starting CompassMot — raise throttle slowly to maximum…");
    try {
      bool ok = await Task.Run(() =>
          comPort.doCommand(
              comPort.MAV.sysid,
              comPort.MAV.compid,
              MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
              0,
              0,
              0,
              0,
              0,
              1,
              0
          )
      );
      AppendLog(ok ? "CompassMot started." : "CompassMot requires AC 3.2+.");
    } catch (Exception ex) {
      AppendLog(ex.Message);
    }
  }

  private async void Finish() {
    if (!RequireConnection()) {
      return;
    }

    AppendLog("Finishing CompassMot…");
    try {
      await Task.Run(() => comPort.SendAck());
      AppendLog("CompassMot stopped.");
    } catch (Exception ex) {
      AppendLog(ex.Message);
    }
  }
}
