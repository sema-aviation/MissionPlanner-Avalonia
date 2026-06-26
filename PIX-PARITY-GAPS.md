# Pix-Images Parity Gaps (SETUP + CONFIG, plane-connected)

Source: 27 MP 1.3.83 screenshots (ArduPlane V4.6.3 / CubeOrangePlus) in `pix-images/`, audited
against upstream `external/MissionPlanner/...Designer.cs` + handlers and our current port.

**All screenshot pages already exist as VM+View with a real backend.** No net-new pages here.
Work = design fidelity + a few un-wired sub-features. Ranked below.

## P0 — wrong page / major missing feature
- [ ] **Antenna Tracker** — WRONG PAGE. We shipped a Maestro/ArduTracker serial pan-tilt tool
  (`ConfigAntennaTrackerView` + `AntennaTrackerView`). Upstream `ConfigAntennaTracker` is an
  ArduTracker-firmware MAVLink **param/PID page**: AHRS_ORIENTATION / SERVO_YAW_TYPE /
  SERVO_PITCH_TYPE / ALT_SOURCE combos; RC1_*/RC2_* MIN/MAX/TRIM/REV + Test Yaw/Test Pitch
  (DO_SET_SERVO) + live ch1/2out; YAW_RANGE/PITCH_MIN/MAX; YAW2SRV + PITCH2SRV PID (P/I/D/IMAX/
  SLEW_TIME); Write PIDS. Build the real page; keep Maestro tool as a separate entry.
- [x] **Radio Calibration** — DONE. Rebuilt to 16ch + RCMAP roll/pitch/throttle/yaw mapping,
  Pitch/Throttle vertical bars + Roll/Yaw horizontal, Reverse checks → RC{n}_REVERSED (hidden on
  copter), Radio 5–16 aux grid, Elevon Config group (plane: ELEVON_MIXING/REVERSE/CH1_REV/CH2_REV),
  Spektrum Bind DSM2/DSMX/DSM8 → START_RX_PAIR, full calibrate writes RC{n}_MIN/MAX/TRIM. Build green.
  Remaining P2: reverse-fill bar direction (cosmetic), Wiki link.
- [ ] **Compass** — missing the entire **Compass Priority DataGrid** (Priority/DevID/BusType/Bus/
  Address/DevType/Missing/External/Orientation/Up/Down), Use-Compass-1/2/3 dedicated checks,
  Remove Missing, Reboot, priority reorder (COMPASS_PRIO1/2/3_ID). Mag-cal + large-vehicle wired.
- [ ] **DroneCAN/UAVCAN** — backend real (`DroneCanBridge`), but UI missing: SLCAN-direct +
  multicast interface selectors, top checks (Exit-SLCAN/Log/Check-Updates), Filter + Stats
  buttons, node-detail panel, debug log grid, SW-CRC/Menu columns, CANPassThrough tunnel.

## P1 — medium (un-wired sub-feature or notable layout)
- [x] **SiK Radio** — DONE: Random AES button, Copy-required-to-remote, live Status LEDs, AND real
  firmware upload — linked upstream `Radio/Uploader.cs`+`IHex.cs` (STK500 bootloader); flow = AT mode
  → AT&UPDATE → reopen @115200 → sync + getDevice → download radio~*.ihx (stable/beta) → upload+
  verify+reboot. Covers .ihx SiK boards (HM_TRP/RFD900/a/p/u); RFD900x/ux (.bin/bootloaderX) reported
  as vendor-tool-only (aborts before erase).
- [x] **RTK/GPS Inject** — Septentrio RTCM-amount panel added (pending agent confirm). Map: see report.
- [x] **Serial Ports** — DONE: SERIALx_OPTIONS bitmask flyout, embedded SerialOptionRules presets
  (proto 1/2), max-MAVLink-ports warning. (@SYS/uarts.txt name lookup still skipped.)
- [x] **ADSB** — DONE: ADSB_OPTIONS/RF_CAPABLE/RF_SELECT now bitmask checkbox flyouts; Flight ID +
  Aircraft Registration textboxes added (Save no-op, matches upstream).
- [x] **Battery Monitor 1 & 2** — DONE: full 6-row calibration incl. current-side recompute
  (→AMP_PERVLT); "MP Alert on Low Battery" speech check (settings-backed); BattMon2 amp-param
  name fallback modern `BATT2_AMP_PERVLT`→legacy `BATT2_AMP_PERVOL`. (Image still skipped — needs asset.)
- [~] **Shell** — DEFERRED-BY-DESIGN: Stats reachable via right-click menu; our `Ports` list already
  unifies serial+net so a separate conn-type dropdown has no VM backing (would need plumbing).
  Disconnect plug-icon = cosmetic only, deferred.
- [x] **CONFIG list** — DONE: Extended Tuning → "QP Extended Tuning" when plane; MAVFtp added to
  CONFIG list. "CubeLan 8 port Switch" = upstream PLUGIN (`Plugins/example23-switch.cs`), not a core
  page → out of core-parity scope.
- [x] **SETUP tree** — realigned to screenshot order: Serial Ports + ADSB moved under Mandatory
  Hardware; Optional Hardware reordered 1:1; Frame Type copter-gated; new **Antenna Tracker** param
  page registered (Maestro tool kept as "Antenna Tracker (Maestro)").

## P0 done
- [x] **Antenna Tracker** — built real ArduTracker param/PID page (ConfigAntennaTrackerParamView*),
  AHRS/SERVO/ALT combos, RC1/RC2 min/max/trim/rev + Test Yaw/Pitch (DO_SET_SERVO) + live ch1/2out,
  YAW_RANGE/PITCH_MIN/MAX, YAW2SRV/PITCH2SRV PID, Write PIDS, ArduTracker-gated.

## Incidental fix (pre-existing, found via boot-smoke)
- [x] **Startup crash** — `Theme/MpTheme.axaml` base Button style set `ToolTip.Tip="{Binding
  $self.Content}"`, aliasing a button's live StackPanel content into its own ToolTip → Avalonia
  "control already has a visual parent" crash on hovering any control-content button. Fixed with a
  `StringOnlyConverter` so the tip binds only string content (keeps the ellipsized-text-tooltip
  intent), null for control content. Build 0 err, 37/37 tests, app boots + runs clean.

## P2 — cosmetic / over-built
- [ ] Tuning pages (Basic/Extended) use WrapPanel flow vs upstream fixed 3-column grid.
- [ ] RangeFinder/Airspeed/PX4Flow/OptFlow missing upstream reference sensor image.
- [ ] RangeFinder over-built (7 extra param rows) + instanced `RNGFND1_*` vs legacy `RNGFND_*`.
- [ ] Airspeed Enable/Use as combo vs checkbox; over-built Ratio + Ground Cal + live readout.
- [ ] Joystick "Detect" not green "Auto Detect"; Expo numeric vs slider; LoadedConfig no
  firmware suffix.
- [ ] CompassMot single Y-axis vs dual (Interference% left / Amps right), series colors.
- [ ] Titles: "ESC Calibration (AC3.3+)", "UAVCAN GPS Order"; CAN GPS column order + GPS1/GPS2
  button headers. Accel buttons horizontal vs vertical-staggered.
- [ ] FlightModes: Super-Simple wiki link; per-mode enable of Simple/SuperSimple checks.
</content>
