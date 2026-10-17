In-place updates, selectable game language and targeted lettering fixes.

## Added

- **Game language** selector in the configurator, with English selected by default, optional Windows automatic selection and the supported bundled translations. The configurator interface stays in English.
- **Download game update** in the builder. Downloads the latest stable builder from GitHub, verifies its SHA-256 digest and rebuilds an existing installation in place.
- Existing imports, AI-upscaled textures, saves and player settings are preserved during updates. Replaced runtime files are backed up; failed replacements are rolled back.
- **Verify installed files** checks runtime checksums, original content, upscaled texture checksums and audio decoding. New builds also record checksums for derived content.

## Fixed

- Guitar solos prepare both audio stems before the countdown starts, preventing silent notes and keeping the guitar and backing track aligned.
- Reviewed lettering overlays improve the HERBERT machine label and the CYBORG ASSIMILATION MOVEMENT neon sign. Changes stay inside the labels and preserve transparency and the shader bloom classification.
- Updates apply these overlays to existing upscaled packs without rerunning the AI upscale.
- Not all illegible text has been replaced. Other scene and story-panel lettering still needs to be checked and reviewed.
- The builder displays verification progress and reports completion for builds and updates.

Extract `DishSoap-Builder-v0.3.0-win-x64.zip` and open `DishSoap.Builder.exe`. To update a compiled game, select **Download game update** and choose its folder.
