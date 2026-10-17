using System;
using System.Diagnostics;
using System.Threading;

namespace Dishwasher
{
    public sealed class FixedSimulationClock
    {
        public const double StepSeconds = 1.0 / 60;
        private double _accumulator;
        public long Ticks { get; private set; }
        public double Alpha => Math.Clamp(_accumulator / StepSeconds, 0, 1);
        public void Advance(double elapsedSeconds, Action tick)
        {
            _accumulator += Math.Clamp(elapsedSeconds, 0, 0.25);
            while (_accumulator + 1e-10 >= StepSeconds)
            {
                _accumulator -= StepSeconds;
                if (_accumulator < 0) _accumulator = 0;
                Ticks++; tick();
            }
        }
    }
    public sealed class DesktopFramePacer
    {
        private long _next;
        public void Wait(int fps)
        {
            long now = Stopwatch.GetTimestamp();
            if (_next == 0) _next = now;
            long remaining = _next - now;
            if (remaining > 0)
            {
                double ms = remaining * 1000.0 / Stopwatch.Frequency;
                if (ms >= 2) Thread.Sleep((int)ms - 1);
                while (Stopwatch.GetTimestamp() < _next) Thread.SpinWait(20);
            }
            long period = (long)Math.Round((double)Stopwatch.Frequency / fps);
            _next += period;
            now = Stopwatch.GetTimestamp();
            if (_next < now - period) _next = now + period;
        }
    }
    public static class DesktopRenderContext
    {
        public static bool Drawing { get; private set; }
        public static bool NewSimulationFrame { get; private set; } = true;
        private static long _lastTick = -1;
        private static Random _random = new(1);
        public static Random RandomFor(Random simulationRandom) => Drawing ? _random : simulationRandom;
        public static void Begin(long tick)
        {
            Drawing = true; NewSimulationFrame = _lastTick != tick; _lastTick = tick;
            _random = new Random(unchecked((int)(tick * 1664525 + 1013904223)));
        }
        public static void End() { Drawing = false; NewSimulationFrame = true; }
    }
}
