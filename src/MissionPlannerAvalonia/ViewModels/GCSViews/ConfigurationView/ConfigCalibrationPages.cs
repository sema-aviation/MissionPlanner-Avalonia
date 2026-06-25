using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigAccelCalibrationViewModel : ActionPageViewModel {
  public ConfigAccelCalibrationViewModel() {
    Title = "Accelerometer Calibration";
    Instructions =
        "Calibrate Accel: place the vehicle in each orientation as prompted (level, left, right, "
        + "nose down, nose up, back). Simple Accel: keep the vehicle level and press once.";
    Action(
        "Calibrate Accel",
        () => Send(MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 1, 0, 0, "accel")
    );
    Action(
        "Calibrate Level",
        () => Send(MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 2, 0, 0, "level")
    );
    Action(
        "Simple Accel Cal",
        () => Send(MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION, 0, 0, 0, 0, 4, 0, 0, "simple accel")
    );
  }

  private async void Send(
      MAVLink.MAV_CMD cmd,
      float p1,
      float p2,
      float p3,
      float p4,
      float p5,
      float p6,
      float p7,
      string what
  ) {
    if (!RequireConnection()) {
      return;
    }

    AppendLog($"Starting {what} calibration…");
    try {
      bool ok = await Task.Run(() =>
          comPort.doCommand(comPort.MAV.sysid, comPort.MAV.compid, cmd, p1, p2, p3, p4, p5, p6, p7)
      );
      AppendLog(ok ? $"{what}: accepted." : $"{what}: rejected by vehicle.");
    } catch (Exception ex) {
      AppendLog($"{what}: {ex.Message}");
    }
  }
}

public class ConfigCompassViewModel : ActionPageViewModel {
  public ConfigCompassViewModel() {
    Title = "Compass";
    Instructions =
        "Onboard magnetometer calibration. Press Start, then rotate the vehicle through all axes "
        + "until progress completes, then Accept. Cancel aborts.";
    Action("Start", () => Send(MAVLink.MAV_CMD.DO_START_MAG_CAL, 0, 1, 1, 0, 0, 0, 0, "start mag cal"));
    Action(
        "Accept",
        () => Send(MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, 0, 0, 1, 0, 0, 0, 0, "accept mag cal")
    );
    Action(
        "Cancel",
        () => Send(MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, 0, 0, 1, 0, 0, 0, 0, "cancel mag cal")
    );
  }

  private async void Send(
      MAVLink.MAV_CMD cmd,
      float p1,
      float p2,
      float p3,
      float p4,
      float p5,
      float p6,
      float p7,
      string what
  ) {
    if (!RequireConnection()) {
      return;
    }

    AppendLog($"{what}…");
    try {
      bool ok = await Task.Run(() =>
          comPort.doCommand(comPort.MAV.sysid, comPort.MAV.compid, cmd, p1, p2, p3, p4, p5, p6, p7)
      );
      AppendLog(ok ? $"{what}: accepted." : $"{what}: rejected.");
    } catch (Exception ex) {
      AppendLog($"{what}: {ex.Message}");
    }
  }
}

public class ConfigESCCalibrationViewModel : ActionPageViewModel {
  public ConfigESCCalibrationViewModel() {
    Title = "ESC Calibration";
    Instructions =
        "Sets ESC_CALIBRATION=3. After pressing, disconnect, then power the vehicle with the throttle "
        + "high to enter the ESC calibration sequence (follow your ESC's beep procedure). "
        + "DANGER: remove all propellers first.";
    Action("Calibrate ESCs", CalibrateEsc);
  }

  private async void CalibrateEsc() {
    if (!RequireConnection()) {
      return;
    }

    AppendLog("Setting ESC_CALIBRATION = 3…");
    try {
      bool ok = await Task.Run(() =>
          comPort.setParam(comPort.MAV.sysid, comPort.MAV.compid, "ESC_CALIBRATION", 3)
      );
      AppendLog(
          ok
              ? "Done. Now disconnect, then power-cycle with throttle HIGH to run the ESC sequence."
              : "Failed to set ESC_CALIBRATION."
      );
    } catch (Exception ex) {
      AppendLog(ex.Message);
    }
  }
}

public partial class ConfigMotorTestViewModel : ActionPageViewModel {
  public ConfigMotorTestViewModel() {
    Title = "Motor Test";
    Instructions =
        "DANGER: REMOVE ALL PROPELLERS. Spins one motor at the set throttle % for the set duration "
        + "to verify motor order and direction.";
    for (int m = 1; m <= 8; m++) {
      int motor = m;
      Action($"Test Motor {motor}", () => Spin(motor));
    }
    Action("Test All (sequence)", () => Spin(0, all: true));
  }

  public int ThrottlePercent { get; set; } = 8;
  public int DurationSec { get; set; } = 2;

  private async void Spin(int motor, bool all = false) {
    if (!RequireConnection()) {
      return;
    }

    AppendLog(
        all
            ? "Testing all motors in sequence…"
            : $"Spinning motor {motor} at {ThrottlePercent}% for {DurationSec}s…"
    );
    try {
      int count = all ? 8 : 0;
#pragma warning disable CS0612
      bool ok = await Task.Run(() =>
          comPort.doMotorTest(
              all ? 1 : motor,
              MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PERCENT,
              ThrottlePercent,
              DurationSec,
              count
          )
      );
#pragma warning restore CS0612
      AppendLog(ok ? "Command accepted." : "Command rejected.");
    } catch (Exception ex) {
      AppendLog(ex.Message);
    }
  }
}
