# Port status — MissionPlanner → Avalonia

_Updated 2026-06-25. Build: 0 errors. Run: `dotnet run`._

## Architecture (how the port works)

- **Backstage shell** (`ViewModels/BackstageViewModel.cs` + `Views/BackstageView.axaml`): generic
  MP-style sidebar-of-pages → content host. Both CONFIG and SETUP are populated `BackstageViewModel`s
  listing **every real MP page** (mirrors `GCSViews/SoftwareConfig.cs` + `InitialSetup.cs`).
- **Param-page engine** (`ViewModels/GCSViews/ConfigurationView/ParamField.cs` + `ParamPageBase.cs`,
  auto-rendered by `Controls/ParamFieldsView.axaml`): the Avalonia equivalent of MP's
  `mavlinkComboBox/CheckBox/NumericUpDown.setup(...)`. Reads value + metadata
  (`ParameterMetaDataRepository`) and writes via `setParam`. A page declares params (`F("NAME")`),
  the engine draws combo/check/numeric + units + live write status. Powers ~20 config pages.
- **Action-page engine** (`ViewModels/ActionPageViewModel.cs` + `Views/ActionPageView.axaml`):
  buttons that fire real `doCommand`/`doMotorTest` + a log pane. Powers the calibration pages.
- `ViewLocator` falls back to `ParamFieldsView` / `ActionPageView` for those base types, so a
  param/action page needs **only a VM** (no per-page XAML).

## DATA (FlightData) — `FlightDataViewModel`
| Tab | State |
|-----|-------|
| Quick / Actions / Messages / Status | ✅ live (10 Hz `cs` mirror; arm/mode commands real) |
| PreFlight | ✅ live prearm/EKF/GPS/battery readout |
| Servo | ✅ live ch1–8 PWM output bars |
| Telemetry Logs / DataFlash Logs | ✅ file browser (`LogBrowseView`); on-vehicle log download TODO |
| Gauges / Transponder / Scripts | ⛔ placeholder (Gauges = analog dials TODO; Scripts deferred) |

## PLAN (FlightPlanner) — `FlightPlannerViewModel`
✅ Map + right action panel (View KML / map-type / Inject / **Load File / Save File** (QGC WPL 110) /
**Read / Write / Write Fast** via real WP API / **Home Location** + Set-from-Vehicle) + WP grid
(Command, P1–P4, Lat, Lon, Alt, Delete) + WP-radius/loiter/default-alt toolbar + Add WP.
⛔ TODO: drag-edit WPs on map, Grid survey, fence/rally, grad%/angle/dist/AZ computed columns.

## CONFIG — `ConfigViewModel` (backstage)
| Page | State |
|------|-------|
| Flight Modes | ✅ FLTMODE1–6 combos |
| Standard / Advanced Params | ✅ metadata-driven friendly param list |
| GeoFence | ✅ param page |
| Onboard OSD | ✅ param page (OSD*) |
| User Params | ✅ (empty until pinned) |
| Full Parameter List | ✅ (`RawParamsViewModel`, pre-existing) |
| Planner | ✅ full settings page (units/rates/theme/aircraft icon/…) |
| Basic / Extended Tuning | ⛔ stub (PID tables TODO) |

## SETUP — `SetupViewModel` (backstage) — every MP page present
✅ **wired**: Frame Type, Accel Calibration, Compass, Radio Calibration (live RC bars + min/max write),
Servo Output, ESC Calibration, Flight Modes, FailSafe, Battery Monitor 1/2, ADSB, CAN GPS Order,
RangeFinder, Airspeed, OpticalFlow, Onboard OSD, Camera Gimbal, Motor Test, Parachute, FFT Setup,
Install Firmware (vehicle picker), SiK Radio (actions), Advanced (tool grid).
⛔ **stub** (reachable, labeled, logic pending): Install Firmware Legacy, Secure, HW ID, RTK/GPS Inject,
DroneCAN/UAVCAN, Joystick, Compass/Motor Calib, PX4Flow, Antenna Tracker, Bluetooth, ESP8266,
Terminal, Script REPL.

## Deferred subsystems (isolated, large)
On-vehicle DataFlash download, log graphing/analysis, video OSD (FFmpeg), joystick (Silk.NET),
scripting REPL, SITL launch, DroneCAN flashing, firmware uploader, antenna tracker, swarm.

## To port another page
Add a `ParamPageBase` subclass (params only) or `ActionPageViewModel` subclass (commands) — no XAML
needed. For bespoke layout, add `Views/.../XxxView.axaml` (ViewLocator maps `XxxViewModel`→`XxxView`).
Replace the matching `InfoPageViewModel(...)` entry in `ConfigViewModel`/`SetupViewModel`.
