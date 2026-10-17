using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Dishwasher
{
    internal sealed class DesktopXInput
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Pad { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LeftX, LeftY, RightX, RightY; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct State { public uint Packet; public Pad Pad; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rumble { public ushort Left, Right; }
        internal delegate uint Read(uint slot, out State state);
        internal delegate uint Write(uint slot, ref Rumble rumble);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState", ExactSpelling = true)]
        private static extern uint NativeRead(uint slot, out State state);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState", ExactSpelling = true)]
        private static extern uint NativeWrite(uint slot, ref Rumble rumble);
        private readonly Read _read;
        private readonly Write _write;
        private readonly State[] _states = new State[4];
        private readonly double[] _since = { double.NaN, double.NaN, double.NaN, double.NaN };
        private readonly bool[] _connected = new bool[4];
        private static readonly GamePadCapabilities Capabilities = MakeCapabilities();
        internal DesktopXInput(Read read = null, Write write = null) { _read = read ?? NativeRead; _write = write ?? NativeWrite; }
        internal void Poll(double now)
        {
            for (int i = 0; i < 4; i++)
            {
                if (_read((uint)i, out var state) != 0) { Disconnect(i); continue; }
                if (double.IsNaN(_since[i])) _since[i] = now;
                _connected[i] = now - _since[i] >= .25;
                _states[i] = _connected[i] ? state : default;
            }
        }
        private void Disconnect(int slot) { _since[slot] = double.NaN; _connected[slot] = false; _states[slot] = default; }
        internal bool IsConnected(int slot) => slot >= 0 && slot < 4 && _connected[slot];
        internal GamePadCapabilities GetCapabilities(int slot) => IsConnected(slot) ? Capabilities : default;
        private static GamePadCapabilities MakeCapabilities()
        {
            object result = new GamePadCapabilities();
            // MonoGame exposes internal setters on this value type. Populate
            // one immutable template, instead of consulting an SDL device.
            foreach (var property in typeof(GamePadCapabilities).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (property.PropertyType == typeof(bool) && property.CanWrite)
                    property.SetValue(result, property.Name != "HasBigButton");
            typeof(GamePadCapabilities).GetProperty("DisplayName").SetValue(result, "Windows XInput controller");
            typeof(GamePadCapabilities).GetProperty("Identifier").SetValue(result, "windows-xinput");
            typeof(GamePadCapabilities).GetProperty("GamePadType").SetValue(result, GamePadType.GamePad);
            return (GamePadCapabilities)result;
        }
        internal GamePadState GetState(int slot, GamePadDeadZone mode = GamePadDeadZone.IndependentAxes)
        {
            if (!IsConnected(slot)) return default;
            var p = _states[slot].Pad;
            Vector2 left = DeadZone(new Vector2(Axis(p.LeftX), Axis(p.LeftY)), .24f, mode);
            Vector2 right = DeadZone(new Vector2(Axis(p.RightX), Axis(p.RightY)), .265f, mode);
            Buttons buttons = (Buttons)(p.Buttons & 0xF3FF);
            // Virtual directional buttons use raw axes, as in MonoGame's SDL
            // path, rather than applying a second threshold to filtered axes.
            if (Axis(p.LeftX) < -.24f) buttons |= Buttons.LeftThumbstickLeft;
            if (Axis(p.LeftX) > .24f) buttons |= Buttons.LeftThumbstickRight;
            if (Axis(p.LeftY) < -.24f) buttons |= Buttons.LeftThumbstickDown;
            if (Axis(p.LeftY) > .24f) buttons |= Buttons.LeftThumbstickUp;
            if (Axis(p.RightX) < -.265f) buttons |= Buttons.RightThumbstickLeft;
            if (Axis(p.RightX) > .265f) buttons |= Buttons.RightThumbstickRight;
            if (Axis(p.RightY) < -.265f) buttons |= Buttons.RightThumbstickDown;
            if (Axis(p.RightY) > .265f) buttons |= Buttons.RightThumbstickUp;
            if (p.LeftTrigger > 0) buttons |= Buttons.LeftTrigger;
            if (p.RightTrigger > 0) buttons |= Buttons.RightTrigger;
            return new GamePadState(new GamePadThumbSticks(left, right), new GamePadTriggers(p.LeftTrigger / 255f, p.RightTrigger / 255f),
                new GamePadButtons(buttons), new GamePadDPad((p.Buttons & 1) != 0 ? ButtonState.Pressed : ButtonState.Released,
                    (p.Buttons & 2) != 0 ? ButtonState.Pressed : ButtonState.Released, (p.Buttons & 4) != 0 ? ButtonState.Pressed : ButtonState.Released,
                    (p.Buttons & 8) != 0 ? ButtonState.Pressed : ButtonState.Released));
        }
        private static float Axis(short value) => value < 0 ? value / 32768f : value / 32767f;
        private static Vector2 DeadZone(Vector2 value, float threshold, GamePadDeadZone mode)
        {
            if (mode == GamePadDeadZone.None) return value;
            if (mode == GamePadDeadZone.Circular)
            {
                float length = value.Length();
                return length <= threshold ? Vector2.Zero : value / length * Math.Min(1, (length - threshold) / (1 - threshold));
            }
            float AxisZone(float v) => Math.Abs(v) <= threshold ? 0 : (v - Math.Sign(v) * threshold) / (1 - threshold);
            return new Vector2(AxisZone(value.X), AxisZone(value.Y));
        }
        internal bool SetVibration(PlayerIndex slot, float left, float right)
        {
            int index = (int)slot;
            if (!IsConnected(index)) return false;
            var rumble = new Rumble { Left = (ushort)(Math.Clamp(left, 0, 1) * 65535), Right = (ushort)(Math.Clamp(right, 0, 1) * 65535) };
            if (_write((uint)index, ref rumble) == 0) return true;
            Disconnect(index); return false;
        }
    }
}
