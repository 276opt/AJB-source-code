using System;
using System.Collections.Generic;

namespace AccurateJudgementBar
{
    // API callbacks always evaluate the current context. Only the rendered widths animate.
    internal sealed class JudgementBandAnimation
    {
        private sealed class Transition
        {
            internal float Start, Target;
            internal double StartedAt;

            internal Transition(float width, double now)
            { Start = Target = width; StartedAt = now; }

            internal float Progress(double now, float duration)
            {
                if (duration <= 0f) return 1f;
                float t = (float)Math.Max(0.0, Math.Min(1.0, (now - StartedAt) / duration));
                float remaining = 1f - t;
                return 1f - remaining * remaining * remaining;
            }
        }

        private readonly Dictionary<string, Transition> transitions = new Dictionary<string, Transition>();

        internal void Reset() => transitions.Clear();

        internal IReadOnlyList<JudgementBand> Sample(IReadOnlyList<JudgementBand> targets, double now, float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration)) duration = 0f;
            var active = new HashSet<string>();
            var result = new List<JudgementBand>(targets.Count);
            foreach (var band in targets)
            {
                active.Add(band.Id);
                if (!transitions.TryGetValue(band.Id, out var transition))
                {
                    // New/re-enabled bands appear immediately, as they did before.
                    transition = new Transition(band.Width, now);
                    transitions.Add(band.Id, transition);
                }

                // Advance the old transition before retargeting, including continuously changing callbacks.
                float progress = transition.Progress(now, duration);
                float width = transition.Start + (transition.Target - transition.Start) * progress;
                if (transition.Target != band.Width)
                {
                    transition.Start = width;
                    transition.Target = band.Width;
                    transition.StartedAt = now;
                }
                if (duration <= 0f)
                {
                    width = transition.Start = transition.Target;
                }
                result.Add(new JudgementBand(band.Id, band.WindowMs, width, band.Color));
            }
            foreach (var id in new List<string>(transitions.Keys))
                if (!active.Contains(id)) transitions.Remove(id);

            // Widths can cross mid-animation; both renderers need the current visual order.
            result.Sort(JudgementApi.CompareBands);
            return result.AsReadOnly();
        }
    }
}
