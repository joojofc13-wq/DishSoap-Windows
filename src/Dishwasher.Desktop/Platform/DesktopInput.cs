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
        public static GamePadState GetState(PlayerIndex index) => GetState(index, GamePadDeadZone.IndependentAxes);
        public static GamePadState GetState(PlayerIndex index, GamePadDeadZone deadZone)
        {
            if (!Focused) return default;
            var pad = GamePad.GetState(index, deadZone);
            return index == PlayerIndex.One ? Merge(pad, Keyboard.GetState(), Settings, MenuContext) : pad;
        }
        public static GamePadState Merge(GamePadState pad, KeyboardState keyboard, DesktopSettings settings, bool menu = true)
        {
            var down = new List<Buttons>();
            foreach (Buttons button in Enum.GetValues<Buttons>())
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
