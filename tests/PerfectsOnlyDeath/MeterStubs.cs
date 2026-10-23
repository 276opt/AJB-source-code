using System;
using System.Collections.Generic;
using UnityEngine;

// Link the production MeterWatcher so color tests exercise its real mode selection.
namespace ADOFAI.Editor.Actions { }
namespace UnityEngine.UI { }
namespace UnityEngine.UIElements { }
namespace UnityEngine
{
    public class MonoBehaviour { }
    public readonly record struct Color(float r, float g, float b, float a = 1);
    public readonly record struct Color32(byte r, byte g, byte b, byte a)
    {
        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }
    public static class Time { public static double unscaledTimeAsDouble; }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    {
        public class ModEntry { public bool Active; public ModInfo Info = new(); public Log Logger = new(); }
        public class ModInfo { public string DisplayName = "AccurateJudgementBar"; }
        public class Log
        {
            public readonly List<string> Errors = new();
            public void Error(string message) => Errors.Add(message);
        }
        public static ModEntry? TogetherEntry;
        public static ModEntry? FindMod(string name) => name == "Together" ? TogetherEntry : null;
    }
}
public enum Difficulty { Lenient, Normal, Strict }
public class scrHitErrorMeter { }
public class PlanetarySystem { public double speed = 1; }
public class scrConductor
{
    public static scrConductor instance = new();
    public double bpm = 100;
    public Song song = new();
    public class Song { public float pitch = 1; }
}
namespace AccurateJudgementBar
{
    public class JudgementContext
    {
        public JudgementContext(float bpm, Difficulty difficulty, float speed, float pitch = 1, float marginScale = 1) { }
        public float CalculateWindow(float angle, float window, float minimum = 25) => window;
    }
    public class JudgementBand
    {
        public string Id { get; }
        public float WindowMs { get; }
        public float Width { get; }
        public Color Color { get; }
        public JudgementBand(string id, float ms, float width, Color color)
        { Id = id; WindowMs = ms; Width = width; Color = color; }
    }
    public class JudgementBandAnimation
    {
        public void Reset() { }
        public IReadOnlyList<JudgementBand> Sample(IReadOnlyList<JudgementBand> bands, double time, float duration) => bands;
    }
    public static class JudgementApi
    {
        public const string Red = "red", Blue = "blue";
        public static IReadOnlyList<JudgementBand> Evaluate(JudgementContext context, bool enabled) => Array.Empty<JudgementBand>();
    }
    public static class FailureJudgement
    {
        public static IReadOnlyList<JudgementBand> AddBand(IReadOnlyList<JudgementBand> bands) => bands;
    }
    public static class NativeXPerfect
    {
        public static bool Enabled => true;
        public static Color Color => new(0, 1, 1);
    }
}
