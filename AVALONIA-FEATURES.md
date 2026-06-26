# Avalonia Port — Implemented & Verified Feature Checklist

---

## Implementation Log — port-completion effort (build-verified, runtime SITL-pending)

> ### Phase 2 + 3 completion — 13 features via 4 parallel builders (2026-06-26)
> Closed the remaining M-tier and L-tier tractable gaps in one parallel pass (4 builder agents on
> disjoint file sets; integrated with a single fix — stray `x:Name` on DataGrid columns). Build 0
> errors, format clean, 37/37 tests. **Runtime SITL-pending** (static-integrated).
> - **FlightPlanner:** per-command **P1–P4 header relabel** on row select (15-cmd table); **midline "+"
>   insert** between segments; **Ctrl+drag group-select + group-move**; **polygon draw tools** (Draw
>   toggle / Clear / From-Current-WPs / Area via shoelace). (`.poly` file save/load skipped.)
> - **LogBrowse:** **GoToSample sync** — plot click → nearest GPS sample → map marker, and grid-row →
>   marker (new `LivePlot.PointClicked`, VM `TimedTrack`/`NearestTrackSample`); **per-field
>   scale/offset** (`y*scale+offset` before plot, toolbar NumericUpDowns).
> - **FlightData:** map **zoom slider** (`MapView.ZoomLevel`/`SetZoomLevel`) + live **overlays**
>   (sats/hdop/dist/groundspeed/wind/cursor-coords); **tab Customize + MultiLine** (persisted to
>   Settings); **tuning graph** (CB_tuning + rolling-30s `LivePlot` over `cs` fields + field picker).
> - **Shell (4 new Window+VM pairs, wired to the MainWindow context menu):** **Link Stats** popup
>   (BytesReceived/Sent → B/s, packetcount, loss, link quality); **Connection Options** dialog (real
>   Settings); **Log Download over MAVLink** (`GetLogEntry`/`GetLog` stream → save, progress);
>   **Tlog Convert hub** (tlog → KML/GPX/Matlab via shared track writers).
> - **Known follow-ups (static-only, noted by builders):** link-stats not reconnect-aware (reopen after
>   reconnect); mouse-wheel zoom doesn't feed the slider back; group-select doesn't track add/delete
>   while held; tlog "graph" conversion not wired (KML/GPX/Matlab are); `GetLog*` are `[Obsolete]` ExtLib
>   APIs (warnings only). All pending live SITL confirm.

> ### Parity completion pass — re-audit + S-tier wins (2026-06-26)
> Ran 4 parallel ground-truth re-audits (FlightData / Log-review / FlightPlanner / Shell+Config)
> because this checklist keeps under-reporting. **Confirmed already-implemented despite `- [ ]` notes**
> (doc was stale): HUD EKF/Vibe/Prearm click-dialogs, 16-relay UI, HSI gauge, gauge double-click
> set-max, Quick-tab field picker, FlightData map context menu, F2/F3/F4/F5/F12/Ctrl+Y hotkeys, and
> the three Action partials (Resume-Mission full path, Set-Home-Alt offset, Message→send_text).
> **Shipped this pass (S-tier, build 0 errors, 33/33 tests):**
> - FlightPlanner **UTM/MGRS edit-back** — Zone/Easting/Northing/MGRS grid cells now editable and
>   convert back to Lat/Lng (`Geo.FromUtm`/`FromMgrs`, reverse-guarded against the forward recompute).
>   Round-trip unit-tested.
> - FlightPlanner context/toolbar: **Insert-at-Current-Position**, **Insert-Spline-WP**, **Jump→Start**,
>   **Spline default** checkbox (mission click-add → SPLINE_WAYPOINT), and Fence **Set Return Location**
>   (single FENCE_RETURN_POINT). Menu items gate by mission-type.
> - Noted: rally break_alt/land_dir are already editable via the generic Alt + P1–P4 columns (mission
>   protocol stores them there) — only friendly labels missing (folds into the per-command relabel).
>   Transponder NIC/NACp left `- [ ]` (needs UAVIONIX_ADSB_OUT_STATUS packet decode; niche, low value).
> - **M-tier (4 done):** FlightPlanner **on-map per-leg distance labels** (`FlightPlannerMap` midpoint
>   labels, Haversine); LogBrowse **GPS-track map panel** (Map toggle → `MapView.ShowStaticTrack`;
>   `MapView.LiveVehicle=false` + `ShowSampleMarker` ready for GoToSample); LogBrowse **derived-math
>   expression curves** (`EvalExpression` — substitute TYPE.FIELD refs → `DataTable.Compute`, single
>   message type, non-finite rows dropped; wired into preset-apply + typed Graph; **4 unit tests**);
>   **tlog→Matlab** export (`DataFlashLog.ExportMatlab` routes `.tlog` → `MatLab.tlog`). Build 0 errors,
>   37/37 tests.
> - **M-tier (2 more):** FlightPlanner **POI map markers + submenu** (Add/Delete/POI-at-Coords/Clear over
>   the existing `PoiStore`; magenta markers + name labels on a new map layer, persisted); FlightData
>   **"Actions (simple)" tab** (3 large Loiter/RTL/Auto buttons → existing Quick* commands). Build 0
>   errors, 37/37 tests.
> - **Skipped (design decision, not a gap):** shell status progress overlay — the team deliberately
>   moved connect progress to the modal ProgressReporter (`MainWindow.axaml:164` comment); re-adding a
>   persistent strip would revert that. Left for an explicit call.
> - Full plan for the remaining tractable gaps: `~/.claude/plans/complete-upstream-parity.md`.
>   Still open in M: group-select, midline-insert, P1–P4 relabel, FD map zoom/overlays, tab-customize,
>   GoToSample sync (infra ready), per-field scaler, link-stats popup, connection-options.
>   L-tier (Phase 3) untouched.

> ### Parity gap close — FlightPlanner Fence/Rally + ParamLoading + map readouts (2026-06-26)
> Closed the #1 tracked parity gap plus two cheap wins. Build 0 errors, 31/31 tests (added a
> mission-type store-swap round-trip test).
> - **FlightPlanner Mission/Fence/Rally selector — DONE.** Added a Mission Type combo
>   (`MissionType` + `MissionTypes`) that swaps one grid over three backing stores
>   (`_missionStore/_fenceStore/_rallyStore`), mirroring upstream `cmb_missiontype` + `processToScreen`
>   (no per-type row class). `ReadWaypoints`/`WriteWaypoints` now branch on `CurrentMissionType`:
>   MISSION keeps the existing getWP/setWP path; FENCE/RALLY go through `mav_mission.download/upload`
>   (handles modern protocol **and** the legacy FENCE_FETCH_POINT/RALLY_FETCH_POINT fallback).
>   Click-to-add picks the command per type (WAYPOINT / FENCE_POLYGON_VERTEX_INCLUSION / RALLY_POINT).
>   `FlightPlannerMap.SetRenderMode` draws yellow open route (Mission) / red closed polygon (Fence) /
>   lime standalone markers (Rally). Context menu gates mission-only items out of Fence/Rally.
>   **Still `- [ ]`:** set-return-location, polygon draw-mode, fence/rally file load/save, rally
>   break_alt/land_dir field editor.
> - **ConfigParamLoading wired (was orphaned).** It is a transient "params still loading" page, not a
>   nav tab; now overlaid on the Backstage content area (both Config + Setup) while connected and
>   params haven't all arrived (`BackstageViewModel.ShowParamLoading`). Its Retry switched off the
>   NRE-prone no-arg `getParamList()` to `getParamListMavftp`.
> - **FlightPlanner map distance readouts** (Dist/Home/Prev) added to the toolbar from `RecomputeGrid`
>   (mirrors lbl_distance/lbl_homedist/lbl_prevdist). Zoom trackbar intentionally skipped — native
>   scroll/pinch already zooms.
> - **Doc was stale:** shell hotkeys (F2/F3/F4/F5/F12/Ctrl+Y) and the FlightData Action partials
>   (Resume Mission full path, Set-Home-Alt → altoffsethome, Message → send_text) were already
>   implemented in code despite the older `- [ ]` notes; no work needed.

> ### Live-link fixes round 3 — upstream-diff audit + UI/params sweep (2026-06-26)
> Audited the live data path against upstream MP (3 agents) to hunt the "port omits what MainV2.SerialReader
> does" bug class. Found + fixed:
> - **giveComport gate:** reader now backs off while a foreground op (param/wp/command/calibration) owns the
>   port, so it can't steal PARAM_VALUE/ACK responses. Was making vehicle ops flaky.
> - **Link-loss handling:** reader no longer dies silently on remote drop / spins forever on USB unplug;
>   it Close()s and resets the UI to "CONNECT" (was a permanent frozen HUD behind a stale DISCONNECT button).
> - **Per-MAV heartbeat framing** (signed-link correctness).
> - **cs.messages UI-thread race** (Pump + prearm + messages dialogs) — snapshot under try/catch.
> - **ParamValue TOCTOU** + RawParams/SaveSnapshot MAV.param enumeration races — snapshot before iterating.
> - **Params refresh NRE (real cause):** no-arg getParamList() builds a WinForms progress dialog via the
>   unregistered CreateIProgressReporterDialogue static event → NRE. Switched RawParams + ParamPageBase to
>   getParamListMavftp(sysid,compid) (what Open uses). This was ALSO the ESC-calibration refresh crash.
> - **Param metadata (Units/Range/Values/Description) was always empty:** apm.pdef.xml is neither shipped nor
>   downloadable to a writable dir on macOS. Now ship external/MissionPlanner/ParameterMetaDataBackup.xml as
>   Content → the legacy ParameterMetaDataRepositoryAPM fallback loads it from the running dir. (Default
>   column still depends on firmware-reported p.default_value.)
> - **Params actions → dialogs:** Write/Commit/Save/Load/Compare/Refresh now use Dialogs.Alert; deleted the
>   status info line from RawParamsView.
> - **HUD:** GPS uses real fix type (GpsFixType from cs.gpsstatus) with high-contrast green + black halo;
>   Mode right-aligned (fixes clipped "n"); prearm text colour reflects state.
> - **Map:** GPS trail is now one polyline (was hundreds of overlapping ellipse dots); cyan heading line
>   from the vehicle marker.
> - **Servo/Relay:** vertical list, Auto-width columns (no overlap), horizontal scroll, Min/Max watermarks.
> - NOTE: a malfunctioning "read-only" investigator wrote unrequested edits to 3 calibration views during this
>   session; all reverted. Build 0 errors, 30/30 tests. Pending: live bench confirm; optional full HUD repaint.

> ### Live-link fixes round 2 — sustained telemetry + bug sweep (2026-06-26)
> - **Telemetry froze after a brief initial update — ROOT CAUSE: stream rates defaulted to 0.**
>   `UpdateCurrentSettings` re-requests data streams at `cs.rateattitude/rateposition/...`, which the
>   port never initialized (MP loads from Settings) → re-request asked for rate 0 → autopilot stopped
>   streaming. Reader now seeds rates (attitude 4Hz, etc.), requests streams up front, AND sends a 1Hz
>   GCS heartbeat (autopilot stops streaming to an inactive GCS). `ConnectionViewModel`.
> - **Battery accuracy:** `BatteryEstimator` now prefers coulomb counting (BATT_CAPACITY − used mAh when
>   a current sensor exists), else voltage curve with cell count **latched from peak voltage** (a sagging
>   pack can't shrink the count). VM tracks `_peakBattVoltage`, reads `BATT_CAPACITY`.
> - **ESC-calibration "Refresh Params" crashed the app** — `ParamPageBase.Refresh()` called
>   `getParamList()` with no try/catch; a fetch timeout faulted the async command unhandled. Now
>   guarded + not-connected check + error dialog (mirrors RawParams).
> - **Cannot delete waypoints** — grid had no SelectedItem binding / no Delete key. Added `SelectedWaypoint`
>   + a Delete `KeyBinding`; `DeleteWaypoint` now also raises `WaypointsChanged` for consistency.
> - **Prearm dialog** rebuilt: lists the actual failing `PreArm:`/`Arm:` checks (deduped, newest-first)
>   instead of one messageHigh line. HUD prearm text now reflects real state (green "Ready to Arm" /
>   red "Not Ready") via new `HudControl.PrearmOk`, instead of hardcoded red.
> - **EKF + Vibe dialogs** confirmed already implemented/wired (click HUD text) — they work once telemetry flows.
> - **Blue Fluent tab underline removed** (`MpTheme.axaml` hides `PART_SelectedPipe`).
> - **Serial dropdown** now also hides macOS internal ports (Bluetooth-Incoming-Port, debug-console).
> - Build 0 errors, battery tests 5/5. Pending live bench confirm.

> ### Live-link fixes — first real Pixhawk (CubeOrange+) bring-up (2026-06-26)
> Connected a CubeOrange+ over USB; telemetry was frozen (HUD all-zero) and param refresh failed.
> - **Keystone: no background packet reader existed.** `MAVLinkInterface` does not self-read; upstream
>   runs `MainV2.SerialReader` to drain the stream + call `cs.UpdateCurrentSettings`. Added an equivalent
>   reader task in `ConnectionViewModel` (start on connect, stop on disconnect). This unfroze the HUD
>   (roll/pitch/speed/alt/GPS/heading/nav-bearing — all already rendered, just data-starved) **and** fixed
>   "Refresh Params → fetch failed" (unread heartbeats were desyncing the stream).
> - **Serial port double-listing (macOS):** `GetPortNames()` returns both `/dev/cu.X` and `/dev/tty.X`;
>   `DedupePorts()` now keeps `cu.*`.
> - **Battery %:** diverged from MP's firmware-passthrough — new `Services/BatteryEstimator.cs` estimates
>   from resting voltage + inferred cell count (LiPo curve). 5 tests. (22.7V/6S now ~38%, was 99%.)
> - **Info bar removed:** status strip below the tabs deleted; connect progress/success shows in the
>   ProgressReporter dialog; param-refresh errors now use `Dialogs.Alert`.
> - **HUD link quality** moved from the left speed-tape to the top-right corner (MP layout).
> - Build 0 errors, tests green (now 30). Pending: live confirm telemetry animates on the bench.

> ### Phase V (static slice) — checkmark re-audit + tests (2026-06-26)
> - **Adversarial re-audit of the flight-critical `- [x]` claims** (arm/disarm, set-mode, mission read/write,
>   guided fly-to, takeoff, set-home, set-EKF-origin, accel/compass/radio/ESC calibration, motor-test,
>   failsafe params, Ed25519 secure signing, resume-mission/RTL/loiter): **zero false-positives** — every box
>   traces to a real `comPort`/`setParam`/sign backend call, none logs-only/stub.
> - **One real bug found + FIXED:** "Set Home Here" used `doCommand` (COMMAND_LONG, float32 param5/6 →
>   lat/lng truncated to ~7 sig-figs, tens of metres error). Switched to `doCommandInt` (lat/lng ×1e7 int32,
>   GLOBAL frame) for full GPS precision, matching upstream. (commit `fix(flightdata): … Set Home …`.)
> - **Tests now 25** (was 8 pre-drive): geo round-trip, graph-preset parse, log-analyzer classifier, WpRow,
>   param-field, mission-file, shell smoke, **Ed25519 secure-command sign/verify/tamper**, **NMEA-0183
>   checksum vs canonical GGA/RMC vectors**. Build 0 errors; whitespace+style format gates clean.
> - Full runtime Phase V (live SITL fly-through) still pending a SITL-capable machine.


> ### ⚠️ KNOWN REAL GAPS (surfaced by the 2026-06-26 UPSTREAM-FEATURES.md re-audit — NOT yet built)
> These are genuine missing features (not lib/hardware-blocked), tracked in backlog task #9:
> 1. ~~FlightPlanner geofence + rally EDITING + `cmb_missiontype` switch~~ — **DONE (2026-06-26).**
>    Mission/Fence/Rally selector + read/write (via `mav_mission.download/upload`, legacy fallback) +
>    map overlays + per-type click-add shipped. Remaining sub-items still `- [ ]`: set-return-location,
>    polygon draw-mode, fence/rally file load/save, rally break_alt/land_dir editor.
> 2. ~~`ConfigParamLoading` not wired~~ — **DONE (2026-06-26).** It's a transient progress page (not a
>    nav tab); now overlaid on the Backstage content area while connected + params loading.
> Full not-ported list (299 items): the `- [ ] … — (not ported)` lines in `UPSTREAM-FEATURES.md`.


> The items below were **implemented after the original audit**. They compile, are wired to real
> backend (`AppState.comPort` / `setParam` / `doCommand` / file IO), and pass `dotnet build` +
> `dotnet test`. They are **not yet runtime-confirmed against SITL / a vehicle** — that pass happens
> later on a machine with a SITL link. Treat these as "done, pending live confirm". Items that need
> libraries/subsystems not on the modern stack are listed under **Noted-blocked** and remain `- [ ]`.

### Phase 0 — Shared primitives (DONE)
- `Services/Dialogs.cs` — InputBox / Confirm / Alert / AltInputBox / OpenUrl / MessageShowAgain.
- `Services/ProgressDialogs.cs` — LoadingBox + ProgressReporter.
- `Services/Geo.cs` — UTM/MGRS conversions (GeoUtility); **unit-tested** (round-trip).
- `Controls/MavMarker.cs` — heading-aware vehicle marker (getMAVMarker stand-in), both maps.
- `MapView` / `FlightPlannerMap` — clicked-lat/lng capture, click events, CenterOn.
- `HudControl.IndicatorClicked` + EKF / Vibration / Prearm live dialogs (`FlightDataDialogs.cs`).

### Phase 1 — Core flight ops (LARGELY DONE; remainder noted)
- FlightData: Message→send_text (1:1), map context menu (Fly-To/Coords, Point-Camera, Trigger,
  Set-Home, Set-EKF-Origin, TakeOff, Jump-To-Tag), real GPS track + Clear Track + Auto-Pan + map
  overlay readouts, Resume-Mission full path, Set-Home-Alt (altoffsethome) fix, Messages tab live
  STATUSTEXT.
- FlightPlanner: editable Command/Frame/UTM/MGRS grid + Up/Down reorder (**tested**), click-to-add
  WP, mission-mode map context menu (Insert/Delete/Takeoff/Land/RTL/ROI/Loiter×3/Jump/Clear/Reverse/
  Modify-Alt), home "H" marker, WP/Loiter radius written on upload, View-KML now generates mission KML.
- **Phase-1 remainder still `- [ ]`** (tractable, not yet built): full HUD live-field rendering
  (turnrate/vibe-xyz/AOA/SSA/battery2/datetime/targetairspeed/xtrack), Tuning checkbox + LivePlot
  graph + curve picker, Gauges set-max + HSI, Quick-tab configurability, Relay 4→16 UI, Transponder
  NIC/NACp, fence/rally editing + `cmb_missiontype` switch, midline-insert + group-select markers,
  POI store + POI submenus, zoom trackbar + dist/home/prev readouts, Auto-WP circle/text, map-tool
  measure/rotate/zoom-to.

### Phase 2 — Shell + boot (DONE; rest noted)
- Global hotkeys F2/F3/F4 (nav), F5 (getParamList), F12 (connect toggle), Ctrl+Y (PREFLIGHT_STORAGE).
- Top-bar context menu: Full Screen, Readonly (`comPort.ReadOnly`).
- ArduPilot wordmark → opens ardupilot.org (`Dialogs.OpenUrl`).
- `status1` green progress overlay bound to real `MAVLinkInterface.Progress(percent,status)`.
- Global crash sink (`Program.cs` UnhandledException → app-data `crash.log`).
- SITL processes killed on app exit (`SitlLauncher.StopAll`).

### Phase 3 — Log review (LogBrowse rebuilt; hub/download tail noted)
- `Services/LogAnalyzer.cs` — real multi-category auto-analysis (Vibration / GPS / VCC / Compass /
  Motor-balance / NaN) GOOD/WARN/FAIL; replaces the old 3-metric `AutoAnalysis` stub. Classifier
  **unit-tested**.
- `Services/GraphPresets.cs` — parser for `graphs/*.xml` presets (curves + `:2` right-axis).
  **Unit-tested**. Loaded into the LogBrowse preset dropdown from the submodule `graphs/` dir.
- **LogBrowse rebuilt** (`LogBrowseView` + VM + `LivePlot`): 3-level message **tree** (double-click
  field graphs it), **dual Y-axes** (Graph Left / Graph Right), **Remove Item**, **preset dropdown +
  Apply**, **Mode/Errors/Events overlays** (vertical lines), **data grid** (Data Table toggle),
  **Export Visible CSV**, plus existing KML/GPX/Matlab/Bin. Now **routed**: FlightData "Review a Log"
  opens `LogBrowseWindow` with the file loaded.
- Relay tab widened 4 to 16 (RelayChannel 0-15).
- **Phase-3 remainder still `- [ ]`**: GPS-track map panel + GoToSample (plot/grid/map sync),
  derived-math expression evaluation (preset curves containing `()+-*/` are skipped, count reported),
  per-field scaler/offset, MavlinkLog conversion hub, LogDownload-over-MAVLink screen, tlog to matlab.

### Phase 4 — Config/Setup completion (DONE; runtime SITL-pending)
- **Partials finished to full coverage:**
  - Airspeed: added `ARSPD_RATIO` + Ground-Calibration command (PREFLIGHT_CALIBRATION press-zero, with
    upstream airborne guard) + live readout.
  - RangeFinder: added `RNGFND1_PIN/SCALING/FUNCTION/OFFSET/RMETRIC` to TYPE/MIN/MAX + live sonar readout.
  - Frame Type: bespoke `ConfigFrameClassTypeViewModel` + view — FRAME_CLASS/FRAME_TYPE combos (TYPE list
    filtered per CLASS via `Common.ValidList`), writes both params. *Frame diagram art still `- [ ]`:
    upstream bitmaps not yet imported to `Assets/` (placeholder shown).*
  - Planner: expanded ~25 → ~55 Settings-backed controls (units/lang/theme/layout/severity/HUD/map-cache/
    log-dir/telemetry-rates/track-length/speech master+8 sub-toggles/map-marker toggles/loadwps/maprotation/
    nofly/adsb/beta/password/autocommit/analytics + Re-request Params). *Blocked tail `- [ ]`: DirectShow
    video source combos, Joystick/Theme-editor/Vario dialog launchers, folder-browse buttons.*
  - Failsafe: reworked flat grid → upstream per-vehicle Radio/GCS/Battery groups selected by `cs.firmware`
    (plane THR_FS_*/FS_*; copter/rover FS_THR_*/BATT_*), 16-ch RC PWM bars retained + wiki link.
  - DroneCAN: per-node param editor (GetParameters/SetParameter/SaveConfig/ExecuteOpCode ERASE), Restart
    node, and node firmware update (DroneCAN.Update over the active file-server; `.apj` unpacked via px4
    Firmware). *Skipped only the optional internet-manifest LookForUpdate branch.*
  - Secure (MAVLink keys): **real ArduPilot SECURE_COMMAND Ed25519 signing** (BouncyCastle `Ed25519Signer`/
    `Ed25519PrivateKeyParameters`; signs `seq(LE)‖op(LE)‖data‖session_key`, wire `data‖sig`). Set/Remove now
    produce valid signatures; trusted private key loaded from PRIVATE_KEYV1/.dat/PEM. **Unit-tested**
    (verify-against-pubkey, layout, tamper-fail). Upstream finding: ConfigSecure.cs/ConfigSecureAP.cs are
    offline CubePilot signers, not the on-wire scheme — implemented the ArduPilot/MAVProxy protocol instead.
  - Onboard OSD (Config): bespoke layout editor `ConfigOSDViewModel` + view — 30×16 char screen canvas,
    draggable items + X/Y NumericUpDowns + per-item enable, wires `OSD<n>_<ITEM>_EN/_X/_Y`.
  - Onboard OSD (Setup, ConfigHWOSD): corrected to upstream 1:1 — it is the SR0/1/3 "Enable Telemetry"
    2 Hz command + note, **not** a MinimOSD panel editor (the legacy MinimOSD char editor is a separate
    serial/EEPROM control not part of ConfigHWOSD upstream; transport-gap noted).
  - Friendly Params (Standard/Advanced): search box + 250 ms debounce filter, favourite star + persisted
    ordering, opt-in bitmask checkbox-flyout editor (ParamField `IsBitmask`/`BitOptions`), out-of-range red
    highlight. ParamField/ParamFieldsView changes additive + backward-compatible.
- **New pages (were missing):** `ConfigTradHeli` (legacy heli swashplate/servo + H_SV_MAN test buttons),
  `ConfigTradHeli4` (4.0+ servo/governor tables), `ConfigCubeID` (CubeID ODID node FW update, byte-for-byte
  worker), `ConfigInitialParams` (initial-param calculator with inline current-vs-new grid + Write-to-FC),
  `ConfigParamLoading` (transient param-load progress), `MavFTPUI` (MAVFtp browser: dir tree + file grid,
  download/upload/mkdir/delete/CRC over real `MAVFtp`).
- **Container gating + nav:** `BackstageViewModel` now supports firmware/vehicle `VisibleWhen` predicates
  (heli pages gated to traditional-heli copters; plane/rover tuning gated by `cs.firmware`) + **last-page
  memory** (persisted per Config/Setup via Settings). Registry wired the new pages (Heli Setup, Initial
  Parameters, CubeID, MAVFtp).
- **Install Firmware (modern):** verified already fully wired to real `APFirmware` manifest + px4 `Uploader`
  (vehicle tiles → bootloader reboot → board detect → manifest pick → download → checksum/upload w/ progress)
  — no stub remained.
- **Script REPL:** wired to the Lua host (`LuaScriptHost`) — input runs Lua, output/errors streamed, Abort
  command; replaces the "not available" stub (IronPython→Lua substitution is the intended port story).
- **Advanced tools:** Anon-Log wired to real `Privacy.anonymise`; the other 12 buttons (MAVLink Inspector,
  NMEA, FollowMe, Proximity, Signing, Mirror, Moving-Base, log-FFT, Spectrogram, Param-gen, Warnings,
  Support-Proxy) open explicit "not yet ported" notices (targets are Phase 5/6 windows) — never silent.

### Phase 5 — Simulation + Tools + Auxiliary (DONE; runtime SITL-pending)
- **SITL (was 5/20):** draggable SRTM-altitude home "H" marker + click-to-set spawn (Mapsui drag,
  `srtm.getAltitude`); Dev/Beta/Stable/Skip channel selector (persisted `sitl_download_version`,
  per-channel Windows URLs); heading / 34-frame model list / sim-speed / extra-cmdline / wipe inputs;
  full cmdline `-O{home} -s{speed} --wipe` + UDP-5501 RC override (`SendRcInput` packs rcoverridech1..8);
  auto-switch to FlightData after connect via `SimulationViewModel.RequestFlightData` (wired in
  MainWindowViewModel). macOS still has no prebuilt binary (graceful). *Linux channel filtering best-effort
  (manifest exposes only "latest").*
- **Joystick:** 7 per-action Settings dialogs (Mode/Mount-mode/Relay/Servo/Repeat/Button-axis → Joy_*
  params via getButton/setButton), import/export `.joycfg` (ExportConfig/ImportConfig), loaded-config label.
- **SiK Radio:** typed per-firmware combos + validation (S-registers keyed off the ATI5 dump), AES key
  (`AT&E`), extra/GPIO/ENCRYPTION registers, ATI2/3/7 readouts, live RSSI graph (ScottPlot, 4 series), AT
  command terminal. *RFD900 firmware upload `- [ ]` blocked — needs the unported `Radio/RFD900.cs`+RFDLib
  orchestration (board detect / internet FW download / AT&UPDATE bootloader) + reflash hardware.*
- **Antenna Tracker (standalone):** new `AntennaTrackerUIViewModel`/View (registered "Antenna Tracker
  (Live)") — Maestro / ArduTracker / Degree backends, live vehicle+commanded Az/El, point-at-vehicle loop
  (10 Hz), manual slew, PWM range/center/reverse per axis, Find-Trim-Pan SiK sweep, `Tracker_*` persistence.
- **Grid/Survey full UI:** new `GridUIViewModel`/`GridUIWindow` over the real `Grid.CreateGrid` —
  Simple/Grid-Options/Camera tabs (alt/angle/overshoot/overlap/sidelap/leadin/cross-grid/corridor/spiral),
  camera DB (`camerasBuiltin.xml`+`cameras.xml`), live stats (area/distance/photos/strips/time), display
  toggles. Launched from the FlightPlanner Survey button (`OpenForPolygon` → `AppendSurveyGrid`). *Area uses
  equirectangular shoelace (not UTM); metric-only.*
- **GeoRef:** new `GeoRefViewModel`/`GeoRefWindow` (wired from FlightData "Geo Reference Images") — reads
  real photo EXIF DateTimeOriginal (MetadataExtractor NuGet) and matches to log CAM (by index) or GPS+ATT
  (by time-offset, ±5 s) from a `.bin`/`.log` via `DFLogBuffer`; outputs `geotagged/location.txt` +
  `location.kml` + results grid. *In-place JPEG EXIF GPS re-embed `- [ ]` blocked: MetadataExtractor is
  read-only (matches upstream's primary txt/KML outputs). tlog path not ported (dataflash-only).*
- **NoFly:** `Services/NoFlyOverlay.BuildLayer` parses `.kml`/`.kmz` polygons → Mapsui layer; wired as a
  FlightPlanner map context-menu Load/Clear NoFly Overlay (`FlightPlannerMap.SetNoFlyLayer`).
- **Updater:** `Services/Updater` (check GitHub releases / version-compare / download asset / notify) wired
  to a Help "Download Update" command. *In-place `.new` swap + Updater.exe handoff `- [ ]` is
  Windows-installer-specific — leaves the downloaded asset for an external installer.*
- **Utilities:** `Services/PoiStore` (persisted POI points) + `Services/Speech` (TTS — macOS `say`, Linux
  `spd-say`/`festival`, Windows SAPI best-effort).
- **Dependency added:** `MetadataExtractor` 2.8.1 (EXIF reading for GeoRef) — restore-verified.

### Phase 6 — Controls + ExtLibs sweep (DONE; runtime SITL-pending)
- **MAVLink Inspector:** new `MAVLinkInspectorWindow` — live sysid→compid→msgtype→fields tree with Hz/Bps
  headers, from `comPort.OnPacketReceived`(+`OnPacketSent`) via `PacketInspector`, 3 Hz redraw, pause/filter,
  handler detached on close. Wired to the Advanced "MAVLink Inspector" button (was a notice).
- **DroneCAN Inspector + standalone Params:** new `DroneCANInspectorWindow` (per-node msg stream/rates) and
  `DroneCANParamsWindow` (`OpenForNode`/OpenWindow — get/edit/write/save/erase, `.param` load/save, fav/filter)
  over a new headless `DroneCanBridge` (factored from the config page's CAN bootstrap; runs over the active
  MAVLink link, `CAN_FRAME` subscribe + `CAN_FORWARD` keep-alive, torn down on close). Inspector wired to a
  new "Bus Inspector" button on the DroneCAN setup page. *Graph-It/Subscribe omitted (ZedGraph/DroneCANSubscriber not ported).*
- **HUD full draw set:** HudControl StyledProperties 30→47 — now draws wind dir+speed arrow, AOA/SSA,
  X-Track, Battery2 (V/%/A), throttle %, target-alt + target-airspeed markers, turn-rate, and failsafe/
  safetyactive/low-voltage alert-flash; the previously inert Battery2/AOA/XTrack/Connection menu toggles now
  have real draw paths. All fed live from `cs` in Pump(). Closes the Phase-1 HUD live-field backlog.
- **AGauge / HSI / QuickView:** Gauge.cs extended toward AGauge (multi-needle + colored range arcs, existing
  single-needle callers intact); new `Controls/Hsi.cs` heading/course indicator on the Gauges tab (yaw/
  nav_bearing/wp_dist); new `Controls/QuickView.cs` big-value cells with double-click field picker (any cs
  property, persisted) replacing the Quick-tab static TextBlocks. Gauges tab double-click → set Min/Max
  (persisted) — closes Phase-1 Gauges/HSI/Quick backlog.
- **Mag-cal visualization:** new `Controls/MagCalSphere.cs` on the Compass page — live mag-sample cloud
  (orthographic 3-axis projection, not GL 3D — noted) showing coverage while turning the vehicle.
- **Serial/telemetry tools (new windows, wired to Advanced buttons that were notices):** `SerialPassThroughWindow`
  (Mavlink Mirror — bidirectional byte forward to a second serial port), `SerialOutputNMEAWindow` (emit
  GGA/RMC/VTG from cs), `FollowMeWindow` (GUIDED setpoint follow). Advanced notice table shrank from 12 to 8.
- **ExtLibs:** swept — every capability a ported feature needs is wired through the 3 referenced ExtLibs
  projects (ArduPilot/MAVLink/Comms, DroneCAN, Px4Uploader) + the modern NuGet stack (Mapsui/ScottPlot/
  MoonSharp/LibVLC/MetadataExtractor/BouncyCastle). No new upstream ExtLib needed for Phase 6.
- **Still `- [ ]` (genuinely unported targets, kept as explicit Advanced notices):** Warning Manager,
  Proximity (360 lidar), Mavlink Signing (AuthKeys), Param-gen, Moving Base, log-FFT (fftui), Spectrogram,
  Support Proxy. Inspector "Graph It" (ZedGraph). These are tool windows, not flight-path features.

### Noted-blocked (remain `- [ ]` — need unported target windows / libs / deferred subsystems)
- Shell: auto-hide slide-out, Connection Options dialog, Connection List (multi-vehicle), link-stats
  popup (`ConnectionStats`), Splash window, branding/GMap-provider/CleanupFiles boot steps,
  `loadph_serial` HW fixups, `UpdateSysIDS`; Ctrl+F/P/G/X/L/W/Z/T/J tool-window hotkeys (targets not
  ported); Script.cs IronPython (deferred — Lua host stands in).
- FlightPlanner/FlightData: SHP load, GDAL opacity, georef-image custom-map import, elevation graph,
  tile prefetch, KML overlay draw (external-load split out), GimbalVideoControl window.
- Deferred per project decision: Swarm suite, Plugins subsystem + IronPython examples.

---

Companion to `UPSTREAM-FEATURES.md`. Each upstream feature was re-verified against the actual
Avalonia source (`src/MissionPlannerAvalonia/`). **A box is checked `- [x]` ONLY when the feature is
100%-certain working**: the control exists, is bound to a real member, and its handler reaches real
backend (`AppState.comPort` / `setParam` / `doCommand` / MAVLink / real file IO / correct live data),
with design matching upstream intent.

Anything that only logs, shows "not ported", sets a label, is unbound, broken, wrong-design, a generic
param-grid where upstream needs bespoke UX, or entirely absent is left `- [ ]` with a tag:
`(missing) (stub) (logs-only) (not-bound) (broken) (wrong-design) (partial) (referenced-unused)`.

> Verification was static (code reading), not a runtime click-through. Backend reality was confirmed by
> tracing each handler to `comPort`/`setParam`/`doCommand`/file IO. Map=Mapsui, plots=ScottPlot,
> Lua=MoonSharp, video=LibVLC (NuGets, not upstream ExtLibs). Where the older `docs/portspec/config.md`
> and `button-audit.md` disagreed with current code, the code won (several were stale — see notes).

## Coverage tally (verified-working / upstream-itemized)

| Section | Verified working |
|---|---|
| Application Shell (MainV2/Program/Common/Splash) | **10 / 65** |
| FlightData (HUD + Map) | **49 / 119** |
| FlightPlanner (mission editor) | **15 / 86** |
| Help · Simulation(SITL) · Setup/Config containers | **52 / 77** |
| ConfigurationView pages | **36 / 56** (+11 partial param-grids) |
| Log Review (LogBrowse/MavlinkLog/LogAnalyzer/presets) | **8 / 87** |
| Tool Modules (Swarm/Antenna/Joystick/Radio·SiK) | **11 / 78** |
| Auxiliary (Grid/GeoRef/NoFly/Utilities/Plugins/Scripts/Updater) | **4 / 121** |
| Controls (custom controls) | **8 / 8 present** (~70+ upstream controls absent) |
| ExtLibs (backend libs wired) | **7 / 61** |

**Biggest gaps:** Swarm (0), Plugins (0), GeoRef/NoFly/Updater (0), Log analysis suite, map context
menus + drag-edit + fence/rally on FlightPlanner, HUD click/right-click + map panel on FlightData,
global hotkeys / boot sequence / window chrome on the shell.

## Partial features (built, wired to backend, but NOT yet 1:1 — finish these)

These work enough to touch real backend but diverge from upstream (reduced control set, missing
sub-UX, or simplified math). Listed by section; each is also tagged `(partial)`/`(wrong-design)`
inline in its section below.

**Shell**
- `doConnect` extras — opens stream + pulls params; omits `loadph_serial()` HW fixups, HW/SW-config refresh, `UpdateSysIDS()`.
- `doDisconnect` extras — closes port; no speech cancel, `DtrEnable=false`, background tlog sort, conn-stats teardown.
- `status1` connection overlay — static text only; no green percent progress bar / telemetry-driven fill. *(wrong-design)*
- App exit — runs, but no SITL-process kill on exit.

**FlightData**
- HUD live data — draws core set live; missing datetime, wp_dist/wpno, failsafe, linkqualitygcs, messageHigh, safetyactive, targetalt/airspeed, turnrate, vibe x/y/z, xtrack, AOA/SSA, battery2.
- Video buttons (Record/Stop/MJPEG/Camera/GStreamer/HereLink) — drive the libVLC *stream* popup, not the HUD canvas. *(wrong-design)*
- "Resume Mission" — sends MISSION_START only; no WP-reprogram / copter arm+GUIDED+TAKEOFF path.
- Gauges — 4 dials render live but Min/Max fixed; no double-click set-max, no HSI heading gauge.
- Transponder — Mode A/C/S/1090ES work; Faults static, NIC/NACp hardcoded "0".
- Relay controls — only 4 relays (upstream 16).
- "Set Home Alt" — sends DO_SET_HOME; upstream toggles `cs.altoffsethome` display offset. *(wrong-design)*
- "Message" — opens received-messages window; upstream sends `send_text`. *(wrong-design)*
- "Video Control" — DO_DIGICAM_CONTROL; upstream opens GimbalVideoControl window. *(wrong-design)*
- "Tlog > Kml" — real KML export only, no graph option / MavlinkLog form.
- "Download DataFlash Log" — lists logs (`GetLogEntry`) but no download/save UI.
- "Review a Log" — reports GPS-point count only; no LogBrowse viewer.
- Map panel — basic Mapsui pan/zoom only; no Fly-To, tooltip, MAV swap, distance overlay.

**FlightPlanner**
- "Write Fast" — bound to same `WriteWaypointsCommand`; no distinct block/no-ack fast path.
- Home Lat/Long/ASL — feed `RecomputeGrid` but draw no home marker, no map-center/home-set helper.
- WP Radius / Loiter Radius — bound but value never consumed/sent.
- Param1–Param4 — editable + written, but no per-command relabel/defaulting.
- Command column — read-only text, NOT an editable MAV_CMD dropdown. *(wrong-design)*
- Route overlay — polyline drawn, but no on-map distance labels.
- "View KML" — LOADS external .kml; upstream GENERATES mission KML. *(wrong-design)*
- "Inject Custom Map" — swaps XYZ tile URL; upstream imports georeferenced map image. *(wrong-design)*

**Simulation / Help / Containers**
- SITL map — hardcoded `DefaultHome` const; no draggable "H" marker, no SRTM alt, no spawn-set. *(partial)*
- SITL launch — real `--model/--home/-I0 --serial0 tcp` + TCP-5760 connect; no UDP-5501 RC override, no `-O{home}`/`-s{speed}`, no auto-switch to FlightData.
- Help RTF pane — hardcoded static TextBlocks, not upstream `Resources.help_text` RTF. *(wrong-design)*

**Config pages (11 reduced param-grids + variants)**
- Standard / Advanced Params — live read/write via metadata; no Find/search debounce, fav ordering, bitmask editor, out-of-range highlight.
- Onboard OSD (Config) — generic `OSD*` grid; upstream is a bespoke OSD layout editor.
- Onboard OSD (Setup, `ConfigHWOSD`) — repurposed as SR0/1/3 stream-rate grid; upstream is the MinimOSD panel editor. *(wrong-design)*
- Airspeed — Type/Enable/Use/Pin + live readout; no ARSPD_RATIO, no autocal/ground-cal.
- RangeFinder — RNGFND1 TYPE/MIN/MAX + live readout; missing pin/scaling/function/offset/ratiometric.
- OptFlow — full FLOW_* set; no calibration/sensor-test UX.
- Frame Type (3.5+) — FRAME_CLASS/TYPE/Q_* combos; no image/diagram selector.
- Planner — real settings + datastream rates, but only ~25 of upstream's ~95 controls.
- Failsafe — FS fields + 16-ch RC bars; one flat grid mixing copter+plane, not per-vehicle layout.
- DroneCAN — real node enumeration; no per-node param editor / FW-update / restart.
- Secure — get session/public keys work; Set/Remove sent unsigned (no Ed25519 signer) → autopilot rejects, write not functional.

**Logs**
- "Review a Log" / "Tlog>Kml" entry buttons — reduced (see FlightData).
- Title bar — name/size/typecount/GPS only; no vehicle/version sniff.
- Plot — single-axis ScottPlot zoom/pan; no synced dual Y axes; right-click menu is ScottPlot's, not ZedGraph's.
- "Create KML + GPX" — writes real .kml only, no .gpx.
- "Auto Analysis" — custom 3-metric string (min batt / max VibeZ / GPS<6sats); NOT the upstream test runner. *(wrong-design)*
- Message tree — replaced by two flat ComboBoxes; no 3-level checkbox tree. *(wrong-design)*

**Tools**
- Joystick — axis map + runtime action firing work; **per-action "Settings" dialogs missing** so action params never set; no expo/reverse live hooks, no import/export.
- SiK Radio — raw-serial AT load/save works; no per-firmware typed combos/validation (raw text only).

**Controls**
- HudControl — see FlightData; EKF/Vibe/Prearm are non-clickable labels (no hit-zone dialogs).
- BackstageView — nav switch works; no `>>` expand/collapse, no double-click pop-out, no Advanced gating, no IActivate/IDeactivate lifecycle.

## Worth-fixing notes (stale docs found during verification)

The older port docs disagree with current code; verifiers traced the code and the code won. Update these:
- `docs/portspec/button-audit.md:212` — claims LogBrowse "Open log is a STUB / does NOT parse". **Stale** — `LoadFileAsync` parses via `DFLogBuffer` and graphs real series. (RawParams rows 202-204 still accurate.)
- `docs/portspec/button-audit.md` FlightData section — marks gimbal sliders / Telemetry+DataFlash logs / Transponder / Lua as DEAD/STUB. **Stale** — VM now wires `TlogPlayer`/`DataFlashLog`/`LuaScriptHost`/`VideoControl`/`uAvionixADSBControl` + gimbal sliders. (Conversely the **Map** is *worse* than audited: live-marker only, no context menu/zoom/tuning-graph/overlays.)
- `docs/portspec/button-audit.md` FlightPlanner — marks `chk_grid "Grid"` DEAD. **Stale** — wired to `Map.SetGraticuleVisible`/`GridLayer`.
- `docs/portspec/button-audit.md` Help/Simulation — marks VMs DEAD/STUB. **Stale** — Help/Sim VMs rewritten; both update-checks hit GitHub, SITL launches+connects for real.
- `docs/portspec/config.md` — grades Plane/Rover tuning, PlannerAdv, UserParams, FlightModes, GeoFence, CompassMot as MISSING/PARTIAL/stub. **Stale** — built out into real working ports in later commits.

> Recommend a follow-up pass to refresh `button-audit.md` + `config.md` from current code (or regenerate
> them), so the port docs stop under-reporting what now works.

---

## Application Shell (MainV2 / Program / Common / Splash) — Avalonia status

Verified working: 10 / 65

The Avalonia shell is `MainWindow.axaml` (a `DockPanel`: top nav strip + a static "ARDUPILOT" wordmark + connection control, a thin connection-status text line, and a `ContentControl` swapping `CurrentScreen`). It is a thin re-implementation: the 6 nav tabs + the connect control work, but the entire WinForms shell scaffolding (context menu, auto-hide, full-screen, link-stats popup, global hotkeys, splash, `Program.Start` boot sequence, `Common`/`Script` helpers) has no equivalent.

### Top Navigation Bar (MainMenu ToolStripButtons, left-aligned)
- [x] DATA — `Button` MainWindow.axaml:38-48 `Command=NavigateCommand` param "DATA" → `Navigate` MainWindowViewModel.cs:25-36 sets `CurrentScreen=FlightData` (live VM, real data pump).
- [x] PLAN — MainWindow.axaml:49-59 → `CurrentScreen=FlightPlanner` (MainWindowViewModel.cs:29). Real screen.
- [x] SETUP — MainWindow.axaml:60-70 → `CurrentScreen=Setup` (:30) Backstage nav. (no upstream password-gate; minor)
- [x] CONFIG — MainWindow.axaml:71-81 → `CurrentScreen=Config` (:31) Backstage nav. (no password-gate; minor)
- [x] SIMULATION — MainWindow.axaml:82-92 → `CurrentScreen=Simulation` (:32). Nav renders the view (destination is a SITL stub, but the tab itself navigates). (always shown; no DisplayConfiguration gate)
- [x] HELP — MainWindow.axaml:93-103 → `CurrentScreen=Help` (:33). Nav renders HelpView (its buttons are dead, but navigation works). (always shown; no gate)
- [ ] (Terminal) — no top tab and no shell-level Terminal navigation; only a `ConfigTerminalView` config sub-page exists. (missing)
- [ ] DONATE — no donate/PayPal control anywhere in the shell. (missing) (legacy/orphaned upstream)
- [x] `MainMenu_ItemClicked` active-button highlight — `Classes.active="{Binding ActiveTab, Converter=Eq, ConverterParameter=...}"` MainWindow.axaml:40 etc. + `ActiveTab` set in `Navigate` (:26). Visual active state works.

### Connection Control (right-aligned)
- [x] COM port dropdown — `ComboBox` MainWindow.axaml:126-131 bound `Ports`/`SelectedPort` (ConnectionViewModel.cs:19,24); `RefreshPorts` (:41-53) populates AUTO + `SerialPort.GetPortNames()` + TCP/UDP/UDPCl/WS. Real enumeration. (no preset labels; AUTO triggers real `CommsSerialScan.Scan`)
- [x] Baud dropdown — `ComboBox` MainWindow.axaml:132 bound `Bauds`/`SelectedBaud` (ConnectionViewModel.cs:20,27); consumed in `BuildStreamAsync` default case `BaudRate=SelectedBaud` (:142). (fixed list, no per-port `<PORT>_BAUD` memory; net types don't disable it)
- [x] Connect/Disconnect button — `Button Content={Binding ConnectText}` MainWindow.axaml:134 → `ToggleConnect` ConnectionViewModel.cs:56-88: opens/closes real `AppState.comPort` (`_comPort.Open(getparams:true,...)` / `_comPort.Close()`); text toggles CONNECT/DISCONNECT. Real MAVLink. (also a separate Refresh "⟳" button :133 → `RefreshPortsCommand`, no upstream equivalent)
- [ ] `doConnect(...)` extras — `ToggleConnect` opens the stream and pulls params, but omits `loadph_serial()` hardware fixups, HWConfig/SWConfig refresh, and `UpdateSysIDS()`. (partial)
- [ ] `doDisconnect(...)` extras — `_comPort.Close()` runs, but no speech cancel, no `DtrEnable=false`, no background tlog sort, no connection-stats teardown. (partial)
- [ ] Link-stats / inspector (`ShowLinkStats` → `ConnectionStats` popup) — no `ShowConnectionStatsForm`/`ConnectionStats` equivalent anywhere. (missing)

### Right-side menus / branding / Context Menu
- [ ] ArduPilot logo button (`MenuArduPilot_Click` → opens ardupilot.org) — Avalonia has only a static, non-interactive `TextBlock Text="ARDUPILOT"` (MainWindow.axaml:107-116); no command, no URL open. (missing) (wrong-design)
- [ ] Auto-hide slide-out trigger (`menu`/`AutoHideMenu`) — no slide-out, no `menu_autohide` setting. (missing)
- [ ] Right-click context menu `CTX_mainmenu` — no `ContextMenu` on the shell at all. (missing)
- [ ] AutoHide menu item (`autoHideToolStripMenuItem`). (missing)
- [ ] Full Screen menu item (`fullScreenToolStripMenuItem`). (missing)
- [ ] Readonly menu item (`comPort.ReadOnly`). (missing)
- [ ] Connection Options (`new ConnectionOptions().Show()`). (missing)
- [ ] Connection List (multi-vehicle parallel connect from file). (missing)

### Status bar
- [ ] `status1` green progress overlay (`status1.Percent`, telemetry/param-download progress, 10 s auto-hide) — Avalonia instead has a static `TextBlock` bound to `Connection.Status` (MainWindow.axaml:139-147) showing connect text only; no percent progress bar, no telemetry-driven fill. (wrong-design) (partial)

### Global keyboard shortcuts (ProcessCmdKey / KeyPreview)
- [ ] F12 connect/disconnect toggle. (missing — no KeyBinding/hotkey anywhere in shell)
- [ ] F2 Flight Data. (missing)
- [ ] F3 Flight Planner. (missing)
- [ ] F4 Config/Tuning. (missing)
- [ ] F5 `getParamList()` + refresh. (missing)
- [ ] Ctrl+F temperature/diag tool. (missing)
- [ ] Ctrl+P PluginUI. (missing)
- [ ] Ctrl+G SerialOutputNMEA. (missing)
- [ ] Ctrl+X GMAPCache. (missing)
- [ ] Ctrl+L SpectrogramUI. (missing)
- [ ] Ctrl+W PropagationSettings. (missing)
- [ ] Ctrl+Z Camera test. (missing)
- [ ] Ctrl+T force connect. (missing)
- [ ] Ctrl+Y PREFLIGHT_STORAGE write. (missing)
- [ ] Ctrl+J DevopsUI. (missing)
- [ ] Unhandled key → `ProcessCmdKeyCallback` (per-screen). (missing)

### Boot sequence (Program.cs `Start`)
- [ ] Static ctor AppDomain handlers (AssemblyLoad/UnhandledException/TypeResolve/FirstChance logging, ThreadException sink). Avalonia `Program.cs:6-12` has none. (missing)
- [ ] `Main`→`Start`: data/log/running dir setup, Mono detect, `SetCurrentDirectory`, trace listener, log4net config, TLS/connection-limit. None present. (missing)
- [ ] CLI handling (`/update`, `/updatebeta` → `Update.DoUpdate()` then exit). `Main` ignores args beyond passing to lifetime. (missing)
- [ ] Branding load (logo.txt name, logo.png/icon.png/splashbg overrides, libSkiaSharp). None. (missing)
- [ ] Splash show (`new Splash()`, version/build text, TopMost). None. (missing)
- [ ] Theme/provider hooks (CustomMessageBox/InputBox/BackstageView/Comms providers, Tracking). App.axaml.cs:17-23 only sets `MainWindow`+DataContext; `AppState` wires `CommsBase.Settings`/`InputBoxShow` only. (partial — comms providers only) (most missing)
- [ ] GMap setup (image cache, ~30 custom map providers, Google key, GDAL, proxy). None in Program/App. (missing)
- [ ] Tracking/analytics + `CreateIProgressReporterDialogue` + VVVVZ build + `CleanupFiles()`. None. (missing)
- [ ] `Application.Run(new MainV2())` + kill SITL on exit — Avalonia runs the window via `StartWithClassicDesktopLifetime` (Program.cs:8) / `App.OnFrameworkInitializationCompleted` creates `MainWindow` (App.axaml.cs:18-20). App launches, but no SITL-process cleanup on exit. (partial)
- [ ] `handleException` central error sink (benign-error swallow, missing-DLL prompts, ClrMD stack capture, POST report). None. (missing)

### Splash screen (Splash.cs)
- [ ] 600×375 borderless TopMost splash with `splashdark`/`splashbg` background. (missing — no Splash type)
- [ ] `pictureBox1` custom-logo display. (missing)
- [ ] `TXT_version` version label. (missing)
- [ ] `label1` "by Michael Oborne" credit. (missing)
- [ ] (no progress bar; closed on MainV2 load). (missing)

### Common.cs — reusable UX helpers
- [ ] `getMAVMarker(MAVState, overlay)` map-marker builder — no shell-wired Avalonia equivalent. (missing)
- [ ] `LoadingBox(title, prompt)` modeless wait form. (missing)
- [ ] `MessageShowAgain(...)` show-again dialog + `SHOWAGAIN_<tag>` persistence. (missing)
- [ ] `CreateMessageShowAgainForm(...)`. (missing)
- [ ] `chk_CheckStateChanged` (saves show-again state). (missing)
- [ ] `OpenUrl(url)` cross-platform launcher — no shell URL launcher (the would-be logo button doesn't exist). (missing)
- [ ] Other app-wide dialog providers (CustomMessageBox/InputBox/ProgressReporterDialogue/Download wiring). (missing) — note: a separate `ConnectDialog` (Views/ConnectDialog.cs) exists for TCP/UDP/WS host prompts and works, but it is not the Common.cs provider set.

### Script.cs — scripting entry (IronPython)
- [ ] `Script(redirectOutput)` ctor (Python engine, search paths, injected globals MainV2/FlightPlanner/MAV/cs/…). No IronPython engine. A `Services/LuaScriptHost.cs` and `ConfigScriptReplViewModel` exist but the REPL only prints "Python scripting is not available in this build". (missing/stub)
- [ ] `runScript(filename)` execute .py in scope. (missing)
- [ ] Helper API (ChangeParam/GetParam/ChangeMode/WaitFor/SendRC/Sleep, mavutil compat). (missing)


---

## GCSViews / FlightData (HUD + Map flight screen) — Avalonia status

Verified working: 49 / total 119
(Prior button-audit was stale — VM now wires TlogPlayer/DataFlashLog/LuaScriptHost/VideoControl/uAvionixADSBControl/gimbal sliders that the audit marked DEAD. Map, by contrast, is far worse than audited: it is just a live-marker `MapView` with no context menu, zoom widgets, tuning graph, or overlays.)

Files audited: `Views/FlightDataView.axaml`, `ViewModels/FlightDataViewModel.cs`, `Views/FlightDataDialogs.cs`, `Controls/{HudControl,Gauge,VideoControl,MapView}.cs`, `Services/{TlogPlayer,DataFlashLog,LuaScriptHost}.cs`.

Layout: `Grid` 2*/3* columns, HUD column + Map column swappable via `HudColumn`/`MapColumn` (SwapHudMap). Live data pushed by a 100 ms `DispatcherTimer` `Pump()` reading `_comPort.MAV.cs` (FlightDataViewModel.cs:127-201). No left/right SplitContainer fidelity, but functionally equivalent split.

### HUD (`HudControl`) — overlay + events
- [ ] HUD double-click → HUD Dropout float. (missing) — no pointer/Tapped handlers in HudControl.cs.
- [ ] HUD EKF click → EKFStatus window. (missing) — EKF drawn (HudControl.cs:412) but not clickable.
- [ ] HUD vibration click → Vibration window. (missing) — Vibe drawn :415, no click.
- [ ] HUD prearm click → PrearmStatus window. (missing) — prearm drawn :419, no click.
- [ ] HUD live data (full CurrentState set). (partial) — `Render` (HudControl.cs:294) draws airspeed/groundspeed/alt/yaw-heading/roll-pitch/battery V+A+remaining/mode/armed/gps/ekf/vibe/prearm/nav_bearing live from 10 Hz Pump. Missing: datetime, wp_dist, failsafe, linkqualitygcs, messageHigh, safetyactive, targetalt/airspeed, turnrate, vibe x/y/z, wpno, xtrack, AOA/SSA, lowairspeed, battery2.

### HUD right-click context menu
- [ ] "Video" submenu. (partial/wrong-design) — present, but drives a separate `VideoPopupWindow` (libVLC) instead of HUD-background video:
  - [ ] "Record Hud to AVI" (wrong-design) — `RecordVideo` :1349 records the *stream* via libVLC `TryRecord`, not the HUD canvas.
  - [ ] "Stop Record" (partial) — `StopVideo` :1361 stops popup video.
  - [ ] "Set MJPEG source" (wrong-design) — `SetVideoSource("mjpeg")` :1325 → popup `VideoControl.Play`.
  - [ ] "Start Camera" (wrong-design) — preset "camera" → popup.
  - [ ] "Set GStreamer Source" (wrong-design) — preset "gstreamer" → popup.
  - [ ] "HereLink Video" (wrong-design) — preset "herelink" → popup.
  - [ ] "GStreamer Stop" (partial) — reuses StopVideo. (All depend on native libvlc; VideoControl.cs:104 degrades gracefully if absent.)
- [ ] "Set Aspect Ratio" (stub) — `SetAspectRatio` :1370 logs only.
- [x] "User Items" — `HudUserItems` :1375 dialog populates `_hudUserFields`, consumed in Pump :180 → `HudCustomText` → `CustomItemsText` drawn HudControl.cs:424.
- [x] "Russian Hud" — TwoWay `HudRussian`, consumed in Render :312/354 (reticle banking).
- [x] "Swap With Map" — `SwapHudMap` :1268 swaps HudColumn/MapColumn.
- [x] "Ground Color" — `SetGroundColor` :1234 color picker → `HudGroundColor`, consumed :277.
- [x] "Battery Cell Voltage" — `SetBatteryCells` :1426 dialog → `HudBatteryCells`, consumed :389.
- [ ] "Show icons / Show text" (not-bound) — `HudShowIcons` TwoWay bound but `ShowIcons` never read in Render (declared HudControl.cs:144, only in AffectsRender, no draw use).

Avalonia-extra "HUD Items" submenu (not a 1:1 upstream item): Heading/Speed/Alt/RollPitch/GPS/Battery/EKF/Vibe/Prearm toggles all consumed by Display* flags in Render (working); Connection/X-Track/Battery2/AOA toggles bound but NOT consumed by HudControl (no-op).

### Bottom tab bar
Tabs present in Avalonia (fixed `TabControl`): Quick, Actions, Messages, PreFlight, Gauges, Status, Servo/Relay, Scripts, Payload Control, Telemetry Logs, DataFlash Logs, Transponder, Aux Function (13).
Entirely absent upstream tab: **Actions (simple)**. No owner-drawn green-gradient styling, no user-customizable tab order.
- [ ] "Customize" tab-visibility dialog. (missing)
- [ ] "MultiLine" toggle. (missing)

#### Quick (`tabQuick`)
Live values render (Alt/GroundSpeed/BatCurrent/AirSpeed/VerticalSpeed/DistToHome) bound from Pump — display works, but configurability is gone:
- [ ] QuickView cell double-click → field picker. (missing) — static `TextBlock`s.
- [ ] "Set View Count". (missing)
- [ ] "Undock". (missing)

#### Actions (`tabActions`)
- [x] "Arm/ Disarm" — `ToggleArm` :777 `doARM`. (no Force-Arm fallback / STATUSTEXT reason.)
- [x] "Do Action" + combo — `DoAction` :985 → `RunAction` :996 real MAV_CMDs. (subset enum vs upstream; missing Format_SD/Scripting/System_Time/HighLatency/ADSB-Ident etc.)
- [x] "Set Mode" + combo — `SetMode` :789 `setMode`. (modes hardcoded list :87, not `getModesList(firmware)`.)
- [x] "Set WP" + combo — `SetWp` :1040 `setWPCurrent`. (WpNumbers hardcoded 0-30, not CMD_TOTAL.)
- [x] "Restart Mission" — `RestartMission` :925 setWPCurrent(0)+MISSION_START.
- [ ] "Resume Mission" (partial) — `ResumeMission` :940 sends MISSION_START only; no waypoint-reprogram / copter arm+GUIDED+TAKEOFF path.
- [x] "Set Mount" + combo — `SetMount` :911 DO_MOUNT_CONFIGURE.
- [ ] "Set Home Alt" (wrong-design) — `SetHome` :1052 sends DO_SET_HOME; upstream toggles `cs.altoffsethome` (display offset), not a home command.
- [x] "Abort Landing" — `AbortLand` :1065 DO_GO_AROUND.
- [ ] "Clear Track" (logs-only) — `ClearTrack` :980 logs "Track cleared." (MapView keeps no polyline to clear.)
- [ ] "Message" (wrong-design) — `ShowMessage` :966 opens MessagesWindow (read received); upstream sends `send_text` from an InputBox.
- [ ] "Joystick" (stub) — `Joystick` :963 logs only.
- [x] "Raw Sensor View" — `RawSensorView` :951 opens RawSensorWindow, live IMU/mag/baro from cs (FlightDataDialogs.cs:35).
- [x] "RTL" — `QuickRtl` :815 setMode RTL.
- [x] "Loiter" — `QuickLoiter` :808 setMode LOITER.
- [x] "Auto" — `QuickAuto` :801 setMode AUTO.
- [x] "Loiter Rad" — `SetLoiterRad` :891 setParam LOITER_RAD/WP_LOITER_RAD.
- [x] "Alt" — `ChangeAlt` :876 setNewWPAlt.
- [x] "Speed" — `ChangeSpeed` :862 DO_CHANGE_SPEED.

#### Actions (simple)
- [ ] Loiter/RTL/Auto large buttons. (missing) — tab does not exist.

#### Messages (`tabPagemessages`)
- [ ] Live STATUSTEXT box. (partial/wrong-design) — TextBox bound to `Messages`, which is a command-log string appended by handlers; not live `cs.messages`, no 200 ms refresh timer.

#### PreFlight (`tabPagePreFlight`)
- [x] Checklist — `PreflightChecks` (FlightDataViewModel.cs:237) auto items (GPS/sats/telem/battery/mode/alt) live from cs `RefreshPreflight` :242 + manual items + Edit dialog `EditPreflight` :262. Functional (different control than MP CheckListControl).

#### Gauges (`tabGauges`)
- [ ] Gauges + double-click set-max. (partial) — 4 `Gauge` controls (VSI/Speed/Alt/Heading) render live (Gauge.cs AffectsRender), but Min/Max fixed; no `Gspeed_DoubleClick` max-config, no HSI heading gauge.

#### Transponder (`tabTransponder`) — uAvionix ADS-B
- [x] "Connect to Transponder" — `ConnectTransponder` :1504 SET_MESSAGE_INTERVAL for UAVIONIX_ADSB_OUT_STATUS.
- [x] "STBY" — `TransponderStandby` :1517 → `uAvionixADSBControl` :1501.
- [x] "ON" — `TransponderOn` :1524.
- [x] "ALT" — `TransponderAlt` :1534.
- [x] "IDENT" — `TransponderIdent` :1541 (state bit 8).
- [x] "Squawk" NUD — `OnSquawkChanged` :1547 → SendTransponder. (Max 7777, no octal-digit clamp.)
- [x] "FlightID" textbox — bound, consumed in SendTransponder :1500 (sent with next command, not on TextChanged).
- [ ] Mode_clb / fault_clb / NIC / NACp readouts. (partial) — Mode A/C/S/1090ES checkboxes work (state bits :1495), but Faults checkboxes are static IsEnabled=False and NIC/NACp are hardcoded "0" (not telemetry-driven).

#### Status (`tabStatus`)
- [x] Live full-CurrentState dump — `Statuses` reflection over every cs property, refreshed live `RefreshStatus` :218.

#### Servo/Relay (`tabServo`)
- [x] Servo PWM controls — ServoChannel ch5-16 (12) Low/Mid/High/Toggle → `SetServoChannel` :320 DO_SET_SERVO.
- [ ] Relay controls. (partial) — only 4 relays (RelayChannel 0-3) → `SetRelay` :1103 DO_SET_RELAY; upstream has 16.

#### Aux Function (`tabAuxFunction`)
- [x] 7× Aux options — RC7-13 `ParamField("RC{n}_OPTION")` (FlightDataViewModel.cs:123), writes setParam via ParamField.

#### Scripts (`tabScripts`) — Lua (MoonSharp), not Python
- [x] "Select Lua Script" — `SelectScript` :375 real .lua file picker.
- [x] "Run" — `RunScript` :398 `LuaScriptHost.RunAsync` (MoonSharp `Script.DoString`, real). (engine is Lua, upstream is IronPython.)
- [x] "Abort" — `AbortScript` :416 `_lua.Abort` (best-effort; MoonSharp can't hard-abort).
- [x] "Edit Selected" — `EditScript` :422 reads file into in-app editor TextBox (not external Process.Start).
- [ ] "Redirect Program Output" checkbox. (missing) — `RedirectOutput` prop exists but no checkbox in axaml.
- [x] Status/Selected labels — bound `ScriptStatus`/`SelectedScript`.

#### Payload Control (`tabPayload`) — gimbal (now wired; audit was stale)
- [x] "Tilt" slider — `OnTiltChanged` :702 → NudgeMount (10 Hz throttle) → `PointMount` :726 DO_MOUNT_CONTROL.
- [x] "Roll" slider — `OnRoll2Changed` :707 → NudgeMount.
- [x] "Pan" slider — `OnPanChanged` :705 → NudgeMount.
- [x] "Reset Position" — `ResetPosition` :737 zeroes + DO_MOUNT_CONTROL(0,0,0).
- [ ] "Video Control" button. (wrong-design) — `TriggerCamera` :1147 DO_DIGICAM_CONTROL; upstream opens GimbalVideoControl window. No gimbal position readout text.

#### Telemetry Logs (`tabTLogs`) — TlogPlayer (real)
- [x] "Load Log" — `LoadTlog` :480 → `TlogPlayer.Open` parses .tlog (TlogPlayer.cs:57).
- [x] "Play/Pause" — `PlayPauseTlog` :495 playback thread feeds cs → HUD/map replay (`OnTlogPacket` :539).
- [x] Speed buttons 10x..0.1 — `SetTlogSpeed` :510 → `_tlog.Speed`.
- [ ] "Tlog > Kml or Graph". (partial) — `TlogToKml` :567 → `TlogPlayer.ExportKml` (real KML); no graph option / MavlinkLog form.
- [x] Position scrub slider — TwoWay `TlogProgress`, `OnTlogProgressChanged` :520 → `_tlog.Seek`.
- [x] Percent/filename/speed labels — bound TlogPositionText/LogStatus/PlaybackSpeedText.

#### DataFlash Logs (`tablogbrowse`) — DataFlashLog (real DFLogBuffer)
- [ ] "Download DataFlash Log Via Mavlink". (partial) — `DownloadDataflashLog` :759 calls `GetLogEntry` (lists logs) but no download/save UI (upstream LogDownloadMavLink form).
- [ ] "Review a Log". (partial) — `ReviewLog` :589 `DataFlashLog.ReadTrack` reports GPS-point count only; no LogBrowse viewer.
- [x] "Auto Analysis" — `AutoAnalysis` :661 reads BAT/VIBE/GPS fields → summary (simplified vs LogAnalyzer, but real parse).
- [x] "Create KML + gpx" — `CreateKmlGpx` :611 → DataFlashLog.ExportKml + ExportGpx (real).
- [x] "Create Matlab File" — `CreateMatlab` :647 → `MatLab.ProcessLog` (real, upstream lib).
- [x] "Convert .Bin to .Log" — `ConvertBinToLog` :629 → `BinaryLog.ConvertBin` (real, upstream lib).
- [ ] "Geo Reference Images". (stub) — `GeoReferenceImages` :684 logs only (EXIF tooling not bundled).

### Map (`MapView`) controls + overlays — almost entirely absent
MapView (MapView.cs) is a Mapsui satellite map showing the live vehicle marker (`UpdateVehicle` :44, 300 ms) and auto-centering once. Pan/zoom work via built-in Mapsui gestures, but every named upstream widget/handler is missing:
- [ ] Zoom trackbar (`TRK_zoom`). (missing)
- [ ] Zoom NUD (`Zoomlevel`). (missing)
- [ ] "Auto Pan" checkbox. (missing) — centers once only, no continuous follow toggle.
- [ ] "Tuning" checkbox + ZedGraph plot. (missing) — `Controls/LivePlot.cs` exists but is NOT referenced by FlightData.
- [ ] Tuning graph double-click curve picker. (missing)
- [ ] Map drag / Ctrl+click Fly-To / dist-to-home tooltip / swap-MAV. (partial) — only basic Mapsui pan/zoom; no Fly-To, tooltip, or MAV swap.
- [ ] Map zoom-changed widget sync. (missing)
- [ ] Overlay widgets (distanceBar, windDir, coords, hdop, sats, legend, Disable Joystick). (missing)

### Map right-click context menu — entirely missing
`<ctl:MapView/>` has no `ContextMenu`. All of the following are absent:
- [ ] Fly To Here. (missing)
- [ ] Fly To Here Alt. (missing)
- [ ] Fly To Coords. (missing)
- [ ] Add Poi. (missing)
- [ ] Poi → Delete. (missing)
- [ ] Poi → Save File. (missing)
- [ ] Poi → Load File. (missing)
- [ ] Poi → Coords. (missing)
- [ ] Point Camera Here. (missing)
- [ ] Point Camera Coords. (missing)
- [ ] Trigger Camera NOW. (missing)
- [ ] Flight Planner (embed). (missing)
- [ ] Set Home Here. (missing)
- [ ] Set EKF Origin Here. (missing)
- [ ] Set Home Here (submenu). (missing)
- [ ] TakeOff. (missing)
- [ ] Camera Overlap. (missing)
- [ ] Jump To Tag. (missing)
- [ ] Gimbal Video → Full Sized. (missing)
- [ ] Gimbal Video → Mini. (missing)
- [ ] Gimbal Video → Pop Out. (missing)
- [ ] GimbalVideoControl own menu (Mini map/Swap/Close). (missing)
- [ ] (Add Poi parent header). (missing)

### Timers / live displays
- [ ] ZedGraphTimer (tuning roll). (missing) — no tuning graph.
- [ ] Messagetabtimer (200 ms live STATUSTEXT). (partial) — Messages tab not live telemetry text.
- [ ] scriptChecker. (partial) — Lua status updates via `OnLuaOutput` :366 event, not a polling timer; rough equivalent.
- [x] Background 10 Hz live loop — `Pump` (100 ms DispatcherTimer :127) pushes cs → HUD/quick/status/preflight; MapView marker updates (300 ms). No auto-pan / map-bearing / AVI frame capture.


---

## GCSViews / FlightPlanner (mission/waypoint editor) — Avalonia status

Verified working: 15 / total 86

Avalonia sources verified: `Views/FlightPlannerView.axaml` (+ `.axaml.cs`), `ViewModels/FlightPlannerViewModel.cs`, `Controls/FlightPlannerMap.cs`. (Upstream's `MapView.cs` is not used by FlightPlanner — it embeds the custom `FlightPlannerMap` instead.)

Backend confirmed real: `MAVLinkInterface.getWPCount/getWP/setWPTotal/setWP/setWPACK` (ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs:3267+), `Utilities.Grid.CreateGrid` (real survey generator). Read/Write/Survey reach genuine APIs.

### Top toolbar

#### panel5 — Read/Write WPs
- [x] `BUT_read` "Read" — `ReadWaypointsCommand` → `ReadWaypoints` VM:67-90 real `getWPCount`/`getWP(i)` loop → `Replace(rows)`. Button axaml:93.
- [x] `BUT_write` "Write" — `WriteWaypointsCommand` → `WriteWaypoints` VM:92-123 real `setWPTotal`/`setWP`/`setWPACK`. Button axaml:94.
- [ ] `but_writewpfast` "Write Fast" (partial) — axaml:95-99 bound to the SAME `WriteWaypointsCommand`; no distinct block/no-ack fast path. Uploads, but not the upstream fast routine.

#### panel2 — File load/save
- [x] `BUT_loadwpfile` "Load File" — `OnLoadFile` .cs:90 real file picker → `LoadFileAsync` VM:158-187 parses QGC WPL 110 (skips header, tab-split, 12-col).
- [x] `BUT_saveWPFile` "Save File" — `OnSaveFile` .cs:109 picker → `SaveFileAsync` VM:125-156 writes "QGC WPL 110" tab format.
- [ ] `lbl_wpfile` filename label (missing) — no dedicated loaded-filename label; only a generic `Status` string.

#### panel3 — Map type / overlays
- [x] `comboBoxMapType` — ComboBox axaml:80-85 bound `MapTypes`/`MapType`; `OnMapTypeChanged` → `Map.SetMapType` → `ReplaceBase(BuildTileLayer)` real provider switch (FlightPlannerMap.cs:81,192). (Avalonia offers 5 providers vs upstream's larger list.)
- [x] `chk_grid` "Grid" — CheckBox axaml:78 `IsChecked=ShowGrid`; `OnVmPropertyChanged` .cs:67 → `Map.SetGraticuleVisible` adds/removes `GridLayer` (FlightPlannerMap.cs:117). (Prior audit's "DEAD" is now stale — it is wired.)
- [ ] `lnk_kml` "View KML" (wrong-design) — `OnViewKml` .cs:128 opens a file picker to LOAD an external .kml and draw its track on the map; upstream GENERATES KML of the current mission and opens it in an external viewer. Different feature.
- [ ] `BUT_InjectCustomMap` (wrong-design) — `OnInjectCustomMap` .cs:191 prompts for an XYZ tile-URL template and swaps the tile source; upstream imports a georeferenced custom map image (GMapMarkerCustom). Not the same op.
- [ ] `progressBarInjectCustomMap` (missing).
- [ ] `lbl_status` inject-status label (missing) — only a static "Status: ready" TextBlock (axaml:86), not bound.

#### panel1 — Home Location
- [ ] `label4` "Home Location" LinkLabel → set home to map center (missing) — Avalonia shows a static non-clickable TextBlock (axaml:101). (A separate "Set from Vehicle" button exists, not the upstream link.)
- [ ] `TXT_homelat` "Lat" (partial) — TextBox bound `HomeLat` (axaml:103); feeds `RecomputeGrid` (VM:340) but draws no home marker on map and has no map-center/home-set helper.
- [ ] `TXT_homelng` "Long" (partial) — bound `HomeLng`, same as above.
- [ ] `TXT_homealt` "ASL" (partial) — bound `HomeAlt`, same as above.

#### panel4 — Coords readout
- [ ] `coords1` live cursor lat/lng/alt readout (missing).

### Waypoint grid panel

#### Alt / option controls
- [x] `TXT_DefaultAlt` "Default Alt" — NumericUpDown axaml:27 bound `DefaultAlt`; applied to new WPs in `AddWaypoint` VM:195 and survey dialog.
- [ ] `TXT_WPRad` "WP Radius" (partial) — NumericUpDown axaml:23 bound `WpRadius` but value never consumed/sent (no per-WP acceptance radius applied).
- [ ] `TXT_loiterrad` "Loiter Radius" (partial) — bound `LoiterRadius` (axaml:25) but never consumed.
- [ ] `CHK_verifyheight` "Verify Height" (missing).
- [ ] `CMB_altmode` Relative/Absolute/Terrain (missing) — no alt-frame combo; Write hardcodes `GLOBAL_RELATIVE_ALT` (VM:113).
- [ ] `CHK_splinedefault` "Spline" (missing).
- [ ] `TXT_altwarn` "Alt Warn" (missing).
- [ ] `chk_usemavftp` "MAVFTP" (missing).
- [ ] `but_mincommands` collapse grid (missing).
- [x] `BUT_Add` "Add" — "Add WP" button axaml:28 → `AddWaypointCommand` → `AddWaypoint` VM:189 appends a real WP row at default alt + home pos, renumbers. (Appends at end vs upstream "add below current" — minor.)

#### Commands DataGridView columns
- [ ] `Command` column (wrong-design) — read-only text `CommandName` (axaml:41-46); NOT an editable MAV_CMD dropdown, no `ChangeColumnHeader`/P1–P4 relabel/defaults.
- [ ] `Param1`–`Param4` (partial) — P1–P4 editable text cols bound to `WpRow` (axaml:47-50) and written, but no per-command header relabel or defaulting.
- [x] `Lat`/`Lon` — editable cols (axaml:51-52); edit → `OnRowChanged` VM:291 → `RecomputeGrid` + `WaypointsChanged` redraws marker. (No UTM/MGRS conversion path, but core lat/lng edit is real.)
- [x] `Alt` — editable col (axaml:53), bound, written in Write/Save.
- [ ] `Frame` column (missing).
- [ ] `coordZone`/`coordEasting`/`coordNorthing` (missing).
- [ ] `MGRS` column (missing).
- [x] `Delete` button col — template col axaml:58-69 → `DeleteWaypointCommand(row)` VM:203 removes + renumbers.
- [ ] `Up` reorder col (missing).
- [ ] `Down` reorder col (missing).
- [x] `Grad %`/`Angle`/`Dist`/`AZ` computed cols — read-only cols axaml:54-57; filled by `RecomputeGrid` VM:302-338 (real height/distance/bearing math). The four upstream computed columns are present and populated.
- [n/a] `TagData` hidden — not applicable.

### Map left-click / drag / marker behavior
- [ ] Left-click empty map adds WP (missing) — `OnMapPointerPressed` (FlightPlannerMap.cs:208) only starts a drag if a marker is hit; no add-on-click.
- [x] Left-drag map background pans — inherited Mapsui `MapControl` panning (real).
- [x] Drag a WP marker — `HitTest` (FlightPlannerMap.cs:240) + `OnMapPointerMoved`/`Released` → `WaypointDragMoved/Committed` → `MoveWaypoint` VM:225 updates that WP's lat/lng.
- [ ] Drag green "+" midline marker → insert WP (missing).
- [ ] Drag grid handle marker (survey) (missing).
- [ ] Drag a POI marker (missing).
- [ ] Drag a rally point marker (missing).
- [ ] Ctrl + left-drag selection rectangle / group-select (missing).
- [ ] Ctrl + click marker group-add / group-drag (missing).
- [ ] Click WP marker → select grid row (missing).
- [ ] Hover WP marker → highlight + select row (missing).
- [ ] `MainMap_Paint` distance/lines overlay (partial) — route polyline IS drawn (`AddPolyline` FlightPlannerMap.cs:141) but no on-map distance labels.

### Map zoom controls
- [ ] `TRK_zoom` trackbar (missing).
- [ ] `Zoomlevel` NumericUpDown (missing).
- [ ] `label11`/`lbl_distance`/`lbl_homedist`/`lbl_prevdist` readouts (missing).
- [ ] `cmb_missiontype` Mission/Fence/Rally (missing) — only mission editing exists.
- [ ] On-map `polyicon` button (missing).
- [ ] On-map `zoomicon` button (missing).

### Map right-click context menu (`contextMenuStrip1`) — ABSENT (no map context menu in Avalonia)
- [ ] "Delete WP" (missing)
- [ ] "Insert Wp" (missing)
  - [ ] "At Current Position" (missing)
- [ ] "Insert Spline WP" (missing)
- [ ] "Loiter" → Forever / Time / Circles (missing)
- [ ] "Jump" → Start / WP # (missing)
- [ ] "RTL" (missing)
- [ ] "Land" (missing)
- [ ] "Takeoff" (missing)
- [ ] "DO_SET_ROI" (missing)
- [ ] "Clear Mission" (missing)
- [ ] "Polygon" submenu (Draw/Clear/Save/Load/From SHP/From WPs/Offset/Area) (missing)
- [ ] "Geo-Fence" submenu (Upload/Download/Set Return/Load/Save/Clear) (missing)
- [ ] "Rally Points" submenu (Set/Download/Upload/Clear/Save/Load) (missing)
- [ ] "Auto WP" submenu:
  - [ ] "Create Wp Circle" (missing)
  - [ ] "Create Spline Circle" (missing)
  - [ ] "Area" (missing)
  - [ ] "Text" (missing)
  - [ ] "Create Circle Survey" (missing)
  - [x] "Survey (Grid)" — exposed as a side-panel "Survey (Grid)" button (axaml:88) → `OnSurveyGrid` .cs:203 → `GenerateSurveyGrid` VM:235 uses real `Grid.CreateGrid` over the current WP polygon and appends grid WPs. (Relocated from context menu to a button, but functional.)
- [ ] "Map Tool" submenu (Measure/Rotate/Zoom To/Prefetch/Prefetch WP Path/KML Overlay/Elevation Graph/Reverse WPs/GDAL Opacity) (missing)
- [ ] "File Load/Save" submenu (Load WP/Load+Append/Save WP/Load KML/Load SHP) (missing — top-level Load/Save buttons cover only basic WP file)
- [ ] "POI" submenu (Add/Delete/Edit) (missing)
- [ ] "Tracker Home" (missing)
- [ ] "Modify Alt" (missing)
- [ ] "Enter UTM Coord" (missing)
- [ ] "Switch Docking" (missing)
- [ ] "Set Home Here" (missing)

### Polygon context menu (`contextMenuStripPoly`) — ABSENT
- [ ] Draw / Clear / Save / Load / From SHP / From WPs / Offset / Area / Fence Inclusion / Fence Exclusion (all missing)

### Zoom context menu (`contextMenuStripZoom`) — ABSENT
- [ ] Zoom to Vehicle (missing)
- [ ] Zoom to Mission (missing)
- [ ] Zoom to Home (missing)

### Extra Avalonia behavior (not penalised, noted)
- Live vehicle marker overlay: `FlightPlannerMap.UpdateVehicle` (cs:257) on a 300 ms timer draws the vehicle from `comPort.MAV.cs` — real, roughly mirrors upstream `timer1.Tick`.
- "Set from Vehicle" button (axaml:108) → `SetHomeFromVehicleCommand` VM:211 reads `MAV.cs` lat/lng/altasl into Home fields (real read; no upstream-exact equivalent — closest to a home helper).

### Summary
Working = the core read/write/load/save round-trip, basic map (provider switch, graticule, pan, single-WP drag), the editable WP grid with the 4 computed columns (Grad/Angle/Dist/AZ DO exist and compute), Add/Delete rows, Lat/Lon/Alt edit, and a relocated Survey (Grid). Missing/non-faithful = ALL three context menus (map/polygon/zoom) entirely, fence & rally editing, mission-type switching, command-dropdown + P1–P4 relabel, Frame/UTM/MGRS columns, Up/Down reorder, click-to-add-WP, insert/midline/group-select/POI map interactions, zoom trackbar + distance readouts, Write-Fast, coords readout, and home-set-on-map. View KML and Inject Custom Map exist but implement different behavior than upstream (wrong-design).


---

## Help · Simulation(SITL) · Setup & Config containers — Avalonia status

Verified working: 52 / 77

Scope: container/nav shells + Help + Simulation only. Individual ConfigurationView page internals are out of scope (owned by another agent); for the two backstage nav trees only the nav-switch mechanism + page-listing coverage is scored. Help/Sim VMs were rewritten since the prior `button-audit.md` (which marked them DEAD/STUB) — re-verified against current on-disk source.

Files verified:
- `src/MissionPlannerAvalonia/Views/HelpView.axaml` + `ViewModels/HelpViewModel.cs`
- `src/MissionPlannerAvalonia/Views/SimulationView.axaml` + `ViewModels/SimulationViewModel.cs` + `Services/SitlLauncher.cs`
- `src/MissionPlannerAvalonia/Views/{SetupView,ConfigView,BackstageView,InfoPageView}.axaml` + matching VMs

---

### HELP tab — 2 / 5

- [x] **Check for Updates** (`HelpView.axaml:37`) → `CheckForUpdatesCommand` → `HelpViewModel.CheckAsync(beta:false)` → real `HttpClient` GET against GitHub releases API (`HelpViewModel.cs:66`), compares assembly version, opens download page in browser. Hits network + real action. (Mechanism differs from upstream `Utilities.Update.CheckForUpdate` in-app installer — GitHub-API + browser instead — but the network update-check bar is met.)
- [x] **Check for BETA Updates** (`:38`) → `CheckForBetaUpdatesCommand` → `CheckAsync(beta:true)`, includes prereleases. Real network. (No Ctrl-held → MASTER branch like upstream `BUT_betaupdate`.)
- [ ] **`richTextBox1` RTF help pane** — (wrong-design) Avalonia uses hardcoded static `TextBlock`s (welcome blurb, doc URLs, library list, F2–F5 shortcuts), NOT the upstream `Resources.help_text` RTF resource. Content present but not the upstream changelog RTF.
- [ ] **`linkLabel1` "Change Log"** → ChangeLog.txt URL — (missing) no such link in HelpView.
- [ ] **`CHK_showconsole` "Show Console Window (restart)"** — (missing) no console-toggle checkbox; no `Settings["showconsole"]` write.

---

### SIMULATION / SITL tab — 5 / 20

Vehicle picture-buttons replaced by 4 `RadioButton`s (`IsPlane/IsRover/IsCopter/IsHeli`) + single `Start/Stop` button → `StartStopCommand` → `SimulationViewModel.StartStop` → `SitlLauncher.StartAsync(SelectedVehicle)` then `ConnectAsync` (TCP 127.0.0.1:5760). Real process launch + connect on Windows/Linux; macOS has no prebuilt binary so `StartAsync` returns false (handled gracefully).

- [x] **Plane** → `SitlLauncher.Map` → `ArduPlane` / frame `plane`; downloads + launches + waits for port + connects.
- [x] **Multirotor** → `ArduCopter` / frame `quad`; real launch+connect.
- [x] **Rover** → `ArduRover` / frame `rover`; real launch+connect.
- [x] **Helicopter** → `ArduHeli` / frame `heli`; real launch+connect.
- [x] **`CheckandGetSITLImage` download/resolve internals** → `SitlLauncher.EnsureBinaryAsync`: Windows downloads `<base>/<exe>.elf` + 10 cygwin DLLs to cache; Linux resolves x86_64/arm build via `manifest.json.gz` + chmod 0755; macOS/other → "not published" message. Real, caching, working on Win/Linux. (Partial vs upstream: no `BundledPath`, single master `…/sitl/` channel only — no Beta/Stable per-vehicle URLs.)
- [ ] **Map (`myGMAP1`) home/spawn select** — (partial) `MapView` control is shown but `DefaultHome` is a hardcoded const (`-35.363261,149.165230,584,353`); no draggable "H" marker, no `BuildHomeLocation`, no SRTM altitude. Map does not set spawn.
- [ ] **Version selector `cmb_version` (Latest Dev / Beta / Stable / Skip Download)** — (missing) no UI; always pulls master.
- [ ] **`NUM_heading` (initial yaw)** — (missing).
- [ ] **`cmb_model` 34-frame override** — (missing); frame fixed per vehicle.
- [ ] **`num_simspeed` (`-s` speedup)** — (missing); no `-s` in args.
- [ ] **`txt_cmdline` extra command line** — (missing).
- [ ] **`chk_wipe` (`--wipe`)** — (missing).
- [ ] **Per-vehicle guards (no-home / download-fail)** — (partial) download-fail is guarded + logged; no-home guard N/A (no home marker exists to validate).
- [ ] **Swarm: Copter Single-link (`but_swarmseq`/`StartSwarmChain`)** — (missing).
- [ ] **Swarm: Copter Multilink (`StartSwarmSeperate`)** — (missing).
- [ ] **Swarm: Plane Multilink** — (missing).
- [ ] **Swarm: Rover Multilink** — (missing).
- [ ] **Swarm per-instance `identity.parm` / bat-sh writing** — (missing).
- [ ] **`GetDefaultConfig` (`sim_vehicle.py` defaults → `--defaults`)** — (missing); no defaults param fetch.
- [ ] **`StartSITL` final cmd + connect + UDP 5501 RC override + auto-switch to FlightData** — (partial) launch args `--model {frame} --home {const} -I0 --serial0 tcp:0` and TCP-5760 auto-connect via `ConnectAsync` are real; but no UDP 5501 RC-override send (`rcinput()`), no `-O{home}`/`-s{speed}`, no auto-switch to FlightData screen.

---

### CONFIG container (`ConfigView` → `BackstageView`) — 10 / 13

- [x] **Nav-switch mechanism** — `BackstageView` `SelectCommand` (`BackstageViewModel.cs:48`) → `SelectedPage` → `OnSelectedPageChanged` (:38) → `CurrentContent = page.Content` (lazy factory). `ContentControl` (`BackstageView.axaml:40`) renders it. `ConfigViewModel : BackstageViewModel`, `SelectFirst()` default. Real page switching.

Upstream config nav pages listed in Avalonia tree (`ConfigViewModel.cs`):
- [x] **GeoFence** → `ConfigAC_FenceViewModel`.
- [x] **Basic Tuning** → `ConfigBasicTuningViewModel` (+ Plane/Rover variants).
- [x] **Extended Tuning** → `ConfigExtendedTuningViewModel` (advanced).
- [x] **Standard Params** → `ConfigFriendlyParamsViewModel(advanced:false)`.
- [x] **Advanced Params** → `ConfigFriendlyParamsViewModel(advanced:true)`.
- [x] **Onboard OSD** → `ConfigOSDViewModel`.
- [x] **User Params** → `ConfigUserDefinedViewModel`.
- [x] **Full Parameter List** → `RawParamsViewModel`.
- [x] **Planner** → `ConfigPlannerViewModel` (+ `Planner (Advanced)`).
- [ ] **MAVFtp** (`MavFTPUI`) — (missing) not in nav tree.
- [ ] **Loading** (`ConfigParamLoading`) — (missing) no loading placeholder page.
- [ ] **Ateryx pages** (FlightModes/Zero Sensors/Pids) — (missing) legacy, not listed.

> Note: Avalonia also adds a top "Flight Modes" entry to Config (not in upstream's Config container); `display*` gating + connection/firmware conditioning is absent (all pages always listed).

---

### SETUP container (`SetupView` → `BackstageView`) — 35 / 39

- [x] **Nav-switch mechanism** — same `BackstageView`/`SelectCommand` path; `SetupViewModel : BackstageViewModel`, `SelectFirst()`. Real switching. Group headers `>> Mandatory Hardware` / `>> Optional Hardware` land on `InfoPageViewModel` stubs (not real `ConfigMandatory`/`ConfigOptional` landing pages); `>> Advanced` → `ConfigAdvancedViewModel`.

Upstream setup nav pages listed in Avalonia tree (`SetupViewModel.cs`):
- [x] **Install Firmware** → `InstallFirmwareViewModel` (+ Legacy → `ConfigFirmwareLegacyViewModel`).
- [x] **Secure** → `ConfigSecureViewModel` (+ Sign Firmware → `ConfigSecureApViewModel`).
- [x] **Frame Type** → `ConfigFrameClassTypeViewModel` (+ Legacy → `ConfigFrameTypeViewModel`).
- [x] **Accel Calibration** → `ConfigAccelCalibrationViewModel`.
- [x] **Compass** → `ConfigCompassViewModel`.
- [x] **Radio Calibration** → `ConfigRadioInputViewModel`.
- [x] **Servo Output** → `ConfigRadioOutputViewModel`.
- [x] **Serial** → `ConfigSerialViewModel` (listed under Optional in Avalonia vs Mandatory upstream).
- [x] **ESC Calibration** → `ConfigESCCalibrationViewModel`.
- [x] **Flight Modes** → `ConfigFlightModesViewModel`.
- [x] **Failsafe** → `ConfigFailSafeViewModel`.
- [x] **HW ID** → `ConfigHWIDViewModel`.
- [x] **ADSB** → `ConfigADSBViewModel`.
- [x] **RTK/GPS Inject** → `ConfigGpsInjectViewModel`.
- [x] **Sik Radio** → `SikRadioViewModel`.
- [x] **CAN GPS Order** → `ConfigGPSOrderViewModel`.
- [x] **Battery Monitor** → `ConfigBatteryMonitoringViewModel` (+ Battery2).
- [x] **DroneCAN/UAVCAN** → `ConfigDroneCanViewModel` (+ HW CAN).
- [x] **Joystick** → `ConfigJoystickViewModel`.
- [x] **Compass/Motor Calib** → `ConfigCompassMotViewModel`.
- [x] **RangeFinder** → `ConfigRangeFinderViewModel`.
- [x] **Airspeed** → `ConfigAirspeedViewModel`.
- [x] **PX4Flow** → `ConfigPX4FlowViewModel`.
- [x] **OptFlow** → `ConfigOptFlowViewModel`.
- [x] **OSD** → `ConfigHWOSDViewModel` ("Onboard OSD").
- [x] **Gimbal/Camera** → `ConfigMountViewModel` ("Camera Gimbal").
- [x] **Antenna Tracker (page)** → `ConfigAntennaTrackerViewModel`.
- [x] **Motor Test** → `ConfigMotorTestViewModel`.
- [x] **Bluetooth** → `ConfigHWBTViewModel`.
- [x] **Parachute** → `ConfigParachuteViewModel`.
- [x] **ESP8266** → `ConfigHWESP8266ViewModel`.
- [x] **FFT Setup** → `ConfigFFTViewModel`.
- [x] **Terminal** → `ConfigTerminalViewModel`.
- [x] **Script REPL** → `ConfigScriptReplViewModel`.
- [ ] **Loading** (`ConfigParamLoading`) — (missing) no loading placeholder.
- [ ] **Initial Tune Params** (`ConfigInitialParams`) — (missing) not in nav tree.
- [ ] **CubeID Update** (`ConfigCubeID`) — (missing).
- [ ] **Antenna Tracker UI** (`Antenna.TrackerUI`, separate from the page) — (missing) only the tuning page exists.

> Notes: Avalonia nav lists every page unconditionally — no `display*`-flag / connection / firmware / vehicle gating, no last-page memory, no plugin pages. Mandatory/Optional parent headers are info-stub placeholders rather than real `ConfigMandatory`/`ConfigOptional` landing pages.


---

## GCSViews / ConfigurationView pages — Avalonia status

**Verified working: 36 / 56 functional Config pages.**
Separately: **11 pages exist as generic/reduced param-grid PARTIALs** (read live + real `setParam` but miss upstream's bespoke UX or full field coverage), 1 wrong-design, 3 stubs, 8 missing (no VM).

Engines verified (all real):
- `ParamField.cs` — single metadata-driven widget; reads `MAV.param[name].Value`, auto-writes via `comPort.setParam(name,v,true)` on change (offline → stages into in-memory list). Combo/bool/numeric + range/increment/options from `ParameterMetaDataRepository`. ✅
- `ParamPageBase.cs` — Title/Intro + `Fields` of `ParamField`; `Refresh`→`getParamList()`+reload; `F()/FByPrefix()` helpers. ✅
- `ParamFieldsView.axaml` — generic Refresh + label/combo/checkbox/NumericUpDown/units/status grid bound to `ParamPageBase`. ✅
- `ActionPageViewModel.cs` — `Actions` of `ActionItem(label,ICommand)`, `AppendLog`, `RequireConnection`. ✅
- `ConfigCalibrationPages.cs` — Accel/Compass/ESC/MotorTest action pages with real MAVLink (see below). ✅
- `TuningPageBase` (in `ConfigArduplaneViewModel.cs`) — grouped `TuningGroup`/`TuningRow` PID engine: alias-resolve (`Tuning.Resolve`, first-existing-wins), dirty tracking, Write w/ >2× confirm dialog, RefreshParams/RefreshScreen, real `setParam(...,true)`. ✅
- `BatteryMonitorPageBase`, `SetupActionPages.cs` (InstallFirmware stub + full SiK AT-radio engine). ✅
- Nav registries: `ConfigViewModel.cs` (13 CONFIG pages), `SetupViewModel.cs` (~35 SETUP pages).

> NOTE: the deep spec (`docs/portspec/config.md`) is **stale** — it grades Plane/Rover tuning, PlannerAdv, UserParams, FlightModes, GeoFence, CompassMot as MISSING/PARTIAL/stub. Re-verification in code shows these were since built out into real ports (commits after the spec). Grades below reflect current code.

---

### Param-engine pages (`ParamPageBase` / `ParamField` grids)

- [ ] **Standard Params** (`ConfigFriendlyParamsViewModel.cs`, adv=false) — filters `MAV.param` by metadata DisplayName + User=Standard, live read/write via `ParamField`. PARTIAL: no Find/search debounce, no fav ordering, no bitmask editor, no out-of-range highlight. (partial)
- [ ] **Advanced Params** (`ConfigFriendlyParamsViewModel.cs`, adv=true) — same engine, User=Advanced. Same gaps; also predicate shows only mode==Advanced (upstream also includes uncategorised/empty). (partial)
- [x] **Full Parameter List** (`RawParamsViewModel.cs`) — full grid + prefix category tree, Search(≥2)+Modified+Non-Default filters, Load/Save/Compare `.param` (`ParamFile`), Write (mxparser eval + reboot note), Commit-to-Flash (`PREFLIGHT_STORAGE`), fav persist. Real `setParam`. Matches upstream `ConfigRawParams`.
- [ ] **Onboard OSD** (`ConfigParamPages.cs` `ConfigOSDViewModel`) — generic `FByPrefix("OSD")` grid; upstream `ConfigOSD` is a bespoke OSD panel/screen layout editor. (partial)
- [x] **User Params** (`ConfigUserDefinedViewModel.cs`) — label+editor `ParamField` rows for CH/RC option params, Modify (rewrites `Settings["UserParams"]`), Refresh, real write. Matches `ConfigUserDefined`.
- [x] **GeoFence** (`ConfigAC_FenceViewModel.cs`) — labelled Enable/Type/Action/MaxAlt/MinAlt/MaxRadius/RtlAlt fields with distance-unit labels, self-binding live read/write. Matches `ConfigAC_Fence`.
- [x] **ADSB** (`ConfigADSBViewModel.cs`) — ADSB_/AVD_ filtered grid + search + Write (ENABLE last). Real `setParam`; covers upstream `ConfigADSB` params.
- [x] **Serial** (`ConfigSerialViewModel.cs`) — per-port baud/protocol combos + OPTIONS bitmask text + RTS/CTS labels, real per-row `setParam`. Matches `ConfigSerial` (skips uarts.txt MAVFtp lookup).
- [x] **CAN GPS Order** (`ConfigGPSOrderViewModel.cs`) — detects `GPS_CAN_NODEID*` nodes, Override1/2 write `GPS1/2_CAN_OVRIDE` via real `setParam`. Matches `ConfigGPSOrder`.
- [ ] **Airspeed** (`ConfigAirspeedViewModel.cs`) — Type/Enable/Use/Pin fields + live airspeed readout, real write. Reduced: no ARSPD_RATIO field, no autocal/ground-cal. (partial)
- [ ] **RangeFinder** (`ConfigRangeFinderViewModel.cs`) — only RNGFND1_TYPE/MIN/MAX + live distance/voltage + one TeraRanger preset. Reduced: missing pin/scaling/function/offset/ratiometric. (partial)
- [ ] **OptFlow** (`ConfigOptFlowViewModel.cs`) — full FLOW_* param set + legacy/new + rover height-override panel, real write. PARTIAL: no calibration/sensor-test UX. (partial)
- [ ] **Onboard OSD (Setup)** (`ConfigHWOSDViewModel.cs`) — repurposed as SR0/1/3 stream-rate grid + "Enable Telemetry" bulk-set; upstream `ConfigHWOSD` is the MinimOSD panel-layout editor. (wrong-design)
- [x] **Parachute** (`ConfigParachuteViewModel.cs`) — CHUTE_* fields + servo (RC9–14) auto-assign (FUNCTION=27, ensure-disabled others), real write. Matches `ConfigHWParachute`.
- [x] **HW ID** (`ConfigHWIDViewModel.cs`) — decodes `*_ID/*_DEVID` via `Device.DeviceStructure` into bus/type/addr/devtype table. Read-only inspector, matches `ConfigHWIDs`.
- [x] **FFT Setup** (`ConfigFFTViewModel.cs` + `ConfigFFTView.axaml.cs`) — INS_LOG_BAT_CNT slider/MASK/LOG_BITMASK fields (real write) + "FFT" button file-picks a .bin and graphs a real DFT on `LivePlot`. Divergent from upstream `fftui` window but functionally equivalent.
- [ ] **Frame Type (3.5+)** (`ConfigParamPages.cs` `ConfigFrameClassTypeViewModel`) — FRAME_CLASS/TYPE/Q_* combos, real write. PARTIAL: no image/diagram-based selector like upstream `ConfigFrameClassType`. (partial)
- [x] **Frame Type (legacy)** (`ConfigFrameTypeViewModel.cs`) — Plus/X/V/H/Y/VTail radios write `FRAME`, real `setParam`. Matches `ConfigFrameType`.
- [x] **Battery Monitor** (`ConfigBatteryMonitoringViewModel.cs` : `BatteryMonitorPageBase`) — sensor-type & HW-version presets compute volt-mult/amp-per-volt/pins, live voltage/current. Matches `ConfigBatteryMonitoring`.
- [x] **Battery Monitor 2** (`ConfigBatteryMonitoring2ViewModel.cs` : `BatteryMonitorPageBase`) — same engine on BATT2. Matches `ConfigBatteryMonitoring2`.
- [ ] **Planner** (`ConfigPlannerViewModel.cs`) — real `Settings.Instance` persistence + telemetry-rate `requestDatastream`. PARTIAL: only ~25 of upstream's ~95 controls (no OSD color, map cache, ADSB server, aircraft-icon line length, speech phrase prompts, video/joystick/vario, password, analytics, slow-machine, etc.). (partial)
- [x] **Planner (Advanced)** (`ConfigPlannerAdvViewModel.cs`) — read-only dump of all `Settings.Instance` keys, sorted. Matches `ConfigPlannerAdv`.
- [x] **Antenna Tracker** (`ConfigAntennaTrackerViewModel.cs`) — servo/range/PID param fields + live ch1/ch2 PWM readout + Test Yaw/Pitch via real `DO_SET_SERVO doCommand`. Matches `ConfigAntennaTracker`.

### Action / calibration pages (real `doCommand`/`doMotorTest`/MAVLink)

- [x] **Accel Calibration** (`ConfigCalibrationPages.cs` `ConfigAccelerometerCalibration`) — `PREFLIGHT_CALIBRATION` (full/level/simple), subscribes STATUSTEXT + COMMAND_LONG/ACCELCAL_VEHICLE_POS, sends position acks, success/fail detect. Matches `ConfigAccelerometerCalibration`.
- [x] **Compass** (`ConfigCalibrationPages.cs` `ConfigCompassViewModel`) — params + real `DO_START/ACCEPT/CANCEL_MAG_CAL`, MAG_CAL_PROGRESS/REPORT subscriptions w/ per-compass bars + offsets, large-vehicle `FIXED_MAG_CAL_YAW`, declination write. Covers `ConfigHWCompass`+`ConfigHWCompass2`.
- [x] **ESC Calibration** (`ConfigCalibrationPages.cs` `ConfigESCCalibration`) — real `setParam("ESC_CALIBRATION",3)` + power-cycle instructions. Matches `ConfigESCCalibration`.
- [x] **Motor Test** (`ConfigCalibrationPages.cs` `ConfigMotorTest`) — real `doMotorTest` per-motor / test-all-sequence / stop-all, frame-class→layout from `APMotorLayout.json`, SpinArm/SpinMin writes. Matches `ConfigMotorTest`.
- [x] **Radio Calibration** (`ConfigRadioInputViewModel.cs`) — 100 ms live ch1–8 bars, min/max capture during calibrate, writes `RCn_MIN/RCn_MAX`. Matches `ConfigRadioInput`.
- [x] **Servo Output** (`ConfigRadioOutputViewModel.cs`) — live chNout PWM (reflection) + per-servo FUNCTION/MIN/TRIM/MAX/REVERSED `ParamField`s (16/32). Matches `ConfigRadioOutput`.
- [x] **Flight Modes** (`ConfigFlightModesViewModel.cs`) — 6 PWM-band rows, mode combos (FLTMODE/MODE/COM_FLTMODE per fw), live current-mode + active-row highlight + PWM readout, copter Simple/SuperSimple bitmask grid, Save via real `setParam`. Matches `ConfigFlightModes`.
- [ ] **Failsafe** (`ConfigFailSafeViewModel.cs`) — FS param fields + 16-ch live RC bars + ch3 throttle-low recolor, real write. PARTIAL: one flat grid mixing copter+plane FS params, not upstream's per-vehicle bespoke layout/conditional fields. (partial)
- [x] **Compass/Motor Calib** (`ConfigCompassMotViewModel.cs`) — real `PREFLIGHT_CALIBRATION` compassmot start + COMPASSMOT_STATUS subscription, live throttle/current/interference/compensation + `LivePlot`, SendAck finish. Matches `ConfigCompassMot`.
- [ ] **DroneCAN/UAVCAN** (`ConfigDroneCanViewModel.cs`) — real MAVLink `CAN_FORWARD` + SLCAN node enumeration with health/mode/uptime/SW-HW version. PARTIAL: node-inspector only; no per-node parameter editor / firmware-update / restart menu like upstream. (partial)
- [x] **RTK/GPS Inject** (`ConfigGpsInjectViewModel.cs`) — `CommsNTRIP` caster connect + worker thread feeding `InjectGpsData`, byte/rate stats, GGA from live pos. Matches `ConfigSerialInjectGPS`.
- [ ] **Secure** (`ConfigSecureViewModel.cs`) — real `SECURE_COMMAND` get-session-key / get-public-keys work. PARTIAL: Set/Remove keys sent unsigned (no Ed25519 signer reachable) so the autopilot rejects them — write path not functional. (partial)
- [x] **Secure (Sign Firmware)** (`ConfigSecureApViewModel.cs`) — 1:1 Ed25519 keygen + APJ/BL signing via upstream `SignedFW` + BouncyCastle. Matches `ConfigSecureAP`.
- [x] **Terminal** (`ConfigTerminalViewModel.cs`) — live serial passthrough read/write with backspace handling + +++ escape. Functional console (no firmware CLI-mode entry/log-dump, which upstream `ConfigTerminal` had for legacy boards).
- [ ] **Script REPL** (`ConfigScriptReplViewModel.cs`) — echoes "IronPython engine not available in this build"; no execution. (stub)
- [x] **Bluetooth** (`ConfigHWBTViewModel.cs`) — AT-command HC-05/06 programming over serial across candidate bauds (NAME/BAUD/PSWD/ROLE/RESET). Matches `ConfigHWBT`.
- [x] **ESP8266** (`ConfigHWESP8266ViewModel.cs`) — loads/saves WIFI_* over the UDP_BRIDGE component (param_request_list + packed SSID/PWD/IP), reboot, reset-defaults. Matches `ConfigHWESP8266`.
- [x] **HW CAN** (`ConfigHWCANViewModel.cs`) — BRD_CAN_ENABLE combo (real write) + UAVCAN enumerate start/stop (`PREFLIGHT_UAVCAN`) + save/factory-reset (`PREFLIGHT_STORAGE`). Matches `ConfigHWCAN`.
- [x] **PX4Flow** (`ConfigPX4FlowViewModel.cs`) — live `OpticalFlow` image stream → WriteableBitmap + Focus/Video toggle (`CalibrationMode`). Matches `ConfigHWPX4Flow`.
- [x] **Joystick** (`ConfigJoystickViewModel.cs`) — `JoystickBase` device enum + per-channel axis/expo/reverse map + button functions + Enable/Manual-control. (DirectInput device enum is Windows-only, matching upstream.)
- [ ] **Install Firmware** (modern manifest) (`Setup/SetupActionPages.cs` `InstallFirmwareViewModel`) — vehicle buttons only log "not yet wired". (stub)
- [x] **Install Firmware (Legacy)** (`ConfigFirmwareLegacyViewModel.cs`) — real manifest fetch (`APFirmware`), download .apj, reboot-to-bootloader + `px4uploader` board-id-matched flash with progress. Matches `ConfigFirmware`.
- [ ] **Advanced (section tools)** (`ConfigAdvancedViewModel.cs`) — 13 tool buttons (MAVLink Inspector, Signing, NMEA, FollowMe…) all log "(port pending)". (stub)

### Bespoke pages

- [x] **Basic Tuning — Copter** (`ConfigBasicTuningViewModel.cs`) — slider items (`SimplePidItem`, metadata range/increment) with live per-change write + coupled `<relation>` multipliers + info log. Mirrors `ConfigSimplePids` (hardcoded template vs `acsimplepids.xml`).
- [x] **Extended Tuning — Copter/QP** (`ConfigExtendedTuningViewModel.cs` : `TuningPageBase`) — full Rate/Stabilize/Throttle/Vel/Pos/WPNav/Filters PID matrix, long alias chains incl. Q_/PSC_, CH6–10 in-flight-tuning combos, lock-roll/pitch mirroring, >2× confirm. Matches `ConfigArducopter`. (Minor: mirror is RLL→PIT one-way, no NAV_LAT/LON pairing, no 4.7 unit warning.)
- [x] **Basic Tuning — Plane** (`ConfigArduplaneViewModel.cs` : `TuningPageBase`) — Throttle/Airspeed/Nav/Mixes/Energy/Alt/AS/Yaw/Servo/L1/TECS groups, aliases, >2× guard, Write/RefreshParams/RefreshScreen. Matches `ConfigArduplane`.
- [x] **Basic Tuning — Rover** (`ConfigArduroverViewModel.cs` : `TuningPageBase`) — Throttle/Speed/Nav/Steering/Avoidance(conditional)/SteeringMode/ChannelOpts groups, aliases, real write. Matches `ConfigArdurover`.
- [x] **Camera Gimbal** (`ConfigMountViewModel.cs`) — 3-axis (Tilt/Roll/Pan) function assignment, servo MIN/MAX/REVERSED, angle limits, RC input channel, shutter (relay/transistor/servo), neutral/retract, MNT type — all real `setParam`. Matches `ConfigMount`.
- [ ] **Traditional Heli** (`ConfigTradHeli`) — no VM; swashplate/servo/RSC/ZedGraph collective curve not ported. (missing)
- [ ] **Traditional Heli gen-2** (`ConfigTradHeli4`) — no VM; H_* servo/swash/throttle/governor metadata grid not ported. (missing)
- [ ] **CubeID** (`ConfigCubeID`) — no VM. (missing)
- [ ] **Initial Tune Params** (`ConfigInitialParams`) — no VM; weight/prop preset wizard not ported. (missing)
- [ ] **MAVFtp** (`MavFTPUI`) — no VM. (missing)

> Also missing (no VM): `ConfigAteryx`/`ConfigAteryxSensors` legacy Ateryx pages, `ConfigHWCAN` legacy already covered by HW CAN above. Header/landing pages (`ConfigMandatory`/`ConfigOptional`) map to `InfoPageViewModel` (fine).


---

## Log Review (LogBrowse · MavlinkLog · LogAnalyzer · graph presets) — Avalonia status

Verified working: 8 / total 87

Scope verified in Avalonia:
- `Views/LogBrowseView.axaml` (+`.axaml.cs`) + `ViewModels/LogBrowseViewModel.cs` — standalone reduced log viewer (combo type/field + single ScottPlot series + export buttons). NOTE: not wired into any nav/MainWindow (only referenced in PORT_STATUS.md); the live entry points are the FlightData bottom tabs.
- `ViewModels/FlightDataViewModel.cs` lines 463-687 — Telemetry Logs tab (TlogPlayer) + DataFlash Logs tab (DataFlashLog) commands.
- `Services/DataFlashLog.cs` — real DFLogBuffer parse (`MissionPlanner.Log`), real KML (KMLib) / GPX (XmlWriter) / Matlab (`MatLab.ProcessLog`) / `BinaryLog.ConvertBin`. Backend types confirmed in `external/MissionPlanner/ExtLibs/Utilities/`.
- `Services/TlogPlayer.cs` — real MavlinkParse tlog reader, play/pause/seek/speed thread, replay onto `cs`, `ExportKml` from GLOBAL_POSITION_INT.
- `Controls/LivePlot.cs` — ScottPlot AvaPlot wrapper (SetSeries/ClearAll, single axis).

Design reality: the entire LogBrowse left checkbox-tree, GPS track map, virtual datagrid, dual Y-axis, preset dropdown, derived-math expressions, MavlinkLog conversion hub, LogDownload-via-MAVLink, and the py2exe LogAnalyzer auto-test suite have NO Avalonia equivalent. What exists is a flat "pick TYPE.FIELD → plot one series + export track" reduction.

### Entry points — FlightData "DataFlash Logs" / "Telemetry Logs" tabs
- [ ] `BUT_logbrowse` "Review a Log" — `ReviewLog` (FlightDataVM:589) opens a real picker + `DataFlashLog.ReadTrack`, but only reports GPS-point count; full LogBrowse form is the reduced `LogBrowseView`, not opened as a form. (partial)
- [ ] `BUT_loganalysis` "Auto Analysis" — `AutoAnalysis` (VM:662) parses the real bin but emits a custom 3-metric string (min batt / max VibeZ / GPS<6sats); NOT the py2exe runner, no GOOD/WARN/FAIL, none of the upstream test categories. (wrong-design)
- [ ] `BUT_DFMavlink` "Download DataFlash Log Via Mavlink" — no equivalent. (missing)
- [x] `but_bintolog` "Convert .Bin to .Log" — `ConvertBinToLog` (VM:630) → `BinaryLog.ConvertBin` writes a real text .log. (single-file vs upstream multi-select)
- [x] `but_dflogtokml` "Create KML + gpx" — `CreateKmlGpx` (VM:611) → real `ExportKml`+`ExportGpx` next to the log.
- [x] `BUT_loadtelem` "Load Log" — `LoadTlog` (VM:480) → `TlogPlayer.Open` parses real tlog packets.
- [x] `BUT_playlog` "Play/Pause" + speed slider — `PlayPauseTlog`/`SetTlogSpeed`/seek (VM:495-536); real thread replays packets onto `cs` (HUD/map), speed clamp + seek work.
- [ ] `BUT_log2kml` "Tlog > Kml or Graph" — only `TlogToKml` (VM:567) exists; the MavlinkLog graph/convert/extract hub form is absent. (partial)
- [ ] `BUT_matlab` (tlog → .mat) — tlog Matlab export absent (`MatLabForms.ProcessTLog` not wired); only DataFlash bin Matlab exists. (missing)

### LogBrowse — opening a log
- [x] `BUT_loadlog` "Load A Log" — `OpenBtn`→`LoadFileAsync` (VM:51) filter `*.tlog;*.bin;*.log`, parses via `DFLogBuffer`, fills MessageTypes/Fields. (no last-dir memory)
- [x] Async load — `Task.Run(Parse)` with `Busy`/`Status="Parsing log…"`; real background parse. (no "Scanning File" modal, but async + progress text)
- [ ] Title bar `Log Browser - <file> - <vehicle version>` — Info shows name/size/typecount/GPS only; no vehicle/version sniff, no title. (partial)
- [ ] Warns + closes if FMT records missing — no guard. (missing)
- [ ] Preset list from `mavgraph.readmavgraphsxml()` → `CMB_preselect` — no preset system. (missing)

### LogBrowse — layout (3 split containers)
- [ ] `splitContainerAllTree` (graph/grid + tree + txt_info) — replaced by combo/combo/textbox sidebar. (missing)
- [ ] `splitContainerZgGrid` (graph+map / buttons+grid) — no map, no grid. (missing)
- [ ] `splitContainerZgMap` (zg1 plot + map + 4 legend labels) — single plot, no map/legend. (missing)
- [ ] `splitContainerButGrid` (button strip + dataGridView1) — no datagrid. (missing)

### LogBrowse — left tree (message types)
- [ ] `treeView1` 3-level checkbox tree (MsgType→instance→field), check adds/removes curve — replaced by two flat ComboBoxes (type, field). (wrong-design)
- [ ] Left-click = left axis / right-click = right axis — single left axis only. (missing)
- [ ] Node hover tooltip + `txt_info` field unit/description — no field metadata shown. (missing)
- [ ] Double-click field → `DataModifer` scaler & offset (`/100 +50`) — none. (missing)
- [ ] `treeView1_DrawNode` curve-coloured node text — none. (missing)

### LogBrowse — button / checkbox strip
- [x] `BUT_Graphit` "Graph Left" — `GraphBtn`→`OnGraph` → `DataFlashLog.ReadField` real series → `LivePlot.SetSeries` (left axis).
- [ ] `BUT_Graphit_R` "Graph Right" — no right axis. (missing)
- [x] `BUT_cleargraph` "Clear Graph" — `ClearBtn`→`Plot.ClearAll`. Real.
- [ ] `BUT_removeitem` "Remove Item" — no single-curve removal (only clear-all). (missing)
- [ ] `chk_datagrid` "Data Table" (LB_Grid) — no datagrid. (missing)
- [ ] `chk_time` "Time" axis toggle (LB_Time) — X always time(s), no line-number toggle. (missing)
- [ ] `CHK_map` "Map" (LB_Map) — no map panel. (missing)
- [ ] `chk_mode` "Mode" overlay — none. (missing)
- [ ] `chk_errors` "Errors" overlay — none. (missing)
- [ ] `chk_events` "Events" overlay — none. (missing)
- [ ] `chk_msg` "MSG" overlay — none. (missing)
- [ ] `chk_params` "Show Params" overlay — none. (missing)
- [ ] `CMB_preselect` preset dropdown — none. (missing)

### LogBrowse — graph plot (zg1, ZedGraph)
- [ ] Synchronized dual Y axes; zoom/pan — ScottPlot gives built-in zoom/pan on a single axis; no synchronized left/right axes. (partial)
- [ ] Right-click context menu (Copy/Save Image/Print/Un-Zoom/Set Scale…) — ScottPlot's own menu, not the ZedGraph set. (partial)
- [ ] Double-click plot → `GoToSample` (scroll grid + move map marker) — no grid/map to target. (missing)
- [ ] Derived-math expressions (`mag_field`, `earth_accel_df`, `gps_velocity_df`, `delta`, `distance_from`…) — `ReadField` only parses literal TYPE.FIELD. (missing)

### LogBrowse — GPS track map (myGMAP1)
- [ ] Draw colour-coded GPS/GPS2/POS/GPSB routes — no map. (missing)
- [ ] `myGMAP1_OnRouteClick` jump to nearest sample — no map. (missing)
- [ ] Map marker drag / position pickup — no map. (missing)

### LogBrowse — data grid (dataGridView1)
- [ ] Virtual grid (`CellValueNeeded`), per-message columns, context menu — none. (missing)
- [ ] Double-click row → `GoToSample` — none. (missing)
- [ ] RowEnter / column ops / `get_extra_info` (SERVOn_FUNCTION, GPS/MAG/BARO annotations) — none. (missing)
- [ ] Context menu "Export Visible" → output.csv — none. (missing)
- [ ] Context menu "Export Files" (embedded FILE records, path-sanitized) — none. (missing)

### MavlinkLog form (tlog graph + conversion/export hub)
- [ ] `BUT_graphmavlog` "Graph Log" — no tlog graphing. (missing)
- [ ] Tree node double-click → `GraphItem` (left/right axis) — none. (missing)
- [ ] `BUT_redokml` "Create KML + GPX" — `TlogPlayer.ExportKml` writes real .kml from GLOBAL_POSITION_INT, but no .gpx. (partial)
- [ ] `BUT_humanreadable` "Convert to Text" — none. (missing)
- [ ] `BUT_convertcsv` "Convert to CSV" — none. (missing)
- [ ] `BUT_paramsfromlog` "Extract Params" — none. (missing)
- [ ] `BUT_getwpsfromlog` "Extract WPs" — none. (missing)
- [ ] `BUT_matlab` tlog → .mat — none. (missing)
- [ ] `but_cs` "Extract CS" — none. (missing)

### LogDownloadMavLink (download DataFlash via MAVLink)
- [ ] `BUT_DLall` / `BUT_DLthese` — no log-download-over-MAVLink screen. (missing)
- [ ] `BUT_clearlogs` "Clear Logs" — none. (missing)
- [ ] `BUT_redokml` / `BUT_firstperson` / `BUT_bintolog` post-download — none. (missing)
- [ ] Serial variant (`LogDownload`/`BUT_dumpdf`) + `LogIndex` session index — none. (missing)

### LogAnalyzer — Auto Analysis (Utilities/LogAnalyzer.cs + py2exe runner)
- [ ] `CheckLogFile` downloads `LogAnalyzer(64).zip` / runs `runner.exe -x …` — none (Avalonia uses an inline custom analyzer). (missing)
- [ ] `Results()` XML → analysis metadata parse — none. (missing)
- [ ] `Controls.LogAnalyzer` read-only metadata + per-test text UI — none (just a `LogStatus` string). (missing)
- [ ] Status values GOOD/WARN/FAIL/NA/UNKNOWN — none. (missing)

### LogAnalyzer — automated test categories
- [ ] Brownout (TestBrownout) — (missing)
- [ ] Compass (TestCompass) — (missing)
- [ ] GPS (TestGPSGlitch) — (missing)
- [ ] VCC (TestVCC) — (missing)
- [ ] PM / Performance (TestPerformance) — (missing)
- [ ] Vibration (TestVibration) — (missing)
- [ ] IMU Mismatch (TestIMUMatch) — (missing)
- [ ] Gyro Drift (TestDualGyroDrift) — (missing)
- [ ] Pitch/Roll (TestPitchRollCoupling) — (missing)
- [ ] Autotune (TestAutotune) — (missing)
- [ ] Motor Balance (TestMotorBalance) — (missing)
- [ ] Thrust (TestThrust) — (missing)
- [ ] OpticalFlow (TestOptFlow) — (missing)
- [ ] Event/Failsafe (TestEvents) — (missing)
- [ ] Parameters (TestParams) — (missing)
- [ ] NaNs (TestNaN) — (missing)
- [ ] Dupe/Empty (TestDupeLogData/TestEmpty) — (missing)

### Graph presets (graphs/*.xml — CMB_preselect source)
- [ ] `mavgraphs.xml` (108 graphs) — no preset system. (missing)
- [ ] `mavgraphs2.xml` (137 graphs) — (missing)
- [ ] `mavgraphsMP.xml` (30 graphs) — (missing)
- [ ] `ekfGraphs.xml` (41 graphs) — (missing)
- [ ] `ekf3Graphs.xml` (47 graphs) — (missing)

---
Notes:
- MATLAB export from a DataFlash .bin DOES work (`CreateMatlab` VM:647 → `DataFlashLog.ExportMatlab` → real `MatLab.ProcessLog`) — this is an Avalonia addition not in the upstream LogBrowse strip, so it's not scored as an upstream item but is genuinely functional.
- Standalone `LogBrowseView` is fully functional for load/graph/clear/KML/GPX/Matlab/Bin→Log but appears unrouted in navigation; the same backend is reachable via the FlightData log tabs which ARE wired.
- Prior `button-audit.md` claim that LogBrowse "Open log" is a STUB (line 212, "only sets Info, does NOT parse") is now STALE — `LoadFileAsync` parses via `DFLogBuffer` and graphing reads real series. The earlier RawParams rows (202-204) remain accurate.


---

## Tool Modules (Swarm · Antenna Tracker · Joystick · Radio/SiK) — Avalonia status

Verified working: 11 / total 78

Scope note: only items that exist in the Avalonia port AND reach a real backend are `- [x]`.
Whole upstream modules with no counterpart in `src/MissionPlannerAvalonia` are mirrored
unchecked + `(missing)`. The port has NO Swarm code at all, and its "Antenna Tracker" is a
different thing (a param-config page) — not the upstream standalone TrackerUI (Maestro/ArduTracker
serial servo controller), so all upstream Tracker items are unchecked.

Files inspected:
- `src/MissionPlannerAvalonia/ViewModels/Setup/SetupActionPages.cs` (SikRadioViewModel — real)
- `src/MissionPlannerAvalonia/Views/Setup/SikRadioView.axaml(.cs)`
- `src/MissionPlannerAvalonia/ViewModels/GCSViews/ConfigurationView/ConfigJoystickViewModel.cs`
- `src/MissionPlannerAvalonia/Views/GCSViews/ConfigurationView/ConfigJoystickView.axaml(.cs)`
- `src/MissionPlannerAvalonia/ViewModels/GCSViews/ConfigurationView/ConfigAntennaTrackerViewModel.cs`
- `src/MissionPlannerAvalonia/Views/GCSViews/ConfigurationView/ConfigAntennaTrackerView.axaml(.cs)`
- backend: `external/MissionPlanner/ExtLibs/ArduPilot/Joystick/JoystickBase.cs` + JoystickWindows/Linux

---

## Swarm (Formation · Follow Path · Follow Leader · Sequence · SRB · Waypoint Leader)

`grep -ri "swarm" src/MissionPlannerAvalonia` → ZERO matches. Entire module absent.

- [ ] Swarm.cs base (Arm/Disarm/Takeoff/Land/Guided/Auto) **(missing)**
- [ ] FormationControl GUI (Arm/Disarm/Takeoff/Land/Guided/Auto/SetLeader/Start/UpdatePos, grid, status) **(missing)**
- [ ] FollowPathControl GUI (Arm/Disarm/SetLeader/Start/ConnectMAVs, status cards) **(missing)**
- [ ] Status per-drone card UserControl **(missing)**
- [ ] Grid formation canvas (drag icons, vertical toggle, change-alt menu, zoom) **(missing)**
- [ ] Formation.cs / FollowPath.cs / DroneBase.cs backends **(missing)**
- [ ] FollowLeader/Control (ground/air master, arm/takeoff/auto/guided/navguided/start, sep/lead/alt) **(missing)**
- [ ] SRB/Control (start/z/land/stop, alt/offset/zspeed, live BasePos/Vel/Mode/Heading) **(missing)**
- [ ] Sequence/LayoutEditor (load/new/addstep/runstep/save/setimage, steps listbox, grid) **(missing)**
- [ ] WaypointLeader/WPControl (masters, start/reset/rth/rtl, V/alt-interleave, zedGraph plot) **(missing)**

---

## Antenna Tracker (upstream TrackerUI — Maestro/ArduTracker/DegreeTracker serial controller)

Port has `ConfigAntennaTrackerViewModel`, but it is a **vehicle param-config page** (writes
SERVO_*/RC*/YAW2SRV/PITCH2SRV params on a connected tracker autopilot + a DO_SET_SERVO test),
NOT the standalone TrackerUI that drives a Maestro/ArduTracker servo board over its own serial
port and follows live MAVLink az/el. None of the upstream TrackerUI items are implemented.

- [ ] CMB_interface (Maestro/ArduTracker backend select) **(missing)** — no ITrackerOutput layer
- [ ] CMB_serialport / CMB_baudrate (tracker's own port) **(missing)**
- [ ] BUT_connect (open port, push ranges/trims/PWM/speed/accel, 10 Hz PanAndTilt thread) **(missing)**
- [ ] BUT_find (SiK SNR auto pan-trim search) **(missing)**
- [ ] Pan section: TXT_panrange/TRK_pantrim/LBL_pantrim/CHK_revpan/PWM/center/speed/accel **(missing)**
- [ ] Tilt section: TXT_tiltrange/TRK_tilttrim/LBL_tilttrim/CHK_revtilt/PWM/center/speed/accel **(missing)**
- [ ] mainloop 10 Hz tracker.PanAndTilt(cs.AZToMAV, cs.ELToMAV) — live vehicle follow **(missing)**
- [ ] saveconfig()/Activate persistence as `Tracker_<name>` settings **(missing)**
- [ ] TrackerGeneric.cs controller + Maestro/ArduTracker/DegreeTracker ITrackerOutput backends **(missing)**

(Port's actual tracker page — RefreshParams + Yaw/Pitch sliders → DO_SET_SERVO servo test — is a
genuine but unrelated feature; not part of the upstream tool spec, so not counted here.)

---

## Joystick (JoystickSetup + button/axis assignment)

Real backend confirmed: `JoystickBase.Create()` → `JoystickWindows` (SharpDX DirectInput) on
Win / `JoystickLinux` on Unix. `getDevices/getMovingAxis/getPressedButton` are real HID.
`joy.start()` spawns the mainloop thread that writes RC override AND runs `ProcessButtonEvent`
→ fires real commands (setMode/Arm/Disarm/DO_SET_SERVO/relay/mount/etc.). So enable + axis +
button-press → command path is real.

### JoystickSetup main form
- [x] CMB_joysticks device select + RefreshDevices (getDevices, persisted via JoystickBase) — real HID enumeration
- [x] BUT_enable Enable/Disable → JoystickBase.Create + start(device) + clearRCOverride on disable
- [x] BUT_save → ApplyConfigTo + joystick.saveconfig()
- [x] CHK_elevons → joy.elevons (CH1/CH2 mix), pushed live in Pump
- [x] chk_manualcontrol → joy.manual_control (MANUAL_CONTROL vs RC override), pushed live
- [x] timer/Pump live loop → getValueForChannel into axis bars + isButtonPressed into button bars
- [ ] but_export / but_import (*.joycfg ExportConfig/ImportConfig) **(missing)** — not in view or VM
- [ ] label14 "Loaded Config for <firmware>" **(missing)**

### Per-axis row (JoystickAxis)
- [x] axis map (CMB_CH) → setChannel(ch, axis, reverse, expo)
- [x] BUT_detch Auto Detect → getMovingAxis(name,16000) → CMB_CH
- [x] live output bar (ProgressBarCH) ← getValueForChannel
- [ ] expo_ch / revCH wired in VM model (Expo/Reverse fields exist) but **(partial)** — bound on row, applied via setChannel; reverse/expo editable but no dedicated upstream-style live setExpo/setReverse hooks; counted under axis map

### Button rows + action settings
- [x] cmbbuttonN physical-button assign + mybutN Detect → getPressedButton; functions execute at runtime via ProcessButtonEvent
- [ ] cmbactionN sets `function` only; **butsettingsN "Settings" dialogs MISSING (partial)** — no Joy_* dialogs, so action params never set:
  - [ ] Joy_Button_axis (PWM1/PWM2 → p1/p2) **(missing)**
  - [ ] Joy_ChangeMode (mode → config.mode) **(missing)** — ChangeMode action can't be given a target mode
  - [ ] Joy_Mount_Mode (p1) **(missing)**
  - [ ] Joy_Do_Repeat_Relay (relay/repeat/time → p1-p3) **(missing)**
  - [ ] Joy_Do_Repeat_Servo (servo/pwm/reptime/delay → p1-p4) **(missing)**
  - [ ] Joy_Do_Set_Relay (relay → p1) **(missing)**
  - [ ] Joy_Do_Set_Servo (servo/pwm → p1/p2) **(missing)** — Do_Set_Servo can't be given servo#/pwm
- Net: joystick as RC-override controller works end-to-end; parameterised button actions are not configurable.

---

## Radio / SiK (Sikradio settings + standalone SikRadio app)

Port has `SikRadioViewModel` (a custom reimplementation, NOT upstream ComPort.cs/Uploader.cs).
It opens a raw `SerialPort`, enters AT mode via guard-time `+++`, reads ATI/ATI5/RTI/RTI5,
parses S0..S15 registers, and writes `ATSn=`/`AT&W`/`ATZ` (local) + `RTSn=`/`RT&W`/`RTZ` (remote),
plus `AT&F`/`RT&F` reset. Real read/write/reset path confirmed.

### Action buttons
- [x] BUT_getcurrent "Load Settings" → AT mode, ATI/ATI5 + RTI/RTI5, parse local+remote registers
- [x] BUT_savesettings "Save Settings" → write changed ATSn=/RTSn=, AT&W/RT&W commit, ATZ/RTZ reboot
- [x] BUT_resettodefault "Reset to Defaults" → RT&F/AT&F + &W + Z (remote first)
- [ ] BUT_upload "Upload Firmware (standard)" **(stub)** — button present but `IsEnabled=False`, command logs "deferred"
- [ ] BUT_loadcustom "Upload Firmware (custom)" **(missing)**
- [ ] BUT_Syncoptions "Copy required to remote" **(missing)**
- [ ] BUT_SetPPMFailSafe (L/R) **(missing)**
- [ ] btnRandom AES key / btnSaveToFile / btnLoadFromFile (L+R) **(missing)**
- [ ] Progressbar secret beta-channel toggle **(missing)**
- [ ] linkLabel1 / linkLabel_mavlink / linkLabel_lowlatency **(missing)**

### Param rows
- [x] Core S0..S15 register grid (FORMAT/SERIAL_SPEED/AIR_SPEED/NETID/TXPOWER/ECC/MAVLINK/OPPRESEND/MIN_FREQ/MAX_FREQ/NUM_CHANNELS/DUTY_CYCLE/LBT_RSSI/MANCHESTER/RTSCTS/MAX_WINDOW) as Local/Remote text boxes, read & written by ATSn — real
- [ ] Typed combos/validation per RFD900x/firmware (band ranges, baud/airspeed lists) **(partial)** — raw text only, no per-firmware constraint
- [ ] ENCRYPTION_LEVEL / AESKEY (AT&E= padded key) **(missing)**
- [ ] Extra/dynamic params (NODEID/DESTID/MAX_DATA/MAX_RETRIES/GLOBAL_RETRIES/SER_BRK_DETMS, ENCAP methods) **(missing)**
- [ ] GPIO params (GPI/GPO/SBUS/TXEN485, TPO/TPI=1) **(missing)**

### Live display
- [x] Local/Remote version (ATI / RTI) shown; status + log console populated live
- [ ] ATI2 board / ATI3 band / ATI7 RSSI / txtCountry / RTI2 / remote country **(missing)**

### Underlying (Uploader / RFD900 firmware)
- [ ] getFirmware board→URL + Uploader IHex/XModem flash; Board/Frequency enums **(missing)**

---

## SikRadio standalone app (Config wrapper + Terminal + RSSI forms)

Only the embedded "Settings" panel is reimplemented (above). The standalone app shell is absent.

- [ ] Config.cs wrapper (Settings/Terminal/RSSI/About/Help/Project menus, connect/disconnect modem) **(missing)** — port reuses its own Load/Save buttons instead
- [ ] Rssi.cs live ZedGraph (RSSI/Noise Local+Remote curves) **(missing)**
- [ ] Terminal.cs raw AT console + AT cheat-sheet **(missing)** — port shows a one-way log only
- [ ] RFD900.cs TSession modem protocol / firmware upload **(missing)**


---

## Auxiliary Modules (Grid · GeoRef · NoFly · Utilities · Plugins · Scripts · Updater) — Avalonia status

Verified working: 4 / 121
Almost everything absent. The only real working code: a heavily-simplified Survey (Grid) generator (3 fields → upstream `Grid.CreateGrid` → real WP push). GeoRef is an explicit stub; NoFly / Plugin host / all plugins / IronPython scripts / Updater auto-install are entirely missing. Two adjacent things exist but don't map to checklist items: a working **Lua** script host (`Services/LuaScriptHost.cs`, not the upstream IronPython examples) and a GitHub-releases **update *check*** in Help (not the auto-update installer).

Evidence:
- Survey grid: `ViewModels/FlightPlannerViewModel.cs:235` `GenerateSurveyGrid()` calls upstream `Grid.CreateGrid(...)` and appends real waypoints; dialog `Views/FlightPlannerView.axaml.cs:247` `SurveyDialogAsync` exposes only Alt / Line spacing / Angle. Button `Views/FlightPlannerView.axaml:88` "Survey (Grid)".
- GeoRef: `ViewModels/FlightDataViewModel.cs:685` `GeoReferenceImages()` = one-line stub ("EXIF tooling not bundled").
- NoFly: no match anywhere (the only "plugin" hit is `Avalonia.Data.Core.Plugins`).
- Scripts: `Services/LuaScriptHost.cs` (MoonSharp Lua), wired in `FlightDataViewModel`. Not the upstream `Scripts/*.py`.
- Updater: `ViewModels/HelpViewModel.cs:39` `CheckAsync()` hits GitHub releases API + opens download page; no `.new` swap, no `Updater.exe`.

### Grid — Survey (Grid) Generator

**Tab: Simple**
- [ ] `tabSimple` "Simple" — tab structure (missing; flat 3-field dialog)
- [x] `label1` "Altitude" → AGL altitude (feeds real `Grid.CreateGrid`)
- [x] `label4` "Angle [deg]" → grid line bearing (feeds real generation)
- [ ] `label3` "Take a picture every [m]" (missing — no trigger spacing)
- [x] `label2` "Distance between lines [m]" → lane spacing (feeds real generation)
- [ ] `label5` "OverShoot [m]" (missing)
- [ ] `label6` "StartFrom" corner combo (missing — hardcoded `StartPosition.Home`)
- [ ] `label8` "Overlap [%]" / `label15` "Sidelap [%]" (missing)
- [ ] `label32` "LeadIn [m]" (missing)
- [ ] `label26` "Camera" selector + camera DB (missing)
- [ ] `CHK_camdirection` "Camera top facing forward" (missing)
- [ ] `label24` "Flying Speed" / `CHK_usespeed` (missing)
- [ ] `CHK_toandland` / `CHK_toandland_RTL` TO/Land/RTL bracketing (missing)
- [ ] `label37` "Split into x segments" (missing)
- [ ] `label38` Control-S save / Control-O load (missing)

**Tab: Grid Options (advanced)**
- [ ] `tabGrid` / `groupBox1` "Grid Options" (missing)
- [ ] `chk_crossgrid` "Cross Grid" (missing)
- [ ] `chk_Corridor` + `label43` Corridor (missing)
- [ ] `chk_spiral` + `groupBoxSpiral` Spiral options (missing)
- [ ] `groupBoxSpiral` laps/clockwise/match-perimeter (missing)
- [ ] `groupBox7` "Plane Options" optimise/alt-lanes/spline (missing)
- [ ] `groupBox_copter` heading-hold/delay (missing)
- [ ] `groupBox3` "Trigger Method" radios (missing)
- [ ] `chk_stopstart` "Breakup starts" (missing)
- [ ] `groupBox4` "Display" toggles (boundary/markers/grid/internals/footprints) (missing)
- [ ] `groupBox5` "Stats" live readouts (area/dist/pictures/etc.) (missing)
- [x] `BUT_Accept` "Accept" → generate waypoints & push to mission (real via `Grid.CreateGrid`; partial — only alt/spacing/angle honored)

**Tab: Camera Config**
- [ ] `tabCamera` "Camera Config" (missing)
- [ ] focal length / image+sensor W/H (missing)
- [ ] "Calculated Values" FOV / cm-per-pixel (missing)
- [ ] `BUT_samplephoto` "Load Sample Photo" EXIF read (missing)
- [ ] `BUT_save` save camera to DB (missing)

### GeoRef — Geo Ref Images (all stub/missing)
- [ ] `BUT_browselog` "Browse Log" (missing)
- [ ] `BUT_browsedir` "Browse Pictures" (missing)
- [ ] CAM-msg vs Time-offset radios (missing)
- [ ] `RDIO_trigmsg` / `chk_cammsg` (missing)
- [ ] seconds-offset textbox (missing)
- [ ] `BUT_estoffset` "Estimate Offset" (missing)
- [ ] `TXT_shutterLag` shutter lag (missing)
- [ ] "Min Shutter (s) (CAM)" (missing)
- [ ] drop-at-start / drop-at-end (missing)
- [ ] AMSL / base-alt / GPSAlt / GPS2 altitude options (missing)
- [ ] Dir/Cross fov + Rotation tags (missing)
- [ ] `BUT_doit` "Pre-process" build match table (stub)
- [ ] `BUT_Geotagimages` "GeoTag Images" write EXIF (stub — `GeoReferenceImages()` one-liner)
- [ ] `BUT_networklinkgeoref` "Location Kml" (missing)

### NoFly (all missing)
- [ ] startup `*.kmz` scan → purple overlay (missing)
- [ ] shipped zone files (missing)
- [ ] online HK / EU zone fetch (missing)
- [ ] `UpdateNoFlyZone(plla)` movement refresh (missing)

### Utilities (almost all missing)
- [ ] `AirMarket.cs` airspace-market panel (missing)
- [ ] `BoardDetect.cs` USB VID/PID board autodetect (missing)
- [ ] `CircleSurveyMission.cs` circular survey (missing)
- [ ] `ExtensionsMP.cs` helpers (missing)
- [ ] `Firmware.cs` firmware list/flash (missing)
- [ ] `GStreamerUI.cs` pipeline dialog (missing; note a `VideoControl.cs` exists but no GStreamer config UI)
- [ ] `httpserver.cs` built-in HTTP server (missing)
- [ ] `ImageMatch.cs` template matching (missing)
- [ ] `LangUtility.cs` culture helper (missing)
- [ ] `LogAnalyzer.cs` automated log analysis (missing)
- [ ] `NativeLibrary.cs` P/Invoke loader (missing)
- [ ] `OsdTuningSlotProvider.cs` (missing)
- [ ] `POI.cs` points-of-interest store (missing)
- [ ] `Speech.cs` TTS engine (partial — only a Speech toggle surfaced in `ConfigPlannerViewModel`; no engine ported)
- [ ] `SSHTerminal.cs` SSH window (missing)
- [ ] `ThemeManager.cs` global theming (missing)
- [ ] `Update.cs` in-app update checker (partial — see Updater)
- [ ] `Win32DeviceMgnt.cs` COM enumeration (missing)
- [ ] `XMLColor.cs` color serializer (missing)
- [ ] `protogen/` codegen tool (missing; build-time, n/a)
- [ ] cross-cutting utils (ParameterMetaDataParser / Airports / srtm / ImageLabel / KML libs) (missing as ported Avalonia utilities)

### Plugins (entire subsystem missing)
- [ ] all example*.cs loadable plugins (example, watchbutton, menu, fencedist, latency, mapicondesc, modechange, hudonoff, canlog, canrtcm, trace, forwarding, multiforward, herelink2, herelink, mass, leds, donate, menuremove, externalapi, multiplepositions, persistentsimple, fontsize, payloadconfig, switch) — all (missing)
- [ ] `AnonymizeBinlogPlugin.cs` (missing)
- [ ] `generator.cs` generator monitor (missing)
- [ ] `InitialParamsCalculator.cs` (missing)
- [ ] `Dowding/` counter-UAS (missing)
- [ ] `FaceMap/` quarry-face survey (missing)
- [ ] `OpenDroneID2/` Remote-ID receiver (missing)
- [ ] `Shortcuts/` Alt+key shortcuts (missing)
- [ ] `TerrainMakerPlugin/` Make Terrain DAT (missing)

### Plugin host (missing)
- [ ] `Plugin.cs` base + `PluginLoader.cs` runtime compile + `PluginUI.cs` manager dialog (missing — no plugin runtime in Avalonia port)

### Scripts (IronPython examples all missing; a Lua host exists instead)
- [ ] `example1.py`…`example10.py` tutorials (missing)
- [ ] `example4 wp.py` (missing)
- [ ] `example5 inject data.py` (missing)
- [ ] `example8 - speech.py` (missing)
- [ ] `example9 - sitl.py` (missing)
- [ ] `TAKEOFF.py` (missing)
- [ ] `PARACHUTE LANDING APPROACH.py` (missing)
- [ ] `rc.py` / `rc - heli.py` (missing)
- [ ] `ui.py` (missing)
- [ ] `wipe.py` (missing)
- [ ] `cubeorange.py` / `datetime.py` / `debugenv.py` (missing)
  - NOTE: a real working **Lua** scripting host exists (`Services/LuaScriptHost.cs`, MoonSharp; binds `comPort`/`cs`, cooperative abort) wired into `FlightDataViewModel`. It is NOT the upstream IronPython subsystem and ports none of the `.py` examples — so no checklist item is satisfied, but the scripting *capability* is present.

### Updater (auto-install missing; only a release check exists)
- [ ] In-app `CheckForUpdate()` → version.txt + MD5 diff + download `.new` (partial — `HelpViewModel.CheckAsync` checks GitHub releases API and opens download page; no version.txt, no MD5, no `.new` download)
- [ ] Handoff: launch `Updater.exe`, exit (missing)
- [ ] `Updater/Program.cs` `.new`→real swap with `.old` backup (missing)
- [ ] Relaunch MissionPlanner (missing)
- [ ] Files: app.config / app.manifest / mykey.snk (missing)


---

## Controls (custom WinForms controls & dialogs) — Avalonia status

Verified working: 8 Avalonia controls
(HudControl, Gauge, MapView, FlightPlannerMap, LivePlot, VideoControl, ParamFieldsView, BackstageView). The port has only ~8 custom controls; the upstream catalogue is ~80, so the vast majority are unported (missing).

---

### Avalonia custom controls — verdicts

- [x] **HudControl** (`Controls/HudControl.cs`, ~600 lines) — genuine GDI-faithful HUD. `Render()` draws, driven by live telemetry. Bound in `Views/FlightDataView.axaml:20-48` to FlightDataViewModel props, which are fed every 100 ms from `_comPort.MAV.cs` (`FlightDataViewModel.cs:127` timer → `Pump()` :136-178: Roll/Pitch/Yaw/Alt/AirSpeed/GroundSpeed/VerticalSpeed/SatCount/Armed/Mode/BatteryVoltage/BatteryRemaining/NavBearing/CurrentAmps). **Overlay items it actually renders vs upstream's set:** roll/pitch ladder + sky/ground horizon (`DisplayRollPitch`), roll/bank arc + pointer, centre aircraft reticle (banks in Russian mode), heading ribbon with N/E/S/W labels + green nav-bearing marker (`DisplayHeading`), airspeed/groundspeed scroll tape + AS/GS readout (`DisplaySpeed`), altitude scroll tape + target-alt marker + VSI triangle + flight-mode text (`DisplayAlt`), VSI, battery line cell/V/A/% with low-volt red/orange (`DisplayBattery`), GPS 3D-Fix/No-Fix (`DisplayGps`), EKF + Vibe **static text labels** (`DisplayEkf`/`DisplayVibe`), "Not Ready to Arm" when disarmed (`DisplayPrearm`), custom user items text, ARMED/DISARMED. Right-click context menu present (Record AVI, User Items, Russian, Swap, Show icons, per-item toggles). **Partial vs upstream:** EKF/Vibe/Prearm are non-interactive labels — NO clickable hit-zones opening EKFStatus/Vibration/PrearmStatus dialogs (those dialogs aren't ported); Battery2, AOA/SSA, X-Track, wind dir/speed, throttle, connection/link-info, payload icons, alert-flash (failsafe/safetyactive/lowvoltage) are NOT drawn (menu toggles for Battery2/AOA/XTrack/Connection exist but no draw path). The items it does draw are live-telemetry-driven → checked. (partial)

- [x] **Gauge** (`Controls/Gauge.cs`) — analog circular gauge (face, rim, 11 ticks, needle, value + label/units text). Bound in `FlightDataView.axaml:288-290` to live `VerticalSpeed` / `AirSpeed`. Renders a real needle from live telemetry → checked. Upstream **AGauge** is far richer (5 needles, 5 range arcs, configurable caps); this is a single-needle simplification. (partial vs AGauge)

- [x] **MapView** (`Controls/MapView.cs`) — Mapsui `MapControl`; fetches REAL tiles via BruTile `HttpTileSource` from Esri World Imagery (`services.arcgisonline.com/.../World_Imagery`). Live vehicle marker (red symbol) updated every 300 ms from `AppState.comPort.MAV.cs.lat/lng`, auto-centres once. Real tiles + live position → checked.

- [x] **FlightPlannerMap** (`Controls/FlightPlannerMap.cs`) — Mapsui map with REAL tile providers switchable (Google Satellite `mt1.google.com/vt?lyrs=s`, Google Hybrid, OpenStreetMap, Esri) plus custom URL template. Renders waypoint markers (numbered), yellow route polyline, KML track polyline, optional graticule, live vehicle marker (300 ms timer). Supports drag-edit of waypoints with hit-test (`WaypointDragMoved`/`Committed` events). Fully functional map → checked.

- [x] **LivePlot** (`Controls/LivePlot.cs`) — extends `ScottPlot.Avalonia.AvaPlot` (real plotting lib). `SetSeries`/`AppendPoint`/`SetAxisLabels` add real scatter series, thread-marshalled. Used in `LogBrowseView.axaml:42`; `LogBrowseView.axaml.cs:78` plots REAL DataFlash log field data (`DataFlashLog.ReadField` → xs/ys). Also used by ConfigCompassMot / ConfigFFT. Real plotting → checked.

- [x] **VideoControl** (`Controls/VideoControl.cs`) — real LibVLCSharp `VideoView` + `MediaPlayer`. `Play(mrl)` (RTSP/file), `Stop`, `TryRecord` (sout duplicate→file), `Snapshot` all call real libvlc APIs. Used by FlightData (`FlightDataDialogs.cs`, `FlightDataViewModel.cs`). Degrades gracefully to "video unavailable" if native libvlc absent (`InitializeCore` try/catch) — but the pipeline is genuine, not a stub → checked. (Caveat: requires native libvlc at runtime.)

- [x] **ParamFieldsView** (`Controls/ParamFieldsView.axaml`+`.cs`) — the param-render engine. Per-field `ItemsControl` with ComboBox (enum), CheckBox (bool), NumericUpDown (numeric, with Min/Max/Increment from metadata), label+tooltip(description)+units+status. "Refresh Params" button → `RefreshCommand`. Re-verified against `docs/portspec/button-audit.md:218-225`: edits push to FC via `ParamField` prop-change → `Push` → `setParam` (ParamField.cs); Refresh → `comPort.getParamList()`. Functions with live params → checked.

- [x] **BackstageView** (`Views/BackstageView.axaml`+`.cs`, `ViewModels/BackstageViewModel.cs`) — nav container for Setup/Config. Left `ItemsControl` of `BackstagePage` buttons (sub/active classes) + right `ContentControl` bound to `CurrentContent`. Click → `SelectCommand` → `SelectedPage` → lazily instantiates page VM (`Content => _content ??= _factory()`) and shows it via ViewLocator. Functions as a nav switcher → checked. **Simplified vs upstream BackstageView:** no `>>` expand/collapse parent grouping draw, no double-click pop-out-to-Form, no Advanced-toggle gating (IsAdvanced stored but unused), no IActivate/IDeactivate lifecycle. (partial)

**Converters.cs** — only `StringEqualsConverter` (trivial value-equality converter). Functions, but it is a converter, not a control; not counted in the tally.

---

### Major upstream controls with NO port equivalent (unchecked — missing)

Gauges / instruments: **HSI**, **AGauge** (full version), **WindDir**, **QuickView** (Avalonia uses plain TextBlocks instead), **Sphere/ProgressReporterSphere** (mag-cal 3D).
Buttons/inputs: **MyButton**, **MyTrackBar**, **MyLabel**, **MyProgressBar/Horizontal/Vertical/ProgressStep**, **RangeControl**, **ValuesControl**, **ModifyandSet**, **ClickBindingButton/KeyBindingButton**, **FileBrowse** (no native owner-drawn equivalents; standard Avalonia controls used).
Generic dialogs: **InputBox**, **AltInputBox**, **CustomMessageBox**, **OptionForm**, **ProgressReporterDialogue**, **Loading**, **FlashMessage**.
Nav/base: **BackstageViewButton/Page** (folded into XAML template), **MainSwitcher**, **MyUserControl**, **MyDataGridView** (DataGrid used directly).
Connection/link: **ConnectionControl**, **ToolStripConnectionControl**, **ConnectionOptions**, **ConnectionStats**.
Sensor/diagnostics: **ControlSensorsStatus**, **PrearmStatus**, **EKFStatus**, **Vibration**, **Status**, **RAW_Sensor**, **ProximityControl**, **DistanceBar** (HUD EKF/Vibe/Prearm are inert text, not these dialogs).
Param/mavlink tooling: **MAVLinkInspector**, **MavCommandSelection**, **DroneCANInspector/Params/Subscriber**, **DroneCANFileUI/MavFTPUI**, **MavlinkCheckBox/BitMask/ComboBox/NumericUpDown** (ParamFieldsView covers basic param editing only), **paramcompare**.
Setup/config utilities: **DefaultSettings**, **GMAPCache**, **ThemeEditor**, **ScriptConsole**, **AuthKeys**, **LogAnalyzer**, **DevopsUI**, **DigitalSkyUI**, **SB**, **OpenGLtest**.
Telemetry fwd/follow: **FollowMe**, **MovingBase**, **SerialSupportProxy**, **SerialOutputPass/CoT/MD/NMEA**.
Video/gimbal: **Video** (RTSP discovery), **OSDVideo**, **GimbalVideoControl**, **VideoStreamSelector**, **GimbalControlSettingsForm** (VideoControl covers raw playback only).
Signal analysis: **fftui**, **SpectrogramUI**, **PropagationSettings**.
Pre-flight: **CheckListControl/Editor/Input/Item**.
Icons/decorative: **Icon (File/Polygon/Zoom)**, **LineSeparator**, **GradientBG/RadialGradientBG**, **LabelWithPseudoOpacity**, **PictureBoxWithPseudoOpacity**, **PictureBoxMouseOver**, **TransparentPanel**, **ImageLabel**, **Coords**, **SKControl/SKGLControl**, **BindableListView**, **ElevationProfile**.


---

## ExtLibs (backend libraries powering the UI) — Avalonia status

**Verified wired: 7 / 61 libs.**

The Avalonia port directly references only **3** upstream ExtLibs projects (`MissionPlannerAvalonia.csproj` L46-48): `ExtLibs/ArduPilot` (MissionPlanner.ArduPilot.csproj), `ExtLibs/DroneCAN`, and `../Px4Uploader`. From those, the genuinely wired backend capabilities are: **MAVLink protocol**, **Comms transports** (serial/TCP/UDP/UDP-client/WebSocket/NTRIP/Injection), **parameter metadata**, **DroneCAN node enumeration**, and **firmware upload via px4uploader**. A handful of additional libs (Utilities log-parsing, BaseClasses, Joystick) arrive *transitively* through the ArduPilot project and are used by the UI, but are not direct references — flagged below.

Everything UI-side that upstream got from GMap.NET, ZedGraph, IronPython, SharpDX, OpenTK, AviFile/DirectShow, netDxf, ProjNet is instead supplied by **non-MP NuGet replacements**: Mapsui (maps), ScottPlot (plots), MoonSharp/Lua (scripting), LibVLCSharp (video) — so those upstream libs are correctly marked not-referenced.

`grep -riE "MAVLink|GMap|ZedGraph|Joystick|IronPython|SharpDX|OpenTK|AviFile|Proj4|NETDXF"` over `*.cs` returns **zero** `using GMap/ZedGraph/IronPython/OpenTK/netDxf/ProjNet/SharpDX/AviFile` directives — all such hits are config-page *names* (e.g. ConfigJoystickViewModel) or MAVLink/Comms usage, not those libraries.

### Comms / MAVLink (the heart of the GCS)

- [x] **Mavlink** — MAVLink message defs/parsing/packing. `AppState.comPort = new MAVLinkInterface()` (AppState.cs L8); every VM consumes it. **WIRED.**
- [x] **ArduPilot** — ArduPilot MAVLink layer: `MAVLinkInterface.Open(getparams)`, param get/set, message routing. Direct ProjectReference; used by Connection/FlightData/Config VMs. **WIRED.**
- [x] **Comms** — `ICommsSerial` link abstraction reachable from the Connect dropdown (ConnectionViewModel.BuildStreamAsync). Transports actually reachable from the UI:
  - **Serial port** (`SerialPort` / CommsSerialPort) — default case, ConnectionViewModel L142; also ConfigHWBT, SetupActionPages. ✓
  - **TCP-as-serial** (`TcpSerial` / CommsTCPSerial) — "TCP" option L106; also SimulationViewModel L107 (SITL). ✓
  - **UDP listener** (`UdpSerial`) — "UDP" option L128. ✓
  - **UDP client** (`UdpSerialConnect` / CommsUDPSerialConnect) — "UDPCl" option L118. ✓
  - **WebSocket** (`WebSocket` / CommsWebSocket) — "WS" option L138. ✓
  - **NTRIP** (`CommsNTRIP`) — ConfigGpsInjectViewModel L89 (RTCM correction stream). ✓
  - **RTCM Injection** (`CommsInjection`) — ConfigDroneCanViewModel L89 (DroneCAN GPS inject). ✓
  - NOT reachable: CommsBLE/Bluetooth, CommsWinUSB, CommsFile (file replay), CommsSerialPipe, CommsStream — no instantiation anywhere. **WIRED (7 transport types).**
- [x] **Ntrip** — NTRIP caster client via `CommsNTRIP` in GPS/RTK Inject. **WIRED.**
- [ ] **MavlinkMessagePlugin / MockDroneID / Mock** — no references. (not-referenced)
- [ ] **HIL** — Simulation screen uses a TcpSerial→SITL link, not the HIL glue lib. (not-referenced)
- [ ] **Antenna** — ConfigAntennaTrackerViewModel exists but no antenna-tracker protocol lib wired. (not-referenced)
- [ ] **TrackerHome** — no references. (not-referenced)

### Mapping (GMap.NET stack)

- [ ] **GMap.NET.Core** — replaced by Mapsui.Tiling NuGet; no GMap reference. (not-referenced)
- [ ] **GMap.NET.WindowsForms** — replaced by Mapsui.Avalonia + custom FlightPlannerMap control. (not-referenced)
- [ ] **GMap.NET.Drawing** — (not-referenced)
- [ ] **Maps** — (not-referenced)
- [ ] **KMLib / SharpKml** — TlogPlayer.ExportKml writes KML by hand, not via SharpKml. (not-referenced)
- [ ] **kmlpolygons** — (not-referenced)
- [ ] **MissionPlanner.Gridv2** — survey grid not ported. (not-referenced)
- [ ] **GeoidHeightsDotNet** — (not-referenced)

### Firmware / Upload / Device

- [x] **px4uploader** — `px4uploader.Firmware.ProcessFirmware` + `new Uploader(port,115200)` real flash in ConfigFirmwareLegacyViewModel L151-258. Direct ProjectReference. **WIRED.** (Note: the *new* "Install Firmware" page is a deferred stub — "Firmware fetch/flash not yet wired" — but the **Legacy** page genuinely flashes via px4uploader.)
- [ ] **NetDFULib** — STM32 DFU upload not referenced. (not-referenced)
- [x] **UAVCANFlasher / DroneCAN** — `DroneCAN.DroneCAN`, CANFrame/CANPayload used in ConfigDroneCanViewModel L17-125 (node enumeration over the live MAVLink link). Direct ProjectReference. DroneCAN comms **WIRED**; standalone UAVCANFlasher (node firmware flashing) not present.
- [ ] **NMEA2000** — (not-referenced)
- [ ] **Flasher / Arduino** — (not-referenced)
- [ ] **SharpAdbClient** — (not-referenced)
- [ ] **WinUSBNet / UsbSerialForAndroid / ManagedNativeWifi.Simple** — (not-referenced)
- [ ] **solo** — (not-referenced)
- [ ] **OSDConfigurator** — (not-referenced)

### Plotting / 3D / Video / Drawing

- [ ] **ZedGraph** — replaced by ScottPlot.Avalonia + custom LivePlot control. (not-referenced)
- [ ] **OpenTK-1.0.dll / GLControl** — no 3D view ported. (not-referenced)
- [ ] **Exocortex.DSP** — no FFT analysis. (not-referenced)
- [ ] **AviFile** — no AVI capture. (not-referenced)
- [ ] **DirectShowLib / WebCamService / LibVLC.NET / GStreamerHud / Onvif** — replaced by LibVLCSharp.Avalonia NuGet + custom VideoControl. (not-referenced)
- [ ] **netDxf / dxf** — (not-referenced)
- [ ] **SvgNet** — (not-referenced)
- [ ] **Transitions** — (not-referenced)
- [ ] **MissionPlanner.Drawing / CoreCompat.System.Drawing / SkiaSharp.Views.Forms** — Avalonia/Skia native; shim libs not referenced. (not-referenced)
- [ ] **MetaDataExtractorCSharp240d** — no geotagging tool. (not-referenced)
- [ ] **BitMiracle.LibTiff / GDAL / ogr/osr_csharp** — (not-referenced)

### Scripting

- [ ] **python (IronPython)** — replaced by MoonSharp/Lua (Services/LuaScriptHost.cs, ConfigScriptReplViewModel). IronPython itself not-referenced. (not-referenced)
- [ ] **Core / Interfaces** — plugin SDK not ported. (not-referenced)
- [ ] **TestPlugin / SimpleExample / SimpleGrid** — (not-referenced)

### Geo / Projection / Math

- [ ] **GeoUtility** — UTM/MGRS conversions not wired. (not-referenced)
- [ ] **ProjNet / DotSpatial.Projections** — (not-referenced)
- [ ] **alglibnet** — (not-referenced)
- [ ] **LibTessDotNet** — (not-referenced)
- [ ] **AStar.dll** — (not-referenced)
- [ ] **Utilities** — *transitive via ArduPilot.* Directly consumed: `DFLogBuffer`/`dflog` log parsing in LogBrowseViewModel L97-105, and `ParameterMetaDataRepository`/`ParameterMetaDataConstants` in ParamField.cs. Used but not a direct project reference; not in the scoped [x] checklist. (referenced-transitive, used)

### Misc / Support

- [ ] **BaseClasses** — *transitive via ArduPilot.* PointLatLngAlt/locationwp used across FlightPlanner/FlightData. (referenced-transitive, used)
- [ ] **Controls** — upstream WinForms HUD/gauges replaced by Avalonia controls. (not-referenced)
- [ ] **BSE.Windows.Forms / ObjectListView / SimpleGrid / LEDBulb / ImageVisualizer** — replaced by Avalonia DataGrid. (not-referenced)
- [ ] **Strings** — localization not ported. (not-referenced)
- [ ] **speech (System.Speech)** — no TTS. (not-referenced)
- [ ] **WebAPIs / Zeroconf / AltitudeAngelWings / DigitalSky** — (not-referenced)
- [ ] **MissionPlanner.Stats / Benchmark** — (not-referenced)
- [ ] **Ionic.Zip / SharpZipLib / 7zip / zlib.net** — (not-referenced; System.IO.Compression available via BCL only)
- [ ] **System.Data.SQLite** — (not-referenced; Mapsui handles its own tile cache)
- [ ] **System.Reactive** — (not-referenced)
- [ ] **md5sum / Crypto** — BouncyCastle.Cryptography NuGet present but the upstream MP crypto lib not referenced. (not-referenced)
- [ ] **DriverCleanup / CleanDrivers / pinvokegen / Installer / WindowsStore** — (not-referenced)
- [ ] **Xamarin / MonoMac / uno / wasm / mono** — (not-referenced)
- [ ] **ExtGuided** — (not-referenced)
- [ ] **GDAL/Maps/DTED helpers, tlogThumbnailHandler** — (not-referenced)
- [x] **ParameterMetaDataGenerator** — generated param metadata (ranges/desc/options/units) consumed via `ParameterMetaDataRepository` in ParamField.cs L53-146 → Full Parameter List & config-page tooltips/validation. **WIRED.**

> Scoped checklist note: per verification rules, `- [x]` is reserved for the explicitly named capabilities (MAVLink protocol, Comms serial/tcp/udp transports, parameter metadata, DroneCAN, px4uploader firmware upload). Utilities & BaseClasses are genuinely used by the UI but only arrive *transitively* through the single ArduPilot project reference, so they are flagged (referenced-transitive, used) rather than checked. Joystick (`using MissionPlanner.Joystick` in ConfigJoystickViewModel) is likewise transitive; its DirectInput/SharpDX enumeration is Windows-only and effectively dormant on the port's target platforms.


---

