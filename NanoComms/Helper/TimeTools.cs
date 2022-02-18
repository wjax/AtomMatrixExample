using System;
using System.Diagnostics;

namespace NanoComms.Helper
{
    public class TimeTools
    {
        // Ojo. Reset every 24 days. 0 --> Max --> Min

        private static readonly double microsPerCycle = 1000000.0 / Stopwatch.Frequency;
        private static readonly double millisPerCycle = 1000000.0 / Stopwatch.Frequency;

        public static int GetCoarseMillisNow()
        {
            return ((int)(Stopwatch.GetTimestamp() * millisPerCycle));
        }

        internal static long GetLocalMicrosTime(long offset = 0)
        {
            return ((long)(Stopwatch.GetTimestamp() * microsPerCycle) - offset);
        }
    }
}
