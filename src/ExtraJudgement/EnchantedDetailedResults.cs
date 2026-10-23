using System;
using System.Globalization;
using UnityEngine;

namespace AccurateJudgementBar
{
    // A snapshot for this player's displayed results, never a replacement tracker/cache.
    internal sealed class DetailedResultsCounts
    {
        internal readonly int Minus, XPerfect, Plus, Auto;

        internal DetailedResultsCounts(scrMarginTracker tracker)
        {
            Minus = tracker.GetHits(HitMargin.PerfectMinus);
            XPerfect = tracker.GetHits(HitMargin.XPerfect);
            Plus = tracker.GetHits(HitMargin.PerfectPlus);
            Auto = tracker.GetHits(HitMargin.Auto);
        }
    }

    internal static class EnchantedDetailedResults
    {
        internal static bool Active => Main.is_mod_enable && Main.settings != null
            && Main.settings.EnchantedDetailedResults;

        internal static bool XPerfectMode => NativeXPerfect.Enabled
            || Persistence.hitMarginPerfectText.IsShowXPerfect();

        private static string Label(string key) => RDString.Get("status.results." + key, null) + ": ";

        internal static string SplitPerfects(string native, DetailedResultsCounts counts)
        {
            if (string.IsNullOrEmpty(native)) return native;
            string minus = Label("perfectM"), plus = Label("perfectP"), x = Label("xPerfect");
            // Already signed: preserve the original order, spacing and rich text.
            int newline = native.IndexOf('\n');
            string first = newline < 0 ? native : native.Substring(0, newline);
            if (first.Contains(minus) && first.Contains(plus) && first.Contains(x)) return native;

            bool removed = false;
            foreach (string label in new[] { Label("perfect"), minus, plus, x })
            {
                int start = first.IndexOf(label, StringComparison.Ordinal);
                // "Perfect: " is also a substring of "EPerfect: " / "XPerfect: ".
                while (start > 0 && (start < 5 || first.Substring(start - 5, 5) != "     "))
                    start = first.IndexOf(label, start + 1, StringComparison.Ordinal);
                if (start < 0) continue;
                // Only handle the vanilla colored numeric spans. Unknown output is left intact.
                int colorStart = start + label.Length;
                if (first.IndexOf("<color=", colorStart, StringComparison.Ordinal) != colorStart) return native;
                int colorEnd = first.IndexOf('>', colorStart);
                if (colorEnd < 0) return native;
                int end = first.IndexOf("</color>", colorEnd + 1, StringComparison.Ordinal);
                if (end <= colorEnd + 1) return native;
                for (int i = colorEnd + 1; i < end; i++)
                    if (!char.IsDigit(first[i]) && first[i] != '(' && first[i] != ')') return native;
                first = first.Remove(start, end + "</color>".Length - start);
                // Keep the EPerfect / LPerfect row when the native text preset had no XPerfect.
                first = first.Replace("          ", "     ").Trim();
                removed = true;
            }
            if (!removed) return native;

            string color = RDConstants.data.hitMarginColoursUI.colourPerfect.ToHex(false, true);
            string Count(int value) => "<color=" + color + ">" + value.ToString(CultureInfo.InvariantCulture) + "</color>";
            string auto = counts.Auto == 0 ? "" : "(" + counts.Auto.ToString(CultureInfo.InvariantCulture) + ")";
            string row = minus + Count(counts.Minus) + "     " + x + "<color=" + color + ">"
                + counts.XPerfect.ToString(CultureInfo.InvariantCulture) + auto + "</color>     " + plus + Count(counts.Plus);
            return row + (first.Length == 0 ? "" : "\n" + first)
                + (newline < 0 ? "" : native.Substring(newline));
        }

        // ColourScheme instances are mutable (including worldColourScheme entries).
        // Keep the results palette independent of level effects and shared game objects.
        internal static Color DefaultTextColor => Color.white;
        internal static Color DefaultShadowColor => new Color(0f, 0f, 0f, .5f);
    }
}
