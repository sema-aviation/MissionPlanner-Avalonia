# Mission Planner → Avalonia — Setup/Config Port Spec

Generated from upstream `external/MissionPlanner/` (1.3.83) by source extraction. Every page below
has its exact control tree + functionality + our current Avalonia coverage status. Use this to verify
what still needs porting. Status legend: **DONE** / **PARTIAL** (stub or generic param-grid, missing the
bespoke MP UX) / **MISSING** (no Avalonia VM) / **DIVERGENT** (ours differs from MP).

## Files
- [setup-mandatory.md](setup-mandatory.md) — SETUP nav tree (full) + Mandatory Hardware pages
  (Install Firmware, Frame Type, Accel Calibration, Compass, Radio Calibration, Servo Output,
  ESC Calibration, Flight Modes, FailSafe, HW ID).
- [setup-optional.md](setup-optional.md) — Optional Hardware + Advanced (25 pages: Airspeed, RangeFinder,
  OptFlow, PX4Flow, Parachute, OSD, Compass2, BT, ESP8266, CAN, DroneCAN, Battery 1/2, Antenna Tracker,
  ADSB, Motor Test, CompassMot, Mount, GPS Order, Serial, InjectGPS, Secure(AP), UserDefined, Optional).
- [config.md](config.md) — CONFIG/TUNING nav tree + pages (Flight Modes, GeoFence, Basic/Extended Tuning,
  Standard/Advanced Params, Full Parameter List, Planner(+Adv), Arducopter/plane/rover, TradHeli, FFT,
  User Params).

## High-level coverage summary (from extraction)

### Mandatory (setup-mandatory.md)
- Near-complete: **HW ID**.
- PARTIAL (param-grid / action-button stubs, missing rich WinForms UX): Install Firmware, Accel Cal,
  Compass, Radio Calibration, Servo Output, ESC Cal, Flight Modes, FailSafe, Mandatory hub.
- MISSING: **ConfigFrameType (legacy)**, **ConfigCubeID**.

### Optional + Advanced (setup-optional.md)
- DONE: PX4Flow, HW Bluetooth, ESP8266 (Optional = header row).
- PARTIAL (17): generic ParamPageBase/ActionPage stubs missing custom UI / live readouts / calibration math.
- MISSING: **HW OSD**, **HW CAN**, **Serial**.
- DIVERGENT: **Secure** (ours is a MAVLink key-manager, not MP's CubePilot cloud-signing flow);
  **ConfigSecureAP** absent.

### Config / Tuning (config.md)
- Nav tree built in `SoftwareConfig.cs:142-321` (there is no `Setup.cs`). No "Full Parameter Tree" page
  exists in 1.3.83 (only an unused string).
- MISSING: **ConfigRawParams (Full Parameter List)**, **ConfigArduplane**, **ConfigArdurover**,
  **TradHeli/TradHeli4**, **ConfigPlannerAdv**.
- PARTIAL: Basic/Extended Tuning, Friendly Params, Flight Modes, GeoFence, FFT, User Params, Planner —
  exist as generic param grids, lacking bespoke widgets (PID matrices, sliders, graph, live RC bars).
