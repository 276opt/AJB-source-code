using System.Collections.Generic;
using UnityEngine;

namespace AccurateJudgementBar
{
    internal static class MeterBandLayout
    {
        internal readonly struct Slice
        {
            internal readonly float Start, End;
            internal readonly Color Color;
            internal readonly bool XPerfect;
            internal Slice(float start, float end, JudgementBand band)
            { Start = start; End = end; Color = band.Color; XPerfect = band.Id == JudgementApi.Blue; }
        }

        internal static IReadOnlyList<Slice> CurvedSlices(IReadOnlyList<JudgementBand> bands)
        {
            var order = bands;
            var boundaries = new SortedSet<float>();
            foreach (var band in order)
            {
                boundaries.Add(-band.Width * .5f);
                boundaries.Add(band.Width * .5f);
            }
            var edges = new List<float>(boundaries);
            var result = new List<Slice>();
            int previousLayer = -1;
            // Resolve the visible color in each interval using the straight renderer's paint order.
            // Outlines then follow the visible white intervals, including occlusion by custom bands.
            for (int i = 0; i + 1 < edges.Count; i++)
            {
                float start = edges[i], end = edges[i + 1];
                int layer = -1;
                for (int j = 0; j < order.Count; j++)
                {
                    var band = order[j];
                    if (band.Width > 0f && -band.Width * .5f <= start && band.Width * .5f >= end) layer = j;
                }
                if (layer >= 0)
                {
                    if (layer == previousLayer && result.Count > 0)
                        result[result.Count - 1] = new Slice(result[result.Count - 1].Start, end, order[layer]);
                    else result.Add(new Slice(start, end, order[layer]));
                }
                previousLayer = layer;
            }
            return result;
        }
    }
}
