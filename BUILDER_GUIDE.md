# Builder guide

## Start the builder

Download the Windows x64 builder ZIP from GitHub Releases and extract the entire archive. Keep its files together in a writable directory. Open `DishSoap.Builder.exe`; its .NET runtime is included.

`Build-My-Game.bat` is the alternative entry point. In the binary release it opens the compiled builder. In a source checkout it starts `tools/windows/owner-builder.ps1` using Windows PowerShell.

## Build your game

Select your legitimate local copy using File or Folder. Inputs can be a supported STFS/LIVE XBLA package, a ZIP or an extracted game folder. A standalone managed `game.exe` can be used for manual source preparation, but needs the original references and separate content to produce a playable installation.

Choose a destination parent folder. It can already contain files. Each run creates a separate `Dishwasher-yyyyMMdd-HHmmss-id` child directory.

Click Build my game and follow these stages:

1. Prepare local tools.
2. Extract managed assemblies, decompile and adapt the source.
3. Compile the game and configurator for Windows x64.
4. Import content and convert audio.
5. Prepare AI-upscaled texture sheets and backgrounds.
6. Verify completion and write the local build report.

The log updates while the worker runs. Initial setup shows an animated bar; later progress follows stages and texture counts. Downloaded megabytes appear when a transfer file is available. Percentages describe work stages, not an estimate of remaining time.

On success, the bar reaches 100%, a message appears and the game output folder opens. On failure, read the error in the log. A compiled executable left by a failed content or upscale stage does not mean the full preparation succeeded. The builder never starts the game automatically.

## Update an existing installation

Open the builder and click **Download game update**. Select the folder containing `Dishwasher.exe` and `local-build.json`. The builder checks the latest stable GitHub release, downloads its kit and verifies the ZIP against the SHA-256 digest reported by GitHub before running it. The builder compares the installed version and the release ZIP digest. An updated ZIP under the same release tag can be applied once; the same installed digest is not downloaded again.

The downloaded kit recompiles your copy and replaces the game and configurator in the same folder. It reuses the active imported content and upscaled textures instead of extracting audio or running AI processing again. Your original local game copy is still needed to regenerate the adapted source. If it has moved, select its new location in the source field before updating.

Close the game and configurator before updating. The builder does not close them for you. Replaced files are preserved under `UpdateBackups` in the game folder. A failed replacement restores the earlier files; if Windows prevents restoration, the error identifies the backup folder for manual recovery. Saves and settings under `%LOCALAPPDATA%\DishSoapWindows` are preserved. Future updates can be downloaded from this builder without manually replacing its kit.

Download the revised 0.3.0 builder once to receive the lettering overlays and same-version ZIP detection. Earlier 0.3.0 builders only compare version numbers. Version 0.1.0 and 0.2.0 installations can also be updated using this builder. Their old builders do not gain the new update button automatically. The original game copy and a working content import must still be available on this PC.

## Verify installed files

Click **Verify installed files** and choose a game built with version 0.3.0 or later. Verification runs in a background worker and reports individual audio files in the log. It checks installed runtime hashes, original unmodified content hashes, derived texture formats, AI texture hashes and WAV/Vorbis audio decoding, including guitar cues. It does not play audio or start gameplay.

New builds and successful update preparation record a SHA-256 baseline of the derived content. Older imports are first checked using the original manifest, texture manifests and decoded file formats; their first baseline cannot prove that an already altered derived file matches its original import. Verification reports this distinction. Damaged content is reported rather than silently accepted. Use **Build my game** with your original copy to regenerate broken assets.

## Play and configure

Open `DishSoap.Configurator.exe`, choose your preferences and save. It does not start the game. Open `Dishwasher.exe` to play with the saved configuration.

Output is fixed at 1280 x 720. Settings include Original or Upscaled textures, fullscreen, FPS limit, VSync, Show FPS and Smooth Motion. The interfaces use English on every Windows region. **Game language** selects the bundled in-game translation. English is the default; Automatic optionally follows the Windows UI language. Portuguese is the original European Portuguese translation. This setting takes effect on the next game launch and does not translate lettering painted into scenes.

## Tools and storage

The initial build needs internet access for tools and NuGet packages. Compatible installed tools may be reused. Otherwise, the scripts obtain .NET 8, Python, FFmpeg and Real-ESRGAN in `.local-tools`, with download verification. AI processing requires a Vulkan-compatible GPU. Preparation can take considerably longer than compilation.

The game uses `%LOCALAPPDATA%\DishSoapWindows` for imports, saves, configuration, cache and logs. Assets are stored there rather than copied into the executable folder. Moving only the compiled game folder to another computer does not transfer its imported content.

The builder's `work` directory contains generated private source and preparation reports. Keep it out of public uploads. The output game contains recompiled logic from your copy; the builder release grants no rights to distribute it or the original assets.

See [manual commands](docs/WINDOWS_BUILD.md), [rendering](docs/HD_LAUNCHER.md) and [release details](docs/RELEASE_NOTES.md).
