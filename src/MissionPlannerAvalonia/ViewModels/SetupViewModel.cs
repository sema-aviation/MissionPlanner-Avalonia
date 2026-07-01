using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;
using MissionPlannerAvalonia.ViewModels.Setup;

namespace MissionPlannerAvalonia.ViewModels;

public class SetupViewModel : BackstageViewModel {
  private static bool IsCopter() =>
      AppState.comPort.MAV.cs.firmware == MissionPlanner.ArduPilot.Firmwares.ArduCopter2;

  private static bool IsHeli() =>
      IsCopter() && AppState.comPort.MAV.param.ContainsKey("H_SWASH_TYPE");

  public SetupViewModel() : base(persistKey: "setup_lastpage") {
    Add("Install Firmware", () => new InstallFirmwareViewModel());
    Add("Install Firmware Legacy", () => new ConfigFirmwareLegacyViewModel(), badge: "DEPRECATED");
    Add("Secure", () => new ConfigSecureApViewModel());
    Add("Secure (MAVLink Keys)", () => new ConfigSecureViewModel(), requiresConnection: true);

    Add(
        ">> Mandatory Hardware",
        () =>
            new InfoPageViewModel("Mandatory Hardware", "Required setup before flight. Pick a sub-page."),
        requiresConnection: true
    );

    Add("Heli Setup (4.0+)", () => new ConfigTradHeli4ViewModel(), sub: true, requiresConnection: true,
        visibleWhen: IsHeli);

    Add("Frame Type", () => new ConfigFrameClassTypeViewModel(), sub: true, requiresConnection: true,
        visibleWhen: IsCopter);
    Add("Frame Type (Legacy)", () => new ConfigFrameTypeViewModel(), sub: true, requiresConnection: true,
        visibleWhen: IsCopter);
    Add("Accel Calibration", () => new ConfigAccelCalibrationViewModel(), sub: true, requiresConnection: true);
    Add("Compass", () => new ConfigCompassViewModel(), sub: true, requiresConnection: true);

    Add("Compass (Legacy)", () => new ConfigCompassLegacyViewModel(), sub: true, requiresConnection: true);
    Add("Radio Calibration", () => new ConfigRadioInputViewModel(), sub: true, requiresConnection: true);
    Add("Servo Output", () => new ConfigRadioOutputViewModel(), sub: true, requiresConnection: true);
    Add("Serial Ports", () => new ConfigSerialViewModel(), sub: true, requiresConnection: true);
    Add("ESC Calibration", () => new ConfigESCCalibrationViewModel(), sub: true, requiresConnection: true);
    Add("Flight Modes", () => new ConfigFlightModesViewModel(), sub: true, requiresConnection: true);
    Add("FailSafe", () => new ConfigFailSafeViewModel(), sub: true, requiresConnection: true);

    Add("Initial Parameters", () => new ConfigInitialParamsViewModel(), sub: true, requiresConnection: true);
    Add("HW ID", () => new ConfigHWIDViewModel(), sub: true, requiresConnection: true);
    Add("ADSB", () => new ConfigADSBViewModel(), sub: true, requiresConnection: true);

    Add(
        ">> Optional Hardware",
        () => new InfoPageViewModel("Optional Hardware", "Optional peripherals. Pick a sub-page.")
    );
    Add("RTK/GPS Inject", () => new ConfigGpsInjectViewModel(), sub: true);
    Add("CubeID Update", () => new ConfigCubeIDViewModel(), sub: true);
    Add("Sik Radio", () => new SikRadioViewModel(), sub: true);
    Add("CAN GPS Order", () => new ConfigGPSOrderViewModel(), sub: true, requiresConnection: true);
    Add("Battery Monitor", () => new ConfigBatteryMonitoringViewModel(), sub: true, requiresConnection: true);
    Add("Battery Monitor 2", () => new ConfigBatteryMonitoring2ViewModel(), sub: true, requiresConnection: true);
    Add("DroneCAN/UAVCAN", () => new ConfigDroneCanViewModel(), sub: true);
    Add("Joystick", () => new ConfigJoystickViewModel(), sub: true);
    Add("Compass/Motor Calib", () => new ConfigCompassMotViewModel(), sub: true, requiresConnection: true);
    Add("Range Finder", () => new ConfigRangeFinderViewModel(), sub: true, requiresConnection: true);
    Add("Airspeed", () => new ConfigAirspeedViewModel(), sub: true, requiresConnection: true);
    Add("PX4Flow", () => new ConfigPX4FlowViewModel(), sub: true);
    Add("Optical Flow", () => new ConfigOptFlowViewModel(), sub: true, requiresConnection: true);
    Add("OSD", () => new ConfigHWOSDViewModel(), sub: true, requiresConnection: true);
    Add("Camera Gimbal", () => new ConfigMountViewModel(), sub: true, requiresConnection: true);
    Add("Motor Test", () => new ConfigMotorTestViewModel(), sub: true, requiresConnection: true);
    Add("Bluetooth Setup", () => new ConfigHWBTViewModel(), sub: true);
    Add("Parachute", () => new ConfigParachuteViewModel(), sub: true, requiresConnection: true);
    Add("ESP8266 Setup", () => new ConfigHWESP8266ViewModel(), sub: true, requiresConnection: true);
    Add("Antenna Tracker", () => new ConfigAntennaTrackerParamViewModel(), sub: true, requiresConnection: true);
    Add("FFT Setup", () => new ConfigFFTViewModel(), sub: true, requiresConnection: true);

    Add("Antenna Tracker (Maestro)", () => new ConfigAntennaTrackerViewModel(), sub: true);
    Add("Antenna Tracker (Live)", () => new AntennaTrackerUIViewModel(), sub: true);
    Add("HW CAN", () => new ConfigHWCANViewModel(), sub: true, requiresConnection: true);
    Add("MAVFtp", () => new MavFTPUIViewModel(), sub: true, requiresConnection: true);

    Add(">> Advanced", () => new InfoPageViewModel("Advanced", "Advanced configuration. Pick a sub-page."));
    Add("Advanced Tools", () => new ConfigAdvancedViewModel(), sub: true);
    Add("Terminal", () => new ConfigTerminalViewModel(), sub: true);
    Add("Script REPL", () => new ConfigScriptReplViewModel(), sub: true);

    SelectFirst();
  }
}
