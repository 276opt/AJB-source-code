using HarmonyLib;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    internal static class XPerfectOnly
    {
        // Competitive mode is the gameplay prerequisite; text/tick presets only affect display.
        internal static bool Active => MarginLimitState.IsSelected(MarginLimitState.Mode.XPerfectOnly) &&
            Persistence.enableCompetitiveMode;

        // Keep the native judgement and timing windows. Reject the extra perfect grades
        // at the same gate used by SwitchChosen, so they reach the game's damage path.
        [HarmonyPatch(typeof(HitMarginHelper), nameof(HitMarginHelper.IsCounted))]
        internal static class CountedPatch
        {
            [HarmonyPostfix]
            internal static void Postfix(HitMargin hitMargin, HitMarginLimit? hitMarginLimit, ref bool __result)
            {
                if (Active && (hitMarginLimit ?? GCS.hitMarginLimit) == HitMarginLimit.PurePerfectOnly)
                    __result &= hitMargin == HitMargin.XPerfect;
            }
        }

        [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.OnDamage))]
        internal static class DeathPatch
        {
            [HarmonyPostfix]
            internal static void Postfix(scrPlayer __instance, HitMargin hitMargin, ref bool __result)
            {
                // Vanilla PurePerfectOnly already handles every non-pure judgement.
                // Only fill its PerfectMinus/PerfectPlus gap, avoiding duplicate Die calls.
                if (!Active || (hitMargin != HitMargin.PerfectMinus && hitMargin != HitMargin.PerfectPlus))
                    return;
                var controller = scrController.instance;
                if (controller == null || !controller.gameworld || controller.noFail ||
                    __instance.currFloor == null || __instance.currFloor.freeroam || !__instance.alive)
                    return;

                string message = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object>
                {
                    { "judgement", hitMargin.ToLocalized(null) }
                });
                __instance.Die(false, false, message, false);
                __result |= !__instance.alive;
            }
        }
    }
}
