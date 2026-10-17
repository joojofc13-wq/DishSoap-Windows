using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Dishwasher
{
    internal static class DesktopControllerNative
    {
        internal const int ControllerSubsystems = 0x200 | 0x1000 | 0x2000;
        private static Delegate _nativeInit, _filteredInit;
        internal static int FilterFlags(int flags) => flags & ~ControllerSubsystems;
        internal static void Configure(bool safeReconnect)
        {
            if (!safeReconnect || !OperatingSystem.IsWindows() || _filteredInit != null) return;
            // MonoGame 3.8.5.1 initializes video + joystick + haptic + controller
            // together. Keep its video/event loop, but use Windows XInput for
            // pads so cable flapping never enters SDL's device-handle lifecycle.
            var sdl = typeof(Game).Assembly.GetType("Sdl", true);
            var field = sdl.GetField("SDL_Init", BindingFlags.Static | BindingFlags.Public);
            if (field == null || field.IsInitOnly || field.FieldType.GetMethod("Invoke")?.ReturnType != typeof(int))
                throw new NotSupportedException("Safe reconnect mode needs the supported MonoGame SDL initialization interface.");
            _nativeInit = field.GetValue(null) as Delegate ?? throw new NotSupportedException("SDL initialization delegate is missing.");
            _filteredInit = Delegate.CreateDelegate(field.FieldType, typeof(DesktopControllerNative).GetMethod(nameof(Initialize), BindingFlags.Static | BindingFlags.NonPublic));
            field.SetValue(null, _filteredInit);
            Log.Info("[input] Safe reconnect: Windows XInput; SDL controller subsystems disabled.");
        }
        private static int Initialize(int flags) => (int)_nativeInit.DynamicInvoke(FilterFlags(flags));
        [DllImport("SDL2.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint SDL_WasInit(uint flags);
        internal static void Verify()
        {
            if (_filteredInit != null && SDL_WasInit(ControllerSubsystems) != 0)
                throw new InvalidOperationException("SDL controller subsystems are active in safe reconnect mode. Restart the game.");
        }
    }
}
