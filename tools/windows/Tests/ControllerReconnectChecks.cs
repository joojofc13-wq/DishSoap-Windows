using System.Runtime.InteropServices;
using Dishwasher;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

internal static class ControllerReconnectChecks
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
    public static void Run()
    {
        Check(Marshal.SizeOf<DesktopXInput.Pad>() == 12 && Marshal.SizeOf<DesktopXInput.State>() == 16 && Marshal.SizeOf<DesktopXInput.Rumble>() == 4,
            "XInput native structures match the Windows ABI");
        Check(DesktopControllerNative.FilterFlags(0x3220) == 0x20 && DesktopControllerNative.FilterFlags(0x3231) == 0x31,
            "safe mode removes joystick/haptic/controller initialization while retaining video, audio and timer flags");
        bool connected = false, writeFails = false; int writes = 0;
        uint Read(uint slot, out DesktopXInput.State state)
        {
            state = new DesktopXInput.State { Pad = new DesktopXInput.Pad { Buttons = 0x1001, LeftTrigger = 255, LeftX = short.MaxValue, RightY = short.MinValue } };
            return slot == 2 && connected ? 0u : 1167u;
        }
        uint Write(uint slot, ref DesktopXInput.Rumble state) { writes++;return writeFails ? 1167u : 0u; }
        var pads = new DesktopXInput(Read, Write); double now = 0;
        for (int i = 0; i < 1000; i++)
        {
            connected = true; pads.Poll(now); now += .01;
            if (pads.IsConnected(2)) throw new Exception("Unstable connection was accepted.");
            connected = false; pads.Poll(now); now += .01;
            if (pads.GetState(2).IsConnected || pads.SetVibration(PlayerIndex.Three, 1, 1)) throw new Exception("Disconnected input or rumble remained active.");
        }
        Check(writes == 0, "1000 rapid reconnect cycles do not expose unstable state or send rumble to a removed device");
        connected = true; pads.Poll(now); pads.Poll(now + .251);
        var state = pads.GetState(2);
        Check(state.IsConnected && state.IsButtonDown(Buttons.A) && state.DPad.Up == ButtonState.Pressed && state.Triggers.Left == 1 &&
            state.ThumbSticks.Left.X == 1 && state.ThumbSticks.Right.Y == -1, "stable reconnect restores buttons, D-pad, triggers and signed thumbstick axes");
        Check(pads.GetCapabilities(2).IsConnected && pads.GetCapabilities(2).HasLeftVibrationMotor && !pads.GetCapabilities(0).IsConnected,
            "capabilities follow the stable XInput slot without an SDL device");
        Check(pads.SetVibration(PlayerIndex.Three, .5f, .5f) && writes == 1, "rumble resumes after the connection settles");
        writeFails = true;
        Check(!pads.SetVibration(PlayerIndex.Three, .5f, .5f) && !pads.IsConnected(2) && !pads.GetState(2).IsConnected,
            "removal during a rumble write immediately clears the cached state");
        pads.Poll(now + .3); Check(!pads.IsConnected(2), "failed rumble write starts a fresh reconnect settling interval");
        connected = false; pads.Poll(now + .6);
        Check(!pads.GetState(2).IsButtonDown(Buttons.A) && !pads.GetCapabilities(2).IsConnected, "disconnect releases cached buttons and capabilities");
    }
    public static void Native(string root)
    {
        DesktopPaths.Initialize(Path.GetFullPath(root)); Log.Initialize();
        DesktopControllerNative.Configure(true);
        using var game = new Game();
        DesktopControllerNative.Verify();
        var pads = new DesktopXInput();
        for (int i = 0; i < 1000; i++) pads.Poll(i / 60.0);
        Console.WriteLine("Stable native XInput slots detected: " + Enumerable.Range(0, 4).Count(pads.IsConnected));
        Check(true, "real MonoGame window initializes with SDL controller subsystems disabled and 4000 native XInput reads succeed");
    }
}
