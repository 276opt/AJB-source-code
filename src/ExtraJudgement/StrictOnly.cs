
using HarmonyLib;

namespace AccurateJudgementBar
{
    public static class StrictOnly
    {
        [HarmonyPatch(typeof(scrMisc), "DetermineDifficultyUIMode")]
        public static class StrictOnlyPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ref DifficultyUIMode __result)
            {
                if (Main.is_mod_enable && Main.settings.ShowAllDifficulties &&
                    !(Main.settings.ShowAllDifficultiesExcludeOfficial && ADOBase.isOfficialLevel)) {
                    if (
                        __result == DifficultyUIMode.DontShow ||
                        __result == DifficultyUIMode.ShowLenientAndNormal
                    ) {
                        __result = DifficultyUIMode.ShowAll;
                    }
                }
            }
        }
    }
}
