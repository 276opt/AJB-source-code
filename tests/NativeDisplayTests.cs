using System;
using System.Linq;
using AccurateJudgementBar;
using UnityEngine;

internal static class NativeDisplayTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Native display: " + message); }
        bool Near(float a, float b) => Math.Abs(a - b) < .001f;
        var savedPreset = Persistence.hitMarginPerfectTick;
        bool savedCompetitive = Persistence.enableCompetitiveMode;
        JudgementApi.TryGet(JudgementApi.Blue, out var savedDefinition);
        Persistence.enableCompetitiveMode = true;
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        try
        {
            var context = new JudgementContext(85f, Difficulty.Normal, 1f, 1f, .25f, true);
            var displayed = JudgementApi.Evaluate(context);
            var blue = displayed.Single(b => b.Id == JudgementApi.Blue);
            Check(Near(blue.WindowMs, 49.019607f), "supplied case follows HOTPATCH XPerfect window");
            Check(Math.Abs(blue.Width - 62.5f) <= .5f, "supplied case has a centered 62.5 px window before rounding");
            Check(Near(displayed.Single(b => b.Id == JudgementApi.Green).WindowMs, 117.64706f), "green formula is unchanged");

            var slices = MeterBandLayout.CurvedSlices(displayed);
            var white = slices.Single(s => s.XPerfect);
            Check(white.Start == -blue.Width / 2f && white.End == blue.Width / 2f, "curved white range stays centered");
            Check(slices.Count(s => s.Color.Equals(displayed.Single(b => b.Id == JudgementApi.Green).Color)) == 2,
                "green remains visible on both sides of white");
            foreach (var sample in new[] { (-210f, JudgementApi.Orange), (-190f, JudgementApi.Orange),
                (-150f, JudgementApi.Yellow), (-100f, JudgementApi.Green), (-30f, JudgementApi.Blue),
                (0f, JudgementApi.Blue), (30f, JudgementApi.Blue), (100f, JudgementApi.Green),
                (150f, JudgementApi.Yellow), (190f, JudgementApi.Orange), (210f, JudgementApi.Orange) })
            {
                float x = sample.Item1 * 150f / (60000f / 21.25f * 60f / 180f * .25f);
                var expectedColor = displayed.Single(b => b.Id == sample.Item2).Color;
                var curved = slices.Last(s => s.Start <= x && x <= s.End);
                var straight = displayed.Last(b => -b.Width / 2f <= x && x <= b.Width / 2f);
                Check(curved.Color.Equals(expectedColor) && straight.Id == sample.Item2,
                    "both renderers match HOTPATCH judgement at " + sample.Item1 + " ms");
            }

            var animation = new JudgementBandAnimation();
            var fast = JudgementApi.Evaluate(new JudgementContext(600f, Difficulty.Normal, 1f, 2f, 1f, false));
            animation.Sample(displayed, 0, 1f);
            animation.Sample(fast, 1, 1f);
            var midBands = animation.Sample(fast, 1.5, 1f);
            var mid = midBands.Single(b => b.Id == JudgementApi.Blue);
            var target = fast.Single(b => b.Id == JudgementApi.Blue);
            Check(Near(mid.Width, blue.Width + (target.Width - blue.Width) * .875f), "XPerfect width animation keeps its easing");
            var midWhite = MeterBandLayout.CurvedSlices(midBands).Single(s => s.XPerfect);
            Check(midWhite.Start == -midWhite.End, "animation keeps the native window centered");
            Check(animation.Sample(fast, 2, 1f).Single(b => b.Id == JudgementApi.Blue).Width == target.Width,
                "animation reaches its final width");

            const string ultraId = "test.native-ultra";
            var ultraColor = new Color(.1f, .2f, .3f);
            JudgementApi.Register(new JudgementDefinition(ultraId, _ => 5f, _ => ultraColor));
            try
            {
                var withCustom = JudgementApi.Evaluate(context);
                Check(withCustom.Last().Id == ultraId, "custom inner bands stay visible");
                var customSlices = MeterBandLayout.CurvedSlices(withCustom);
                Check(customSlices.Single(s => s.Start < 0f && s.End > 0f).Color.Equals(ultraColor), "custom center color is preserved");
                Check(customSlices.Count(s => s.XPerfect) == 2 &&
                    customSlices.Where(s => s.XPerfect).All(s => s.End <= 0f || s.Start >= 0f),
                    "white outlines stay split around a custom center band");
            }
            finally { JudgementApi.Remove(ultraId); }

            JudgementApi.Replace(new JudgementDefinition(JudgementApi.Blue, _ => 200f, _ => new Color(1, 1, 1)));
            var customBlue = JudgementApi.Evaluate(context);
            Check(customBlue.Single(b => b.Id == JudgementApi.Blue).WindowMs == 200f, "custom blue formula remains untouched");
            Check(MeterBandLayout.CurvedSlices(customBlue).Count(s => s.XPerfect) == 2,
                "custom wider blue keeps the API's symmetric width ordering");
        }
        finally
        {
            JudgementApi.Replace(savedDefinition);
            Persistence.hitMarginPerfectTick = savedPreset;
            Persistence.enableCompetitiveMode = savedCompetitive;
        }
        Console.WriteLine($"Passed {checks} native display checks (HOTPATCH, both renderers and custom bands).");
    }
}
