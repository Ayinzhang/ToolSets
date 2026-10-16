# AutoClicker

[English Version](README.md)

**一个简洁的 Windows 鼠标连点器，分别用 C++、C# 和 Python 实现，采用相近的界面与核心逻辑，方便对照不同技术栈的实现。**

*支持鼠标左键、中键和右键连点，可设置点击间隔。*

*自定义单键热键，后台也能切换开始 / 停止。*

*支持连点计时关机，停止连点取消倒计时，重新开始会重新计时。*

## 版本与依赖

| 版本 | 界面 | 开发环境 |
| --- | --- | --- |
| [C++](CppApplication/) | Dear ImGui + GLFW + OpenGL | Visual Studio 2022，C++ 桌面开发工作负载、MSVC v143、Windows SDK；支持 OpenGL 3.3 的显卡驱动 |
| [C#](CsApplication/) | Windows Forms | .NET 9 SDK；使用 Visual Studio 时需安装 .NET 桌面开发工作负载 |
| [Python](PyApplication/) | Tkinter / ttk | Python 3.9 及以上，包含可用的 Tcl/Tk；无需第三方 pip 包 |

C++ 版本已在 `ThirdParty` 中包含 Dear ImGui 和 GLFW 源码，无需单独安装。
三个版本的核心均调用 Windows API，目前仅支持 Windows。

## 快速开始

**C++ / C#**

打开对应目录下的 `Application.sln`，将 `Application` 设为启动项目后运行。
C++ 可选择 `x64` 配置；C# 也可在本目录用 PowerShell 启动：

```powershell
dotnet run --project .\CsApplication\Application\Application.csproj
```

**Python**

在本目录使用 PowerShell 运行：

```powershell
$env:PYTHONPATH = (Resolve-Path .\PyApplication\Core).Path
python .\PyApplication\Application\Main.py
```

也可打开 `PyApplication\PyApplication.sln`，将 `Application` 设为启动项目。
所用的 Visual Studio 实例需安装 **Python 开发**工作负载；项目使用 VS 默认的 Python 环境，不绑定具体版本。
若 VS 2022 内置调试器在 Python 3.14 下报 `_MainThread._handle` 缺失，
请保留项目启动环境中的 `PYDEVD_USE_SYS_MONITORING=0`，使用传统追踪方式。

## 使用方式

1. 在 `Click` 中选择 `Left`、`Middle` 或 `Right`。
2. 设置 `Gap(ms)`，单位为毫秒，默认 `100`；建议使用正数，实际频率受界面更新和系统调度影响。
3. 点击 `Hotkey`，按下需要绑定的键；`Esc` 取消绑定。再次按下已绑定热键即可开始 / 停止连点。
4. 如需定时关机，在 `PowerOff(mins)` 中填写分钟数；默认 `0`，小于或等于 `0` 时不关机。

**注意：** 倒计时从开始连点时计算，到时会尝试关闭整台电脑，而不是仅退出应用，请提前保存工作。

热键为单键而非组合键；`Enter` 与小键盘 `Enter` 共用同一热键。进入热键绑定模式会停止连点。
