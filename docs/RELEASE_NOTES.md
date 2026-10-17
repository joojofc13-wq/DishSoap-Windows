# DishSoap Windows — Initial Windows Release

A Windows x64 port of **The Dishwasher: Dead Samurai**, with an own-copy builder and a separate Windows settings editor.

## What's included

- **Own-copy builder:** select your legitimate XBLA package, ZIP, or extracted folder and build the game locally. Each build gets its own subfolder inside your chosen destination.
- **Live build progress:** readable logs while the build is running, stage updates, texture counters, a completion notification, and automatic opening of the output folder.
- **Separate settings editor:** save your preferences in `DishSoap.Configurator.exe`, then open `Dishwasher.exe` to launch the game directly.
- **Original or Upscaled textures:** optional 2× upscaling and restored backgrounds, with original colors and transparency preserved to prevent false purple/cyan bloom.
- **Rendering fixes:** corrected Xbox DXT decoding and fixed 720p output downsampled from a higher-resolution internal canvas.
- **Display options:** windowed/fullscreen mode, adjustable FPS cap, VSync, FPS counter, and motion smoothing. Game simulation remains at 60 Hz.
- **Local play:** keyboard/controller support, a local profile, and saves with atomic writes and backup recovery.
- **Regional interface language:** Portuguese, English, or Spanish for the builder and settings editor, based on your Windows region.

## Getting started

1. Download and extract the source ZIP.
2. Run `Compilar-meu-jogo.cmd` and select your own game copy and destination folder.
3. Wait for all preparation and build stages to finish.
4. Optionally save your preferences with `DishSoap.Configurator.exe` in the generated folder.
5. Run `Dishwasher.exe` to play.

## Requirements

Windows x64, your own legitimate game copy, and internet access for the initial setup. Preparing upscaled textures requires a Vulkan-compatible GPU. Missing build tools are downloaded from their providers; no Android SDK or emulator is required.

## Important notes

This release provides the port's source and builder. **It does not include the original game, decompiled game logic, game assets, saves, or a ready-to-play game executable.**

The full campaign, every stage, co-op, and long-term stability have not been validated. Higher FPS caps do not guarantee that every scene will reach the selected frame rate. Some effects may still need visual verification during gameplay.

Based on [Hdmaniak/DishSoap](https://github.com/Hdmaniak/DishSoap). See `LICENSE` and `THIRD-PARTY-NOTICES.md` for license scope and third-party credits. Not affiliated with Ska Studios, Microsoft, or Xbox.
