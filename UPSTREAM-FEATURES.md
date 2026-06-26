# Mission Planner — Upstream Feature & Interaction Checklist

Full extraction of `external/MissionPlanner/` (ArduPilot Mission Planner 1.3.83, WinForms) by
source reading. Every screen, button, context menu, double-click, tab, and on-screen label below
is mapped to the concrete handler / MAVLink command / method it fires in upstream. Use this as the
port-fidelity reference: each `- [ ]` is a feature to reproduce 1:1 in the Avalonia port.

> Companion docs: `docs/portspec/` has the deep per-control Setup/Config spec and the Avalonia
> button-audit (port status). This file is the **upstream-side** master inventory across *all* folders.

Format: `- [ ] Label/Control — fires Handler → does X`. Headers mark each screen / tab / menu.

> **Port status (updated 2026-06-26, after parity Phases 0–6).** Boxes now reflect the Avalonia port:
> - `- [x]` = implemented in the port (build-verified; runtime SITL-confirm still pending per Phase V).
> - `- [ ] … — (deferred)` = intentionally out of the 100% target (Swarm suite; Plugins + IronPython + `.py`).
> - `- [ ] … — (not ported)` = genuine gap (target window/lib/behavior not reproduced).
> - `- [ ] … — (unverified)` = couldn't confirm either way from static reading.
>
> Tally: **430 `- [x]` / 887** items implemented. 457 open = 140 deferred + 299 not-ported + 6 unverified
> + 12 structural sub-headers. Authoritative port-status detail lives in `AVALONIA-FEATURES.md` (top
> Implementation Log). **Notable not-ported gaps worth tracking:** FlightPlanner geofence/rally EDITING +
> `cmb_missiontype` switch (mission-type hardcoded); LogBrowse GPS-track map + GoToSample sync + derived-math
> curves; the unported tool windows (Moving Base, Proximity, Spectrogram, log-FFT, Warning Manager, etc.).

## Contents
1. [Application Shell (MainV2 / Program / Common / Splash)](#application-shell-mainv2--program--common--splash)
2. [GCSViews / FlightData (HUD + Map flight screen)](#gcsviews--flightdata-hud--map-flight-screen)
3. [GCSViews / FlightPlanner (mission/waypoint editor)](#gcsviews--flightplanner-missionwaypoint-editor)
4. [GCSViews / Help · Simulation(SITL) · Setup & Config containers](#gcsviews--help--simulationsitl--setup--config-containers)
5. [Log Review (LogBrowse · MavlinkLog · LogAnalyzer · graph presets)](#log-review-logbrowse--mavlinklog--loganalyzer--graph-presets)
6. [Tool Modules (Swarm · Antenna Tracker · Joystick · Radio/SiK)](#tool-modules-swarm--antenna-tracker--joystick--radiosik)
7. [Auxiliary Modules (Grid · GeoRef · NoFly · Utilities · Plugins · Scripts · Updater)](#auxiliary-modules-grid--georef--nofly--utilities--plugins--scripts--updater)
8. [Controls (custom WinForms controls & dialogs)](#controls-custom-winforms-controls--dialogs)
9. [ExtLibs (backend libraries powering the UI)](#extlibs-backend-libraries-powering-the-ui)

---

## Application Shell (MainV2 / Program / Common / Splash)

The shell is a single WinForms `MainV2` form. Its top strip is a `MenuStrip` named `MainMenu` (hosted in `panel1`), holding the nav `ToolStripButton`s on the left and the connect control on the right. Screen bodies swap inside a `MainSwitcher MyView`. There is **no classic File/Edit menu bar** — the only "menu" is the top icon strip plus a right-click `ContextMenuStrip` (`CTX_mainmenu`) and a slide-out auto-hide trigger button (`menu`, a `MyButton`).

Source: `MainV2.cs` (4826 ln), `MainV2.Designer.cs`, `MainV2.resx`, `Program.cs`, `Common.cs`, `Splash.cs`/`.Designer.cs`, `Script.cs`, `Controls/Status.cs`.

### Top Navigation Bar (MainMenu ToolStripButtons, left-aligned)
Each is a `ToolStripButton`; icon comes from `displayicons.*` (theme light/dark, overridable by PNG in running dir); `.Text` from resx (caption under icon).
- [x] DATA — `MenuFlightData` (resx "DATA", tooltip "Flight Data") → `MenuFlightData_Click` → `MyView.ShowScreen("FlightData")` + `SaveConfig()`; screen is the persistent `FlightData` instance (added `AddScreen("FlightData", FlightData, true)`).
- [x] PLAN — `MenuFlightPlanner` (resx "PLAN") → `MenuFlightPlanner_Click` → `MyView.ShowScreen("FlightPlanner")` + `SaveConfig()`; persistent `FlightPlanner` instance.
- [x] SETUP — `MenuInitConfig` (resx "SETUP") → `MenuSetup_Click` (public) → password-gate if `password_protect`, else `MyView.ShowScreen("HWConfig")` → `typeof(GCSViews.InitialSetup)` (lazy, not cached).
- [x] CONFIG — `MenuConfigTune` (resx "CONFIG") → `MenuTuning_Click` → password-gate, then `MyView.ShowScreen("SWConfig")` → `typeof(GCSViews.SoftwareConfig)` (lazy).
- [x] SIMULATION — `MenuSimulation` (resx "SIMULATION", hidden unless `DisplayConfiguration.displaySimulation`) → `MenuSimulation_Click` → `MyView.ShowScreen("Simulation")`; persistent `Simulation` instance.
- [x] HELP — `MenuHelp` (resx "HELP", hidden unless `DisplayConfiguration.displayHelp`) → `MenuHelp_Click` → `MyView.ShowScreen("Help")` → `typeof(GCSViews.Help)` (lazy).
- [ ] (Terminal) — no nav button in Designer; `MenuTerminal_Click` exists → `MyView.ShowScreen("Terminal")` (reached via Setup/Config sub-pages, not a top tab). — (not ported)
- [ ] DONATE — **not a current top button**; legacy PayPal handler `toolStripMenuItem1_Click` opens a paypal donation URL (orphaned). The visible right-side branded button is the ArduPilot logo (below). — (not ported)
- [ ] No `MyView.AddScreen` call exists for Terminal in the registration block (the 6 registered screens are FlightData, FlightPlanner, HWConfig, SWConfig, Simulation, Help) — Terminal is added elsewhere/on demand. — (not ported)
- [x] `MainMenu_ItemClicked` highlights the active button (`BackColor = ThemeManager.ControlBGColor`) and resets others to the `displayicons.bg` background.

### Connection Control (right-aligned on MainMenu)
- [x] COM port dropdown — `_connectionControl.CMB_serialport` (inside `ToolStripConnectionControl`). Populated on `CMB_serialport_Click`: items = "AUTO", `SerialPort.GetPortNames()`, then "TCP","UDP","UDPCl","WS", plus preset labels. Selecting TCP/UDP/etc disables baud box.
- [x] Baud dropdown — `_connectionControl.CMB_baudrate`; `CMB_baudrate_TextChanged` parses to `comPortBaud`. Default load: serialport index 0, baud index 8; remembered per-port via Setting `"<PORT>_BAUD"`.
- [x] Connect/Disconnect button — `MenuConnect` (right-aligned, resx "CONNECT") → `MenuConnect_Click` → `Connect()`: if `comPort.BaseStream.IsOpen` → `doDisconnect(comPort)` else `doConnect(comPort, CMB_serialport.Text, CMB_baudrate.Text)`. Image/Tag/Text toggle between connect/disconnect icons (`Strings.CONNECTc`/`DISCONNECTc`); sanity-check warns if still moving (groundspeed>4).
- [x] `doConnect(comPort, portname, baud, getparams, showui)` — opens stream, sets serialport/baud text from stream, pulls params, refreshes HWConfig/SWConfig if shown, runs `loadph_serial()` (hardware-specific param fixups e.g. CubeBlack INS), updates sysid list (`UpdateSysIDS()`).
- [x] `doDisconnect(comPort)` — cancels speech, `DtrEnable=false`, `comPort.Close()`, stops connection-stats updates, sorts tlogs in background, resets connect icon.
- [ ] Link-stats / inspector — `_connectionControl.ShowLinkStats` — (not ported) event → `ShowConnectionStatsForm()` opens a borderless `Form` hosting `ConnectionStats(comPort)` (live link quality / bytes / time-connected). `ResetConnectionStats()` rebuilds it on reconnect. (This is the link inspector; no separate MAVLink-inspector button in the shell.)

### Right-side menus / branding / Context Menu
- [x] ArduPilot logo button — `MenuArduPilot` (right-aligned, image-only) → `MenuArduPilot_Click` → opens `https://ardupilot.org/?utm_source=Menu&utm_campaign=MP`.
- [ ] Auto-hide slide-out trigger — `menu` (`MyButton`) — (not ported) → `menu_MouseEnter` slides `panel1` (the whole menu strip) into view at top; leaving the strip hides it again. Toggled by `AutoHideMenu(bool)`, persisted in Setting `menu_autohide`.
- [x] Right-click context menu `CTX_mainmenu` items:
  - [ ] AutoHide — `autoHideToolStripMenuItem` — (not ported) (CheckOnClick) → `autoHideToolStripMenuItem_Click` → `AutoHideMenu(Checked)`, saves `menu_autohide`.
  - [x] Full Screen — `fullScreenToolStripMenuItem` → `fullScreenToolStripMenuItem_Click` → borderless TopMost maximized vs FixedSingle maximized.
  - [x] Readonly — `readonlyToolStripMenuItem` → `readonlyToolStripMenuItem_Click` → `comPort.ReadOnly = Checked` (blocks writes to vehicle).
  - [ ] Connection Options — `connectionOptionsToolStripMenuItem` — (not ported) → `connectionOptionsToolStripMenuItem_Click` → `new ConnectionOptions().Show()`.
  - [ ] Connection List — `connectionListToolStripMenuItem` — (not ported) → `connectionListToolStripMenuItem_Click` → file picker of tcp/udp/udpcl/serial URIs, parallel-connects each as a `MAVLinkInterface` (multi-vehicle).

### Status bar
- [x] No text status strip. `status1` (`Controls.Status`, in `panel1`) is a thin green progress overlay: `Percent` (0–100) paints a green fill bar and auto-hides after 10 s. Driven from telemetry/param-download progress (`status1.Percent = ...`). Window title shows product/version via Splash text; live link stats live in the ConnectionStats popup.

### Global keyboard shortcuts (`ProcessCmdKey`, also routed from `MainV2_KeyDown`; `KeyPreview=true`)
- [x] F12 — `MenuConnect_Click` (connect/disconnect toggle).
- [x] F2 — Flight Data screen.
- [x] F3 — Flight Planner screen.
- [x] F4 — Config/Tuning screen (`MenuTuning_Click`).
- [x] F5 — `comPort.getParamList()` then refresh current screen.
- [ ] Ctrl+F — open `temp` form (temperature/diag tool). — (not ported)
- [ ] Ctrl+P — open `PluginUI` (plugin manager). — (deferred)
- [ ] Ctrl+G — `SerialOutputNMEA` (NMEA-out). — (not ported)
- [ ] Ctrl+X — `GMAPCache` (map cache tool). — (not ported)
- [ ] Ctrl+L — `SpectrogramUI`. — (not ported)
- [ ] Ctrl+W — `PropagationSettings`. — (not ported)
- [ ] Ctrl+Z — `Camera().test(comPort)`. — (not ported)
- [ ] Ctrl+T — `comPort.Open(false)` (override/force connect). — (not ported)
- [x] Ctrl+Y — send `PREFLIGHT_STORAGE` write (param save to EEPROM).
- [ ] Ctrl+J — `DevopsUI`. — (not ported)
- [ ] (Ctrl+S screenshot is commented out.) Any unhandled key → `ProcessCmdKeyCallback` — (not ported) event (lets active screen add its own); short-circuited entirely when `ConfigTerminal.SSHTerminal`.

### Boot sequence (Program.cs `Start`)
- [ ] Static ctor wires `AppDomain` handlers: — (not ported) AssemblyLoad/UnhandledException/TypeResolve/FirstChanceException logging; `Application.ThreadException` → `handleException`.
- [ ] `Main`→`Start(args)`: prints data/log/running dirs, — (not ported) detects Mono, `SetCurrentDirectory(running)`, adds trace listener if `trace` arg, `EnableVisualStyles()`, configures log4net (rewrites file appender paths on Unix), sets TLS/connection-limit.
- [ ] CLI handling: `/update` → `Update.DoUpdate()` — (not ported) then exit; `/updatebeta` → beta update then exit.
- [ ] Branding load: `name` = "Mission Planner" — (not ported) (or first line of `logo.txt`); loads `logo.png`/`logo2.png`/`icon.png`/`splashbg.png` overrides; loads `libSkiaSharp` native lib.
- [ ] Splash: `Splash = new Splash()`, — (not ported) applies custom bg/icon, `Splash.Text = name + version + build`, `Splash.Show()` (TopMost unless debugger attached), pumps `Application.DoEvents()`.
- [ ] Wires theme/provider hooks: — (not ported) `CustomMessageBox.ShowEvent`→MsgBox, `ApplyTheme` for MsgBox/MainSwitcher/InputBox/BackstageView/CommsBase, `Tracking` page hooks, Comms settings/inputbox/devicename providers, `Extensions.MessageLoop`.
- [ ] GMap setup: custom image cache, — (not ported) cache mode from `mapCache` setting, registers ~30 custom map providers (WMS/WMTS/MapBox/Japan/GIBS/Esri…), Google API key, GDAL provider if `gdal/` dir exists, system web proxy.
- [ ] Tracking/analytics product info; — (not ported) `MAVLinkInterface.CreateIProgressReporterDialogue` factory; special "VVVVZ" build sets password + disables wizard/update; `CleanupFiles()` deletes stale/`.new` updater & lib files.
- [x] `Application.Run(new MainV2())` (thread named "Base Thread"); on exit kills any running SITL `simulator` processes.
- [ ] `handleException` — central error sink: — (not ported) swallows known benign serial/registry/dispose errors, prompts install messages for missing DLL/.NET, else "Report this Error?" dialog → captures all-thread stacks via ClrMD → POSTs to `vps.oborne.me/mail.php`.

### Splash screen (Splash.cs / Splash.Designer.cs)
- [ ] 600×375 borderless (`FixedSingle`, no ControlBox), — (not ported) centered, TopMost. Background = `Resources.splashdark` (or `splashbg.png`).
- [ ] `pictureBox1` — shows `Program.Logo` — (not ported) over `bgdark` when a custom logo exists (else hidden).
- [ ] `TXT_version` label — — (not ported) "Version: " + `Application.ProductVersion` (white, right-aligned).
- [ ] `label1` — static credit — (not ported) "by Michael Oborne" (olive, right-aligned).
- [ ] No progress bar/text; `Program.Splash` — (not ported) is closed once `MainV2` loads.

### Common.cs — reusable UX helpers (static `Common`)
- [x] `getMAVMarker(MAVState, overlay)` — builds/updates the correct map marker for a vehicle by MAV_TYPE/firmware (Plane/Quad/Rover/Boat/Sub/Heli/Single/AntennaTracker/green-dot); sets heading/cog/target/nav bearings, active-vehicle flag, tooltip from `mapicondesc`.
- [x] `LoadingBox(title, promptText)` — modeless themed "please wait" Form with a label; caller closes it.
- [x] `MessageShowAgain(title, promptText, show_cancel, tag)` — dialog with a "Show me again" checkbox; persists suppression under `SHOWAGAIN_<tag>` setting, marshals to UI thread; returns DialogResult.
- [x] `CreateMessageShowAgainForm(...)` — builds that dialog (TableLayout: message label, optional inline `[link;url;text]` LinkLabel, footer checkbox + OK/Cancel `MyButton`s), themed.
- [x] `chk_CheckStateChanged` — saves the show-again checkbox state to Settings.
- [x] `OpenUrl(url)` — cross-platform URL launcher (Process.Start; Windows shell-execute fallback, `xdg-open` Linux, `open` macOS).
- [x] (Other reusable shell dialogs — `CustomMessageBox`, `InputBox`, `ProgressReporterDialogue`, `Download.GetFileFromNet` — live in their own files but are wired up from Program.cs as the app-wide dialog providers.)

### Script.cs — scripting entry (IronPython)
- [ ] `Script(redirectOutput)` ctor — — (deferred) creates a Python engine, adds search paths (`Lib.zip`, `lib`, running dir), loads all loaded assemblies, and injects globals: `MainV2`, `FlightPlanner`, `FlightData`, `Ports`(Comports), `MAV`(comPort), `cs`(current state), `Script`/`mavutil`(self), `Joystick`. Optional output redirect to `StringRedirectWriter`.
- [ ] `runScript(filename)` — — (deferred) executes a .py file in scope; errors shown via `CustomMessageBox`.
- [ ] Helper API exposed to scripts: — (deferred) `ChangeParam`/`GetParam`, `ChangeMode`, `WaitFor(message,timeout)`, `SendRC(channel,pwm,sendnow)` (rc override), `Sleep(ms)`; stub `mavlink_connection`/`recv_match` for pymavlink-compat.


---

## GCSViews / FlightData (HUD + Map flight screen)

Layout: `MainH` SplitContainer — Panel1 = left column (`SubMainLeft`: HUD `hud1` on top, `tabControlactions` + `panel_persistent` below); Panel2 = `tableMap` (map `gMapControl1` + tuning graph `zg1` in `splitContainer1`, zoom/overlay widgets). HUD and Map can be swapped (`SwapHud1AndMap`). All vehicle commands go through `MainV2.comPort` (the active MAVLink link).

### HUD (`hud1`) — overlay + events
- [ ] HUD double-click — fires `hud1_DoubleClick` → undocks HUD into a floating "HUD Dropout" form (re-dock on close via `dropout_FormClosed`). — (not ported)
- [x] HUD EKF status indicator click — fires `hud1_ekfclick` → opens `EKFStatus` window. — Avalonia: HudControl hit-rect → EKFStatusWindow (verified).
- [x] HUD vibration indicator click — fires `hud1_vibeclick` → opens `Vibration` window. — Avalonia: VibrationWindow (verified).
- [x] HUD prearm indicator click — fires `hud1_prearmclick` → opens `PrearmStatus` window. — Avalonia: PrearmStatusWindow (verified).
- [x] HUD live data (DataBindings → `bindingSourceHud` = `CurrentState`): airspeed, alt, groundspeed, groundcourse, heading/yaw, nav_roll/pitch, roll/pitch, battery voltage/current/remaining (×2), datetime, wp_dist, ekfstatus, failsafe, gpsstatus/hdop (×2), linkqualitygcs, messageHigh (+severity), mode, prearmstatus, safetyactive, armed (status), targetalt, nav_bearing, targetairspeed, turnrate, verticalspeed, vibex/y/z, wpno, xtrack_error, AOA/SSA, lowairspeed.

### HUD right-click context menu (`contextMenuStripHud`)
- [x] "Video" (submenu `videoToolStripMenuItem`):
  - [x] "Record Hud to AVI" — `recordHudToAVIToolStripMenuItem_Click` → starts `AviWriter` to LogDir .avi (button text→"Recording").
  - [x] "Stop Record" — `stopRecordToolStripMenuItem_Click` → `aviwriter.avi_close()` (text→"Start Recording").
  - [x] "Set MJPEG source" — `setMJPEGSourceToolStripMenuItem_Click` → InputBox url → `CaptureMJPEG.runAsync()`.
  - [x] "Start Camera" — `startCameraToolStripMenuItem_Click` → `new Capture(video_device)` → bgimage feed (skipped on MONO).
  - [x] "Set GStreamer Source" — `setGStreamerSourceToolStripMenuItem_Click` → InputBox pipeline → `hudGStreamer.Start(url)`.
  - [x] "HereLink Video" — `HereLinkVideoToolStripMenuItem_Click` → InputBox herelink IP → GStreamer rtsp pipeline start.
  - [x] "GStreamer Stop" — `GStreamerStopToolStripMenuItem_Click` → `hudGStreamer.Stop()`.
- [ ] "Set Aspect Ratio" — `setAspectRatioToolStripMenuItem_Click` → toggle HUD 16:9 vs 4:3. — (not ported)
- [x] "User Items" — `hud_UserItem` → checkbox dialog of `CurrentState` numeric fields to add custom HUD overlay items.
- [x] "Russian Hud" — `russianHudToolStripMenuItem_Click` → toggles `hud1.Russian`, saves `russian_hud`.
- [x] "Swap With Map" — `swapWithMapToolStripMenuItem_Click` → `SwapHud1AndMap()` (swaps HUD↔map panels).
- [x] "Ground Color" (checkable) — `groundColorToolStripMenuItem_Click` → toggles HUD ground brown/green.
- [x] "Battery Cell Voltage" — `setBatteryCellCountToolStripMenuItem_Click` → InputBox cell count → toggles `displayCellVoltage`.
- [x] "Show icons" / "Show text" — `showIconsToolStripMenuItem_Click` → toggles `myhud.displayicons`.

### Bottom tab bar (`tabControlactions`, owner-drawn green gradient `tabControl1_DrawItem`)
Tabs shown/order user-customizable. `tabControl1_SelectedIndexChanged` manages Status/Messages timers. Tabs: Quick, Actions, Messages, Actions(simple), PreFlight, Gauges, Transponder, Status, Servo/Relay, Aux Function, Scripts, Payload Control, Telemetry Logs, DataFlash Logs.

#### Tab-bar right-click menu (`contextMenuStripactionstab`)
- [ ] "Customize" — `customizeToolStripMenuItem_Click` → CheckedListBox dialog choosing which tabs are visible (saves `tabcontrolactions`). — (not ported)
- [ ] "MultiLine" — `multiLineToolStripMenuItem_Click` → toggles `tabControlactions.Multiline`. — (not ported)

#### Tab: Quick (`tabQuick`) — 6 default `QuickView` cells in `tableLayoutPanelQuick`
- [x] QuickView cell double-click — `quickView_DoubleClick` → checkbox dialog of `CurrentState` fields to pick the displayed value (locked if `lockQuickView`).
- [ ] Quick-tab right-click (`contextMenuStripQuickView`): — (not ported)
  - [ ] "Set View Count" — `setViewCountToolStripMenuItem_Click` → InputBox cols+rows → `setQuickViewRowsCols` (saves quickViewRows/Cols). — (not ported)
  - [ ] "Undock" — `undockDockToolStripMenuItem_Click` → pops Quick tab into a floating form; re-docks on close. — (not ported)

#### Tab: Actions (`tabActions`) — buttons (`tableLayoutPanel1`)
- [x] "Arm/ Disarm" (`BUT_ARM`) — `BUT_ARM_Click` → `comPort.doARM(!armed)`; on fail offers Force Arm `doARM(...,true)`. Subscribes STATUSTEXT for failure reason.
- [x] "Do Action" (`BUTactiondo`) + combo `CMB_action` — `BUTactiondo_Click` → executes selected `actions` enum: Format_SD_Card→`STORAGE_FORMAT`; Trigger_Camera→`setDigicamControl`; Scripting_cmd_stop/stop_and_restart→`SCRIPTING`; System_Time→`SYSTEM_TIME` packet; Terminate_Flight→`DO_FLIGHTTERMINATION`; Preflight_Reboot_Shutdown→`doReboot`; HighLatency_Enable/Disable→`doHighLatency`; Toggle_Safety_Switch→`setMode SAFETY_ARMED`; Engine_Start/Stop→`doEngineControl`; Battery_Reset; else generic `doCommand(MAV_CMD)` parsed from name (e.g. Loiter_Unlim, Return_To_Launch, Mission_Start, Preflight_Calibration, Do_Parachute, ADSB_Out_Ident).
- [x] "Set Mode" (`BUT_setmode`) + combo `CMB_modes` — `BUT_setmode_Click` → `comPort.setMode(CMB_modes.Text)` (failsafe confirm). `CMB_modes_Click` repopulates from `Common.getModesList(firmware)`.
- [x] "Set WP" (`BUT_setwp`) + combo `CMB_setwp` — `BUT_setwp_Click` → `setWPCurrent(selectedIndex)`. `CMB_setwp_Click` fills list from CMD_TOTAL/WP_TOTAL/MIS_TOTAL.
- [x] "Restart Mission" (`BUTrestartmission`) — `BUTrestartmission_Click` → `setWPCurrent(0)`.
- [x] "Resume Mission" (`BUT_resumemis`) — `BUT_resumemis_Click` → reprograms mission from waypoint#, (copter) arms + GUIDED + TAKEOFF then AUTO.
- [x] "Set Mount" (`BUT_mountmode`) + combo `CMB_mountmode` — `BUT_mountmode_Click` → sets `MNT_MODE` param or `DO_MOUNT_CONTROL`.
- [x] "Set Home Alt" (`BUT_Homealt`) — `BUT_Homealt_Click` → toggles `cs.altoffsethome`.
- [x] "Abort Landing" (`BUT_abortland`) — `BUT_abortland_Click` → `comPort.doAbortLand()`.
- [x] "Clear Track" (`BUT_clear_track`) — `BUT_clear_track_Click` → clears route + camerapoints.
- [x] "Message" (`BUT_SendMSG`) — `BUT_SendMSG_Click` → InputBox → `comPort.send_text(5,txt)`.
- [ ] "Joystick" (`BUT_joystick`) — `BUT_joystick_Click` → opens `JoystickSetup`. — (not ported)
- [x] "Raw Sensor View" (`BUT_RAWSensor`) — `BUT_RAWSensor_Click` → opens `RAW_Sensor` form.
- [x] "RTL" (`BUT_quickrtl`) — `BUT_quickrtl_Click` → `setMode("RTL")`.
- [x] "Loiter" (`BUT_quickmanual`) — `BUT_quickmanual_Click` → `setMode("Loiter")`.
- [x] "Auto" (`BUT_quickauto`) — `BUT_quickauto_Click` → `setMode("Auto")`.
- [x] "Loiter Rad" (`modifyandSetLoiterRad`) — `modifyandSetLoiterRad_Click` → `setParam(LOITER_RAD/WP_LOITER_RAD)`.
- [x] "Alt" (`modifyandSetAlt`) — `modifyandSetAlt_Click` → `setNewWPAlt`.
- [x] "Speed" (`modifyandSetSpeed`) — `modifyandSetSpeed_Click` → `doCommandAsync(DO_CHANGE_SPEED)`.

#### Tab: Actions (simple) (`tabActionsSimple`)
- [ ] "Loiter" (`myButton1`), "RTL" (`myButton2`), "Auto" (`myButton3`) — large simplified mode buttons (wired to setMode quick handlers). — (not ported)

#### Tab: Messages (`tabPagemessages`)
- [x] Read-only `txt_messagebox` — shows live MAVLink status text; refreshed by `Messagetabtimer` (200ms) while tab active; auto-scrolls to end (`txt_messagebox_TextChanged`).

#### Tab: PreFlight (`tabPagePreFlight`)
- [x] `checkListControl1` (`PreFlight.CheckListControl`) — preflight checklist UI.

#### Tab: Gauges (`tabGauges`)
- [x] `Gspeed` AGauge — `Gspeed_DoubleClick` → InputBox max speed (saves `GspeedMAX`). Also `Galt`, `Gvspeed`, `Gheading` (HSI). Layout via `tabPage1_Resize`.

#### Tab: Transponder (`tabTransponder`) — uAvionix ADS-B
- [x] "Connect to Transponder" (`XPDRConnect_btn`) — `XPDRConnect_btn_Click` → `SET_MESSAGE_INTERVAL` for UAVIONIX_ADSB_OUT_STATUS, waits, `updateTransponder()`.
- [x] "STBY" (`STBY_btn`) — `STBY_btn_Click` → all mode bits off → `uAvionixADSBControl`.
- [x] "ON" (`ON_btn`) — `ON_btn_Click` → mode bits on (no ALT) → `uAvionixADSBControl`.
- [x] "ALT" (`ALT_btn`) — `ALT_btn_Click` → all mode bits on → `uAvionixADSBControl`.
- [x] "IDENT" (`IDENT_btn`) — `IDENT_btn_Click` → `uAvionixADSBControl` with IDENT bit.
- [x] "Squawk" NUD (`Squawk_nud`) — `Squawk_nud_ValueChanged` (octal clamp 0-7 digits), `Squawk_nud_MouseWheel`.
- [x] "FlightID" textbox (`FlightID_tb`) — `FlightID_tb_TextChanged` (max 8 chars) → `uAvionixADSBControl`.
- [ ] `Mode_clb` checked-list (mode flags), `fault_clb`, NIC/NACp readouts (`NIC_tb`/`NACp_tb` via tables). — (not ported)

#### Tab: Status (`tabStatus`)
- [x] Owner-drawn (`tabStatus_Paint`) two-column live dump of every `CurrentState` field via `bindingSourceStatusTab`.

#### Tab: Servo/Relay (`tabServo`)
- [x] 12× `ServoOptions` controls (`servoOptions1..12`) — per-servo PWM set controls (DO_SET_SERVO).
- [x] 16× `RelayOptions` controls (`relayOptions1..16`) — per-relay on/off (DO_SET_RELAY).

#### Tab: Aux Function (`tabAuxFunction`)
- [x] 7× `AuxOptions` controls (`auxOptions1..7`) — trigger RC aux functions.

#### Tab: Scripts (`tabScripts`)
- [x] "Select Script" (`BUT_select_script`) — `BUT_select_script_Click` → file dialog, sets `selectedscript`.
- [x] "Run Script" (`BUT_run_script`) — `BUT_run_script_Click` → runs script on background thread.
- [x] "Abort Running Script" (`BUT_abort_script`) — `BUT_abort_script_Click` → `scriptthread.Abort()`.
- [x] "Edit Selected Script" (`BUT_edit_selected`) — `BUT_edit_selected_Click` → `Process.Start(selectedscript)`.
- [ ] "Redirect Program Output" checkbox (`checkBoxRedirectOutput`) — passed to `Script` ctor. — (not ported)
- [x] Labels: "Selected Script: None" (`labelSelectedScript`), "Script Status: No Script Running" (`labelScriptStatus`).

#### Tab: Payload Control (`tabPayload`) — gimbal
- [x] "Tilt"/Pitch trackbar (`trackBarPitch`) — `gimbalTrackbar_Scroll` → `setMountControl(pitch,roll,yaw)`. Position text `TXT_gimbalPitchPos`.
- [x] "Roll" trackbar (`trackBarRoll`) — `gimbalTrackbar_Scroll`. Text `TXT_gimbalRollPos`.
- [x] "Pan"/Yaw trackbar (`trackBarYaw`) — `gimbalTrackbar_Scroll`. Text `TXT_gimbalYawPos`.
- [x] "Reset Position" (`BUT_resetGimbalPos`) — `BUT_resetGimbalPos_Click` → zero trackbars + `setMountConfigure` + `setMountControl`.
- [ ] "Video Control" (`BUT_GimbalVideo`) — `gimbalVideoPopOutToolStripMenuItem_Click` → opens GimbalVideoControl in its own window. — (not ported)

#### Tab: Telemetry Logs (`tabTLogs`)
- [x] "Load Log" (`BUT_loadtelem`) — `BUT_loadtelem_Click` → open .tlog/.mavlog → `LoadLogFile`.
- [x] "Play/Pause" (`BUT_playlog`) — `BUT_playlog_Click` → toggles `logreadmode` playback, clears tuning lists.
- [x] Playback speed buttons "10x/5x/2x/1x/0.5/0.25/0.1" (`BUT_speed10`..`BUT_speed1_10`) — `BUT_speed1_Click` → sets `LogPlayBackSpeed` from Tag, updates "x 1.0" (`lbl_playbackspeed`).
- [x] "Tlog > Kml or Graph" (`BUT_log2kml`) — `BUT_log2kml_Click` → opens `MavlinkLog` form.
- [x] Position scrub trackbar (`tracklog`) — `tracklog_Scroll` → seeks playback file position, `updateLogPlayPosition`.
- [x] Labels: "0.00 %" (`lbl_logpercent`), filename (`LBL_logfn`), "Speed" (`label2`).

#### Tab: DataFlash Logs (`tablogbrowse`)
- [x] "Download DataFlash Log Via Mavlink" (`BUT_DFMavlink`) — `BUT_DFMavlink_Click` → opens `LogDownloadMavLink`.
- [x] "Review a Log" (`BUT_logbrowse`) — `BUT_logbrowse_Click` → opens `LogBrowse`.
- [x] "Auto Analysis" (`BUT_loganalysis`) — `BUT_loganalysis_Click` → `LogAnalyzer` on chosen .log/.bin.
- [x] "Create KML + gpx" (`but_dflogtokml`) — `but_dflogtokml_Click` → `LogOutput.writeKML`.
- [x] "Geo Reference Images" (`BUT_georefimage`) — `BUT_georefimage_Click` → opens `Georefimage`.
- [x] "Create Matlab File" (`BUT_matlab`) — `BUT_matlab_Click` → `MatLabForms.ProcessLog`.
- [x] "Convert .Bin to .Log" (`but_bintolog`) — `but_bintolog_Click` → `BinaryLog.ConvertBin`.

### Map (`gMapControl1`) controls + overlays
- [ ] Zoom trackbar (`TRK_zoom`) — `TRK_zoom_Scroll` → sets `gMapControl1.Zoom` + syncs `Zoomlevel`. — (not ported)
- [ ] Zoom NUD "Zoom" (`Zoomlevel`, label1) — `Zoomlevel_ValueChanged` → sets map zoom + syncs `TRK_zoom`. — (not ported)
- [x] "Auto Pan" checkbox (`CHK_autopan`) — `CHK_autopan_CheckedChanged` → saves `CHK_autopan` (auto-center on vehicle).
- [ ] "Tuning" checkbox (`CB_tuning`) — `CB_tuning_CheckedChanged` → shows/hides ZedGraph `zg1` tuning plot + starts `ZedGraphTimer`. — (not ported)
- [ ] Tuning graph double-click (`zg1`) — `zg1_DoubleClick` → checkbox dialog to pick up to 20 `CurrentState` curves; per-checkbox `chk_box_tunningCheckedChanged` adds curve (right-click via `Chk_box_tunningMouseDown` → Y2 axis). — (not ported)
- [ ] Map drag — `gMapControl1_MouseDown`/`_MouseMove`/`_MouseUp` → pan; Ctrl+click → Fly To Here; MouseMove shows "Dist to Home" tooltip marker; MouseUp can swap active MAV (ClickSwapMAV). — (not ported)
- [ ] Map zoom-changed (`gMapControl1_OnMapZoomChanged`) syncs zoom widgets; marker enter/leave track `CurrentGMapMarker`. — (not ported)
- [ ] Overlay widgets: `distanceBar1`, `windDir1`, `coords1`, `lbl_hdop` ("hdop: 0"), `lbl_sats` ("Sats: 0"), legend labels "GPS Track (Black)", "Target Heading", "Direct to current WP", "Current Heading", "Disable Joystick" (`but_disablejoystick` → `but_disablejoystick_Click` clears RC override). — (not ported)

### Map right-click context menu (`contextMenuStripMap`)
- [x] "Fly To Here" (`goHereToolStripMenuItem`) — `goHereToolStripMenuItem_Click` → `setGuidedModeWP` at clicked point (prompts alt if unset).
- [ ] "Fly To Here Alt" (`flyToHereAltToolStripMenuItem`) — `flyToHereAltToolStripMenuItem_Click` → AltInputBox sets guided alt/frame. — (not ported)
- [x] "Fly To Coords" (`flyToCoordsToolStripMenuItem`) — `flyToCoordsToolStripMenuItem_Click` → InputBox lat;lng;alt → `setGuidedModeWP`.
- [ ] "Add Poi" (`addPoiToolStripMenuItem`) — `addPoiToolStripMenuItem_Click` → `POI.POIAdd(MouseDownStart)`. Submenu: — (not ported)
  - [ ] "Delete" (`deleteToolStripMenuItem`) — `deleteToolStripMenuItem_Click` → `POI.POIDelete`. — (not ported)
  - [ ] "Save File" (`saveFileToolStripMenuItem`) — `saveFileToolStripMenuItem_Click` → `POI.POISave`. — (not ported)
  - [ ] "Load File" (`loadFileToolStripMenuItem`) — `loadFileToolStripMenuItem_Click` → `POI.POILoad`. — (not ported)
  - [ ] "Coords" (`poiatcoordsToolStripMenuItem`) — `poiatcoordsToolStripMenuItem_Click` → InputBox lat;lng → `POI.POIAdd`. — (not ported)
- [x] "Point Camera Here" (`pointCameraHereToolStripMenuItem`) — `pointCameraHereToolStripMenuItem_Click` → InputBox alt → `DO_SET_ROI` at point.
- [ ] "Point Camera Coords" (`PointCameraCoordsToolStripMenuItem1`) — `PointCameraCoordsToolStripMenuItem1_Click` → InputBox coords → `DO_SET_ROI`. — (not ported)
- [x] "Trigger Camera NOW" (`triggerCameraToolStripMenuItem`) — `triggerCameraToolStripMenuItem_Click` → `setDigicamControl(true)`.
- [ ] "Flight Planner" (`flightPlannerToolStripMenuItem`) — `flightPlannerToolStripMenuItem_Click` → embeds FlightPlanner screen in map panel w/ Close button (`but_Click`). — (not ported)
- [x] "Set Home Here" (`setHomeHereToolStripMenuItem`) — `setHomeHereToolStripMenuItem_Click` → confirm → `DO_SET_HOME` at point + `getHomePositionAsync`. Submenu:
  - [x] "Set EKF Origin Here" (`setEKFHomeHereToolStripMenuItem`) — `setEKFHomeHereToolStripMenuItem_Click` → `SET_GPS_GLOBAL_ORIGIN` packet.
  - [x] "Set Home Here" (`setHomeHereToolStripMenuItem1`) — also `setHomeHereToolStripMenuItem_Click`.
- [x] "TakeOff" (`takeOffToolStripMenuItem`) — `takeOffToolStripMenuItem_Click` → InputBox alt → `setMode("GUIDED")` + `MAV_CMD.TAKEOFF`.
- [ ] "Camera Overlap" (checkable, `onOffCameraOverlapToolStripMenuItem`) — `onOffCameraOverlapToolStripMenuItem_Click` → toggles photo footprint overlay markers. — (not ported)
- [x] "Jump To Tag" (`jumpToTagToolStripMenuItem`) — `jumpToTagToolStripMenuItem_Click` → InputBox tag → `DO_JUMP_TAG`.
- [ ] "Gimbal Video" (`gimbalVideoToolStripMenuItem`) submenu: — (not ported)
  - [ ] "Full Sized" (`gimbalVideoFullSizedToolStripMenuItem`) — `gimbalVideoFullSizedToolStripMenuItem_Click` → gimbal video fills panel, map→mini. — (not ported)
  - [ ] "Mini" (`gimbalVideoMiniToolStripMenuItem`) — `gimbalVideoMiniToolStripMenuItem_Click` → gimbal video→mini, map fills. — (not ported)
  - [ ] "Pop Out" (`gimbalVideoPopOutToolStripMenuItem`) — `gimbalVideoPopOutToolStripMenuItem_Click` → gimbal video in own "Gimbal Control" form. — (not ported)
- [ ] GimbalVideoControl's own video-box right-click menu (built in `gimbalVideoControl` getter): "Mini map" (CheckOnClick toggles minimap), "Swap with map", "Close" (stops+disposes video). — (not ported)

### Timers / live displays
- [ ] `ZedGraphTimer` — `ZedGraphTimer_Tick` → rolls 30s tuning graph X-axis, redraws `zg1` (on while Tuning checked). — (not ported)
- [x] `Messagetabtimer` (200ms) — `Messagetabtimer_Tick` → refreshes Messages tab text (on only while Messages tab active).
- [x] `scriptChecker` — `scriptChecker_Tick` → polls script run state, restores buttons when finished.
- [x] Background `mainloop`/`updateBindingSource` (10Hz) — pushes `CurrentState` to HUD/quickview/status bindings, map vehicle marker, track route, optional auto-pan and map bearing (`setMapBearing`), AVI/MJPEG frame capture.


---

## GCSViews / FlightPlanner (mission/waypoint editor)

Source: `GCSViews/FlightPlanner.cs` (8576 lines), `GCSViews/FlightPlanner.Designer.cs`, `GCSViews/FlightPlanner.resx`.
Layout: `panelBASE` hosts (left→right) `panelAction` (top toolbar via `flowLayoutPanel1`), `panelWaypoints` (the WP grid + alt controls), splitter, `panelMap` (map + zoom). Form events: `FlightPlanner_Load`, `Planner_Resize`, `FlightPlanner_FormClosing`, `timer1.Tick` (1.2 s, redraws live vehicle/POI overlays).

### Top toolbar — `panelAction` / `flowLayoutPanel1` (panels 4,3,2,5,1 in order)

#### panel5 — Read/Write WPs
- [x] `BUT_read` "Read" — `BUT_read_Click` → background `getWPs`/reads mission from vehicle, calls `processToScreen(...)` to populate grid + map.
- [x] `BUT_write` "Write" — `BUT_write_Click` → runs `saveWPs(...)` (progress dialog) to upload full mission to vehicle.
- [ ] `but_writewpfast` "Write Fast" — `but_writewpfast_Click` → runs `saveWPsFast(...)` (MISSION_ITEM_INT block upload, no per-item ack wait). — (not ported) bound to same Write command, no distinct no-ack fast path

#### panel2 — File load/save
- [x] `BUT_loadwpfile` "Load File" — `BUT_loadwpfile_Click` → open-file dialog (.txt/.waypoints), `readQGC110wpfile`/loads into grid.
- [x] `BUT_saveWPFile` "Save File" — `BUT_saveWPFile_Click` → save-file dialog, writes waypoints file.
- [ ] `lbl_wpfile` "..." — label showing current loaded WP filename (display only). — (not ported)

#### panel3 — Map type / overlays
- [x] `comboBoxMapType` (dropdown, tooltip "Change the current map type") — populated in Load; selection switches `MainMap.MapProvider` (Google/Bing/etc).
- [x] `chk_grid` "Grid" — `chk_grid_CheckedChanged` → toggles map tile grid lines overlay.
- [x] `lnk_kml` "View KML" (LinkLabel) — `lnk_kml_LinkClicked` → opens generated KML of current mission in external viewer.
- [ ] `BUT_InjectCustomMap` "Inject Custom Map" — `BUT_InjectCustomMap_Click` → imports custom map tiles (GMapMarkerCustom), drives `progressBarInjectCustomMap`. — (not ported) repurposed as custom tile-URL source, not georef image import
- [ ] `progressBarInjectCustomMap` — progress for the inject operation (display only). — (not ported)
- [x] `lbl_status` "Status" — status text label (display only).

#### panel1 — Home Location
- [ ] `label4` "Home Location" (LinkLabel) — `label4_LinkClicked` → sets home to current map center / opens home set helper. — (not ported) replaced by a Set-from-Vehicle button
- [x] `TXT_homelat` "Lat" (`Label1`) — `TXT_homelat_TextChanged` / `TXT_homelat_Enter` → updates home latitude, redraws home marker.
- [x] `TXT_homelng` "Long" (`label2`) — `TXT_homelng_TextChanged` → updates home longitude.
- [x] `TXT_homealt` "ASL" (`label3`) — `TXT_homealt_TextChanged` → updates home altitude (ASL).

#### panel4 — Coords readout
- [ ] `coords1` (Coords control, vertical) — live cursor lat/lng/alt readout; `coords1_SystemChanged` → switches coordinate system display. — (not ported)

### Waypoint grid panel — `panelWaypoints` ("Waypoints")

#### Alt / option controls
- [x] `TXT_DefaultAlt` "Default Alt" (`LBL_defalutalt`, default 100) — `TXT_DefaultAlt_KeyPress` (digits) / `TXT_DefaultAlt_Leave` → default altitude applied to new WPs.
- [x] `TXT_WPRad` "WP Radius" (`LBL_WPRad`, default 30) — `TXT_WPRad_KeyPress` / `TXT_WPRad_Leave` → waypoint acceptance radius (plane).
- [x] `TXT_loiterrad` "Loiter Radius" (`label5`, default 45) — `TXT_loiterrad_KeyPress` / `TXT_loiterrad_Leave` → loiter radius for loiter cmds.
- [ ] `CHK_verifyheight` "Verify Height" — when checked, alt values verified/adjusted against terrain (srtm) on WP add. — (not ported)
- [ ] `CMB_altmode` "Relative" (dropdown: Relative/Absolute/Terrain) — `CMB_altmode_SelectedIndexChanged` → sets Frame/alt frame for new rows + existing. — (not ported) Frame is per-row only, no global alt-mode dropdown
- [ ] `CHK_splinedefault` "Spline" — `CHK_splinedefault_CheckedChanged` → new map-added WPs become SPLINE_WAYPOINT instead of WAYPOINT. — (not ported)
- [ ] `TXT_altwarn` "Alt Warn" (`label17`, default 0) — altitude warning threshold; rows over it flagged. — (not ported)
- [ ] `chk_usemavftp` "MAVFTP" — `chk_usemavftp_CheckedChanged` → use MAVFTP for mission read/write transfer. — (not ported)
- [ ] `but_mincommands` "˅" — `but_mincommands_Click` → collapse/minimize the commands grid (show/hide extra columns/rows). — (not ported)
- [x] `BUT_Add` "Add Below" (tooltip "Add a line to the grid bellow") — `BUT_Add_Click` → appends a new WP row below current using default alt.

#### Commands DataGridView (`Commands`, MyDataGridView) — columns
- [x] `Command` (ComboBox col, "Command") — dropdown of MAV_CMD names; changing fires `Commands_SelectionChangeCommitted` → `ChangeColumnHeader(cmd)` relabels P1–P4 per command, sets defaults. — (no P1–P4 relabel)
- [x] `Param1`–`Param4` ("P1".."P4", not sortable) — command parameters; headers relabeled per selected command. — (editable, headers not relabeled per command)
- [x] `Lat` ("Lat") / `Lon` ("Lon") — editing fires `Commands_CellEndEdit` → `convertFromGeographic(lat,lng)` updates UTM/MGRS cells + map marker; invalid → error box.
- [x] `Alt` ("Alt") — waypoint altitude (frame per `Frame`).
- [x] `Frame` (ComboBox col, "Frame") — per-row alt frame (Relative/Absolute/Terrain); seeded from `CMB_altmode` in `Commands_DefaultValuesNeeded`.
- [ ] `coordZone`/`coordEasting`/`coordNorthing` ("Zone"/"Easting"/"Northing") — editing fires `Commands_CellEndEdit` → `convertFromUTM(row)` back to lat/lng. — (not ported) display-only read-only cells, no UTM→lat/lng edit
- [ ] `MGRS` ("MGRS") — editing → `convertFromMGRS(row)` back to lat/lng. — (not ported) display-only read-only cell
- [x] `Delete` (button col, "Delete"/"X") — `Commands_CellContentClick` → `updateUndoBuffer`, `Rows.RemoveAt(row)`, `writeKML()`.
- [x] `Up` (image col) — `Commands_CellContentClick` → remove+reinsert row at index-1, `writeKML()`.
- [x] `Down` (image col) — `Commands_CellContentClick` → remove+reinsert row at index+1, `writeKML()`.
- [x] `Grad %`/`Angle`/`Dist`/`AZ` (read-only computed) — gradient %, climb angle, leg distance, azimuth between WPs (filled on redraw).
- [ ] `TagData` (read-only, hidden) — internal per-row tag storage. — (not ported)
- Grid events: `Commands_RowEnter` → `ChangeColumnHeader`; `Commands_RowsAdded` seeds "0"/default cmd; `Commands_RowsRemoved`; `Commands_RowValidating`; `Commands_DefaultValuesNeeded` seeds Frame/Delete="X"/Up/Down images; `Commands_DataError` swallows errors; `Commands_EditingControlShowing` styles combo editor.

### Map left-click / drag / marker behavior (`MainMap`)
- [x] Left-click empty map (no drag) — `MainMap_MouseUp` → `AddWPToMap(lat,lng,0)` adds a new waypoint row at cursor.
- [x] Left-drag map background — `MainMap_MouseMove` pans `MainMap.Position` incrementally. — (native Mapsui pan)
- [x] Drag a WP rect marker — `MainMap_OnMarkerEnter` sets `CurentRectMarker`; drag → `MouseUp` `callMeDrag(tag,lat,lng,-2)` updates that WP's lat/lng.
- [ ] Drag a green "+" midline marker — inserts a WP/fence point between the two adjacent WPs (`InsertCommand`, `updateUndoBuffer`, `ReCalcFence` for fences). — (not ported)
- [ ] Drag a grid handle marker (tag "grid") — moves `drawnpolygon` point, `redrawPolygonSurvey(...)`. — (not ported)
- [ ] Drag a POI marker — `MouseUp` → `POI.POIMove(marker)`. — (not ported)
- [ ] Drag a rally point marker — moves `CurrentRallyPt.Position`. — (not ported)
- [ ] Ctrl + left-drag — draws selection rectangle (`MainMap.SelectedArea`); on release group-selects enclosed WPs (`groupmarkeradd`). — (not ported)
- [ ] Ctrl + click marker / group present — `MainMap_OnMarkerClick` adds marker to `groupmarkers`; later group-drag moves all selected WPs together via `callMeDrag`. — (not ported)
- [ ] Click WP marker — `MainMap_OnMarkerClick` → selects matching grid row (`Commands.CurrentCell`). — (not ported)
- [ ] Hover WP marker — `MainMap_OnMarkerEnter` highlights rect red + selects grid row; `OnMarkerLeave` resets. — (not ported)
- [ ] `MainMap_Paint` — draws distance/lines overlay; `panelMap_Resize` re-lays map. — (not ported) route polyline drawn, no on-map distance labels

### Map zoom controls (`panelMap`)
- [ ] `TRK_zoom` (MyTrackBar, 1–24) — `TRK_zoom_Scroll` → sets `MainMap.Zoom`. — (not ported) native scroll/pinch zoom
- [ ] `Zoomlevel` (NumericUpDown 1–18, tooltip "Change Zoom Level") — `Zoomlevel_ValueChanged` → sets map zoom. — (not ported)
- [ ] `label11` "Zoom", `lbl_distance` "Distance", `lbl_homedist` "Home", `lbl_prevdist` "Prev" — readout labels (total dist / dist-to-home / dist-from-prev). — (not ported)
- [x] `cmb_missiontype` (Mission / Fence / Rally) — `Cmb_missiontype_SelectedIndexChanged` → switches editor between mission, geofence, and rally point editing (changes grid contents + which context items show). — Avalonia: `MissionType` combo swaps one grid over three stores; map render mode + context gating + per-type click-add.
- [ ] On-map `polyicon` button — left-up shows `contextMenuStripPoly`; right-up clears polygon (`clearPolygonToolStripMenuItem_Click`). — (not ported)
- [ ] On-map `zoomicon` button — left-up shows `contextMenuStripZoom`. — (not ported)

### Map right-click context menu (`contextMenuStrip1`) — `contextMenuStrip1_Opening` shows/hides per mission type
- [x] "Delete WP" — `deleteWPToolStripMenuItem_Click` → deletes WP under cursor.
- [x] "Insert Wp" — `insertWpToolStripMenuItem_Click` → inserts a WP at the clicked position.
  - [ ] "At Current Position" — `currentPositionToolStripMenuItem_Click` → inserts WP at vehicle's current GPS position. — (not ported)
- [ ] "Insert Spline WP" — `insertSplineWPToolStripMenuItem_Click` → inserts SPLINE_WAYPOINT. — (not ported)
- [ ] "Loiter" (submenu) —
  - [x] "Forever" — `loiterForeverToolStripMenuItem_Click` → LOITER_UNLIM.
  - [x] "Time" — `loitertimeToolStripMenuItem_Click` → LOITER_TIME (prompts seconds).
  - [x] "Circles" — `loitercirclesToolStripMenuItem_Click` → LOITER_TURNS (prompts turns).
- [ ] "Jump" (submenu) —
  - [ ] "Start" — `jumpstartToolStripMenuItem_Click` → DO_JUMP to start. — (not ported)
  - [x] "WP #" — `jumpwPToolStripMenuItem_Click` → DO_JUMP to a chosen WP index.
- [x] "RTL" — `rTLToolStripMenuItem_Click` → adds RETURN_TO_LAUNCH.
- [x] "Land" — `landToolStripMenuItem_Click` → adds LAND at cursor.
- [x] "Takeoff" — `takeoffToolStripMenuItem_Click` → adds TAKEOFF (prompts alt).
- [x] "DO_SET_ROI" — `setROIToolStripMenuItem_Click` → adds DO_SET_ROI at cursor.
- [x] "Clear Mission" — `clearMissionToolStripMenuItem_Click` → clears all WP rows.
- [ ] (separator)
- [ ] "Polygon" (submenu) —
  - [ ] "Draw a Polygon" — `addPolygonPointToolStripMenuItem_Click` → enter polygon-draw mode (click adds points). — (not ported)
  - [ ] "Clear Polygon" — `clearPolygonToolStripMenuItem_Click`. — (not ported)
  - [ ] "Save Polygon" — `savePolygonToolStripMenuItem_Click` → save .poly file. — (not ported)
  - [ ] "Load Polygon" — `loadPolygonToolStripMenuItem_Click`. — (not ported)
  - [ ] "From SHP" — `fromSHPToolStripMenuItem_Click` → build polygon from shapefile. — (not ported)
  - [ ] "From Current Waypoints" — `fromCurrentWaypointsMenuItem_Click` → polygon from existing WPs. — (not ported)
  - [ ] "Offset Polygon" — `offsetPolygonToolStripMenuItem_Click` → inflate/shrink polygon by distance. — (not ported)
  - [ ] "Area" — `areaToolStripMenuItem_Click` → reports enclosed area. — (not ported)
- [ ] "Geo-Fence" (submenu) — (Avalonia: Fence edit via the Mission Type selector, not this submenu)
  - [x] "Upload" — `GeoFenceuploadToolStripMenuItem_Click` → upload fence to vehicle. — Avalonia: Write button in Fence mode (`mav_mission.upload`).
  - [x] "Download" — `GeoFencedownloadToolStripMenuItem_Click`. — Avalonia: Read button in Fence mode (`mav_mission.download`, legacy fallback).
  - [ ] "Set Return Location" — `setReturnLocationToolStripMenuItem_Click`. — (not ported)
  - [ ] "Load from File" — `loadFromFileToolStripMenuItem_Click`. — (not ported)
  - [ ] "Save to File" — `saveToFileToolStripMenuItem_Click`. — (not ported)
  - [x] "Clear" — `clearToolStripMenuItem_Click`. — Avalonia: Clear context item clears the active (fence) store.
- [ ] "Rally Points" (submenu) — (Avalonia: Rally edit via the Mission Type selector, not this submenu)
  - [x] "Set Rally Point" — `setRallyPointToolStripMenuItem_Click` → add rally point at cursor. — Avalonia: click-to-add in Rally mode (RALLY_POINT).
  - [x] "Download" — `getRallyPointsToolStripMenuItem_Click`. — Avalonia: Read in Rally mode.
  - [x] "Upload" — `saveRallyPointsToolStripMenuItem_Click`. — Avalonia: Write in Rally mode.
  - [x] "Clear Rally Points" — `clearRallyPointsToolStripMenuItem_Click`. — Avalonia: Clear context item.
  - [ ] "Save Rally to File" — `saveToFileToolStripMenuItem1_Click`. — (not ported)
  - [ ] "Load Rally from File" — `loadFromFileToolStripMenuItem1_Click`. — (not ported)
- [ ] "Auto WP" (submenu) —
  - [ ] "Create Wp Circle" — `createWpCircleToolStripMenuItem_Click` → ring of WPs (prompts radius/count). — (not ported)
  - [ ] "Create Spline Circle" — `createSplineCircleToolStripMenuItem_Click`. — (not ported)
  - [ ] "Area" — `areaToolStripMenuItem_Click`. — (not ported)
  - [ ] "Text" — `textToolStripMenuItem_Click` → render text as WPs. — (not ported)
  - [ ] "Create Circle Survey" — `createCircleSurveyToolStripMenuItem_Click`. — (not ported)
  - [x] "Survey (Grid)" — `surveyGridToolStripMenuItem_Click` → opens Grid survey planner over polygon. — (relocated to a side-panel button)
- [ ] "Map Tool" (submenu) —
  - [ ] "Measure Distance" — `ContextMeasure_Click` → click-to-click distance/area measure. — (not ported)
  - [ ] "Rotate Map" — `rotateMapToolStripMenuItem_Click` → set map bearing. — (not ported)
  - [ ] "Zoom To" — `zoomToToolStripMenuItem_Click` → enter lat/lng to center. — (not ported)
  - [ ] "Prefetch" — `prefetchToolStripMenuItem_Click` → cache tiles for selected area. — (not ported)
  - [ ] "Prefetch WP Path" — `prefetchWPPathToolStripMenuItem_Click` → cache tiles along WP route. — (not ported)
  - [x] "KML Overlay" — `kMLOverlayToolStripMenuItem_Click` → load KML/KMZ overlay.
  - [ ] "Elevation Graph" — `elevationGraphToolStripMenuItem_Click` → terrain profile of mission. — (not ported)
  - [x] "Reverse WPs" — `reverseWPsToolStripMenuItem_Click` → reverse WP order.
  - [ ] "GDAL Opacity" — `gDALOpacityToolStripMenuItem_Click` → set GDAL layer opacity. — (not ported)
- [ ] "File Load/Save" (submenu) —
  - [x] "Load WP File" — `loadWPFileToolStripMenuItem_Click`.
  - [ ] "Load and Append" — `loadAndAppendToolStripMenuItem_Click` → append file WPs to current. — (not ported)
  - [x] "Save WP File" — `saveWPFileToolStripMenuItem_Click`.
  - [x] "Load KML File" — `loadKMLFileToolStripMenuItem_Click`.
  - [ ] "Load SHP File" — `loadSHPFileToolStripMenuItem_Click`. — (not ported)
- [ ] "POI" (submenu) —
  - [ ] "Add" — `poiaddToolStripMenuItem_Click`. — (not ported)
  - [ ] "Delete" — `poideleteToolStripMenuItem_Click`. — (not ported)
  - [ ] "Edit" — `poieditToolStripMenuItem_Click`. — (not ported)
- [ ] "Tracker Home" — `trackerHomeToolStripMenuItem_Click` → set antenna-tracker home here. — (not ported)
- [x] "Modify Alt" — `modifyAltToolStripMenuItem_Click` → bulk-modify all WP altitudes.
- [ ] "Enter UTM Coord" — `enterUTMCoordToolStripMenuItem_Click` → add WP via UTM entry. — (not ported)
- [ ] "Switch Docking" — `switchDockingToolStripMenuItem_Click` → toggle map/grid dock layout. — (not ported)
- [ ] "Set Home Here" — `setHomeHereToolStripMenuItem_Click` → set home to clicked location. — (not ported)

### Polygon context menu (`contextMenuStripPoly`, via on-map poly button) — `ContextMenuStripPoly_Opening`
- [ ] "Draw a Polygon" — `addPolygonPointToolStripMenuItem_Click`. — (not ported)
- [ ] "Clear Polygon" — `clearPolygonToolStripMenuItem_Click`. — (not ported)
- [ ] "Save Polygon" — `savePolygonToolStripMenuItem_Click`. — (not ported)
- [ ] "Load Polygon" — `loadPolygonToolStripMenuItem_Click`. — (not ported)
- [ ] "From SHP" — `fromSHPToolStripMenuItem_Click`. — (not ported)
- [ ] "From Current Waypoints" — `fromCurrentWaypointsMenuItem_Click`. — (not ported)
- [ ] "Offset Polygon" — `offsetPolygonToolStripMenuItem_Click`. — (not ported)
- [ ] "Area" — `areaToolStripMenuItem_Click`. — (not ported)
- [ ] "Fence Inclusion" — `FenceInclusionToolStripMenuItem_Click` → make polygon an inclusion fence. — (not ported)
- [ ] "Fence Exclusion" — `FenceExclusionToolStripMenuItem_Click` → make polygon an exclusion fence. — (not ported)

### Zoom context menu (`contextMenuStripZoom`, via on-map zoom button)
- [ ] "Zoom to Vehicle" — `zoomToVehicleToolStripMenuItem_Click`. — (not ported)
- [ ] "Zoom to Mission" — `zoomToMissionToolStripMenuItem_Click`. — (not ported)
- [ ] "Zoom to Home" — `zoomToHomeToolStripMenuItem_Click`. — (not ported)

### Notes for porting
- Most edits call `writeKML()` to redraw the map route + recompute Grad/Angle/Dist/AZ columns.
- Undo is via `updateUndoBuffer(true)` before mutating ops (delete/move/insert).
- `quickadd` flag suppresses per-row redraw during bulk grid changes.
- Mission/Fence/Rally mode (`cmb_missiontype`) gates which context-menu items are enabled (handled in `contextMenuStrip1_Opening`).
- Hidden/unused designer items present but not wired into menus: `drawAPolygonToolStripMenuItem`, `toolStripMenuItem3` ("1"), `testToolStripMenuItem` ("test").


---

## GCSViews / Help · Simulation(SITL) · Setup & Config containers

Source: `external/MissionPlanner/GCSViews/{Help,SITL,SoftwareConfig,InitialSetup}.cs` (+ `.Designer.cs`/`.resx`) and `GCSViews/ConfigurationView/Config*.cs`.
ConfigurationView control internals are already deep-documented in `docs/portspec/config.md`, `setup-mandatory.md`, `setup-optional.md` — below only the nav-tree wiring + a one-line page index.

---

### HELP tab (`Help.cs` / `Help.Designer.cs` / `Help.resx`)

A `MyUserControl` (`IActivate`). Layout = one big read-only `RichTextBox` (help text) + 3 buttons/links + 1 checkbox. On `Help_Load`: `richTextBox1.Rtf = Resources.help_text;` then `ThemeManager.ApplyThemeTo`. `DetectUrls=false`, cursor=Default (the RTF is static help content shipped in resources).
On `Activate()`: loads `CHK_showconsole` from `Settings["showconsole"]`; if `Program.WindowsStoreApp` hides both update buttons (store builds self-update).

- [ ] **`richTextBox1`** (read-only RTF pane) — displays `Resources.help_text` (bundled changelog/help blurb); themed; URL auto-detect disabled. — (not ported)
- [x] **`BUT_updatecheck` "Check for Updates"** → `BUT_updatecheck_Click` → guards `WindowsStoreApp` (no-op), else `Utilities.Update.CheckForUpdate(true)` (interactive update check; `true` = show UI/prompt). Hidden on store builds.
- [x] **`BUT_betaupdate` "Check for BETA Updates"** → `BUT_betaupdate_Click` → sets `Utilities.Update.dobeta = true`; if **Ctrl held** also sets `Update.domaster = true` and shows "This will update to MASTER release"; then `Utilities.Update.DoUpdate()`. Hidden on store builds.
- [ ] **`linkLabel1` "Change Log"** (LinkLabel) → `linkLabel1_LinkClicked` → `Process.Start("https://firmware.ardupilot.org/Tools/MissionPlanner/upgrade/ChangeLog.txt")` (opens firmware-history/changelog URL in browser). — (not ported)
- [ ] **`CHK_showconsole` "Show Console Window (restart)"** → `CHK_showconsole_CheckedChanged` → writes `Settings["showconsole"]` (bool, applied on next restart — toggles the debug console window). — (not ported)

> Note: this is the lean modern Help tab. There are **no** forum/screenshot/wiki buttons in the current control (those live elsewhere in the app menu, not here). On-screen text strings above are verbatim from `Help.resx`.

---

### SIMULATION / SITL tab (`SITL.cs` / `SITL.Designer.cs` / `SITL.resx`)

A `MyUserControl` (`IActivate`) that downloads a prebuilt ArduPilot SITL binary and launches it, then auto-connects MP to it over TCP `127.0.0.1:5760` (RC override out via UDP `5501`). Layout: groupBox1 = map (`myGMAP1`), groupBox2 = vehicle picture buttons, groupBox3 = heading + version, groupBox4 = model/speed/cmdline/wipe + swarm buttons.

**Version selector** `cmb_version` (built in ctor) → maps to `APFirmware.RELEASE_TYPES`:
- [x] "Latest (Dev)" = DEV (master) · "Beta" = BETA · "Stable" = OFFICIAL · "Skip Download" = null (use already-downloaded exe). Persisted to `Settings["sitl_download_version"]`.

**Map (`myGMAP1`)** — sets the home/spawn location. `Activate()` seeds home from `cs.PlannedHomeLocation` (or Canberra `-35.3633515,149.1652412` if zero). Draggable "H" marker (`homemarker`); drag marker = move home, drag map = pan. Home lat/lng + SRTM altitude + heading feed `BuildHomeLocation`.

**Vehicle picture buttons** (`PictureBoxMouseOver`, groupBox2) — each downloads its `.elf` via `CheckandGetSITLImage()` then `StartSITL(model, home, "", simspeed)`:
- [x] **Plane** (`pictureBoxplane`, label6 "Plane", Tag `plane`) → `ArduPlane.elf`, model `plane`.
- [x] **Multirotor** (`pictureBoxquad`, label4 "Multirotor", Tag `copter`) → `ArduCopter.elf`, model `+` (plus-quad).
- [x] **Rover** (`pictureBoxrover`, label5 "Rover", Tag `rover`) → `ArduRover.elf`, model `rover`.
- [x] **Helicopter** (`pictureBoxheli`, label3 "Helicopter", Tag `heli`) → `ArduHeli.elf`, model `heli`.
- [x] Each guards "no home marker" → `Strings.Invalid_home_location`; download failure → CustomMessageBox.

**groupBox3 (spawn params)**:
- [x] **`NUM_heading`** (label1 "Heading", 0–360) — initial yaw, appended to home location string.
- [x] **`cmb_version`** — release-type selector (see above).

**groupBox4 (model + run options)**:
- [x] **`cmb_model`** (label7 "Model") — optional override of the frame model string; 34 items: `quadplane, xplane, xplane-heli, firefly, +, quad, copter, x, hexa, octa, tri, y6, heli, heli-dual, heli-compound, singlecopter, coaxcopter, rover, crrcsim, jsbsim, flightaxis, gazebo, last_letter, tracker, balloon, plane, calibration, plane-jet, sailboat, motorboat, morse-rover, rover-skid, plane-3d`. If non-empty, overrides the button's default model (`StartSITL` line `if (cmb_model.Text != "") model = cmb_model.Text;`).
- [x] **`num_simspeed`** (label2 "Sim Speed", min 1) — `-s<speedup>` time-acceleration factor.
- [x] **`txt_cmdline`** (label8 "Extra command line") — extra raw args appended to the SITL exe command.
- [x] **`chk_wipe` "Wipe"** — appends `--wipe` (reset EEPROM/params on start).

**Swarm buttons** (groupBox4) — Ctrl+S also triggers chain; Ctrl+D triggers separate-copter:
- [ ] **"Copter Swarm - Single link"** (`but_swarmseq`) → `StartSwarmChain()` — N copters, daisy-chained MAVLink (serial2 tcpclient to previous), single GCS link; also writes `sitl.bat`/`sitl1.sh`. Prompts "how many?". — (deferred)
- [ ] **"Copter Swarm - Multilink"** (`but_swarmlink`) → `StartSwarmSeperate(ArduCopter2)` — N copters, each its own TCP link (`5760 + 10*i`), each added to `MainV2.Comports`. — (deferred)
- [ ] **"Plane Swarm - Multilink"** (`but_swarmplane`) → `StartSwarmSeperate(ArduPlane)`. — (deferred)
- [ ] **"Rover Swarm - Multilink"** (`but_swarmrover`) → `StartSwarmSeperate(ArduRover)`. — (deferred)
- [ ] Swarm writes per-instance `identity.parm` (SYSID_THISMAV, SIM_RATE_HZ=400, terrain off, etc.), spaced 4 m apart along heading. — (deferred)

**Download/launch internals** (for fidelity):
- [x] `CheckandGetSITLImage(filename)` resolution order: (1) `BundledPath` (local bundled binary, tries `{name}`,`.exe`,`lib*.so`,`*.so`,`*.elf`); (2) on x86/x64 Linux → `APFirmware.GetOptions` platform `SITL_x86_64_linux_gnu`; (3) on ARM → `SITL_arm_linux_gnueabihf` (both chmod 0755); (4) else Windows → download `<release-url>/<exe>` + 10 cygwin DLLs (cygwin1.dll etc.) into the sitl dir with a LoadingBox. URLs: master `…/sitl/`, beta `…/sitl/Beta/`, stable per-vehicle `…/sitl/{Copter,Plane,Rover}Stable/`. — (partial; no BundledPath)
- [ ] `GetDefaultConfig(model)` — downloads ArduPilot `sim_vehicle.py` (regex-parse `default_params_filename`) or fallback `vehicleinfo.py`, fetches the matching `*.parm` defaults, appended as `--defaults`. — (not ported)
- [x] `StartSITL` final cmd: `-M{model} -O{home} -s{speedup} --serial0 tcp:0 {extraargs}`, working dir = `<userdata>/sitl/<model>/`, `HOME` env set there; after 2 s switches MP to FlightData screen and `doConnect` over `127.0.0.1:5760`, opens UDP RC-send on `5501` (`rcinput()` packs 8 ch RC override).

---

### CONFIG container (`SoftwareConfig.cs` / `SoftwareConfig.Designer.cs`)

`MyUserControl` hosting a single `backstageView` (left nav tree + right page host). `SoftwareConfig_Load` builds pages top-to-bottom; each gated by `MainV2.DisplayConfiguration.display*` flags **and** connection/firmware/param state. Helper props: `isConnected, isTracker, isCopter, isCopter35plus, isHeli, isQuadPlane, isPlane, isRover, gotAllParams`. Remembers last page via static `lastpagename`; `pluginViewPages` appended at end (filtered by `pageOptions` flags). `start` = default-activated page.

Nav-tree page order (only added when its `display*` flag + condition pass):
- [x] **GeoFence** → `ConfigAC_Fence` (Copter only, `displayGeoFence`).
- [x] **Basic Tuning** → `ConfigSimplePids` (Copter) / `ConfigArduplane` (Plane) / `ConfigArdurover` (Rover) — sets `start`.
- [x] **Extended Tuning** → `ConfigArducopter` (Copter; Plane adds it as "QP Extended Tuning"); Tracker → `ConfigAntennaTracker` as Extended Tuning.
- [x] **Standard Params** → `ConfigFriendlyParams` (`displayStandardParams`).
- [x] **Advanced Params** → `ConfigFriendlyParamsAdv` (advanced flag, `displayAdvancedParams`).
- [x] **Onboard OSD** → `ConfigOSD` (non-MONO, applicable, `displayOSD`).
- [x] **MAVFtp** → `MavFTPUI` (if vehicle FTP capability, `displayMavFTP`).
- [x] **User Params** → `ConfigUserDefined` (`displayUserParam`).
- [x] **Full Parameter List** → `ConfigRawParams` (`displayFullParamList`; shown disconnected too).
- [ ] **(Ateryx only)** FlightModes → `ConfigFlightModes`, "Ateryx Zero Sensors" → `ConfigAteryxSensors`, "Ateryx Pids" → `ConfigAteryx`. — (not ported)
- [ ] **Loading** → `ConfigParamLoading` (shown while `!gotAllParams`, connected). — (unverified; VM+View exist but not registered in nav tree)
- [x] **Planner** → `ConfigPlanner` (`displayPlannerSettings`; default page when disconnected).

### SETUP container (`InitialSetup.cs` / `InitialSetup.Designer.cs`)

Same `backstageView` pattern (`HardwareConfig_Load`). Pages added with explicit `enabled` flag (disabled = greyed/hidden) and a `Parent` to nest under section headers. Two parent group pages: **`mand` = ConfigMandatory** ("Mandatory Hardware") and **`opt` = ConfigOptional** ("Optional Hardware"), plus **`adv` = ConfigAdvanced** ("Advanced", only if `isAdvancedMode`). Resource header strings via `ResourceManager` (`backstageViewPage*.Text`).

Top-level / firmware:
- [ ] **Loading** → `ConfigParamLoading` (while `!gotAllParams`, connected). — (unverified; VM+View exist but not registered in nav tree)
- [x] **Install Firmware** (`displayInstallFirmware`) → `ConfigFirmwareDisabled` (connected), `ConfigFirmwareManifest` (disconnected, the modern picker), `ConfigFirmware` "… Legacy" (disconnected).
- [x] **Secure** → `ConfigSecureAP` (disconnected).

Mandatory Hardware (parent `mand` = `ConfigMandatory`, enabled when connected+gotAllParams):
- [x] **Frame Type** (`displayFrameType`) → `ConfigTradHeli4` (heli) / `ConfigFrameType` (copter <3.5) / `ConfigFrameClassType` (copter 3.5+ or has FRAME_CLASS).
- [x] **Accel Calibration** → `ConfigAccelerometerCalibration` (`displayAccelCalibration`).
- [x] **Compass** → `ConfigHWCompass2` (if COMPASS_PRIO1_ID present) else `ConfigHWCompass` (`displayCompassConfiguration`).
- [x] **Radio Calibration** → `ConfigRadioInput` (`displayRadioCalibration`).
- [x] **Servo Output** → `ConfigRadioOutput` (`displayServoOutput`).
- [x] **Serial** → `ConfigSerial` (`displaySerialPorts`).
- [x] **ESC Calibration** → `ConfigESCCalibration` (`displayEscCalibration`).
- [x] **Flight Modes** → `ConfigFlightModes` (`displayFlightModes`).
- [x] **Failsafe** → `ConfigFailSafe` (`displayFailSafe`).
- [x] **Initial Tune Params** → `ConfigInitialParams` (copter/quadplane, `displayInitialParams`).
- [x] **HW ID** → `ConfigHWIDs` (`displayHWIDs`).
- [x] **ADSB** → `ConfigADSB` (`displayADSB`, added under mand).

Optional Hardware (parent `opt` = `ConfigOptional`):
- [x] **RTK/GPS Inject** → `ConfigSerialInjectGPS` (`displayRTKInject`).
- [x] **CubeID Update** → `ConfigCubeID` (connected).
- [x] **Sik Radio** → `Sikradio` (`displaySikRadio`).
- [x] **CAN GPS Order** → `ConfigGPSOrder` (`displayGPSOrder`).
- [x] **Battery Monitor** → `ConfigBatteryMonitoring` + **Battery2** → `ConfigBatteryMonitoring2` (`displayBattMonitor`).
- [x] **DroneCAN/UAVCAN** → `ConfigDroneCAN` (`displayCAN`).
- [x] **Joystick** → `Joystick.JoystickSetup` (`displayJoystick`).
- [x] **Compass/Motor Calib** → `ConfigCompassMot` (`displayCompassMotorCalib`).
- [x] **RangeFinder** → `ConfigHWRangeFinder` (`displayRangeFinder`).
- [x] **Airspeed** → `ConfigHWAirspeed` (`displayAirSpeed`).
- [x] **PX4Flow** → `ConfigHWPX4Flow` (`displayPx4Flow`).
- [x] **OptFlow** → `ConfigHWOptFlow` (`displayOpticalFlow`).
- [x] **OSD** → `ConfigHWOSD` (`displayOsd`).
- [x] **Gimbal/Camera** → `ConfigMount` (`displayCameraGimbal`).
- [x] **Antenna Tracker (page)** → `ConfigAntennaTracker` (tracker) + **Antenna Tracker (UI)** → `Antenna.TrackerUI` (`displayAntennaTracker`).
- [x] **Motor Test** → `ConfigMotorTest` (`displayMotorTest`).
- [x] **Bluetooth** → `ConfigHWBT` (`displayBluetooth`).
- [x] **Parachute** → `ConfigHWParachute` (`displayParachute`).
- [x] **ESP8266** → `ConfigHWESP8266` (`displayEsp`).
- [x] **FFT Setup** → `ConfigFFT` (`displayFFTSetup`).

Advanced (parent `adv` = `ConfigAdvanced`, only if `isAdvancedMode`):
- [x] **Terminal** → `ConfigTerminal` (`displayTerminal`).
- [x] **Script REPL** → `ConfigREPL` (connected, `displayREPL`).

---

### ConfigurationView page index — one line per `Config*.cs` (62 files; details in `docs/portspec/{config,setup-mandatory,setup-optional}.md`)

| Page (`*.cs`) | Purpose |
|---|---|
| `ConfigAC_Fence` | Copter geofence enable/type/radius/altitude/action. |
| `ConfigAccelerometerCalibration` | 6-position accel cal + level + simple accel cal wizard. |
| `ConfigADSB` | ADS-B receiver/avoidance setup. |
| `ConfigAdvanced` | Container/header for the Advanced section (Terminal, REPL); misc advanced actions. |
| `ConfigAntennaTracker` | Antenna-tracker tuning (pitch/yaw PIDs, ranges). |
| `ConfigArducopter` | Copter Extended Tuning (full PID grid, AC_PID columns). |
| `ConfigArduplane` | Plane Basic Tuning (roll/pitch/nav/throttle params). |
| `ConfigArdurover` | Rover Basic Tuning (steering/throttle/nav params). |
| `ConfigAteryx` | Ateryx airframe PID tuning (legacy). |
| `ConfigAteryxSensors` | Ateryx "Zero Sensors" calibration (legacy). |
| `ConfigBatteryMonitoring` | Primary battery monitor: sensor/type, capacity, voltage/current pin & multipliers. |
| `ConfigBatteryMonitoring2` | Secondary (battery 2) monitor, same fields. |
| `ConfigCompassMot` | Compassmot — compass interference from motors/throttle/current. |
| `ConfigCubeID` | Cube ID firmware/secure-boot update utility. |
| `ConfigDroneCAN` | DroneCAN/UAVCAN node inspector & parameter/firmware tool. |
| `ConfigESCCalibration` | ESC throttle endpoint calibration. |
| `ConfigFailSafe` | Radio/battery/GCS failsafe thresholds & actions; live RC bars. |
| `ConfigFFT` | In-flight FFT / harmonic-notch setup. |
| `ConfigFirmware` | Legacy firmware install (hard-coded board list / manual hex). |
| `ConfigFirmwareDisabled` | Placeholder shown when connected (firmware install disabled while connected). |
| `ConfigFirmwareManifest` | Modern firmware installer (manifest-driven board/vehicle picker, custom build). |
| `ConfigFlightModes` | Map 6 RC mode-switch positions → flight modes (+ simple/super-simple). |
| `ConfigFrameClassType` | Copter 3.5+ frame class + type selector (X/Plus/etc.). |
| `ConfigFrameType` | Legacy copter frame type selector (<3.5). |
| `ConfigFriendlyParams` | Standard Params friendly editor (curated metadata-driven list). |
| `ConfigFriendlyParamsAdv` | Advanced Params friendly editor (full curated list). |
| `ConfigGPSOrder` | CAN GPS ordering / DroneCAN GPS index assignment. |
| `ConfigHWAirspeed` | Airspeed sensor enable/type/pin/ratio. |
| `ConfigHWBT` | Bluetooth module config. |
| `ConfigHWCAN` | Legacy generic CAN config (largely superseded by DroneCAN). |
| `ConfigHWCompass` | Legacy compass setup (orientation, enable, declination, live cal). |
| `ConfigHWCompass2` | Modern multi-compass setup (PRIO IDs, per-compass cal/use). |
| `ConfigHWesp8266` | ESP8266 wifi telemetry module config. |
| `ConfigHWIDs` | Hardware/sensor ID inspector (INS/compass/baro device IDs). |
| `ConfigHWOptFlow` | Optical-flow sensor setup/calibration. |
| `ConfigHWOSD` | MinimOSD / onboard OSD panel layout config. |
| `ConfigHWParachute` | Parachute enable/release params. |
| `ConfigHWPX4Flow` | PX4Flow optical-flow + sonar setup/test. |
| `ConfigHWRangeFinder` | Rangefinder/sonar enable, type, pin, scaling, min/max. |
| `ConfigInitialParams` | Initial tuning param presets by vehicle weight/prop size (copter/QP). |
| `ConfigMandatory` | Header/landing page for the Mandatory Hardware section. |
| `ConfigMotorTest` | Per-motor test (spin order/throttle %, duration). |
| `ConfigMount` | Camera gimbal/mount channel, angle limits, stabilize, retract. |
| `ConfigOptional` | Header/landing page for the Optional Hardware section. |
| `ConfigOSD` | Onboard (HUD) OSD parameter screens editor. |
| `ConfigParamLoading` | "Loading parameters…" progress placeholder page. |
| `ConfigPlanner` | Mission Planner app settings (units, layout, speech, telemetry rates). |
| `ConfigPlannerAdv` | Advanced planner app settings. |
| `ConfigRadioInput` | RC calibration (move sticks to set min/max, reverse, live bars). |
| `ConfigRadioOutput` | Servo/output channel viewer & test (SERVOn functions). |
| `ConfigRawParams` | Full Parameter List grid — search/filter/load/save/compare .param, write/refresh/fav (already DONE in port). |
| `ConfigREPL` | Embedded scripting REPL console. |
| `ConfigSecure` | Secure-boot/signing config. |
| `ConfigSecureAP` | Secure AP (signed firmware / key provisioning) page. |
| `ConfigSerial` | Serial port protocol/baud assignment (SERIALn_PROTOCOL/BAUD). |
| `ConfigSerialInjectGPS` | RTK/GPS RTCM inject (NTRIP/serial base, inject to vehicle). |
| `ConfigSimplePids` | Copter Basic Tuning (simplified roll/pitch/throttle/altitude sliders). |
| `ConfigTerminal` | Serial terminal / CLI console. |
| `ConfigTradHeli` | Legacy traditional-heli swashplate/servo setup. |
| `ConfigTradHeli4` | Modern traditional-heli (H_*) swashplate/servo setup. |
| `ConfigUserDefined` | User-defined params page (custom curated param subset). |

> `ConfigMount.designer.cs` is the only Config page with a separate WinForms designer split; the rest pair with `*.Designer.cs` of the same base name.


---

## Log Review (LogBrowse · MavlinkLog · LogAnalyzer · graph presets)

Source: `Log/LogBrowse.cs` (+`.designer.cs`,`.resx`), `Log/MavlinkLog.cs` (+`MavlinkLogBase.cs`), `Log/LogDownload*.cs`, `Log/LogIndex.cs`, `Utilities/LogAnalyzer.cs`, `Controls/LogAnalyzer.cs`, `LogAnalyzer/py2exe/tests/`, `graphs/*.xml`. Entry points are the FlightData bottom tabs **DataFlash Logs** (`tablogbrowse`) and **Telemetry Logs** (`tabTLogs`).

### Entry points — FlightData "DataFlash Logs" / "Telemetry Logs" tabs
- [x] `BUT_logbrowse` "Review a Log" — opens `LogBrowse` form (DataFlash .bin/.log review screen).
- [ ] `BUT_loganalysis` "Auto Analysis" — file picker (`*.log;*.bin`), auto-converts .bin→.log, runs `LogAnalyzer.CheckLogFile` then shows `Controls.LogAnalyzer` results window. — (not ported)
- [ ] `BUT_DFMavlink` "Download DataFlash Log Via Mavlink" — opens `LogDownloadMavLink` form (download logs over MAVLink). — (not ported)
- [x] `but_bintolog` "Convert .Bin to .Log" — multi-select .bin → `BinaryLog.ConvertBin` to .log alongside source.
- [x] `but_dflogtokml` "Create KML + gpx" — multi-select .log/.bin → `LogOutput.writeKML` (writes `<file>.kml`).
- [x] `BUT_loadtelem` "Load Log" — opens a .tlog/.mavlog for in-window telemetry replay (sets `logplaybackfile`).
- [x] `BUT_playlog` "Play/Pause" + speed slider — telemetry log playback transport on the map.
- [ ] `BUT_log2kml` "Tlog > Kml or Graph" — opens `MavlinkLog` form (tlog graph + conversion/export hub). — (not ported)
- [ ] `BUT_matlab` — `MatLabForms.ProcessLog()` exports tlog to a MATLAB .mat file. — (not ported)

### LogBrowse — opening a log
- [x] `BUT_loadlog` "Load A Log" — file dialog filter `*.log;*.bin;*.BIN;*.LOG`; remembers last dir; `LoadLog()` parses via `DFLogBuffer`/`DFLog`.
- [x] Load is async with `Loading.ShowLoading("Scanning File")`; scans column widths, then `LoadLog2` builds chart, tree, datagrid.
- [ ] Title bar set to `Log Browser - <file> - <vehicle version>` (vehicle sniffed from MSG lines: ArduCopter/Plane/Sub/Blimp/AntennaTracker). — (not ported)
- [ ] Warns + closes if FMT records missing (`WarningLogBrowseFMTMissing`). — (not ported)
- [x] Preset list loaded from `mavgraph.readmavgraphsxml()`, sorted, bound to `CMB_preselect`.

### LogBrowse — layout (3 split containers)
- [ ] `splitContainerAllTree`: Panel1 = graph/grid area, Panel2 = message-type `treeView1` (top) + `txt_info` field-description box (bottom). — (not ported)
- [ ] `splitContainerZgGrid`: Panel1 = graph+map, Panel2 = buttons+datagrid. — (not ported)
- [ ] `splitContainerZgMap`: Panel1 = `zg1` ZedGraph plot, Panel2 = `myGMAP1` map + 4 legend labels (label1 GPS=blue, label2 GPS2=green, label3 POS=red, label4 GPSB=yellow). — (not ported)
- [ ] `splitContainerButGrid`: Panel1 = button/checkbox strip, Panel2 = `dataGridView1` data table. — (not ported)

### LogBrowse — left tree (message types)
- [ ] `treeView1` with checkboxes: 3 levels — MsgType → instance → field (4 levels for bitmask fields); checking a field adds its curve, unchecking removes matching curve(s). — (not ported)
- [ ] Left-click check = left Y axis; right-click (`treeView1_MouseDown` sets `wasrightclick`) toggles check and plots on right Y axis. — (not ported)
- [ ] Node hover (`NodeMouseHover`) shows field unit/description tooltip; `txt_info` shows the field description. — (not ported)
- [ ] Double-click a field node (`treeView1_DoubleClick`) — InputBox to apply a per-field **scaler & offset** (`DataModifer`, modifiers `x + - /`, e.g. `/100 +50`); stored in `dataModifierHash`. — (not ported)
- [ ] `treeView1_DrawNode` owner-draws coloured node text matching curve colours. — (not ported)

### LogBrowse — button / checkbox strip
- [x] `BUT_Graphit` "Graph Left" — graph the currently selected datagrid cell's column on the left axis (`graphit_clickprocess(true)`).
- [x] `BUT_Graphit_R` "Graph Right" — same on the right axis.
- [x] `BUT_cleargraph` "Clear Graph" — removes all curves and unticks the tree.
- [x] `BUT_removeitem` "Remove Item" — removes a single selected curve.
- [x] `chk_datagrid` "Data Table" (persisted `LB_Grid`) — show/hide the bottom data grid.
- [ ] `chk_time` "Time" (persisted `LB_Time`, default on) — X axis = Date/time (`HH:mm:ss.fff`) vs Line Number. — (not ported)
- [ ] `CHK_map` "Map" (persisted `LB_Map`) — show/hide the GPS track map panel. — (not ported)
- [x] `chk_mode` "Mode" (`LB_Mode`) — overlay flight-mode change vertical lines/labels.
- [x] `chk_errors` "Errors" (`LB_Error`) — overlay ERR events.
- [x] `chk_events` "Events" — overlay EV events.
- [ ] `chk_msg` "MSG" (`LB_MSG`) — overlay MSG text events. — (not ported)
- [ ] `chk_params` "Show Params" — overlay parameter-change markers. — (not ported)
- [x] `CMB_preselect` — preset graph dropdown (see graph presets); selecting clears graph and plots all expressions in the chosen preset group (left/right axis per item).

### LogBrowse — graph plot (zg1, ZedGraph)
- [x] Synchronized Y axes; zoom via `zg1_ZoomEvent`; mouse-move tooltip/point values via default ZedGraph behaviour.
- [ ] Right-click context menu = **default ZedGraph menu** (Copy, Save Image As, Page Setup, Print, Show Point Values, Un-Zoom/Undo All Zoom/Pan, Set Scale to Default) — the custom `Zg1_ContextMenuBuilder` Properties items are commented out (no custom plot context items). — (not ported)
- [ ] Double-click the plot (`zg1_MouseDoubleClick`) — maps X (time/line) back to a log sample and `GoToSample` (scrolls datagrid + moves map marker to that line). — (not ported)
- [ ] Expressions support derived math (`mag_field`, `earth_accel_df`, `gps_velocity_df`, `delta`, `distance_from`, etc.) parsed from preset/typed expressions. — (not ported)

### LogBrowse — GPS track map (myGMAP1)
- [ ] Draws GPS/GPS2/POS/GPSB routes (colour-coded per legend labels); markers enabled, drag/zoom map. — (not ported)
- [ ] `myGMAP1_OnRouteClick` — clicking the track finds nearest log point and jumps the graph/datagrid to that sample. — (not ported)
- [ ] Map mouse down/move/up handlers for marker drag + position pickup. — (not ported)

### LogBrowse — data grid (dataGridView1)
- [ ] Virtual grid (`CellValueNeeded`), read-only, single cell-select; columns = line/time + per-message fields; `contextMenuStrip1` attached. — (not ported)
- [ ] Double-click a row (`dataGridView1_CellDoubleClick`) — `GoToSample` jumps map marker + graph cursor to that line. — (not ported)
- [ ] `RowEnter` updates selection; `ColumnHeaderMouseClick` for column ops; derived param info via `get_extra_info` (e.g. SERVOn_FUNCTION names for RCOU/RCIN, GPS/MAG/BARO annotations). — (not ported)
- [x] Context menu **"Export Visible"** (`exportVisibleToolStripMenuItem`) — writes currently visible grid rows to `output.csv` (SaveFileDialog).
- [ ] Context menu **"Export Files"** (`exportFilesToolStripMenuItem`) — extracts embedded FILE records to a chosen folder (path-traversal sanitized). — (not ported)

### MavlinkLog form (tlog graph + conversion/export hub)
- [ ] `BUT_graphmavlog` "Graph Log" — pick a tlog, build the message-type tree, graph fields on `zg1`. — (not ported)
- [ ] Tree node double-click → `GraphItem(parent, field, leftaxis)`; left-click = left axis, right-click = right axis. — (not ported)
- [ ] `BUT_redokml` "Create KML + GPX" — `MavlinkLogBase.writeKML` writes .kml + .gpx flight track (progressBar1). — (not ported)
- [ ] `BUT_humanreadable` "Convert to Text" — dump tlog to human-readable text. — (not ported)
- [ ] `BUT_convertcsv` "Convert to CSV" — export tlog messages to CSV. — (not ported)
- [ ] `BUT_paramsfromlog` "Extract Params" — pull parameter set from tlog → .param. — (not ported)
- [ ] `BUT_getwpsfromlog` "Extract WPs" — pull mission/waypoints from tlog. — (not ported)
- [ ] `BUT_matlab` "Create Matlab file" — `MatLabForms.ProcessTLog()` → .mat. — (not ported)
- [ ] `but_cs` "Extract CS" — extract CurrentState fields. — (not ported)

### LogDownloadMavLink (download DataFlash via MAVLink)
- [ ] `BUT_DLall` "Download All Logs" / `BUT_DLthese` "Download Selected Logs" — list logs on autopilot, download to disk. — (not ported)
- [ ] `BUT_clearlogs` "Clear Logs" — erase logs on the autopilot. — (not ported)
- [ ] `BUT_redokml` "Create KML", `BUT_firstperson` "First Person KML", `BUT_bintolog` ".bin to .log" — post-download conversions; `labelBytes` progress. — (not ported)
- [ ] (Serial variant `LogDownload`/`LogDownloadscp`: `BUT_DLall`/`BUT_DLthese`/`BUT_clearlogs`/`BUT_redokml`/`BUT_firstperson`/`BUT_dumpdf` "(adv) Dump All DF"/`BUT_bintolog`.) — (not ported)
- [ ] `LogIndex` — scans a tlog directory, parses each for time-in-air/home/distance, builds a sortable session index. — (not ported)

### LogAnalyzer — Auto Analysis (Utilities/LogAnalyzer.cs + py2exe runner)
- [ ] `CheckLogFile` downloads `LogAnalyzer(64).zip` from firmware.ardupilot.org into the data dir (if `runner.exe` missing), runs `runner.exe -x <file>.xml -s <file>`, returns the XML. — (not ported)
- [ ] `Results()` parses XML → `analysis` (logfile, sizekb, sizelines, duration, vehicletype, firmware version/hash, hardware type, free mem, skipped lines + per-test results). — (not ported)
- [ ] `Controls.LogAnalyzer` UI = single read-only text box: header block of log metadata, then one line per test `Test: <name> = <status> - <message>`. — (not ported)
- [x] Result status values: **GOOD / WARN / FAIL / NA / UNKNOWN** (no colour grid — plain text).

### LogAnalyzer — automated test categories (LogAnalyzer/py2exe/tests/)
- [ ] **Brownout** (`TestBrownout`) — detects power loss / incomplete log (altitude not returning to ground). — (not ported)
- [x] **Compass** (`TestCompass`) — mag field length vs expected, offsets, compass health.
- [x] **GPS** (`TestGPSGlitch`) — GPS glitches / HDop / sat count drops.
- [x] **VCC** (`TestVCC`) — board voltage (5V rail) stability.
- [ ] **PM / Performance** (`TestPerformance`) — scheduler PM record / main loop performance, free memory. — (not ported)
- [x] **Vibration** (`TestVibration`) — IMU accel vibration levels.
- [ ] **IMU Mismatch** (`TestIMUMatch`) — IMU1 vs IMU2 acceleration agreement. — (not ported)
- [ ] **Gyro Drift** (`TestDualGyroDrift`) — dual-gyro drift comparison. — (not ported)
- [ ] **Pitch/Roll** (`TestPitchRollCoupling`) — pitch/roll coupling / attitude tracking (Copter). — (not ported)
- [ ] **Autotune** (`TestAutotune`) — autotune result/gains sanity. — (not ported)
- [x] **Motor Balance** (`TestMotorBalance`) — per-motor output balance (Copter).
- [ ] **Thrust** (`TestThrust`) — thrust/throttle vs climb. — (not ported)
- [ ] **OpticalFlow** (`TestOptFlow`) — optical-flow sensor quality. — (not ported)
- [ ] **Event/Failsafe** (`TestEvents`) — EV/ERR failsafe events. — (not ported)
- [ ] **Parameters** (`TestParams`) — parameter sanity checks. — (not ported)
- [x] **NaNs** (`TestNaN`) — NaN/inf values in the log.
- [ ] **Dupe Log Data** (`TestDupeLogData`) — duplicated/garbage log data. — (not ported)
- [ ] **Empty** (`TestEmpty`) — empty/too-short log guard. — (not ported)

### Graph presets (graphs/*.xml — `CMB_preselect` source)
- [x] `mavgraphs.xml` (108 graphs) — groups: **Sensors** (49), **PSC**, **Plane**, **Replay**, **EKF3**, **Copter**, **Servos**, **Attitude**, **Speed**, **SITL**, **RC**, **Aliasing**.
- [x] `mavgraphs2.xml` (137 graphs) — groups: **Sensors** (36), **Quadplane**, **Plane**, **Servos**, **Flow**, **Copter**, **TECS**, **Rover**, **EKF3**, **Replay**, **Radio**, **PSC**, **Vibe**, **Power**, **PM**, **Notch**, **Link**, **EKF**, **Wind**, **OBC**, **Board**, **Aerobatics**, **ADAP**.
- [x] `mavgraphsMP.xml` (30 graphs) — groups: **Builtin** (24: attitude/velocity/position deltas, TECS deltas, EKF innovations, Battery Watts, Efficiency mah-KM / watth-KM, Throw, DistDelta), **Sensors**, **Speed**, **Plane**.
- [x] `ekfGraphs.xml` (41 graphs) — all **EKF2** group.
- [x] `ekf3Graphs.xml` (47 graphs) — all **EKF3** group.
- [x] Group naming convention `Group/SubGroup/Name`; each `<graph>` holds one or more `<expression>` lines (suffix `:2` = right axis).


---

## Tool Modules (Swarm · Antenna Tracker · Joystick · Radio/SiK)

Port-fidelity extraction of Mission Planner's standalone tool modules. Each form's controls → handler → action, plus context menus, on-screen labels, and what the screen displays live. Source paths under `external/MissionPlanner/{Swarm,Antenna,Joystick,Radio,SikRadio}/`.

---

## Swarm (multi-vehicle: Formation · Follow Path · Follow Leader · Sequence · SRB · Waypoint Leader)

### Swarm.cs (abstract base — no UI)
- [ ] Base `Swarm` class. `Arm()` doARM true all-except-leader; `Disarm()` doARM false; `Takeoff()` GUIDED + MAV_CMD.TAKEOFF alt 5; `Land()` setMode "Land" all; `GuidedMode()`/`AutoMode()` setMode excl-leader; abstract `Update()`/`SendCommand()` — (deferred)

### FormationControl (Title "Control") — Formation swarm GUI
- [ ] BUT_Arm ("Arm (exl leader)") → BUT_Arm_Click → SwarmInterface.Arm() — (deferred)
- [ ] BUT_Disarm ("Disarm (exl leader)") → BUT_Disarm_Click → SwarmInterface.Disarm() — (deferred)
- [ ] BUT_Takeoff ("Takeoff") → BUT_Takeoff_Click → SwarmInterface.Takeoff() — (deferred)
- [ ] BUT_Land ("Land (all)") → BUT_Land_Click → SwarmInterface.Land() — (deferred)
- [ ] but_guided ("Guided Mode (exl leader)") → but_guided_Click → SwarmInterface.GuidedMode() — (deferred)
- [ ] but_auto ("Auto Mode (exl leader)") → but_auto_Click → SwarmInterface.AutoMode() — (deferred)
- [ ] BUT_leader ("Set Leader") → BUT_leader_Click → rebase all offsets to current MAV, setLeader, updateicons, enable Start+UpdatePos — (deferred)
- [ ] BUT_Start ("Start"/"Stop", disabled until leader) → BUT_Start_Click → toggle 10 Hz mainloop (Update+SendCommand; req POSITION stream @10Hz on leader) — (deferred)
- [ ] BUT_Updatepos ("Update Pos", disabled until leader) → BUT_Updatepos_Click → recompute each non-leader UTM offset from leader (clamp <200m), update icons + Formation.setOffsets — (deferred)
- [ ] CMB_mavs (combo, "Port sysid compid") → CMB_mavs_SelectedIndexChanged → set MainV2.comPort + sysid/compid current — (deferred)
- [ ] grid1 (Swarm.Grid, tabPage1 "Stage 1") → UpdateOffsets → grid1_UpdateOffsets → Formation.setOffsets (blocks moving Leader: "Can not move Leader") — (deferred)
- [ ] Mouse wheel → FollowLeaderControl_MouseWheel → grid1 zoom (setScale ±4) — (deferred)
- [ ] timer_status (200ms) → timer_status_Tick → rebuild PNL_status cards per MAV; leader red — (deferred)
- [ ] FormClosing → threadrun=false; Ctor pops "this is beta, use at own risk" — (deferred)
- [ ] DISPLAYS: grid1 formation grid w/ draggable drone icons (X/Y or vertical Z), PNL_status per-MAV Status cards, leader red — (deferred)

### FollowPathControl (Title "Control") — FollowPath swarm GUI
- [ ] BUT_Arm ("Arm (exl leader)") → BUT_Arm_Click → SwarmInterface.Arm() — (deferred)
- [ ] BUT_Disarm ("Disarm (exl leader)") → BUT_Disarm_Click → SwarmInterface.Disarm() — (deferred)
- [ ] BUT_leader ("Set Leader") → BUT_leader_Click → setLeader, enable Start — (deferred)
- [ ] BUT_Start ("Start"/"Stop", disabled until leader) → BUT_Start_Click → toggle 5 Hz mainloop (Update trail + SendCommand guided WPs) — (deferred)
- [ ] BUT_connect ("Connect MAVs") → BUT_connect_Click → CommsSerialScan.Scan(true), wait ≤50s, reset bindings — (deferred)
- [ ] CMB_mavs (combo) → CMB_mavs_SelectedIndexChanged → set MainV2.comPort by port string — (deferred)
- [ ] timer_status (200ms) → timer_status_Tick → rebuild PNL_status cards per PORT — (deferred)
- [ ] Note: BUT_Takeoff/BUT_Land handlers exist in .cs but NOT wired in Designer — (deferred)
- [ ] Ctor pops "this is beta, use at own risk"; DISPLAYS: PNL_status per-port Status cards — (deferred)

### Status (UserControl — per-drone status card)
- [ ] No interactive controls. Labels: Armed(lbl_armed), GPS(lbl_gps), Mode(lbl_mode), MAV(lbl_mav), Guided(lbl_guided), Location(lbl_loc), Speed(lbl_spd) — (deferred)
- [ ] Static captions: "Armed","Mode","GPS","Guided","Location","Speed" + MAV header — (deferred)
- [ ] DISPLAYS: live armed flag, GPS OK/Bad, flight mode, MAV id, guided target lat/lng/alt, location, ground speed — (deferred)

### Grid (custom MyUserControl — formation grid canvas)
- [ ] CHK_vertical ("Vertical") → CHK_vertical_CheckedChanged → toggle X/Y vs Z(side) icon view, Invalidate — (deferred)
- [ ] contextMenuStrip1 → changeAltToolStripMenuItem ("Change Alt") → InputBox new alt for hovered icon, fire UpdateOffsets — (deferred)
- [ ] Mouse drag icon → OnMouseMove → move icon x/y (or z if vertical), fire UpdateOffsets — (deferred)
- [ ] Mouse drag empty → pan grid (centerx/centery); mouse-wheel zoom (host-driven) — (deferred)
- [ ] OnPaint → scaled grid lines, green center axes, red meter labels, BGImage, drone icons (pie+name+z) — (deferred)
- [ ] DISPLAYS: meter-scaled coordinate grid, optional bg image, draggable icons (red movable / blue leader) w/ name+altitude — (deferred)

### Formation.cs / FollowPath.cs / DroneBase.cs (backends, no UI)
- [ ] Formation : Swarm — setOffsets/getOffsets per MAV; SendCommand WGS84↔UTM rotated offsets; ArduPlane uses PID roll/pitch/yaw/thrust + set_attitude_target, else setPositionTargetGlobalInt + CONDITION_YAW (or gimbal mount); PID helper (P/I/D/FF + input filter) — (deferred)
- [ ] FollowPath : Swarm — Update appends leader trail; SendCommand PlanMove() spaced path (FollowDistance=2), setGuidedModeWP per follower — (deferred)
- [ ] DroneBase — Location/Velocity/Heading/ProjectedLocation; SendPositionVelocityYaw/SendPositionVelocity/SendVelocity (setPositionTargetGlobalInt) / SendYaw (CONDITION_YAW if >3° error) — (deferred)

### FollowLeader/Control (Title "Control")
- [ ] but_master ("Set Ground Master") → but_master_Click → DG.groundmaster=comPort.MAV; rebuild Drones — (deferred)
- [ ] but_airmaster ("Set Air Master") → but_airmaster_Click → DG.airmaster=comPort.MAV; rebuild Drones — (deferred)
- [ ] but_arm ("Arm") → but_arm_Click → doARM true all — (deferred)
- [ ] but_takeoff ("TakeOff") → but_takeoff_Click → setMode GUIDED + MAV_CMD.TAKEOFF alt5 all — (deferred)
- [ ] but_auto ("Auto") → but_auto_Click → setMode AUTO all — (deferred)
- [ ] but_guided ("Guided") → but_guided_Click → setMode GUIDED all — (deferred)
- [ ] but_navguided ("NAV Guided") → but_navguided_Click → MAV_CMD.GUIDED_ENABLE all — (deferred)
- [ ] but_start ("Start"/"Stop") → but_start_Click → toggle mainloop (DG.UpdatePositions 10Hz) — (deferred)
- [ ] numericUpDown1 ("Seperation", def 5.0) → DG.Seperation; numericUpDown2 ("Lead", def 20) → DG.Lead; numericUpDown3 ("Altitude", def 10) → DG.Altitude — (deferred)
- [ ] Backends: Drone.cs, DroneGroup.cs (leader-follow path logic, no UI); no live status panel — (deferred)

### SRB/Control (Title "Control")
- [ ] but_start ("Start") → but_start_Click → recreate Controller, push TakeOffAlt/MinOffset/MaxOffset/ZSpeed, Start() — (deferred)
- [ ] but_z ("Start Z") → but_z_Click → DG.CurrentMode = z — (deferred)
- [ ] but_land ("Start Land") → but_land_Click → DG.CurrentMode = LandAlt — (deferred)
- [ ] but_stop ("Stop") → but_stop_Click → ctl.Stop() — (deferred)
- [ ] num_TakeOffAlt (def 2) → DG.TakeOffAlt; num_minoffset (def 4) → DG.MinOffset; num_maxoffset (def 14) → DG.MaxOffset; num_zspeed (def 0.005) → DG.ZSpeed — (deferred)
- [ ] timer1 → timer1_Tick → label4 "BasePos:", label5 "BaseVel:", label6 "Mode:", label7 "BaseHeading:" — (deferred)
- [ ] DISPLAYS: live BasePos / BaseVel / Mode / BaseHeading; backends Controller.cs, Drone.cs, DroneGroup.cs — (deferred)

### Sequence/LayoutEditor (Title "Layout Editor")
- [ ] BUT_load ("Load") → BUT_load_Click → OpenFileDialog, Sequence.Load, set num_drones, rebuild, UpdateDisplay — (deferred)
- [ ] comboBox1 (layouts, DisplayMember Id) → comboBox1_SelectedIndexChanged → set workingLayout, refresh grid — (deferred)
- [ ] BUT_new ("New Layout") → BUT_new_Click → InputBox name, add Layout (copy offsets), rebind — (deferred)
- [ ] num_drones (1–1000) → num_drones_ValueChanged → add/remove drone offset across all layouts — (deferred)
- [ ] BUT_addstep ("Add Step") → BUT_addstep_Click → append combo's layout name to Steps — (deferred)
- [ ] but_takeoff ("Takeoff") → but_takeoff_Click → restart Controller; all GUIDED, ARM, MAV_CMD.TAKEOFF alt2 (parallel) — (deferred)
- [ ] BUT_resetstep ("Reset") → BUT_resetstep_Click → step=0 — (deferred)
- [ ] BUT_runstep ("Run Step") → BUT_runstep_Click → run current step: each drone SendPositionVelocity to offset; label1 "name : step"; step++ — (deferred)
- [ ] BUT_save ("Save") → BUT_save_Click → SaveFileDialog, workingSequence.Save — (deferred)
- [ ] but_setimage ("set image") → but_setimage_Click → OpenFileDialog → grid.BGImage, pop "Move Image" nudge ButtonList (Up/Down/Left/Right/X±/Y±/Scale 0.1/Scale 1) — (deferred)
- [ ] listBox1 (steps, drag-drop reorder) → MouseDown DoDragDrop + select layout; DragDrop reorder; KeyUp Delete remove step — (deferred)
- [ ] grid (Swarm.Grid) → UpdateOffsets → grid_UpdateOffsets → workingLayout.AddOffset(sysid, vec); mouse wheel zoom ±4 — (deferred)
- [ ] but_mission_Click handler exists (MISSION_START) but no wired button — (deferred)
- [ ] DISPLAYS: grid w/ offset icons + bg image, steps listbox, current-step label; backends Controller/Drone/DroneGroup/Layout.cs — (deferred)

### WaypointLeader/WPControl (Title "WPControl", TopMost)
- [ ] but_master ("Set Ground Master") → but_master_Click → DG.groundmaster; rebuild Drones — (deferred)
- [ ] but_airmaster ("Set Air Master") → but_airmaster_Click → DG.airmaster; rebuild Drones — (deferred)
- [ ] but_start ("Start"/"Stop") → but_start_Click → warn if airborne; toggle mainloop (UpdatePositions 10Hz) — (deferred)
- [ ] but_resetmode ("Reset Mode") → but_resetmode_Click → warn if airborne; DG.CurrentMode = idle — (deferred)
- [ ] but_rth ("set mode rth") → but_rth_Click → DG.CurrentMode = RTH — (deferred)
- [ ] but_setmoderltland ("RTL (abandon mission)") → but_setmoderltland_Click → DG.CurrentMode = LandAlt — (deferred)
- [ ] numericUpDown1 ("Line Seperation", def 5.0) → DG.Seperation; numericUpDown2 ("Lead", def 20) → DG.Lead — (deferred)
- [ ] num_useroffline ("User Offline trig", def 10) → DG.OffPathTrigger; num_rtl_alt ("alt seperation", def 2) → DG.Takeoff_Land_alt_sep — (deferred)
- [ ] num_wpnav_accel ("WPNAV_ACCEL", def 1) → DG.WPNAV_ACCEL — (deferred)
- [ ] chk_V ("V") → chk_V_CheckedChanged → DG.V (V-formation); chk_alt_interleave ("Alt Interleave") → DG.AltInterleave — (deferred)
- [ ] timer1 → timer1_Tick → txt_mode=CurrentMode; rebuild PNL_status cards (GPS+sats, Armed, Mode, MAV id w/ air/ground master tag red, Guided, Location, Speed); update zedGraph alt-vs-distance — (deferred)
- [ ] textBox1 (readonly) → static instructions; zedGraphControl1 → live "Distance" vs "Altitude" plot (red Path + per-MAV alt markers) — (deferred)
- [ ] Note: but_arm/but_takeoff/but_auto/but_guided/but_navguided handlers exist in .cs but NOT wired in Designer — (deferred)
- [ ] DISPLAYS: txt_mode, PNL_status cards, zedGraph plot, instructions; backends Drone/DroneGroup/Path.cs; localized resx (tr/ru-KZ/uk/ko-KR/pt/az) — (deferred)

---

## Antenna Tracker (TrackerUI)

### Connection bar
- [x] CMB_interface ("Interface"; items Maestro, ArduTracker, def Maestro) → CMB_interface_SelectedIndexChanged → select tracker backend; enable Speed/Accel only for Maestro
- [x] CMB_serialport → none (populated from SerialPort.GetPortNames() on Activate) → choose COM port
- [x] CMB_baudrate (items 4800–115200, DropDownList) → none → choose baud
- [x] BUT_connect ("Connect"/"Disconnect") → BUT_connect_Click → saveconfig, instantiate backend, open port, push ranges/trims/PWM/speed/accel, Init+Setup, center PanAndTilt(0,0), disable fields, start 10Hz "Antenna Tracker" thread; 2nd click stops thread, closes port, re-enables fields, toggles label
- [x] BUT_find ("Find Trim Pan (Sik Radio)", Visible=False default) → BUT_find_Click → queues tm1_Tick SiK SNR-guided auto pan-trim search (coarse→fine on localsnrdb), errors if snr==0

### Pan section
- [x] label2 "Pan"; label12 bold warning "Misusing this interface can cause servo damage, use with caution!!!"
- [x] TXT_panrange ("Range"/"Angle", def 360) → TXT_panrange_TextChanged → set TRK_pantrim Min/Max (±180); on connect PanStartRange/PanEndRange (±range/2)
- [x] TRK_pantrim ("Trim", ±360, tick 5) → TRK_pantrim_Scroll → tracker.TrimPan, update LBL_pantrim (manual pan offset)
- [x] LBL_pantrim → display-only live pan trim value
- [x] CHK_revpan ("Rev") → CHK_revpan_CheckedChanged (empty; read at connect → PanReverse) → reverse pan
- [x] TXT_pwmrangepan ("PWM Range", def 1000) → none (read at connect → PanPWMRange)
- [x] TXT_centerpan ("Center PWM", def 1500) → TXT_centerpan_TextChanged (read at connect → PanPWMCenter; may write back)
- [x] TXT_panspeed ("Speed", def 100, Maestro-only) → TXT_panspeed_TextChanged → PanSpeed
- [x] TXT_panaccel ("Acceleration", def 5, Maestro-only) → TXT_panaccel_TextChanged → PanAccel

### Tilt section
- [x] label7 "Tilt"
- [x] TXT_tiltrange ("Range"/"Angle", def 90) → TXT_tiltrange_TextChanged → set TRK_tilttrim Min/Max (±range/2); on connect TiltStartRange/TiltEndRange
- [x] TRK_tilttrim ("Trim", ±180, tick 5) → TRK_tilttrim_Scroll → tracker.TrimTilt, update LBL_tilttrim
- [x] LBL_tilttrim → display-only live tilt trim value
- [x] CHK_revtilt ("Rev") → CHK_revtilt_CheckedChanged (empty; read at connect → TiltReverse) → reverse tilt
- [x] TXT_pwmrangetilt ("PWM Range", def 1000) → none (read at connect → TiltPWMRange)
- [x] TXT_centertilt ("Center PWM", def 1500) → TXT_centertilt_TextChanged (read at connect → TiltPWMCenter; may write back)
- [x] TXT_tiltspeed ("Speed", def 100, Maestro-only) → TXT_tiltspeed_TextChanged → TiltSpeed
- [x] TXT_tiltaccel ("Acceleration", def 5, Maestro-only) → TXT_tiltaccel_TextChanged → TiltAccel

### Live display / runtime
- [x] LBL_pantrim / LBL_tilttrim → live pan & tilt trim angle as sliders move (and as auto-find sets pan)
- [x] BUT_connect.Text → live connection status (Connect ⇄ Disconnect)
- [x] mainloop thread (10 Hz) → tracker.PanAndTilt(cs.AZToMAV, cs.ELToMAV) — follows live vehicle az/el from MAVLink; no on-screen az/el readout; auto-find reads cs.localsnrdb
- [x] Persistence → saveconfig() (on Deactivate + connect) writes every control to Settings as `Tracker_<name>`; Activate() restores + repopulates ports

### TrackerGeneric.cs (controller, no UI)
- [x] Owns static `ITrackerOutput tracker` + tracking thread; wires control events. Backend by CMB_interface enum `{Maestro, ArduTracker, DegreeTracker}`:
  - [x] Maestro — Pololu Maestro servo controller; only backend with Speed/Accel
  - [x] ArduTracker — ArduPilot-based tracker over serial
  - [x] DegreeTracker — degree-commanded tracker (selectable in code, not in combo resx)
- [x] ITrackerOutput: ComPort, Pan/Tilt Start/End Range, TrimPan/TrimTilt, Reverse, PWMRange, PWMCenter, Speed, Accel, Init/Setup/PanAndTilt(az,el)/Close. Connect: Init→Setup→PanAndTilt(0,0)→spawn mainloop

---

## Joystick (JoystickSetup + button/axis assignment dialogs)

### JoystickSetup (MAIN form)
- [x] CMB_joysticks ("Joystick") → CMB_joysticks_Click (repopulate devices) + CMB_joysticks_SelectedIndexChanged (UnAcquire if not enabled) → select DirectInput device; from getDevices(), persisted to Settings["joystick_name"]
- [x] BUT_enable ("Enable"/"Disable") → BUT_enable_Click → toggle: create JoystickBase, set elevons, joy.start(), MainV2.joystick.enabled=true (or disable + clearRCOverride); errors "Please Connect a Joystick"
- [x] BUT_save ("Save") → BUT_save_Click → MainV2.joystick.saveconfig() + save joy_elevons; warns "Please select a joystick"
- [x] CHK_elevons ("Elevons") → CHK_elevons_CheckedChanged → MainV2.joystick.elevons (CH1/CH2 mixing); via Settings["joy_elevons"]
- [x] chk_manualcontrol ("Manual Control") → chk_manualcontrol_CheckedChanged → MainV2.joystick.manual_control (MANUAL_CONTROL msg vs RC override)
- [x] but_export ("Export") → but_export_Click → SaveFileDialog *.joycfg, saveconfig + ExportConfig
- [x] but_import ("Import") → but_import_Click → confirm, OpenFileDialog *.joycfg, ImportConfig+loadconfig, force form close ("reopen for changes")
- [x] Static headers: label5 "Joystick", label6 "Expo", label7 "Output", label8 "Controller Axis", label9 "Reverse"
- [x] label14 ("Loaded Config for") → on load appends firmware (e.g. "Loaded Config for ArduCopter")
- [x] timer1 → timer1_Tick → LIVE LOOP: lazily acquire joystick + build button rows; write joy.getValueForChannel(1..18) into cs.rcoverridechN (live PWM out); update each button hbar 100/0 from isButtonPressed
- [x] Joystick_Load → enumerate devices, restore saved joystick/elevons, build maxaxis=16 JoystickAxis rows (wire Detect/Reverse/SetAxis/GetValue/Expo); start timer
- [x] DISPLAYS LIVE: per-axis output PWM bars, per-button pressed bars, channel PWM pushed to rcoverridech1..18
- [x] Dynamic AXIS rows: one JoystickAxis per RC channel 1..16
- [x] Dynamic BUTTON rows (timer doButtontoUI, up to min(16,numButtons)):
  - [x] butlabel ("But N") → static button-number label
  - [x] cmbbuttonN (button list -1..126, DropDownList) → cmbbutton_SelectedIndexChanged → joystick.changeButton(row, physicalBtn)
  - [x] mybutN ("Detect") → BUT_detbutton_Click → getPressedButton() auto-fill combo
  - [x] hbarN → live pressed-state bar (timer)
  - [x] cmbactionN (DropDownList = buttonfunction enum) → cmbaction_SelectedIndexChanged → set config.function (Tag=button idx)
  - [x] butsettingsN ("Settings") → but_settings_Click → open matching dialog by buttonfunction (ChangeMode/Mount_Mode/Do_Repeat_Relay/Do_Repeat_Servo/Do_Set_Relay/Do_Set_Servo/Button_axis0/1) else "No settings to set"

### JoystickAxis (per-axis row control)
- [x] label13 ("RC N") → axis/channel name
- [x] CMB_CH (items RZ/X/Y/SL1, bound joystickaxis enum) → CMB_CH_SelectedIndexChanged → SetAxis → joystick.setAxis(ch, axis)
- [x] BUT_detch ("Auto Detect") → BUT_detch_Click → Detect = getMovingAxis(name,16000) auto-detect axis into CMB_CH
- [x] ProgressBarCH (Min 800 / Max 2200) → live output bar, row timer reads GetValue=cs.rcoverridechN
- [x] expo_ch ("0") → expo_ch_TextChanged → Expo = joystick.setExpo(ch, value)
- [x] revCH → revCH_CheckedChanged → Reverse = joystick.setReverse(ch, value)
- [x] timer1 → timer1_Tick → update ProgressBarCH Value+maxline from GetValue()

### Button-action settings dialogs (modal, take button index as ctor name/Tag; read/write MainV2.joystick.getButton(i)/setButton(i,config), config fields p1–p4/mode/function/axis/buttonno)
- [x] Joy_Button_axis (toggle axis between 2 PWM): numericUpDownpwmmin ("PWM 1", 800–2200 def 800) → config.p1; numericUpDownpwmmax ("PWM 2", def 1500) → config.p2
- [x] Joy_ChangeMode: comboBox1 (modes from Common.getModesList(firmware)) → config.function=ChangeMode, config.mode
- [x] Joy_Mount_Mode: comboBox1 (from param MNT1_DEFLT_MODE/MNT_DEFLT_MODE/MNT_MODE) → config.function=Mount_Mode, config.p1; label1 static help (Retract/Neutral/Mavlink_targeting/rc_targeting/gps_point)
- [x] Joy_Do_Repeat_Relay: numericUpDown1 ("Relay No#", max 3)→p1; numericUpDown2 ("Repeat #")→p2; numericUpDown3 ("Time")→p3 (written together)
- [x] Joy_Do_Repeat_Servo: ("Servo No#")→p1; ("Pwm Value", 900–2100 def 1500)→p2; ("Rep Time")→p3; ("Delay (ms)")→p4 (together)
- [x] Joy_Do_Set_Relay: numericUpDown1 ("Relay No#", max 3) → config.p1
- [x] Joy_Do_Set_Servo: numericUpDownservono ("Servo No#")→p1; numericUpDownpwm ("PWM", 900–2100 def 1500)→p2
- [x] Note: no DataGridView — both "grids" are dynamically generated rows of discrete controls; buttonfunction enum drives action combo + which dialog opens

---

## Radio — Sikradio (legacy 3DR/SiK config; also embedded as SikRadio Config "Settings" panel)

### Action buttons (Local / global)
- [x] BUT_getcurrent ("Load Settings") → BUT_getcurrent_Click → open session, AT mode, ATI/ATI2/ATI3/ATI5/ATI7 + RTI*, parse local+remote, populate all controls, set version/board/freq/RSSI/country
- [x] BUT_savesettings ("Save Settings") → BUT_savesettings_Click → validate, AT mode, write changed via AT/RT `S..=`, set AES keys, `AT&W`/`RT&W` commit, `ATZ`/`RTZ` reboot
- [ ] BUT_Syncoptions ("Copy required to remote") → BUT_Syncoptions_Click → copy Local→Remote: AIR_SPEED, NETID, ECC, MAVLINK, MIN/MAX_FREQ, NUM_CHANNELS, MAX_WINDOW, ENCRYPTION_LEVEL, AESKEY — (not ported)
- [x] BUT_resettodefault ("Reset to Defaults") → BUT_resettodefault_Click → `RT&F`/`AT&F` + `&W` + `Z` reboot (remote first)
- [ ] BUT_upload ("Upload Firmware (standard)") → BUT_upload_Click → ProgramFirmware(false): download board-matched .ihx/.bin from firmware.ardupilot.org / rfdesign, flash — (not ported)
- [ ] BUT_loadcustom ("Upload Firmware (custom)") → BUT_loadcustom_Click → ProgramFirmware(true): OpenFileDialog local firmware, flash — (not ported)
- [ ] BUT_SetPPMFailSafe ("Set PPM Fail Safe") → BUT_SetPPMFailSafe_Click → SetPPMFailSafe("AT&R","AT&W"); enabled only when GPO1_1R_COUT checked — (not ported)
- [ ] btnRandom ("Random") → btnRandom_Click → fill AESKEY(+RAESKEY) random hex; enabled only when encryption on — (not ported)
- [ ] btnSaveToFile ("Save to File...") → btnSaveToFile_Click → SaveToFile(_LocalSettings) → TSettings.SaveToFile — (not ported)
- [ ] btnLoadFromFile ("Load from File...") → btnLoadFromFile_Click → LoadFromFile + UpdateControlsWithValues — (not ported)
- [ ] Progressbar → Progressbar_Click → secret toggle: flips `beta` firmware-channel flag; shows firmware % — (not ported)
- [ ] btnRemoteSaveToFile ("Save to File...", remote) → btnRemoteSaveToFile_Click → SaveToFile(_RemoteSettings, groupBoxRemote, true) — (not ported)
- [ ] btnRemoteLoadFromFile ("Load from File...", remote) → btnRemoteLoadFromFile_Click → LoadFromFile(_RemoteSettings, ...) — (not ported)
- [ ] BUT_SetPPMFailSafeRemote ("Set PPM Fail Safe", remote) → BUT_SetPPMFailSafeRemote_Click → SetPPMFailSafe("RT&R","RT&W"); enabled only when RGPO1_1R_COUT checked — (not ported)

### LinkLabels
- [ ] linkLabel1 ("Status Leds") → linkLabel1_LinkClicked → MessageBox red/green LED meanings — (not ported)
- [ ] linkLabel_mavlink ("Settings for Standard Mavlink") → MAVLINK=1 & MAX_WINDOW=131 (L+R) — (not ported)
- [ ] linkLabel_lowlatency ("Settings for Low Latency") → MAVLINK=2 & MAX_WINDOW=33 (L+R) — (not ported)

### Param rows — LOCAL column (R-prefixed twin in REMOTE column, same handlers; read/written generically by name→AT map)
- [x] FORMAT ("Format") → read-only display, excluded from writes
- [x] SERIAL_SPEED ("Baud") combo; def {1,2,4,9,19,38,57,115,230,460} (RFD900x)
- [x] AIR_SPEED ("Air Speed") combo; RFD900x {4,64,125,250,500}
- [x] NETID ("Net ID") combo; 0–500 (or 0–65535 / 0–255 ASYNC on RFD900x)
- [x] TXPOWER ("Tx Power") combo; 0–30 (RFD900-family) or 0–20
- [x] ECC ("ECC") checkbox
- [x] MAVLINK ("Mavlink") combo; RawData/Mavlink/LowLatency
- [x] OPPRESEND ("Op Resend") checkbox
- [x] MIN_FREQ ("Min Freq") / MAX_FREQ ("Max Freq") combos; range by band (915/433/868)
- [x] NUM_CHANNELS ("# of Channels") combo; 1–50 RFD900x
- [x] DUTY_CYCLE ("Duty Cycle") combo
- [x] LBT_RSSI ("LBT Rssi") combo; 0–220 step25 (RFD900x) else 0–1
- [x] RTSCTS ("RTS CTS") checkbox
- [x] MAX_WINDOW ("Max Window (ms)") combo; 33–131 def, 20–400 RFD900x
- [x] ENCRYPTION_LEVEL ("AES Encryption") → ENCRYPTION_LEVEL_CheckedChanged → write level live, re-read AES key, toggle btnRandom
- [x] AESKEY ("AES Key") textbox → txt_aeskey_TextChanged → strip non-hex live; written via `AT&E=` padded to max key len
- [ ] FSFRAMELOSS ("Failsafe Frame Loss") spare editor — (unverified)
- [ ] Extra/dynamic (NODEID, DESTID, TX_ENCAP_METHOD, RX_ENCAP_METHOD, MAX_DATA, MAX_RETRIES, GLOBAL_RETRIES, SER_BRK_DETMS) → ExtraParamControlsSet.SetModel per firmware (P2P/MULTIPOINT/ASYNC/MULTIPOINT_X) — (unverified)
- [ ] GPIO params (GPI1_1R_CIN, GPO1_1R_COUT, GPO1_3STATLED, GPI1_2AUXIN, GPO1_3AUXOUT, GPO1_0TXEN485, GPIO1_1FUNC, GPO1_3SBUSIN/OUT) → CheckBox/ComboBox; SBUSIN/OUT use TDynamicLabelEditorPair; COUT/CIN/SBUS issue `TPO/TPI=1`; CIN&COUT disabled on RFD900x — (not ported)

### Live display (read-only, populated by Load Settings)
- [x] ATI ("Version", local) ← `ATI` firmware version
- [x] ATI2 ← `ATI2` board type (Uploader.Board); ATI3 ← `ATI3` freq band (Uploader.Frequency)
- [x] RSSI ← `ATI7` live link RSSI text (preserved across remote reset loop)
- [ ] txtCountry ("Country:") ← GetCountryCodeFromSession; "--" if unlocked — (not ported)
- [ ] RTI ("Remote Version") ← `RTI`; empty ⇒ no remote radio (gates remote save); RTI2 ← `RTI2`; txtRCountry remote country — (unverified)
- [x] lbl_status ← live status text + NOTE "Always click Copy required to remote…" + uploader/iHex LogEvent

### Underlying (ComPort.cs / Uploader.cs / Models.cs)
- [x] Connect/Disconnect → GetComPortForSiKRadio / FinishedWithComPortForSiKRadio; closes MainV2.comPort, opens SerialPort/TcpSerial or MAVLinkSerialPort(TELEM1)
- [x] doCommand(port,cmd,multiline,level) → AT exchange w/ echo check + retry
- [ ] ProgramFirmware → getFirmware (board→URL: HM_TRP, RFD900/A/U/P/X) or getFirmwareLocal; RFD900.ProgramFirmware via Uploader.upload (IHex/XModem) — (not ported)
- [ ] Uploader.Board enum (RF50, HM_TRP, RFD900/A/U/P/X/X2/UX/UX2) & Frequency enum (433/470/868/915) drive ranges + firmware selection — (unverified)

---

## SikRadio (newer standalone SiK config app)

### Config form — wrapper (Config.cs)
Hosts active sub-form in panel1/groupBox2 (header = sub-form's Header). LOCAL/REMOTE param UI lives in embedded `Sikradio` settings control (see Radio above), shown by default.
- [x] CMB_SerialPort ("Port") → CMB_SerialPort_SelectedIndexChanged → set PortName/comPortName; CMB_SerialPort_Click → repopulate ports + "TCP"
- [x] CMB_Baudrate ("Baud", def 57600; 2400…) → CMB_Baudrate_SelectedIndexChanged → set baud
- [ ] btnConnect ("Connect"/"Disconnect") → btnConnect_Click → connect/disconnect modem, toggle text, enable sub-form, lock port/baud combos — (unverified)
- [x] settingsToolStripMenuItem ("Settings") → settingsToolStripMenuItem_Click → ShowForm(loadSettings) → embed Sikradio
- [x] terminalToolStripMenuItem ("Terminal") → terminalToolStripMenuItem_Click → ShowForm(loadTerminal)
- [x] rssiToolStripMenuItem ("RSSI") → rssiToolStripMenuItem_Click → ShowForm(loadRssi)
- [ ] aboutToolStripMenuItem ("About") → dropdown only — (not ported)
- [ ] helpToolStripMenuItem ("Help") → helpToolStripMenuItem_Click → open wiki (ardupilot-mega/3DRadio) — (not ported)
- [ ] projectPageToolStripMenuItem ("Project Page") → projectPageToolStripMenuItem_Click → open github.com/tridge/SiK — (not ported)
- [ ] groupBox1 "ComPort", groupBox2 "Settings", panel1 (host), pictureBox1 (sik logo) → no handler — (not ported)

### Settings panel — Sikradio (LOCAL groupBoxLocal "Local" / REMOTE groupBoxRemote "Remote")
Action buttons & param/checkbox controls are identical to the Radio/Sikradio module above (Load/Save Settings, Upload std/custom, Reset to Defaults, Copy required to remote, Set PPM Fail Safe L/R, Load/Save to File L/R, Random, the 3 link-labels, ENCRYPTION_LEVEL/RENCRYPTION_LEVEL checked-changed, and all SERIAL_SPEED/AIR_SPEED/NETID/TXPOWER/MAVLINK/MIN_FREQ/MAX_FREQ/NUM_CHANNELS/DUTY_CYCLE/LBT_RSSI/MAX_WINDOW/… combos + ECC/OPPRESEND/RTSCTS/GPIO checkboxes + AESKEY/FORMAT/txtCountry textboxes). Newer combos also exposed: MAX_DATA "Max Data", MAX_RETRIES "Max Retries", GLOBAL_RETRIES "Global Retries", SER_BRK_DETMS "Break Detection", ANT_MODE "Antenna Mode", RATE_FREQBAND "Rate/FreqBand" (also updates txtCountry), NODEID/DESTID, TX/RX_ENCAP_METHOD, GPIO1_1FUNC, GPO1_3SBUSOUT, FSFRAMELOSS.
- [x] Live display: ATI ("Version"), ATI2 board, ATI3 band, RSSI ("RSSI"=ATI7); REMOTE RTI/RTI2 ver/board, RFORMAT, RAESKEY; lbl_status live status + static NOTE

### Rssi form (Rssi.cs) — live signal plot
ZedGraph `zedGraphControl1` (title "RSSI"); `timer1` polls port (write A–Z, read line, regex `RSSI: n/n  L/R noise: n/n`). Connect issues `AT&T=RSSI`; logs to Terminal-*.txt. No buttons.
- [x] Curve "RSSI Local" (red) — local RSSI
- [x] Curve "RSSI Remote" (green) — remote RSSI
- [x] Curve "Noise Local" (blue) — local noise
- [x] Curve "Noise Remote" (orange) — remote noise

### Terminal form (Terminal.cs) — raw AT console
- [x] TXT_terminal (RichTextBox, black bg / white Courier) → KeyPress buffer chars, Enter sends `cmd+\r` (or `+++` raw); KeyDown swallow arrows; Click auto-scroll. Displays modem RX live; logs to Documents\Terminal-<timestamp>.txt
- [ ] textBox1 (read-only) → static AT cheat-sheet: ATI/ATI2..ATI7, ATO, ATSn?/ATSn=X, ATZ, AT&W, AT&F, AT&T=RSSI, AT&T=TDM, AT&T — (unverified)
- [x] No buttons; Connect verifies AT-command mode via RFD900 TSession

### Modem protocol (RFD900.cs / MainV2.cs / ComPort.cs)
- [ ] RFD900 TSession owns AT/RT command exchange, firmware upload (IHex/XModem), board/frequency detection; MainV2 wires the standalone app shell — (not ported)


---

## Auxiliary Modules (Grid · GeoRef · NoFly · Utilities · Plugins · Scripts · Updater)

Port-fidelity checklist for Mission Planner auxiliary feature modules. Source paths relative to repo root (`external/MissionPlanner/`). One line per user-facing control → action.

### Grid — Survey (Grid) Generator (`Grid/GridUI.cs` + `.Designer.cs`, title "Survey (Grid)")
Multi-tab dialog that fills a polygon (drawn on the planner map) with a camera survey flightpath. Tabs: **Simple · Grid Options · Camera Config**. `Grid/camerainfo.cs` = camera DB record; `GridData.cs` = persisted state.

**Tab: Simple**
- [x] `tabSimple` "Simple" — quick-setup tab (Simple Options group)
- [x] `label1` "Altitude" + alt textbox → AGL flight altitude
- [x] `label4` "Angle [deg]" → grid line bearing
- [ ] `label3` "Take a picture every [m]" → trigger spacing — (not ported) (derived from overlap, no direct field)
- [x] `label2` "Distance between lines [m]" → lane spacing
- [x] `label5` "OverShoot [m]" → turnaround run past polygon edge
- [x] `label6` "StartFrom" → corner to start from (combo)
- [x] `label8` "Overlap [%]" / `label15` "Sidelap [%]" → forward/side image overlap
- [x] `label32` "LeadIn [m]" → straight lead-in before first photo
- [x] `label26` "Camera" → camera model selector (combo, pulls from camera DB)
- [x] `CHK_camdirection` "Camera top facing forward" → sensor orientation toggle
- [ ] `label24` "Flying Speed (est)" + `CHK_usespeed` "Use speed for this mission" → set mission airspeed — (not ported) (speed feeds stats only, no airspeed/use-speed)
- [ ] `CHK_toandland` "Add Takeoff and Land WP's" + `CHK_toandland_RTL` "Use RTL" → bracket grid with TO/Land/RTL — (not ported)
- [ ] `label37` "Split into x segments" → break grid into N sub-missions — (not ported)
- [ ] `label38` "Control-S to save / Control-O to load" → keyboard save/load of grid config — (not ported)

**Tab: Grid Options (advanced)**
- [x] `tabGrid` "Grid Options" / `groupBox1` "Grid Options" — advanced path shaping
- [x] `chk_crossgrid` "Cross Grid" → second perpendicular pass (crosshatch)
- [x] `chk_Corridor` "Corridor" + `label43` "Corridor Width [m]" → linear corridor survey
- [x] `chk_spiral` "Spiral" → spiral pattern (with `groupBoxSpiral`)
- [x] `groupBoxSpiral` "Spiral Options": `LBL_laps` "Number of Laps", `LBL_clockwise_laps` "Number of Clockwise Laps" (`LBL_clockwise_laps1` "-1 for Clockwise Spiral"), `CHK_match_spiral_perimeter` "Match Perimeter to Polygon"
- [ ] `groupBox7` "Plane Options": `chk_optimize_for_distance` "Optimise for Distance", `LBL_Alternating_lanes` "Alternate Lanes", `LBL_Lane_Dist` "Min Lane separation", `chk_spline` "Spline Exit/Entrys" — (not ported) (only Min Lane separation present)
- [ ] `groupBox_copter` "Copter Options": `CHK_copter_headinghold` "Heading Hold", `CHK_copter_headingholdlock` "Unlock from grid", `TXT_headinghold` + `BUT_headingholdplus`/`BUT_headingholdminus` (+/--) heading value, `LBL_copter_delay` "Delay at WP (sec)" — (not ported)
- [ ] `groupBox3` "Trigger Method" (radios): `rad_trigdist` "CAM_TRIGG_DIST", `rad_digicam` "DO_DIGICAM_CONTROL", `rad_repeatservo` "DO_REPEAT_SERVO" (`label16` Servo / `label18` PWM / `label17` Cycle Time [s]), `rad_do_set_servo` "DO_SET_SERVO" (`label39` PWM L / `label41` PWM H / `label42` Servo) — (not ported)
- [ ] `chk_stopstart` "Breakup starts" → stop/start camera per lane — (not ported)
- [x] `groupBox4` "Display" toggles: `CHK_boundary` Boundary, `CHK_markers` Markers, `CHK_grid` Grid, `CHK_internals` Internals, `CHK_footprints` Footprints, `CHK_advanced` "Advanced Options"
- [x] `groupBox5` "Stats" live readouts: `lbl_area` Area, `lbl_distance` Distance, `lbl_spacing` Dist between images, `lbl_grndres` Ground Resolution, `lbl_pictures` Pictures, `lbl_strips` No of Strips, `lbl_footprint` Footprint, `lbl_distbetweenlines` Dist between lines, `lbl_flighttime` Flight Time (est), `lbl_photoevery` Photo every (est), `lbl_turnrad` Turn Dia (at 45d), `lbl_gndelev` Ground Elevation, `lbl_minshutter` Min Shutter Speed — (subset; no gnd-elev)
- [x] `BUT_Accept` "Accept" → generate waypoints and push to Flight Planner mission

**Tab: Camera Config (`groupBox2` "Camera Options")**
- [x] `tabCamera` "Camera Config" — define/save camera optics record
- [x] `label11` "Focal Length [mm]", `TXT_imgwidth`/`TXT_imgheight` "Image Width/Height [Pixels]", `TXT_senswidth`/`TXT_sensheight` "Sensor Width/Height [mm]"
- [x] `label21` "Calculated Values": `label20` "Field of View Horizontal [m]", `label19` "Field of View Vertical [m]", `label12` "cm/pixel"
- [ ] `BUT_samplephoto` "Load Sample Photo" → auto-read sensor/focal from EXIF — (not ported)
- [ ] `BUT_save` "Save" → persist camera to DB combo — (not ported)

### GeoRef — Geo Ref Images (`GeoRef/georefimage.cs` + `.Designer.cs`, title "Geo Ref Images")
Geotags a folder of photos from a dataflash/tlog by matching photo timestamps to CAM/trigger log messages; writes EXIF GPS + KML/log outputs.

- [x] `BUT_browselog` "Browse Log" → pick .tlog/.bin flight log — (dataflash .bin/.log only, no tlog)
- [x] `BUT_browsedir` "Browse Pictures" → pick image folder
- [x] Matching mode radios: `RDIO_CAMMsgSynchro` "CAM Message" vs `RDIO_TimeOffset` "Time offset"
- [ ] `RDIO_trigmsg` "Trigger Message" / `chk_cammsg` "Use Cam Messages" → which log msg drives the match — (not ported) (only CAM-sync vs time-offset)
- [x] `label1` "Seconds offset" + `TXT_offsetseconds` → manual time offset photo↔log
- [ ] `BUT_estoffset` "Estimate Offset" → auto-estimate offset (matches photo count to CAM count) — (not ported)
- [ ] `TXT_shutterLag` "Shutter lag (ms)" (`label27`) → compensate camera latency — (not ported)
- [ ] `label3` "Min Shutter (s) (CAM)" → min interval filter — (not ported)
- [ ] `lbldrpstart` "Drop image at start" / `label2` "Drop image at end" → skip N frames each end — (not ported)
- [ ] Altitude options: `CHECK_AMSLAlt_Use` "Use AMSL Alt", `label28`/`txt_basealt` "Rel Alt base", `chk_camusegpsalt`/`chk_trigusergpsalt` "Use GPSAlt", `chk_usegps2` "Use GPS2" — (not ported) (Use GPS2 only; AMSL captured internally, no UI options)
- [ ] FOV/rotation tags: `label7` "Dir fov", `label8` "Cross fov", `label9` "Rotation" — (not ported) (roll/pitch/yaw written to location.txt, no FOV UI)
- [ ] `BUT_doit` "Pre-process" → parse log+photos, build match table (preview, no write) — (not ported) (single Geo Tag action)
- [ ] `BUT_Geotagimages` "GeoTag Images" → write EXIF GPS into photos — (not ported) (MetadataExtractor read-only; writes location.txt + KML instead)
- [x] `BUT_networklinkgeoref` "Location Kml" → output KML of photo positions — (always emitted alongside location.txt)

### NoFly (`NoFly/NoFly.cs`) — No-Fly Zone Overlay (no dialog; map overlay)
- [ ] Auto-loads every `*.kmz` in `NoFly/` dir on startup (`Scan()`) → purple polygons/routes overlay on planner map; gated by `ShowNoFly` setting — (not ported) (overlay built from one user-picked .kml/.kmz via FlightPlanner menu; no dir scan/startup auto-load)
- [ ] Shipped zone files: `Drone No Fly Zones.kmz`, `regions_11_april_2023.kmz`, `SouthAfricaNoRPASOutlined.kmz`, `UASZoneVersion*.kmz` — (not ported)
- [ ] Online sources fetched near current location (proximity 100 km): Hong Kong (`nfz.HK`, Yes/No confirm prompt) and EU (`nfz.EU`, Yes/No confirm prompt) — polygons + circular airport zones w/ tooltips — (not ported)
- [ ] `UpdateNoFlyZone(plla)` → refreshes nearby online zones when craft moves >100 m — (not ported)

### Utilities (`Utilities/`) — one line per utility
- [ ] `AirMarket.cs` (`AirMarketUI` UserControl) → AirMap/airspace-market panel (live airspace/Remote-ID registration UI) — (not ported)
- [x] `BoardDetect.cs` → autodetect connected autopilot board type from USB VID/PID (no UI) — (port-side `DetectBoardId()` in InstallFirmwareViewModel)
- [ ] `CircleSurveyMission.cs` → generate a circular/orbit survey mission (helper) — (not ported)
- [ ] `ExtensionsMP.cs` → C# extension-method helpers (no UI) — (not ported) (upstream main-project util, not referenced by port)
- [x] `Firmware.cs` → ArduPilot firmware list fetch/flash logic (drives Install Firmware screen) — (via APFirmware + px4 Uploader)
- [ ] `GStreamerUI.cs` (`GStreamerUI` Form) → dialog to set/launch GStreamer video pipeline string for HUD — (not ported) (LibVLC video substitute, no GStreamer pipeline dialog)
- [ ] `httpserver.cs` → built-in HTTP server exposing telemetry/map tiles (no UI) — (not ported)
- [ ] `ImageMatch.cs` → image template-matching helper (vision) — (not ported)
- [ ] `LangUtility.cs` (`CultureInfoEx`) → language/culture selection helper for localization — (not ported)
- [x] `LogAnalyzer.cs` → automated dataflash log analysis (rules → pass/warn/fail report) — (`Services/LogAnalyzer.cs`)
- [ ] `NativeLibrary.cs` → native DLL P/Invoke loader (no UI) — (not ported)
- [ ] `OsdTuningSlotProvider.cs` → supplies tunable OSD parameter slots (no UI) — (not ported)
- [x] `POI.cs` → Points-of-Interest store (add/save/load POIs shown on map, ObservableCollection) — (`Services/PoiStore.cs`)
- [x] `Speech.cs` → text-to-speech engine for telemetry callouts (no UI) — (`Services/Speech.cs`)
- [ ] `SSHTerminal.cs` (`SSHTerminal` Form) → SSH terminal window (companion-computer/Herelink shell) — (not ported)
- [ ] `ThemeManager.cs` → global UI theming (Burnt Kermit / colors) applied to all controls — (not ported) (only a theme-name setting/combo)
- [x] `Update.cs` (`Update`) → in-app update checker (see Updater below) — (`Services/Updater.cs`)
- [ ] `Win32DeviceMgnt.cs` → enumerate Windows serial/COM devices (no UI) — (not ported)
- [ ] `XMLColor.cs` (`XmlColor`) → serialize System.Drawing.Color to XML (config helper) — (not ported)
- [ ] `protogen/` → protobuf-net code generator tool (protogen.exe + xslt; build-time, not user-facing) — (not ported) (build tool)
- [x] Related cross-cutting utils in `ExtLibs/Utilities/`: `ParameterMetaDataParser.cs` (parse ArduPilot apm.pdef param metadata → tooltips/ranges), `Airports.cs` (nearest-airport DB lookup for map), `srtm.cs` (SRTM terrain-elevation tile fetch/cache); `ExtLibs/Controls/ImageLabel.cs` (image+text label control); KML handling in `ExtLibs/KMLib/` + `ExtLibs/SharpKml/` — (ParameterMetaDataParser + srtm reused from ExtLibs; KML parsed locally; Airports settings-toggle only; ImageLabel WinForms n/a)

### Plugins (`Plugins/`) — on-the-fly compiled `Plugin` subclasses; each adds menu items/widgets
Empty stubs (0 bytes, skip): `example2.cs example3.cs example4.cs example5.cs example6.cs example7.cs example8.cs`. Loadable examples:
- [ ] `example.cs` (urlmod) → minimal WebAPI URL example plugin (no menu) — (deferred)
- [ ] `example-watchbutton.cs` "Button change" → demo watching/altering a toolbar button — (deferred)
- [ ] `example2-menu.cs` "Small stuff" → menu "Fix mission top/bottom" (insert mission parts at start/end) — (deferred)
- [ ] `example3-fencedist.cs` "FenceDist" → menu "Draw Fence Dist" (draw geofence distance rings) — (deferred)
- [ ] `example5-latencytracker.cs` "LatencyTracker" → tracks link latency — (deferred)
- [ ] `example6-mapicondesc.cs` "..." → menu "Change icon Description" (relabel a map marker via InputBox) — (deferred)
- [ ] `example8-modechange.cs` "Mode Change Widget" → HUD widget to change flight mode — (deferred)
- [ ] `example9-hudonoff.cs` "HUD" → menu "HUD Items" (toggle individual HUD overlay items) — (deferred)
- [ ] `example10-canlogfile.cs` "CAN Log Extract" → menu; extract CAN frames from log to .txt — (deferred)
- [ ] `example7-canrtcm.cs` "CAN RTCM Extract" → menu "CAN RTCM Extract" (pull RTCM from CAN log) — (deferred)
- [ ] `example11-trace.cs` "tracemp" → menu "tracemp" (diagnostic trace) — (deferred)
- [ ] `example12-forwarding.cs` "Forwarding" → forward mavlink to another endpoint — (deferred)
- [ ] `example19-multiforward.cs` "Link Forward" → menu "Forward between links" (multi-link mavlink bridge) — (deferred)
- [ ] `example13-herelink2.cs` "HL Control" → menus "Herelink info / HL Get Info / HL connect" (Herelink2 mgmt) — (deferred)
- [ ] `example4-herelink.cs` "Camera Control" → menus "Herelink Video / Connect v1 / Set Video stream 1 v1" — (deferred)
- [ ] `example14-mass.cs` "mass Control" → menus "Mass / Arm / Arm(Force)" (multi-vehicle/swarm arm) — (deferred)
- [ ] `example15-leds.cs` "LED Control" → menus "LED / Red / Green ..." (set vehicle LED colors) — (deferred)
- [ ] `example16-donate.cs` "Donate" → adds "MenuCustom"/Donate menu entry — (deferred)
- [ ] `example17-menuremove.cs` "menucleanup" → removes/cleans up menu entries — (deferred)
- [ ] `example18-externalapi.cs` "External API" → menu "Share to API" (push telemetry to external API) — (deferred)
- [ ] `example20-multiplepositions.cs` "multiple positions" → menu "Setup other positions" (multi-antenna/base positions) — (deferred)
- [ ] `example21-persistentsimple.cs` "Persistent Simple Actions" → persisted Simple-tab actions — (deferred)
- [ ] `example22-fontsize.cs` "fontsize" → global UI font-size override — (deferred)
- [ ] `example22-payloadconfig.cs` "Payload Select Page" → adds a payload-config config page — (deferred)
- [ ] `example23-switch.cs` "CubeLan 8 port Switch" → CubeLAN 8-port switch control UI (+ `.resx`) — (deferred)
- [ ] `AnonymizeBinlogPlugin.cs` "Anonymize Binlog" → strip GPS/identifying data from a .bin log — (deferred)
- [ ] `generator.cs` (`generator` UserControl, `.resx`) → engine/hybrid-generator monitor panel (speed/power gauges) — (deferred)
- [ ] `InitialParamsCalculator.cs` "Initial Parameters" → compute starting tune params (Leonard Hall guide / xfacta sheet) — (deferred)
- [ ] `Dowding/` (`DowdingPlugin` "Dowding", `DowdingUI` form, `server/`) → counter-UAS/target-tracking: shows tracked targets as map markers, talks to a Dowding detection server — (deferred)
- [ ] `FaceMap/` (`FaceMapPlugin` "Face Map", `FaceMapUI` form) → vertical quarry/mine-face survey generator (GridUI-style: bench offset, toe angle, camera pitch/overlap → mission) — (deferred)
- [ ] `OpenDroneID2/` (`OpenDroneID_Plugin` "Open Drone ID") → Remote-ID receiver: forms `OpenDroneID_UI`, `OpenDroneID_Map_Status` (map of nearby Remote-ID craft), `NMEA_GPS_Connection`, `NMEA_Viewer`; backend `OpenDroneID_Backend.cs` — (deferred)
- [ ] `Shortcuts/` (`Shortcuts` plugin) → global Alt+key keyboard shortcuts (Alt+A/G/U/S/H/T/L, Alt+0, Alt+F1…) for fast navigation — (deferred)
- [ ] `TerrainMakerPlugin/` ("TerrainMakerPlugin") → menu "Make Terrain DAT" (build ArduPilot terrain .DAT tiles from SRTM) — (deferred)

### Plugin host (`Plugin/`)
- [ ] `Plugin.cs` abstract base (`Name`/`Version`/`Author`, `Init()`/`Loaded()`/`Loop()`/`Exit()` lifecycle, access to `Host`); `PluginLoader.cs` compiles+loads .cs plugins at runtime; `PluginUI.cs` (`.Designer.cs`/`.resx`) = plugin manager dialog. — (deferred)

### Scripts (`Scripts/`) — IronPython example scripts (`cs.<var>` = currentstate API)
- [ ] `example1.py` … `example10.py` → graded scripting tutorials (read currentstate vars, set params) — (deferred)
- [ ] `example4 wp.py` → waypoint manipulation example — (deferred)
- [ ] `example5 inject data.py` → inject mavlink/data via `clr` — (deferred)
- [ ] `example8 - speech.py` → text-to-speech callout example — (deferred)
- [ ] `example9 - sitl.py` → launch/drive SITL example — (deferred)
- [ ] `TAKEOFF.py` → scripted takeoff sequence — (deferred)
- [ ] `PARACHUTE LANDING APPROACH.py` → scripted parachute landing approach — (deferred)
- [ ] `rc.py` / `rc - heli.py` → RC override examples (heli variant) — (deferred)
- [ ] `ui.py` → IronPython UI/dialog example (`#!/usr/bin/ipy`) — (deferred)
- [ ] `wipe.py` → wipe/reset example — (deferred)
- [ ] `cubeorange.py` → Cube Orange specific setup (uses `clr`) — (deferred)
- [ ] `datetime.py` / `debugenv.py` → date/time + environment-debug helpers — (deferred)

### Updater (`Updater/`) — auto-update flow
Two-stage: in-app check (`Utilities/Update.cs`) downloads files as `*.new`; standalone `Updater.exe` (`Updater/Program.cs`) swaps them in after MP closes.
- [x] In-app: `Update.CheckForUpdate()` → GET `version.txt` (release / beta / master per AppSettings); if newer, prompt with `[link …ChangeLog.txt]` → on accept `DoUpdate()` runs `IProgressReporterDialogue` worker, `CheckMD5` per-file vs `*UpdateLocationMD5`, downloads changed files as `.new` — (check/download/notify via GitHub releases API instead of version.txt; no MD5/`.new`)
- [ ] Handoff: MP launches `Updater.exe`, exits — (not ported) (Windows-installer-specific)
- [ ] `Updater/Program.cs`: detects MAC/Unix (mono), 5 s grace sleep, `UpdateFiles()` → for each `*.new`: move current → `.old`, move `.new` → real name (10 retries), then delete leftover `*.old`; on failure prints "Update failed, please try it later." + waits keypress — (not ported)
- [ ] Relaunch: starts MissionPlanner again (`mono MissionPlanner.exe` on Mac, else exe) from same dir — (not ported)
- [ ] Files: `app.config` (update URLs/AppSettings), `app.manifest`, `mykey.snk` (strong-name key) — (not ported)


---

## Controls (custom WinForms controls & dialogs)

Custom controls live in two trees: `ExtLibs/Controls/` (shared primitives, gauges, dialogs) and `Controls/` (app-level dialogs/forms). DialogResult-returning dialogs noted explicitly.

### Core display / HUD

- [x] **HUD.cs** (`ExtLibs/Controls/HUD.cs`, ~3850 lines) — artificial-horizon + flight overlay, dual OpenGL/GDI+ render path (`opengl` auto-fallback to GDI on failure). Draws: roll/pitch ladder & sky/ground horizon, heading tape (`displayheading`), airspeed/groundspeed tape (`displayspeed`), altitude + target-alt tape (`displayalt`), vertical-speed, throttle, wind direction/speed, GPS status (`displaygps`), connection/link info (`displayconninfo`), cross-track error (`displayxtrack`), battery x2 (`batteryon/batteryon2`), cell voltage (`displayCellVoltage`), AOA/SSA (`displayAOASSA`), EKF/Vibe/Prearm status hit-zones (`displayekf/displayvibe/displayprearm`), payload icons (`displayicons`). Alert flags flash overlay: `failsafe`, `safetyactive`, `lowvoltagealert`, `criticalvoltagealert`, `lowgroundspeed/lowairspeed`. **Left-click on EKF zone → ekfclick (opens EKFStatus); on Vibe zone → vibeclick (opens Vibration); on Prearm zone (only when disarmed) → prearmclick (opens PrearmStatus)**; hover over these zones shows hand cursor. **Right-click context menu (defined in FlightData, `contextMenuStripHud`): Record HUD to AVI, User Items (pick extra fields), Russian HUD (alt horizon style), Swap With Map, Ground Color.** `Russian` and per-item display bools are toggles persisted in settings.

### Buttons / inputs / sliders

- [x] **MyButton** — owner-drawn rounded gradient Button; colors BGGradTop/Bot, Outline, TextColor, mouse-over/down overlay tints, disabled tint; strips `&` mnemonic; standard Click. Used as the app's standard button everywhere (incl. dialog OK/Cancel).
- [x] **MyTrackBar** — TrackBar with float-scaled Min/Max/Value/Tick/Small/LargeChange (internally ×1000 int).
- [x] **MyLabel** — SkiaSharp label with optional auto-width resize. **MyProgressBar / HorizontalProgressBar / HorizontalProgressBar2 / VerticalProgressBar(2)** — custom progress bars (gradient fill, optional min/max text overlay, marquee/reverse modes; Vertical = Horizontal rotated 270°). **ProgressStep** — progress bar with "Progress… X of Y" label.
- [ ] **RangeControl** — (not ported) linked TrackBar + NumericUpDown + min/max labels for editing one numeric parameter (Increment/DisplayScale/Min/MaxRange); `ValueChanged` event; implements IDynamicParameterControl (auto-expands bounds to contain input).
- [x] **ValuesControl** — ComboBox value selector; `ValueChanged` on selection. **ModifyandSet** — NumericUpDown + button; exposes Click + ValueChanged.
- [ ] **ClickBindingButton / KeyBindingButton** — (not ported) capture a mouse-button / key binding on click (outline turns red while listening, then shows the binding string). Used by gimbal/joystick settings.
- [x] **FileBrowse** — textbox + browse button; `OpenFile` flag chooses open-vs-save dialog.

### Gauges / instruments

- [x] **HSI** — horizontal situation indicator: compass rose drawing heading + nav bearing (cardinal/intermediate letters); redraws on Heading/NavHeading change.
- [x] **AGauge** — generic configurable analog gauge: up to 5 needles (color/width/type/radius/value), 5 colored range arcs, scale numbers, major/minor ticks, multiple cap labels. (No dedicated separate attitude-indicator control exists — attitude is rendered by HUD.)
- [x] **WindDir** — circular wind direction (0–360°) + speed dial with max-speed scaling.
- [x] **QuickView** — SkiaSharp widget showing a description line + one large auto-sized number (`number`/`numberformat`/`desc`/`numberColor`); used in FlightData side panel. (Double-click to change the shown telemetry field is wired in FlightData, not the control.)
- [ ] **Sphere / ProgressReporterSphere** — (not ported) OpenTK 3D point-cloud (compass/mag-cal) viewer; arrow keys rotate ±5° yaw/pitch, mouse-sensitive. ProgressReporterSphere wraps 3 Spheres + auto-rotate/accept checkboxes for mag calibration.

### Generic dialogs (return DialogResult / value)

- [x] **InputBox** (static `Show`) — modal prompt: label + textbox (+ optional password/multiline; autocomplete from saved history) with **OK / Cancel** MyButtons. Returns DialogResult; value via `ref string/int/double`. Themed via ApplyTheme.
- [x] **AltInputBox** — variant input dialog (alternate layout) returning entered value.
- [x] **CustomMessageBox** (static `Show`, namespace MissionPlanner.MsgBox) — themed replacement for MessageBox: text auto-wrapped, optional icon, custom Yes/No button text, supports inline clickable link via `[link;url;text]` markup. Standard MessageBoxButtons → returns DialogResult. Thread-marshals to UI thread.
- [ ] **OptionForm** — (not ported) modal ComboBox picker with **OK / Cancel**; returns SelectedItem.
- [x] **ProgressReporterDialogue** — modal background-op dialog: progress bar + status label + Cancel; runs `DoWork` on a threadpool thread (Mono-safe), supports cooperative cancel (CancelRequested/CancelAcknowledged), on exception shows error text with a Close button. UpdateProgressAndStatus(percent,text) from worker.
- [x] **Loading** — singleton splash/"please wait" form; static `ShowLoading(text)` / `Close`; auto-hides after UI activity.
- [ ] **FlashMessage** — (not ported) auto-fading toast; `FadeInOut(msg, success)` (yellow-green vs coral), queues if one is visible.

### Navigation containers (Setup/Config depend on these)

- [x] **BackstageView** (`BackstageView/BackstageView.cs`) — MS-Office-style left nav menu + content panel used by Setup and Config tabs. `AddPage(userControlType, headerText, parent, advanced)` registers a `BackstageViewPage`; `AddSpacer(h)` inserts gaps. Left menu (`pnlMenu`) is rebuilt by `DrawMenu` from the Pages collection: top-level items become `BackstageViewButton`s; parents with children are prefixed `>>` and expand/collapse to show indented child buttons; pages flagged `Advanced` are hidden unless static `Advanced` is on. **Click a nav button → ActivatePage(page): hides old page (calls IDeactivate.Deactivate), lazily creates & shows the page control, calls IActivate.Activate, marks button selected; raises static `Tracking` event.** **Double-click a nav button → pops that page out into a standalone Form (re-docked back on close).** `Close()` disposes all created pages.
- [x] **BackstageViewButton** — nav tab button: selected state fills gradient + right-edge arrow pointer and uses SelectedTextColor; hover paints subtle dark overlay; unselected uses UnSelectedTextColor.
- [x] **BackstageViewPage** — page descriptor; `Page` property lazily instantiates the UserControl on first access; carries Parent/Advanced/Spacing/Show. (BackstageViewCollection = typed list.)
- [x] **MainSwitcher** — top-level screen switcher for MainV2 main tabs (FlightData/Plan/Setup/Config/Sim/Help/Terminal). `AddScreen(Screen)` registers a name→Type/Control; **`ShowScreen(name)` swaps the visible MyUserControl**: deactivates+disposes the outgoing (unless `Persistent`), lazily Activator-creates the incoming, docks fill, calls IActivate/IDeactivate, applies theme, raises `Tracking`.
- [x] **MyUserControl** — base UserControl for nearly all controls; adds Close()/FormClosing/FormClosed events, Mono-safe WndProc & OnPaint (early-exit on empty clip / swallow paint exceptions).

### Data grid

- [x] **MyDataGridView** — DataGridView with double-buffering and Mono-safe overrides (swallows paint/cell/mousemove exceptions, logs them). Used by every param/data table.

### Connection bar & link stats

- [x] **ConnectionControl** — top-right connection bar: COM-port combo (`CMB_serialport`), baud combo (`CMB_baudrate`), sysid combo; custom-drawn port items; link-stats label click → `ShowLinkStats` event; `IsConnected(bool)` disables port/baud combos while connected.
- [x] **ToolStripConnectionControl** — ToolStripControlHost wrapper so ConnectionControl sits on the main toolstrip.
- [ ] **ConnectionOptions** (Form) — (not ported) extra link config; **BUT_connect → doConnect()** adds the port to MainV2.Comports.
- [ ] **ConnectionStats** (UserControl) — (not ported) live link stats (packets/bytes/quality/loss) updated ~4 Hz from reactive streams.

### Sensor status / diagnostics (mostly Forms opened from HUD clicks or menus)

- [ ] **ControlSensorsStatus** — (not ported) grid of MAV_SYS_STATUS_SENSOR bits (present/enabled/healthy per sensor).
- [x] **PrearmStatus** (Form) — timer requests RUN_PREARM_CHECKS, lists prearm failure messages; auto-closes on arm.
- [x] **EKFStatus** (Form) — EKF confidence bars (velocity/pos/compass/terrain) + flag indicators (red when unhealthy), timer-updated.
- [x] **Vibration** (Form) — X/Y/Z vibration bars + accel clip counts, timer-updated.
- [x] **Status** (UserControl) — single green percent bar (Percent 0–100), auto-hides after 10 s.
- [x] **RAW_Sensor** (Form) — live 6-curve ZedGraph (accel X/Y/Z + gyro X/Y/Z).
- [ ] **ProximityControl** (Form) — (not ported) proximity/obstacle field render; **+/- zoom, [ / ] resize MAV symbol**, timer invalidate.
- [ ] **DistanceBar** (UserControl) — (not ported) stacked per-waypoint distance bar; AddWPDist/ClearWPDist; `traveleddist` triggers repaint.

### Parameter editing controls (MAVLink-bound)

- [x] **MavlinkCheckBox / MavlinkCheckBoxBitMask / MavlinkComboBox / MavlinkNumericUpDown** — param-bound editors: `setup(...paramname)` binds to a live FC parameter, pulls min/max/enum/scale from ParameterMetaDataRepository; raise CheckedChanged / ValueUpdated on edit. BitMask shows one checkbox per bit. **MAVLinkParamChanged** is the (name,value) EventArgs.
- [x] **paramcompare** (Form) — side-by-side grid comparing two param sets; **OK/Cancel** → DialogResult; can call setParam directly or via callback.
- [x] **ModifyandSet** — see Buttons section.

### Parameter / mavlink tooling dialogs

- [x] **MAVLinkInspector** (Form) — tree of live MAVLink traffic (sysid→compid→msgid), timer-updated; **"Graph it"** plots selected field.
- [ ] **MavCommandSelection** (Form) — (not ported) grid of MAV_CMD (id/name/param1-7); **"Add Line"** (InputBox for cmd id). Used to insert mission commands.
- [x] **DroneCANInspector** (Form) — tree of DroneCAN traffic (node→msgTypeID); **"Graph it"**, **"Subscribe"** buttons, timer-updated.
- [x] **DroneCANParams** (UserControl, IActivate) — UAVCAN param grid; buttons **Write PIDs / Re-request / Reset / Commit to Flash**.
- [ ] **DroneCANSubscriber** (UserControl) — (not ported) message-type combo + line count + multiline packet log.
- [x] **DroneCANFileUI / MavFTPUI** (UserControl) — file-system tree browsers over DroneCAN file API / MAVLink-FTP; async dir enumeration, progress bar.

### Setup/Config sub-pages & utilities

- [x] **DefaultSettings** (UserControl, IActivate) — frame-param dropdown (from ArduPilot GitHub); **"Load"** opens ParamCompare; `OnChange` event.
- [ ] **GMAPCache** (UserControl, IActivate) — (not ported) map-tile cache browser per provider; per-row **"Remove 30+ days" / "Remove All"**.
- [ ] **ThemeEditor** (Form) — (not ported) theme color list; **double-click color patch → color picker**; buttons **Copy (rename) / Preview / Restore / Save&Apply**; IconSet checkbox.
- [x] **ScriptConsole** (Form) — script stdout view; **Clear** button + autoscroll checkbox, ~5 Hz refresh.
- [ ] **AuthKeys** (Form) — (not ported) MAVLink signing-key grid; **"Add"** (InputBox name + passphrase with strength meter), **"Save"**, row delete; 190 ms refresh timer.
- [x] **LogAnalyzer** (Form) — displays log-analysis report (file/size/duration/vehicle/fw/hw/memory) + test pass/fail results.
- [ ] **DevopsUI** (UserControl) — (not ported) low-level I2C/SPI register read/write bridge; bustype combo toggles fields; **"Do It" / "Test"** append hex results.
- [ ] **DigitalSkyUI** (UserControl, IActivate) — (not ported) Indian DigitalSky airspace/permit integration (login/drones/flights/permits API).
- [ ] **SB** — (not ported) Cube service-bulletin dialog (name/email + **"Service Bulletin"** opens URL).
- [ ] **OpenGLtest / OpenGLtest2** — (not ported) dev/test GL surfaces (not user-facing).

### Telemetry forwarding / follow-me

- [x] **FollowMe** (Form) — port/baud/rate combos; thread streams GPS home/target to drone; **BUT_connect** toggles thread + button state.
- [ ] **MovingBase** (Form) — (not ported) moving-baseline base station; serial/TCP-host/TCP-client/UDP modes; can update rally points; TCP-host listener.
- [ ] **SerialSupportProxy** (Form) — (not ported) bridge to support.ardupilot.org (UDP/TCP); mirror-stream buttons.
- [x] **SerialOutputPass / CoT / MD / NMEA** (Forms) — mirror/convert telemetry to a secondary serial/TCP/UDP sink: Pass = raw passthrough, CoT = Cursor-on-Target XML, MD = MicroDrones downlink, NMEA = GPS NMEA position. Each has port/baud/updaterate combos, **BUT_connect** toggle, conversion thread; TCP-host variants run a listener.

### Video / gimbal

- [x] **Video** (Form) — discovers RTSP streams (ZeroConf); per-stream button + "Display External" checkbox; click launches GStreamer pipeline.
- [ ] **OSDVideo** (Form) — (not ported) overlays flight HUD on recorded/live video via DirectShow (ISampleGrabberCB), AVI playback; feeds frames into an embedded HUD.
- [ ] **GimbalVideoControl** (UserControl, IMessageFilter) — (not ported) video + gimbal pitch/yaw/zoom via key/mouse bindings; camera + gimbal-manager MAVLink protocols; auto-connect timer.
- [ ] **VideoStreamSelector** (Form) — (not ported) detected-stream combo + raw gstreamer pipeline textbox; **"Launch"** sets DialogResult.OK and `gstreamer_pipeline`.
- [ ] **GimbalControlSettingsForm** (Form) — (not ported) property grid of gimbal control settings using KeyBindingButton/ClickBindingButton + per-row clear (❌).

### Signal analysis

- [x] **fftui** (Form) — **"Run WAV"** opens file, FFT histogram (freq vs amplitude), sample-rate via InputBox, averaged.
- [ ] **SpectrogramUI** (Form) — (not ported) **"Load Log"**, sensor combo (ACC/GYR), 3 spectrograms (X/Y/Z) in a ZedGraph MasterPane, min/max-freq spinners.
- [ ] **PropagationSettings** (Form) — (not ported) RF/terrain propagation analysis options: terrain/RF/home/drone-distance checkboxes, resolution/rotational/angular combos, range/height/tolerance/alt spinners; persisted.

### Pre-flight checklist (Controls/PreFlight)

- [x] **CheckListControl** (UserControl) — runtime checklist panel driven by XML config; items color green/red on live trigger condition; ~100 ms refresh; LoadConfig/Draw.
- [x] **CheckListEditor** (Form) — edits checklist items; **Add/Delete** buttons, nested children, Save persists XML to the CheckListControl.
- [ ] **CheckListInput** (UserControl) — (not ported) one item editor: condition/source combos, trigger-value spinner, text/description boxes, true/false color dropdowns; `ReloadList` event.
- [ ] **CheckListItem** — (not ported) data model (Name/Description/Text/TriggerValue/ConditionType + child nesting); DisplayText() expands {trigger}/{value}/{name}.

### Drawable icons (Controls/Icon)

- [x] **Icon** (abstract base) + **File / Polygon / Zoom** subclasses — vector-drawn toolbar/map icons (Width/Height/colors/LineWidth, IsSelected, Location; each overrides doPaint: file lines / polygon outline / magnifier).

### Decorative / helper controls (grouped)

- [x] **LineSeparator, GradientBG, RadialGradientBG, LabelWithPseudoOpacity, PictureBoxWithPseudoOpacity, PictureBoxMouseOver, TransparentPanel, ImageLabel** — visual helpers: 2 px gradient divider; linear/radial gradient backgrounds; pseudo-alpha label/picturebox overlays; hover image-swap PictureBox; transparent panel; **ImageLabel** = image+caption combo whose PictureBox click re-raises a `Click` event (used as clickable menu tiles in Setup/Help).
- [x] **Coords** — geographic coordinate display/editor (GEO/UTM/MGRS, lat/lng/alt/unit).
- [x] **SKControl / SKGLControl** — SkiaSharp 2D-bitmap / GL render surfaces (base for QuickView, MyLabel, etc.).
- [x] **BindableListView** — ListView supporting IList/IBindingList/IListSource data binding.

### Path / map utilities

- [ ] **ElevationProfile** (Form) — (not ported) elevation profile of the current waypoint path from SRTM data (distance vs altitude curves).


---

## ExtLibs (backend libraries powering the UI)

Mission Planner's `ExtLibs/` holds ~3,300 `.cs` files across ~90 projects/folders. These are the non-UI backend libraries the WinForms screens depend on. Below, grouped by role, is the port-fidelity checklist. Each line: library — what it does → which UI features depend on it.

### Comms / MAVLink (the heart of the GCS)

- [x] **Mavlink** — MAVLink message definitions, parsing/packing, CRC, mavlink generator output (`MAVLink.cs`, message structs/enums) → every screen that talks to a vehicle (telemetry, HUD, status, commands).
- [x] **ArduPilot** — ArduPilot-specific MAVLink layer: command sending, parameter get/set, mission upload/download, message routing, firmware version handling → Flight Data commands, Flight Planner write/read mission, parameter config pages, calibration flows.
- [x] **Comms** — connection-link abstraction (`CommsBase.cs`, `ICommsSerial`). Supported connection types: **Serial port** (`CommsSerialPort`), **TCP-as-serial** (`CommsTCPSerial`), **UDP** (`CommsUdpSerial`, `CommsUDPSerialConnect`, `UDPStream`), **WebSocket** (`CommsWebSocket`), **BLE/Bluetooth LE** (`CommsBLE`, simpleble dlls), **WinUSB** (`CommsWinUSB`), **File replay** (`CommsFile`), **NTRIP** (`CommsNTRIP`), **serial pipe/stream** (`CommsSerialPipe`, `CommsStream`), plus RTCM **injection** (`CommsInjection`) → the connection dropdown/Connect button on the main toolbar; all link I/O.
- [x] **Ntrip** — NTRIP caster client for RTCM correction streams → RTK/GPS Inject (RTCM injection to base/rover), DGPS.
- [ ] **MavlinkMessagePlugin / MockDroneID / Mock** — message plugin hooks, simulated/mock MAVLink endpoints and DroneID → SITL/simulation, testing, RemoteID. — (not ported)
- [ ] **HIL** — Hardware-in-the-loop simulation glue → Simulation screen. — (not ported)
- [ ] **Antenna** — antenna tracker protocols/serial control → Antenna Tracker screen. — (not ported)
- [ ] **TrackerHome** — tracker home-position logic → Antenna Tracker. — (not ported)

### Mapping (GMap.NET stack)

- [x] **GMap.NET.Core** — map engine: tile fetching, caching (SQLite tile cache), projections, routing, geocoding, and the **map tile providers**. Providers present: **Google** (map/satellite/hybrid/terrain), **Bing**, **OpenStreetMap** (+ OSM variants), **ArcGIS/ESRI**, **Yandex**, **AMap** (AutoNavi/China), **Ovi/Nokia HERE**, **NearMap**, **Czech**, **Lithuania**, plus `Etc` (custom/WMS) → all map screens (Flight Data map, Flight Planner map, Antenna Tracker, ESP/geofence editors). The provider dropdown is sourced here.
- [x] **GMap.NET.WindowsForms** — WinForms map control (`GMapControl`), overlays, markers, polygons, routes, drag/zoom interaction → the interactive map widget embedded in Flight Data & Flight Planner.
- [x] **GMap.NET.Drawing** — GDI+ rendering of map markers/overlays → map marker/polygon drawing on those screens.
- [x] **Maps** — additional map data/provider helpers → map screen support.
- [ ] **KMLib / SharpKml** — KML/KMZ read/write (placemarks, polygons, paths) → import/export of geofences, survey grids, flight paths to/from Google Earth; KML overlay loading on the map. — (not ported)
- [ ] **kmlpolygons** *(see `KMLib`/SharpKml — polygon parsing)* — polygon geofence import → Geofence editor. — (not ported)
- [ ] **MissionPlanner.Gridv2** — survey-grid generation (camera footprint, overlap, waypoint grid) → Flight Planner "Survey (Grid)" auto WP tool. — (not ported)
- [ ] **GeoidHeightsDotNet** — EGM geoid undulation (ellipsoid↔AMSL altitude correction) → altitude display/planning accuracy, terrain following. — (not ported)

### Firmware / Upload / Device

- [x] **px4uploader** — PX4/Pixhawk bootloader serial uploader → Install Firmware screen (flash ArduPilot to autopilot).
- [ ] **NetDFULib** — STM32 DFU (USB) firmware upload protocol → firmware flashing for boards in DFU mode. — (not ported)
- [x] **UAVCANFlasher / DroneCAN** — DroneCAN/UAVCAN node communication and firmware update → DroneCAN/UAVCAN setup pages, CAN node flashing (GPS/ESC/peripherals).
- [ ] **NMEA2000** — NMEA2000 CAN parsing → marine/CAN sensor support. — (not ported)
- [ ] **Flasher / Arduino** — generic AVR/Arduino flashing helpers → bootloader/firmware tooling. — (not ported)
- [ ] **SharpAdbClient** — Android ADB client → Android-related device transfer features. — (not ported)
- [ ] **WinUSBNet / UsbSerialForAndroid / ManagedNativeWifi.Simple** — low-level USB/serial/WiFi enumeration → device auto-detect/connection. — (not ported)
- [ ] **solo** — 3DR Solo-specific provisioning/comms → Solo setup wizard. — (not ported)
- [ ] **OSDConfigurator** — on-screen-display (MinimOSD) config → OSD setup page. — (not ported)

### Plotting / 3D / Video / Drawing

- [x] **ZedGraph** — 2D charting/plotting → Tuning graph (live telemetry plot), Log Browse graphs, DataFlash log analysis charts.
- [ ] **OpenTK-1.0.dll / GLControl** — OpenGL bindings + WinForms GL control → 3D log view, 3D map/terrain view, Earth model rendering. — (not ported)
- [ ] **Exocortex.DSP** — FFT/DSP math → vibration/FFT analysis in log review. — (not ported)
- [ ] **AviFile** *(see `Utilities/AviWriter.cs`, `DirectShowLib`)* — AVI writing/capture → record HUD/screen to video. — (not ported)
- [x] **DirectShowLib / WebCamService / LibVLC.NET / GStreamerHud / Onvif** — video capture/streaming (DirectShow webcams, VLC, GStreamer pipelines, ONVIF IP cameras) → live video overlay on HUD, camera/gimbal screens, FPV feeds.
- [ ] **netDxf / dxf** — DXF CAD read/write → export survey/mission geometry to DXF. — (not ported)
- [ ] **SvgNet** — SVG generation → vector export/icons. — (not ported)
- [ ] **Transitions / Transitions.dll** — UI animation/tweening → animated panel transitions in the UI. — (not ported)
- [ ] **MissionPlanner.Drawing(.Common) / CoreCompat.System.Drawing / SkiaSharp.Views.Forms.WinForms** — cross-platform GDI+/Skia drawing shims → HUD and custom-control rendering (esp. for non-Windows). — (not ported)
- [x] **MetaDataExtractorCSharp240d** — image EXIF/metadata extraction → geo-reference images (geotagging photos from log), georefimage tool.
- [ ] **BitMiracle.LibTiff / GDAL / gdal_csharp / ogr/osr_csharp** — GeoTIFF/raster + OGR vector geospatial I/O → terrain/elevation import, GeoTIFF overlays, DEM handling. — (not ported)

### Scripting

- [ ] **python** *(IronPython)* — embedded IronPython engine → the Scripts console / automation (run user `.py` to drive the vehicle and UI). — (deferred)
- [ ] **Core / Interfaces** — plugin SDK interfaces and core host services → third-party plugin loading (the Plugins menu). — (deferred)
- [ ] **TestPlugin / SimpleExample / SimpleGrid** — sample plugins/examples → plugin developer references. — (deferred)

### Geo / Projection / Math

- [ ] **GeoUtility** — geodesy: lat/lon ↔ UTM/MGRS conversions, distance/bearing → coordinate display, MGRS/UTM entry, distance tools. — (not ported)
- [ ] **ProjNet / DotSpatial.Projections.dll** — coordinate-system reprojection (Proj4-style) → reprojecting imported geospatial data to the map CRS. — (not ported)
- [ ] **alglibnet** — numerical/linear-algebra library → curve fitting, compass/accel calibration math, optimization. — (not ported)
- [ ] **LibTessDotNet** — polygon tessellation → filled polygon/geofence rendering. — (not ported)
- [ ] **AStar.dll** — A* pathfinding → route/path planning helpers. — (not ported)
- [x] **Utilities** — grab-bag of backend helpers (DataFlash log parsing `BinaryLog/DFLog`, ADSB, airports DB, terrain `DTED/SRTM`, compass calibrator, georef image, GStreamer, downloads, crypto, custom message box, etc.) → log review, ADS-B traffic, terrain following, calibration, firmware download — pervasive across the app.

### Misc / Support

- [x] **BaseClasses** — shared base types/POCOs (PointLatLngAlt, locationwp, etc.) → used everywhere (waypoints, positions).
- [ ] **Controls** — custom WinForms controls: HUD, HSI, AGauge, gauges, message boxes, etc. → Flight Data HUD/instruments, dialogs. — (not ported)
- [ ] **BSE.Windows.Forms / ObjectListView / CsAssortedWidgets / SimpleGrid / LEDBulb / ImageVisualizer** — third-party WinForms widgets (collapsible panels, advanced list/grid views, LED indicators) → config grids, status panels, parameter lists. — (not ported)
- [ ] **Strings** — localization/translation resources → UI language switching. — (not ported)
- [ ] **speech** *(`System.Speech.dll`)* — text-to-speech → spoken telemetry alerts/warnings (low battery, mode change). — (not ported)
- [ ] **WebAPIs / Zeroconf / AltitudeAngelWings(.Plugin) / DigitalSky** — web service clients (airspace/UTM — Altitude Angel, India DigitalSky), mDNS service discovery → airspace overlays, UTM/regulatory features, network device discovery. — (not ported)
- [ ] **MissionPlanner.Stats / Benchmark** — usage telemetry/stats and benchmarking → anonymous stats reporting. — (not ported)
- [ ] **Ionic.Zip / ICSharpCode.SharpZipLib / 7zip / zlib.net / System.IO.Compression** — archive/compression → firmware/parameter/log packaging, downloads. — (not ported)
- [ ] **System.Data.SQLite.DLL** — SQLite → map tile cache, plugin/airport/data stores. — (not ported)
- [ ] **System.Reactive.dll** — Rx (reactive streams) → async event/telemetry pipelines. — (not ported)
- [x] **md5sum / Crypto** — hashing/integrity → firmware download verification.
- [ ] **DriverCleanup / CleanDrivers / pinvokegen / Installer / WindowsStore** — driver install/cleanup, P/Invoke gen, packaging → first-run driver setup, installer. — (not ported)
- [ ] **Xamarin(.Forms.Platform.WinForms) / MonoMac / uno / wasm / mono / System.Drawing.android** — cross-platform/mobile shims → Android/Mac/WASM build variants of MP. — (not ported)
- [ ] **ExtGuided** — external/guided control API → companion-computer/offboard guided commands. — (not ported)
- [ ] **GDAL/Maps/DTED helpers, tlogThumbnailHandler** — Windows shell thumbnail for `.tlog` files → Explorer integration. — (not ported)
- [x] **ParameterMetaDataGenerator** — generates parameter metadata (ranges/descriptions) → tooltips and validation in the Full Parameter List / config pages.

> Note: some folders in the brief (georefimage, AviFile, kmlpolygons, speech) exist as files inside `Utilities` or as DLLs (`System.Speech.dll`) rather than standalone project folders; mapped accordingly above.


---

