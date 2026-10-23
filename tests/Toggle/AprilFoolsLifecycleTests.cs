using System;
using AccurateJudgementBar;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void AprilFoolsLifecycleTests()
    {
        var localTime = new DateTime(2026, 3, 31, 23, 59, 59);
        AprilFools.LocalNow = () => localTime;
        var entry = new UnityModManager.ModEntry();
        entry.Info.DisplayName = "Original name";
        Check(Mod.Load(entry), "Holiday integration loads through the actual Main");
        entry.OnToggle(entry, true);
        Check(entry.Info.DisplayName == "Original name", "Normal day preserves the original name");
        localTime = new DateTime(2026, 4, 1);
        entry.OnUpdate(entry, .016f);
        Check(entry.Info.DisplayName == "InaccurateJudgementBar (April Fool)", "UMM update detects local midnight");
        entry.OnToggle(entry, false);
        Check(entry.Info.DisplayName == "Original name", "Main disable restores the original name");
        entry.OnUpdate(entry, .016f);
        Check(entry.Info.DisplayName == "Original name", "Updates while disabled cannot rename the mod");
        entry.OnToggle(entry, true);
        Check(entry.Info.DisplayName == "InaccurateJudgementBar (April Fool)", "Re-enable restores the holiday name");
        localTime = new DateTime(2026, 4, 2);
        entry.OnUpdate(entry, .016f);
        Check(entry.Info.DisplayName == "Original name", "Main update restores the name after the holiday");
        entry.OnToggle(entry, false);

        localTime = new DateTime(2027, 4, 1);
        var nextEntry = new UnityModManager.ModEntry();
        nextEntry.Info.DisplayName = "Another original name";
        Check(Mod.Load(nextEntry), "A fresh holiday load succeeds");
        nextEntry.OnToggle(nextEntry, true);
        Check(nextEntry.Info.DisplayName == "InaccurateJudgementBar (April Fool)", "Starting on the holiday applies the name");
        nextEntry.OnToggle(nextEntry, false);
        Check(nextEntry.Info.DisplayName == "Another original name" && entry.Info.DisplayName == "Original name",
            "Each load restores its own original name");
        AprilFools.LocalNow = () => DateTime.Now;
    }
}
