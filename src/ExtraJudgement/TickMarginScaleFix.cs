using System;
using HarmonyLib;

namespace AccurateJudgementBar
{
    // ADOFAI 3.4 HOTPATCH's native tick display reads marginScale from the previous floor.
    // For B -> C, currfloor is still B when scrPlanet.SwitchChosen judges the beat,
    // so currfloor.nextfloor.marginScale correctly reads C's scale.
    // However, scrPlayer.Hit saves B before SwitchChosen and then passes B as hitFloor
    // to scrController.UpdateHitErrorMeter, which calls AddHit with B.marginScale.
    // AddHit uses that scale for the Counted window and normalizes the angular error
    // into a tick position. Tick appearance uses the existing HitMargin independently.
    // With B = 100% and C = 800%, when the minimum time cap does not control the window,
    // an input 80 degrees early is within C's XPerfect window (100 degrees), but outside
    // B's Counted window (60 degrees) used for positioning. The native display therefore
    // clamps the XPerfect tick to the meter's edge. Equal adjacent scales hide this issue.
    //
    // This patch changes AddHit's scale only within a native UpdateHitErrorMeter call.
    // Keep hitFloor = B: B.speed supplies the movement speed for B -> C, so replacing
    // hitFloor with C would be incorrect. Do not write floor data, recalculate judgements,
    // or change the death flow. Direct AddHit calls, including AJB's synthetic failure
    // ticks, are unaffected. Disabling this option or AJB restores native positioning
    // for subsequent ticks without recalculating ticks already on screen.
    internal static class TickMarginScaleFix
    {
        internal struct DisplayScope
        {
            internal scrHitErrorMeter? Meter;
            internal scrFloor? Departure;
            internal scrPlanet? Planet;
            internal float MarginScale;
        }

        [ThreadStatic] private static DisplayScope current;
        private static bool Enabled => Main.is_mod_enable && Main.settings?.FixTickMarginScale == true;

        [HarmonyPatch(typeof(scrController), "UpdateHitErrorMeter")]
        internal static class MeterUpdatePatch
        {
            [HarmonyPrefix]
            internal static void Prefix(scrController __instance, scrFloor hitFloor,
                scrPlanet priorChosenPlanet, out DisplayScope __state)
            {
                __state = current;
                current = default;
                // Catch only errors in our display setup, never exceptions from the game.
                try
                {
                    if (!Enabled || __instance == null || hitFloor == null ||
                        priorChosenPlanet == null || __instance.errorMeter == null) return;
                    var target = hitFloor.nextfloor;
                    if (target == null) return;
                    float scale = (float)target.marginScale;
                    if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale)) return;
                    current = new DisplayScope
                    {
                        Meter = __instance.errorMeter,
                        Departure = hitFloor,
                        Planet = priorChosenPlanet,
                        MarginScale = scale
                    };
                }
                catch (Exception)
                {
                    // Keep the original display arguments if this optional setup fails.
                    current = default;
                }
            }

            // A void finalizer restores nested/reentrant calls without suppressing or
            // replacing any exception thrown by the original UpdateHitErrorMeter/AddHit.
            [HarmonyFinalizer]
            internal static void Finalizer(DisplayScope __state) => current = __state;
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), "AddHit")]
        internal static class AddHitPatch
        {
            [HarmonyPrefix]
            internal static void Prefix(scrHitErrorMeter __instance, ref float marginScale,
                scrPlanet planet, scrFloor hitFloor)
            {
                // Leave direct API calls and AJB's synthetic failure ticks alone.
                // No writes to floors, input angles, HitMargin, speed, or gameplay state.
                if (!Enabled || current.Meter == null ||
                    !ReferenceEquals(current.Meter, __instance) ||
                    !ReferenceEquals(current.Departure, hitFloor) ||
                    !ReferenceEquals(current.Planet, planet)) return;
                marginScale = current.MarginScale;
            }
        }
    }
}
