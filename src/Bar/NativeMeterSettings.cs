using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using HarmonyLib;

namespace AccurateJudgementBar
{
    // Predrop's tick-preset setter refreshes the sprite with Normal/Straight.
    // Replace only that call's layout arguments; retain the original setter,
    // sprite assignment and exception behavior. Ordinary layout previews stay native.
    internal static class NativeMeterSettings
    {
        internal struct LayoutScope
        {
            internal scrHitErrorMeter? Meter;
            internal ErrorMeterSize Size;
            internal ErrorMeterShape Shape;
        }

        [ThreadStatic] private static LayoutScope current;

        [HarmonyPatch(typeof(Persistence), "set_hitMarginPerfectTick")]
        internal static class TickPresetPatch
        {
            [HarmonyPrefix]
            internal static void Prefix(out LayoutScope __state)
            {
                __state = current;
                current = default;
                try
                {
                    if (!Main.is_mod_enable) return;
                    current = new LayoutScope
                    {
                        Meter = scrController.instance?.errorMeter,
                        // Off has no native layout baseline; leave visibility/prefs alone
                        // but avoid treating an already offset wrapper as a new baseline.
                        Size = Persistence.hitErrorMeterSize == ErrorMeterSize.Off
                            ? ErrorMeterSize.Normal : Persistence.hitErrorMeterSize,
                        Shape = Persistence.hitErrorMeterShape
                    };
                }
                catch (Exception ex) { DisplayErrors.Report("tick preset layout", ex); }
            }

            // A void finalizer restores reentrant scopes without swallowing native exceptions.
            [HarmonyFinalizer]
            internal static void Finalizer(LayoutScope __state) => current = __state;
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), nameof(scrHitErrorMeter.UpdateLayout))]
        internal static class LayoutPatch
        {
            [HarmonyPrefix]
            internal static void Prefix(scrHitErrorMeter __instance, ref ErrorMeterSize size,
                ref ErrorMeterShape shape)
            {
                if (!Main.is_mod_enable || current.Meter == null ||
                    !ReferenceEquals(current.Meter, __instance)) return;
                size = current.Size;
                shape = current.Shape;
            }
        }

        [HarmonyPatch]
        internal static class PreferencePatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Persistence), "set_hitErrorMeterSize");
                yield return AccessTools.Method(typeof(Persistence), "set_hitErrorMeterShape");
                yield return AccessTools.Method(typeof(Persistence), "set_enableCompetitiveMode");
            }

            [HarmonyPostfix]
            internal static void Postfix()
            {
                if (!Main.is_mod_enable || Main.settings == null) return;
                // Applies saved size/shape and the effective sprite preset even when the
                // meter is inactive in the editor. Refresh never writes Persistence.
                DisplayErrors.Run("native meter preferences", ErrorMeterSizeSettings.Refresh);
            }
        }
    }
}
