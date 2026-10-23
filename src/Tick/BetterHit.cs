
using HarmonyLib;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    public static class BetterHit
    {
        public static scrHitErrorMeter? HitErrorMeter;
        private static readonly Dictionary<scrHitErrorMeter, (float sensitivity, float tickLife)> originals =
            new Dictionary<scrHitErrorMeter, (float sensitivity, float tickLife)>();

        [HarmonyPatch(typeof(scrHitErrorMeter), "UpdateLayout")]
        public static class UpdateLayoutPatch
        {
            [HarmonyPostfix]
            public static void Postfix(scrHitErrorMeter __instance)
            {
                HitErrorMeter = __instance;
                if (Main.is_mod_enable) Apply(__instance);
            }
        }
        public static void Update()
        {
            LegacyHitVisual.RefreshAll();
            if (!Main.is_mod_enable) return;
            // Include inactive meters so enabling during a paused/hidden layout works too.
            foreach (var meter in UnityEngine.Object.FindObjectsByType<scrHitErrorMeter>(
                UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
                Apply(meter);
        }

        private static void Apply(scrHitErrorMeter meter)
        {
            if (meter == null) return;
            if (!originals.ContainsKey(meter))
                originals.Add(meter, (meter.sensitivity, meter.tickLife));
            meter.sensitivity = Main.settings.SensitivityOfHit;
            meter.tickLife = Main.settings.LifeOfHit;
        }

        public static void Reversed()
        {
            LegacyHitVisual.RefreshAll();
            foreach (var entry in originals)
            {
                if (entry.Key == null) continue;
                entry.Key.sensitivity = entry.Value.sensitivity;
                entry.Key.tickLife = entry.Value.tickLife;
            }
            originals.Clear();
            HitErrorMeter = null;
        }
    }
}

