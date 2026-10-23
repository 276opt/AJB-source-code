using System;
using UnityEngine;

namespace AccurateJudgementBar
{
    internal static class NativeXPerfect
    {
        // The effective getter also respects the game's competitive-mode switch.
        internal static bool Enabled => Persistence.hitMarginPerfectTick.IsShowXPerfect();
        internal static Color Color => RDConstants.data.hitMarginColoursUI.colourXPerfect;

        internal static float CalculateWindow(JudgementContext context)
        {
            // HOTPATCH unifies both input paths. Bpm already includes pitch / marginScale
            // and divides out SpeedTrial; restore only that normalization before the time floor.
            return Math.Max(60000f / context.Bpm * 12.5f / 180f / context.SpeedTrial, 1000f / 60f);
        }
    }
}
