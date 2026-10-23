using System;
using System.Collections.Generic;
using System.Linq;
using AccurateJudgementBar;
using UnityEngine;

internal static class JudgementBandAnimationTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Band animation: " + message); }
        bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
        float Width(IReadOnlyList<JudgementBand> bands, string id = JudgementApi.Green) => bands.Single(b => b.Id == id).Width;
        IReadOnlyList<JudgementBand> At(float bpm) => JudgementApi.Evaluate(new JudgementContext(bpm, Difficulty.Normal));
        var animation = new JudgementBandAnimation();

        var slow = At(50f);
        var fast = At(1000f);
        float start = Width(slow), end = Width(fast);
        Check(Width(animation.Sample(slow, 0, 1f)) == start, "initial layout has no transition");
        Check(Width(animation.Sample(fast, 1, 1f)) == start, "BPM jump starts at current width");
        float midpoint = Width(animation.Sample(fast, 1.5, 1f));
        Check(Near(midpoint, start + (end - start) * .875f), "50 -> 1000 uses complete width transition");
        Check(midpoint > end, "still moving after the old BPM tween would have exceeded 500");
        float late = Width(animation.Sample(fast, 1.75, 1f));
        Check(late < midpoint && late > end, "unchanged target does not restart or finish the animation early");
        Check(Width(animation.Sample(fast, 2, 1f)) == end, "finishes at exact target");
        Check(Width(animation.Sample(At(500f), 3, 1f)) == end, "500 -> 1000 saturation needs no visual motion");

        animation.Reset();
        animation.Sample(slow, 0, 1f);
        animation.Sample(fast, 1, 1f);
        float before = Width(animation.Sample(fast, 1.5, 1f));
        float after = Width(animation.Sample(slow, 1.5, 1f));
        Check(Near(before, after), "retarget preserves current position");
        Check(Width(animation.Sample(slow, 1.75, 1f)) > after, "retarget moves towards the new endpoint");
        Check(Width(animation.Sample(slow, 2, 0f)) == start, "zero duration finishes immediately mid-animation");
        Check(Width(animation.Sample(slow, 2.1, 1f)) == start, "re-enabling animation does not revive an old transition");
        animation.Reset();
        Check(Width(animation.Sample(fast, 3, 1f)) == end, "rebuilt UI initializes at current target");

        // Callbacks must see actual normalized BPM even without native XPerfect enabled.
        const string customId = "test.animation";
        float observedBpm = 0;
        JudgementApi.Register(new JudgementDefinition(customId, c => { observedBpm = c.Bpm; return 10000f / c.Bpm; }, _ => new Color(1, 0, 0)));
        try
        {
            animation.Reset();
            var low = At(25f);
            Check(observedBpm == 25f, "custom callback receives BPM below the old minimum");
            var high = At(1000f);
            Check(observedBpm == 1000f, "custom callback receives BPM above 500");
            Check(!high.Any(b => b.Id == JudgementApi.Blue), "custom motion tested with XPerfect disabled");
            animation.Sample(At(500f), 0, 1f);
            float customStart = Width(animation.Sample(high, 1, 1f), customId);
            var displayed = animation.Sample(high, 1.5, 1f);
            Check(Width(displayed, customId) < customStart && Width(displayed, customId) > Width(high, customId),
                "custom band animates above 500 BPM");
            Check(displayed.Single(b => b.Id == customId).WindowMs == 10f, "window metadata remains current during animation");
            Check(Width(displayed) == Width(high), "unchanged vanilla width stays fixed while custom band moves");
        }
        finally { JudgementApi.Remove(customId); }

        IReadOnlyList<JudgementBand> Bands(float a, float b, Color color) => new[]
        {
            new JudgementBand("a", 10f, a, color), new JudgementBand("b", 20f, b, color)
        };
        var red = new Color(1, 0, 0);
        var blue = new Color(0, 0, 1);
        animation.Reset();
        animation.Sample(Bands(300, 100, red), 0, 1f);
        var crossing = animation.Sample(Bands(100, 300, blue), 1, 1f);
        Check(crossing[0].Id == "a", "draw order uses displayed widths before crossing");
        Check(crossing.All(b => b.Color.Equals(blue)), "color changes are immediate");
        Check(animation.Sample(Bands(100, 300, blue), 1.5, 1f)[0].Id == "b", "draw order changes when animated widths cross");
        Check(animation.Sample(Bands(200, 200, blue), 2, 0f)[0].Id == "a", "equal widths use stable ordinal ID order");
        Check(animation.Sample(Array.Empty<JudgementBand>(), 3, 1f).Count == 0, "disabled or removed bands disappear immediately");
        Check(animation.Sample(Bands(50, 100, red), 4, 1f)[1].Width == 50, "re-enabled bands have no stale transition");

        animation.Reset();
        animation.Sample(Bands(300, 100, red), 0, 1f);
        animation.Sample(Bands(200, 100, red), .1, 1f);
        float moving = Width(animation.Sample(Bands(190, 100, red), .2, 1f), "a");
        Check(moving < 300, "continuously changing target still advances");
        Check(Width(animation.Sample(Bands(180, 100, red), .3, 1f), "a") < moving, "successive retargets keep moving");

        // All vanilla windows converge to the same minimum at high speed.
        var saturated = JudgementApi.Evaluate(new JudgementContext(1000f, Difficulty.Strict, 10f));
        Check(saturated.Single(b => b.Id == JudgementApi.Orange).Width == Width(saturated),
            "regression reproduces equal orange and green widths");
        Check(saturated.Last().Id == JudgementApi.Green, "equal windows keep green above orange and yellow");
        Check(MeterBandLayout.CurvedSlices(saturated).Single(s => s.Start < 0 && s.End > 0).Color.Equals(MeterWatcher.GetGreen()),
            "curved center also stays green when windows coincide");
        animation.Reset();
        animation.Sample(At(100f), 0, 1f);
        animation.Sample(saturated, 1, 1f);
        foreach (double time in new[] { 1.0, 1.25, 1.5, 1.99, 2.0 })
            Check(animation.Sample(saturated, time, 1f).Last().Id == JudgementApi.Green,
                "green stays on top throughout convergence");
        var equalBands = new[] { JudgementApi.Blue, JudgementApi.Green, JudgementApi.Yellow, JudgementApi.Orange, JudgementApi.Red }
            .Select(id => new JudgementBand(id, 25f, 300f, red)).ToArray();
        animation.Reset();
        Check(animation.Sample(equalBands, 0, 0f).Select(b => b.Id).SequenceEqual(
            new[] { JudgementApi.Red, JudgementApi.Orange, JudgementApi.Yellow, JudgementApi.Green, JudgementApi.Blue }),
            "equal-width depth includes XPerfect above green regardless of input order");
        Console.WriteLine($"Passed {checks} band-animation checks (both renderers share this state machine).");
    }
}
