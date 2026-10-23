using HarmonyLib;
using UnityEngine;

namespace AccurateJudgementBar
{
    // Move the shared wrapper so both meter shapes, ticks and hand stay aligned.
    public class MeterPosition : MonoBehaviour
    {
        private RectTransform? wrapper;
        private Vector2 baseline;
        private Vector2 lastPosition;

        public void CaptureLayout(scrHitErrorMeter meter)
        {
            wrapper = meter.wrapperRectTransform;
            if (wrapper == null)
                return;
            baseline = lastPosition = wrapper.anchoredPosition;
            ApplyPosition();
        }

        private void LateUpdate()
        {
            if (!Main.is_mod_enable) { RestorePosition(); return; }
            if (wrapper == null)
                return;

            // Respect layout changes made by the game or another mod.
            if (wrapper.anchoredPosition != lastPosition)
                baseline = wrapper.anchoredPosition;
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            if (wrapper == null)
                return;
            lastPosition = baseline + new Vector2(
                Main.settings.BarPositionX, Main.settings.BarPositionY);
            wrapper.anchoredPosition = lastPosition;
        }

        public void RestorePosition()
        {
            if (wrapper != null && wrapper.anchoredPosition == lastPosition)
                wrapper.anchoredPosition = baseline;
            wrapper = null;
        }

        private void OnDestroy() => RestorePosition();
    }

    [HarmonyPatch(typeof(scrHitErrorMeter), "UpdateLayout")]
    internal static class MeterPositionLayoutPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(scrHitErrorMeter __instance)
        {
            if (!Main.is_mod_enable)
                return;
            var position = __instance.GetComponent<MeterPosition>();
            if (position != null)
                position.CaptureLayout(__instance);
        }
    }
}
