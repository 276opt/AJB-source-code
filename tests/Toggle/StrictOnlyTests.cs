using AccurateJudgementBar;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void StrictOnlyTests(UnityModManager.ModEntry entry)
    {
        foreach (bool enabled in new[] { false, true })
        {
            entry.OnToggle(entry, enabled);
            foreach (bool parent in new[] { false, true })
            foreach (bool excludeOfficial in new[] { false, true })
            foreach (bool official in new[] { false, true })
            foreach (var original in new[] { DifficultyUIMode.DontShow, DifficultyUIMode.ShowLenientAndNormal,
                DifficultyUIMode.ShowAll, (DifficultyUIMode)99 })
            {
                Mod.settings.ShowAllDifficulties = parent;
                Mod.settings.ShowAllDifficultiesExcludeOfficial = excludeOfficial;
                ADOBase.isOfficialLevel = official;
                var result = original;
                StrictOnly.StrictOnlyPatch.Postfix(ref result);
                bool expand = enabled && parent && !(excludeOfficial && official) &&
                    (original == DifficultyUIMode.DontShow || original == DifficultyUIMode.ShowLenientAndNormal);
                Check(result == (expand ? DifficultyUIMode.ShowAll : original),
                    "Difficulty selection must preserve native choices on excluded official levels and when disabled");
                Check(Mod.settings.ShowAllDifficulties == parent &&
                    Mod.settings.ShowAllDifficultiesExcludeOfficial == excludeOfficial,
                    "Changing level type must not overwrite either saved preference");
            }
        }

        Mod.settings.ShowAllDifficulties = Mod.settings.ShowAllDifficultiesExcludeOfficial = true;
        foreach (bool official in new[] { false, true, false })
        {
            ADOBase.isOfficialLevel = official;
            var result = DifficultyUIMode.DontShow;
            StrictOnly.StrictOnlyPatch.Postfix(ref result);
            Check(result == (official ? DifficultyUIMode.DontShow : DifficultyUIMode.ShowAll),
                "Custom -> official -> custom must re-evaluate the current level without cached state");
        }
        ADOBase.isOfficialLevel = false;
        entry.OnToggle(entry, false);
    }
}
