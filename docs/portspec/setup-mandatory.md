# SETUP section — port spec (mandatory hardware)

Source of truth: Mission Planner 1.3.83 submodule under `external/MissionPlanner/`.
Target: 1:1 Avalonia port under `src/MissionPlannerAvalonia/`.

This document covers:
- **Part A** — the full SETUP navigation tree (`GCSViews/InitialSetup.cs`).
- **Part B** — control-tree + functionality spec for each Mandatory Hardware page, plus Avalonia status.

> Note on file location: the host control is `external/MissionPlanner/GCSViews/InitialSetup.cs` /
> `InitialSetup.Designer.cs` (in `GCSViews/`, **not** `GCSViews/ConfigurationView/`). All page
> UserControls referenced live in `GCSViews/ConfigurationView/`.

---

## PART A — SETUP navigation tree

The tree is built dynamically at runtime in `InitialSetup.HardwareConfig_Load` (`GCSViews/InitialSetup.cs:155-395`)
via `AddBackstageViewPage(type, headerText, enabled, parent, advanced)`. It is a **BackstageView** (left-rail
navigation with collapsible top-level groups). Pages appear only when:
- their `MainV2.DisplayConfiguration.displayXxx` flag is true, **and**
- their `enabled` condition holds (connection state / firmware type / params present).

Group nodes are created without a parent (top-level); child pages pass `mand`/`opt`/`adv` as `Parent`.
Header label strings come from `InitialSetup.resx` (`rm.GetString("backstageViewPage*.Text")`).

Tree order (exactly as added; conditional pages noted in parentheses):

- **Loading** → `ConfigParamLoading` — shown only while params still downloading (`!gotAllParams` && connected). `InitialSetup.cs:159-163`
- **Install Firmware** *(group `displayInstallFirmware`)* — `InitialSetup.cs:165-176`
  - **Install Firmware** → `ConfigFirmwareDisabled` (when **connected**)
  - **Install Firmware** → `ConfigFirmwareManifest` (when **disconnected**) — the current/default firmware page
  - **Install Firmware Legacy** → `ConfigFirmware` (when **disconnected**) — label = `"Install Firmware" + " Legacy"`
- **Secure** → `ConfigSecureAP` (when disconnected) — `InitialSetup.cs:178-179`
- **Mandatory Hardware** *(group, var `mand`; enabled when connected && gotAllParams)* → `ConfigMandatory` — `InitialSetup.cs:182`
  - **Heli Setup** → `ConfigTradHeli4` (isHeli) — `:187`
  - **Frame Type** → `ConfigFrameType` (isCopter && !isCopter35plus) — legacy single-`FRAME` page — `:188`
  - **Frame Type** → `ConfigFrameClassType` (has `FRAME_CLASS` || (isCopter && isCopter35plus)) — modern class+type page — `:189-191`
  - **Accel Calibration** → `ConfigAccelerometerCalibration` *(displayAccelCalibration)* — `:194-197`
  - **Compass** → `ConfigHWCompass2` (if param `COMPASS_PRIO1_ID` exists) **else** `ConfigHWCompass` *(displayCompassConfiguration)* — `:200-208`
  - **Radio Calibration** → `ConfigRadioInput` *(displayRadioCalibration)* — `:209-212`
  - **Servo Output** → `ConfigRadioOutput` *(displayServoOutput)* — `:213-217`
  - **Serial Ports** → `ConfigSerial` *(displaySerialPorts)* — `:218-221`
  - **ESC Calibration** → `ConfigESCCalibration` *(displayEscCalibration)* — `:222-225`
  - **Flight Modes** → `ConfigFlightModes` *(displayFlightModes)* — `:226-229`
  - **FailSafe** → `ConfigFailSafe` *(displayFailSafe)* — `:230-233`
  - **Initial Tune Parameter** → `ConfigInitialParams` ((isCopter || isQuadPlane) && displayInitialParams) — `:235-238`
  - **HW ID** → `ConfigHWIDs` *(displayHWIDs)* — `:240-241`
  - **ADSB** → `ConfigADSB` *(displayADSB; note: added with parent `mand`)* — `:262-263`
- **Optional Hardware** *(group, var `opt`)* → `ConfigOptional` — `InitialSetup.cs:243`
  - **RTK/GPS Inject** → `ConfigSerialInjectGPS` *(displayRTKInject)* — `:244-252`
  - **CubeID Update** → `ConfigCubeID` (when connected) — `:254-255`
  - **Sik Radio** → `Sikradio` *(displaySikRadio)* — `:257-260`
  - **CAN GPS Order** → `ConfigGPSOrder` *(displayGPSOrder)* — `:265-266`
  - **Battery Monitor** → `ConfigBatteryMonitoring` *(displayBattMonitor)* — `:268-272`
  - **Battery Monitor 2** → `ConfigBatteryMonitoring2` *(displayBattMonitor)* — `:271`
  - **DroneCAN/UAVCAN** → `ConfigDroneCAN` *(displayCAN)* — `:273-277`
  - **Joystick** → `Joystick.JoystickSetup` *(displayJoystick)* — `:278-281`
  - **Compass/Motor Calib** → `ConfigCompassMot` *(displayCompassMotorCalib)* — `:283-286`
  - **Range Finder** → `ConfigHWRangeFinder` *(displayRangeFinder)* — `:287-290`
  - **Airspeed** → `ConfigHWAirspeed` *(displayAirSpeed)* — `:291-294`
  - **PX4Flow** → `ConfigHWPX4Flow` *(displayPx4Flow)* — `:295-298`
  - **Optical Flow** → `ConfigHWOptFlow` *(displayOpticalFlow)* — `:299-302`
  - **OSD** → `ConfigHWOSD` *(displayOsd)* — `:303-306`
  - **Camera Gimbal** → `ConfigMount` *(displayCameraGimbal)* — `:307-310`
  - **Antenna tracker** → `ConfigAntennaTracker` (isTracker, displayAntennaTracker) — `:311-314`
  - **Motor Test** → `ConfigMotorTest` *(displayMotorTest)* — `:315-318`
  - **Bluetooth Setup** → `ConfigHWBT` *(displayBluetooth)* — `:319-322`
  - **Parachute** → `ConfigHWParachute` *(displayParachute)* — `:323-326`
  - **ESP8266 Setup** → `ConfigHWESP8266` *(displayEsp)* — `:327-330`
  - **Antenna Tracker** → `Antenna.TrackerUI` *(displayAntennaTracker)* — `:331-334`
  - **FFT Setup** → `ConfigFFT` *(displayFFTSetup)* — `:335-338`
- **Advanced** *(group, var `adv`; only when `isAdvancedMode`)* → `ConfigAdvanced` — `InitialSetup.cs:340-342`
  - **Terminal** → `ConfigTerminal` *(displayTerminal)* — `:344-347`
  - **Script REPL** → `ConfigREPL` (connected, displayREPL) — `:349-352`
- **Plugin pages** — any registered via `AddPluginViewPage(...)`, filtered by `pageOptions` flags. `:356-382`

Behaviour notes:
- The last-viewed page name is remembered (`lastpagename`) and re-activated on reload (`:384-392`).
- `ThemeManager.ApplyThemeTo(this)` themes the whole rail on load.
- `pageOptions` flags (`InitialSetup.cs:21-35`): `isConnected, isDisConnected, isTracker, isCopter, isCopter35plus, isHeli, isQuadPlane, isPlane, isRover, gotAllParams`.

---

## PART B — Mandatory Hardware page specs

Custom MP controls referenced throughout:
- `MavlinkCheckBox` / `MavlinkComboBox` / `MavlinkNumericUpDown` — auto-bind to a MAVLink param via `.setup(...)`,
  read on activate and **write the param immediately on user change** (no save button). Combo option lists come from
  `ParameterMetaDataRepository.GetParameterOptionsInt(paramName, firmware)`.
- `HorizontalProgressBar2` / `VerticalProgressBar2` / `HorizontalProgressBar` — custom range bars with a `Label`,
  `minline`/`maxline` overlay markers, `reverse`, and configurable colors.
- `MyButton`, `MyLabel`, `MyDataGridView`, `PictureBoxWithPseudoOpacity` (supports `Opacity` for fade via the
  `Transitions` lib), `ImageLabel` (image + caption, clickable).

---

### Install Firmware Legacy (`ConfigFirmware`)

Base `MyUserControl`, `IActivate, IDeactivate`. Flat absolute layout (bounds in `.resx`).

- **Controls** (`ConfigFirmware.Designer.cs`):
  - Airframe tiles, type `ImageLabel` (`Cursor=Hand`, `TabStop=false`, `Tag=""`, all → `pictureBoxFW_Click`); caption `.Text` set at runtime from the firmware list, `.Image` is a fixed resource:
    - `pictureBoxAPM` img `APM_airframes_001` (Plane) — `:83-89`
    - `pictureBoxQuad` img `FW_icons_2013_logos_04` (Copter Quad) — `:91-99`
    - `pictureBoxHexa` img `FW_icons_2013_logos_10` (Hexa) — `:101-109`
    - `pictureBoxTri` img `FW_icons_2013_logos_08` (Tri) — `:111-119`
    - `pictureBoxY6` img `y6a` (Y6) — `:121-129`
    - `pictureBoxHeli` img `APM_airframes_08` (Trad Heli) — `:147-155`
    - `pictureBoxOcta` img `FW_icons_2013_logos_12` (Octa) — `:157-164`
    - `pictureBoxOctaQuad` img `x8` (Octa Quad / X8) — `:166-173`
    - `pictureBoxRover` img `rover_11` (Rover) — `:175-183`
    - `pictureAntennaTracker` img `Antenna_Tracker_01` — `:234-242`
    - `pictureBoxSub` img `sub` (ArduSub) — `:258-265`
    - `imageLabel1` img `pixhawk2cube` → `picturebox_ph2_Click` (vendor link, not a flash) — `:267-274`
  - `lbl_status` Label (flash status) `:131-134`; `progress` ProgressBar (`Step=1`) `:136-140`; `label2` `:142-145`; `label1` `:185-188`.
  - `CMB_history` ComboBox (DropDownList, version history) → `CMB_history_SelectedIndexChanged` `:190-197`.
  - Advanced-mode-only clickable labels: `CMB_history_label` `:199-204`, `lbl_Custom_firmware_label` (load custom fw) `:206-211`, `lbl_devfw` (beta/dev fw) `:213-218`, `lbl_dlfw` (download site) `:220-225`.
  - `lbl_px4bl` (reboot to bootloader) → `lbl_px4bl_Click` `:227-232`; `lbl_licence` → `lbl_dlfw_Click` `:244-249`; `linkLabel1` LinkLabel (motor-order wiki) `:251-256`.
  - All caption strings are in `.resx`; tile captions are overwritten at runtime with firmware names + suffix (" Quad", " Hexa", " Y6", " heli", " Octa", " Octa Quad", " Tri").
- **Functionality** (`ConfigFirmware.cs`):
  - `Activate()` (`:34-64`): first run calls `UpdateFWList()`, subscribes `MainV2.instance.DeviceChanged`; advanced-mode toggles visibility of dev/custom/dl/history labels.
  - `Instance_DeviceChanged` (`:66-106`): on USB arrival scans ports, `px4uploader.Uploader(port,115200).identify()` to detect board/rev/bl-rev/fw-max/chip.
  - `ProcessCmdKey` (`:108-129`): **Ctrl+Q** = trunk/dev firmware (sets `firmwareurl` to `dev/firmwarelatest.xml`, hides history); **Ctrl+P** = upload px4 from list.
  - `UpdateFWList` / `pdr_DoWork` (`:131-167`): `Firmware.getFWList(firmwareurl)`, then `updateDisplayName` assigns each software to its tile.
  - `ConvertToOld` (`:169-215`): maps `APFirmware.FirmwareInfo` → legacy `Firmware.software`, picking URLs per platform (`apm1/apm2`, `px4-v1..v4`, `fmuv2..v5`, `mindpx-v2`, `bebop2`, `disco`).
  - `updateDisplayName` (`:270-389`): substring dispatcher matching each fw URL/name/desc to a tile; copter-generic match fans firmware to all copter tiles.
  - `findfirmware(fw)` (`:393-464`): core flash — confirm (`Strings.AreYouSureYouWantToUpload`), close MAVLink stream, optionally rewrite urls for selected history version, gather COM ports, call `fw.updateLegacy(comPortName, fw, history, ports)`; AC3.1/3.2 post-warnings.
  - `pictureBoxFW_Click` (`:466-475`): validate `Tag` is `Firmware.software` else `Strings.ErrorFirmwareFile`, then `findfirmware`.
  - `CMB_history_SelectedIndexChanged` (`:496-501`); `CMB_history_label_Click` (`:504-518`); `Custom_firmware_label_Click` (`:521-595`, OpenFileDialog `*.hex;*.px4;*.vrx;*.apj`, Solo prompt, `BoardDetect.DetectBoard`, `fw.UploadFlash`); `lbl_devfw_Click` (`:597-604`); `lbl_dlfw_Click` → `https://firmware.ardupilot.org/` (`:607-617`); `lbl_px4bl_Click` (`:619-639`, `doReboot(true,false)` into bootloader); `linkLabel1_LinkClicked` motor-order wiki (`:641-651`); `picturebox_ph2_Click` proficnc URL (`:653-663`); `Deactivate` unsubscribes (`:665-673`).
  - **MAVLink params:** none. This page only flashes via the px4uploader / `Firmware` pipeline + serial bootloader.
- **Avalonia status:** **PARTIAL.** `ViewModels/Setup/SetupActionPages.cs` `InstallFirmwareViewModel` is an action-button stub (vehicle buttons that only log "not yet wired"); `ViewModels/GCSViews/ConfigurationView/ConfigFirmwareLegacyViewModel.cs` (329 lines) is the legacy list/download page. Gap: no airframe tile grid, no actual px4uploader flashing/bootloader/board-detect, no custom/dev/history flows. (Matches repo note: legacy = list/download only.)

---

### Frame Type (`ConfigFrameClassType`) — modern class + type selector

Base `MyUserControl`, `IActivate, IDeactivate`. Used when the vehicle exposes both `FRAME_CLASS` and `FRAME_TYPE`.

- **Controls** (`ConfigFrameClassType.Designer.cs`): two side-by-side groupboxes.
  - `groupBox3` (`:241-253`) — **FRAME_CLASS** picker: image radio buttons (`BackColor=Black`, `ForeColor=White`, images resized 60×60 in ctor), all → `radioButtonClass_CheckedChanged`:
    - `radioButtonUndef` (no image, Undefined) `:332-340`; `radioButtonQuad` `FW_icons_2013_logos_03` `:321-330`; `radioButtonHexa` `..._09` `:310-319`; `radioButtonOcta` `..._12` `:299-308`; `radioButtonOctaQuad` `..._06` `:255-264`; `radioButtonY6` `..._07` `:288-297`; `radioButtonHeli` `..._13` `:277-286`; `radioButtonTri` `..._08` `:266-275`.
  - `groupBox2` (`:194-219`) — **FRAME_TYPE** picker (enabled only when the class has valid types): paired `PictureBoxWithPseudoOpacity` (`Cursor=Hand`) + radio per type, plus description labels `label1`–`label7`,`label9` (resx). Both picturebox `Click` and radio `CheckedChanged` → `radioButtonType_CheckedChanged`:
    - `radioButton_Plus`/`pictureBoxPlus` img `frames_plus` `:75-80,100-107`; `radioButton_X`/`pictureBoxX` img `frames_x` `:82-88,109-116`; `radioButton_V`/`pictureBoxV` img `new_3DR_04` `:123-129,131-138`; `radioButton_H`/`pictureBoxH` img `frames_h` `:145-151,153-160`; `radioButton_Y`/`pictureBoxY` img `y6b` `:172-178,180-187`; `radioButton_VTail`/`pictureBoxVTail` (no fixed image) `:226-239`.
    - `radio_type_other` — hidden default radio (`Checked=true`), neutral "none selected" sink, not wired `:342-348`.
- **Functionality** (`ConfigFrameClassType.cs`):
  - Ctor (`:22-34`) resizes class radio images to 60×60.
  - `Activate()` (`:36-54`): if `FRAME_CLASS` or `FRAME_TYPE` missing → `Enabled=false`. Else parse `work_frame_class`/`work_frame_type` (enums `motor_frame_class`/`motor_frame_type`), call `DoClass` + `DoType`.
  - `DoClass(class)` (`:61-167`): computes valid types from `ArduPilot.Common.ValidList`; disables `groupBox2` if none; checks the class radio; fades all type pictureboxes to `DisabledOpacity` (0.2), fades-in (`EnabledOpacity` 1.0) only valid types; calls `SetFrameParam`.
  - `DoType(type)` (`:169-279`): fades chosen type in, others out; sets radios; `SetFrameParam`.
  - `SetFrameParam(class,type)` (`:281-293`): **writes `FRAME_CLASS` and `FRAME_TYPE`** via `setParam`; error → `Strings.ErrorSetValueFailed`.
  - `FadePicBoxes` (`:295-300`): 400 ms linear `Opacity` via `Transitions`. `radioButtonType_CheckedChanged` (`:302-316`), `radioButtonClass_CheckedChanged` (`:318-336`).
  - **Reads:** `FRAME_CLASS`, `FRAME_TYPE`. **Writes:** same, immediately on selection (no save button). Note Plane variants `Q_FRAME_CLASS`/`Q_FRAME_TYPE` are handled by the same param family on QuadPlane.
- **Avalonia status:** **PARTIAL.** `ConfigParamPages.cs` `ConfigFrameClassTypeViewModel` (`:230-238`) is a param-grid (`FRAME_CLASS`, `FRAME_TYPE`, `Q_FRAME_CLASS`, `Q_FRAME_TYPE` as combos). Gap: none of the visual airframe image/radio grid, fade animation, or class→valid-type gating.

---

### Frame Type (`ConfigFrameType`) — legacy single-`FRAME` selector

Base `MyUserControl`, `IActivate, IDeactivate`. Old copter firmware (single `FRAME` param, no class).

- **Controls** (`ConfigFrameType.Designer.cs`): two groupboxes.
  - `groupBox1` (`:187-192`) — single child `configDefaultSettings1`, type `Controls.DefaultSettings` (vehicle default-settings selector, raises `OnChange`) `:194-197`.
  - `groupBox2` (`:199-223`) — same type grid as the class page but with **separate dedicated handlers** per control:
    - `radioButton_Plus`(`radioButton_Plus_CheckedChanged`)/`pictureBoxPlus` img `frames_plus`(`pictureBoxPlus_Click`) `:67-73,93-100`; `_X` img `frames_x` `:75-81,102-109`; `_V` img `new_3DR_04` `:116-122,124-131`; `_H` img `frames_h` `:138-144,146-153`; `_Y` img `y6b` `:165-171,173-180`; `_VTail` (no fixed image) `:230-244`. Labels `label1`–`label7`,`label9` (resx). All radios `TabStop=true`; no `radio_type_other`, no class box.
- **Functionality** (`ConfigFrameType.cs`):
  - Ctor (`:18-23`) subscribes `configDefaultSettings1.OnChange`.
  - `Activate()` (`:25-34`): if `FRAME` missing → `Enabled=false`; else parse `Frame` enum and `DoChange(frame)`.
  - `configDefaultSettings1_OnChange` (`:41-44`) re-runs `Activate()`.
  - `DoChange(frame)` (`:46-154`): per case (Plus/X/V/H/Y/VTail) fade chosen in / others out, set radios, `SetFrameParam`.
  - `SetFrameParam(frame)` (`:156-167`): **writes `FRAME`** via `setParam("FRAME",(int)frame)`; error → `Strings.ErrorSetValueFailed`.
  - `Deactivate` sets `giveComport=false` (`:36-39`). Enum `MissionPlanner.ArduPilot.Frame` = {Plus,X,V,H,Y,VTail,…}.
  - **Reads/Writes:** `FRAME` (immediately; no save button).
- **Avalonia status:** **MISSING.** No dedicated legacy `ConfigFrameType` ViewModel; only the modern `ConfigFrameClassTypeViewModel` exists. Gap: legacy single-`FRAME` page (incl. `DefaultSettings` selector) not ported.

---

### Accel Calibration (`ConfigAccelerometerCalibration`)

Base `MyUserControl`, `IActivate, IDeactivate`. Flat layout, no groupboxes (`Designer.cs:108-116`).

- **Controls**:
  - `label5` Label `"Accelerometer Calibration"` (title, ForeColor WhiteSmoke) `:47-49`; `lineSeparator2` `LineSeparator` divider `:69-77`.
  - Calibrate-Accel section: `label4` `"Level your Autopilot to set default accelerometer Min/Max (3 axis).\nThis will ask you to place your autopilot on each edge."` `:51-54`; `BUT_calib_accell` `MyButton` `"Calibrate Accel"` → `BUT_calib_accell_Click` `:61-67`; `lbl_Accel_user` Label (runtime position-prompt/status) `:56-59`.
  - Calibrate-Level section: `label1` `"Level your Autopilot to set default accelerometer offsets (1 axis/AHRS trims).\nThis requires you to place your autopilot flat and level."` `:95-98`; `BUT_level` `MyButton` `"Calibrate Level"` → `BUT_level_Click` `:79-85`.
  - Simple-Accel section: `label2` `"Level your Autopilot to set default accelerometer scale factors for level flight (1 axis).\nThis requires you to place your autopilot flat and level."` `:100-103`; `BUT_simpleAccelCal` `MyButton` `"Simple Accel Cal"` → `BUT_simpleAccelCal_Click` `:87-93`.
- **Functionality** (`ConfigAccelerometerCalibration.cs`):
  - `Activate()` (`:27-31`) enables Calibrate-Accel, `_incalibrate=false`. `Deactivate()` (`:33-37`) `giveComport=false`.
  - `BUT_calib_accell_Click` (`:39-89`) — stateful 6-position cal toggle:
    - First click: `doCommand(PREFLIGHT_CALIBRATION, 0,0,0,0, 1, 0,0)` (param5=1). On success `_incalibrate=true`, subscribes `STATUSTEXT` + `COMMAND_LONG` → `receivedPacket`, button Text → `Strings.Click_when_Done`. Failure → `CommandFailed`.
    - Next clicks: increments `count`, sends `command_long` `ACCELCAL_VEHICLE_POS` with `param1=(float)pos` to advance to the next position.
  - `receivedPacket` (`:91-132`): `STATUSTEXT` → `UpdateUserMessage`; on "calibration successful"/"failed" sets button → `Strings.Done`, disables, `_incalibrate=false`, unsubscribes. `COMMAND_LONG` with `ACCELCAL_VEHICLE_POS` → stores `pos`, prompts "Please place vehicle <pos>" (LEVEL/LEFT/RIGHT/NOSEDOWN/NOSEUP/BACK…).
  - `UpdateUserMessage` (`:134-141`) updates `lbl_Accel_user` when text contains "place vehicle"/"calibration".
  - `BUT_level_Click` (`:143-163`): `doCommand(PREFLIGHT_CALIBRATION,…, 2, …)` (param5=2 level/AHRS-trim), success → `Strings.Completed`.
  - `BUT_simpleAccelCal_Click` (`:165-185`): `doCommand(PREFLIGHT_CALIBRATION,…, 4, …)` (param5=4 simple), success → `Strings.Completed`.
  - **MAVLink:** commands `PREFLIGHT_CALIBRATION` (param5 = 1/2/4) + `ACCELCAL_VEHICLE_POS`; consumes `STATUSTEXT`, `COMMAND_LONG`. No params read/written.
- **Avalonia status:** **PARTIAL.** `Setup/ConfigCalibrationPages.cs` `ConfigAccelCalibrationViewModel` (`:7-52`) has the three actions ("Calibrate Accel" p5=1, "Calibrate Level" p5=2, "Simple Accel Cal" p5=4) via `doCommand` + a log. Gap: no interactive 6-position step flow (no `ACCELCAL_VEHICLE_POS` advance handshake, no per-position "place vehicle" prompts / STATUSTEXT-driven completion).

---

### Compass (`ConfigHWCompass`)

Base `MyUserControl`, `IActivate` (+ `Deactivate`). Constants `THRESHOLD_OFS_RED=600`, `THRESHOLD_OFS_YELLOW=400` (`cs:15-16`). Enum `CompassNumber{Compass1=0,Compass2,Compass3}` for the primary combo. (When `COMPASS_PRIO1_ID` exists the host loads `ConfigHWCompass2` instead.)

- **Controls** (`ConfigHWCompass.Designer.cs`, root adds at `:505-518`):
  - `label5` `"Compass"` `:115-118`; `groupBox2` empty frame `:120-124`; `label1` `"Select device to quick-configure parameters:"` `:133-136`.
  - Quick-config `MyButton`s: `buttonQuickPixhawk` `"Pixhawk/PX4"` `:489-494`; `QuickAPM25` `"APM2.5 (Internal Compass)"` `:482-487`; `buttonAPMExternal` `"APM and External Compass"` `:475-480`; `but_largemagcal` `"Large Vehicle MagCal"` `:496-501`.
  - `groupBoxGeneralSettings` `"General Compass Settings"` (`:286-299`): `CMB_primary_compass` MavlinkComboBox `:301-308`; `LBL_primary_compass` `"Primary Compass:"` `:310-313`; `CHK_compass_learn` MavlinkCheckBox `"Automatically learn offsets"` → `CHK_compasslearn_CheckedChanged` `:315-323`; `CHK_autodec` CheckBox `"Obtain declination automatically"` → `CHK_autodec_CheckedChanged` `:108-113`; `label2` `"Degrees"`, `label3` `"Minutes"` `:144-152`; `TXT_declination_deg` TextBox `:102-106`; `TXT_declination_min` TextBox `:138-142`; `linkLabelmagdec` LinkLabel `"Declination WebSite"` `:95-100`.
  - `groupBoxCompass1` `"Compass #1"` (`:325-334`): `CMB_compass1_orient` MavlinkComboBox `:366-373`; `CHK_compass1_use` `"Use this compass"` → `CHK_compass` `:356-364`; `CHK_compass1_external` `"Externally mounted"` → `CHK_compass` `:346-354`; `LBL_compass1_offset` `"OFFSET"` (runtime → "OFFSETS X:…,Y:…,Z:…" color-coded) `:341-344`; `LBL_compass1_mot` `"MOT"` `:336-339`.
  - `groupBoxCompass2` `"Compass #2"` and `groupBoxCompass3` `"Compass #3"` — identical structure (`CMB_compassN_orient`, `CHK_compassN_use`, `CHK_compassN_external`, `LBL_compassN_offset`, `LBL_compassN_mot`) `:375-473`.
  - `label4` `"OR"` `:281-284`.
  - `groupBoxmpcalib` `"Mission Planner Mag Calibration"` (`:266-272`): `BUT_MagCalibrationLive` `"Live Calibration"` → `BUT_MagCalibration_Click` `:274-279`; `linkLabel1` `"Youtube Example"` `:126-131`.
  - `groupBoxonboardcalib` `"Onboard Mag Calibration"` (`:154-171`): `BUT_OBmagcalstart` `"Start"` `:255-260`; `BUT_OBmagcalcancel` `"Cancel"` `:248-253`; `BUT_OBmagcalaccept` `"Accept"` `:241-246`; `lbl_obmagresult` ReadOnly multiline TextBox `:235-239`; `horizontalProgressBar1/2/3` (`HorizontalProgressBar`, DrawLabel) `:208-233`; `label7/8/9` `"Mag 1/2/3"` `:193-206`; `label10` `"Fitness"` `:179-182`; `mavlinkComboBoxfitness` (COMPASS_CAL_FIT) `:184-191`; `label6` `"Relax fitness if calibration fails"` (Red) `:173-177`.
  - `timer1` Timer → `timer1_Tick` `:262-264`.
- **Functionality** (`ConfigHWCompass.cs`):
  - `Activate()` (`:31-243`): if not connected → `Enabled=false`. `startup=true`. Quick-config buttons hidden when version >3.2.1 && ArduCopter2 (`:42-49`). Onboard-vs-MP-cal visibility (`:51-70`): show onboard when ≥3.7.1 & Plane or Ctrl held; else by `COMPASS_CALIBRATION` capability bit.
    - General: `CHK_compass_learn.setup("COMPASS_LEARN")`; reads `COMPASS_DEC` (rad→deg) → deg/min textboxes; reads `COMPASS_AUTODEC` → `CHK_autodec` (`:74-88`).
    - Compass 1 (`:91-134`): setups `COMPASS_USE`, `COMPASS_EXTERNAL`, `COMPASS_ORIENT`; if `COMPASS_OFS_X` absent → disable. Reads `COMPASS_OFS_X/Y/Z` (color: red if absmax>600, yellow >400, red if all 0 = uncalibrated, else green) → `LBL_compass1_offset`; reads `COMPASS_MOT_X/Y/Z` → `LBL_compass1_mot`.
    - Compass 2 (`:136-183`): only if `COMPASS_EXTERN2` exists; setups `COMPASS_USE2`, `COMPASS_EXTERN2`, `COMPASS_ORIENT2`, primary combo `CMB_primary_compass.setup(typeof(CompassNumber),"COMPASS_PRIMARY")`; reads `COMPASS_OFS2_*`, `COMPASS_MOT2_*`.
    - Compass 3 (`:185-228`): only if `COMPASS_EXTERN3` exists; `COMPASS_EXTERN3`, `COMPASS_USE3`, `COMPASS_ORIENT3`, `COMPASS_OFS3_*`, `COMPASS_MOT3_*`.
    - Fitness combo `COMPASS_CAL_FIT` (`:230-231`); `ShowRelevantFields()`; `startup=false`.
  - `ShowRelevantFields()` (`:735-756`): decl textboxes enabled when not auto-dec; orient combo visible only when that compass is external; MOT+OFFSET labels visible only when that compass is "used"; primary combo visible only if `COMPASS_PRIMARY` exists.
  - Declination: `TXT_declination_Validated` (`:283-322`) writes `COMPASS_DEC = dec*deg2rad`; `CHK_autodec_CheckedChanged` (`:375-406`) writes `COMPASS_AUTODEC`; `linkLabel1_LinkClicked` → magnetic-declination.com.
  - `CHK_compasslearn_CheckedChanged` (`:710-727`) writes `COMPASS_LEARN`. `CHK_enablecompass_CheckedChanged` (`:324-360`) would write `MAG_ENABLE` (mostly commented, not wired).
  - MP cal: `BUT_MagCalibration_Click` (`:257-261`) → `MagCalib.DoGUIMagCalib()` then `Activate()`; YouTube link (`:408-418`).
  - Onboard mag cal (`:420-610`): buffers `MAG_CAL_PROGRESS`/`MAG_CAL_REPORT`. `BUT_OBmagcalstart_Click` → `doCommand(DO_START_MAG_CAL,0,1,1,…)`, subscribes, enables Accept/Cancel, starts timer. `BUT_OBmagcalaccept_Click` → `DO_ACCEPT_MAG_CAL`. `BUT_OBmagcalcancel_Click` → `DO_CANCEL_MAG_CAL`. `timer1_Tick` (`:515-610`) drives the 3 progress bars by `compass_id`, prints per-id report, on all-autosaved shows "Please reboot the autopilot".
  - Quick-config buttons (write COMPASS params then `Activate()`): `buttonQuickPixhawk_Click` (`:612-652`) sets `COMPASS_USE/USE2/USE3/EXTERNAL/EXTERN2/EXTERN3/PRIMARY/LEARN` + orient (`ROTATION_NONE` or `ROTATION_ROLL_180` per FW prompt); `QuickAPM25_Click` (`:654-679`); `buttonAPMExternal_Click` (`:681-708`).
  - `but_largemagcal_Click` (`:758-780`): InputBox heading → `doCommand(FIXED_MAG_CAL_YAW, heading, …)`; success → `Strings.Completed`.
  - **Params read:** `COMPASS_LEARN, COMPASS_DEC, COMPASS_AUTODEC, COMPASS_USE[2/3], COMPASS_EXTERNAL/EXTERN2/EXTERN3, COMPASS_ORIENT[2/3], COMPASS_OFS[2/3]_X/Y/Z, COMPASS_MOT[2/3]_X/Y/Z, COMPASS_PRIMARY, COMPASS_CAL_FIT`. **Written:** `COMPASS_DEC, COMPASS_AUTODEC, COMPASS_LEARN`, quick-config USE/EXTERN/PRIMARY/orient, (`MAG_ENABLE` commented). **Commands:** `DO_START/ACCEPT/CANCEL_MAG_CAL, FIXED_MAG_CAL_YAW`. **Messages:** `MAG_CAL_PROGRESS, MAG_CAL_REPORT`.
- **Avalonia status:** **PARTIAL.** `Setup/ConfigCalibrationPages.cs` `ConfigCompassViewModel` (`:54-96`) only does onboard mag cal Start/Accept/Cancel (`DO_*_MAG_CAL`) + log. Gap: no per-compass use/external/orient/offset/MOT display, declination, primary selector, quick-config buttons, large-vehicle yaw cal, or live progress bars. Separate `ConfigHWCompass2` variant not ported.

---

### Radio Calibration (`ConfigRadioInput`)

Base `MyUserControl`, `IActivate, IDeactivate`. Layout positions from `.resx`.

- **Controls** (`ConfigRadioInput.Designer.cs`):
  - Vertical bars (`VerticalProgressBar2`, Min 800/Max 2200): `BARpitch` `"Pitch"` `:200-214`; `BARthrottle` `"Throttle"` (Value 1000, magenta) `:216-230`.
  - Horizontal bars (`HorizontalProgressBar2`): `BARroll` `"Roll"` `:248-262`; `BARyaw` `"Yaw"` `:232-246`; `BAR5`..`BAR16` `"Radio 5"`…`"Radio 16"` `:136-400`. (4 attitude + 12 numbered = 16 channels.)
  - `BUT_Calibrateradio` `MyButton` `"Calibrate Radio"` → `BUT_Calibrateradio_Click` `:129-134`.
  - Reverse `MavlinkCheckBox` (all Text `"Reverse"`): `CHK_revroll`(name `CHK_revch1`) `:432-440`; `CHK_revpitch`(`CHK_revch2`) `:422-430`; `CHK_revthr`(`CHK_revch3`) `:402-410`; `CHK_revyaw`(`CHK_revch4`) `:412-420`.
  - `groupBoxElevons` `"Elevon Config"` (`:70-78`): `CHK_mixmode` `"Elevons"` `:80-84`; `CHK_elevonrev` `"Elevons Rev"` `:94-98`; `CHK_elevonch1rev` `"Elevons CH1 Rev"` `:100-105`; `CHK_elevonch2rev` `"Elevons CH2 Rev"` `:87-91`.
  - `groupBox1` `"Spektrum Bind"` (`:264-271`): `BUT_BindDSM2` `"Bind DSM2"` `:122-127`; `BUT_BindDSMX` `"Bind DSMX"` `:115-120`; `BUT_BindDSM8` `"Bind DSM8"` `:108-113`.
  - `currentStateBindingSource` → `CurrentState` `:442-444`.
- **Functionality** (`ConfigRadioInput.cs`):
  - Live: 100 ms Timer started in `Activate()`; `timer_Tick` → `cs.UpdateCurrentSettings(...)`.
  - Channel map: reads `RCMAP_ROLL/PITCH/THROTTLE/YAW` (default 1/2/3/4); binds each bar `Value` to `currentStateBindingSource` `ch{n}in`; appends `(rcN)` to attitude labels; requests `RC_CHANNELS` at 2 Hz.
  - Elevon (Plane only): `CHK_mixmode`→`ELEVON_MIXING`, `CHK_elevonrev`→`ELEVON_REVERSE`, `CHK_elevonch1rev`→`ELEVON_CH1_REV`, `CHK_elevonch2rev`→`ELEVON_CH2_REV`.
  - Reverse: `setup({-1,1},{1,0},{"RC{ch}_REV","RC{ch}_REVERSED"})` — dual param (old `RCx_REV` -1/1, new `RCx_REVERSED` 1/0). `reverseChannel` (`:439-469`) recolors bar; if not startup and `SWITCH_ENABLE==1` sets `SWITCH_ENABLE=0`.
  - Calibration `BUT_Calibrateradio_Click` (`:197-406`): toggle. Warn props/tx on; bump raterc=10, request `RC_CHANNELS` 10 Hz; "move all sticks/switches to extremes" loop captures per-channel `rcmin`/`rcmax` (ch1in–ch16in) updating each bar's minline/maxline; validate ch1; "center sticks, throttle down" captures `rctrim`; for each channel with valid range writes **`RC{n}_MIN`, `RC{n}_MAX`, `RC{n}_TRIM`**; restore stream rates; show summary "CH{n} {min} | {max}". Button Text cycles `Click_when_Done`→`Saving`→`Completed`.
  - Spektrum bind (`:471-508`): `doCommand(START_RX_PAIR,…)` param2: DSM2=0, DSMX=1, DSM8=2.
  - **Params:** read `RCMAP_*`; write `RC{1..16}_MIN/MAX/TRIM`, `RC{n}_REV`/`RC{n}_REVERSED`, `ELEVON_*`, `SWITCH_ENABLE`.
- **Avalonia status:** **PARTIAL.** `ConfigRadioInputViewModel.cs` (119 lines) has 8 live `RcChannel` bars + a 100 ms timer + min/max capture during calibrate. Gap: only 8 channels (not 16), no `RCMAP_*` mapping/labels, no reverse checkboxes, no Elevon group, no Spektrum bind, no TRIM capture / param-write summary flow.

---

### Servo Output (`ConfigRadioOutput`)

Base `MyUserControl`. Header-only Designer; rows built in code.

- **Controls** (`ConfigRadioOutput.Designer.cs`):
  - `flowLayoutPanel1` (Dock=Fill) → `tableLayoutPanel1` (7 cols × 17 rows, AutoSize) `:47-86`. Header row labels: `label15` `"#"`, `label1` `"Position"`, `label2` `"Reverse"`, `label3` `"Function"` (w160), `label4` `"Min"`, `label5` `"Trim"`, `label6` `"Max"` `:88-148`.
  - `bindingSource1` (→ `CurrentState`), `timer1` → `timer1_Tick`.
- **Functionality** (`ConfigRadioOutput.cs`):
  - Ctor (`:11-33`): `num_servos=16`, → 32 if `SERVO_32_ENABLE>0`; `setup(i)` per servo.
  - `setup(servono)` (`:35-69`) per row, prefix `SERVO{n}`: col0 number Label; col1 `HorizontalProgressBar2` `BAR{n}` (800–2200) bound to `ch{n}out`; col2 `MavlinkCheckBox` → **`SERVOx_REVERSED`**; col3 `MavlinkComboBox` (w160) → **`SERVOx_FUNCTION`**; col4 `MavlinkNumericUpDown` (800–2200) → **`SERVOx_MIN`**; col5 → **`SERVOx_TRIM`**; col6 → **`SERVOx_MAX`**. Mavlink controls created `Enabled=false`, self-enable when param found.
  - Live: `Activate()` starts `timer1`; `timer1_Tick` (`:82-92`) `cs.UpdateCurrentSettings(...)` so each BAR shows current `chNout` PWM. No save button — inline param edits write directly.
- **Avalonia status:** **PARTIAL.** `ConfigParamPages.cs` `ConfigRadioOutputViewModel` (`:213-228`) is a generic `FByPrefix("SERVO")` param grid. Gap: no per-servo row layout (#/Position bar/Reverse/Function/Min/Trim/Max), no live output bars, no 32-servo handling.

---

### ESC Calibration (`ConfigESCCalibration`)

Base `MyUserControl`. Static param editor + one trigger button (Designer authored in zh-CN; runtime strings English via resx).

- **Controls** (`ConfigESCCalibration.Designer.cs`):
  - `label1` `"ESC Calibration (AC3.3+)"` `:68-71`; `label2` multiline `"Remove Props!\nAfter pushing this button:\n-Disconnect USB and battery\n-Plug in battery\n-when LEDs flash, push Saftey Switch (if present)\n-ESCs should beep as they are calibrated\n- restart flight controller normally"` `:73-76`.
  - `buttonStart` `MyButton` `"Calibrate ESCs"` → `buttonStart_Click` `:78-83`.
  - `label3` `"ESC Type:"` + `mavlinkComboBox1` (DropDownList) `:85-109`.
  - Five `MavlinkNumericUpDown` + labels: `"Output PWM Min"`, `"Output PWM Max"`, `"Spin when Armed"`, `"Spin minimum"`, `"Spin Maximum"` `:111-149`; description labels `label9`–`label13` (e.g. `"Leave as 0 to use RX input range"`, idle/min/max explanations) `:176-199`; `groupBox1/2/3` cosmetic frames.
- **Functionality** (`ConfigESCCalibration.cs`):
  - `Activate()` (`:15-26`): `mavlinkComboBox1`→**`MOT_PWM_TYPE`**; NUDs → **`MOT_PWM_MIN`** (0–1500), **`MOT_PWM_MAX`** (0–2200), **`MOT_SPIN_ARM`**, **`MOT_SPIN_MIN`**, **`MOT_SPIN_MAX`** (each 0–1, step 0.01).
  - `buttonStart_Click` (`:28-45`): `setParam("ESC_CALIBRATION", 3)`; on fail → `"Set param error. Please ensure your version is AC3.3+."`; on success disables button (one-shot). User then power-cycles per `label2`. No timer / no live bars.
- **Avalonia status:** **PARTIAL.** `Setup/ConfigCalibrationPages.cs` `ConfigESCCalibrationViewModel` (`:98-127`) sets `ESC_CALIBRATION=3` + instructions/log. Gap: no `MOT_PWM_TYPE`/`MOT_PWM_MIN/MAX`/`MOT_SPIN_ARM/MIN/MAX` editor fields.

---

### Flight Modes (`ConfigFlightModes`)

Base `MyUserControl`, `IActivate, IDeactivate`. Root = `tableLayoutPanel1` (5 cols × 7 rows) + 4 floating top labels.

- **Controls** (`ConfigFlightModes.Designer.cs`):
  - Top: `label13` `"Current Mode:"` `:124`; `lbl_currentmode` `"Manual"` bound to `currentStateBindingSource` `mode` `:128-130`; `label14` `"Current PWM:"` `:113`; `LBL_flightmodepwm` `"0"` (live PWM) `:118`.
  - Grid col0: `labelfm1`..`labelfm6` `"Flight Mode 1"`..`"Flight Mode 6"`. col1: `CMB_fmode1`..`CMB_fmode6` ComboBox (DropDownList) → `flightmode_SelectedIndexChanged` `:171-254`; row6 = `BUT_SaveModes` `MyButton` `"Save Modes"` `:256-261`. col2: `CB_simple1`..`6` CheckBox `"Simple Mode"` `:105-109`. col3: `chk_ss1`..`6` CheckBox `"Super Simple Mode"` `:293-297`; row6 = `linkLabel1_ss` `"Simple and Super Simple description"` `:336-341`. col4: PWM band labels `"PWM 0 - 1230"`, `"PWM 1231 - 1360"`, `"PWM 1361 - 1490"`, `"PWM 1491 - 1620"`, `"PWM 1621 - 1749"`, `"PWM 1750 +"`.
  - `currentStateBindingSource` → `CurrentState` `:132-134`.
- **Functionality** (`ConfigFlightModes.cs`):
  - `Activate()` (`:38`) populates 6 combos per firmware + 100 ms timer:
    - Plane/Ateryx: hide Simple/SS; params **`FLTMODE1..6`**.
    - Rover: hide Simple/SS; params **`MODE1..6`**.
    - Copter: Simple/SS visible unless `standardFlightModesOnly`; params **`FLTMODE1..6`** + bitmasks **`SIMPLE`** / **`SUPER_SIMPLE`** (bits 0-5 → checkboxes).
    - PX4: hide Simple/SS; **`COM_FLTMODE1..6`**, combo DataSource from `GetParameterOptionsInt("COM_FLTMODEn","PX4")`.
  - `updateDropDown` (`:416`): DataSource `ArduPilot.Common.getModesList(firmware)` (key=mode#, value=name).
  - `timer_Tick` (`:250`): 10 Hz; reads switch channel **`FLTMODE_CH`** (fallback **`MODE_CH`**) → maps to `cs.chNin`; sets `LBL_flightmodepwm` `"<ch>: <pwm>"`; highlights active combo `BackColor` via `readSwitch(pwm)`.
  - `readSwitch` (`:342`): PWM→index ≤1230→0,…,≥1750→5.
  - `BUT_SaveModes_Click` (`:354`): writes the 6 mode params (FLTMODE* / MODE* / COM_FLTMODE* by which exists); Copter also packs `SIMPLE`/`SUPER_SIMPLE` bitmasks; Text → `"Complete"`. Ctrl+S triggers save.
  - `flightmode_SelectedIndexChanged` (`:436`): Copter only — enable/disable Simple/SS per whether selected mode supports them. `linkLabel1_ss_LinkClicked` → simple/super-simple docs.
- **Avalonia status:** **PARTIAL.** `ConfigParamPages.cs` `ConfigFlightModesViewModel` (`:240-252`) is a combo grid (FLTMODE1–6 + SIMPLE/SUPER_SIMPLE/FLTMODE_CH). Gap: no live current-mode/PWM readout, no active-row highlight, no PWM band reference, no per-mode Simple/SS enable logic, no Rover/PX4 param families, no explicit Save button (param grid auto-writes).

---

### FailSafe (`ConfigFailSafe`)

Base `MyUserControl`, `IActivate, IDeactivate`. Three groupboxes + 16 live bars + status labels.

- **Controls** (`ConfigFailSafe.Designer.cs`):
  - Headers `label1` `"Radio IN"`, `label2` `"Servo/Motor OUT"`.
  - 16 `HorizontalProgressBar` (1000–2000, DrawLabel) bound to `currentStateBindingSource`: IN `"Radio 1".."Radio 8"` → `ch1in..ch8in` `:441-559`; OUT `"Radio 1".."Radio 8"` → `ch1out..ch8out` `:321-439`.
  - Status: `lbl_currentmode` `"Manual"` (bound `mode`, `TextChanged`); `lbl_armed` MyLabel `"Dissarmed"` (bound `armed`, custom Paint); `lbl_gpslock` MyLabel `"No Lock"` (bound `gpsstatus`, custom Paint); `LNK_wiki` LinkLabel `"Wiki"`.
  - `groupBox2` `"Radio"` (`:561-569`): `mavlinkComboBox_fs_thr_enable` → **`FS_THR_ENABLE`**; `PNL_thr_fs_value` panel (`label3` `"FS Pwm"`, `mavlinkNumericUpDownfs_thr_value`→`FS_THR_VALUE`, `mavlinkNumericUpDownthr_fs_value`→`THR_FS_VALUE`); `mavlinkCheckBoxthr_fs` `"Throttle FailSafe"` → **`THR_FAILSAFE`**; `mavlinkCheckBoxthr_fs_action` `"Throttle Failsafe Action"` → **`THR_FS_ACTION`**.
  - `groupBox3` `"GCS"` (`:571-579`): `mavlinkCheckBoxFS_GCS_ENABLE` `"GCS FS Enable"` → **`FS_GCS_ENABLE`** (copter); `mavlinkCheckBoxgcs_fs` `"GCS FailSafe"` → **`FS_GCS_ENABL`** (plane); `mavlinkCheckBoxshort_fs` `"FailSafe Short (1 sec)"` → **`FS_SHORT_ACTN`**; `mavlinkCheckBoxlong_fs` `"FailSafe Long (20 sec)"` → **`FS_LONG_ACTN`**.
  - `groupBox4` `"Battery"` (`:581-589`): `pnltimer` (`label6` `"Low Timer"`, NUD→`BATT_LOW_TIMER`); `mavlinkComboBoxfs_batt_enable` → **`BATT_FS_LOW_ACT`** / fallback **`FS_BATT_ENABLE`**; `PNL_low_bat` (`label4` `"Low Battery"`, NUD→`LOW_VOLT`/`FS_BATT_VOLTAGE`/`BATT_LOW_VOLT`); `pnlmah` (`label5` `"Reserved MAH"`, NUD→`FS_BATT_MAH`/`BATT_LOW_MAH`).
  - `toolTip1` on every mavlink control; `currentStateBindingSource` → `CurrentState`.
- **Functionality** (`ConfigFailSafe.cs`):
  - `Activate()` (`:24`): wires every control via `.setup(...)` with fallbacks (batt enable `BATT_FS_LOW_ACT`→`FS_BATT_ENABLE`; low-volt `LOW_VOLT`→`FS_BATT_VOLTAGE`→`BATT_LOW_VOLT`; mAh `FS_BATT_MAH`→`BATT_LOW_MAH`); plane params `FS_GCS_ENABLE`, `THR_FAILSAFE`, `THR_FS_VALUE`, `THR_FS_ACTION`, `FS_GCS_ENABL`, `FS_SHORT_ACTN`, `FS_LONG_ACTN`. Starts 100 ms timer; pops blocking `"Ensure your props are not on the Plane/Quad"` (title "FailSafe").
  - `timer_Tick` (`:100`): 10 Hz to drive bars/labels. `lbl_currentmode_TextChanged` (`:173`): red if `ch3in < FS_THR_VALUE`. `lbl_armed_Paint`/`lbl_gpslock_Paint` rewrite bound text. `LNK_wiki_LinkClicked` (`:112`): copter→failsafe-landing-page, else→advanced-failsafe-configuration. `Deactivate()` stops timer.
  - **Params:** read/write `FS_THR_ENABLE, FS_THR_VALUE, THR_FS_VALUE, THR_FAILSAFE, THR_FS_ACTION, FS_GCS_ENABLE/FS_GCS_ENABL, FS_SHORT_ACTN, FS_LONG_ACTN, BATT_FS_LOW_ACT/FS_BATT_ENABLE, BATT_LOW_TIMER, LOW_VOLT/FS_BATT_VOLTAGE/BATT_LOW_VOLT, FS_BATT_MAH/BATT_LOW_MAH`.
- **Avalonia status:** **PARTIAL.** `ConfigParamPages.cs` `ConfigFailSafeViewModel` (`:4-24`) is a param grid covering most FS_* / THR_* / BATT_* params. Gap: no live 16-bar IN/OUT visualization, no mode/armed/GPS status indicators, no low-throttle red warning, no props-off warning prompt, no wiki link.

---

### HW ID (`ConfigHWIDs`)

Base `MyUserControl`, `IActivate`. Single full-dock read-only grid.

- **Controls** (`ConfigHWIDs.Designer.cs`):
  - `myDataGridView1` `MyDataGridView` (Dock=Fill, ReadOnly, no add/delete, `AutoGenerateColumns=false`) `:44-64`. Columns (read-only text): `"ParamName"` (w150), `"DevID"`, `"BusType"`, `"Bus"`, `"Address"`, `"DevType"` `:70-111`.
  - `deviceInfoBindingSource` → type `DeviceInfo` `:66-68`.
- **Functionality** (`ConfigHWIDs.cs`):
  - `Activate()` (`:16`): filters params whose name contains `"_ID"` **or** `"_DEVID"`, excluding `"_IDX"` and `"FRSKY"`; projects to `DeviceInfo(index, name, (uint)value)` ordered by name; binds to grid. Read-only listing, no write/refresh button.
  - `DeviceInfo` decode (`DeviceInfo.cs`): wraps `Device.DeviceStructure(name, id)`. `BusType` = enum minus `"BUS_TYPE_"` (I2C/SPI/UAVCAN…); `DevType` context-sensitive — UAVCAN→`"SENSOR_ID#"+devtype`; name `"COMP"`→compass devtype, `"BARO"`→baro, `"ASP"`→airspeed, else IMU (each minus `"DEVTYPE_"`).
- **Avalonia status:** **DONE (close).** `ConfigHWIDViewModel.cs` (76 lines) builds the same `ParamName/DevID/BusType/Bus/Address/DevType` rows with identical filter (`_ID`/`_DEVID`, exclude `_IDX`/`FRSKY`) and `Device.DeviceStructure` decode (incl. UAVCAN/COMP/BARO/ASP branches). Minor: needs the equivalent grid View; logic parity is good.

---

### CubeID Update (`ConfigCubeID`)

Base `MyUserControl`, `IActivate`. Absolute layout, literal strings (no resx). (Lives under Optional Hardware, included here per task scope.)

- **Controls** (`ConfigCubeID.Designer.cs`):
  - `label1` multiline `"To use this feature\nPlease enable serial passthough to the port the CubeID is connected to.\nSet param SERIAL_PASSTIMO to 0\nSet param SERIAL_PASS2 to the telem port\n"` `:54-63`.
  - `label3` `"SERIAL_PASSTIMO"` + `mavnumtimeout` `MavlinkNumericUpDown` (disabled, 0–1) → **`SERIAL_PASSTIMO`** `:74-92`.
  - `label2` `"SERIAL_PASS2 "` + `mavpasscombo` `MavlinkComboBox` (DropDownList, disabled) → **`SERIAL_PASS2`** `:65-104`.
  - `CHK_forcebaud` CheckBox `"Force 57600 baud"` (Checked by default) `:116-126`.
  - `label4` `"Select the ODID device from the dropdown in the top right corner\nthen click the Update Firmware button"` `:106-114`.
  - `but_upfw` `MyButton` `"Upload Firmware"` → `but_upfw_Click` `:43-52`; `but_customfw` `MyButton` `"Upload Custom Firmware"` → `but_customfw_Click` `:128-137`.
- **Functionality** (`ConfigCubeID.cs`):
  - `Activate()` (`:148`): if current component id ≠ `MAV_COMP_ID_ODID_TXRX_1` → setup `SERIAL_PASS2`/`SERIAL_PASSTIMO` against MAV[1,1], **disable `but_upfw`** (must pick ODID device first). Else enable + `but_upfw`.
  - `but_upfw_Click` (`:32`): reset state, `ProgressReporterDialogue` → `Prd_DoWork` (downloads stock fw). `but_customfw_Click` (`:168`): OpenFileDialog `*.bin` then `Prd_DoWork`.
  - `Prd_DoWork` (`:61`): download `https://firmware.cubepilot.org/UAVCAN/com.cubepilot.cubeid/1.0/serial_fw_update.bin` (or local file), compute CRC32 (`crc32_update`), optionally force 57600 baud; subscribe **`CUBEPILOT_FIRMWARE_UPDATE_RESP`** → reply **`ENCAPSULATED_DATA`** chunks ≤252 B at `seqnr=offset/252`; loop sending **`CUBEPILOT_FIRMWARE_UPDATE_START`** (size, crc32, target) every 1 s + progress until `offset>size`.
- **Avalonia status:** **MISSING.** No `ConfigCubeID`/`CubeID` ViewModel in `ViewModels/`. Gap: entire CubeID/UAVCAN firmware-update flow (passthrough setup, ODID component gating, CUBEPILOT_FIRMWARE_UPDATE protocol) unported.

---

### Mandatory Hardware (`ConfigMandatory`)

Base `MyUserControl`, `IActivate`. Near-empty hub/intro page; the actual sub-pages are added as children of the `mand` group by `InitialSetup`, not by this class.

- **Controls** (`ConfigMandatory.Designer.cs`): single `label1` `"The following pages are required to be configured before your autopilot will work. Please work though them all."` `:35-38`.
- **Functionality** (`ConfigMandatory.cs`): empty `Activate()` (`:13`); no params, no handlers.
- **Avalonia status:** **PARTIAL.** No dedicated `ConfigMandatory` ViewModel; `SetupViewModel.cs:12-16` adds a `>> Mandatory Hardware` group header backed by a generic `InfoPageViewModel("Mandatory Hardware", "Required setup before flight. Pick a sub-page.")`. Functionally equivalent (intro label) but text differs from upstream.

---

## Avalonia status summary

| MP page | Avalonia type | Location | Status |
|---|---|---|---|
| ConfigFirmware (Install Firmware) | InstallFirmwareViewModel / ConfigFirmwareLegacyViewModel | Setup/SetupActionPages.cs; ConfigFirmwareLegacyViewModel.cs | PARTIAL (legacy list/download only; no flashing) |
| ConfigFrameClassType (Frame Type) | ConfigFrameClassTypeViewModel | ConfigParamPages.cs:230 | PARTIAL (param combos, no image grid/fade/gating) |
| ConfigFrameType (legacy Frame) | — | — | MISSING |
| ConfigAccelerometerCalibration | ConfigAccelCalibrationViewModel | Setup/ConfigCalibrationPages.cs:7 | PARTIAL (no 6-position step flow) |
| ConfigHWCompass | ConfigCompassViewModel | Setup/ConfigCalibrationPages.cs:54 | PARTIAL (onboard cal only) |
| ConfigRadioInput | ConfigRadioInputViewModel | ConfigRadioInputViewModel.cs | PARTIAL (8ch, no reverse/elevon/bind/RCMAP) |
| ConfigRadioOutput (Servo Output) | ConfigRadioOutputViewModel | ConfigParamPages.cs:213 | PARTIAL (SERVO* grid, no per-servo row/live bars) |
| ConfigESCCalibration | ConfigESCCalibrationViewModel | Setup/ConfigCalibrationPages.cs:98 | PARTIAL (ESC_CALIBRATION only, no MOT_* fields) |
| ConfigFlightModes | ConfigFlightModesViewModel | ConfigParamPages.cs:240 | PARTIAL (combo grid, no live/highlight/Simple logic) |
| ConfigFailSafe | ConfigFailSafeViewModel | ConfigParamPages.cs:4 | PARTIAL (param grid, no bars/status/warnings) |
| ConfigHWIDs (HW ID) | ConfigHWIDViewModel | ConfigHWIDViewModel.cs | DONE (logic parity) |
| ConfigCubeID | — | — | MISSING |
| ConfigMandatory | InfoPageViewModel (generic) | SetupViewModel.cs:12 | PARTIAL (intro only, text differs) |

Nav-tree parity: `SetupViewModel.cs` groups (Install Firmware / Mandatory / Optional / Advanced) match upstream order
broadly, but flatten the BackstageView into a header-prefixed flat list and omit the connection/firmware/param gating
conditions and several pages (e.g. Heli Setup, Serial Ports, Initial Tune Parameter, separate Compass2/FrameType variants).
