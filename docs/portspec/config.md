# Port Spec — CONFIG / TUNING Section

Source: Mission Planner 1.3.83 (`external/MissionPlanner/`). This documents the
**CONFIG/TUNING** tab (WinForms host `GCSViews/SoftwareConfig.cs`), distinct from
the `InitialSetup` (SETUP) tab. Goal: human-verifiable 1:1 Avalonia port spec.

Port target: `src/MissionPlannerAvalonia/ViewModels/GCSViews/ConfigurationView/`.

---

## PART A — CONFIG nav tree

The CONFIG tab is hosted by `MissionPlanner.GCSViews.SoftwareConfig` (NOT
`InitialSetup`/`Setup.cs`). Pages are added to a `BackstageView` (left side-nav)
in `SoftwareConfig.SoftwareConfig_Load(...)`
(`external/MissionPlanner/GCSViews/SoftwareConfig.cs:142-321`) via
`AddBackstageViewPage(typeof(...), <label>, parent, advanced)`.

Page set + order is conditional on connection state, firmware, and the
`MainV2.DisplayConfiguration.display*` toggles. Labels resolve from
`ExtLibs/Strings/Strings.resx`. The full conditional order:

| # | Condition | Class loaded | Label (`Strings`) | Label text |
|---|-----------|--------------|-------------------|------------|
| 1 | connected + Copter + `displayGeoFence` | `ConfigAC_Fence` | `GeoFence` | **GeoFence** |
| 2 | connected + Copter + `displayBasicTuning` | `ConfigSimplePids` | `BasicTuning` | **Basic Tuning** |
| 3 | connected + Copter + `displayExtendedTuning` | `ConfigArducopter` | `ExtendedTuning` | **Extended Tuning** |
| 2′ | connected + Plane + `displayBasicTuning` | `ConfigArduplane` | `BasicTuning` | **Basic Tuning** |
| 3′ | connected + Plane + `displayExtendedTuning` | `ConfigArducopter` | — | **QP Extended Tuning** (literal `"QP " + Strings.ExtendedTuning`) |
| 2″ | connected + Rover | `ConfigArdurover` | `BasicTuning` | **Basic Tuning** |
| 2‴ | connected + Tracker | `ConfigAntennaTracker` | `ExtendedTuning` | **Extended Tuning** |
| 4 | connected + `displayStandardParams` | `ConfigFriendlyParams` | `StandardParams` | **Standard Params** |
| 5 | connected + `displayAdvancedParams` | `ConfigFriendlyParamsAdv` | `AdvancedParams` (advanced=true) | **Advanced Params** |
| 6 | connected + !MONO + `ConfigOSD.IsApplicable()` + `displayOSD` | `ConfigOSD` | `OnboardOSD` | **Onboard OSD** |
| 7 | connected + FTP capability + `displayMavFTP` | `MavFTPUI` | `MAVFtp` | **MAVFtp** |
| 8 | connected + `displayUserParam` | `ConfigUserDefined` | `User_Params` | **User Params** |
| 9 | `displayFullParamList` (offline OR all params received) | `ConfigRawParams` | `FullParameterList` | **Full Parameter List** |
| 10 | connected + Ateryx firmware | `ConfigFlightModes` | `FlightModes` | **Flight Modes** |
| 10a | connected + Ateryx | `ConfigAteryxSensors` | (literal) | **Ateryx Zero Sensors** |
| 10b | connected + Ateryx | `ConfigAteryx` | (literal) | **Ateryx Pids** |
| 11 | connected + params not all received | `ConfigParamLoading` | `Loading` | **Loading** |
| 12 | `displayPlannerSettings` (connected or not) | `ConfigPlanner` | `Planner` | **Planner** |
| 13 | plugin-registered pages (filtered by `pageOptions`) | `item.page` | per plugin | — |

Notes:
- `Strings.FullParameterTree` (**"Full Parameter Tree"**) exists in `Strings.resx`
  but is **not referenced by any `.cs`** in 1.3.83 — there is **no Full Parameter
  Tree page** in this version. (`ConfigRawParams` itself embeds a prefix `TreeView`
  beside its grid; that is the closest analogue.)
- `ConfigPlannerAdv` and `ConfigTradHeli`/`ConfigTradHeli4`/`ConfigFFT` are NOT
  added here directly — they are reached via other hosts (e.g. `ConfigPlannerAdv`
  is opened from elsewhere; heli pages appear when heli firmware is detected by
  the equivalent extended-tuning slot). They are specified in Part B because the
  task requires them.
- `start` = page auto-activated on open (Basic/Extended tuning, else Planner, else
  Loading). Last-viewed page is remembered (`lastpagename`).
- Per-firmware enums gating the above: `isCopter` (ArduCopter2), `isPlane`
  (ArduPlane/Ateryx), `isRover`, `isTracker`, `isHeli` (MAV_TYPE.HELICOPTER),
  `isQuadPlane` (`Q_ENABLE==1`), `gotAllParams`.

---

## PART B — Per-page specs

### Flight Modes (`ConfigFlightModes`)

- **Controls** (`ConfigFlightModes.Designer.cs`): absolute-positioned `MyUserControl`
  with a header row plus a `TableLayoutPanel tableLayoutPanel1` (5 cols × 7 rows;
  `:299-334`). Top-level controls added at `:343-356`.
  - **Header (above the table):** `Label label13` = **"Current Mode:"** (`:121`);
    `Label lbl_currentmode` = **"Manual"** databound to `currentStateBindingSource.mode`
    (`:126`); `Label label14` = **"Current PWM:"** (`:111`); `Label LBL_flightmodepwm`
    = **"0"** live PWM readout (`:116`).
  - **Grid rows 0–5 (one per flight mode):**
    - Col 0: `Label labelfm1..labelfm6` = **"Flight Mode 1"** … **"Flight Mode 6"**
    - Col 1: `ComboBox CMB_fmode1..CMB_fmode6` (DropDownList, AutoComplete), each →
      `flightmode_SelectedIndexChanged` (`:171-254`)
    - Col 2: `CheckBox CB_simple1..CB_simple6` = **"Simple Mode"**
    - Col 3: `CheckBox chk_ss1..chk_ss6` = **"Super Simple Mode"**
    - Col 4: `Label` PWM ranges = **"PWM 0 - 1230"**, **"PWM 1231 - 1360"**,
      **"PWM 1361 - 1490"**, **"PWM 1491 - 1620"**, **"PWM 1621 - 1749"**, **"PWM 1750 +"**
  - **Row 6:** `MyButton BUT_SaveModes` = **"Save Modes"** (`:256`); `LinkLabel
    linkLabel1_ss` = **"Simple and Super Simple description"** (`:336`).
- **Functionality** (`ConfigFlightModes.cs`):
  - `Activate()` branches per firmware (`:38-232`): **Plane/Ateryx** → params
    `FLTMODE1..FLTMODE6`, Simple/SS hidden; **Rover** → `MODE1..MODE6`, Simple/SS
    hidden; **Copter** → `FLTMODE1..FLTMODE6` + reads bitmask params `SIMPLE` /
    `SUPER_SIMPLE` (bit per mode) into checkboxes (`:154-176`); **PX4** →
    `COM_FLTMODE1..COM_FLTMODE6`, options from
    `ParameterMetaDataRepository.GetParameterOptionsInt("COM_FLTMODE#","PX4")`.
  - `updateDropDown()` (`:416`) fills combos from `ArduPilot.Common.getModesList(firmware)`
    (DisplayMember=Value, ValueMember=Key).
  - Live 100 ms timer (`:227-339`): updates current mode; reads `FLTMODE_CH`/`MODE_CH`
    to select active RC channel (ch5in..ch16in); sets `LBL_flightmodepwm` =
    `"<channel>: <pwm>"`; highlights active combo via `readSwitch(pwm)` (PWM→mode map `:342`).
  - `BUT_SaveModes_Click` (`:354-414`): writes `FLTMODE1..6` / `MODE1..6` /
    `COM_FLTMODE1..6` (whichever exists) via `setParam`; Copter also recomputes &
    writes `SIMPLE`/`SUPER_SIMPLE` bitmasks (enum `SimpleMode`, bits 1..32). Sets
    button text **"Complete"**; errors → `CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR)`.
  - `flightmode_SelectedIndexChanged` (`:436`): Copter-only — enables/disables the
    matching Simple/SS checkbox depending on whether the selected mode name is one of
    althold/auto/autotune/land/loiter/ofloiter/poshold/rtl/sport/stabilize/flowhold/zigzag.
  - `linkLabel1_ss` → `https://ardupilot.org/copter/docs/simpleandsuper-simple-modes.html`.
    Ctrl+S = save (`ProcessCmdKey :239`).
- **Avalonia status**: **PARTIAL** — `ConfigFlightModesViewModel`
  (`ConfigParamPages.cs:240`) is a generic param grid for FLTMODE1–6/SIMPLE; missing
  the live current-mode highlight, PWM readout, and per-mode Simple/SS checkbox grid.

---

### GeoFence (`ConfigAC_Fence`)

- **Controls** (`ConfigAC_Fence.Designer.cs`): `MyUserControl` with title + separator +
  `TableLayoutPanel tableLayoutPanel1` (2 cols: label / input; rows 0–6, `:61-78`).
  - `Label label1gftitle` = **"Geo Fence"**; `LineSeparator lineSeparator2`.
  - Row 0: `Label label3enable` = **"Enable"** + `MavlinkCheckBox mavlinkCheckBox1`
    (Text **"Enable"**, On=1/Off=0).
  - Row 1: `Label label4type` = **"Type"** + `MavlinkComboBox mavlinkComboBox1` (DropDownList).
  - Row 2: `Label label5action` = **"Action"** + `MavlinkComboBox mavlinkComboBox2`.
  - Row 3: `Label label6maxalt` = **"Max Alt"** + `MavlinkNumericUpDown mavlinkNumericUpDown1`.
  - Row 4: `Label label8minalt` = **"Min Alt"** + `MavlinkNumericUpDown mavlinkNumericUpDown4` (min −100).
  - Row 5: `Label label7maxrad` = **"Max Radius"** + `MavlinkNumericUpDown mavlinkNumericUpDown2` (min 30).
  - Row 6: `Label label2rtlalt` = **"RTL Altitude"** + `MavlinkNumericUpDown mavlinkNumericUpDown3`.
  - At ctor the four numeric labels get the distance unit appended, e.g. **"Max Alt[m]"**
    (`ConfigAC_Fence.cs:13-16`, `CurrentState.DistanceUnit`).
- **Functionality** (`ConfigAC_Fence.cs`): all in `Activate()` (`:19-49`) via
  self-binding Mavlink controls (read current value, write on change — **no Save button**):
  - `FENCE_ENABLE` (checkbox; on enable re-runs `getParamList()`),
    `FENCE_TYPE` (combo, options from `GetParameterOptionsInt`),
    `FENCE_ACTION` (combo), `FENCE_ALT_MAX` (10–1000), `FENCE_ALT_MIN` (−100..100),
    `FENCE_RADIUS` (30–65536). RTL alt: `RTL_ALT_M` if present (1–500) else `RTL_ALT`
    with cm→m scaling (`:43-48`). Scale = `CurrentState.fromDistDisplayUnit(1)`.
- **Notes**: legacy Copter single circular/alt fence (not the polygon editor).
- **Avalonia status**: **PARTIAL** — `ConfigAC_FenceViewModel` (`ConfigParamPages.cs:51`)
  is a flat FENCE_* param list; no per-row labelled layout, no polygon/inclusion zones.

---

### Basic Tuning — Copter (`ConfigSimplePids`)

- **Controls** (`ConfigSimplePids.Designer.cs:29-57`): only two designer controls —
  `TextBox TXT_info` (multiline status log; initial Text **"NOTE: using this interface
  may reset some off your custom pids."**) and an empty `Panel panel1`. The body is
  **runtime-generated**: one `RangeControl` (labelled slider + value box) per param,
  stacked vertically (`ConfigSimplePids.cs:157,167`). The slider set is **data-driven**
  by external XML `acsimplepids.xml` (loaded from `Settings.GetRunningDirectory()`,
  `:35`); each `<param>` node supplies `title`, `desc`, `name`, `value`, `min`, `max`,
  and optional `<relation>` (param + multiplier). No group boxes, no PID table.
- **Functionality** (`ConfigSimplePids.cs`):
  - `Activate()` (`:25`) disposes existing children, `LoadXML(acsimplepids.xml)`.
  - `LoadXML()` (`:46`) parses `<simple><ac><param>` → `configitem` objects → `ProcessItem`.
  - `ProcessItem()` (`:122`) skips absent params; clamps min/max to live value; then
    overrides min/max/increment from metadata (`GetParameterMetaData(..., Range/Increment)`,
    default incr `0.01f`); builds `RangeControl(name, desc, title, incr, 1, min, max, value)`.
  - `RNG_ValueChanged` (`:175`): parses invariant float, **writes immediately** via
    `setParam(name, value)` (no batch save), logs to `TXT_info`; for each `<relation>`
    also writes `setParam(relParam, value * multiplier)` to keep coupled params in sync.
- **Notes**: live-write model (every slider change pushed instantly). Parsing quirk in
  the relation branch (`:94-107` reads `inner` not `relation`).
- **Avalonia status**: **PARTIAL** — `ConfigBasicTuningViewModel` is a flat list of
  ~14 copter tuning params; no sliders, no XML-driven RangeControls, no live-write log.

---

### Extended Tuning — Copter (`ConfigArducopter`)

(Also used for Plane as **"QP Extended Tuning"** and reused for QuadPlane via `Q_*` aliases.)

- **Controls** (`ConfigArducopter.Designer.cs`): all numeric = `MavlinkNumericUpDown`,
  combos = `MavlinkComboBox`. Group boxes (exact Text) and members:
  - **Rate PID tables**, columns **P / I / IMAX / D / FLTE / FLTD / FLTT**:
    - **"Rate Roll"** (`groupBox25`, `:824-918`): RATE_RLL_P/I/IMAX/D, FLTE, FLTD, FLTT
    - **"Rate Pitch"** (`groupBox24`, `:699-822`)
    - **"Rate Yaw"** (`groupBox23`, `:574-697`)
  - **Stabilize (angle-P + ACCEL MAX):** **"Stabilize Roll (Error to Rate)"**
    (`groupBox22`, P + **"ACCEL MAX"**), **"Stabilize Pitch (Error to Rate)"**
    (`groupBox21`), **"Stabilize Yaw (Error to Rate)"** (`groupBox20`).
  - **Throttle/Alt:** **"Throttle Rate (VSpd to accel)"** (`groupBox5`, P),
    **"Throttle Accel (Accel to motor)"** (`groupBox2`, P/I/IMAX/D),
    **"Altitude Hold (Alt to climbrate)"** (`groupBox7`, P).
  - **Loiter/Pos:** **"Position XY (Dist to Speed)"** (`groupBox19`, P + **"INPUT TC"**),
    **"Velocity XY (Vel to Accel)"** (`groupBox1`, P/I/IMAX/D),
    **"WPNav (cm's)"** (`groupBox4`: **"Speed "**, **"Radius"**, **"Speed Dn"**,
    **"Loiter Speed"**, **"Speed Up"**).
  - **Filters:** **"Basic Filters"** (`groupBox3`: **"Gyro"**, **"Accel"**),
    **"Static Notch Filter"** (`groupBox8`: **"Enabled"** combo, **"Frequency"**,
    **"BandWidth"**, **"Attenuation"**), **"Harmonic Notch Filter"** (`groupBox9`:
    **"Enabled"**, **"Mode"**, **"Reference"**, **"Frequency"**, **"Attenuation"**,
    **"Bandwidth"**, **"Options"**, **"Harmonics"**), **"Filter Logs"** (`groupBox6`:
    **"Mask"** combo, **"Options"** combo).
  - **Top-level (no group):** combos `TUNE` (**"Tune"**), `TUNE_LOW` (**"Min"**),
    `TUNE_HIGH` (Max), CH6..CH10 opts (**"RC6 Opt"**…**"RC10 Opt"**); checkbox
    `CHK_lockrollpitch` = **"Lock Pitch and Roll Values"** (default checked); hidden
    `lblUnitWarning`; buttons `BUT_writePIDS` **"Write Params"**, `BUT_rerequestparams`
    **"Refresh Params"**, `BUT_refreshpart` **"Refresh Screen"**.
- **Functionality** (`ConfigArducopter.cs`):
  - Gate: enabled only if `firmware==ArduCopter2` OR `Q_ENABLE!=0` (`:26-42`).
  - Each control bound via `.setup(min,max,displayIncr,scale, string[] aliases, paramDict)`
    — **first existing alias wins** (`:49-166`). Example alias chains: RATE_RLL_P →
    `{RATE_RLL_P, ATC_RAT_RLL_P, Q_A_RAT_RLL_P}`; D scale `0.0001f`; FLTE →
    `{RATE_RLL_FILT, ATC_RAT_RLL_FILT, ATC_RAT_RLL_FLTE, Q_A_RAT_RLL_FLTE}`; FLTD/FLTT →
    `ATC_RAT_RLL_FLTD/FLTT` (+Q_A). **IMAX scaling branch** (`:80-105`): if
    `ATC_RAT_*_IMAX`/`Q_A_*` exists → `setup(0,0,1,1f)`, else legacy `RATE_*_IMAX` →
    `setup(0,0,10,1f)`. Stabilize-P → `{STB_*_P, ATC_ANG_*_P, Q_A_ANG_*_P}`; accel-max →
    `{ATC_ACCEL_R_MAX, Q_A_ACCEL_R_MAX, ATC_ACC_R_MAX, Q_A_ACC_R_MAX}`. Throttle →
    `{THR_RATE_P, VEL_Z_P, PSC_VELZ_P, Q_P_VELZ_P, ...}`, alt → `{THR_ALT_P, POS_Z_P,
    PSC_POSZ_P, ...}`, accel → `ACCEL_Z_* / PSC_ACCZ_* / Q_P_ACCZ_*`. Loiter →
    `{HLD_LAT_P, POS_XY_P, PSC_POSXY_P, ...}` / `{LOITER_LAT_P, VEL_XY_P, PSC_VELXY_P, ...}`.
    WPNav → `{WPNAV_SPEED, Q_WP_SPEED, WP_SPD, Q_WP_SPD}` etc. Filters single-alias
    (`INS_GYRO_FILTER`, `INS_ACCEL_FILTER`, `INS_NOTCH_*`, `INS_HNTCH_*`, `INS_LOG_BAT_*`).
    Combos via `setup(string[], param)`: TUNE from `GetParameterOptionsInt`; CH6..CH10 →
    `{CHx_OPT, CHx_OPTION, RCx_OPTION}`.
  - **Lock pitch/roll** (`:170-180`, `:260-351`): when checked, editing any
    `RATE_/STB_/ACRO_` `_RLL_` mirrors into `_PIT_` (and vice-versa); also auto-pairs
    `NAV_LAT_↔NAV_LON_` and `LOITER_LAT_↔LOITER_LON_`. Auto-unchecked if RLL≠PIT or
    `H_SWASH_TYPE` present (heli).
  - Edits → `EEPROM_View_float_TextChanged` stores to `changes` hashtable, BackColor
    Green (ok) / Red (error); nothing written until save.
  - `BUT_writePIDS_Click` (`:353-411`): iterate `changes`; if new > 2× old → confirm
    **"<param> has more than doubled the last input. Are you sure?"** (title **"Large
    Value"**) else `setParam`, reset BackColor `#434445`. Ctrl+S = save. `BUT_rerequestparams`
    → `getParamList()`+`Activate()`; `BUT_refreshpart` → per-control `GetParam`+`Activate()`.
  - `OnEnter_NumUpDown` (`:484`): Copter ≥4.7 shows red `lblUnitWarning` via
    `ParamChanges47.changedByNewParamWarning(...)` for renamed/rescaled params. Tooltips
    set to `"<ParamName>:\n<Description>"`.
- **Avalonia status**: **PARTIAL** — `ConfigExtendedTuningViewModel` is a large flat
  param list incl. notch filters; no PID matrix layout, no CH6 in-flight-tuning combos,
  no lock-roll/pitch mirroring, no >2× guard, no 4.7 warning.

---

### Basic Tuning — Plane (`ConfigArduplane`)

- **Controls** (`ConfigArduplane.Designer.cs`): `MavlinkNumericUpDown` boxes in group boxes:
  - **"Throttle 0-100%"** (`groupBox3`, `:195-263`): **"SlewRate"**, **"Max"**, **"Min"**, **"Cruise"**
  - **"Airspeed m/s"** (`groupBox1`): **"Ratio"** (max 2.5, incr 0.005), **"Max"**, **"Min"**, **"Cruise"**
  - **"Navigation Angles"** (`groupBox2`): **"Pitch Min"**, **"Pitch Max"**, **"Bank Max"**
  - **"Other Mix's"** (`groupBox16`): **"P to T"**, **"Rudder Mix"**
  - **"Energy/Alt Pid"** (`groupBox14`): **"P"**, **"I"**, **"D"**, **"INT_MAX"** (max 100)
  - **"Nav Pitch Alt Pid"** (`groupBox13`): P/I/D/INT_MAX
  - **"Nav Pitch AS Pid"** (`groupBox12`): P/I/D/INT_MAX
  - **"Servo Yaw"** (`groupBox10`): **"Yaw 2 roll"**, **"Integral"**, **"Dampening"**, **"Intergrator Max"** (sic)
  - **"Servo Pitch Pid"** (`groupBox9`), **"Servo Roll Pid"** (`groupBox8`): P/I/D/INT_MAX
  - **"L1 Control - Turn Control"** (`groupBox4`): **"Period"**, **"Damping"**
  - **"TECS"** (`groupBox5`): **"Climb Max (m/s)"**, **"Sink Min (m/s)"**, **"Pitch Dampening"**,
    **"Time Const"**, **"Sink Max (m/s)"**
  - Buttons: `BUT_writePIDS` **"Write Params"**, `BUT_rerequestparams` **"Refresh Params"**,
    `BUT_refreshpart` **"Refresh Screen"**. No combos/checkboxes.
- **Functionality** (`ConfigArduplane.cs`): gate `firmware==ArduPlane` (`:26-41`). Params
  (`:45-99`), mostly literal; multi-alias: `ARSPD_FBW_MAX`→`{AIRSPEED_MAX, ARSPD_FBW_MAX}`,
  `ARSPD_FBW_MIN`→`{AIRSPEED_MIN, ARSPD_FBW_MIN}`, `TRIM_ARSPD_CM`→`AIRSPEED_CRUISE`,
  `LIM_PITCH_MIN`→`PTCH_LIM_MIN_DEG`, `LIM_PITCH_MAX`→`PTCH_LIM_MAX_DEG`,
  `LIM_ROLL_CD`→`ROLL_LIMIT_DEG`, `KFF_PTCH2THR`→`{KFF_THR2PTCH, KFF_PTCH2THR}`,
  `PTCH2SRV_*`→`{PTCH2SRV_*, PTCH_RATE_*}`, `RLL2SRV_*`→`{RLL2SRV_*, RLL_RATE_*}`. Energy/
  Alt/AS/Yaw/L1/TECS use literals (`ENRGY2THR_*`, `ALT2PTCH_*`, `ARSP2PTCH_*`, `YAW2SRV_*`,
  `NAVL1_*`, `TECS_*`). Edits→`EEPROM_View_float_TextChanged` (`:177-201`) Green/Red.
  `BUT_writePIDS_Click` (`:203-261`): same >2× **"has more than doubled… Are you sure?"**
  guard, `setParam`, reset `#434445`. Ctrl+S=save. No lock/pairing logic.
- **Avalonia status**: **MISSING** — no plane tuning VM.

---

### Basic Tuning — Rover (`ConfigArdurover`)

- **Controls** (`ConfigArdurover.Designer.cs`): `MavlinkNumericUpDown` + `MavlinkComboBox`:
  - **"Throttle and Motors"** (`groupBox3`, `:138-214`): `MOT_PWM_TYPE` combo
    (**"Motor Type"**), **"Throttle Max (%)"**, **"Throttle Min (%)"**
  - **"Speed/Throttle"** (`groupBox14`): **"Cruise Speed"**, **"Cruise Throttle"**,
    `ATC_BRAKE` combo (**"Brake"**), **"Accel Max (m/s/s)"**, P/I/D/**"IMAX"**
  - **"Navigation"** (`groupBox4`): **"WP Speed"**, **"Lat Acc Cntl Damp"**,
    **"Lat Acc Cntl Period"**, **"WP Overshoot"**, **"WP Radius"**, **"Turn G Max"**
  - **"Steering Rate"** (`groupBox5`): P/I/D/**"IMAX"**, **"FF"** (max 100)
  - **"Avoidance"** (`groupBox1`): **"Trigger Dist (cm)"**, **"Turn Angle"**,
    **"Turn Time"**, **"Sonar Debounce"**
  - **"Steering Mode"** (`groupBox2`): **"Turn Radius"**
  - Top-level combos: **"RC7 Opt"**…**"RC10 Opt"**
  - Buttons: **"Write Params"**, **"Refresh Params"**, **"Refresh Screen"**
- **Functionality** (`ConfigArdurover.cs`): gate `firmware==ArduRover` (`:26-41`). Params
  (`:45-87`): CH7..CH10→`{CHx_OPTION, RCx_OPTION}`; steering rate→`{STEER2SRV_*,
  ATC_STR_RAT_*}`; speed/throttle→`{SPEED2THR_*, ATC_SPEED_*}`; THR_MIN/MAX→`{THR_*,
  MOT_THR_*}`; TURN_G_MAX→`{TURN_MAX_G, ATC_TURN_MAX_G}`; sonar→`{SONAR_*, RNGFND_*}`.
  **Avoidance group hidden** if both `SONAR_TRIGGER_CM` and `RNGFND_TRIGGR_CM` absent
  (`:79-82`). `BUT_writePIDS_Click` (`:155-195`): same >2× guard, `setParam`, reset
  `#434445`. Note: this designer wires NO `numeric_ValueUpdated` → handler on the
  NumericUpDowns (only combos/buttons), so numeric change-tracking entry differs from
  Copter/Plane.
- **Avalonia status**: **MISSING** — no rover tuning VM.

---

### Standard Params (`ConfigFriendlyParams`)

- **Controls** (`ConfigFriendlyParams.Designer.cs:29-82`): single vertical
  `FlowLayoutPanel flowLayoutPanel1` of runtime-generated per-param editors —
  **NOT a grid**. Three toolbar `MyButton`s: `BUT_Find` = **"Find"**,
  `BUT_rerequestparams` = **"Refresh Params"**, `BUT_writePIDS` = **"Write Params"**
  (Ctrl+S, `:159-168`). Per-param editor is one of: `RangeControl` (Range+Increment
  metadata; `NumericUpDownControl`, out-of-range cell painted `Color.Orange` `:439`),
  `MavlinkCheckBoxBitMask` (bitmask), `ValuesControl` (enumerated; `ComboBoxControl`,
  DisplayMember "Value"/ValueMember "Key").
- **Functionality** (`ConfigFriendlyParams.cs`):
  - Population: `Activate()` (`:253`) → `FilterParamList()` (`:269`) walks
    `MainV2.comPort.MAV.param.Keys`, querying `ParameterMetaDataRepository.GetParameterMetaData`
    for `DisplayName` + `User`. Shown only if it has a friendly `DisplayName` AND its
    `User` category == `ParameterMode` (here `Standard`, set in ctor `:156`). List is
    **metadata-driven**, not the raw param list.
  - `AddControl` (`:341`): reads Description/Units/Range/Increment/Values/bitmask. Units
    `"centi-degrees"` rescaled to **"Degrees (Scaled)"** (×100). Display name =
    `"<friendly> (<PARAM_NAME>)"`.
  - Fav: `BindParamList` reads `Settings.Instance` list `"fav_params"` and floats
    favourites to top (`:316,325-330`); no per-row fav toggle on this page.
  - Write `BUT_writePIDS_Click` (`:179`): iterate `_params_changed`, `SortENABLE()`,
    `setParam(... InvariantCulture)`; success → **"Parameters successfully saved."**/**"Saved"**.
  - Refresh (`:212`): confirm `Strings.WarningUpdateParamList`, `getParamList()`, `Activate()`.
  - Find/filter (`:21`): `InputBox.Show("Search For", "Enter a single word to search for")`,
    500 ms debounce `_filterTimer`, toggles control `.Visible` (min 2 chars).
  - No Load/Save/Compare. Modified tracking via `Control_ValueChanged`→`_params_changed`.
- **Avalonia status**: **DONE (mostly)** — `ConfigFriendlyParamsViewModel` filters by
  metadata User=Standard/Advanced and rebuilds on refresh; verify Find debounce, fav
  ordering, and bitmask/values control parity.

---

### Advanced Params (`ConfigFriendlyParamsAdv`)

- **Controls**: none of its own. 12-line subclass of `ConfigFriendlyParams`
  (`ConfigFriendlyParamsAdv.cs`, no Designer) — reuses the same flow panel + Find/
  Refresh/Write toolbar and the RangeControl/ValuesControl/MavlinkCheckBoxBitMask editors.
- **Functionality**: identical to `ConfigFriendlyParams`; ctor sets
  `ParameterMode = Advanced` (`:9`). `FilterParamList` predicate (`ConfigFriendlyParams.cs:284-289`):
  in Advanced mode show param if `User`==`"Advanced"` **OR empty** (uncategorised params
  fall through to Advanced). All Write/Refresh/Find/Fav inherited.
- **Avalonia status**: **DONE (mostly)** — same `ConfigFriendlyParamsViewModel` in
  Advanced mode (see above caveats).

---

### Full Parameter List (`ConfigRawParams`)

- **Controls** (`ConfigRawParams.Designer.cs`): top-level `SplitContainer splitContainer1`
  (Panel1 fixed/collapsible).
  - **Panel1:** `TreeView treeView1` — param-prefix category tree (`AfterSelect`→filter).
  - **Panel2:** the `Params` grid + `but_collapse` (**"<"** / **">"**) + `tableLayoutPanel1` toolbar.
  - **`Params` = `MyDataGridView`** (no add/delete rows, no row headers; header style
    maroon bg/white text). Columns in order (`:224-309`):
    1. `Command` — HeaderText **"Name"**, ReadOnly, FillWeight 20
    2. `Value` — **"Value"**, editable, FillWeight 11
    3. `Default_value` — **"Default"**, ReadOnly, FillWeight 11 (visible only if firmware reports defaults)
    4. `Units` — **"Units"**, ReadOnly, FillWeight 9
    5. `Options` — **"Options"**, ReadOnly, FillWeight 28 (overlaid live combo/numeric/bitmask-button per current row)
    6. `Desc` — **"Desc"**, ReadOnly, Fill/WrapMode, FillWeight 25 (click opens URL)
    7. `Fav` — `DataGridViewCheckBoxColumn` **"Fav"**, FillWeight 4
  - **Toolbar `tableLayoutPanel1`** (`:322-337`), `MyButton` unless noted:
    `BUT_load` **"Load from file"**, `BUT_save` **"Save to file"**, `BUT_writePIDS`
    **"Write Params"**, `BUT_rerequestparams` **"Refresh Params"**, `BUT_compare`
    **"Compare Params"**, `BUT_commitToFlash` **"Commit Params"** (if
    `displayParamCommitButton`), `label1` **"All Units are in raw \nformat with no
    scaling"**, `CMB_paramfiles` (combo of presaved GitHub frame param files),
    `BUT_paramfileload` **"Load Presaved"**, `BUT_reset_params` **"Reset to Default"**,
    `label2` **"Search"**, `txt_search` (TextBox), `chk_modified` CheckBox **"Modified"**,
    `chk_none_default` CheckBox **"None Default"** (if defaults present), `BUT_refreshTable`
    **"Refresh Table"** (if `SlowMachine`).
- **Functionality** (`ConfigRawParams.cs`):
  - Population `processToScreen` (`:563`): on startup enumerate `MAV.param.Keys`, one row
    (Height 36) each; cells Command/Value/Fav(`fav_params`)/Default; metadata
    (`GetParameterMetaData`) → Units cell, Options cell (`range + "\n" + values`), Desc
    cell + tooltips. Then `Params.Sort(Command, Asc)` via `OnParamsOnSortCompare`
    (natural sort + favourites first).
  - Tree `BuildTree` (`:688`): prefix tree split on `_`, root **"All"**; selection sets
    `filterPrefix`. Split distance/collapsed persisted.
  - Per-row Options editor `Params_RowEnter` (`:1153`): overlays `MyButton` **"Set
    Bitmask"** (→`MavlinkCheckBoxBitMask`) / `ComboBox` (enumerated options) /
    `NumericUpDown` (Range/Increment bounds).
  - Edit/validation `Params_CellValueChanged` (`:454`): Value column only; evaluates as
    math expression (`mxparser`); `_REV` 0→−1; honours ReadOnly metadata; range →
    out-of-range Yes/No confirm; valid → cell Green + `_changes[name]=val`, invalid → Red.
  - Write `BUT_writePIDS_Click` (`:257`): `SortENABLE()`; ≤20 changes → confirm dialog
    listing `name: prev -> new`, >20 → count-only; `setParam` each; reboot-required via
    `GetParameterRebootRequired`; clear Green/remove from `_changes`. Ctrl+S = write.
  - Refresh (`:421`): confirm, `getParamList()`, rebuild. Refresh Table (`:1119`): rebuild
    without re-fetch (SlowMachine).
  - Compare (`:396`): OpenFileDialog → `ParamFile.loadParamFile` → `ParamCompare` form
    diffing current vs file.
  - Load .param (`:127`→`loadparamsfromfile :149`): match names→rows, update Value;
    blacklist `SYSID_SW_MREV, WP_TOTAL, CMD_TOTAL, FENCE_TOTAL, SYS_NUM_RESETS,
    ARSPD_OFFSET, GND_ABS_PRESS, GND_TEMP, CMD_INDEX, LOG_LASTFILE, FORMAT_VERSION`.
  - Save .param (`:224`): SaveFileDialog (`Param List|*.param;*.parm`)→`ParamFile.SaveParamFile`.
  - Load Presaved (`:860,946`): fetches ArduPilot GitHub `/Tools/Frame_params/`
    (`QuadPlanes/` if `Q_ENABLE>=1`) → `ParamCompare`.
  - Reset to Default (`:980`): confirm → `setParam(["FORMAT_VERSION","SYSID_SW_MREV"],0)`,
    reboot, close. Commit Params (`:1093`): `MAV_CMD.PREFLIGHT_STORAGE` param1=1.
  - Search/filter (`txt_search`→`_filterTimer` 500 ms→`filterList :889`): regex (`*`→`.*`,
    IgnoreCase, min 2 chars), combined with tree `filterPrefix`; `chk_modified` shows only
    `_changes`; `chk_none_default` shows Value≠Default.
  - Fav `Params_CellContentClick` (`:1027`): toggling appends/removes name in `fav_params`,
    re-sorts (favs pinned). Desc click → `CheckForUrlAndLaunchInBrowser` (`:1066`).
  - Column widths persisted as `rawparam_<col>_width`. `static rowlist` cached across
    activations; natural+fav sort via `NaturalStringComparer` (`:746`).
- **Avalonia status**: **MISSING** — no raw/full editable param table with tree +
  search/compare/load/save/presaved/commit. Only metadata-driven friendly pages exist.

---

### Planner (`ConfigPlanner`)

- **Controls** (`ConfigPlanner.Designer.cs:894-991`): large flat absolute-positioned
  `MyUserControl` (no GroupBox/TabControl — section headings are plain Labels). ~95
  controls. Logical groups (exact Text in quotes):
  - **Layout/units:** **"Layout"** + `CMB_Layout` (Basic/Advanced/Custom); **"Dist Units"**,
    **"Speed Units"**, **"Alt Units"** combos; note **"NOTE: The Configuration Tab will NOT
    display these units, as those are raw values."**
  - **Appearance:** **"Theme"** + `CMB_theme` + `BUT_themecustom` **"Custom"**; **"UI
    Language"** + `CMB_language`; **"OSD Color"** + `CMB_osdcolor`; **"HUD"** +
    `CHK_hudshow` **"Enable HUD Overlay"**.
  - **Speech** (**"Speech"**): `CHK_enablespeech` **"Enable Speech"**, `CHK_speechArmedOnly`
    **"Only when Armed"**, `CHK_speechwaypoint` **"Waypoint"**, `CHK_speechmode` **"Mode "**,
    `CHK_speechcustom` **"30s Interval"**, `CHK_speechbattery` **"Battery Warning"**,
    `CHK_speechaltwarning` **"Alt Warning"**, `CHK_speecharmdisarm` **"Arm/Disarm"**,
    `CHK_speechlowspeed` **"Low Speed"**; severity `CMB_severity`
    (Emergency/Alert/Critical/Error/Warning/Notice/Info/Debug) + note **"NOTE: Set the
    low level of SEVERITY to speak"**.
  - **Telemetry Rates** (**"Telemetry Rates"**): **"Attitude"**, **"Position"**,
    **"Mode/Status"**, **"RC"**, **"Sensor"** combos (0..50).
  - **Video:** **"Video Device"** + `CMB_videosources`, **"Video Format"** +
    `CMB_videoresolutions`, `BUT_videostart` **"Start"**, `BUT_videostop` **"Stop"**.
  - **Joystick** (**"Joystick"**): `BUT_Joystick` **"Joystick Setup"**.
  - **Map/icon:** **"Map Follow"** + `CHK_maprotation` **"Map is rotated to follow the
    plane"**; **"Dist to Home"** + `CHK_disttohomeflightdata` **"Display in Flightdata"**;
    `chk_shownofly` **"No Fly"**, `CHK_showairports` **"Show Airports"**, `chk_ADSB` **"ADSB"**,
    `chk_tfr` **"TFR's"**; **"Map Access Mode"** + `CMB_mapCache` + `BUT_mapCacheDir`
    **"Open Map Cache"**. **Aircraft Icon** (**"Aircraft Icon"**): `chk_displaycog`
    **"Display COG"**, `chk_displayheading` **"Display Heading"**, `chk_displaynavbearing`
    **"Display Nav Bearing"**, `chk_displayradius` **"Display Turn Radius"**,
    `chk_displaytarget` **"Display Target"**, `chk_displaytooltip` **"Display ToolTip"**,
    **"Line Length"** + `num_linelength` (10–2000), **"Inactive Aircraft"** +
    `cmb_secondarydisplaystyle`.
  - **Waypoints/tracks:** **"Waypoints"** + `CHK_loadwponconnect` **"Load Waypoints on
    connect?"**; **"Track Length"** + `NUM_tracklength` (100–200000).
  - **Misc/advanced:** `CHK_GDIPlus` **"GDI+ (old type/no HW acceleration)"**, `CHK_beta`
    **"Beta Updates"**, `CHK_Password` **"Password Protect Config"**, `chk_analytics`
    **"OptOut Anon Stats"**, `CHK_AutoParamCommit` **"Auto Commit Params"**, `CHK_params_bg`
    **"Params Download in BackGround"**, `chk_slowMachine` **"Runing on a slow computer"**,
    `chk_norcreceiver` **"No RC Receiver"**, `CHK_mavdebug` **"Mavlink Message Debug"**,
    **"Connect Reset"** + `CHK_resetapmonconnect` **"Reset on USB Connect (toggle DTR)"** +
    `CHK_rtsresetesp32` **"Disable RTS reset on ESP32 SerialUSB"**, **"GCS ID"** +
    `num_gcsid` (1–255), **"Log Path"** + `txt_log_dir` + `BUT_logdirbrowse` **"Browse"**,
    `BUT_Vario` **"Start/Stop Vario"**, `chk_temp` **"Testing Screen"**.
- **Functionality** (`ConfigPlanner.cs`): almost everything reads/writes
  `Settings.Instance[...]` (app config), NOT vehicle params. `startup` guards population.
  - `Activate()` (`:55-256`): selects layout from `MainV2.DisplayConfiguration`; fills
    osdcolor/units/theme/language(`en-US,zh-Hans,zh-TW,ru-RU,Fr,Pl,it-IT,es-ES,de-DE,
    ja-JP,id-ID,ko-KR,ar,pt,tr,ru-KZ,uk`)/mapCache combos; `num_gcsid`=`MAVLinkInterface.gcssysid`;
    loads checkboxes via `SetCheckboxFromConfig(key, chk)` (`:767`); loads rate combos
    from `MAV.cs.rate*`; aircraft-icon from `GMapMarkerBase_*` keys; `LogDir`.
  - Telemetry rate combos (`:573-640`): write setting + `MAV.cs.rate*` + backup +
    `requestDatastream(...)` (EXTRA1/2 attitude, POSITION, EXTENDED_STATUS, RC_CHANNELS,
    EXTRA3/RAW_SENSORS).
  - Speech checkboxes (`:442-916`): on enable prompt via `InputBox.Show` for spoken phrase
    (and battery volt/percent, alt height, speed triggers) → `Settings.Instance` keys.
  - Language change (`:416`): `changelanguage(...)`, **"Please Restart the Planner"**, close
    app. Units → settings + `ChangeUnits()`. ADSB (`:924`): prompt server (default
    `https://api.adsb.lol/`) + port. `BUT_Joystick`→`JoystickSetup`. `CHK_Password` prompts.
- **Avalonia status**: **PARTIAL** — `ConfigPlannerViewModel` has the option lists/toggles
  as `[ObservableProperty]` but no Settings persistence wiring shown; video/joystick/vario
  Windows-specific actions absent.

---

### Planner Advanced (`ConfigPlannerAdv`)

- **Controls** (`ConfigPlannerAdv.Designer.cs`, hardcoded strings): `Label label1` =
  **"NOTE: You can break the planner using this screen"** (`:91`); `MyDataGridView Params`
  (read-only, no add/delete, no row headers; maroon/white header), columns
  `Name1` HeaderText **"Name"** (ReadOnly, w150) + `Value` HeaderText **"Value"** (ReadOnly, w300).
- **Functionality** (`ConfigPlannerAdv.cs`): `Activate()` (`:15-28`) clears rows, iterates
  every `Settings.Instance.Keys` adding name/value rows, sorts ascending by Name. Pure
  read-only dump of all app settings — no save/edit, no MAVLink.
- **Avalonia status**: **MISSING** — no advanced-planner settings VM.

---

### Traditional Heli Setup (`ConfigTradHeli`)

`MyUserControl, IActivate, IDeactivate`; 100 ms WinForms `Timer` for live updates
(`ConfigTradHeli.cs:15,28-32`).

- **Controls** (`ConfigTradHeli.Designer.cs:34-143`; Text from `.resx`):
  - **"Swash Type"** (`groupBox5`): RadioButtons `CCPM` **"CCPM"** + `H_SWASH_TYPE` **"H1"**;
    `MavlinkCheckBox fbl_modeFBL` **"Flybarless"**; `label41` **"Bottom"**.
  - **"Tail Type"** (`groupBox3`): `MavlinkComboBox` (`H_TAIL_TYPE`), `H_TAIL_SPEED` (**"Tail
    Speed"**), `H_GYR_GAIN` (**"Gain"**).
  - Collective range (`groupBox1`): `H_COL_MIN`/`H_COL_MID`/`H_COL_MAX`; labels **"Zero"**,
    **"Top"**, **"Collective Travel"**.
  - **"Servo"** (`groupBox2`, `:52-660`) — swashplate servo table: `H_SV1_POS/H_SV2_POS/
    H_SV3_POS` (range −180..180), `HS1_REV..HS4_REV` (checkboxes), `HS1_TRIM..HS4_TRIM`,
    `HS4_MIN/HS4_MAX`. Column labels **"Servo"**, **"Position"**, **"Rev"**, **"Trim"**,
    **"Min"**, **"Max"**, **"Swash-Servo position"**, **"1"/"2"/"3"**, **"Rudder Travel"**.
  - **"Collective Pre-Comp"** (`groupBox4`): `H_COLYAW`.
  - **"RSC Ramp Rate"** (`groupBox7`): `H_RSC_RATE` (**"Rate(Sec)"**), runuptime (**"Runup(Sec)"**),
    `H_RSC_CRITICAL` (**"Critical"**).
  - **"Mode 1 Setpoint"** (`groupBox8`): `H_RSC_SETPOINT`.
  - **"Rotor Speed Control"** (`groupBox9`): `H_RSC_MODE` combo.
  - Cyclic/misc (`groupBox10`): `h_cyc_max` (**"Cyclic Travel"**), `atc_piro_comp`
    (**"Piro Comp"**), `atc_hovr_rol_trm` (**"Hover Roll"**), `h_sv_test` (**"Bootup Servo
    Test"**), `H_PHANG` (**"Phase Angle"**), `land_col_min` (**"Landing Collective"**).
  - **"Throttle Output"** (`groupBox11`): max/min/rev (**"Max"/"Min"/"Rev"**).
  - **"V-Curve"** (`groupBox12`): high/low/idle pwr (**"High Pwr"/"Low Pwr"/"Idle"**).
  - **"Curve"** (`groupBox13`): `im_stab_col_1..4` (**"Stab 1..4"**), `im_acro_col_exp`
    (**"Acro Expo"**).
  - Manual servo test (`groupBox6`): six `MyButton` **"Active"/"Manual"/"Max"/"Zero"/"Min"/
    "Test"**; `label2` **"Servo Mode"**.
  - `HorizontalProgressBar2 HS3, HS4` (live RC in/out bars); `Gservoloc` (graphical swash
    servo display); `ZedGraph.ZedGraphControl zedGraphControl1` (Collective Control curve).
- **Functionality** (`ConfigTradHeli.cs`):
  - `Activate()` (`:24`): starts timer; ZedGraph pane Title **"Collective Control"**, X
    **"Collective Input (%)"** 0–100, Y **"Collective Output"** 0–1000; `.setup(...)` binds
    every control (`:52-99`). Params: `H_PHANG, ATC_PIRO_COMP, H_SV_TEST, ATC_HOVR_ROL_TRM,
    H_CYC_MAX, H_RSC_CRITICAL, H_RSC_MAX/H_RSC_PWM_MAX, H_RSC_MIN/H_RSC_PWM_MIN,
    H_RSC_REV/H_RSC_PWM_REV, H_RSC_POWER_HIGH/LOW, H_RSC_IDLE, IM_STAB_COL_1..4 (alias
    IM_STB_COL_*), IM_ACRO_COL_EXP, H_TAIL_TYPE, H_TAIL_SPEED, H_LAND_COL_MIN, H_COLYAW,
    H_RSC_RAMP_TIME, H_RSC_RUNUP_TIME, H_RSC_MODE, H_RSC_SETPOINT, H_GYR_GAIN, H_COL_MIN/
    MID/MAX, HS4_MIN/MAX (alias SERVO4_MIN/MAX), H_SV1_POS/H_SV2_POS/H_SV3_POS, H_SWASH_TYPE`.
    Rev/trim alias sets chosen by firmware: `HSn_REV / H_SVn_REV / SERVOn_REVERSED`,
    `HSn_TRIM / H_SVn_TRIM / SERVOn_TRIM` (`:145-172`). `H_FLYBAR_MODE` via fbl checkbox.
  - `GenerateGraphData()` (`:180`): "Stabalize Collective" curve from `IM_STAB_COL_1..4` at
    X={0,40,60,100}; 100-pt "Acro Collective" expo curve from `IM_ACRO_COL_EXP`
    (`col_out=expo*in³+(1-expo)*in`); red dashed cursor at current collective from
    `cs.ch6out` via `H_COL_MIN/MAX` (`map()` `:251`).
  - `timer_Tick` (`:445`): updates graph; when `H_SV_MAN!=0` updates `HS3/HS4` from
    `cs.ch3in/ch4in`. Servo manual-pos validators (`:348-421`): set `H_SV_MAN=1`, write pos,
    Sleep(100), `H_SV_MAN=0`; update `Gservoloc`. Six button handlers set `H_SV_MAN` 0..5.
    `H_SWASH_TYPE_CheckedChanged` (`:263`) writes 0=CCPM/1=H1. `Deactivate()` stops timer.
- **Avalonia status**: **MISSING** — no heli swashplate/servo/collective/RSC page or
  ZedGraph collective curve.

---

### Traditional Heli Setup gen-2 (`ConfigTradHeli4`)

`UserControl, IActivate, IDeactivate`. Static designer frame; param rows generated at runtime.

- **Controls** (`ConfigTradHeli4.Designer.cs`):
  - **"Servo Setup"** (`groupBoxservo` → `tableLayoutPanel4`): static 8-servo grid. Column
    labels **"Servo"**, **"Function"**, **"Min"**, **"Max"**, **"Trim"**, **"Reversed"** +
    row numbers **"1".."8"**. Per servo: `MavlinkCheckBox revN`, `MavlinkComboBox funcN`,
    `MavlinkNumericUpDown minN/trimN/maxN`.
  - **"Swashplate Setup"** (`groupBoxswash` → `tableLayoutPanel5`, dynamic).
  - **"Throttle Settings"** (`groupBoxthrot` → `tableLayoutPanel3`, dynamic).
  - **"Governor Settings"** (`groupBoxgover` → `tableLayoutPanel2`, dynamic).
  - **"Misc Settings"** (`groupBoxmisc` → `tableLayoutPanel1`, dynamic).
  - No graph, no live timer, no manual-servo-test buttons.
- **Functionality** (`ConfigTradHeli4.cs`):
  - `Activate()` (`:28`): `setup(...)` servos 1–8 (`:32-47`), then `populatetable` lambda
    (`:51-85`) builds each dynamic panel from `ItemInfo` lists — per item adds Label +
    `MavlinkComboBox` (Combo) or `MavlinkNumericUpDown` (Num, range from metadata since
    min=0/max=0), tooltip = Description.
  - `setup(...)` (`:174`): `SERVO{n}_REVERSED/_FUNCTION (combo)/_MIN/_TRIM/_MAX`.
  - Param lists: **Swash** `H_SV_MAN, H_SW_TYPE, H_SW_COL_DIR, H_SW_LIN_SVO, H_FLYBAR_MODE,
    H_CYC_MAX, H_COL_MAX/MID/MIN, H_COL_ANG_MIN/MAX, H_COL_ZERO_THRST, H_COL_LAND_MIN`;
    **Throttle** `H_RSC_MODE, H_RSC_CRITICAL, H_RSC_RAMP_TIME, H_RSC_RUNUP_TIME,
    H_RSC_CLDWN_TIME, H_RSC_SETPOINT, H_RSC_IDLE, H_RSC_THRCRV_0/25/50/75/100`;
    **Governor** `H_RSC_GOV_COMP/SETPNT/DISGAG/DROOP/FF/TCGAIN/RANGE/RPM/TORQUE`;
    **Misc** `IM_STB_COL_1..4, H_TAIL_TYPE, H_TAIL_SPEED, H_GYR_GAIN, H_GYR_GAIN_ACRO,
    H_COLYAW`. `Deactivate()` clears all 4 panels (rebuilt each activate).
- **Avalonia status**: **MISSING** — no gen-2 heli metadata param-grid page.

---

### FFT (`ConfigFFT`)

`MyUserControl, IActivate, IDeactivate`. **No Designer.cs** — `InitializeComponent()` is
hand-written in `ConfigFFT.cs:72-160`.

- **Controls** (code-built):
  - **"FFT Setup"** (`groupBox1`, `:128`): `RangeControl INS_LOG_BAT_CNT` (slider, range
    32..4096, bound to `INS_LOG_BAT_CNT`); `MavlinkCheckBoxBitMask INS_LOG_BAT_MASK` (IMU
    log mask).
  - `MyButton but_fft` **"FFT"** (`:76,108`).
  - **"Please ensure IMU_RAW and IMU_FAST are turned off to use FFT"** (`groupBox2`, `:139`):
    `MavlinkCheckBoxBitMask LOG_BITMASK`.
  - Size 710×455; **no chart on this page**.
- **Functionality** (`ConfigFFT.cs`): ctor (`:26`) disables page if `INS_LOG_BAT_CNT` absent;
  sets up RangeControl (min 32, max 4096) → `RangeControl1OnValueChanged` (`:53`) does
  `setParam`. `INS_LOG_BAT_MASK.setup("INS_LOG_BAT_MASK")`, `LOG_BITMASK.setup("LOG_BITMASK")`.
  Params: **`INS_LOG_BAT_CNT, INS_LOG_BAT_MASK, LOG_BITMASK`** only. `but_fft_Click` (`:162`):
  `new fftui().Show()` — a **separate window** does the actual FFT plotting (from batch-logged
  IMU dataflash data, not live telemetry). This page is only FFT logging *setup*.
- **Avalonia status**: **PARTIAL** — `ConfigFFTViewModel` (`ConfigParamPages.cs:194`) is a
  param grid for INS_LOG_BAT_CNT/MASK/LOG_BITMASK; no "FFT" button / fftui plot window.

---

### User Params (`ConfigUserDefined`)

- **Controls** (`ConfigUserDefined.Designer.cs`): `TableLayoutPanel tableLayoutPanel1`
  (2 cols, Dock=Fill, grown dynamically). Row 0: `MyButton` **"Modify"** spanning both
  columns. Per configured present param: `Label` (col 0) + `MavlinkComboBox` (if enumerated
  options) or `MavlinkNumericUpDown` (if numeric range) (col 1). No grid, no fixed toolbar.
- **Functionality** (`ConfigUserDefined.cs`):
  - `Activate()`→`LoadOptions()` (`:46`): for each name in `Options`, skip unless
    `MAV.param.ContainsKey`; if `GetParameterOptionsInt` returns options → `MavlinkComboBox`,
    else `MavlinkNumericUpDown` from `GetParameterRange`. Controls auto-write on change.
  - `Options` default (`:19-44`): `CH6_OPT`…`CH16_OPT` + `RC6_OPTION`…`RC16_OPTION`,
    overridable from `Settings.Instance["UserParams"]`.
  - **Modify** button (`:53-60`): `InputBox.Show("Params","Enter Param Names")`, re-split,
    persist to `Settings.Instance["UserParams"]`, re-`Activate()`.
  - No Write/Save/Refresh/Compare button; no fav/units/highlighting.
- **Notes**: upstream bug — the numeric-only branch (`:73-77`) never `Controls.Add`s the
  created NumericUpDown, so numeric-only user params show a label with no editor.
- **Avalonia status**: **PARTIAL** — `ConfigUserDefinedViewModel` (`ConfigParamPages.cs:254`)
  is a Title/Intro stub; no Modify/add-from-list mechanism, no label+editor rows.

---

## Avalonia port status summary

Shared infra in `src/MissionPlannerAvalonia/.../ConfigurationView/`: `ParamPageBase.cs`
(Title/Intro + `Fields` of `ParamField`, `Refresh`→`getParamList`) and `ParamField.cs`
(single metadata-driven param widget, auto `setParam`). Most config pages are thin
`ParamPageBase` subclasses listing param names — generic grids, no bespoke graphs/sliders/
live RC bars/servo-test/swash widgets yet.

| MP page | Avalonia VM | Status | Gap |
|---|---|---|---|
| ConfigFlightModes | `ConfigFlightModesViewModel` (ConfigParamPages.cs:240) | PARTIAL | no live mode highlight/PWM readout, no Simple/SS grid |
| ConfigAC_Fence (GeoFence) | `ConfigAC_FenceViewModel` (ConfigParamPages.cs:51) | PARTIAL | flat FENCE_* list, no labelled layout |
| ConfigSimplePids (Basic Tuning Copter) | `ConfigBasicTuningViewModel` | PARTIAL | flat list, no XML-driven sliders/live-write |
| ConfigArducopter (Extended Tuning) | `ConfigExtendedTuningViewModel` | PARTIAL | flat list, no PID matrix/CH6 tuning/lock/>2× guard |
| ConfigArduplane (Basic Tuning Plane) | — | MISSING | none |
| ConfigArdurover (Basic Tuning Rover) | — | MISSING | none |
| ConfigFriendlyParams (Standard) | `ConfigFriendlyParamsViewModel` | DONE* | verify Find debounce/fav/bitmask parity |
| ConfigFriendlyParamsAdv (Advanced) | `ConfigFriendlyParamsViewModel` (adv mode) | DONE* | same |
| ConfigRawParams (Full Param List) | — | MISSING | no raw grid+tree+search/compare/load/save/presaved/commit |
| ConfigUserDefined (User Params) | `ConfigUserDefinedViewModel` (ConfigParamPages.cs:254) | PARTIAL | stub, no Modify/rows |
| ConfigPlanner | `ConfigPlannerViewModel` | PARTIAL | no Settings persistence wiring, no video/joystick/vario |
| ConfigPlannerAdv | — | MISSING | no settings-dump grid |
| ConfigTradHeli | — | MISSING | no heli swash/servo/RSC + ZedGraph |
| ConfigTradHeli4 | — | MISSING | no gen-2 heli grid |
| ConfigFFT | `ConfigFFTViewModel` (ConfigParamPages.cs:194) | PARTIAL | no FFT button / fftui plot |

\* DONE for the friendly metadata-filtered behaviour; minor feature parity to verify.
