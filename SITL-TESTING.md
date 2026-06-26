# Testing this build with SITL

SITL (Software-In-The-Loop) runs a **simulated ArduPilot autopilot** in software — no flight
controller, no aircraft, no risk. It is the correct way to check whether this GCS behaves before you
trust it near real hardware.

> **Safety first.** This is an unverified community port. SITL testing tells you whether a screen
> *works*; it does **not** make the app airworthy. For any real flight, use official Mission Planner
> or QGroundControl as the ground station. Bench-test a real Pixhawk only with **props removed** and
> **never armed**.

There are two ways to get a SITL, depending on the OS.

---

## A. Windows — built-in SITL (your friend's machine, easiest)

The app can download and launch ArduPilot SITL itself.

1. Install & launch the release: unzip `MissionPlannerAvalonia-2026.6.4-win-x64.zip`, run
   `MissionPlannerAvalonia.exe`. (If SmartScreen warns: *More info → Run anyway* — it's unsigned.)
2. Click the **SIMULATION** tab in the top toolbar.
3. Pick the vehicle: **Plane**, **Copter**, **Rover**, or **Heli**.
4. **Model**: leave blank to use the vehicle default (fine for a first test).
5. **Channel**: choose **Stable** (most reliable). "Latest (Dev)" / "Beta" also work; "Skip Download"
   only if you already have a binary.
6. (Optional) drag the **H** home marker on the map; set **Sim speed** = 1; tick **Wipe EEPROM** for a
   clean first run.
7. Click **Start**. First run downloads the SITL binary (needs internet); watch the status line.
8. On success it auto-connects over `tcp:127.0.0.1:5760` and jumps to **FlightData**. Status reads
   *"SITL running and connected."*
9. To stop: back to SIMULATION → **Stop**.

If Start fails: read the status/log line. Usual causes are no internet (download blocked), antivirus
quarantining the binary, or a busy port — Stop, retry, or pick a different channel.

---

## B. macOS / Linux — external SITL (this repo's host machine)

The built-in launcher has **no prebuilt binary on macOS**, so start SITL yourself and connect over UDP.

**Get SITL** (any one):
- Linux / WSL2 / macOS with the ArduPilot dev env:
  ```bash
  git clone --recursive https://github.com/ArduPilot/ardupilot
  cd ardupilot
  ./Tools/environment_install/install-prereqs-ubuntu.sh -y   # or install-prereqs-mac.sh
  ./Tools/autotest/sim_vehicle.py -v ArduPlane --out=udp:127.0.0.1:14550
  ```
  (`-v ArduCopter`/`ArduRover` for other vehicles.) `sim_vehicle.py` builds + runs SITL and forwards
  MAVLink to UDP `14550`.
- Or run SITL in Docker / on another box and point its `--out` at this machine's IP.

**Connect this app:**
1. Launch the app, top-right connection bar.
2. Connection type: **UDP** (listen) on port **14550** — or whatever `--out` you set.
3. Click **Connect**. Params download, then telemetry appears on FlightData.

> macOS note: the host machine here is Apple-Silicon macOS, so use **method B**. Method A is for your
> friend's Windows box.

---

## C. Validation checklist — what to actually exercise

Walk these with SITL connected. The goal is to find pages that read/write wrong, *before* anyone
relies on them. Tick what behaves; report what doesn't.

**Connect & telemetry**
- [ ] Connects; full parameter list downloads without error.
- [ ] FlightData HUD: attitude horizon, altitude, speed, heading move and look sane.
- [ ] Mode shows correctly; arm/disarm from the GCS reflects in HUD (SITL, props irrelevant).
- [ ] Map shows the vehicle at the home location; it moves when the vehicle moves.

**Mission**
- [ ] PLAN tab: draw a few waypoints, **Write** to vehicle, **Read** back — they match.
- [ ] Auto mode flies the mission in SITL.

**Config pages reworked in this release (the point of the validation)**
- [ ] **Radio Calibration**: bars move with SITL RC; reverse checkboxes write `RCn_REVERSED`.
- [ ] **Flight Modes**: reads the 6 mode slots; Save writes them back.
- [ ] **Failsafe / Battery Monitor**: values populate; calibration writes the expected params.
- [ ] **Compass**: priority grid lists compasses; reorder/Use checkboxes write `COMPASS_*`.
- [ ] **Serial Ports / ADSB**: OPTIONS bitmask flyouts toggle the right bits.
- [ ] **Full Parameter List**: edit one param, write, refresh — value persisted.
- [ ] **Antenna Tracker** page only enables on ArduTracker firmware (expected: disabled otherwise).

**Stability**
- [ ] Switch between all six top tabs repeatedly — no crash, no frozen telemetry.
- [ ] Leave it connected for ~10 min — link stays up, no runaway memory.

Anything that misbehaves is a real bug to fix before this port is trustworthy. Until the checklist
passes cleanly for the screens you depend on, **fly with official Mission Planner**, not this build.
