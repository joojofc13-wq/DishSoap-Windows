# Windows build and execution

The automated path is `DishSoap.Builder.exe` from the release, or `Build-My-Game.bat` from a source checkout. Both run the own-copy build pipeline. Manual commands below require Windows x64, .NET 8 SDK, Python 3 and your legitimate game copy. No Android SDK, JDK or NDK is needed.

## Prepare local source

```powershell
./tools/windows/prepare-game.ps1 -Source 'C:\MyCopy\XBLA-package'
```

Supported inputs include an STFS/LIVE package, ZIP, extracted folder, or managed `game.exe` with its original dependencies. ILSpy 9.1.0.7988 decompiles the assemblies extracted from XEX2 BASIC/LZX containers. Preparation creates a separate desktop `GameSource` and refuses to overwrite an existing source tree. `port-report.json` records hashes and source transformations. Other game revisions may need additional adaptation.

```powershell
./tools/windows/build-windows.ps1
# Equivalent game publication:
dotnet publish src/Dishwasher.Desktop/Dishwasher.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishDir=../../dist/DishSoap-Windows-x64/
```

The script also publishes `DishSoap.Configurator.exe`. MonoGame DesktopGL 3.8.5.1 and NVorbis 0.10.5 are restored through NuGet. The game output includes the .NET runtime and recompiled logic from your copy. Original resources and private source are kept outside the executable folder.

To build only the importer and diagnostics, without game logic:

```powershell
./tools/windows/build-windows.ps1 -SetupOnly
```

This creates `DishSoap.Setup.exe`; it is not a playable game.

## Import content

The importer accepts the game package, a content ZIP or an extracted root containing `data`, `gfx`, `Resources` and `sfx`. For an Xbox directory hierarchy, select the actual package inside `58410902\000D0000`, not a parent containing only numbered folders.

The automated builder obtains FFmpeg when needed. For a manual import, provide an executable with XMA2 decoding and Vorbis encoding:

```powershell
$env:DISHWASHER_FFMPEG = 'C:\FFmpeg\bin\ffmpeg.exe'
./dist/DishSoap-Windows-x64/Dishwasher.exe --import 'C:\MyCopy\XBLA-package'
./dist/DishSoap-Windows-x64/Dishwasher.exe
```

XMA conversion occurs during import. PCM big-endian conversion is handled in C#. Playback uses XactShim, NVorbis and the desktop audio backend. The separately downloaded Gyan FFmpeg build is GPLv3; its notices stay with the tool.

Data lives under `%LOCALAPPDATA%\DishSoapWindows`:

| Directory | Contents |
| --- | --- |
| Imports | A separate content installation for each import |
| Saves | Game XML saves, profile/settings files and recovery backups |
| Config | `desktop.json` and the active-content pointer |
| Cache | Temporary conversion data |
| Logs | Startup, import, audio and error logs |

A failed import preserves the previous active installation. Read `import-report.json` and the log for warnings. Do not remove active imported files while playing.

## Input and settings

The local Player profile signs in automatically. There is no Xbox LIVE sign-in service. Keyboard and SDL/MonoGame controller input are polled during play.

| Key | Default action/button |
| --- | --- |
| WASD or arrow keys | Movement and menus |
| Enter | Confirm / Start |
| Escape | Back / pause |
| J | A / jump / confirm |
| K | X / attack |
| L | B / grab / back |
| I | Y / heavy attack |
| Q / E | LB / RB |
| U / O | LT / RT |
| Alt+Enter | Toggle fullscreen |

The original game menus use controller-style prompts. The mouse does not activate them. Bindings can be edited in `Config/desktop.json` with the game closed.

`Dishwasher.exe` starts directly with saved preferences. `DishSoap.Configurator.exe` edits settings separately; `Dishwasher.exe --configure` opens the same editor. Output is fixed at 720p, with Original or Upscaled textures, fullscreen, FPS limit, VSync, Show FPS and Smooth Motion. There is no resolution selector, volume slider or controls legend. Windows interfaces use English.

The internal canvas is 1920 x 1080; game coordinates remain 1280 x 720. Original logic runs at 60 Hz independently of rendering. A selectable 180 FPS cap does not guarantee that performance in every scene. See [rendering details](HD_LAUNCHER.md).

## Prepare AI textures manually

```powershell
./tools/windows/upscale-game.ps1 -ContentRoot 'C:\LocalData\Imports\import-id'
```

The script processes 128 sheets at 2x and adds eight restored background sheets. It requires a supported Vulkan GPU and refuses an old import with the incorrect DXT index ordering. Reimport using the updated game before upscaling such content.

## Diagnostics

```powershell
Dishwasher.exe --diagnose
Dishwasher.exe --graphics-check
Dishwasher.exe --graphics-check --render-benchmark --data-root C:\Diagnostics
dotnet run --project tools/windows/Tests -c Release -- C:\NewTestFolder
powershell -STA -File tools/windows/owner-builder.ps1 -UiCheck
```

Graphics diagnostics load, draw and read back the rewritten effects and check HD atlas coordinates, transparency and fonts. The benchmark measures a short synthetic 1080p scene; it does not establish campaign performance or visual fidelity.

`--smoke-seconds N` and `--capture-dir PATH` are diagnostic options. Scripted gamepad input support was removed. Diagnostics do not supply gameplay input. Keep captures and all derived game data out of public commits.

## Package the compiled builder

From a clean committed checkout:

```powershell
./tools/windows/package-builder-release.ps1 -Version 0.1.0 -Dotnet dotnet
```

This publishes a self-contained Windows x64 builder and combines it with a committed source allowlist. It verifies binary origins, rejects private content and writes SHA256 manifests. `package-builder.ps1` produces a source-only kit.
