# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Native cross-platform (macOS/Linux/Windows) **Avalonia (.NET 10)** port of ArduPilot **Mission Planner**
(originally Windows-only WinForms). The UI is rebuilt ~1:1 (visual target = MP 1.3.83); **Mission
Planner's flight/protocol/log/param/mission logic libraries are reused unchanged** via project reference
to the `external/MissionPlanner` git submodule. Independent community project, GPLv3, not affiliated with
ArduPilot.

**Hard rule: stay near 1:1 with upstream Mission Planner.** Never drop features. Mirror upstream
icons/layout/behavior for every change. `AVALONIA-FEATURES.md` (Implementation Log at top = source of
truth for port progress) and `UPSTREAM-FEATURES.md` track parity. `HANDOFF.md` is a deep, self-contained
state doc — **read it first** for full context (it is gitignored / local-only).

## Commands

```bash
git submodule update --init --recursive          # required; logic libs live in the submodule
dotnet run --project src/MissionPlannerAvalonia   # build + launch native window
dotnet test                                       # full xUnit suite
dotnet test --filter "FullyQualifiedName~WpRowTests"   # single test class
dotnet format src/MissionPlannerAvalonia/MissionPlannerAvalonia.csproj   # apply Google C# style
dotnet format <proj> --verify-no-changes          # CI format gate
```

- Lint = Roslyn analyzers (`EnableNETAnalyzers` + `EnforceCodeStyleInBuild`, set in `Directory.Build.props`).
- Foreground `sleep` is blocked in this shell. To verify the GUI: launch detached, poll, then
  `screencapture -x -D 1`.

## Toolchain pins (do NOT bump blindly)

- **.NET 10** (`global.json` 10.0.301). App TFM `net10.0`; reused MP libs are `netstandard2.0`.
- **Avalonia 11.3.13** — NOT 12 (12 + Mapsui crashes with `OptionalFeatureProviderExtensions` TypeLoad).
  `Mapsui.Avalonia`/`Mapsui.Tiling` 5.1.0. `CommunityToolkit.Mvvm` 8.4.1.
- Tests: **xUnit v2** (2.9.3) + `Avalonia.Headless.XUnit` 11.3.13 (headless integration targets v2, not v3).
- **Central Package Management**: all our versions in `Directory.Packages.props`; csproj `PackageReference`
  carries NO `Version`. The submodule is *shielded* — its own (empty) `Directory.Build.props` +
  `Directory.Packages.props` stop our root props from leaking in, so it keeps its inline versions.
  When adding a package, add the version to `Directory.Packages.props`.

## Architecture (the spine — every screen plugs into this)

- **`AppState.comPort`** (`src/.../AppState.cs`) — single shared `MissionPlanner.MAVLinkInterface`,
  replacing MP's `MainV2.comPort`. The `MAVLink` class is in the GLOBAL namespace; `Locationwp` is in
  `MissionPlanner.Utilities`.
- **Telemetry**: `comPort.MAV.cs` (`CurrentState`, ~170 props: attitude, alt, speeds, battery, sats,
  mode, armed, lat/lng, channel in/out, prearm/ekf status…). A 10 Hz `DispatcherTimer` mirrors `cs`
  into FlightData/RadioInput VMs.
- **Params**: `comPort.MAV.param["NAME"]`; metadata via `ParameterMetaDataRepository` +
  `ParameterMetaDataConstants` (`MissionPlanner.Utilities`).
- **Threading**: UI updates via `Dispatcher.UIThread`; blocking MAVLink calls via `Task.Run`.
- **Two reusable engines make new pages cheap** (often a VM with no XAML):
  - *Param-page*: `ViewModels/GCSViews/ConfigurationView/ParamField.cs` + `ParamPageBase.cs`, rendered
    by `Controls/ParamFieldsView.axaml`. A page declares params via `F("NAME")`; engine reads
    value+metadata and writes via `setParam`. ~20 config pages need only a VM.
  - *Action-page*: `ViewModels/ActionPageViewModel.cs` + `Views/ActionPageView.axaml`. Buttons fire real
    `doCommand`/`doMotorTest` plus a log pane; powers calibration pages.
  - `ViewLocator` falls back to these engines for `ParamPageBase`/`ActionPageViewModel` subtypes.
- **Backstage shell**: `ViewModels/BackstageViewModel.cs` + `Views/BackstageView.axaml` — sidebar →
  content host. CONFIG (`ConfigViewModel`) and SETUP (`SetupViewModel`) list every real MP page.
- Layout mirrors MP's tree: `Views/` + `ViewModels/` with parallel `GCSViews/`, `Setup/` subfolders.

## Conventions

- **Commits = Conventional Commits, one-line subject, NO body** (`feat:`/`fix:`/`chore:`/`refactor:`/
  `docs:`/`build:`/`ci:`/`test:`). Never add a Claude co-author trailer.
- **Code comments: keep nearly none** (user preference) — strip new comments unless a rare load-bearing note.
- **Style: Google C# Style Guide** — 2-space indent, 100 col, K&R braces, System-first usings,
  `_camelCase` private fields, PascalCase members, `I`-prefixed interfaces. Enforced via `.editorconfig`
  + `dotnet format`. File-scoped namespaces are kept.
- **Versioning: CalVer** `YEAR.MONTH.PATCH` (`<Version>` in `Directory.Build.props`, month/year
  auto-stamped; set `VersionPatch` by hand). Independent of MP's 1.3.x.

## Tracking upstream

MP is pinned via the `external/MissionPlanner` submodule. To adopt upstream changes:
`cd external/MissionPlanner && git fetch && git checkout <commit>`, rebuild/test, commit the bumped
pointer here, then cut a new CalVer release noting the MP commit.
