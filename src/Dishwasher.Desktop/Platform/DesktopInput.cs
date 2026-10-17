using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Dishwasher
{
    public static class DesktopInput
    {
        public static DesktopSettings Settings { get; set; } = new DesktopSettings();
        public static bool Focused { get; set; } = true;
        public static bool MenuContext { get; set; } = true;
        private static readonly DesktopControllerRouting Routing=new();
        private static readonly bool[] Connected=new bool[4];
        private static readonly bool[] Active=new bool[4];
        private static readonly Buttons[] AllButtons=Enum.GetValues<Buttons>();
        private static readonly Buttons[] DigitalButtons={Buttons.A,Buttons.B,Buttons.X,Buttons.Y,Buttons.Start,Buttons.Back,Buttons.LeftShoulder,Buttons.RightShoulder,Buttons.LeftStick,Buttons.RightStick,Buttons.DPadUp,Buttons.DPadDown,Buttons.DPadLeft,Buttons.DPadRight};
        private static bool _routingReady;
        private static readonly DesktopXInput SafePads = new();
        public static void BeginFrame()
        {
            if (Settings.SafeControllerReconnect) SafePads.Poll((double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency);
            for(int i=0;i<4;i++)
            {
                var pad=Settings.SafeControllerReconnect ? SafePads.GetState(i) : GamePad.GetState((PlayerIndex)i);
                Connected[i]=pad.IsConnected;
                Active[i]=pad.IsConnected&&HasActivity(pad);
            }
            Routing.Update(Connected,Settings.LocalCoop,Active);
            _routingReady=true;
        }
        internal static bool HasActivity(GamePadState pad)
        {
            if(pad.ThumbSticks.Left.LengthSquared()>.09f||pad.ThumbSticks.Right.LengthSquared()>.09f||pad.Triggers.Left>.2f||pad.Triggers.Right>.2f) return true;
            foreach(Buttons button in DigitalButtons)
                if(pad.IsButtonDown(button)) return true;
            return false;
        }
        private static int Physical(PlayerIndex index)
        {
            if(!_routingReady) BeginFrame();
            return Routing.Resolve((int)index);
        }
        public static GamePadCapabilities GetCapabilities(PlayerIndex index)
        {
            int physical=Physical(index);
            return physical<0?default:Settings.SafeControllerReconnect?SafePads.GetCapabilities(physical):GamePad.GetCapabilities((PlayerIndex)physical);
        }
        public static bool SetVibration(PlayerIndex index,float leftMotor,float rightMotor)
        {
            int physical=Physical(index);
            return Settings.SafeControllerReconnect ? SendVibration(physical,leftMotor,rightMotor,SafePads.SetVibration) : SendVibration(physical,leftMotor,rightMotor,GamePad.SetVibration);
        }
        internal static bool SendVibration(int physical,float leftMotor,float rightMotor,Func<PlayerIndex,float,float,bool> send)
        {
            if(physical<0||physical>3) return false;
            // Send an explicit stop instead of leaving a previous rumble running.
            if(!Settings.ControllerVibration) { leftMotor=0;rightMotor=0; }
            return send((PlayerIndex)physical,leftMotor,rightMotor);
        }
        public static GamePadState GetState(PlayerIndex index) => GetState(index, GamePadDeadZone.IndependentAxes);
        public static GamePadState GetState(PlayerIndex index, GamePadDeadZone deadZone)
        {
            if (!Focused) return default;
            int physical=Physical(index);
            var pad = physical<0?default:Settings.SafeControllerReconnect?SafePads.GetState(physical,deadZone):GamePad.GetState((PlayerIndex)physical, deadZone);
            if(index!=PlayerIndex.One && physical<0) return default;
            return index == PlayerIndex.One ? Merge(pad, Keyboard.GetState(), Settings, MenuContext) : pad;
        }
        public static GamePadState Merge(GamePadState pad, KeyboardState keyboard, DesktopSettings settings, bool menu = true)
        {
            var down = new List<Buttons>();
            foreach (Buttons button in AllButtons)
                if (button != 0 && ((int)button & ((int)button - 1)) == 0 && pad.IsButtonDown(button)) down.Add(button);
            float x = 0, y = 0;
            if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) x--;
            if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) x++;
            if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) y++;
            if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) y--;
            if (keyboard.IsKeyDown(Keys.Enter) && !keyboard.IsKeyDown(Keys.LeftAlt) && !keyboard.IsKeyDown(Keys.RightAlt))
                down.Add(menu ? Buttons.A : Buttons.Start);
            if (keyboard.IsKeyDown(Keys.Escape)) down.Add(menu ? Buttons.B : Buttons.Start);
            foreach (var map in settings.Keys)
                if (Enum.TryParse<Buttons>(map.Key, out var button) && Enum.TryParse<Keys>(map.Value, true, out var key)
                    && keyboard.IsKeyDown(key)) down.Add(button);
            Vector2 stick = pad.ThumbSticks.Left;
            if (stick.LengthSquared() < 0.04f)
            {
                stick = new Vector2(x, y);
                if (stick.LengthSquared() > 1) stick.Normalize();
            }
            return new GamePadState(stick, pad.ThumbSticks.Right,
                Math.Max(pad.Triggers.Left, down.Contains(Buttons.LeftTrigger) ? 1 : 0),
                Math.Max(pad.Triggers.Right, down.Contains(Buttons.RightTrigger) ? 1 : 0), down.ToArray());
        }
    }
}
