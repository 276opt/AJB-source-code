using HarmonyLib;
using System;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    internal static class NoMissOnly
    {
        internal static bool Active => MarginLimitState.IsSelected(MarginLimitState.Mode.NoMissOnly);

        // None already rejects TooEarly/TooLate. Do not patch IsCounted or the hit windows.
        [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.OnDamage))]
        internal static class DeathPatch
        {
            [HarmonyPostfix]
            internal static void Postfix(scrPlayer __instance, HitMargin hitMargin, bool multipress)
            {
                if (!Active || multipress || (hitMargin != HitMargin.TooEarly && hitMargin != HitMargin.TooLate))
                    return;
                var controller = scrController.instance;
                if (controller == null || !controller.gameworld || controller.noFail || !__instance.alive ||
                    __instance.currFloor == null || __instance.currFloor.freeroam)
                    return;

                // In 3.4 HOTPATCH, Die(false, ...) can call Hit() on safe floors,
                // during miss cooldown, or when an expo life remains. Do not introduce
                // that synthetic hit for an otherwise rejected input or spend an extra life.
                int lives = scrLivesCounter.instance?.lives ?? 0;
                if (__instance.currFloor.isSafe || __instance.missCooldownActive ||
                    lives > (ADOBase.isExpo ? 1 : 0))
                    return;

                string message = "No Miss Only";
                try
                {
                    message = RDString.Get("status.purePerfectOnlyExplainer", new Dictionary<string, object>
                    {
                        { "judgement", hitMargin.ToLocalized(null) }
                    });
                }
                catch (Exception) { } // A missing display string must not interrupt the damage path.
                // Native Die keeps autoplay/debug guards and performs all death bookkeeping.
                __instance.Die(false, false, message, false);
                // Preserve OnDamage's result: SwitchChosen would otherwise replace the
                // original TooEarly/TooLate record with FailOverload under native None.
            }
        }
    }
}
