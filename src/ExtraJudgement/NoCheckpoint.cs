using System;
using HarmonyLib;

namespace AccurateJudgementBar
{
    internal static class NoCheckpoint
    {
        // Every selected limit includes this restriction, even when the Perfects Only
        // repair or Competitive Mode is off. PurePerfectOnly also has its native guard.
        internal static bool Active => MarginLimitState.Enabled && MarginLimitState.Current != MarginLimitState.Mode.None;

        // predrop StartEffect performs checkpoint revival, saves progress and marks the
        // tracker. Stop at its entry; never touch hits, deaths, existing progress or icons.
        // This includes the native parameterless wrapper, which dispatches to this overload.
        [HarmonyPatch(typeof(ffxCheckpoint), nameof(ffxCheckpoint.StartEffect), new Type[] { typeof(scrPlanet) })]
        internal static class ActivationPatch
        {
            [HarmonyPrefix]
            internal static bool Prefix() => !Active;
        }
    }
}
