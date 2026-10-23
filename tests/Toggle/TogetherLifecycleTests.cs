using System;
using AccurateJudgementBar;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

namespace Together
{
    internal static class Main
    {
        internal static bool IsInTogether;
        internal static UnityModManager.ModEntry? ModEntry;
    }
}

static partial class Program
{
    private static void TogetherLifecycleTests()
    {
        var entry = new UnityModManager.ModEntry();
        Check(Mod.Load(entry), "Together integration loads through the actual Main");
        var together = new UnityModManager.ModEntry { Active = true };
        UnityModManager.TogetherEntry = Together.Main.ModEntry = together;
        Together.Main.IsInTogether = false;
        Mod.settings.NoMissOnly = true;
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.None;
        entry.OnToggle(entry, true);
        entry.OnUpdate(entry, .016f);
        Check(Mod.settings.NoMissOnly && Mod.settings.saveCalls == 0,
            "Merely enabling Together must preserve single-player preferences");
        Together.Main.IsInTogether = true;
        entry.OnUpdate(entry, .016f);
        Check(!Mod.settings.NoMissOnly && Mod.settings.saveCalls == 1,
            "Entering Together clears and saves AJB restrictions through Main update");
        int resets = MarginLimitMenu.togetherResets;
        entry.OnUpdate(entry, .016f);
        Check(Mod.settings.saveCalls == 1 && MarginLimitMenu.togetherResets == resets,
            "Normal Together frames neither save again nor scan menus again");

        entry.OnToggle(entry, false);
        Mod.settings.XPerfectOnly = true;
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.PurePerfectOnly;
        entry.OnUpdate(entry, .016f);
        Check(Mod.settings.XPerfectOnly && GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly,
            "Disabled AJB does not rewrite any limits");
        entry.OnToggle(entry, true);
        Check(!Mod.settings.XPerfectOnly && GCS.hitMarginLimit == HitMarginLimit.None &&
            Persistence.hitMarginLimit == HitMarginLimit.None && Mod.settings.saveCalls == 2,
            "Late AJB enable resets persisted and live native limits before display setup");

        Together.Main.IsInTogether = false;
        entry.OnUpdate(entry, .016f);
        Check(!Mod.settings.XPerfectOnly && !Mod.settings.NoMissOnly &&
            GCS.hitMarginLimit == HitMarginLimit.None && Mod.settings.saveCalls == 2,
            "Leaving Together does not restore restrictions");
        Mod.settings.NoMissOnly = true;
        Mod.SettingsUpdate();
        Check(Mod.settings.NoMissOnly, "Single-player choices work after leaving Together");

        Together.Main.IsInTogether = true;
        together.Active = false;
        entry.OnUpdate(entry, .016f);
        Check(Mod.settings.NoMissOnly && !TogetherCompatibility.InSession,
            "Together's stale session flag after disable cannot clear single-player choices");
        entry.OnToggle(entry, false);
        UnityModManager.TogetherEntry = Together.Main.ModEntry = null;
        Together.Main.IsInTogether = false;
    }
}
