using System;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    // Use only around AJB-owned display work, never the original Hit/OnDamage/Die.
    internal static class DisplayErrors
    {
        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static void Reset() => reported.Clear();

        internal static void Report(string operation, Exception error)
        {
            if (!reported.Add(operation)) return;
            try { Main.Mod?.Logger.Error("AJB display failure (" + operation + "): " + error); }
            catch { } // Diagnostics must not become another exception in a display callback.
        }

        internal static void Run(string operation, Action display) => TryRun(operation, display);

        internal static bool TryRun(string operation, Action display)
        {
            try { display(); return true; }
            catch (Exception ex) { Report(operation, ex); return false; }
        }
    }
}
