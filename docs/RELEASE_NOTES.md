# DishSoap Windows v0.1.0

This preview provides a Windows x64 builder for The Dishwasher: Dead Samurai. It prepares a playable Windows build from your own legitimate game copy, with a separate settings editor and optional AI-upscaled textures.

## Download and use

Download `DishSoap-Builder-v0.1.0-win-x64.zip`, extract the entire archive to a writable folder and open `DishSoap.Builder.exe`. Select your local game copy, choose a destination parent folder and click Build my game. Each run creates a new game subfolder inside that destination.

The builder displays its stages and live log. On success it reaches 100%, shows a completion message and opens the output folder. Open `DishSoap.Configurator.exe` there to save preferences, then open `Dishwasher.exe` to play. The builder does not launch the game automatically.

`Build-My-Game.bat` is an alternative entry point. In this release it opens the compiled builder; in a source checkout it starts the PowerShell UI directly. All Windows-facing tools, guides and entry filenames use English regardless of the Windows region.

## What the ZIP contains

The ZIP includes the compiled `DishSoap.Builder.exe`, its self-contained .NET 8 Windows x64 runtime, the source and scripts required by the build pipeline, rewritten shaders, documentation and license notices. `BUILD_INFO.txt` records the version and source commit. `SHA256SUMS.txt` lists the packaged file hashes. A separate `.zip.sha256` asset provides the archive hash.

The compiled entry point starts the included Windows PowerShell WinForms builder without a console window. Keep the whole extracted directory together; the executable needs its runtime and the included scripts. You do not need to type commands or install a .NET runtime to open this release.

The ZIP does not include the original game, a ready-to-play Dishwasher.exe, decompiled game logic, imported textures, audio, maps, saves or AI model weights. Those are prepared locally from your copy. Downloading the builder alone does not provide the game.

## Build pipeline

The builder runs six stages:

1. Find compatible local tools or download the required toolchain.
2. Extract the managed assemblies, decompile the game and apply desktop source adaptations.
3. Publish the game and standalone configurator as self-contained Windows x64 applications.
4. Import the original content and convert audio.
5. Generate the AI texture pack and the additional background pass.
6. Write the local build report and confirm the output executable exists.

The source preparer accepts supported STFS/LIVE packages, ZIP archives and extracted folders. XEX2 BASIC and NORMAL/LZX compression are supported for embedded managed x86 PE assemblies. It does not emulate an Xbox 360, translate PowerPC code or implement the Xbox kernel. Delta patches and XEX1 are unsupported.

ILSpy 9.1.0.7988 decompiles the extracted assemblies using their original references. The scripts create a private desktop GameSource tree and record source transformations and hashes in preparation reports. A different retail revision may require additional work; this preview was validated against the locally supplied copy.

The tool bootstrap uses .NET SDK 8.0.426 and Python when suitable installations are unavailable. The game uses MonoGame DesktopGL 3.8.5.1 and NVorbis 0.10.5, restored through NuGet. FFmpeg is used for XMA audio conversion and Real-ESRGAN NCNN Vulkan for image preparation. The release includes the builder runtime, while these development and conversion tools are obtained separately when needed.

Pinned downloads are checked against expected hashes. The FFmpeg download is checked against the provider's published checksum. Tool downloads remain under `.local-tools`, with their notices. The first complete preparation needs internet access. No Android SDK, emulator or administrator installation is required.

## Progress and completion

This version fixes a Windows file-sharing bug that prevented live progress from appearing. Start-Process keeps redirected stdout and stderr files open for writing. The earlier UI tried File.ReadAllText, whose sharing mode conflicted with that writer, then ignored the read failure.

The log reader now uses FileShare.ReadWrite with FileShare.Delete. The worker also writes atomic per-run progress JSON independently of buffered console output. The initial stage shows an animated bar; later updates follow stages and texture counts. Received megabytes appear when a download file is available. Percentages describe build stages, not predicted time remaining.

Completion uses a separate result file and checks the actual executable. A failure shows an error instead of reporting a partial build as ready. Regression checks cover a genuinely running redirected child process, live stage/log updates, success and failure UI states, and a real owned-copy game compilation reaching 100%.

## Display and configuration

The game starts directly using `%LOCALAPPDATA%\DishSoapWindows\Config\desktop.json`. The separate settings editor uses standard Windows controls, DishSoap branding and the title `dishwasher`. Saving settings closes the editor; it does not start gameplay.

Display output is fixed at 1280 x 720. Rendering uses a 1920 x 1080 internal canvas and downsamples it for presentation. Game coordinates remain 1280 x 720. Desktop adapters scale render targets, sprite coordinates, source rectangles and pivots while preserving logical positioning. Font atlases and glyph metrics are handled separately. The internal canvas is not a selectable output resolution.

The configurator offers Original or Upscaled textures, windowed/fullscreen display, VSync, Show FPS, Smooth Motion and FPS limits of 60, 90, 120, 144, 165, 180, 240 or 360. It has no volume slider or controls legend. Existing saved volume is retained. Alt+Enter toggles fullscreen during play.

## AI textures and backgrounds

Upscaled textures are produced by Real-ESRGAN AI models on your own machine. The standard pack contains 128 sheets at 2x dimensions: 121 art sheets use AnimeVideo-v3 and seven font sheets use bicubic scaling to preserve letter shapes. Eight background sheets receive a separate Real-ESRGAN x4plus pass at 4x, then are reduced to 2x. The background overrides keep the same final dimensions as the other HD sheets.

RGB padding is dilated before inference. Alpha is reconstructed from the corrected source rather than inferred by the model. The final compositor combines neural luminance detail with the original channel relationships. Transparent pixels are zeroed and source black stays black. The content loader maps the larger atlas back to logical sprite rectangles, scales, origins and font measurements.

A Vulkan-compatible GPU is required for AI preparation. Processing occurs before playing; there is no neural inference in the render loop. Models and engine binaries come from the separately downloaded upstream release. The Original option uses the original texture treatment. AI can alter small details and does not recover an authoritative high-resolution master of the artwork.

## Graphics corrections

The desktop DXT decoder corrects 8-in-16 color-index byte ordering. Incorrect ordering swapped pairs of rows inside 4 x 4 blocks and caused broken-looking diagonals. BC3/DXT5 palette handling now keeps four colors even when endpoint0 is less than or equal to endpoint1. Windows imports opt into the corrected shared decoder and record the decoder version in the report.

A second fix addresses false purple/cyan bloom in AI textures. The bloom effect uses B > R as an emission mask. Even a one-byte blue drift on a neutral surface could trigger a bright themed glow. The new compositor preserves the source B-R and G-R differences exactly while applying bounded luminance changes. It leaves legitimate authored emission available.

The pixel audit of eight old scene sheets found more than 6.6 million opaque pixels that incorrectly activated the mask. The corrected pack had zero new activations and zero lost activations relative to the interpolated original baseline. Separate audits covered alpha, dimensions and channel differences across 128 sheets and eight overrides. A GPU test using the actual bloom shader reproduced the drift and verified both the fix and original blue emission.

These changes address confirmed defects. They do not establish that every stage composition is correct; large empty-area blocks shown in an earlier gameplay screenshot still need manual review.

## Frame timing

Game logic advances in fixed 1/60-second steps while rendering runs at the selected cap. Catch-up is limited to 0.25 seconds and 15 simulation steps. Optional interpolation draws character/camera positions between states and restores the real simulation values afterward. Discontinuities over 256 units skip interpolation. Interpolation may add up to one simulation step of visual latency.

Draw-related counters advance once per logic tick. Visual random values use a separate sequence, so extra render frames do not consume the game simulation RNG. Rendering drops to a 30 FPS cap when unfocused. The overlay shows actual render FPS and logic Hz.

A local synthetic benchmark reached 180 render FPS with 60 Hz logic on an RX 6650 XT. It was a short warmed-up technical scene. Campaign performance, long sessions and every effect at high render rates remain unverified. VSync and monitor refresh can limit presentation regardless of the selected cap.

## Input, audio and saves

Keyboard and SDL/MonoGame gamepad input are read during play. A local Player profile signs in through the compatibility layer; no Xbox LIVE account service is provided. Default keyboard bindings are documented in WINDOWS_BUILD.md. Scripted gameplay input support has been removed.

Audio preparation converts XMA through the separately obtained FFmpeg executable. PCM big-endian data is converted in C#. Runtime playback uses XactShim, NVorbis and the desktop audio backend. A previous audit of the supplied copy reported 458 content manifest entries, 222 audio outputs and 216 cues; those counts describe that input copy.

Runtime data is stored under `%LOCALAPPDATA%\DishSoapWindows`: Imports contains separate content installations, Saves contains game XML and recovery backups, Config contains preferences and the active-content pointer, Cache holds conversion intermediates and Logs records startup/import/audio errors. Save writes use atomic replacement and backup recovery. A failed import keeps the previous active installation.

The imported content is outside the executable directory. Copying only the compiled game folder to a different PC does not move that content. Keep original packages, generated GameSource, imported data and saves out of public uploads.

## Credits and limits

The Windows port is developed by joojofc13-wq. Most Windows-specific implementation was developed for this project, including the builder/configurator, desktop integration, storage, timing/rendering changes and AI texture workflow. Shared import infrastructure, STFS/XNB readers, audio bank/cue readers, XactShim, content manager, XNA compatibility shims, rewritten shaders and reference tools are reused or adapted from Hdmaniak/DishSoap, the Android port. The LZX decoder is separately credited to Xenia Manager.

Real-ESRGAN models and tools, .NET, MonoGame, NVorbis and other dependencies keep their upstream licenses. The downloaded Gyan FFmpeg executable is GPLv3 and is not bundled in this ZIP. Read LICENSE and THIRD-PARTY-NOTICES.md for scope and notices. This release grants no rights to distribute the original game or recompiled game logic.

The full campaign, every stage, co-op/network play, all controller hotplug cases and prolonged high-FPS sessions have not been validated. This is a preview release for local owned-copy builds.
