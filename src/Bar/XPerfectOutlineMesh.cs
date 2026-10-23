using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // A scalable counterpart to the colored silhouette behind the game's
    // XPerfect tick. All distances are in the meter's local UI coordinates.
    internal static class XPerfectOutlineMesh
    {
        private const float Thickness = 1.5f;
        private const float Feather = .5f;
        // The native band sprite paints a fixed translucent rim outside the
        // white fill. AJB's adjustable opaque/inward rim remains the default.
        private const float VanillaThickness = 2f;
        private const float VanillaOpacity = .4f;

        internal static int EffectiveWidth(bool vanillaStyle, bool vanillaOutline, int ajbWidth) =>
            vanillaStyle ? (vanillaOutline ? 0 : -1) : ajbWidth;
        // Keep the original outer silhouette; larger presets only cover more
        // of the white fill. Unknown saved values fall back to Thin.
        private static float Inset(int width) => width == 1 ? .75f : width == 2 ? 1.5f : 0f;
        private static readonly Color[] GradientStops = {
            new Color32(247, 79, 79, 255),   // #f74f4f, 0%
            new Color32(255, 137, 199, 255), // #ff89c7, 25%
            new Color32(185, 148, 255, 255), // #b994ff, 50%
            new Color32(139, 173, 255, 255), // #8badff, 75%
            new Color32(0, 132, 255, 255)    // #0084ff, 100%
        };

        private static Color BorderColor(float y, float bottom, float top, float alpha)
        {
            float t = Mathf.InverseLerp(top, bottom, y);
            float scaled = t * 4f;
            int index = (int)Mathf.Min(3f, scaled);
            Color gradient = Color.Lerp(GradientStops[index], GradientStops[index + 1], scaled - index);
            // Match the supplied SVG: an opaque white base covered by the
            // five-stop gradient at 40% opacity, rather than a translucent rim.
            Color color = Color.Lerp(new Color(1f, 1f, 1f), gradient, .4f);
            color.a = alpha;
            return color;
        }

        internal readonly struct ArcRange
        {
            internal readonly float Start, End, Alpha;
            internal ArcRange(float start, float end, float alpha)
            { Start = start; End = end; Alpha = alpha; }
        }

        internal static IReadOnlyList<ArcRange> ConnectedArcs(IEnumerable<ArcRange> arcs)
        {
            var ordered = new List<ArcRange>();
            foreach (var arc in arcs) if (arc.End > arc.Start && arc.Alpha > 0f) ordered.Add(arc);
            ordered.Sort((a, b) => a.Start.CompareTo(b.Start));
            var result = new List<ArcRange>();
            foreach (var arc in ordered)
            {
                if (result.Count > 0)
                {
                    var previous = result[result.Count - 1];
                    if (arc.Start <= previous.End && arc.Alpha == previous.Alpha)
                    {
                        result[result.Count - 1] = new ArcRange(previous.Start, Mathf.Max(previous.End, arc.End), arc.Alpha);
                        continue;
                    }
                }
                result.Add(arc);
            }
            return result;
        }

        internal static void AddArcs(VertexHelper vh, IEnumerable<ArcRange> arcs, float innerRadius, float outerRadius,
            int width = 0, bool vanillaStyle = false)
        {
            if (width == -1) return;
            foreach (var arc in ConnectedArcs(arcs))
                AddArc(vh, arc.Start, arc.End, innerRadius, outerRadius, arc.Alpha, width, vanillaStyle);
        }

        internal static void AddRectangle(VertexHelper vh, Rect rect, float alpha, int width = 0, bool vanillaStyle = false)
        {
            if (width == -1 || rect.width <= 0f || rect.height <= 0f || alpha <= 0f) return;
            float inset = vanillaStyle ? 0f : Mathf.Min(Inset(width), Mathf.Min(rect.width, rect.height) / 2f);
            AddContour(vh, new[] {
                new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)
            }, alpha, inset <= 0f ? null : new[] {
                new Vector2(rect.xMin + inset, rect.yMin + inset), new Vector2(rect.xMax - inset, rect.yMin + inset),
                new Vector2(rect.xMax - inset, rect.yMax - inset), new Vector2(rect.xMin + inset, rect.yMax - inset)
            }, vanillaStyle);
        }

        internal static void AddArc(VertexHelper vh, float startAngle, float endAngle,
            float innerRadius, float outerRadius, float alpha, int width = 0, bool vanillaStyle = false)
        {
            if (width == -1 || endAngle <= startAngle || innerRadius <= 0f || outerRadius <= innerRadius || alpha <= 0f) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(endAngle - startAngle));
            var contour = new List<Vector2>(2 * (steps + 1));
            float inset = vanillaStyle ? 0f : Mathf.Min(Inset(width), (outerRadius - innerRadius) / 2f);
            // Clamp both dimensions so tiny/animated bands collapse to a center
            // line instead of crossing over or spilling into a neighboring band.
            float angleInset = Mathf.Min(inset / innerRadius / Mathf.Deg2Rad, (endAngle - startAngle) / 2f);
            var inside = inset > 0f ? new List<Vector2>(2 * (steps + 1)) : null;
            for (int i = 0; i <= steps; i++)
            {
                contour.Add(ArcPoint(startAngle + (endAngle - startAngle) * i / steps, innerRadius));
                inside?.Add(ArcPoint(startAngle + angleInset + (endAngle - startAngle - 2f * angleInset) * i / steps, innerRadius + inset));
            }
            for (int i = steps; i >= 0; i--)
            {
                contour.Add(ArcPoint(startAngle + (endAngle - startAngle) * i / steps, outerRadius));
                inside?.Add(ArcPoint(startAngle + angleInset + (endAngle - startAngle - 2f * angleInset) * i / steps, outerRadius - inset));
            }
            AddContour(vh, contour, alpha, inside, vanillaStyle,
                vanillaStyle ? innerRadius - VanillaThickness : 0f,
                vanillaStyle ? outerRadius + VanillaThickness : 0f);
        }

        private static Vector2 ArcPoint(float angle, float radius)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
        }

        private static void AddContour(VertexHelper vh, IReadOnlyList<Vector2> contour, float alpha,
            IReadOnlyList<Vector2>? inside = null, bool vanillaStyle = false,
            float gradientInnerRadius = 0f, float gradientOuterRadius = 0f)
        {
            float thickness = vanillaStyle ? VanillaThickness : Thickness;
            float opacity = alpha * (vanillaStyle ? VanillaOpacity : 1f);
            // Sample side walls too so the intermediate gradient stops remain
            // visible between the red top and blue bottom.
            var sampled = new List<Vector2>();
            var sampledInside = inside != null ? new List<Vector2>() : null;
            for (int i = 0; i < contour.Count; i++)
            {
                Vector2 a = contour[i], b = contour[(i + 1) % contour.Count];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 3f));
                for (int j = 0; j < steps; j++)
                {
                    sampled.Add(Vector2.Lerp(a, b, (float)j / steps));
                    if (inside != null)
                        sampledInside!.Add(Vector2.Lerp(inside[i], inside[(i + 1) % inside.Count], (float)j / steps));
                }
            }
            contour = sampled;
            var points = new List<Vector2>();
            var normals = new List<Vector2>();
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < contour.Count; i++)
            {
                Vector2 point = contour[i];
                Vector2 incoming = (point - contour[(i + contour.Count - 1) % contour.Count]).normalized;
                Vector2 outgoing = (contour[(i + 1) % contour.Count] - point).normalized;
                Vector2 before = new Vector2(incoming.y, -incoming.x);
                Vector2 after = new Vector2(outgoing.y, -outgoing.x);
                float turn = incoming.x * outgoing.y - incoming.y * outgoing.x;
                // AJB rounds exposed corners; Vanilla uses native square/miter
                // joins. Sampled arcs retain the exact concave inner offset.
                int cornerSteps = !vanillaStyle && turn > .2f ? 5 : 0;
                for (int j = 0; j <= cornerSteps; j++)
                {
                    Vector2 offset = cornerSteps > 0
                        ? Vector2.Lerp(before, after, (float)j / cornerSteps).normalized
                        : (before + after) / Mathf.Max(.001f, 1f + Vector2.Dot(before, after));
                    points.Add(point);
                    normals.Add(offset);
                    float y = point.y + offset.y * thickness;
                    bottom = Mathf.Min(bottom, y);
                    top = Mathf.Max(top, y);
                }
            }

            int start = vh.currentVertCount;
            int stride = vanillaStyle ? 2 : 3;
            for (int i = 0; i < points.Count; i++)
            {
                AddVertex(vh, points[i], bottom, top, opacity, gradientInnerRadius, gradientOuterRadius);
                AddVertex(vh, points[i] + normals[i] * thickness, bottom, top, opacity,
                    gradientInnerRadius, gradientOuterRadius);
                if (!vanillaStyle)
                    AddVertex(vh, points[i] + normals[i] * (thickness + Feather), bottom, top, 0f);
            }
            for (int i = 0; i < points.Count; i++)
            {
                int a = start + i * stride;
                int b = start + ((i + 1) % points.Count) * stride;
                Quad(vh, a, a + 1, b + 1, b);
                if (!vanillaStyle) Quad(vh, a + 1, a + 2, b + 2, b + 1);
            }

            // Append an inward strip after the unchanged legacy outline. Its
            // inner corners meet exactly, without reversed rounded offsets.
            if (sampledInside == null) return;
            start = vh.currentVertCount;
            for (int i = 0; i < contour.Count; i++)
            {
                AddVertex(vh, sampledInside[i], bottom, top, alpha);
                AddVertex(vh, contour[i], bottom, top, alpha);
            }
            for (int i = 0; i < contour.Count; i++)
            {
                int a = start + i * 2;
                int b = start + ((i + 1) % contour.Count) * 2;
                Quad(vh, a, a + 1, b + 1, b);
            }
        }

        private static void AddVertex(VertexHelper vh, Vector2 position, float bottom, float top, float alpha,
            float gradientInnerRadius = 0f, float gradientOuterRadius = 0f)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            // Native curved artwork follows the radius: red outside, blue
            // inside. AJB's original gradient continues to follow global Y.
            vertex.color = gradientOuterRadius > gradientInnerRadius
                ? BorderColor(Vector2.Distance(position, new Vector2()), gradientInnerRadius, gradientOuterRadius, alpha)
                : BorderColor(position.y, bottom, top, alpha);
            vh.AddVert(vertex);
        }

        private static void Quad(VertexHelper vh, int a, int b, int c, int d)
        {
            vh.AddTriangle(a, b, c);
            vh.AddTriangle(a, c, d);
        }
    }
}
