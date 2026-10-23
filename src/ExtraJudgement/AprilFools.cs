using System;
using UnityModManagerNet;

namespace AccurateJudgementBar
{
    internal static class AprilFools
    {
        // Cosmetic only: the local holiday changes names, never modes or gameplay.
        internal static Func<DateTime> LocalNow = () => DateTime.Now;
        internal static bool IsAprilFirst(DateTime localTime) => localTime.Month == 4 && localTime.Day == 1;
        internal static bool Available => IsAprilFirst(LocalNow());
        internal static string SettingsTitle(string normalTitle) => Main.is_mod_enable && Available
            ? "Inaccurate Judgement Bar by opt276"
            : normalTitle;

        private static UnityModManager.ModEntry? entry;
        private static string originalName = "";

        internal static void Initialize(UnityModManager.ModEntry modEntry)
        {
            entry = modEntry;
            originalName = modEntry.Info.DisplayName;
        }

        internal static void Update()
        {
            bool available = Available;
            if (entry != null)
                entry.Info.DisplayName = Main.is_mod_enable && available ? "InaccurateJudgementBar (April Fool)" : originalName;
        }

        internal static void Disable()
        {
            if (entry != null) entry.Info.DisplayName = originalName;
        }
    }
}
