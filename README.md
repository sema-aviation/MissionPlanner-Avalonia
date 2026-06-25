# MissionPlanner Avalonia

A native, cross-platform (macOS / Linux / Windows) port of the **ArduPilot Mission Planner** UI,
built with **Avalonia (.NET 10)**. The original is Windows-only WinForms; this rebuilds the interface
while **reusing Mission Planner's flight / protocol / log / param / mission logic unchanged**.

> Independent community port — **not** affiliated with or endorsed by ArduPilot. Based on
> [Mission Planner](https://github.com/ArduPilot/MissionPlanner) (© Michael Oborne), GPLv3. See `NOTICE.md`.

## Status

Early. DATA / PLAN / SETUP / CONFIG screens, the backstage page nav, a reusable parameter-editing
engine and ~40 config/setup pages are wired to the real MAVLink API. Many pages are reachable stubs;
several subsystems (log graphing, video OSD, joystick, scripting, SITL, firmware uploader) are
deferred. Not yet tested against a live vehicle/SITL.

## Repository layout

```
.
├── src/MissionPlannerAvalonia/        # the Avalonia app (Views + ViewModels, mirrors MP's GCSViews tree)
├── tests/MissionPlannerAvalonia.Tests # xUnit + Avalonia.Headless tests
├── external/MissionPlanner            # ArduPilot Mission Planner — pinned git submodule (reused logic libs)
├── Directory.Build.props              # shared settings + identity (our projects only)
├── Directory.Packages.props           # central package versions
├── global.json                        # pinned .NET SDK
├── .editorconfig / .csharpierrc.json  # style + formatter config
└── MissionPlannerAvalonia.sln
```

## Build & run

Requires the **.NET 10 SDK**. Mission Planner's libraries come in as a git submodule.

```bash
git clone --recurse-submodules https://github.com/sema-aviation/MissionPlanner-Avalonia.git
cd MissionPlanner-Avalonia
dotnet run --project src/MissionPlannerAvalonia
```

Already cloned without submodules: `git submodule update --init --recursive`.

## Development

```bash
dotnet tool restore               # restore CSharpier (pinned in .config/dotnet-tools.json)
dotnet csharpier format src tests # format
dotnet csharpier check  src tests # CI format gate
dotnet test                       # run tests
```

Formatting is **CSharpier** (opinionated); linting is the built-in Roslyn analyzers via
`.editorconfig` + `EnforceCodeStyleInBuild`. CI (`.github/workflows/ci.yml`) runs the format check +
tests on every push/PR.

## Versioning & tracking upstream

- **CalVer** — `YEAR.MONTH.PATCH` (e.g. `2026.6.0`). Independent of Mission Planner's `1.3.x`.
- Mission Planner is tracked via the **`external/MissionPlanner` submodule pin**. To adopt upstream
  changes: `cd external/MissionPlanner && git fetch && git checkout <new-commit>`, rebuild/test, then
  commit the bumped submodule pointer in this repo and cut a new CalVer release. Each GitHub Release
  notes the upstream commit it was built against.

## Releases

Pushing a `v*` tag (e.g. `git tag v2026.6.0 && git push origin v2026.6.0`) runs
`.github/workflows/release.yml`, which publishes self-contained builds for macOS (arm64),
Windows (x64) and Linux (x64) and attaches them to a GitHub Release. The macOS build is **unsigned**
(right-click → Open, or `xattr -dr com.apple.quarantine <app>`).

## License

**GPLv3** (see `LICENSE`). This is a derivative work of Mission Planner and inherits its license.
