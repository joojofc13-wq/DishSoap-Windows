# DishSoap Windows

A Windows x64 port of **The Dishwasher: Dead Samurai**, with a builder for your own copy and a separate settings editor.

This repository contains the port's code, tools, rewritten shaders, and documentation. **It does not include the original game, decompiled game logic, textures, audio, maps, saves, or a ready-to-play game executable.** You must supply your own legitimate copy.

## Build your copy

1. Download this repository using **Code → Download ZIP** and extract it to a writable folder.
2. Open **Compilar-meu-jogo.cmd**.
3. Select your local copy: an XBLA package, ZIP archive, or extracted folder.
4. Choose a destination folder. The builder creates its own subfolder, so the selected folder does not need to be empty.
5. Follow the build stages and log. When the build finishes, a notification appears and the game folder opens automatically.

The output folder contains:

- **Dishwasher.exe**, which launches the game directly using your saved preferences.
- **DishSoap.Configurator.exe**, which saves preferences through a standard Windows interface without launching the game.

The builder downloads .NET 8, Python, FFmpeg, and Real-ESRGAN from their providers when needed. Initial preparation requires internet access; preparing upscaled textures requires a Vulkan-compatible GPU. No Android SDK, emulator, or administrator privileges are required.

[Builder guide](LEIA-ME-COMPILADOR.md) · [Manual build instructions](docs/WINDOWS_BUILD.md)

The additional guides linked below are currently in Portuguese.

## Features

- Fixed **720p** output, downsampled from a higher-resolution internal canvas.
- **Original** or **Upscaled** textures, with restored backgrounds and preserved original colors and masks.
- Windowed or fullscreen display, FPS limit, VSync, frame counter, and motion smoothing.
- A 60 Hz simulation independent of the rendering frame rate.
- Settings editor and builder in Portuguese, English, or Spanish, based on the Windows region.
- A local player profile, keyboard/controller support, and saves with atomic writes and backup recovery.

Preferences are stored in `%LOCALAPPDATA%\DishSoapWindows\Config\desktop.json`; saves are stored in `%LOCALAPPDATA%\DishSoapWindows\Saves`.

[Settings and rendering](docs/HD_LAUNCHER.md) · [Color fixes and builder progress](docs/UI_COLOR_FIX.md)

## Status and limitations

The game has been built and played locally. The full campaign, every stage, co-op, and long-term stability have not been validated. A 180 FPS limit is a preference, not a performance guarantee for every machine or stage. Shader and texture tests do not establish visual fidelity for every effect.

[Verified status and limitations](docs/WINDOWS_PORT_STATUS.md)

## Project structure

- `src/Dishwasher.Desktop`: Windows adaptation, importer, settings, and rendering.
- `src/DishSoap.Configurator`: standalone settings editor.
- `src/Dishwasher`: shared components and rewritten shaders from the base project.
- `tools/windows`: extraction, local-copy preparation, building, upscaling, and verification tools.
- `docs`: Windows documentation and historical notes from the base project.

The private `GameSource` folder is generated locally from the copy supplied by the user. It and the `work`, `dist`, and `.local-tools` directories are excluded from Git. Publish only the project sources; do not add your game copy, imported data, or builds containing recompiled game logic.

## Credits and license

This work is based on **[Hdmaniak/DishSoap](https://github.com/Hdmaniak/DishSoap)**, originally an Android port. Windows support and its builder use components from that project. The preserved Android notes are historical references, not instructions for this Windows build.

The project's original code is covered by the [MIT license, within the scope described in LICENSE](LICENSE). Dependencies and third-party code retain their own licenses: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The Dishwasher: Dead Samurai and its assets belong to their respective rights holders. This project is not affiliated with Ska Studios, Microsoft, or Xbox.
