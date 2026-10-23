using System;
using System.Linq;
using AccurateJudgementBar;
using UnityEngine;

// Minimal game-setting stubs; no XPerfect mod is present in this harness.
public enum HitMarginPerfectTickPreset { Default, ShowXPerfect, ShowXPerfectWithoutOutline }
public static class AsyncInputManager { public static bool isActive; }
public static class HitMarginHelper
{
    public static bool IsShowXPerfect(this HitMarginPerfectTickPreset preset) =>
        preset == HitMarginPerfectTickPreset.ShowXPerfect || preset == HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline;
}
public static class Persistence
{
    public static bool enableCompetitiveMode;
    private static HitMarginPerfectTickPreset savedTickPreset;
    public static HitMarginPerfectTickPreset hitMarginPerfectTick
    {
        get => enableCompetitiveMode ? savedTickPreset : HitMarginPerfectTickPreset.Default;
        set => savedTickPreset = value;
    }
    public static bool hitMarginPerfectText; // Must never enable a meter band.
}
public class ColourSchemeHitMargin { public Color colourXPerfect = new Color(.7f, .8f, .9f); }
public static class RDConstants
{
    public static Data data = new Data();
    public class Data { public ColourSchemeHitMargin hitMarginColoursUI = new ColourSchemeHitMargin(); }
}

internal static class NativeXPerfectTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Native XPerfect: " + message); }
        bool HasBand() => JudgementApi.Evaluate(new JudgementContext(200, Difficulty.Normal)).Any(b => b.Id == JudgementApi.Blue);
        Persistence.enableCompetitiveMode = true;
        Persistence.hitMarginPerfectText = true;
        Check(!HasBand(), "text setting alone does not enable band");
        foreach (var preset in new[] { HitMarginPerfectTickPreset.ShowXPerfect, HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline })
        {
            Persistence.hitMarginPerfectTick = preset;
            Check(HasBand(), "tick preset enables without external mod: " + preset);
            Persistence.hitMarginPerfectText = false;
            Check(HasBand(), "text off does not disable meter");
            Persistence.enableCompetitiveMode = false;
            Check(!HasBand(), "effective setting respects competitive mode off");
            Persistence.enableCompetitiveMode = true;
        }
        var nativeColor = new Color(.3f, .7f, .8f);
        RDConstants.data.hitMarginColoursUI.colourXPerfect = nativeColor;
        Check(JudgementApi.Evaluate(new JudgementContext(200, Difficulty.Normal))
            .Single(b => b.Id == JudgementApi.Blue).Color.Equals(nativeColor), "reads native UI color");

        // Golden values executed from the installed HOTPATCH DLL, in real milliseconds.
        foreach (var sample in new[] {
            (21.25f, .1f, 1f, 19.607843f), (21.25f, .25f, 1f, 49.019607f),
            (21.25f, .4f, 1f, 78.431372f), (21.25f, 1f, 1f, 196.078430f),
            (300f, 1f, 2f, 16.666668f), (160f, .4f, 1f, 16.666668f),
            (100f, 1f, 1f, 41.666666f) })
        foreach (float speed in new[] { .5f, 1f, 2f })
        foreach (bool timeBased in new[] { false, true })
        {
            var (floorBpm, margin, pitch, expected) = sample;
            var context = new JudgementContext(floorBpm * pitch / margin / speed, Difficulty.Normal, speed, pitch, margin, timeBased);
            Check(Math.Abs(NativeXPerfect.CalculateWindow(context) - expected) < .0001f,
                "hotpatch sample preserves margin, pitch and minimum independently of context normalization");
        }

        // API compatibility: input-path metadata stays available, though native windows now agree.
        AsyncInputManager.isActive = true;
        var snapshot = new JudgementContext(100f, Difficulty.Normal);
        AsyncInputManager.isActive = false;
        Check(snapshot.UseTimeBasedJudgement, "context snapshots the input path");
        Check(!new JudgementContext(100f, Difficulty.Normal).UseTimeBasedJudgement, "new contexts follow input toggle");

        // Independent native angle oracle, including the outer Counted normalization used by hit ticks.
        foreach (var difficulty in new[] { Difficulty.Lenient, Difficulty.Normal, Difficulty.Strict })
        foreach (float floorBpm in new[] { 21.25f, 100f, 160f, 300f, 1000f })
        foreach (float pitch in new[] { .5f, 1f, 2f })
        foreach (float margin in new[] { .1f, .25f, .4f, 1f, 2f })
        foreach (float speed in new[] { .5f, 1f, 2f })
        foreach (bool timeBased in new[] { false, true })
        {
            double countedMinimumMs = Math.Max((difficulty == Difficulty.Lenient ? 91.0 : difficulty == Difficulty.Strict ? 40.0 : 65.0) / speed, 25.0);
            double degreesPerMs = floorBpm * pitch * 180.0 / 60000.0;
            double countedAngle = Math.Max(60.0 * margin, countedMinimumMs * degreesPerMs);
            double xPerfectAngle = Math.Max(12.5 * margin, (1000.0 / 60.0) * degreesPerMs);
            var context = new JudgementContext(floorBpm * pitch / margin / speed, difficulty, speed, pitch, margin, timeBased);
            var bands = JudgementApi.Evaluate(context);
            var actual = bands.Single(b => b.Id == JudgementApi.Blue);
            double expectedMs = xPerfectAngle / degreesPerMs;
            double expectedWidth = Math.Min(400.0, 300.0 * xPerfectAngle / countedAngle);
            Check(Math.Abs(actual.WindowMs - expectedMs) <= Math.Max(.0001, expectedMs * .000001), "real-time boundary matches native angle conversion");
            Check(Math.Abs(actual.Width - expectedWidth) <= .5001, "band width matches native tick coordinates");
            Check(actual.WindowMs <= bands.Single(b => b.Id == JudgementApi.Green).WindowMs, "XPerfect remains inside Pure");
            Check(actual.WindowMs >= 16.66666f, "pitch and speed never shrink the real-time minimum");
        }

        foreach (bool timeBased in new[] { false, true })
        {
            var context = new JudgementContext(85f, Difficulty.Normal, 1f, 1f, .25f, timeBased);
            Check(JudgementApi.Evaluate(context, false).Single(b => b.Id == JudgementApi.Blue).Width == 62.5f,
                "fixed-width mode preserves the existing setting");
        }
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.Default;
        Check(!HasBand(), "turning setting off removes band immediately");
        Console.WriteLine($"Passed {checks} native XPerfect checks (HOTPATCH values and native angle oracle; no Unity runtime).");
    }
}
