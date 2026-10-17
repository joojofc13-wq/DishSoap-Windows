Initial Windows x64 release for building The Dishwasher: Dead Samurai from your own legitimate copy.

Extract `DishSoap-Builder-v0.1.0-win-x64.zip` and open `DishSoap.Builder.exe`.

## Features

- Compiled, self-contained .NET 8 builder with automatic tool setup, live build logs, stage progress and completion notification. Creates a separate output folder and opens it when finished.
- MonoGame DesktopGL 3.8.5.1 runtime with keyboard/controller input, local profiles and atomic save writes with backup recovery.
- Original or AI-upscaled textures using Real-ESRGAN: 2x artwork, bicubic font scaling and an additional background restoration pass. Processing runs locally before gameplay.
- Fixed 1280 x 720 output downsampled from a 1920 x 1080 internal canvas.
- Fixed 60 Hz game simulation with independent rendering, optional motion interpolation and FPS caps from 60 to 360, including 180.
- Separate Windows configurator for texture quality, fullscreen, FPS limit, VSync and the FPS overlay. English interface and filenames.

## Fixes

- Corrected Xbox DXT color-index byte ordering and BC3 palette handling, addressing broken diagonals in decoded textures.
- Preserved source alpha and color-channel relationships during upscaling to prevent false purple/cyan bloom while retaining original emission masks.
- Fixed live-log file sharing and completion tracking so build progress updates while the worker is running and reaches 100% only on success.

## Requirements

Windows x64, a legitimate game copy and internet access for initial tool downloads. AI texture preparation requires a Vulkan-compatible GPU. The builder's .NET runtime is included; the SDK and conversion tools are downloaded when needed.

Original game files, generated game source, imported assets and AI model weights are not included.

## Known limitations

Full campaign completion, co-op, all stage compositions and prolonged high-FPS gameplay have not been validated. FPS settings are caps, not performance guarantees; VSync and monitor refresh may limit presentation. Some scene-specific graphics artifacts still require gameplay review.

[Build instructions](https://github.com/joojofc13-wq/DishSoap-Windows/blob/main/BUILDER_GUIDE.md) | [Technical details](https://github.com/joojofc13-wq/DishSoap-Windows/blob/main/docs/HD_LAUNCHER.md) | [Credits and licenses](https://github.com/joojofc13-wq/DishSoap-Windows#authorship-and-reused-code)
