Discord Rich Presence support for the Windows port.

## Added

- Discord activity with the full game name, project icon, session timer and menu/game/paused status.
- Discord Rich Presence checkbox in the configurator. Enabled by default; changes apply on the next game launch.
- Background IPC connection with automatic reconnection and activity cleanup on exit. Discord being closed does not prevent playing.

## Requirements

The Discord desktop app must be open with activity sharing enabled. Players do not need a developer account, application ID, OAuth login or token.

Extract `DishSoap-Builder-v0.2.0-win-x64.zip` and open `DishSoap.Builder.exe`. Rebuild your owned copy to use the integration. Original game files and imported assets are not included.

[Build instructions](https://github.com/joojofc13-wq/DishSoap-Windows/blob/main/BUILDER_GUIDE.md)
