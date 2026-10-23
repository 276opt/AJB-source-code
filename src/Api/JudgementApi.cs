using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccurateJudgementBar
{
    /// <summary>Immutable input for a display window calculation. Call the API on Unity's main thread.</summary>
    public sealed class JudgementContext
    {
        public float Bpm { get; }
        public Difficulty Difficulty { get; }
        public float SpeedTrial { get; }
        public float Pitch { get; }
        public float MarginScale { get; }
        /// <summary>True for timestamp input; false for the game's angle-based input path.</summary>
        public bool UseTimeBasedJudgement { get; }
        public JudgementContext(float bpm, Difficulty difficulty, float speedTrial = 1f)
            : this(bpm, difficulty, speedTrial, 1f, 1f) { }

        public JudgementContext(float bpm, Difficulty difficulty, float speedTrial, float pitch, float marginScale)
            : this(bpm, difficulty, speedTrial, pitch, marginScale, AsyncInputManager.isActive) { }

        public JudgementContext(float bpm, Difficulty difficulty, float speedTrial, float pitch, float marginScale,
            bool useTimeBasedJudgement)
        {
            if (!JudgementApi.IsPositive(bpm) || !JudgementApi.IsPositive(speedTrial))
                throw new ArgumentOutOfRangeException(nameof(bpm));
            if (!JudgementApi.IsPositive(pitch)) throw new ArgumentOutOfRangeException(nameof(pitch));
            if (!JudgementApi.IsPositive(marginScale)) throw new ArgumentOutOfRangeException(nameof(marginScale));
            Bpm = bpm;
            Difficulty = difficulty;
            SpeedTrial = speedTrial;
            Pitch = pitch;
            MarginScale = marginScale;
            UseTimeBasedJudgement = useTimeBasedJudgement;
        }
        public float CalculateWindow(float angle, float baseWindowMs, float minimumMs = 25f) =>
            Mathf.Max(Mathf.Max(60000f / Bpm * angle / 180f, baseWindowMs) / SpeedTrial, minimumMs);
    }

    /// <summary>A symmetric display window. IDs are case-sensitive; use a mod-qualified ID.</summary>
    public sealed class JudgementDefinition
    {
        public string Id { get; }
        public Func<JudgementContext, float> WindowMs { get; }
        public Func<JudgementContext, Color> Color { get; }
        public Func<JudgementContext, bool> Enabled { get; }
        public float PixelPadding { get; }
        public JudgementDefinition(string id, Func<JudgementContext, float> windowMs,
            Func<JudgementContext, Color> color, float pixelPadding = 0f,
            Func<JudgementContext, bool>? enabled = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An ID is required.", nameof(id));
            if (float.IsNaN(pixelPadding) || float.IsInfinity(pixelPadding)) throw new ArgumentOutOfRangeException(nameof(pixelPadding));
            Id = id;
            WindowMs = windowMs ?? throw new ArgumentNullException(nameof(windowMs));
            Color = color ?? throw new ArgumentNullException(nameof(color));
            PixelPadding = pixelPadding;
            Enabled = enabled ?? (_ => true);
        }
    }

    public sealed class JudgementBand
    {
        public string Id { get; }
        public float WindowMs { get; }
        public float Width { get; }
        public Color Color { get; }
        internal JudgementBand(string id, float ms, float width, Color color)
        {
            Id = id; WindowMs = ms; Width = width; Color = color;
        }
    }

    /// <summary>Display API only: does not change ADOFAI hit results or scoring. Main thread only.</summary>
    public static class JudgementApi
    {
        public const int ApiVersion = 1;
        public const string Red = "red", Orange = "orange", Yellow = "yellow", Green = "green", Blue = "blue";
        private static readonly Dictionary<string, JudgementDefinition> definitions = CreateDefaults();
        private static readonly HashSet<string> reportedErrors = new HashSet<string>();

        private static Dictionary<string, JudgementDefinition> CreateDefaults() => new Dictionary<string, JudgementDefinition>
        {
            [Red] = new JudgementDefinition(Red, c => c.CalculateWindow(60f, MeterWatcher.DiffToMinimum(c.Difficulty)), _ => MeterWatcher.GetRed(), 16f),
            [Orange] = new JudgementDefinition(Orange, c => c.CalculateWindow(60f, MeterWatcher.DiffToMinimum(c.Difficulty)), _ => MeterWatcher.GetOrange()),
            [Yellow] = new JudgementDefinition(Yellow, c => c.CalculateWindow(45f, 30f), _ => MeterWatcher.GetYellow()),
            [Green] = new JudgementDefinition(Green, c => c.CalculateWindow(30f, 20f), _ => MeterWatcher.GetGreen()),
            [Blue] = new JudgementDefinition(Blue, NativeXPerfect.CalculateWindow, _ => MeterWatcher.GetBlue(),
                enabled: _ => NativeXPerfect.Enabled && (Main.settings?.ShowXPerfectJudgement ?? true))
        };

        public static IReadOnlyList<JudgementDefinition> GetDefinitions() => new List<JudgementDefinition>(definitions.Values).AsReadOnly();
        public static bool TryGet(string id, out JudgementDefinition definition) => definitions.TryGetValue(id, out definition!);
        public static void Register(JudgementDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definitions.Add(definition.Id, definition);
        }
        public static void Replace(JudgementDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!definitions.ContainsKey(definition.Id)) throw new KeyNotFoundException(definition.Id);
            definitions[definition.Id] = definition;
            reportedErrors.Remove(definition.Id);
        }
        public static bool Remove(string id)
        {
            reportedErrors.Remove(id);
            return definitions.Remove(id);
        }
        internal static bool IsPositive(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);

        /// <summary>Returns widest first. The vanilla orange window anchors 300 pixels; widths cap at 400.</summary>
        public static IReadOnlyList<JudgementBand> Evaluate(JudgementContext context)
            => Evaluate(context, true);

        internal static IReadOnlyList<JudgementBand> Evaluate(JudgementContext context, bool accurateWidths)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            float referenceMs = context.CalculateWindow(60f, MeterWatcher.DiffToMinimum(context.Difficulty));
            var result = new List<JudgementBand>();
            // Snapshot allows a callback to register definitions for the next evaluation.
            foreach (var definition in GetDefinitions())
            {
                try
                {
                    if (!definition.Enabled(context)) continue;
                    float ms = definition.WindowMs(context);
                    if (!IsPositive(ms)) throw new InvalidOperationException("Window must be finite and positive.");
                    var color = definition.Color(context);
                    if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b) || !Finite(color.a))
                        throw new InvalidOperationException("Color must be finite.");
                    float width = Mathf.Clamp(Mathf.Round(300f * (ms / referenceMs)) + definition.PixelPadding, 0f, 400f);
                    if (!accurateWidths)
                    {
                        // Only the built-in display widths change; callbacks and custom bands remain active.
                        switch (definition.Id)
                        {
                            case Red: width = 316f; break; // Keep the red border 8 px per side.
                            case Orange: width = 300f; break;
                            case Yellow: width = 225f; break;
                            case Green: width = 150f; break;
                            case Blue: width = 62.5f; break; // 12.5 degrees relative to the 60-degree outer window.
                        }
                    }
                    result.Add(new JudgementBand(definition.Id, ms, width, color));
                }
                catch (Exception ex)
                {
                    if (reportedErrors.Add(definition.Id))
                        Main.Mod?.Logger.Error($"Judgement '{definition.Id}' skipped: {ex.Message}");
                }
            }
            result.Sort(CompareBands);
            return result.AsReadOnly();
        }
        internal static int CompareBands(JudgementBand a, JudgementBand b)
        {
            int order = b.Width.CompareTo(a.Width);
            if (order != 0) return order;
            // Equal windows share an edge: stricter judgements must paint on top.
            // Both UI sibling order and curved slices use this same depth rule.
            order = BandDepth(a.Id).CompareTo(BandDepth(b.Id));
            return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
        }
        private static int BandDepth(string id)
        {
            switch (id)
            {
                case Red: return 0;
                case Orange: return 1;
                case Yellow: return 2;
                case Green: return 3;
                case Blue: return 4;
                default: return 5;
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
