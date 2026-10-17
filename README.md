# DishSoap Windows

The assets Upscaled by AI option is enabled by default. Keeping it selected is recommended.

Windows x64 port of The Dishwasher: Dead Samurai. Build the game from your own copy, then run it with original or upscaled textures.

## Get started

1. Download `DishSoap-Builder-v0.4.0-win-x64.zip` from [Releases](https://github.com/joojofc13-wq/DishSoap-Windows/releases).
2. Extract the entire ZIP to a writable folder.
3. Open `DishSoap.Builder.exe`.
4. Select your legitimate game copy and a destination folder. The builder creates a new game subfolder inside it.
5. Wait for the six build stages to finish. The builder shows a live log, reports completion and opens the output folder.

The output includes `Dishwasher.exe` and `DishSoap.Configurator.exe`. Open the configurator to save your preferences. Open `Dishwasher.exe` to play with those settings.

If you downloaded the repository source instead of the release, open `Build-My-Game.bat`. It starts the same builder through Windows PowerShell.

You need Windows x64, internet access for initial tool setup, your own game copy and a Vulkan-compatible GPU for AI texture preparation. The release includes the builder's .NET runtime. The .NET SDK and other build tools are downloaded separately when needed. No Android SDK or administrator installation is required.

[Builder guide](BUILDER_GUIDE.md) | [Manual build](docs/WINDOWS_BUILD.md) | [Release details](docs/RELEASE_NOTES.md)

## Updating your game

The builder can download the latest stable update and rebuild an existing game folder while preserving imported assets, AI-upscaled textures, saves and settings. It keeps backups of replaced runtime files. Use **Download game update** or **Verify installed files**; see the [builder guide](BUILDER_GUIDE.md#update-an-existing-installation).

## Features

- Keyboard and the primary active controller share player one. Enable **Local co-op (additional controllers)** in the configurator to allow other controllers to join.

- Select the game language in the configurator. English is the default; Automatic optionally follows Windows. The bundled Portuguese translation is European Portuguese.

- Fixed 1280 x 720 output, downsampled from a 1920 x 1080 internal canvas.
- Original or Upscaled textures, including a separate pass for backgrounds.
- Windowed or fullscreen display, VSync and an optional FPS counter.
- FPS limits of 60, 90, 120, 144, 165, 180, 240 and 360.
- Original game simulation at 60 Hz, with optional visual motion interpolation.
- Keyboard and controller input, a local player profile and save backup recovery.
- A separate Windows settings editor. The builder, settings editor and Windows tools use English regardless of region.
- Optional Discord Rich Presence with the game name, icon, elapsed session time and menu/game/paused state.
- A **Cheats** tab with damage protection, resource locks and numeric modifiers for damage, movement and earned orbs. Disabled by default, with manual and automatic save backups.

Settings are stored in `%LOCALAPPDATA%\DishSoapWindows\Config\desktop.json`. Saves are stored in `%LOCALAPPDATA%\DishSoapWindows\Saves`.

## Cheats

**Cheats can permanently change your save. Back up your saves before making any changes.** Use **Back up saves** in the configurator's **Cheats** tab. Backups are stored in `%LOCALAPPDATA%\DishSoapWindows\SaveBackups`. Changed settings with cheats enabled also create a backup automatically before they are saved; a failed backup prevents applying the changes.

Enable the master switch, select cheats and click **Save settings**. The running game reads `Config\cheats.json` within one second. Disable the switch to return to normal behavior, or use **Reset cheats** and save. Other configurator settings still require restarting the game.

Available options: infinite health through damage protection; ammunition locked to 1–999; magic skulls locked to 1–9; infinite movement energy; player damage taken at 0–10x; enemy damage taken at 0.1–20x; movement speed at 0.5–3x; and newly earned orbs at 1–100x. Resource locks temporarily override the player's counters and restore their underlying counts when disabled. Existing orbs and shop prices are not multiplied. Earned orbs, purchases and campaign progress remain in your save after cheats are disabled.

Damage protection does not revive dead characters or bypass scripted events, hit reactions, required boss phases or story ability unlocks. Enemy damage scaling also affects environmental health loss. A backup can be restored by closing the game and copying its files into the Saves folder.

## Discord activity

Discord Rich Presence is enabled by default. Uncheck Discord Rich Presence in the configurator to disable it, then restart the game to apply the change. It requires the Discord desktop app and Discord activity sharing to be enabled. Players do not need to register a developer application, sign in through the game or provide tokens.

The integration uses Discord's local IPC interface. It reconnects in the background, keeps the session timer across reconnects and clears activity when the game exits. It sends the game name, menu/game/paused state, session timestamp and project icon. It does not transmit save contents or account credentials. The game still works when Discord is closed.

## Texture preparation

Textures are prepared locally from the assets in your own copy. No upscaled game artwork is included in this repository or the builder download.

The current pipeline prepares 128 texture sheets at 2x size: 121 art sheets use the AnimeVideo-v3 model, while seven font sheets use bicubic scaling. Eight background sheets receive an additional Real-ESRGAN x4plus pass at 4x, then are reduced to 2x. Original alpha and channel relationships are preserved to keep transparency and bloom masks intact. Processing happens before playing, with no neural inference during gameplay.

AI can change small details. Choose Original in the configurator if you prefer the original texture treatment. Upscaled textures do not turn the fixed 720p output into native 1080p output.

[Rendering details](docs/HD_LAUNCHER.md) | [Color and progress fixes](docs/UI_COLOR_FIX.md)

### Lettering overlays

Some small signs have replacement text drawn over the upscaled texture because the original assets are too low-resolution to remain readable, and AI upscaling alone deforms the letters. The reviewed overlays currently cover the HERBERT machine label and the CYBORG ASSIMILATION MOVEMENT neon sign in a story panel. They preserve the wording, placement and surrounding artwork. This is selective relettering, not recovered detail from the original pixels, and does not replace every background inscription. Original mode keeps the unmodified imported textures.

The builder applies these overlays locally to the owner's imported images. No original or modified game textures are included in the release ZIP.

## Authorship and reused code

This Windows port is developed by [joojofc13-wq](https://github.com/joojofc13-wq). Most of the Windows-specific implementation was developed for this project: the own-copy builder, configurator, desktop host, input and audio integration, save handling, rendering and timing adapters, and AI texture workflow.

The port also uses code from [Hdmaniak/DishSoap](https://github.com/Hdmaniak/DishSoap), the original Android port. The reused or adapted parts include:

- STFS/LIVE package reading, import sources, import manifest and content import infrastructure.
- XNB texture and font readers and PNG output. The Windows port adds the DXT index-order and BC3 palette fixes.
- XWB/XSB audio bank and cue readers, the audio importer and `XactShim`.
- `DishwasherContentManager`, adapted here for desktop texture packs.
- XNA compatibility shims for gamer services, networking and effects.
- Rewritten shaders and the reference extraction, audio and shader tools. Windows also adds a redhaze variant and scene-edge filtering.

The LZX decoder is separately credited to Xenia Manager. The game logic is generated from the user's game copy; it is not original port code and is not published here. Licenses and notices from reused components remain in the repository.

## Status

The game has been built and played locally. The full campaign, all stages, co-op and long sessions have not been validated. Selecting 180 FPS sets a cap; performance depends on the scene, hardware, VSync and monitor. The local graphics tests cover specific shader and texture regressions, not every frame of the campaign.

[Validation and limitations](docs/WINDOWS_PORT_STATUS.md)

## Source layout

| Path | Contents |
| --- | --- |
| `src/DishSoap.Builder` | Compiled Windows entry point for the builder |
| `src/Dishwasher.Desktop` | Desktop runtime, input, settings and rendering |
| `src/DishSoap.Configurator` | Standalone settings editor |
| `src/Dishwasher` | Shared components and shaders from the Android project |
| `tools/windows` | Preparation, compilation, AI upscaling and verification |
| `docs` | Windows guides and historical upstream notes |

`GameSource`, `work`, `dist` and `.local-tools` are local generated directories excluded from Git. The repository and builder release contain no original game package, decompiled game logic, imported artwork, audio, maps, saves or ready-to-play game executable.

## License

Project code is covered by the [MIT license within the scope stated in LICENSE](LICENSE). Dependencies and reused code retain their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The Dishwasher: Dead Samurai belongs to its respective rights holders. This project is not affiliated with Ska Studios, Microsoft or Xbox.
