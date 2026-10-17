using Dishwasher;
using Microsoft.Xna.Framework;

internal static class QolChecks
{
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
    public static void Run()
    {
        var original = DesktopInput.Settings;
        try
        {
            DesktopInput.Settings = new DesktopSettings();
            Check(!DesktopQol.NeedsExitHold(0), "quick room exits are enabled by default");
            DesktopInput.Settings.QuickRoomExits = false;
            Check(DesktopQol.NeedsExitHold(.9f) && !DesktopQol.NeedsExitHold(1), "original exit hold threshold is preserved when disabled");
            Check(DesktopQol.CameraPosition(new Vector2(100), new Vector2(120, 80)) == new Vector2(120, 80), "normal camera shake remains unchanged by default");
            DesktopInput.Settings.ReducedCameraShake = true;
            Check(DesktopQol.CameraPosition(new Vector2(100), new Vector2(120, 80)) == new Vector2(105, 95) && DesktopQol.CameraShakeScale == .25f,
                "reduced camera position and rotation use one quarter of the original amplitude");
            Check(!DesktopQol.ShouldPause(true, false, false, false), "automatic focus pause is opt-in");
            DesktopInput.Settings.PauseOnFocusLoss = true;
            Check(DesktopQol.ShouldPause(true, false, false, false), "unfocused normal gameplay requests a pause");
            Check(!DesktopQol.ShouldPause(false, false, false, false) && !DesktopQol.ShouldPause(true, true, false, false) &&
                !DesktopQol.ShouldPause(true, false, true, false) && !DesktopQol.ShouldPause(true, false, false, true),
                "focus pause does not toggle menus, focused play, an existing pause or blocked scenes");
        }
        finally { DesktopInput.Settings = original; }
    }
}
