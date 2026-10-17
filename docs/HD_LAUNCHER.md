# Desktop settings and rendering

`Dishwasher.exe` runs the game directly. `DishSoap.Configurator.exe` is a separate Windows settings editor with the window title `dishwasher`. Save configuration writes preferences and closes the editor. Open saves folder opens `%LOCALAPPDATA%\DishSoapWindows\Saves` in File Explorer.

Output is fixed at 1280 x 720. The options are Original or Upscaled textures, fullscreen, FPS limit, VSync, Show FPS and Smooth Motion. FPS caps are 60, 90, 120, 144, 165, 180, 240 and 360. The editor uses English on every region. Old resolution settings are migrated to 720p. Existing saved volume is retained, but no volume control or controls legend appears in the editor.

## Internal canvas

Render targets use a 1920 x 1080 canvas, then downsample to 720p. Original game coordinates stay at 1280 x 720. Desktop SpriteBatch adapters transform target coordinates, source rectangles, scale and origin/pivot values. Collision, maps, attack reach and movement speed keep their original coordinates.

## DXT corrections

The Xbox texture decoder already corrected RGB565 endpoints, but color-index words still needed 8-in-16 byte ordering. Incorrect ordering exchanged pairs of color rows in 4 x 4 blocks and produced zipper-like diagonals. Windows imports enable the corrected decoder before AI processing. BC3/DXT5 always uses a four-color palette, including when endpoint0 is less than or equal to endpoint1.

The shared decoder correction is opt-in for desktop use. Imports record `textureDecoder: xbox-dxt-8in16-v2`. The format reference is [Xenia texture_dump.cc](https://github.com/xenia-project/xenia/blob/master/src/xenia/gpu/texture_dump.cc).

## AI texture packs

The pack contains 128 sheets at 2x: 121 art sheets processed with Real-ESRGAN AnimeVideo-v3 and seven bicubic font sheets. A separate Real-ESRGAN x4plus pass restores eight background sheets at 4x before reducing them to 2x. These replace the corresponding standard HD sheets when Upscaled is selected.

RGB padding is dilated before inference. Alpha comes from the corrected source. The final compositor uses neural luminance detail while preserving source channel differences and zeros fully transparent pixels. PNG reading avoids the premultiplication rounding introduced by DrawImage. See [the color fix](UI_COLOR_FIX.md).

CJK font atlas dimensions, glyph bounds, cropping, kerning and metrics are adapted while measurements remain in logical coordinates. The engine and models come from the [author's release](https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.5.0) with a pinned SHA256. They are downloaded separately. A Vulkan-compatible GPU is required for preparation; no AI inference runs during a frame.

## Timing

A variable outer loop renders at the selected cap. An accumulator advances game logic at 1/60 second per update, with catch-up capped at 0.25 seconds and 15 steps. Optional interpolation draws character and camera positions between previous and current states, then restores the simulation state. Large discontinuities over 256 units skip interpolation. Interpolation can add up to one simulation step of visual latency.

Draw-related counters advance once per simulation tick. Visual random numbers use a separate stream so extra drawn frames do not consume the simulation sequence. Background rendering is capped at 30 FPS when the game loses focus.

The FPS overlay shows actual drawn frames and logic Hz. VSync can restrict presentation to the monitor refresh rate. Selecting 180 does not make a 60 Hz display show 180 distinct frames.

## Validation scope

Local tests cover fixed-step timing at multiple render rates, bounded catch-up, RNG separation, BC1/BC3 decoding, atlas crops and pivots, font drawing, 128 texture loads and the real bloom shader regression. Pixel audits cover alpha and channel relationships for all 128 HD sheets and eight scene overrides.

A short warmed-up synthetic benchmark reached 180 FPS rendering with 60 Hz logic on the local RX 6650 XT. This is a technical benchmark, not a measurement of the entire campaign. Full campaign, co-op and prolonged high-FPS gameplay remain unverified.
