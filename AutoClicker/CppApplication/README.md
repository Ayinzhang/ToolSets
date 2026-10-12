# AutoClicker

## Structure

- `Application`: executable entry point, GLFW window, OpenGL rendering and Dear ImGui UI.
- `Core`: static library reserved for clicking, timing and hotkey logic. Currently a template; no clicking is performed.
- `ThirdParty`: official Dear ImGui 1.91.9b (MIT) and GLFW 3.4 (zlib/libpng) sources, with their licenses. Version pins are recorded in `Dependencies.props`; editing them alone does not update the vendored sources.
- `Build.props`: shared C++17 settings and separate per-project intermediate directories.

The solution contains only Application and Core. Inside the Application project,
the `ThirdParty\ImGui` and `ThirdParty\GLFW` filters are siblings. Both libraries'
sources compile directly into Application; GLFW is not a separate project.
GLFW-specific compiler definitions apply only to its C sources.

Application references Core. Core should not depend on ImGui or GLFW;
the UI should call a small Core API rather than own the clicking loop. No extra
rendering abstraction or event framework is needed for this starter.

## Build and run

Open `Application.sln` with Visual Studio 2022, with the **Desktop development
with C++** workload and a Windows 10/11 SDK installed. Set **Application** as the
startup project, select Debug/Release and x64/x86, then build and run.

From a Visual Studio Developer PowerShell, in this directory:

```powershell
msbuild .\Application.sln /m /p:Configuration=Debug /p:Platform=x64
.\bin\x64\Debug\Application.exe
```

An OpenGL 3.3-capable graphics driver is required. Backend/shader initialization
errors are printed to the console and return a nonzero exit code. The console
subsystem is intentionally retained for development diagnostics.

There are no command-line options or built-in smoke tests. Application retains
startup error reporting, minimized-window waiting and destructor-based cleanup.
Core contains only the placeholder `Core.cpp`; precompiled headers are disabled.
The VS template files `pch.h`, `pch.cpp` and `framework.h` were removed: PCH is
an optional build-speed optimization, not a requirement for a static library.

GLFW is compiled from source into the executable, so no GLFW DLL, CMake step or
package download is required to build. ImGui's OpenGL backend supplies its own
function loader; this starter does not need GLAD or GLEW.

The compact, resizable 450-by-64 window combines four settings into one panel:
Button and Gap (ms) on the first row, Hotkey and Shutdown (min) on the second.
The 2-by-2 layout uses only `SameLine`, without cards, child windows or tables.
Binding instructions and the shutdown-disabled hint appear only in hover tooltips.

Mouse button selects Left/Middle/Right (default Left). Interval defaults to
100 ms and is clamped to at least 1 ms. Click the hotkey button and press a
single keyboard key to bind it (default F6); Escape cancels without changing the
binding. Mouse buttons and key combinations are not supported. PC shutdown delay
defaults to 0 (disabled) and cannot be negative. This setting means shutting down
the computer, not stopping clicks or exiting the application.

These are in-memory UI settings only: the hotkey does not yet start/stop clicking,
and no shutdown is scheduled or executed. Close the window with its title-bar
close button. The demo and FPS display have been removed.

Layout persistence is disabled to avoid writing `imgui.ini` into the working
directory. Labels use the bundled default Latin font; Chinese UI will require
a suitable font and glyph ranges.

Build products are under `bin\<Platform>\<Configuration>` and intermediate files
under `obj\<ProjectName>\<Platform>\<Configuration>`.
