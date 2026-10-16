# AutoClicker

[中文版](README_zh.md)

**A simple Windows auto clicker, implemented in C++, C#, and Python, with similar interfaces and core logic for comparison.**

*Repeat left, middle, or right mouse clicks with a configurable interval.*

*Bind a single-key hotkey to start / stop clicking, even in the background.*

*Optionally shut down after a period of clicking. Stopping cancels the countdown; starting again resets it.*

## Versions & Requirements

| Version | UI | Development Environment |
| --- | --- | --- |
| [C++](CppApplication/) | Dear ImGui + GLFW + OpenGL | Visual Studio 2022 with Desktop development with C++, MSVC v143, Windows SDK, and an OpenGL 3.3-capable graphics driver |
| [C#](CsApplication/) | Windows Forms | .NET 9 SDK; .NET desktop development workload when using Visual Studio |
| [Python](PyApplication/) | Tkinter / ttk | Python 3.9 or later with working Tcl/Tk; no third-party pip packages |

Dear ImGui and GLFW sources are included in the C++ version's `ThirdParty` directory.
All three versions use Windows APIs and currently support Windows only.

## Getting Started

**C++ / C#**

Open `Application.sln` in the corresponding directory, set `Application` as the startup project, and run.
For C++, you can select an `x64` configuration. For C#, you can also run this PowerShell command from this directory:

```powershell
dotnet run --project .\CsApplication\Application\Application.csproj
```

**Python**

Run from this directory in PowerShell:

```powershell
$env:PYTHONPATH = (Resolve-Path .\PyApplication\Core).Path
python .\PyApplication\Application\Main.py
```

Alternatively, open `PyApplication\PyApplication.sln` and set `Application` as the startup project.
Install the **Python development** workload in the Visual Studio instance you use.
The projects use VS's default Python environment without pinning a specific version.
If VS 2022's bundled debugger reports a missing `_MainThread._handle` attribute
with Python 3.14, keep `PYDEVD_USE_SYS_MONITORING=0` in the project's launch
environment to use traditional tracing.

## Usage

1. Select `Left`, `Middle`, or `Right` in `Click`.
2. Set `Gap(ms)` in milliseconds; the default is `100`. Use a positive value; actual frequency depends on UI updates and system scheduling.
3. Click `Hotkey` and press a key to bind it; `Esc` cancels binding. Press the bound hotkey to start / stop clicking.
4. Optionally set `PowerOff(mins)` in minutes. The default is `0`; values of `0` or less disable shutdown.

**Warning:** The countdown starts when clicking starts. When it expires, the app attempts to shut down the computer, not just close the window. Save your work first.

Hotkeys are single keys, not key combinations. `Enter` and keypad `Enter` share the same binding. Entering hotkey binding mode stops clicking.
