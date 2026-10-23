using System;

namespace AccurateJudgementBar
{
    // Independent of Unity so timing and visibility can be regression-tested.
    internal sealed class FailureBandAnimation
    {
        private double started;
        private float start, target;
        public void Reset() { start = target = 0f; started = 0; }
        public float Sample(bool allowed, bool triggered, double now, float duration)
        {
            // Disable, disallowed modes and attempt resets clear immediately.
            // Temporary visibility conditions instead animate in both directions.
            if (!allowed) { Reset(); return 0f; }
            float next = triggered ? 1f : 0f;
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                start = target = next;
                started = now;
                return next;
            }

            double t = Math.Max(0, Math.Min(1, (now - started) / duration));
            float value = start + (target - start) * (float)(1 - Math.Pow(1 - t, 3));
            if (next != target)
            {
                start = value;
                target = next;
                started = now;
            }
            return value;
        }
    }
}

