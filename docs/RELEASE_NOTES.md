Fix unintended second-player input and add configurable cheats with save backups.

## Fixed

- Route the primary physical controller to player one even when Windows assigns it a different device slot after reconnecting.
- An active controller can claim player one ahead of an idle virtual device, then keeps its ownership until disconnecting.
- Keyboard and primary controller input stay with player one. Additional controller slots cannot join in the default single-player configuration.
- Controller capabilities and vibration use the same player mapping as button and stick input.

## Added

- **Local co-op (additional controllers)** in the configurator. Disabled by default; enable it before launching the game to allow additional controllers to join.
- A **Cheats** tab, disabled by default: health damage protection, editable ammunition and magic skull locks, infinite movement energy, and multipliers for player damage taken, enemy damage taken, movement speed and newly earned orbs.
- Cheat changes apply to a running game within one second after saving. Resource locks restore underlying counters when disabled; earned orbs and progress remain saved.
- Manual save backups and automatic backups before saving changed cheats while enabled. **Cheats can permanently change your save. Back up saves before making any changes.**

Download and extract `DishSoap-Builder-v0.4.0-win-x64.zip`, open `DishSoap.Builder.exe` and select **Download game update** to update an existing compiled installation.
