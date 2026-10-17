Add a Quality of life tab to the configurator.

## Fixed

- Added opt-in **Safe controller reconnects (XInput)** for repeated USB disconnect/reconnect failures. The reported Windows crash was an `AccessViolationException` inside `SdlGamePlatform.SdlRunLoop`.
- Safe mode bypasses SDL joystick, game-controller and haptic initialization while retaining SDL video, keyboard and window events. Controller state and vibration use Windows XInput slot APIs, without storing SDL device handles.
- Disconnection clears cached input immediately. Reconnected devices must remain present for 250 ms before input or rumble is accepted; a failed vibration write invalidates the slot again.
- Standard SDL input remains the default. Safe mode supports Xbox-compatible controllers and DS4Windows configured for Xbox 360 output. Enable it in **Quality of life**, save and restart the game.

## Added

- **Quick room exits**, enabled by default. Point toward an open side exit to leave without holding the stick or key. Wall checks, locked exits, scripted events, fades and loading are preserved. Uncheck it to restore the original hold delay.
- **Pause when switching windows**, optional. Pauses normal gameplay on focus loss, waits through unpausable sequences and requires manual resuming.
- **Reduced camera shake**, optional. Reduces rendered camera displacement and rotation to 25% without changing simulation state or controller vibration.

Save and restart the game to apply QoL settings. Use **Download game update** in the builder to update an existing installation while keeping its saves, settings and prepared assets.
