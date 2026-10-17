Update existing installations and restore guitar audio.

## Added

- **Download game update** in the builder. Downloads the latest stable builder from GitHub, verifies its SHA-256 digest and rebuilds an existing installation in place.
- Existing imports, AI-upscaled textures, saves and player settings are preserved during updates. Replaced runtime files are backed up; failed replacements are rolled back.
- **Verify installed files** checks runtime checksums, original content, upscaled texture checksums and audio decoding. New builds also record checksums for derived content.

## Fixed

- Guitar solos prepare both audio stems before the countdown starts, preventing silent notes and keeping the guitar and backing track aligned.
- The builder displays verification progress and reports completion for builds and updates.

Extract `DishSoap-Builder-v0.3.0-win-x64.zip` and open `DishSoap.Builder.exe`. To update a compiled game, select **Download game update** and choose its folder.
