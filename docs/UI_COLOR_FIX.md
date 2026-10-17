# Builder progress and texture color fixes

## Builder progress

The destination picker selects a parent directory. Every run creates a separate `Dishwasher-yyyyMMdd-HHmmss-id` child. The UI shows six stages, texture counts and live logs. Progress reaches 100% only after successful completion. These are stage percentages, not a time estimate.

The worker writes separate per-run progress and result JSON files. Success requires a positive result and an existing executable. It enables the buttons, reports completion and opens the executable's folder. Failure shows the error without presenting a partial build as complete. The game is never started automatically.

### Live-log sharing bug

Start-Process holds redirected stdout/stderr files open for writing. File.ReadAllText opens with FileShare.Read, which conflicts with that writer on Windows. The old UI swallowed the error, leaving both the log and progress display empty during a real build.

Read-BuildLog now opens with FileShare.ReadWrite | FileShare.Delete. Stage updates are also written directly to atomic JSON files, independently of stdout buffering. Initial preparation uses an animated bar; downloads show received MB when the transfer file is available; subsequent stages and texture counters update the bar.

The regression starts a real child process with redirected output, checks progress before it exits and verifies completion handling. `-UiCheck -Source <game.exe> -Dotnet <dotnet.exe> -Python <python.exe>` also performs actual game and configurator compilation, without importing assets or starting the game. An already open builder window must be reopened to load new script code.

## Purple and cyan artifacts

The bloom shader tests B > R and uses blue as an emission mask. Slight neural color drift on neutral surfaces activated that mask. Scene themes then tinted the bloom purple or cyan. A one-byte change could create a bright artifact.

The new compositor preserves the interpolated original B-R and G-R differences exactly. It applies a common bounded RGB offset derived from neural luminance, avoiding channel clipping. Alpha comes from the original, transparent pixels are zero and original black remains black. Authored blue and purple effects remain available.

PNG reads use Bitmap.Clone to avoid RGB rounding from premultiplication on translucent pixels. New packs use `hd2x-color-safe`, with the older `hd2x` pack available as a fallback. TextureTools can migrate a previous pack into a separate destination without repeating inference:

```text
TextureTools recolor-pack ORIGINAL_ROOT OLD_PACK NEW_PACK
```

## Evidence and remaining checks

The pixel audit covered 128 HD sheets and eight background overrides, including translucent pixels. The eight old background sheets had more than 6.6 million opaque pixels that falsely triggered the bloom mask. The corrected pack had zero new activations and zero lost activations relative to the interpolated original baseline.

A GPU regression using the actual bloom effect reproduced the one-byte blue drift artifact, verified the corrected color path and checked that original blue emission still worked. All 128 textures passed loading, dimension and hash checks.

These checks address the confirmed color-mask cause. Every stage composition and the large empty-area blocks shown in an earlier screenshot still need gameplay review. The tests do not prove every graphical issue in the port is fixed.

The configurator and builder now use English regardless of Windows region. The settings editor remains separate, with fixed 720p output and only Original or Upscaled texture choices.
