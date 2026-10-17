# Windows port status

## Implemented

The Windows x64 host compiles managed game logic prepared from the owner's copy. The local game has been launched and played. The public repository contains the desktop adaptation, shared upstream components, rewritten shaders, tools and documentation. It contains no original game package, generated game logic or imported artwork/audio.

The builder prepares tools, extracts/decompiles/adapts local source, publishes the game and configurator, imports content and creates AI texture packs. It creates a child folder in the selected destination, shows live progress and opens the folder after reporting success.

The standalone configurator saves fixed-720p display preferences, Original/Upscaled textures, fullscreen, FPS limit, VSync, Show FPS and Smooth Motion. The game starts directly with those preferences. The Windows interfaces use English.

## Local evidence

- Actual managed assemblies were extracted from the provided package and the real game was compiled for Windows x64. Decompiled source can produce unused-field warnings.
- Import diagnostics converted the provided content and audio into a separate local installation. A previous content audit reported 458 manifest entries, 222 audio outputs and 216 cues; counts refer to that copy.
- Platform regressions exercise local storage, save recovery, settings, input mapping, timing and BC1/BC3 texture decoding.
- A synthetic 10-second motion check produced 600 simulation ticks and the same distance at 30, 60, 120, 144, 180 and 240 render FPS. Catch-up and RNG separation were checked separately.
- GPU diagnostics exercise rewritten effects, source rectangles, origins, alpha, font metrics and all 128 imported texture sheets.
- Pixel audits checked the 128 AI/bicubic sheets and eight scene overrides. The bloom regression verifies the original channel mask, corrected output and legitimate blue emission.
- The builder regression reads output from a running redirected child process and verifies progress plus success/failure states. An actual owned-copy compilation also reached 100% through the UI check without launching gameplay.
- A short synthetic RX 6650 XT benchmark measured 180 render FPS with 60 Hz game logic after warm-up. It is not a campaign benchmark.

See [rendering architecture](HD_LAUNCHER.md) and [progress/color regressions](UI_COLOR_FIX.md) for details.

## Not yet established

The high-FPS option is a cap, not a guarantee of constant throughput or equivalent visual timing for every effect.

The source-color fix addresses a reproduced cause of false purple/cyan bloom. Other stage-specific composition artifacts, including large empty-area blocks in an earlier screenshot, still require manual review.

## Private data

Runtime data is under `%LOCALAPPDATA%\DishSoapWindows`. Generated source, extraction intermediates and tool caches are ignored by Git. The builder release contains a compiled builder, its runtime and the source/scripts required to prepare a local copy; it does not contain a playable game.
