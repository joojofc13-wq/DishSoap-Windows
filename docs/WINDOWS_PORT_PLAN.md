# Windows port development plan

This file records the initial plan. For the implemented workflow, read [WINDOWS_BUILD.md](WINDOWS_BUILD.md); for test coverage and limits, read [WINDOWS_PORT_STATUS.md](WINDOWS_PORT_STATUS.md).

The port uses the managed game logic extracted from the owner's copy, with a .NET 8 and MonoGame DesktopGL host. The Android project provides shared format readers, content import infrastructure, XACT compatibility and rewritten shaders.

The desktop work covers local assembly extraction and source adaptation, Windows input/audio/storage, an own-copy build UI, standalone configuration, rendering/timing changes and optional AI texture preparation. Generated game source and original resources stay outside public source and release packages.

Validation proceeds through platform tests, importer/source preparation checks, compilation of the actual owner-provided game, then GPU texture/effect diagnostics. Manual gameplay remains necessary for campaign progression, co-op and long-term stability. A working build and isolated shader tests do not establish those outcomes.
