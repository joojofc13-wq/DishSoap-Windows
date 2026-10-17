using System.Collections.Concurrent;
using System.Threading;

namespace Dishwasher
{
    public static class DesktopThreads
    {
        private static readonly ConcurrentDictionary<Thread, byte> Cancelled = new ConcurrentDictionary<Thread, byte>();
        public static void RequestStop(Thread thread)
        {
            if (thread != null) Cancelled.TryAdd(thread, 0);
        }
        public static bool StopRequested => Cancelled.ContainsKey(Thread.CurrentThread);
    }
}
