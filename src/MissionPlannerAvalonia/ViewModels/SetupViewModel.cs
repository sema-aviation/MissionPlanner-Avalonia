using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;
using MissionPlannerAvalonia.ViewModels.Setup;

namespace MissionPlannerAvalonia.ViewModels;

public class SetupViewModel : BackstageViewModel {
  public SetupViewModel() {
    Add("Install Firmware", () => new InstallFirmwareViewModel());
    Add(
        "Install Firmware Legacy",
        () => new InfoPageViewModel("Install Firmware (Legacy)", "Legacy firmware list — port pending.")
    );
    Add(
        "Secure",
        () => new InfoPageViewModel("Secure", "Secure boot / public key management — port pending.")
    );

    Add(
        ">> Mandatory Hardware",
        () =>
            new InfoPageViewModel("Mandatory Hardware", "Required setup before flight. Pick a sub-page.")
    );
    Add("Frame Type", () => new ConfigFrameClassTypeViewModel(), sub: true);
    Add("Accel Calibration", () => new ConfigAccelCalibrationViewModel(), sub: true);
    Add("Compass", () => new ConfigCompassViewModel(), sub: true);
    Add("Radio Calibration", () => new ConfigRadioInputViewModel(), sub: true);
    Add("Servo Output", () => new ConfigRadioOutputViewModel(), sub: true);
    Add("ESC Calibration", () => new ConfigESCCalibrationViewModel(), sub: true);
    Add("Flight Modes", () => new ConfigFlightModesViewModel(), sub: true);
    Add("FailSafe", () => new ConfigFailSafeViewModel(), sub: true);
    Add(
        "HW ID",
        () => new InfoPageViewModel("HW ID", "Board/sensor hardware IDs — port pending."),
        sub: true
    );

    Add(
        ">> Optional Hardware",
        () => new InfoPageViewModel("Optional Hardware", "Optional peripherals. Pick a sub-page.")
    );
    Add(
        "RTK/GPS Inject",
        () => new InfoPageViewModel("RTK/GPS Inject", "RTCM injection to vehicle — port pending."),
        sub: true
    );
    Add("Sik Radio", () => new SikRadioViewModel(), sub: true);
    Add("ADSB", () => new ConfigADSBViewModel(), sub: true);
    Add("CAN GPS Order", () => new ConfigGPSOrderViewModel(), sub: true);
    Add("Battery Monitor", () => new ConfigBatteryMonitoringViewModel(), sub: true);
    Add("Battery Monitor 2", () => new ConfigBatteryMonitoring2ViewModel(), sub: true);
    Add(
        "DroneCAN/UAVCAN",
        () => new InfoPageViewModel("DroneCAN / UAVCAN", "DroneCAN node management — port pending."),
        sub: true
    );
    Add(
        "Joystick",
        () => new InfoPageViewModel("Joystick", "Joystick mapping (SharpDX→Silk.NET) — deferred."),
        sub: true
    );
    Add("Compass/Motor Calib", () => new ConfigCompassMotViewModel(), sub: true);
    Add("RangeFinder", () => new ConfigRangeFinderViewModel(), sub: true);
    Add("Airspeed", () => new ConfigAirspeedViewModel(), sub: true);
    Add("PX4Flow", () => new InfoPageViewModel("PX4Flow", "PX4Flow setup — port pending."), sub: true);
    Add("OpticalFlow", () => new ConfigOptFlowViewModel(), sub: true);
    Add("Onboard OSD", () => new ConfigHWOSDViewModel(), sub: true);
    Add("Camera Gimbal", () => new ConfigMountViewModel(), sub: true);
    Add("Antenna Tracker", () => new ConfigAntennaTrackerViewModel(), sub: true);
    Add("Motor Test", () => new ConfigMotorTestViewModel(), sub: true);
    Add(
        "Bluetooth Setup",
        () => new InfoPageViewModel("Bluetooth Setup", "Onboard BT module — port pending."),
        sub: true
    );
    Add("Parachute", () => new ConfigParachuteViewModel(), sub: true);
    Add(
        "ESP8266",
        () => new InfoPageViewModel("ESP8266", "WiFi module setup — port pending."),
        sub: true
    );
    Add("FFT Setup", () => new ConfigFFTViewModel(), sub: true);

    Add(">> Advanced", () => new ConfigAdvancedViewModel());
    Add(
        "Terminal",
        () => new InfoPageViewModel("Terminal", "MAVLink/serial terminal — port pending."),
        sub: true
    );
    Add(
        "Script REPL",
        () => new InfoPageViewModel("Script REPL", "Lua/Python REPL — deferred."),
        sub: true
    );

    SelectFirst();
  }
}
