using HarmonyLib;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    // Complete the game's PerfectsOnly damage path without changing its hit windows.
    [HarmonyPatch(typeof(scrPlayer), "OnDamage")]
    internal static class PerfectsOnlyDeath
    {
        [HarmonyPostfix]
        internal static void Postfix(scrPlayer __instance, HitMargin hitMargin, ref bool __result)
        {
            if (!MarginLimitState.IsSelected(MarginLimitState.Mode.PerfectsOnly) ||
                !Main.settings.PerfectsOnlyInstantDeath || hitMargin.IsCounted(GCS.hitMarginLimit))
                return;

            var controller = scrController.instance;
            if (controller == null || !controller.gameworld || controller.noFail ||
                __instance.currFloor == null || __instance.currFloor.freeroam || !__instance.alive)
                return;

            // Reuse the game's judgement rejection message and current game language.
            string message = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object>
            {
                { "judgement", hitMargin.ToLocalized(null) }
            });
            // Use the normal death method so autoplay, debug and revival protections still apply.
            __instance.Die(false, false, message, false);
            // Die can decline the request; only report an additional failure if it actually killed.
            __result |= !__instance.alive;
        }
    }
}
