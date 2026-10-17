using Microsoft.Xna.Framework;

namespace Dishwasher
{
    internal static class DesktopQol
    {
        internal static bool NeedsExitHold(float progress) => !DesktopInput.Settings.QuickRoomExits && progress < 1f;
        internal static float CameraShakeScale => DesktopInput.Settings.ReducedCameraShake ? .25f : 1f;
        internal static Vector2 CameraPosition(Vector2 anchor, Vector2 position) => anchor + (position - anchor) * CameraShakeScale;
        internal static bool ShouldPause(bool playing, bool focused, bool paused, bool blocked) =>
            DesktopInput.Settings.PauseOnFocusLoss && playing && !focused && !paused && !blocked;
    }
}
