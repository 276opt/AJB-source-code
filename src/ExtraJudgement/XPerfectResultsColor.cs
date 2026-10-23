using System;
using HarmonyLib;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    [HarmonyPatch(typeof(DetailedResults), "ShowForPlayer")]
    internal static class XPerfectResultsColor
    {
        private static Text? currentText;
        private static string nativeText = "";
        private static string displayedText = "";
        private static string coloredText = "";
        private static XPerfectResultsGradient? gradient;
        private static DetailedResultsView? view;
        private static DetailedResultsCounts? counts;
        private static scrHUDText? currentHud;

        [HarmonyPostfix]
        private static void Postfix(DetailedResults __instance, int __0)
        {
            if (!DisplayErrors.TryRun("Detailed Results show", () =>
            {
                if (currentText != __instance.textComponent) Restore();
                currentText = __instance.textComponent;
                if (currentText == null) return;
                // Keep the game's cached results intact, including when co-op switches players.
                nativeText = currentText.text;
                displayedText = nativeText;
                counts = null;
                currentHud = __instance.hudText;
                DisplayErrors.Run("Detailed Results counts", () =>
                {
                    var manager = ADOBase.playerManager;
                    if (manager != null && __0 >= 0 && __0 < manager.players.Length)
                        counts = new DetailedResultsCounts(manager.players[__0].marginTracker);
                });
                if (view != null) view.CaptureNative(__instance.hudText);
                Refresh();
            })) Restore();
        }

        internal static void Refresh()
        {
            if (!DisplayErrors.TryRun("Detailed Results refresh", RefreshDisplay)) Restore();
            CompletionResultsText.Refresh();
        }

        private static void RefreshDisplay()
        {
            if (currentText == null) return;
            var settings = Main.settings;
            // Do not restore stale results if the game has cleared or replaced the text.
            if (currentText.text != nativeText && currentText.text != displayedText)
            {
                if (gradient != null) gradient.enabled = false;
                if (view != null) view.enabled = false;
                return;
            }

            string text = nativeText;
            if (EnchantedDetailedResults.Active && settings != null && settings.DetailedResultsSignedPerfects
                && currentText.supportRichText && counts != null && EnchantedDetailedResults.XPerfectMode)
                text = EnchantedDetailedResults.SplitPerfects(text, counts);
            coloredText = Colorize(text, RDString.Get("status.results.xPerfect", null) + ": ");
            bool active = Main.is_mod_enable && settings != null && settings.BlueXPerfectResults
                && currentText.supportRichText && coloredText != text;
            if (active)
            {
                if (gradient == null)
                    gradient = currentText.GetComponent<XPerfectResultsGradient>()
                        ?? currentText.gameObject.AddComponent<XPerfectResultsGradient>();
                gradient.enabled = true;
            }
            else if (gradient != null) gradient.enabled = false;
            displayedText = active ? coloredText : text;
            currentText.text = displayedText;
            bool enhanced = EnchantedDetailedResults.Active && settings != null && !string.IsNullOrEmpty(displayedText)
                && (settings.DetailedResultsBackground || settings.DetailedResultsDefaultColors);
            if (enhanced)
            {
                if (view == null)
                {
                    view = currentText.GetComponent<DetailedResultsView>()
                        ?? currentText.gameObject.AddComponent<DetailedResultsView>();
                    view.CaptureNative(currentHud);
                }
                view.enabled = true;
                view.Refresh();
            }
            else if (view != null) view.enabled = false;
        }

        internal static bool Owns(Text text) => text == currentText && text.text == displayedText;

        internal static bool AppliesTo(Text text) => text == currentText && text.text == coloredText
            && Main.is_mod_enable && Main.settings != null && Main.settings.BlueXPerfectResults;

        private static void Restore()
        {
            DisplayErrors.Run("Detailed Results restore text", () =>
            {
                if (currentText != null && currentText.text == displayedText) currentText.text = nativeText;
            });
            DisplayErrors.Run("Detailed Results restore gradient", () => { if (gradient != null) gradient.enabled = false; });
            DisplayErrors.Run("Detailed Results restore view", () => { if (view != null) view.enabled = false; });
            gradient = null;
            view = null;
            counts = null;
        }

        internal static string Colorize(string text, string label)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int labelStart = text.IndexOf(label, StringComparison.Ordinal);
            if (labelStart < 0) return text;

            // The localized label is followed by a rich-text color span for this count only.
            int colorStart = labelStart + label.Length;
            if (text.IndexOf("<color=", colorStart, StringComparison.Ordinal) != colorStart) return text;
            int colorEnd = text.IndexOf('>', colorStart);
            if (colorEnd < 0) return text;
            int countEnd = text.IndexOf("</color>", colorEnd + 1, StringComparison.Ordinal);
            if (countEnd <= colorEnd + 1) return text;
            // Vanilla may include an Auto count, e.g. 123(4). Refuse unfamiliar markup.
            for (int i = colorEnd + 1; i < countEnd; i++)
                if ((text[i] < '0' || text[i] > '9') && text[i] != '(' && text[i] != ')') return text;

            // A near-blue marker identifies the generated glyphs without relying on
            // character indices (rich text, localization and wrapping change those).
            return text.Substring(0, colorStart) + "<color=#BBBAFF>" + text.Substring(colorEnd + 1);
        }
    }
}
