# MissionPlanner Avalonia

A native, cross-platform (macOS / Linux / Windows) port of the **ArduPilot Mission Planner** UI,
built with **Avalonia (.NET 10)**. The original is Windows-only WinForms; this rebuilds the interface
while **reusing Mission Planner's flight / protocol / log / param / mission logic unchanged**.

> Independent community port — **not** affiliated with or endorsed by ArduPilot. Based on
> [Mission Planner](https://github.com/ArduPilot/MissionPlanner) (© Michael Oborne), GPLv3. See `NOTICE.md`.

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
dotnet tool restore
dotnet csharpier format src tests
dotnet csharpier check  src tests
dotnet test
```

## License

**GPLv3** (see `LICENSE`). This is a derivative work of Mission Planner and inherits its license.
