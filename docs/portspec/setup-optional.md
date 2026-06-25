# Setup → Optional Hardware + Advanced — 1:1 Port Spec

Source of truth: upstream Mission Planner **1.3.83** WinForms, under
`external/MissionPlanner/GCSViews/ConfigurationView/`.
Target: `src/MissionPlannerAvalonia/` (`ViewModels/GCSViews/ConfigurationView/` and `ViewModels/Setup/`).

For each page: **source = `Config<X>.Designer.cs` (control tree) + `Config<X>.cs` (functionality)**.
Strings are quoted verbatim from the designer/`.resx`. File:line citations point at the upstream files.

## How to read the Avalonia status

- **DONE** — faithful port; only cosmetic deltas.
- **PARTIAL** — params/core covered, but custom UI / live readouts / calibration math / write-handlers missing.
- **MISSING** — no matching VM exists.

Most upstream pages with bespoke layouts are folded in Avalonia into generic `ParamPageBase`
param-list editors (`ConfigParamPages.cs`) or `ActionPageViewModel` action pages
(`ConfigCalibrationPages.cs`), registered in `SetupViewModel.cs`. These cover the underlying
MAVLink params but reproduce none of the custom layouts, images, live telemetry, hardcoded option
lists, preset math, popups, or conditional show/hide logic.

---

## Optional Hardware — sensors

### Airspeed (`ConfigHWAirspeed`)

- **Controls** — flat layout (no nesting; all added directly to the UserControl), `ConfigHWAirspeed.Designer.cs:114-122`:
  - `pictureBox4` — `PictureBox`, white background, `BorderStyle.FixedSingle`, BackgroundImage = `Resources.airspeed` (`Designer.cs:54-61`).
  - `groupBox3` — empty `GroupBox` (decorative frame, no Text) (`Designer.cs:63-67`).
  - `label2` — `Label` "Airspeed" (`Designer.cs:78-81`).
  - `CHK_enableairspeed` — `MavlinkCheckBox` "Enable", OnValue=1/OffValue=0 (`Designer.cs:44-52`).
  - `CHK_airspeeduse` — `MavlinkCheckBox` "Use Airspeed", OnValue=1/OffValue=0 (`Designer.cs:69-76`).
  - `label1` — `Label` "Type" (`Designer.cs:106-109`); `mavlinkComboBoxARSPD_TYPE` — `MavlinkComboBox`, DropDownList (`Designer.cs:97-104`).
  - `lbl_airspeed_pin` — `Label` "Pin" (`Designer.cs:92-95`); `mavlinkCheckBoxAirspeed_pin` — `MavlinkComboBox` (misnamed "checkbox"; is a combo), DropDownList, DropDownWidth=200 (`Designer.cs:83-90`).
- **Functionality** (`ConfigHWAirspeed.cs`):
  - `Activate()` disables whole page if link closed (`cs:20-25`). Hides `CHK_airspeeduse` if `ARSPD_USE` absent; hides `CHK_enableairspeed` if `ARSPD_ENABLE` absent (`cs:29-33`).
  - Binds: `CHK_airspeeduse.setup(1,0,"ARSPD_USE")`, `CHK_enableairspeed.setup(1,0,"ARSPD_ENABLE")` (`cs:35-36`).
  - `mavlinkComboBoxARSPD_TYPE.setup(...)` bound to **ARSPD_TYPE** via `ParameterMetaDataRepository.GetParameterOptionsInt("ARSPD_TYPE", firmware)` (`cs:38-39`).
  - `mavlinkCheckBoxAirspeed_pin.setup(options, "ARSPD_PIN")` with a **hardcoded** options list (`cs:41-59`): 0–9 = "APM 2 analog pin 0..9", 64 = "APM 1 AS Port", 11 = "PX4 Analog AS Port", 15 = "Pixhawk Analog AS Port", 65 = "PX4/Pixhawk EagleTree or MEAS I2C AS Sensor".
  - `CHK_enableairspeed_CheckedChanged` (`cs:65-84`): writes **ARSPD_ENABLE** = 1/0 via `setParam`; if param null shows `Strings.ErrorFeatureNotEnabled`; on exception shows `Strings.ErrorSetValueFailed` for "ARSPD_ENABLE". `CHK_airspeeduse` has no handler (MavlinkCheckBox auto-writes ARSPD_USE).
  - Params: **ARSPD_USE, ARSPD_ENABLE, ARSPD_TYPE, ARSPD_PIN**.
- **Avalonia status**: **PARTIAL** — `ConfigAirspeedViewModel` (`ConfigParamPages.cs:103-110`) is a generic `ParamPageBase` list of ARSPD_TYPE/ENABLE/USE/PIN as plain combos. Missing: hardcoded ARSPD_PIN option list, the airspeed picture, ARSPD_ENABLE write-handler with error popups, Enable/Use-Airspeed checkbox styling & visibility logic.

### RangeFinder (`ConfigHWRangeFinder`)

- **Controls** — flat layout (`ConfigHWRangeFinder.Designer.cs:109-116`):
  - `pictureBox3` — `PictureBox`, white, FixedSingle, BackgroundImage = `Resources.sonar` (`Designer.cs:60-67`).
  - `groupBox1` — empty decorative `GroupBox` (`Designer.cs:74-78`); `label1` — `Label` "RangeFinder" (`Designer.cs:69-72`).
  - `CMB_sonartype` — `MavlinkComboBox`, DropDownList, with 4 **vestigial hardcoded Items** "XL-EZ0 / XL-EZ4", "LV-EZ0", "XL-EZL0", "HRLV" (`Designer.cs:45-58`) — overwritten by `setup()` at runtime.
  - `label2` — `Label` "Distance: "; `LBL_dist` — `Label` live value (init "0.0") (`Designer.cs:85-98`).
  - `label4` — `Label` "Voltage: "; `LBL_volt` — `Label` live value (init "0.0").
  - `timer1` — `Timer`, Interval=200 ms (`Designer.cs:100-103`).
- **Functionality** (`ConfigHWRangeFinder.cs`):
  - `Activate()` disables page if closed (`cs:19-24`); binds `CMB_sonartype.setup(GetParameterOptionsInt("RNGFND_TYPE", firmware), "RNGFND_TYPE")` (`cs:27-29`); starts `timer1`. `Deactivate()` stops timer.
  - `timer1_Tick` (`cs:41-45`): `LBL_dist.Text = cs.sonarrange`, `LBL_volt.Text = cs.sonarvoltage` (5 Hz live telemetry).
  - `CMB_sonartype_SelectedIndexChanged` (`cs:47-58`): if selected Text == "TeraRangerOne-I2C", writes **RNGFND_MAX_CM**=100 and **RNGFND_MIN_CM**=20 (upstream has a trailing-space bug: `"RNGFND_MIN_CM "`).
  - Params: **RNGFND_TYPE** (bound), **RNGFND_MAX_CM / RNGFND_MIN_CM** (TeraRanger special-case).
- **Avalonia status**: **PARTIAL** — `ConfigRangeFinderViewModel` (`ConfigParamPages.cs:92-100`) lists RNGFND1_TYPE/MIN_CM/MAX_CM + RNGFND_TYPE/MAX_CM. Missing: live Distance/Voltage 200 ms readout, TeraRangerOne-I2C auto min/max, sonar image.

### Optical Flow (`ConfigHWOptFlow`)

- **Controls** — flat layout (`ConfigHWOptFlow.Designer.cs:242-268`):
  - `pictureBox2` — `PictureBox`, white, FixedSingle, BackgroundImage = `Resources.opticalflow` (`Designer.cs:69-76`).
  - `groupBox4` — empty decorative `GroupBox`; `label3` — `Label` "Optical Flow  " (`Designer.cs:78-87`).
  - `CHK_enableoptflow` — `MavlinkCheckBox` "Enable" (legacy), OnValue=1/OffValue=0 (`Designer.cs:230-238`).
  - `DROP_optflowtype` — `MavlinkComboBox` (label1 "Type"), DropDownList (`Designer.cs:220-228`).
  - `MavlinkNumericUpDown`s: `mavlinkNumericUpDown_yaw` ("Yaw Orientation"/label2; suffix label9 "degrees, relative to vehicle"), `mavlinkNumericUpDownFX` ("FX Scale"/label4), `mavlinkNumericUpDownFY` ("FY Scale"/label5), `mavlinkNumericUpDownX` ("Position X"/label6; label10 "metres foward"), `mavlinkNumericUpDownY` ("Position Y"/label7; label11 "metres right"), `mavlinkNumericUpDownZ` ("Position Z"/label8; label12 "metres down"), `mavlinkNumericUpDownHGTOVR` ("Height Override"/label15; label16 "metres from ground").
  - Section labels: label13 "Position of Sensor, relative to IMU", label14 "Scaling Factors".
- **Functionality** (`ConfigHWOptFlow.cs`):
  - `Activate()` disables if closed (`cs:19-24`). Binds legacy `CHK_enableoptflow.setup(1,0,"FLOW_ENABLE")` (`cs:29`).
  - **Two-panel mode switch** (`cs:31-46`): if FLOW_ENABLE control enabled (legacy firmware) → show only checkbox, hide all new-style combos/numerics and return. Else hide the legacy checkbox and configure the new panel.
  - New panel (`cs:50-64`): `DROP_optflowtype.setup(GetParameterOptionsInt("FLOW_TYPE", firmware),"FLOW_TYPE")`; `_yaw.setup("FLOW_ORIENT_YAW")` then Max=180/Min=-179/Inc=1; `FX→FLOW_FXSCALER`, `FY→FLOW_FYSCALER`, `X→FLOW_POS_X`, `Y→FLOW_POS_Y`, `Z→FLOW_POS_Z`, `HGTOVR→FLOW_HGT_OVR`.
  - Rover-only logic (`cs:66-72`, `DROP_optflowtype_SelectedIndexChanged` `cs:98-113`): hide HGTOVR + label15 + label16 unless `VersionString.Contains("ArduRover")`.
  - `CHK_enableoptflow_CheckedChanged` (`cs:77-96`): writes **FLOW_ENABLE** 1/0; null → "Not Available on <firmware>"; error → "Set FLOW_ENABLE Failed".
  - Params: **FLOW_ENABLE, FLOW_TYPE, FLOW_ORIENT_YAW, FLOW_FXSCALER, FLOW_FYSCALER, FLOW_POS_X/Y/Z, FLOW_HGT_OVR**.
- **Avalonia status**: **PARTIAL** — `ConfigOptFlowViewModel` (`ConfigParamPages.cs:113-124`) lists FLOW_TYPE/FXSCALER/FYSCALER/ORIENT_YAW/POS_X/Y/Z/HGT_OVR. Missing: FLOW_ENABLE legacy checkbox + handler, legacy-vs-new two-panel switch, ArduRover-only Height-Override visibility, suffix/unit labels, optical-flow image.

### PX4 Flow (`ConfigHWPX4Flow`)

- **Controls** — flat layout (`ConfigHWPX4Flow.Designer.cs:78-82`):
  - `pictureBox2` — `PictureBox`, white, FixedSingle, BackgroundImage = `Resources.PX4FLOW1` (`Designer.cs:41-48`).
  - `groupBox4` — empty decorative `GroupBox`; `label3` — `Label` "PX4 Flow" (`Designer.cs:50-59`).
  - `imagebox` — `PictureBox` for the live video stream (`Designer.cs:61-65`).
  - `but_focusmode` — `MyButton` "Focus Mode" (`Designer.cs:67-72`).
- **Functionality** (`ConfigHWPX4Flow.cs`): No MAVLink params. `Activate()` (`cs:20-38`) enabled if link open or `logreadmode`; constructs `OpticalFlow(comPort, sysid, compid)` and subscribes `newImage` → `imagebox.Image = eh.Image.ToSKImage().ToBitmap()`. `but_focusmode_Click` (`cs:40-44`) toggles `focusmode` and calls `_flow.CalibrationMode(focusmode)`. `Deactivate()` (`cs:46-53`) calls `CalibrationMode(false)` + `_flow.Close()`.
- **Avalonia status**: **DONE** — `ConfigPX4FlowViewModel.cs` faithful 1:1: OpticalFlow image stream → `WriteableBitmap`, `ToggleFocus` → `CalibrationMode`, `IsConnected` open/logreadmode gate, `Dispose` mirrors `Deactivate`. Cosmetic only: button label toggles "Focus"/"Video" (upstream static "Focus Mode") and adds a Status string.

### Parachute (`ConfigHWParachute`)

- **Controls** — flat layout (`ConfigHWParachute.Designer.cs:160-173`):
  - `pictureBox3` — `PictureBox`, white, FixedSingle, BackgroundImage = `Resources.sonar`, **Image = `Resources.Parachute`** (`Designer.cs:63-71`).
  - `groupBox1` — empty decorative `GroupBox`; `label1` — `Label` "Parachute" (`Designer.cs:57-61`).
  - `mavlinkCheckBoxEnable` — `MavlinkCheckBox` "Enable (AC3.3+)", OnValue=1/OffValue=0 (`Designer.cs:146-154`).
  - `label2` "Type" + `mavlinkComboBoxType` — `MavlinkComboBox`, DropDownList (`Designer.cs:137-144`).
  - `label3` "Servo Num" + `mavlinkComboBoxServoNum` — **plain `System.Windows.Forms.ComboBox`** (not Mavlink-bound), DropDownList, hardcoded Items "RC9","RC10","RC11","RC12","RC13","RC14" (`Designer.cs:98-111`).
  - `label4` "Resting PWM" + `mavlinkNumericUpDownResting`; `label5` "Deploy PWM" + `mavlinkNumericUpDownDeploy`; `label6` "Min Alt (m)" + `mavlinkNumericUpDownMinAlt` (`MavlinkNumericUpDown`).
- **Functionality** (`ConfigHWParachute.cs`):
  - `Activate()` (`cs:17-45`): scans all params for any `*_FUNCTION == 27` and sets `mavlinkComboBoxServoNum.Text` to that prefix (detecting the chute servo channel). Binds `mavlinkCheckBoxEnable.setup(1,0,"CHUTE_ENABLED")`.
  - `mavlinkComboBoxType.setup(options,"CHUTE_TYPE")`, hardcoded options (`cs:34-40`): 0 "First Relay", 1 "Second Relay", 2 "Third Relay", 3 "Fourth Relay", 10 "Servo".
  - `Resting.setup(1000,2000,..,"CHUTE_SERVO_OFF")`, `Deploy→"CHUTE_SERVO_ON"`, `MinAlt.setup(0,32000,..,"CHUTE_ALT_MIN")`.
  - `mavlinkComboBoxServoNum_SelectedIndexChanged` (`cs:47-55`): calls `ensureDisabled(..,27,selectedText)` then `setParam("<RCx>_FUNCTION", 27)`. `ensureDisabled` (`cs:57-74`) resets any other RC9–RC14 channel's `_FUNCTION` from 27 → 0 (mutual exclusion).
  - Params: **CHUTE_ENABLED, CHUTE_TYPE, CHUTE_SERVO_ON, CHUTE_SERVO_OFF, CHUTE_ALT_MIN**, dynamic **RC9..RC14_FUNCTION** (=27).
- **Avalonia status**: **PARTIAL** — `ConfigParachuteViewModel` (`ConfigParamPages.cs:183-190`) lists CHUTE_ENABLED/TYPE/SERVO_ON/SERVO_OFF/ALT_MIN. Missing: Servo-Num combo (RC9–RC14) + `_FUNCTION==27` auto-detect, assign-on-select + `ensureDisabled` mutual exclusion, hardcoded CHUTE_TYPE labels, parachute image.

---

## Optional Hardware — comms / OSD / compass

### OSD (`ConfigHWOSD`)

- **Controls** (`ConfigHWOSD.Designer.cs:29-97`): simple `MyUserControl`:
  - `label7` — `Label` "You only need to use this if you are having issue with your OSD not updating".
  - `BUT_osdrates` — `MyButton` **"Enable Telemetry"**, green gradient (`BGGradTop`/`BGGradBot`), 245,50 size 180×51 → `BUT_osdrates_Click`.
  - `label6` — `Label` header "OSD" (Microsoft Sans Serif 12pt).
  - `groupBox5` — empty `GroupBox` header rule bar (3,23, 644×5), the standard ConfigHW page-header separator.
  - `pictureBox5` — `PictureBox`, 75×75, white bg, FixedSingle, `BackgroundImage = Resources.MinimOSD`, Zoom.
- **Functionality** (`ConfigHWOSD.cs`):
  - `Activate()` (`cs:14-21`): disables control if `MainV2.comPort.BaseStream.IsOpen` is false, then immediately sets `Enabled = true` (upstream bug — always ends enabled).
  - `BUT_osdrates_Click` (`cs:23-69`): writes value `2` via `setParam` to stream-rate params for **SR0_**, **SR1_** and **SR3_** groups, each: `*_EXT_STAT, *_EXTRA1, *_EXTRA2, *_EXTRA3, *_POSITION, *_RAW_CTRL, *_RAW_SENS, *_RC_CHAN` (24 params). On exception: `CustomMessageBox.Show("Failed to set OSD rates.")`. (`SR*_PARAMS` intentionally NOT written.)
- **Avalonia status**: **MISSING** — no `ConfigHWOSD*` VM. The "Enable Telemetry" bulk SR0/SR1/SR3 stream-rate setter is not ported.

### Compass (`ConfigHWCompass2`)

- **Controls** (`ConfigHWCompass2.Designer.cs`, strings inline):
  - Header `label6` = "Compass Priority" (`:102`); `groupBox5` separator; `label1` (`:91`) = "Set the Compass Priority by reordering the compasses in the table below (Highest at the top)\r\n".
  - `myDataGridView1` (`MyDataGridView`) bound to `compassDeviceInfoBindingSource`. Columns: `Priority` "Priority", `devID...` "DevID", `busType...` "BusType", `bus...` "Bus", `address...` "Address", `devType...` "DevType", `Missing` (CheckBox col "Missing"), `External` (CheckBox col "External"), `Orientation` (ComboBox col "Orientation"), `Up` (Image col "Up"), `Down` (Image col "Down").
  - `but_missing` — `MyButton` "Remove Missing" (`:547`).
  - `mavlinkCheckBoxUseCompass1/2/3` — `MavlinkCheckBox` "Use Compass 1" / "Use Compass 2" / "Use Compass 3".
  - `CHK_compass_learn` — `MavlinkCheckBox` "Automatically learn offsets".
  - `label3` "Do you want to disable any of the first 3 compasses?"; `label4` "A mag calibration is required to remap the above changes."; `label5` "A reboot is required to adjust the ordering.\r\n".
  - `but_reboot` — `MyButton` "Reboot"; `but_largemagcal` — `MyButton` "Large Vehicle MagCal".
  - `groupBoxonboardcalib` — `GroupBox` "Onboard Mag Calibration": `label2` "Relax fitness if calibration fails", `label10` "Fitness", `mavlinkComboBoxfitness`, three `HorizontalProgressBar` (`horizontalProgressBar1/2/3`) with `label7`="Mag 1"/`label8`="Mag 2"/`label9`="Mag 3", status `pictureBox1/2/3` (turn green on success), `lbl_obmagresult` (multiline TextBox), buttons `BUT_OBmagcalstart`="Start", `BUT_OBmagcalaccept`="Accept", `BUT_OBmagcalcancel`="Cancel".
  - `timer1` — Forms Timer for progress polling.
- **Functionality** (`ConfigHWCompass2.cs`, `IActivate, IDeactivate`):
  - `Activate()` (`:86-145`): builds device list from `COMPASS*DEV_ID` (nonzero) and `COMPASS_PRIO*`; marks `Missing`; binds grid. Sets up `mavlinkComboBoxfitness` for **COMPASS_CAL_FIT**; use-checkboxes for **COMPASS_USE/COMPASS1_USE**, **COMPASS_USE2/COMPASS2_USE**, **COMPASS_USE3/COMPASS3_USE**; `CHK_compass_learn` for **COMPASS_LEARN**; Orientation combo from **COMPASS_ORIENT/COMPASS1_ORIENT** options. `CompassDeviceInfo` reads per-compass **COMPASS_DEV_ID[/2/3] / COMPASS1/2/3_DEV_ID**, **COMPASS_ORIENT[2/3]**, **COMPASS_EXTERNAL / COMPASS_EXTERN2/3**. Any missing → `CustomMessageBox.Show("Your compass configuration has changed, please review the missing compass")`.
  - Grid Up/Down click (`myDataGridView1_CellContentClick :184`) reorders, then `UpdateFirst3()` (`:205-262`): writes **COMPASS_PRIO1_ID**, **COMPASS_PRIO2_ID**, **COMPASS_PRIO3_ID** via `setParamAsync` (clears 2/3 if absent); sets `rebootrequired=true`.
  - `BUT_OBmagcalstart_Click` (`:264`): `doCommand DO_START_MAG_CAL(0,1,1,0,0,0,0)`; subscribes `MAG_CAL_PROGRESS` & `MAG_CAL_REPORT`; starts timer1.
  - `BUT_OBmagcalaccept_Click` (`:323`): `DO_ACCEPT_MAG_CAL`; `BUT_OBmagcalcancel_Click` (`:341`): `DO_CANCEL_MAG_CAL`; both unsubscribe + stop timer.
  - `timer1_Tick` (`:358`): aggregates progress into 3 bars + `lbl_obmagresult`, greens pictureBoxes at 100/report, prompts "Please reboot the autopilot" when complete.
  - `but_largemagcal_Click` (`:476`): `InputBox.Show("MagCal Yaw", "Enter current heading in degrees...")` → `doCommand FIXED_MAG_CAL_YAW`.
  - `but_reboot_Click` (`:500`): confirm "Reboot?" → `doReboot`. `but_missing_ClickAsync` (`:519`): removes Missing rows → `UpdateFirst3()`. `Deactivate()`/`CheckReboot()` prompt "Reboot required, reboot now?".
- **Avalonia status**: **PARTIAL** (mostly missing) — only onboard mag-cal exists as `ConfigCompassViewModel` (`ConfigCalibrationPages.cs:54`), an `ActionPageViewModel` with Start/Accept/Cancel firing `DO_START/ACCEPT/CANCEL_MAG_CAL`. Missing: compass priority DataGrid + reordering (COMPASS_PRIO*_ID), per-mag progress bars/results, Use-Compass-1/2/3, COMPASS_LEARN, COMPASS_CAL_FIT, orientation column, Large Vehicle MagCal (FIXED_MAG_CAL_YAW), Remove Missing, reboot handling.

### Bluetooth (`ConfigHWBT`)

- **Controls** (`ConfigHWBT.Designer.cs:29-145`):
  - Header `label6` = "Bluetooth"; `groupBox5` separator; `pictureBox5` (75×75, `Image = Resources.BT_hc06`).
  - `label1` "Name" → `txt_name` (TextBox); `label2` "Baud" → `cmb_baud` (ComboBox, not DropDownList); `label3` "Pin" → `txt_pin` (TextBox).
  - `cmb_baud` items (`:76-84`): "1200", "2400 ", "4800 ", "9600", "19200 ", "38400 ", "57600 ", "115200" (trailing spaces in resx).
  - `BUT_btsettings` — `MyButton` "Save Settings" → `BUT_btsettings_Click`.
- **Functionality** (`ConfigHWBT.cs`): no MAVLink params — talks raw AT commands to an HC-05/06 module over a direct `SerialPort` (not the MAVLink link).
  - `baudmap` (`:16-26`): 1200→1, 2400→2, 4800→3, 9600→4, 19200→5, 38400→6, 57600→7, 115200→8.
  - `Activate()` (`:33-40`): if MAVLink link `IsOpen` set `Enabled=false` then `Enabled=true` (always-enabled quirk).
  - `BUT_btsettings_Click` (`:42-118`): AT command list "AT", "AT+VERSION", "AT+ROLE=0\r\n", "AT+NAME={name}\r\n", "AT+NAME{name}", "AT+BAUD={baud}\r\n", "AT+BAUD{baudmap[baud]}", "AT+PSWD={pin}\r\n", "AT+PIN{pin}", "AT+RESET". Iterates every baud, opens `new SerialPort(MainV2.comPortName, baud)`, probes "AT"+"\r\n", on "OK" sends all commands (1s sleeps). Errors → `CustomMessageBox.Show(Strings.SelectComport ..., Strings.ERROR)`; final → `Strings.ErrorSettingParameter`/`Strings.ProgrammedOK`.
- **Avalonia status**: **DONE** — `ConfigHWBTViewModel.cs` faithful. Same `Baudmap`, same 10 AT-command sequence (`:103-115`), same probe loop (`RunSequence :128-172`), Name/Pin/Baud bound, `IsBusy`, `Output` log. Deltas: adds an explicit serial **port picker** (`Ports`/`SelectedPort`); logs to `Output` instead of `CustomMessageBox`; literal English messages instead of `Strings.*`; default Baud "57600", Pin "1234".

### ESP8266 (`ConfigHWesp8266`)

- **Controls** (`ConfigHWesp8266.Designer.cs:29-238`):
  - Header `label6` = "ESP8266"; `groupBox5` separator; `pictureBox5` (`Image = Resources.BT_hc06`, reused).
  - `label1` "SSID" → `txt_ssid`; `label2` "Baud" → `cmb_baud`; `label3` "Password" → `txt_password`; `label4` "Channel" → `cmb_channel`.
  - `cmb_baud` items (`:89-98`): "1200","2400 ","4800 ","9600","19200 ","38400 ","57600 ","115200","921600".
  - `cmb_channel` items (`:135-146`): "1"…"11".
  - `chk_mode` (CheckBox) "Client Mode" → `chk_mode_CheckedChanged`.
  - `label5` "..." (runtime diagnostics).
  - `BUT_espsettings` — `MyButton` "Save Settings" → `BUT_ESPsettings_Click`; `but_resetdefault` — `MyButton` "Reset to Defaults" → `but_resetdefault_Click`.
  - `groupBoxsta` — `GroupBox` "STA Mode" (disabled by default): `label7` "IP"→`txt_ip`, `label8` "Subnet"→`txt_subnet`, `label9` "Gateway"→`txt_gateway`.
- **Functionality** (`ConfigHWesp8266.cs`): all params target component `MAV_COMP_ID_UDP_BRIDGE`.
  - `Activate()` (`:20-100`): sends `mavlink_param_request_list_t` to UDP_BRIDGE; reads **WIFI_SSID1..4, WIFI_PASSWORD1..4, WIFI_SSIDSTA1..4, WIFI_PWDSTA1..4** (ASCII 4-byte chunks), **UART_BAUDRATE, WIFI_CHANNEL, DEBUG_ENABLED, WIFI_MODE, WIFI_IPADDRESS, WIFI_UDP_HPORT, WIFI_UDP_CPORT, WIFI_IPSTA, WIFI_GATEWAYSTA, WIFI_SUBNET_STA**. Populates fields, `chk_mode`(=WIFI_MODE≠0), dumps diagnostics into `label5`. Disabled if no WIFI_SSID1.
  - `BUT_ESPsettings_Click` (`:115-156`): writes **WIFI_CHANNEL, UART_BAUDRATE, WIFI_SSID1..4, WIFI_PASSWORD1..4, WIFI_SSIDSTA1..4, WIFI_PWDSTA1..4** (stringTobytearray→UInt32), **WIFI_IPSTA, WIFI_GATEWAYSTA, WIFI_SUBNET_STA, WIFI_MODE**. Then `PREFLIGHT_STORAGE(1)` + `PREFLIGHT_REBOOT_SHUTDOWN`; result → `Strings.ErrorSettingParameter`/`Strings.ProgrammedOK`.
  - `but_resetdefault_Click` (`:158-186`): `PREFLIGHT_STORAGE(2)` then `(1)`, calls `Activate()` twice.
  - `chk_mode_CheckedChanged` (`:188-198`): enables/disables `groupBoxsta`.
- **Avalonia status**: **DONE** — `ConfigHWESP8266ViewModel.cs` ports closely. Reads same param set, builds same `Details` string (`:107-117`); `Save` writes identical set incl. SSIDSTA/PWDSTA mirroring + same STORAGE/REBOOT sequence (`:158-211`); `ResetDefaults` matches (`:213-240`); `StaMode` gates STA fields. Deltas: hardcoded `ChannelOptions` 1–13 (upstream 1–11) and `BaudOptions` 9600..921600 (upstream 1200..921600); adds 2s settle delay; default Baud "115200"/Channel "11"/IP 192.168.4.1; `Status` string instead of `CustomMessageBox`.

### CAN (`ConfigHWCAN`)

- **Controls** (`ConfigHWCAN.Designer.cs:29-148`):
  - Header `label6` = "CAN"; `groupBox5` separator; `pictureBox5` (75×75, white, FixedSingle — no image).
  - `label1` "Enable CAN" → `mavlinkComboBox_can` (`MavlinkComboBox`, DropDownList) → `mavlinkComboBox_can_SelectedIndexChanged` (empty body).
  - `label2` "NOTE: a restart is required after changing this option".
  - `groupBox1` (no caption): `but_startenum` "Start Enumeration" → `but_startenum_Click`; `but_stopenum` "Stop Enumeration" → `but_stopenum_Click`; `but_saveconfig` "Save All Config" → `but_saveconfig_Click`; `but_factoryreset` "Reset config" (disabled) → `but_factoryreset_Click`; `checkBox1` "Factory Reset" → `checkBox1_CheckedChanged` (gates `but_factoryreset.Enabled`).
- **Functionality** (`ConfigHWCAN.cs`):
  - `Activate()` (`:15-25`): always-enabled quirk, then `mavlinkComboBox_can.setup(...)` bound to **BRD_CAN_ENABLE** (options metadata).
  - `but_startenum_Click` (`:28`): `doCommand PREFLIGHT_UAVCAN(1,0,0,0,0,0,0)`; `but_stopenum_Click` (`:33`): `PREFLIGHT_UAVCAN(0,...)`.
  - `but_saveconfig_Click` (`:38`): `PREFLIGHT_STORAGE(1,...)`; `but_factoryreset_Click` (`:48`): `PREFLIGHT_STORAGE(2,...)`.
  - `checkBox1_CheckedChanged` (`:43`): `but_factoryreset.Enabled = checkBox1.Checked`.
- **Avalonia status**: **MISSING** — no `ConfigHWCAN*` VM. (`ConfigDroneCanViewModel.cs` is the unrelated DroneCAN/SLCan page.) Not ported: BRD_CAN_ENABLE combo, UAVCAN start/stop (PREFLIGHT_UAVCAN), Save All Config / Factory Reset (PREFLIGHT_STORAGE 1/2) + checkbox gate.

---

## Optional Hardware — CAN / battery / tracker / ADSB

### DroneCAN/UAVCAN (`ConfigDroneCAN`)

- **Controls** (`ConfigDroneCAN.Designer.cs`):
  - `label6` "DroneCAN/UAVCAN" (title, 12pt); `groupBox5` separator.
  - `cmb_interfacetype` — ComboBox (interface selection); `cmb_networkinterface` — ComboBox (hidden by default).
  - `but_connect` "Connect"; `but_filter` "Filter", `but_stats` "Stats", `but_uavcaninspector` "Inspector" (`MyButton`s).
  - `CHK_checkupdate` "Check for Updates"; `chk_canonclose` "Exit SLCAN on leave?" (checked); `chk_log` "Log".
  - `label1` warning: "After enabling SLCAN, you will no longer be able to connect via MAVLINK.\r\nYou must leave this screen and wait 2 seconds before connecting again".
  - `myDataGridView1` — `MyDataGridView` bound to `uAVCANModelBindingSource` (DroneCANModel), columns: `ID, Name, Mode, Health, Uptime, HW Version, SW Version, SW CRC`, plus a `Menu` button column.
  - `contextMenu1` MenuItems: "Parameters", "Restart", "Update", "Update Beta", "CANPassThrough Here3" (RadioCheck), "CANPassThough Here3+/4".
  - `tableLayoutPanel1` — 4-col detail grid bound to selected node: labels "Node ID / Name", "Mode / Health / Uptime", "Vendor-specific code", "Software version/CRC64", "Hardware version/UID" with bound TextBoxes (ID, Name, Mode, Health, Uptime, VSC, SoftwareVersion, SoftwareCRC[X], HardwareVersion, HardwareUID[X]).
  - `DGDebug` — DataGridView (debug log) columns `Node, Level, Source, Text`; fed when `chk_log` on.
- **Functionality** (`ConfigDroneCAN.cs`, ~71 KB): SLCAN/CAN node enumeration over serial *or* MAVLink CAN_FORWARD; node table via DroneCAN GetNodeInfo/NodeStatus; per-node context-menu actions — open node Parameters editor, Restart, firmware Update / Update Beta, CAN pass-through (Here3 / Here3+/4); Inspector window; Filter & Stats dialogs; "Check for Updates"; debug-log grid; exit-SLCAN-on-leave handling. Handlers: `but_connect_Click`, `cmb_interfacetype_SelectedIndexChanged`, `menu_parameters/restart/update/updatebeta/passthrough/passthrough4_Click`, `But_uavcaninspector_Click`, `but_filter_Click`, `but_stats_Click`, `myDataGridView1_CellClick/RowEnter/RowsAdded`, `CHK_checkupdate_CheckedChanged`.
- **Avalonia status**: **PARTIAL** — `ConfigDroneCanViewModel` + `ConfigDroneCanView.axaml` implement the core: Bus selector ("MAVLink CAN1"/"MAVLink CAN2" — upstream uses a generic interface/network combo, not a fixed bus list), Connect/Disconnect, node enumeration via MAVLink `CAN_FORWARD` + SLCAN bridge over `CommsInjection`, a `Nodes` table (Id, Name, Health, Mode, Uptime, HW/SW Version), Refresh. Missing: serial/SLCAN interface selection, SW-CRC / vendor-specific-code / HW-UID detail fields, per-node context menu (Parameters editor, Restart, Update / Update Beta, CANPassThrough Here3 / Here3+/4), Inspector, Filter, Stats, Check-for-Updates, debug log grid, Log/Exit-SLCAN checkboxes, SLCAN warning text.

### Battery Monitor (`ConfigBatteryMonitoring`)

- **Controls** (`ConfigBatteryMonitoring.Designer.cs`):
  - `pictureBox5` — PictureBox (wiring diagram, white bg, fixed border).
  - `label30` "Monitor" + `CMB_batmontype` — `MavlinkComboBox` (DropDownList, w200), items: "0: Disabled", "3: Battery Volts", "4: Voltage and Current" (`Designer:111-118`).
  - `label47` "Sensor" + `CMB_batmonsensortype` — ComboBox (w200), 10 items: "0: Other", "1: AttoPilot 45A", "2: AttoPilot 90A", "3: AttoPilot 180A", "4: 3DR Power Module", "5: 3DR 4 in 1 ESC", "6: 3DR HV Power Module APM", "7: Cube HV Power Module", "8: CUAV HV PM", "9: Holybro Power Module" (`Designer:74-84`).
  - `label1` "HW Ver" + `CMB_HWVersion` — ComboBox (w200), 11 items: "0: CUAV V5/Pixhawk4 or APM1", "1: APM2 - 2.5 non 3DR", "2: APM2.5+/ZealotF427 - 3DR Power Module", "3: PX4", "4: The Cube or Pixhawk", "5: VR Brain 4.5 - 5", "6: VR Micro Brain 5", "7: VR Brain 4", "8: Cube Orange", "9: Durandal/ZealotH743", "10: Pixhawk 6C/Pix32 v6" (`Designer:137-148`).
  - `label29` "Battery Capacity" + `TXT_battcapacity` + `label2` "mAh".
  - `CHK_speechbattery` — CheckBox "MP Alert on Low Battery".
  - `groupBox4` "Calibration": `label32` "1. Measured battery voltage:" + `TXT_measuredvoltage`; `label33` "2. Battery voltage (Calced):" + `TXT_voltage` (ReadOnly); `label34` "3. Voltage divider (Calced):" + `TXT_divider_VOLT_MULT`; `label3` "4. Measured current:" + `txt_meascurrent`; `label4` "5. Current (Calced)" + `txt_current` (ReadOnly); `label35` "6. Amperes per volt:" + `TXT_AMP_PERVLT` (`Designer:217-255`).
  - `timer1` — 1000 ms (live voltage/current).
- **Functionality** (`ConfigBatteryMonitoring.cs`):
  - Gate: open link + **BATT_MONITOR** present (`cs:20`).
  - `CMB_batmontype.setup(...)` → **BATT_MONITOR** (`cs:28`).
  - Reads: **BATT_CAPACITY**→TXT_battcapacity; `cs.battery_voltage`→TXT_voltage/TXT_measuredvoltage; **BATT_AMP_PERVLT / BATT_AMP_PERVOLT / AMP_PER_VOLT** → TXT_AMP_PERVLT; **BATT_VOLT_MULT / VOLT_DIVIDER** → TXT_divider_VOLT_MULT (`cs:32-51`).
  - Speech from `speechbatteryenabled`/`speechenable` settings (`cs:53`).
  - Sensor-type auto-detect: matches AMP_PERVLT+VOLT_MULT value pairs to `CMB_batmonsensortype` index (`cs:64-103`).
  - HW board auto-detect: reads **BATT_VOLT_PIN** (and **BATT_CURR_PIN** for vrbrain) → CMB_HWVersion index (`cs:106-164`).
  - `TXT_battcapacity_Validated` → setParam **BATT_CAPACITY** (`cs:196`).
  - `CMB_batmontype_SelectedIndexChanged`: enables/disables sub-controls; sel 0 writes **BATT_VOLT_PIN=-1**, **BATT_CURR_PIN=-1**; writes **BATT_MONITOR**; if 0→nonzero re-reads param list + re-Activate (`cs:205-266`).
  - `TXT_measuredvoltage_Validated`: `new_divider=(measured*divider)/voltage`, writes {VOLT_DIVIDER, BATT_VOLT_MULT} (`cs:296`).
  - `TXT_divider_Validated`: writes {VOLT_DIVIDER, BATT_VOLT_MULT} (`cs:320`).
  - `TXT_ampspervolt_Validated`: writes {AMP_PER_VOLT, BATT_AMP_PERVOLT, BATT_AMP_PERVLT} (`cs:344`).
  - `CMB_batmonsensortype_SelectedIndexChanged`: hardcoded preset math per sensor computing divider+amps/volt (`cs:356-462`).
  - `CMB_apmversion_SelectedIndexChanged`: writes **BATT_VOLT_PIN + BATT_CURR_PIN** pairs per board (`cs:483-563`).
  - `CHK_speechbattery_CheckedChanged`: speech settings + three `InputBox` popups (message / volt / percent) (`cs:565-601`).
  - `timer1_Tick`: `cs.battery_voltage`→TXT_voltage, `cs.current`→txt_current.
  - `txt_meascurrent_Validated`: amps/volt from measured vs live current (`cs:621-653`).
- **Avalonia status**: **PARTIAL** — `ConfigBatteryMonitoringViewModel` (`ConfigParamPages.cs:26-37`) is a generic `ParamPageBase` list (BATT_MONITOR, BATT_CAPACITY, BATT_VOLT_PIN/CURR_PIN combos, BATT_VOLT_MULT, BATT_AMP_PERVLT, BATT_AMP_OFFSET). Missing all UX: sensor-type presets + preset math, HW-version board dropdown (VOLT/CURR pin pairs), live voltage/current timer, measured-voltage/current → divider/amps-per-volt calibration, low-battery speech popups.

### Battery Monitor 2 (`ConfigBatteryMonitoring2`)

- **Controls** (`ConfigBatteryMonitoring2.Designer.cs`):
  - `pictureBox5` — PictureBox (`Resources.BR_APMPWRDEAN_2`).
  - `label30` "Monitor" + `mavlinkComboBox1` (`MavlinkComboBox`, DropDownList).
  - `label47` "Volt Pin" + `mavlinkComboBox2`; `label1` "Current Pin" + `mavlinkComboBox3`.
  - `label29` "Battery Capacity" + `TXT_battcapacity` + `label2` "mAh".
  - `CHK_speechbattery` "MP Alert on Low Battery".
  - `groupBox4` "Calibration": same six rows as page 1 (TXT_measuredvoltage, TXT_voltage RO, TXT_divider, txt_meascurrent, txt_current RO, TXT_ampspervolt).
  - `timer1` — 1000 ms.
  - (No sensor-preset / HW-version combos; pin selection via two `MavlinkComboBox`.)
- **Functionality** (`ConfigBatteryMonitoring2.cs`):
  - Gate: **BATT2_MONITOR** present (`cs:19`).
  - Reads **BATT2_CAPACITY**→TXT_battcapacity; `cs.battery_voltage2`→TXT_voltage; **BATT2_VOLT_MULT**→TXT_divider; **BATT2_AMP_PERVOL**→TXT_ampspervolt (`cs:27-38`).
  - `mavlinkComboBox1/2/3.setup` → **BATT2_MONITOR, BATT2_VOLT_PIN, BATT2_CURR_PIN** (`cs:52-57`).
  - `TXT_battcapacity_Validated` → **BATT2_CAPACITY** (`cs:82`).
  - `TXT_measuredvoltage_Validated` / `TXT_divider_Validated` → **BATT2_VOLT_MULT** (`cs:113,127`).
  - `TXT_ampspervolt_Validated` / `txt_meascurrent_Validated` → **BATT2_AMP_PERVOL** (`cs:141,246`).
  - `timer1_Tick`: `cs.battery_voltage2`→TXT_voltage, `cs.current2`→txt_current (`cs:162`).
  - `CHK_speechbattery_CheckedChanged`: identical speech + 3 InputBox popups as page 1 (`cs:168-204`).
- **Avalonia status**: **PARTIAL** — `ConfigBatteryMonitoring2ViewModel` (`ConfigParamPages.cs:39-49`) lists BATT2_MONITOR, BATT2_CAPACITY, BATT2_VOLT_PIN/CURR_PIN combos, BATT2_VOLT_MULT, BATT2_AMP_PERVOL (bound-param coverage matches). Missing: live voltage/current timer, measured-voltage/current calibration math, speech popups.

### Antenna Tracker (`ConfigAntennaTracker`)

- **Controls** (`ConfigAntennaTracker.Designer.cs`) — 8 GroupBoxes:
  - `groupBox22` "Board Orientation": `mavlinkComboBox1` (`MavlinkComboBox` DropDownList).
  - `groupBox25` "Yaw Servo": `label16` "Type" + `mavlinkComboBoxservo_yaw_type`; `label91` "Min"/`label90` "Max"/`label17` "Neutral" + `mavlinkNumericUpDown1/2/3`; `mavlinkCheckBox1` "Reverse"; `myTrackBar1` (`MyTrackBar` 1000-2000); `BUT_test_yaw` "Test"; `lbl_yawpwm`.
  - `groupBox3` "Pitch Servo": `label15` "Type" + `mavlinkComboBoxservo_pitch_type`; `label3` "Min"/`label2` "Max"/`label1` "Neutral" + `mavlinkNumericUpDown6/5/4`; `mavlinkCheckBox2` "Reverse"; `myTrackBar2`; `BUT_test_pitch` "Test"; `lbl_pitchpwm`.
  - `groupBox4` "Yaw Range of Movement": `label4` "Range (deg)" + `mavlinkNumericUpDown7`.
  - `groupBox5` "Pitch Range of Movement": `label5` "Min (deg)" + `mavlinkNumericUpDown8`; `label18` "Max (deg)" + `mavlinkNumericUpDown19`.
  - `groupBox6` "Yaw Gain": `label8` "P"/`label7` "I"/`label6` "D"/`label88` "IMAX"/`label9` "Rate Max" + `mavlinkNumericUpDown9/10/11/12/13`.
  - `groupBox7` "Pitch Gain": `label14` "P"/`label13` "I"/`label11` "D"/`label12` "IMAX"/`label10` "Rate Max" + `mavlinkNumericUpDown14/15/16/17/18`.
  - `groupBox1` "Altitude Source": `mavlinkComboBoxalt_source`.
  - `toolTip1`, `timer1`.
- **Functionality** (`ConfigAntennaTracker.cs`):
  - Gate: enabled only when `cs.firmware == Firmwares.ArduTracker` (`cs:32`).
  - Combos: **AHRS_ORIENTATION** (mavlinkComboBox1), **SERVO_YAW_TYPE**, **SERVO_PITCH_TYPE**, **ALT_SOURCE** (`cs:46-58`).
  - NUD/Checkbox: yaw **RC1_MIN/RC1_MAX/RC1_TRIM/RC1_REV**; pitch **RC2_MIN/RC2_MAX/RC2_TRIM/RC2_REV**; ranges **YAW_RANGE** (0-360), **PITCH_MIN** (-90..90), **PITCH_MAX**; yaw gain **YAW2SRV_P/_I/_D/_IMAX/YAW_SLEW_TIME**; pitch gain **PITCH2SRV_P/_I/_D/_IMAX/PITCH_SLEW_TIME** (`cs:62-90`).
  - `ProcessCmdKey` Ctrl+S → `BUT_writePIDS_Click` (writes `changes` hashtable, doubles-value confirm popup) (`cs:99,136`).
  - `BUT_rerequestparams_Click`: `getParamList()` + re-Activate (`cs:183`).
  - `BUT_refreshpart_Click`: per-control `GetParam` (`cs:206-242`).
  - `BUT_test_yaw_Click` / `BUT_test_pitch_Click`: maps trackbar value (respecting reverse) to PWM and sends `MAV_CMD.DO_SET_SERVO` on channel 1 / 2 (`cs:244-291`).
  - `timer1_Tick`: `cs.ch1out`→lbl_yawpwm, `cs.ch2out`→lbl_pitchpwm (`cs:298`).
- **Avalonia status**: **PARTIAL** — `ConfigAntennaTrackerViewModel.cs` is a `ParamPageBase` declaring every upstream param exactly (4 combos incl. RC reverse, RC min/max/trim, ranges, both PID groups) — param coverage complete. Missing interactive bits: live yaw/pitch PWM labels + timer, the two servo Test trackbars/buttons (DO_SET_SERVO), the per-section refresh button, servo grouping/trackbar UX.

### ADSB (`ConfigADSB`)

- **Controls** (`ConfigADSB.Designer.cs`):
  - `tableLayoutPanel1` — actually a `Panel` (scroll container) dynamically filled with parameter rows.
  - `BUT_rerequestparams` "Refresh Params" (`MyButton`); `BUT_writePIDS` "Write Params"; `BUT_Find` "Find".
  - `panel1` (disabled UAvionix block, inert): `label1` "Aircraft Registration" + `txt_acreg` + `but_saveacreg` "Save"; `label2` "Flight Identification:" + `txt_flid` + `but_saveflid` "Save".
  - Dynamic rows: `RangeControl`, `ValuesControl` (ComboBox-backed), or `MavlinkCheckBoxBitMask` per param metadata.
- **Functionality** (`ConfigADSB.cs`):
  - `ParameterMode = ParameterMetaDataConstants.Standard` (`cs:156`).
  - `BindParamList`/`SortParamList`: enumerates `MAV.param`, keeps params with display-name metadata, filters to keys `StartsWith("ADSB_")` or `StartsWith("AVD_")`, favourites (`Settings fav_adsb`) first (`cs:399-408`). Shows **all ADSB_* and AVD_*** params.
  - `AddControl`: RangeControl (range/increment, centi-degrees scaling), else `MavlinkCheckBoxBitMask` (bitmask), else `ValuesControl` (values) — Full-Param-style editor (`cs:421-620`).
  - `BUT_writePIDS_Click`: `list.SortENABLE()` then `setParam` each changed (ENABLE last), success "Parameters successfully saved." (`cs:179-205`).
  - `BUT_rerequestparams_Click`: `MessageShowAgain` confirm → `getParamList()` → re-Activate (`cs:212-236`).
  - `BUT_Find_Click`: `InputBox.Show("Search For", …)` with live `_filterTimer` (500 ms) → `filterList` (`cs:21-116`).
  - `Ctrl+S` → `BUT_writePIDS_Click` (`cs:159`).
  - UAvionix flight-id / registration (txt_flid, txt_acreg, save buttons, UAVIONIX_ADSB_* messages) is **commented out / inert** (`cs:260-305, 667-709`).
- **Avalonia status**: **PARTIAL** — `ConfigADSBViewModel` (`ConfigParamPages.cs:64-80`) does `FByPrefix("ADSB_")` + `FByPrefix("AVD_")` with `OnRefreshed` repopulation (matches upstream's dynamic list). Missing: Find/search filter, explicit Write/Refresh button semantics with ENABLE-last ordering and confirm/success popups, favourites ordering. (Inert UAvionix panel correctly skippable.)

---

## Optional Hardware — motor / mount / GPS order / compassmot

### Motor Test (`ConfigMotorTest`)

- **Controls**: single `GroupBox groupBox1` "Motor Test" (`Designer:102-118`) containing:
  - `NumericUpDown NUM_thr_percent` — throttle %, Min -100, default 5 (`:50-63`); `Label label1` "Throttle %".
  - `Label label2` (multiline): "NOTE: PLEASE HOLD DOWN YOUR UAV\nThis will test your motors are working.\nMotors are tested in a clockwise rotation …".
  - `LinkLabel linkLabel1` "Please click here to see your motor numbers,\nscroll to the bottom of the page" → wiki (`:75-80`).
  - `NumericUpDown NUM_duration` — duration (s), Max 999, default 2 (`:82-95`); `Label label3` "Duration (s)".
  - `Label label4` "Set the min % that will be output when armed, but still on the ground"; `Label label5` "Set the min % that will be output when flying".
  - `MyButton but_mot_spin_arm` "Set Motor Spin Arm" (`:138-144`); `MyButton but_mot_spin_min` "Set Motor Spin Min" (`:125-131`).
  - `Label FrameClass` / `Label FrameType` — set at runtime to "Class: …" / "Type: …".
  - **Dynamically created** in `Activate()` (`cs:43-109`): per-motor `MyButton` "Test motor A", "Test motor B", … (up to `motormax`, lettered), each with optional `Label` "Motor Number: N, <rotation>"; plus "Test all motors", "Stop all motors", "Test all in Sequence".
- **Functionality** (`ConfigMotorTest.cs`):
  - `motormax` from `FRAME`/`Q_FRAME_TYPE`/`FRAME_TYPE`, `FRAME_CLASS`+`FRAME_TYPE` or `Q_FRAME_CLASS`+`Q_FRAME_TYPE` (`cs:111-200`); rover/boat = 4. Layout/rotation from external `APMotorLayout.json` (version "AP_Motors library test ver 1.2") via `lookup_frame_layout` (`cs:236-261`).
  - Test buttons → `testMotor()` → `MAV_CMD.DO_MOTOR_TEST` with `MOTOR_TEST_THROTTLE_PERCENT`, speed=NUM_thr_percent, time=NUM_duration, motorcount (sequence sends count=motormax) (`cs:305-327`). Denied → "Command was denied by the autopilot".
  - `but_mot_spin_arm_Click`: reads/writes **MOT_SPIN_ARM** (deadzone+2%, only if throttle<20) via `setParamAsync`, InputBox prompt (`cs:341-367`).
  - `but_mot_spin_min_Click`: reads/writes **MOT_SPIN_MIN** (arm min+3%) (`cs:369-396`).
  - `linkLabel1` → `https://ardupilot.org/copter/docs/connect-escs-and-motors.html#motor-order-diagrams`.
- **Avalonia status**: **PARTIAL** — `ConfigMotorTestViewModel` (`ConfigCalibrationPages.cs:129-173`) has fixed 8 "Test Motor N" actions + "Test All (sequence)", `ThrottlePercent`/`DurationSec`, calls `comPort.doMotorTest`. Missing: dynamic motormax/frame detection, FrameClass/FrameType + APMotorLayout.json rotation labels, per-motor "Stop all", non-sequence "Test all motors", MOT_SPIN_ARM/MOT_SPIN_MIN buttons, wiki link, NOTE text.

### Compass/Motor Calibration (`ConfigCompassMot`)

- **Controls** (`ConfigCompassMot.Designer.cs`):
  - `MyButton BUT_compassmot` "Start" (toggles to Finish at runtime) (`:41-49`).
  - `TextBox txt_status` — multiline status log (`:51-57`).
  - `Timer timer1` — refreshes `txt_status` from `MAV.cs.messages` (`:59-61`).
  - Hidden `Label lbl_start` "Start", `Label lbl_finish` "Finish" (button-text source, Visible=false) (`:63-81`).
  - `Label lbl_status` "Compass Motor Calibration" — live status readout (`:83-90`).
  - `ZedGraph.ZedGraphControl zedGraphControl1` (`:92-107`) — plots Interference % (Y, red) vs Throttle % (X) and Amps (Y2, green "Current").
- **Functionality** (`ConfigCompassMot.cs`):
  - `Activate()`: sets button text, subscribes `MAVLINK_MSG_ID.COMPASSMOT_STATUS` (`cs:25-30`).
  - `BUT_compassmot_Click`/`DoCompassMot`: toggles; start sends `MAV_CMD.PREFLIGHT_CALIBRATION` with param7=1 (compassmot) (`cs:50-74`); error → "Compassmot requires AC 3.2+". Finish/stop sends `SendAck()`.
  - `ProcessCompassMotMSG` (`cs:87-119`): parses `mavlink_compassmot_status_t` (throttle/10, interference, current, CompensationX/Y/Z) → graph + `lbl_status`.
  - `timer1_Tick`: dumps `MAV.cs.messages` into `txt_status`. `Deactivate()`: `SendAck()`, unsubscribe, stop timer.
- **Avalonia status**: **PARTIAL** — `ConfigCompassMotViewModel.cs` (`ActionPageViewModel`) Title "Compass/Motor Calibration", Instructions, Start/Finish actions. Start sends `PREFLIGHT_CALIBRATION(…,1,0)`; Finish sends `SendAck()`; logs via `AppendLog`. Missing: COMPASSMOT_STATUS subscription/parsing, ZedGraph plot (interference/current vs throttle), live lbl_status readout (current/compensation X-Y-Z/throttle/interference), timer-driven message-log streaming, single Start↔Finish toggle button (uses two actions).

### Camera Gimbal / Mount (`ConfigMount`)

- **Controls** (flat layout; `ConfigMount.designer.cs` — note lowercase `d`):
  - Top: `MavlinkComboBox CMB_mnt_type` → MNT_TYPE (`:1143-1150`); `Label label43` "Type"; `Label label44` "NOTE: the gimbal type takes effect on the next reboot of the fight controller".
  - Three axis columns — **Tilt** (`label5` "Tilt", `pictureBox1`), **Roll** (`label6` "Roll", `pictureBox2`), **Pan** (`label15` "Pan", `pictureBox3`). Each axis has:
    - Function `ComboBox` (DropDownList): `mavlinkComboBoxTilt/Roll/Pan` (`:320-342`) — items from enum `Channelap`/`Channelac` (Disable, RC5–RC14 or SERVO1–SERVO14; filtered "RC" vs "SERVO" by `SERVO1_MIN` presence).
    - "Angle Limits" (`label9/16/1`) with Min/Max NUDs: Tilt `TAM`/`TAMX`, Pan `PAM`/`PAMX`, Roll `RAM`/`RAMX`.
    - "Servo Limits" (`label10/17/2`) with Min/Max: Tilt `TSM`/`TSMX`, Pan `PSM`/`PSMX`, Roll `RSM`/`RSMX` (NUDs 800–2200).
    - Min/Max sub-labels `label11/13/18/20/3/7`="Max", `label12/14/19/21/4/8`="Min".
    - `MavlinkCheckBox mavlinkCheckBoxTR/PR/RR` "Reverse" (`:493-501`).
    - `MavlinkComboBox CMB_inputch_tilt/roll/pan` (`:359-384`) with `label22/23/24` "Input Ch".
  - `MavlinkCheckBox CHK_stab_tilt` "Stabalise Tilt", `CHK_stab_roll` "Stabalise Roll" (`:929-945`).
  - `GroupBox groupBox4` "Retract Angles" → `NUD_RETRACT_x/y/z` with `label25/26/27` = X/Y/Z (`:737-831`).
  - `GroupBox groupBox5` "Neutral Angles" → `NUD_NEUTRAL_x/y/z` with `label30/29/28` = X/Y/Z (`:833-927`).
  - Shutter: `label41`/`label35` "Shutter", `ComboBox CMB_shuttertype` (enum `ChannelCameraShutter`), `pictureBox4`, `groupBox7`, `label36` "Servo Limits", `mavlinkNumericUpDownShutM`/`ShutMX` (`label40`/`label39` Max/Min), `label37` "Not Pushed"/`label38` "Pushed" → `shut_notpushed`/`shut_pushed`, `label34` "Duration (1/10th sec)" → `shut_duration`, `label42` "Please set the Ch7 Option to Camera Trigger".
  - `LinkLabel LNK_wiki` "Wiki".
- **Functionality** (`ParamHead = "MNT_"`, `ConfigMount.cs`):
  - Constructor: combos from `Channelap` (ArduPlane) or `Channelac` enums; filters RC/SERVO by `SERVO1_MIN`; `CMB_mnt_type.setup("MNT_TYPE")` (`cs:100-101`).
  - `Activate()`: disabled unless **CAM_TRIGG_TYPE** present. Reads CAM_TRIGG_TYPE; scans all `*_FUNCTION` (6=Pan, 7=Tilt, 8=Roll, 10=Shutter) → combo text (`cs:104-141`). Then `setup`:
    - **MNT_STAB_TILT, MNT_STAB_ROLL** (CHK_stab_*); **MNT_NEUTRAL_X/Y/Z, MNT_RETRACT_X/Y/Z** (-180..180).
    - Tilt: `<chan>_MIN`/`_MAX`, **MNT_ANGMIN_TIL/MNT_ANGMAX_TIL**, reverse `<chan>_REV`/`_REVERSED`, **MNT_RC_IN_TILT** (`cs:250-257`).
    - Roll: **MNT_ANGMIN_ROL/MNT_ANGMAX_ROL, MNT_RC_IN_ROLL** (`cs:277-284`).
    - Pan: **MNT_ANGMIN_PAN/MNT_ANGMAX_PAN, MNT_RC_IN_PAN** (`cs:302-309`).
    - Shutter: `<chan>_MIN`/`_MAX`, **CAM_SERVO_ON, CAM_SERVO_OFF, CAM_DURATION** (`cs:224-229`).
  - `mavlinkComboBox_SelectedIndexChanged` (`cs:344-371`): clears conflicting `*_FUNCTION` via `ensureDisabled`, writes `<chan>_FUNCTION` (6/7/8/10), sets **CAM_TRIGG_TYPE** (Relay=1, Transistor=4, servo=0), sets **MNT_MODE=3**.
  - `LNK_wiki` → `http://copter.ardupilot.com/wiki/.../common-camera-gimbal/`.
- **Avalonia status**: **PARTIAL** — `ConfigMountViewModel` (`ConfigParamPages.cs:162-181`) is a generic `ParamPageBase` listing params by prefix `MNT` + CAM_TRIGG_TYPE/CAM_DURATION/CAM_SERVO_ON/CAM_SERVO_OFF. Missing entirely: 3-axis UI (function/angle/servo/reverse/input-ch combos), `*_FUNCTION` write-back + `ensureDisabled` conflict clearing, shutter mapping, Retract/Neutral group boxes, MNT_MODE=3 side-effect, ArduPlane vs Copter channel enums, pictures, wiki link.

### CAN GPS Order (`ConfigGPSOrder`)

- **Controls** (`ConfigGPSOrder.Designer.cs`):
  - `Label label6` "UAVCAN GPS Order" (header, 12pt) (`:47-55`).
  - `GroupBox groupBox5` (divider line, no text) (`:57-65`).
  - `Label label1` "Set the GPS order if required" (`:67-74`).
  - `MyDataGridView myDataGridView1` — read-only, FullRowSelect, AutoGenerateColumns=false (`:76-99`), bound to `bindingSource1` (type `GPSCAN`). Columns: `orderDataGridViewTextBoxColumn` "Order" (Order), `nodeIDDataGridViewTextBoxColumn` "NodeID" (NodeID), `nameDataGridViewTextBoxColumn` "Name" (Name), `DataGridViewButtonColumn GPS1` "GPS1" (button "Override 1"), `DataGridViewButtonColumn GPS2` "GPS2" (button "Override 2").
- **Functionality** (`ConfigGPSOrder.cs`):
  - `Activate()`: disabled unless **GPS1_CAN_OVRIDE** present. Reads **GPS_CAN_NODEID1, GPS_CAN_NODEID2** (detected) and **GPS1_CAN_OVRIDE, GPS2_CAN_OVRIDE** (override). Builds `GPSCAN` rows: Order 1/2 = "GPS Override 1/2", Order 98/99 = "GPS Detect 1/2" (only if non-zero and not already an override) (`cs:21-51`).
  - `myDataGridView1_CellContentClick` (`cs:62-86`): GPS1 button → `setParam("GPS1_CAN_OVRIDE", <NodeID of clicked row>)`; GPS2 button → `setParam("GPS2_CAN_OVRIDE", …)`; then re-Activate. Errors → CustomMessageBox.
- **Avalonia status**: **PARTIAL** — `ConfigGPSOrderViewModel` (`ConfigParamPages.cs:82-90`) is a `ParamPageBase` listing the four params as editable fields. Missing: the DataGridView with detected-vs-override rows and the "Override 1/Override 2" per-row buttons that write the clicked node's ID into the override param.

### Serial (`ConfigSerial`)

- **Controls** (designer = header row only; body built at runtime):
  - `TableLayoutPanel tableLayoutPanel1` — 5 columns (152/114/132/307/10), 12 rows (`:31-68`).
  - Header `MyLabel` row 0: `myLabel1` "Port Name", `myLabel2` "Speed", `myLabel3` "Protcol" (sic), `myLabel4` "Options" (`:70-108`).
  - **Runtime rows** (`Activate()`): per serial port `i`, col 0 bold `Label` "SERIAL PORT i\n<uartName>" (name from MAVFtp `@SYS/uarts.txt`, may append " (RTS/CTS)"/" (RTS/CTS Auto)"); col 1 baud `ComboBox`; col 2 protocol `ComboBox`; col 3 options `Label` (active bit names joined by " / "); col 4 `MyButton` "Set Bitmask" opening a `MavlinkCheckBoxBitMask` popup. Final spanning note "Note: Changes to the serial port settings will not take effect until the board is rebooted.".
- **Functionality** (`ConfigSerial.cs`):
  - Loads `SerialOptionRules.json` (`SerialOptionRuleItem`: PresetBaudRate/PresetOptionsByte/Comment) (`cs:40-75`); downloads `@SYS/uarts.txt` via `MAVFtp` for port names (`cs:78-161`).
  - Discovers port count by scanning `SERIALx_BAUD` (`cs:163-176`). Reads `BRD_SERx_RTSCTS` for flow-control suffix.
  - Per port binds combos to **SERIALi_BAUD** and **SERIALi_PROTOCOL** (options from metadata), label/bitmask to **SERIALi_OPTIONS** (`cs:225-358`). Changes → `setParam`.
  - Protocol change → `doApplyRules` (`cs:384-430`): applies preset baud (SERIALi_BAUD) and options (SERIALi_OPTIONS) from JSON rule, sets note to rule comment; warns if ≥4 ports set to MAVLink (protocol 1/2): "Warning: Maximum number of Mavlink ports are 5 including the USB port!".
- **Avalonia status**: **MISSING** — no `ConfigSerial*` VM anywhere (grep confirms none). Needs full implementation: dynamic per-port baud/protocol/options grid, uarts.txt MAVFtp lookup, SerialOptionRules.json rules, bitmask popup, reboot/MAVLink-count notes.

---

## Optional Hardware — RTK/GPS inject, secure, user

### RTK/GPS Inject (`ConfigSerialInjectGPS`)

- **Controls** (`ConfigSerialInjectGPS.Designer.cs`; strings from `.resx`):
  - **Connection row**: `CMB_serialport` (ComboBox; runtime items `cs:89-93`: serial port names + "UDP Host", "UDP Client", "TCP Client", "NTRIP"), `CMB_baudrate` (ComboBox), `BUT_connect` "Connect".
  - `groupBox3` "Link Status": `label3` "Input data rate", `lbl_status1` "9999 bps", `label4` "Output data rate", `lbl_status2`, `label5` "RTCM Base", `lbl_status3` "-00.000000000 000.000000000 0000.000000000", `label6` "Messages Seen", `labelmsgseen` ("rtcm0000\nrtcm0000").
  - `groupBox2` "RTCM" counters: `label11` "Base"/`labelbase`, `label12` "Gps"/`labelgps`, `label13` "Glonass"/`labelglonass`, `label15` "Beidou"/`label14BDS`, `label16` "Galileo"/`labelGall`.
  - `chk_rtcmmsg` "Inject MSG Type".
  - `groupBox_autoconfig` "Automatic Config Options": `chk_autoconfig` "Automatically Configure Receiver", `comboBoxConfigType` (values "UBlox M8P/F9P", "Septentrio", "Unicore UM982" — `cs:455,483,496`), panels `panel_ubloxoptions`, `panel_septentrio`, `panel_um982`.
  - uBlox `panel_ubloxoptions`: `chk_m8p_130p` "M8P fw 130+/F9P", `groupBox1` "Survey In" (`lbl_svin` "Survey In", `label1` "SurveyIn Acc(m)"/`txt_surveyinAcc` "2.00", `label2` "Time(s)"/`txt_surveyinDur` "60", `but_restartsvin` "Restart", `but_save_basepos` "Save Current Position").
  - Septentrio `panel_septentrio`: `chk_septentriofixedposition` "Fixed Position", `label14` "Atitude (WGS84)"/`input_septentriofixedatitude`, `label17` "Longitude (WGS84)"/`input_...longitude`, `label18` "Altitude (m)"/`input_...altitude`, `button_septentriosetposition` "Set Position", `label19` "RTCM Message Amount"/`cmb_septentriortcmamount`, `label20` "RTCM Message Interval (s)"/`input_...interval` (NUD)/`button_septentriortcminterval` "Set Interval", `label21` "RTCM Constellation Usage" with `chk_septentriogps` "GPS", `chk_septentrioglonass` "GLONASS", `chk_septentriobeidou` "BeiDou", `chk_septentriogalileo` "Galileo".
  - `label22` long survey-in explainer ("At connect the receiver will start a "survey-in"…").
  - `dg_basepos` (`MyDataGridView`): columns Lat, Long, Alt, BaseName1, `Use` (ButtonColumn "Use" `:736`), `Delete` (ButtonColumn "Delete" `:743`).
  - `myGMAP1` (map with base marker), `splitContainer1` (Panel1 collapsed `cs:99`), `panel1`/`panel2`, NTRIP `chk_sendgga` "Send NTRIP GGA? (VRS/Smart)", `check_sendntripv1` "Send NTRIP protocol v1.0 ?", `timer1`, `toolTip1`.
- **Functionality** (`ConfigSerialInjectGPS.cs`):
  - **No MAVLink params.** Persists UI to `Settings.Instance` keys (`cs:101-157`): `SerialInjectGPS_port, _baud, _AutoConfigType, _autoconfig, _m8p_130p, _SIAcc, _SITime`, full Septentrio set (`_SeptentrioFixedAtitude/Longitude/Altitude/FixedPosition/GPS/GLONASS/Galileo/BeiDou/RTCMLevel/RTCMInterval`).
  - `BUT_connect_Click` (`cs:301`) toggles; `DoConnect` (`cs:326`) builds inject `comPort` by `CMB_serialport.Text` (`switch cs:334`): "NTRIP"→`CommsNTRIP` (`.lat/.lng/.alt` from `MainV2.comPort.MAV.cs.PlannedHomeLocation`, `.ntrip_v1` from `check_sendntripv1`), "TCP Client", "UDP Host", "UDP Client", else serial `CommsSerialPort` (`PortName`/`BaudRate=int.Parse(CMB_baudrate.Text)`, ReadBufferSize=64KB).
  - Auto-config after open: uBlox (`cs:455`) sends UBX survey-in using `txt_surveyinAcc`/`txt_surveyinDur`/`chk_m8p_130p`; Septentrio (`cs:483` → `ConfigureSeptentrioReceiver cs:553`); Unicore UM982 (`cs:496` → `ConfigureUnicoreReceiver cs:525`; inline cmd bytes `cs:407-411`).
  - `mainloop` (static, `cs:615`): reads inject `comPort`, parses RTCM3/UBX, reconnects on timeout (NTRIP/UDP/TCP); `sendData` (`cs:1136`) forwards via `port.InjectGpsData(MAV.sysid, MAV.compid, data, length, rtcm_msg)` to every MAVLink link (per-MAV gating `cs:1120-1134`).
  - RTCM/UBX: `Rtcm3_ObsMessage` (`cs:167`), `seenRTCM` per-constellation counters (`cs:807`), `ProcessUBXMessage` (`cs:877` — UBX-NAV-PVT/SVIN/HPPOSLLH set `MAV.cs.Base`), `updateSVINLabel` (`cs:991`), `ExtractBasePos` (`cs:1062`).
  - Base-position mgmt: `loadBasePosList`/`saveBasePosList` (JSON, `cs:259,284`), `updateBasePosDG`, grid Use/Delete cell-click (`cs:1308`), `but_save_basepos_Click` (`cs:1260`), `but_restartsvin_Click` (`cs:1396`).
  - Map marker + status in `timer1_Tick` (`cs:1161`).
- **Avalonia status**: **PARTIAL** (NTRIP-only) — `ConfigGpsInjectViewModel.cs` (wired "RTK/GPS Inject" `SetupViewModel.cs:31`) covers only the NTRIP caster path: Host/Port(2101)/Mount/Username/Password/NtripV1/SendGga, `ToggleConnect`, background `Loop()` reading `CommsNTRIP` → `_comPort.InjectGpsData(packet, length)`, byte/rate stats (`Injected`), `Status`. Missing: serial/UDP/TCP modes; baud selection; uBlox/Septentrio/Unicore auto-config; survey-in (acc/duration/restart); RTCM3/UBX parsing + per-constellation "Messages Seen"; in/out data-rate + RTCM Base lat/lon/alt display; base-position save list + DataGridView (Use/Delete); GMap base marker; Septentrio fixed-position/constellation/RTCM-interval panel; multi-link `InjectGpsData(sysid,compid,…,rtcm_msg)` fan-out + "Inject MSG Type" toggle. Uses live `MAV.cs.lat/lng/altasl` for GGA instead of `PlannedHomeLocation`.

### Secure (`ConfigSecure`)

- **Controls** (`ConfigSecure.Designer.cs`, size 477×285): `label1` "CubeOrange Only - DO NOT USE  UNLESS YOU UNDERSTAND THE CONSEQUENCE" (`:69`). Three group boxes:
  - `groupBox1` "Always" (`:96`): `but_login` "Login" (`:190`), `but_getsn` "Enter Bootloader Mode" (`:201`).
  - `groupBox2` "One Time" (`:107`): `but_dfu` "Enter DFU Mode" (disabled, `:169`), `but_bootloader` "Get Bootloader" (disabled, `:180`).
  - `groupBox3` "Firmware" (`:117`): `but_firmware` "Get Firmware" (disabled, `:158`).
  - `textBox1` (ReadOnly, device SN, `:56`), `label3` "Device SN" (`:126`), `txt_sha` (ReadOnly multiline, `:144`), `label5` "Fw SHA" (`:212`), `progressBar1` (`:73`), `label2` "...." (status, `:85`), `label4` "...." (device-count status, `:139`). `timer1` (Interval 4000 ms, `:130-131`).
- **Functionality** (`ConfigSecure.cs`): CubePilot **cloud secure-boot signing** over HTTPS + px4uploader/DFU, NOT MAVLink. No MAVLink params. Endpoints (`cs:34-44`): `secure.cubepilot.com/api/Auth/GetToken`, `/api/Auth/Login`, `/api/Firmware/CreateFirmware`, `/api/Firmware/CreateBootLoader`, `/CubeOrange_dfusetup.apj`, `/api/Firmware/CheckSN`.
  - `but_login_Click` (`cs:60`): loopback `TcpListener` on random port, opens browser to Login with `return_url`, captures bearer token → `Authorization: bearer` header.
  - `but_getsn_Click` (`cs:121`): device scan, "Please re-power to autopilot".
  - `Instance_DeviceChanged` (`cs:133`): parallel-scans serial ports with `px4uploader.Uploader`, `up.identify()`, SN as hex → `textBox1`.
  - `but_dfu_Click` (`cs:181`): `CheckSN`, downloads `CubeOrange_dfusetup.apj`, uploads via `Uploader`.
  - `but_bootloader_Click` (`cs:205`): `DFU.GetSN()`, POST `CreateBootLoader?SN=`, `DFU.Flash(tempfile, 0x08000000)`.
  - `but_firmware_Click` (`cs:227`): `OpenFileDialog` `*.apj`, `Sha256Digest(fw.imagebyte)`→`txt_sha`, multipart POST `CreateFirmware?SN=`, uploads signed FW.
  - `timer1_Tick` (`cs:273`): enables/disables buttons by `UsbDevice.AllDevices` (DFU) and `Win32DeviceMgmt.GetAllCOMPorts()` (`board.EndsWith("BL")`, "SecureBL"); updates `label4` counts.
- **Avalonia status**: **PARTIAL / DIVERGENT** — `ConfigSecureViewModel.cs` (wired `SetupViewModel.cs:10`) implements a **completely different feature**: MAVLink `SECURE_COMMAND` public-key management (`GET_SESSION_KEY, GET_PUBLIC_KEYS, SET_PUBLIC_KEYS, REMOVE_PUBLIC_KEYS` over `mavlink_secure_command_t`/`SECURE_COMMAND_REPLY`), with commands `GetSessionKey, GetKeys, SetKeyFromFileAsync, RemoveKeys`, a `Keys` collection and `Log`. It does NOT implement the CubePilot cloud login (OAuth loopback), SN read via px4uploader, DFU flashing, bootloader/firmware download+sign, SHA display, or the Always/One-Time/Firmware layout. Per VM comments (`cs:140-146,161`) it cannot even sign SET/REMOVE (no Ed25519 signer). **Net: upstream ConfigSecure cloud signing is MISSING; the Avalonia "Secure" page is an unrelated MAVLink key-manager.**

### Secure AP / Sign Firmware (`ConfigSecureAP`)

- **Controls** (`ConfigSecureAP.Designer.cs`, size 511×224): `OpenFileDialog openFileDialog1` (Filter "Supported Files|*.bin;*.pem;*.apj", `:48`). Two group boxes:
  - `groupBox5` "Do Only Once" (`:138`): `but_generatekey` "Generate Key" (`:125`).
  - `groupBox4` "Files" (`:96`): `but_privkey` "Private Key" (`:78`), `but_bootloader` "BootLoader" (`:56`), `but_firmware` "Firmware" (`:67`); TextBoxes `txt_pubkey` (`:114`), `txt_bl` (`:107`), `txt_fwapj` (`:100`). (Leftover unused `textBox1`/`label1`/`label2`/`groupBox1-3` decls `:156-163` not added.)
- **Functionality** (`ConfigSecureAP.cs`): local **Ed25519 firmware-signing tool** via BouncyCastle + `MissionPlanner.Utilities.SignedFW`. No MAVLink, no network. Holds `AsymmetricCipherKeyPair keyPair`.
  - `but_generatekey_Click` (`cs:88`): `SignedFW.GenerateKey()`, writes PEM via `PemWriter`, `SaveFileDialog` `*.pem`, also `_private_key.dat` ("PRIVATE_KEYV1:"+base64) and `_public_key.dat` ("PUBLIC_KEYV1:"+base64), shows pubkey in `txt_pubkey`, warns "Protect your private key, if lost there is no method to get it back.".
  - `but_privkey_Click` (`cs:30`): loads `*.pem;*.dat`; if `PRIVATE_KEYV1` → `SignedFW.GenerateKey(keyap)`, else `PemReader`→`Ed25519PrivateKeyParameters`; populates `txt_pubkey`.
  - `but_bootloader_Click` (`cs:53`): pick `*.bin`, `SignedFW.CreateSignedBL(keyPair, …)`, writes `<name>-signed.bin`.
  - `but_firmware_Click` (`cs:70`): pick `*.apj`, `SignedFW.CreateSignedAPJ(keyPair, …)`, writes `<name>-signed.apj`.
- **Avalonia status**: **MISSING** — no `ConfigSecureAP*` VM/View anywhere (grep for `SecureAP`/`SignedFW` finds nothing in src). Not registered in `SetupViewModel`/`ConfigViewModel`.

### User Params (`ConfigUserDefined`)

- **Controls** (`ConfigUserDefined.Designer.cs`): `MyUserControl` with one root: `tableLayoutPanel1` (`TableLayoutPanel`, 2 columns, Dock=Fill, `:31-46`). All rows generated at runtime in `LoadOptions()` (`cs:46-86`):
  - Row 0: `MyButton` "Modify" (Name "Modify"), column-spanned across 2 columns (`cs:52,63`).
  - Per user param present: a `Label` (col 0, Text/Name = param name, `cs:70`) plus either a `MavlinkComboBox` (enumerated options, `cs:79-81`) or `MavlinkNumericUpDown` (min/max range only, `cs:76-77`).
  - (resx `Name1`/`Color1`/`Bitmap1`/`Icon1` are VS template residue, not real controls.)
- **Functionality** (`ConfigUserDefined.cs`):
  - Default name list (`cs:19-44`): `CH6_OPT`…`CH16_OPT` and `RC6_OPTION`…`RC16_OPTION`.
  - On construct, overrides from `Settings.Instance["UserParams"]` (comma-split, `cs:15-16`).
  - "Modify" button (`cs:53-60`): `InputBox.Show("Params", "Enter Param Names", …)` multiline, re-splits on `, \n \r`, persists to `Settings.Instance["UserParams"]` (comma-joined), then re-Activate.
  - `LoadOptions` reads `MainV2.comPort.MAV.param` per name (skips absent, `cs:67-68`); options/range from `ParameterMetaDataRepository.GetParameterOptionsInt`/`GetParameterRange` keyed on `MAV.cs.firmware`. Combo/NUD `.setup(...)`-bound to live `MAV.param`, edits write the param directly.
  - `Activate()`→`LoadOptions()`; `Deactivate()` empty.
- **Avalonia status**: **PARTIAL** (placeholder) — stub `ConfigUserDefinedViewModel` (`ConfigParamPages.cs:254-258`, Title "User Params", intro text only) wired into the Config tab (`ConfigViewModel.cs:14`). Does NOT implement the dynamic param-name list, "Modify"/InputBox editor, `Settings["UserParams"]` persistence, or per-param combo/NUD generation with metadata options/ranges.

### Optional Hardware (`ConfigOptional`)

- **Controls** (`ConfigOptional.Designer.cs:32,44`): near-empty `MyUserControl` — intro/landing card. Single control: `label1` (`Label`), text from resx "The following pages are OPTIONAL configure them if you have aditional hardware, or other requirements." (`ConfigOptional.resx label1.Text`), at 25,25, size 246×78.
  - NOT itself a tree/menu page. In upstream the tree is built by the host (`GCSViews.ConfigTuning` / BackstageView), inserting `ConfigOptional` as the header node and the Config* user-controls as children. Sub-pages under it (upstream order): Sik Radio, Battery Monitor, Battery Monitor 2, RangeFinder, Airspeed, Adsb, PX4Flow, OpticalFlow, OSD, CameraGimbal, Antenna Tracker, Motor Test, BT, Parachute, ESP8266, DroneCAN/UAVCAN, RTK/GPS Inject (`ConfigSerialInjectGPS`), Joystick, Compass/MotorCalib, FFT, plus `ConfigUserDefined` ("User Params").
- **Functionality**: none. `ConfigOptional.cs:13` `Activate()` empty; implements `IActivate` only. Pure informational placeholder.
- **Avalonia status**: **DONE** (equivalent) — implemented as the ">> Optional Hardware" header row in `SetupViewModel.cs:27-29` via `new InfoPageViewModel("Optional Hardware", "Optional peripherals. Pick a sub-page.")`. The backstage list (SetupViewModel) is the container/tree; sub-pages registered as `sub: true` rows beneath it (`:31-51`). Wording differs slightly; functionally identical.

---

## Summary table

| Page | Avalonia VM | Status |
| --- | --- | --- |
| ConfigHWAirspeed | ConfigAirspeedViewModel (ParamPageBase) | PARTIAL |
| ConfigHWRangeFinder | ConfigRangeFinderViewModel (ParamPageBase) | PARTIAL |
| ConfigHWOptFlow | ConfigOptFlowViewModel (ParamPageBase) | PARTIAL |
| ConfigHWPX4Flow | ConfigPX4FlowViewModel | DONE |
| ConfigHWParachute | ConfigParachuteViewModel (ParamPageBase) | PARTIAL |
| ConfigHWOSD | — | MISSING |
| ConfigHWCompass2 | ConfigCompassViewModel (onboard mag-cal only) | PARTIAL |
| ConfigHWBT | ConfigHWBTViewModel | DONE |
| ConfigHWesp8266 | ConfigHWESP8266ViewModel | DONE |
| ConfigHWCAN | — | MISSING |
| ConfigDroneCAN | ConfigDroneCanViewModel | PARTIAL |
| ConfigBatteryMonitoring | ConfigBatteryMonitoringViewModel (ParamPageBase) | PARTIAL |
| ConfigBatteryMonitoring2 | ConfigBatteryMonitoring2ViewModel (ParamPageBase) | PARTIAL |
| ConfigAntennaTracker | ConfigAntennaTrackerViewModel (ParamPageBase) | PARTIAL |
| ConfigADSB | ConfigADSBViewModel (ParamPageBase) | PARTIAL |
| ConfigMotorTest | ConfigMotorTestViewModel (ActionPage) | PARTIAL |
| ConfigCompassMot | ConfigCompassMotViewModel (ActionPage) | PARTIAL |
| ConfigMount | ConfigMountViewModel (ParamPageBase) | PARTIAL |
| ConfigGPSOrder | ConfigGPSOrderViewModel (ParamPageBase) | PARTIAL |
| ConfigSerial | — | MISSING |
| ConfigSerialInjectGPS | ConfigGpsInjectViewModel (NTRIP only) | PARTIAL |
| ConfigSecure | ConfigSecureViewModel (unrelated key-manager) | PARTIAL/DIVERGENT |
| ConfigSecureAP | — | MISSING |
| ConfigUserDefined | ConfigUserDefinedViewModel (stub) | PARTIAL |
| ConfigOptional | SetupViewModel header row | DONE |
