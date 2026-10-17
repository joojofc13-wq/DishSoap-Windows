using System;
using System.Diagnostics;

namespace Dishwasher
{
    internal static class DesktopCheats
    {
        internal static DesktopCheatSettings Current { get; private set; } = new();
        internal static bool Active { get; private set; }
        private static long _nextRead;

        internal static void BeginFrame(bool playing, bool forceReload = false)
        {
            long now = Stopwatch.GetTimestamp();
            if (forceReload || now >= _nextRead)
            {
                Current = DesktopCheatSettings.Load();
                _nextRead = now + Stopwatch.Frequency;
            }
            Active = playing && Current.Enabled;
        }

        internal static int Damage(int amount, bool player, bool enemy, ref double remainder)
        {
            if (!Active || amount <= 0 || (!player && !enemy)) { remainder = 0; return amount; }
            double multiplier = player ? (Current.GodMode ? 0 : Current.PlayerDamageTaken) : Current.EnemyDamageTaken;
            if (multiplier == 0 || multiplier == 1) { remainder = 0; return multiplier == 0 ? 0 : amount; }
            // Accumulate fractional damage so repeated one-point hits honor values below 1x.
            double total = amount * multiplier + remainder;
            int result = (int)Math.Min(int.MaxValue, Math.Floor(total + 1e-9));
            remainder = total >= int.MaxValue ? 0 : Math.Max(0, total - result);
            return result;
        }

        internal static int EarnedOrbs(int current, int earned)
        {
            long amount = (long)earned * (Active && earned > 0 ? Current.OrbMultiplier : 1);
            return (int)Math.Clamp(current + amount, int.MinValue, int.MaxValue);
        }
    }
}
