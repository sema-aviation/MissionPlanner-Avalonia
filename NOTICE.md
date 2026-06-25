# NOTICE

MissionPlanner Avalonia is an independent, native cross-platform port of the user
interface of **ArduPilot Mission Planner**.

## Upstream credit

- Based on **Mission Planner** © Michael Oborne and the ArduPilot project.
  <https://github.com/ArduPilot/MissionPlanner>
- Mission Planner is licensed under the **GNU General Public License v3.0** (see `LICENSE`).
- This project links Mission Planner's library code (`ExtLibs/…`: Mavlink, Comms, Core,
  Utilities, ArduPilot, MissionPlanner.Drawing, …) **unmodified**, included as a git submodule
  pinned to upstream commit `14840eb0cd56b6ad824e05475383484d3213678f`.

## What this project changes

- The Windows-only WinForms UI is **not** used. A new UI is built with **Avalonia (.NET 10)** so the
  app runs natively on macOS (Apple Silicon), Linux and Windows.
- All flight/protocol/log/param/mission **logic is reused unchanged** from Mission Planner's libraries
  via project reference — only the presentation layer (Views + ViewModels) is new.
- See `MissionPlannerAvalonia/PORT_STATUS.md` for the page-by-page port state.

## License of this project

Because this work links Mission Planner's GPLv3 code, the combined work is a **derivative work and is
also licensed under GPLv3** (see `LICENSE`). You may copy, modify and redistribute it under those
terms, with source available and notices preserved.

## Not affiliated

This is an independent community port. It is **not** affiliated with, endorsed by, or supported by the
ArduPilot project or the original Mission Planner authors.
