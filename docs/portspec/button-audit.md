# Interactive Control Audit — MissionPlanner-Avalonia

Audit of every interactive control (Button, MenuItem, ToggleButton, CheckBox/RadioButton with Command, code-behind `.Click +=`) across `src/MissionPlannerAvalonia/Views`, `Controls`, and their ViewModels.

Status legend:
- **WORKS** — calls comPort / setParam / doCommand / MAVLink / real file IO / real navigation.
- **STUB** — only logs a message, shows "not ported"/"not available", sets a label/status string, or empty body.
- **BROKEN** — bound Command has no matching member (resolves to nothing), or an obvious bug.
- **DEAD** — control has no Command and no Click handler at all (inert).

Paths are relative to `src/MissionPlannerAvalonia/`. Code-behind files (`*.axaml.cs`) are `InitializeComponent`-only unless noted.

---

## FlightDataView.axaml + ViewModels/FlightDataViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Record Hud to AVI (axaml:53) | HudVideoNotPortedCommand | STUB | `HudVideoNotPorted` FlightDataViewModel.cs:907-909 only logs "not ported" |
| Stop Record (54) | HudVideoNotPortedCommand | STUB | same stub :907 |
| Set MJPEG source (55) | HudVideoNotPortedCommand | STUB | same stub :907 |
| Start Camera (56) | HudVideoNotPortedCommand | STUB | same stub :907 |
| Set GStreamer Source (57) | HudVideoNotPortedCommand | STUB | same stub :907 |
| HereLink Video (58) | HudVideoNotPortedCommand | STUB | same stub :907 |
| GStreamer Stop (59) | HudVideoNotPortedCommand | STUB | same stub :907 |
| HereLink Video (60) | HudVideoNotPortedCommand | STUB | same stub :907 |
| Set Aspect Ratio (61) | SetAspectRatioCommand | STUB | `SetAspectRatio` :903-905 only logs |
| User Items (62) | HudUserItemsCommand | WORKS | `HudUserItems` :912 opens dialog, populates `_hudUserFields` consumed in Pump :172 |
| Russian Hud (63) | none (IsChecked TwoWay HudRussian) | WORKS | direct two-way bind drives HUD; no command needed |
| Swap With Map (64) | SwapHudMapCommand | WORKS | `SwapHudMap` :900 swaps HudColumn/MapColumn |
| Ground Color (65) | SetGroundColorCommand | WORKS | `SetGroundColor` :866 color picker → HudGroundColor |
| Battery Cell Voltage (66) | SetBatteryCellsCommand | WORKS | `SetBatteryCells` :963 dialog → HudBatteryCells |
| Show icons (67) | none (IsChecked TwoWay HudShowIcons) | WORKS | two-way bind |
| HUD Items checkboxes x14 (72-84) | none (IsChecked TwoWay) | WORKS | each two-way bound to Hud* bool; some (Connection/XTrack/Battery2/Aoa) bound but not all consumed by HudControl |
| Do Action (188) | DoActionCommand | WORKS | `DoAction` :617 → RunAction switch sends real MAV_CMDs :628 |
| Auto (189) | QuickAutoCommand | WORKS | `QuickAuto` :453 sets AUTO + setMode |
| Set Home Alt (190) | SetHomeCommand | WORKS | `SetHome` :684 DO_SET_HOME |
| Speed (194) | ChangeSpeedCommand | WORKS | `ChangeSpeed` :514 DO_CHANGE_SPEED |
| Set WP (200) | SetWpCommand | WORKS | `SetWp` :672 setWPCurrent |
| Loiter (201) | QuickLoiterCommand | WORKS | `QuickLoiter` :460 |
| Restart Mission (202) | RestartMissionCommand | WORKS | `RestartMission` :577 setWPCurrent(0)+MISSION_START |
| Alt (207) | ChangeAltCommand | WORKS | `ChangeAlt` :528 setNewWPAlt |
| Set Mode (213) | SetModeCommand | WORKS | `SetMode` :441 setMode |
| RTL (214) | QuickRtlCommand | WORKS | `QuickRtl` :467 |
| Raw Sensor View (215) | RawSensorViewCommand | STUB | `RawSensorView` :604 only logs |
| Loiter Rad (220) | SetLoiterRadCommand | WORKS | `SetLoiterRad` :543 setParam LOITER_RAD |
| Set Mount (226) | SetMountCommand | WORKS | `SetMount` :563 DO_MOUNT_CONFIGURE |
| Joystick (227) | JoystickCommand | STUB | `Joystick` :607 only logs |
| Arm/Disarm (228) | ToggleArmCommand | WORKS | `ToggleArm` :429 doARM |
| Clear Track (229) | ClearTrackCommand | STUB | `ClearTrack` :613 only logs (upstream MP clears map track) |
| Message (232) | ShowMessageCommand | STUB | `ShowMessage` :610 logs "Message" |
| Resume Mission (233) | ResumeMissionCommand | WORKS | `ResumeMission` :592 MISSION_START |
| Abort Landing (235) | AbortLandCommand | WORKS | `AbortLand` :697 DO_GO_AROUND |
| Edit (PreFlight, 254) | EditPreflightCommand | WORKS | `EditPreflight` :253 dialog edits manual checklist |
| PreFlight CheckBox (270) | none (IsChecked TwoWay Ok) | WORKS | manual items user-toggle; auto items HitTest-disabled |
| Servo Low/Mid/High/Toggle (327-338) | SetServoChannelCommand | WORKS | `SetServoChannel` :312 DO_SET_SERVO |
| Relay Low/High/Toggle (349-357) | SetRelayCommand | WORKS | `SetRelay` :736 DO_SET_RELAY |
| Redirect Program Output (373) | none (IsChecked RedirectOutput) | WORKS | bind only; value never read elsewhere |
| Select Script (374) | SelectScriptCommand | WORKS | `SelectScript` :350 real file picker (engine itself not bundled) |
| Edit Selected Script (376) | EditScriptCommand | STUB | `EditScript` :373 only logs |
| Run Script (378) | RunScriptCommand | STUB | `RunScript` :366 sets "not bundled" string |
| Abort Running Script (380) | AbortScriptCommand | STUB | `AbortScript` :370 sets status string |
| Reset Position (391) | ResetPositionCommand | STUB | `ResetPosition` :399 only zeroes local Tilt/Pan/Roll2; no MAVLink |
| Video Control (392) | TriggerCameraCommand | WORKS | `TriggerCamera` :778 DO_DIGICAM_CONTROL |
| Tilt slider (389) | none | DEAD | bound to Tilt but nothing transmits; PointMount/PointGimbal exist (:386,:763) but unbound |
| Pan slider (397) | none | DEAD | bound to Pan, never transmitted |
| Roll slider (401) | none | DEAD | bound to Roll2, never transmitted |
| **Load Log** (410) | none | DEAD | no Command, no Click |
| **Play/Pause** (414) | none | DEAD | no Command/Click |
| Telemetry slider (415) | none | DEAD | no Value binding |
| **Tlog > Kml or Graph** (418) | none | DEAD | no Command/Click |
| **Speed buttons 10x/5x/2x/1x/0.5/0.25/0.1** (421-427) | none | DEAD | 7 buttons, no Command/Click |
| Download DataFlash Log Via Mavlink (436) | DownloadDataflashLogCommand | WORKS | `DownloadDataflashLog` :412 GetLogEntry |
| **Review a Log** (438) | none | DEAD | no Command/Click |
| **Auto Analysis** (439) | none | DEAD | no Command/Click |
| **Create KML + gpx** (441) | none | DEAD | no Command/Click |
| **Convert Bin to Log** (442) | none | DEAD | no Command/Click |
| **Create Matlab File** (443) | none | DEAD | no Command/Click |
| **Geo Reference Images** (445) | none | DEAD | no Command/Click |
| **Connect to Transponder** (454) | none | DEAD | no Command/Click; comment at :452 confirms "no wiring" |
| Squawk NumericUpDown (457) | none | DEAD | hardcoded Value=1200, no binding |
| FlightID TextBox (459) | none | DEAD | no binding |
| **STBY / ALT / ON / IDENT** (462-465) | none | DEAD | 4 buttons, no Command/Click |
| Mode A/C/S, 1090ES checkboxes (471-473) | none | DEAD | interactive but unbound |
| Aux Function combos/numerics (501-505) | none (ParamField.SelectedOption/Value) | WORKS | param-write lives in ParamField (see ParamFieldsView), not a Command |

Orphan VM commands defined but never bound (dead code, not dead buttons): `PointMountCommand` (:386), `PointGimbalCommand` (:763), single `SetServoCommand` (:718), `ToggleHudIconsCommand` (:860), `ToggleRussianHudCommand` (:863).

---

## ActionPageView.axaml + ViewModels/ActionPageViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Action button, ItemTemplate (axaml:33) | per-item `Command` (ICommand) | WORKS | `ActionItem.Command` ActionPageViewModel.cs:18, built via `Action(label, run)` :33. Base adds no actions; subclasses inject real RelayCommands. Data-driven, no dead static buttons. Mapped by ViewLocator to Setup/Config calibration pages. |

---

## MainWindow.axaml + ViewModels/MainWindowViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Nav button "DATA" (38-48) | NavigateCommand param DATA | WORKS | `Navigate` MainWindowViewModel.cs:25 → CurrentScreen=FlightData (:28) |
| Nav button "PLAN" (49-59) | NavigateCommand param PLAN | WORKS | → FlightPlanner (:29) |
| Nav button "SETUP" (60-70) | NavigateCommand param SETUP | WORKS | → Setup (:30) |
| Nav button "CONFIG" (71-81) | NavigateCommand param CONFIG | WORKS | → Config (:31) |
| Nav button "SIMULATION" (82-92) | NavigateCommand param SIMULATION | WORKS | → Simulation (:32) |
| Nav button "HELP" (93-103) | NavigateCommand param HELP | WORKS | → Help (:33) |
| ComboBox Ports (126-131) | none (Ports/SelectedPort) | WORKS | binds ConnectionViewModel.cs:19,24 |
| ComboBox Bauds (132) | none (Bauds/SelectedBaud) | WORKS | ConnectionViewModel.cs:20,27 |
| Button refresh ports (133) | RefreshPortsCommand | WORKS | `RefreshPorts` ConnectionViewModel.cs:41 enumerates serial+net ports |
| Button Connect (134) | ToggleConnectCommand | WORKS | `ToggleConnect` ConnectionViewModel.cs:56 opens/closes _comPort (real MAVLink) |

---

## ConnectDialog.cs (code-behind dialog) + ViewModels/ConnectionViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "OK" (ConnectDialog.cs:37) | Click handler (:39) | WORKS | `Close(new[]{box1.Text, box2?.Text})` |
| Button "Cancel" (ConnectDialog.cs:38) | Click handler (:40) | WORKS | `Close(null)` |

`ConnectionViewModel.BuildStreamAsync` (cs:92) drives `ConnectDialog.Show` (cs:171) for TCP/UDP/WS prompts — real transport construction.

---

## BackstageView.axaml + ViewModels/BackstageViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Backstage page button (templated, 25-34) | SelectCommand param {Binding} | WORKS | `Select` BackstageViewModel.cs:48 → SelectedPage → OnSelectedPageChanged (:38) loads CurrentContent from lazy factory (:26). Real navigation. |

---

## ConfigView.axaml + ViewModels/ConfigViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Host only — embeds BackstageView (axaml:14) | inherits SelectCommand | WORKS | `ConfigViewModel : BackstageViewModel` registers 10 pages (cs:7-16): Flight Modes, Standard/Advanced Params, GeoFence, Basic/Extended Tuning, Onboard OSD, User Params, Full Param List, Planner; SelectFirst() :18. Navigation real (destination sub-VM logic out of scope). |

---

## SetupView.axaml + ViewModels/SetupViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Host only — embeds BackstageView (axaml:14) | inherits SelectCommand | WORKS | `SetupViewModel : BackstageViewModel` registers ~35 pages (cs:8-55). Navigation works. The two ">> ..." group headers map to InfoPageViewModel placeholders (cs:13-15, 27-29) — land on info stubs. |

---

## InfoPageView.axaml + ViewModels/InfoPageViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| (none) | — | — | Title/Note TextBlocks only. Default note "Not yet ported." (InfoPageViewModel.cs:6). No interactive controls. |

---

## HelpView.axaml + ViewModels/HelpViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Check for Updates" (axaml:37) | none | DEAD | no Command/Click; HelpViewModel.cs:3 is empty |
| Button "Check for BETA Updates" (axaml:38) | none | DEAD | no Command/Click; nothing in HelpViewModel |

---

## SimulationView.axaml + ViewModels/SimulationViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| "Plane" selector (20-23) | none | DEAD | static TextBlocks in StackPanel, not clickable |
| "Rover" selector (24-27) | none | DEAD | inert TextBlock group |
| "Multirotor" selector (28-31) | none | DEAD | inert TextBlock group |
| "Helicopter" selector (32-35) | none | DEAD | inert TextBlock group |
| Status text (37) | binds Status | STUB | SimulationViewModel.cs:7 = "Select a firmware to simulate (SITL launch = TODO)." No SITL logic. |

---

## FlightPlannerView.axaml (+ .axaml.cs) + ViewModels/FlightPlannerViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Add WP" (28) | AddWaypointCommand | WORKS | `AddWaypoint` FlightPlannerViewModel.cs:182-194 |
| Button "X" Delete (57, DataGrid template) | DeleteWaypointCommand (param=row) | WORKS | `DeleteWaypoint` VM:196-202 |
| CheckBox "Grid" (74) | none (IsChecked ShowGrid) | DEAD | no Command/Click; `ShowGrid` (VM:55) never consumed |
| Button "View KML" (75) | none | DEAD | no Command/Click |
| Button "Inject Custom Map" (82) | none | DEAD | no Command/Click |
| Button "Load File" (84) | Click=OnLoadFile | WORKS | .cs:18 file picker + LoadFileAsync (real file IO, VM:151) |
| Button "Save File" (85) | Click=OnSaveFile | WORKS | .cs:37 file picker + SaveFileAsync (VM:118) |
| Button "Read" (87) | ReadWaypointsCommand | WORKS | `ReadWaypoints` VM:60-83 _comPort.getWPCount/getWP |
| Button "Write" (88) | WriteWaypointsCommand | WORKS | `WriteWaypoints` VM:85-116 setWPTotal/setWP/setWPACK |
| Button "Write Fast" (89-93) | WriteWaypointsCommand | WORKS (mislabeled) | bound to same command as "Write" (:92); no distinct fast-write path |
| Button "Set from Vehicle" (102) | SetHomeFromVehicleCommand | WORKS | `SetHomeFromVehicle` VM:204-216 reads MAV.cs lat/lng/alt |

---

## RawParamsView.axaml + ViewModels/RawParamsViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Refresh from vehicle" (15) | RefreshCommand | WORKS | `Refresh` RawParamsViewModel.cs:30-47 _comPort.getParamList() |
| Button "Write changes" (16) | WriteCommand | WORKS | `Write` VM:49-75 setParam over dirty rows |
| Button "Load Demo" (17) | LoadDemoCommand | WORKS (demo data) | `LoadDemo` VM:77-91 loads hardcoded sample params, not vehicle data |

---

## LogBrowseView.axaml (+ .axaml.cs) + ViewModels/LogBrowseViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Open log..." (OpenBtn, 15) | Click=OnOpen (.cs:12 OpenBtn.Click += OnOpen) | STUB | Handler .cs:15-35 real file picker, but `LoadFile` (VM:10-13) only sets Info to filename/size/path — does NOT parse the log. Button wired; feature is a stub. |

Note: suspicion of dead buttons here is incorrect — the button is wired via `.Click +=` in the constructor.

---

## Controls/ParamFieldsView.axaml + ViewModels/GCSViews/ConfigurationView/ParamField.cs, ParamPageBase.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Refresh Params" (18) | RefreshCommand | WORKS | `Refresh` ParamPageBase.cs:39-49 comPort.getParamList() + reload fields |
| ComboBox per-field (41, SelectedOption) | none (prop-changed) | WORKS | OnSelectedOptionChanged ParamField.cs:107 → Push (:114) → setParam (:129) |
| CheckBox per-field (48, Checked) | none (prop-changed) | WORKS | OnCheckedChanged ParamField.cs:101 → Push → setParam |
| NumericUpDown per-field (49, Value) | none (prop-changed) | WORKS | OnValueChanged ParamField.cs:95 → Push → setParam; offline writes local MAV.param cache |

---

## ConfigDroneCanView.axaml + ConfigDroneCanViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button {ConnectLabel} (24) | ToggleConnectCommand | WORKS | VM:40-57 doCommand(CAN_FORWARD), SLCAN bridge |
| Button "Refresh" (25) | RefreshCommand | WORKS | VM:59-68 clears Nodes, background CAN loop repopulates (no explicit re-request packet) |

---

## ConfigFirmwareLegacyView.axaml + ConfigFirmwareLegacyViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Refresh" (34) | RefreshCommand | WORKS | VM:64-65 APFirmware.GetList |
| Button "Upload Firmware" (54) | UploadCommand | WORKS | VM:109 downloads .apj, px4uploader flashes board |

---

## ConfigGpsInjectView.axaml + ConfigGpsInjectViewModel.cs (has uncommitted edits — audited on-disk)

| Control | Command | Status | Note |
|---|---|---|---|
| Button {ConnectLabel} (55) | ToggleConnectCommand | WORKS | VM:71-111 CommsNTRIP.Open, worker calls _comPort.InjectGpsData |

---

## ConfigHWBTView.axaml + ConfigHWBTViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Write" (53) | WriteCommand | WORKS | VM:78-126 opens SerialPort, sends HC-05/06 AT sequence |

---

## ConfigHWESP8266View.axaml + ConfigHWESP8266ViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Save" (74) | SaveCommand | WORKS | VM:158-211 setParam WIFI_* + doCommand(PREFLIGHT_STORAGE/REBOOT) |
| Button "Reset to defaults" (75) | ResetDefaultsCommand | WORKS | VM:213-240 doCommand(PREFLIGHT_STORAGE,2) |

---

## ConfigHWIDView.axaml + ConfigHWIDViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Refresh" (17) | LoadCommand | WORKS | VM:20-39 decodes _ID/_DEVID params into DataGrid |

---

## ConfigJoystickView.axaml + ConfigJoystickViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Refresh" (33) | RefreshDevicesCommand | WORKS | VM:71-91 JoystickBase.getDevices |
| Per-axis "Detect" (79) | DetectAxisCommand | WORKS | VM:149-160 getMovingAxis, setChannel |
| Per-button "Detect" (103) | DetectButtonCommand | WORKS | VM:162-173 getPressedButton |
| Button {EnableLabel} (132) | ToggleEnableCommand | WORKS | VM:93-135 starts/stops joystick + RC override |
| Button "Save" (133) | SaveCommand | WORKS | VM:137-147 saveconfig() |

---

## ConfigPlannerView.axaml + ConfigPlannerViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| All ~25 controls: Speech/Language/Units combos, Telemetry-rate numerics, display/HUD/reset checkboxes, Theme/Layout, GCS ID, Track Length (35-120) | none (bindings only) | DEAD | All bound only to observable props in ConfigPlannerViewModel.cs. No load from / persist to Settings/CurrentState. No Save/Apply button. Changing anything has no effect. |

---

## ConfigPX4FlowView.axaml + ConfigPX4FlowViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button {FocusLabel} (38) | ToggleFocusCommand | WORKS | VM:92-101 OpticalFlow.CalibrationMode |

---

## ConfigRadioInputView.axaml + ConfigRadioInputViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button {CalibrateLabel} (54) | ToggleCalibrateCommand | WORKS | VM:62-99 captures min/max, setParam(RCn_MIN/MAX) |

---

## ConfigScriptReplView.axaml + ConfigScriptReplViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| CheckBox "Auto scroll" (38) | none (AutoScroll bind) | STUB | prop VM:17-18 never read — cosmetic, no effect |
| Button "Run" (54) | RunCommand | STUB | VM:25-35 echoes input + prints "Python scripting is not available in this build" |
| Button "Clear" (60) | ClearCommand | WORKS | VM:37-41 clears buffer/output |

---

## ConfigSecureView.axaml (+ .axaml.cs) + ConfigSecureViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Get Session Key" (56) | GetSessionKeyCommand | WORKS | VM:32 SECURE_COMMAND_GET_SESSION_KEY |
| Button "Get Keys" (57) | GetKeysCommand | WORKS | VM:56 SECURE_COMMAND_GET_PUBLIC_KEYS |
| Button "Set Key..." (SetKeyBtn, 58) | Click=OnSetKey (code-behind :13/16) | WORKS* | file picker → vm.SetKeyFromFileAsync (VM:99). *Sends unsigned (no Ed25519 signer); target rejects if signature required (VM:140-146) |
| Button "Remove Keys" (59) | RemoveKeysCommand | WORKS* | VM:127, same unsigned caveat |

---

## ConfigTerminalView.axaml + ConfigTerminalViewModel.cs

| Control | Command | Status | Note |
|---|---|---|---|
| Button "Send" (44) | SendCommand | WORKS | VM:69-92 port.Write to BaseStream |
| Button "Clear" (52) | ClearCommand | WORKS | VM:94-97 clears output |

---

# Priority fixes (BROKEN and DEAD controls that should work)

No BROKEN bindings were found anywhere — every `Command=`/`Click=` resolves to an existing member. The gaps are DEAD controls (and a few high-value STUBs). Ranked by user impact:

### High priority — whole feature tabs are inert

1. **FlightData → Telemetry Logs tab (FlightDataView.axaml:410-427)** — Load Log, Play/Pause, playback slider, "Tlog > Kml or Graph", and all 7 speed buttons (10x/5x/2x/1x/0.5/0.25/0.1) are DEAD. The entire telemetry log playback feature does nothing.
2. **FlightData → DataFlash Logs tab (FlightDataView.axaml:438-445)** — Review a Log, Auto Analysis, Create KML + gpx, Convert Bin to Log, Create Matlab File, Geo Reference Images are all DEAD. Only "Download DataFlash Log Via Mavlink" (:436) works.
3. **FlightData → Transponder tab (FlightDataView.axaml:452-473)** — Connect to Transponder, STBY/ALT/ON/IDENT, Squawk, FlightID, Mode A/C/S, 1090ES are all DEAD (static mockup; comment at :452 says "no wiring").
4. **ConfigPlannerView.axaml (entire view)** — ~25 settings controls bound to a VM with no load/save logic and no Save button. The Planner config page does nothing. Wire to Settings/CurrentState and add Apply.

### Medium priority — feature visible but stubbed

5. **FlightData → Payload Control gimbal sliders (FlightDataView.axaml:389/397/401)** — Tilt/Pan/Roll sliders are DEAD; the `PointMountCommand` (:386) and `PointGimbalCommand` (:763) already exist in the VM but are bound to nothing. Wire the sliders' value-changed to those commands. "Reset Position" (:391) only zeroes local values (STUB).
6. **LogBrowseViewModel.cs:10-13** — "Open log..." button works but `LoadFile` only shows file metadata; no .tlog/.bin parsing. The log browser is non-functional past the picker.
7. **FlightData → Scripts (FlightDataView.axaml:376/378/380)** — Edit/Run/Abort are STUBs ("not bundled"); only Select Script works.
8. **HelpView.axaml:37-38** — "Check for Updates" / "Check for BETA Updates" DEAD (HelpViewModel empty).

### Low priority — minor / cosmetic

9. **FlightPlannerView.axaml:74/75/82** — "Grid" checkbox (ShowGrid never consumed), "View KML", "Inject Custom Map" are DEAD.
10. **FlightPlannerView.axaml:89-93** — "Write Fast" reuses WriteWaypointsCommand; no real fast-write path (mislabeled).
11. **SimulationView.axaml:20-35** — firmware selectors are static TextBlocks (DEAD); SITL launch is TODO (STUB).
12. **ConfigScriptReplView.axaml:38** — "Auto scroll" checkbox bound to a property nothing reads (STUB).
13. **FlightData HUD video/GStreamer menu (FlightDataView.axaml:53-61)** — 9 menu items + Set Aspect Ratio all STUB ("not ported"). Raw Sensor View, Joystick, Clear Track, Message (:215/227/229/232) are STUBs that only log.
