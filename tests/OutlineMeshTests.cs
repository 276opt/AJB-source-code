using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class OutlineMeshTests
{
    internal static void Run(bool preview)
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Outline mesh: " + message); }
        var separate = new[] { new XPerfectOutlineMesh.ArcRange(-50, -45, 1), new XPerfectOutlineMesh.ArcRange(45, 50, 1) };
        Check(XPerfectOutlineMesh.ConnectedArcs(separate).Count == 2, "separated white regions keep separate outlines");
        var mesh = new VertexHelper();
        XPerfectOutlineMesh.AddArcs(mesh, separate, 147, 159);
        Check(mesh.Vertices.Count > 0 && mesh.Vertices.All(v => Math.Abs(v.position.x) > 100), "no border vertices cross the intervening green/yellow arc");
        Check(mesh.Vertices.All(v => float.IsFinite(v.position.x) && float.IsFinite(v.position.y)), "finite vertices");
        Check(mesh.Indices.All(i => i >= 0 && i < mesh.Vertices.Count), "valid triangle indices");
        var touching = new[] { new XPerfectOutlineMesh.ArcRange(0, 20, 1), new XPerfectOutlineMesh.ArcRange(-20, 0, 1) };
        var connected = XPerfectOutlineMesh.ConnectedArcs(touching);
        Check(connected.Count == 1 && connected[0].Start == -20 && connected[0].End == 20, "touching halves merge without a center seam");
        var merged = new VertexHelper();
        var direct = new VertexHelper();
        XPerfectOutlineMesh.AddArcs(merged, touching, 147, 159);
        XPerfectOutlineMesh.AddArc(direct, -20, 20, 147, 159, 1);
        Check(merged.Vertices.Count == direct.Vertices.Count && merged.Indices.SequenceEqual(direct.Indices), "merged mesh matches a single continuous outline");
        Check(XPerfectOutlineMesh.ConnectedArcs(new[] { new XPerfectOutlineMesh.ArcRange(0, 0, 1), new XPerfectOutlineMesh.ArcRange(-10, 10, 0) }).Count == 0,
            "empty and invisible bands have no outline");

        void CheckWidths(Action<VertexHelper, int> draw, Func<UIVertex, bool> inside, string shape)
        {
            var thin = new VertexHelper();
            draw(thin, 0);
            float previousArea = Area(thin);
            foreach (int width in new[] { 1, 2 })
            {
                var thick = new VertexHelper();
                draw(thick, width);
                Check(thick.Vertices.Take(thin.Vertices.Count).SequenceEqual(thin.Vertices) &&
                    thick.Indices.Take(thin.Indices.Count).SequenceEqual(thin.Indices), shape + ": outer silhouette, colors and feather stay identical");
                Check(thick.Vertices.Skip(thin.Vertices.Count).All(inside), shape + ": extra thickness stays inside the white band");
                Check(thick.Vertices.All(v => float.IsFinite(v.position.x) && float.IsFinite(v.position.y)), shape + ": finite inset");
                Check(thick.Indices.All(i => i >= 0 && i < thick.Vertices.Count), shape + ": valid inset indices");
                float area = Area(thick);
                Check(area >= previousArea - .01f && area > Area(thin), shape + ": increasing thickness adds coverage");
                previousArea = area;
                for (int i = thin.Indices.Count; i < thick.Indices.Count; i += 3)
                    Check(SignedArea(thick, i) >= -.001f, shape + ": no inverted inset triangles");
            }
            var invalid = new VertexHelper();
            draw(invalid, 99);
            Check(invalid.Vertices.SequenceEqual(thin.Vertices) && invalid.Indices.SequenceEqual(thin.Indices), shape + ": invalid setting uses Thin");
        }
        foreach (float bandWidth in new[] { 40f, 2f, .01f })
            CheckWidths((vh, width) => XPerfectOutlineMesh.AddRectangle(vh, new Rect(-bandWidth / 2, -6, bandWidth, 12), .4f, width),
                v => Math.Abs(v.position.x) <= bandWidth / 2 + .0001f && Math.Abs(v.position.y) <= 6.0001f, "rectangle " + bandWidth);
        foreach (var range in new[] { (-20f, 20f), (45f, 50f), (-50f, -45f), (0f, .01f) })
            CheckWidths((vh, width) => XPerfectOutlineMesh.AddArc(vh, range.Item1, range.Item2, 147, 159, .4f, width),
                v => {
                    float radius = MathF.Sqrt(v.position.x * v.position.x + v.position.y * v.position.y);
                    float angle = MathF.Atan2(v.position.x, v.position.y) / Mathf.Deg2Rad;
                    return radius >= 146.99f && radius <= 159.001f && angle >= range.Item1 - .001f && angle <= range.Item2 + .001f;
                }, "arc " + range);
        foreach (int width in new[] { 1, 2 })
        {
            var joined = new VertexHelper();
            var single = new VertexHelper();
            XPerfectOutlineMesh.AddArcs(joined, touching, 147, 159, width);
            XPerfectOutlineMesh.AddArc(single, -20, 20, 147, 159, 1, width);
            Check(joined.Vertices.SequenceEqual(single.Vertices) && joined.Indices.SequenceEqual(single.Indices), "thicker touching halves have no center seam");
        }
        Console.WriteLine($"Passed {checks} production outline mesh checks.");
        if (preview) WritePreview();
    }

    private static float SignedArea(VertexHelper vh, int index)
    {
        var a = vh.Vertices[vh.Indices[index]].position;
        var b = vh.Vertices[vh.Indices[index + 1]].position;
        var c = vh.Vertices[vh.Indices[index + 2]].position;
        return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) / 2f;
    }

    private static float Area(VertexHelper vh)
    {
        float area = 0;
        for (int i = 0; i < vh.Indices.Count; i += 3) area += Math.Abs(SignedArea(vh, i));
        return area;
    }

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        foreach (var p in new[] { a, b, c, d }) vh.AddVert(new UIVertex { position = p, color = color });
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }
    private static Vector2 Arc(float angle, float radius) => new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);

    private static void WritePreview()
    {
        bool competitive = Persistence.enableCompetitiveMode;
        var preset = Persistence.hitMarginPerfectTick;
        Persistence.enableCompetitiveMode = true;
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        try
        {
            var panels = new List<object>();
            var stylePanels = new List<object>();
            foreach (bool vanillaStyle in new[] { false, true })
            foreach (int row in new[] { 0, 1 })
            foreach (bool curved in new[] { false, true })
            {
                var context = row == 0
                    ? new JudgementContext(85f, Difficulty.Normal, 1f, 1f, .25f, true)
                    : new JudgementContext(600f, Difficulty.Normal, 1f, 2f, 1f, false);
                var bands = JudgementApi.Evaluate(context);
                var vh = new VertexHelper();
                if (curved)
                {
                    var outlines = new List<XPerfectOutlineMesh.ArcRange>();
                    foreach (var slice in MeterBandLayout.CurvedSlices(bands))
                    {
                        float from = slice.Start / 2.5f, to = slice.End / 2.5f;
                        int steps = Math.Max(1, (int)Math.Ceiling(to - from));
                        for (int i = 0; i < steps; i++)
                        {
                            float a = from + (to - from) * i / steps, b = from + (to - from) * (i + 1) / steps;
                            Quad(vh, Arc(a, 147), Arc(a, 159), Arc(b, 159), Arc(b, 147), slice.XPerfect ? new Color(1, 1, 1) : slice.Color);
                        }
                        if (slice.XPerfect) outlines.Add(new XPerfectOutlineMesh.ArcRange(from, to, 1));
                    }
                    XPerfectOutlineMesh.AddArcs(vh, outlines, 147, 159, 0, vanillaStyle);
                }
                else
                {
                    foreach (var band in bands)
                    {
                        float left = -band.Width / 2f, right = band.Width / 2f;
                        Quad(vh, new Vector2(left, -6), new Vector2(right, -6), new Vector2(right, 6), new Vector2(left, 6),
                            band.Id == JudgementApi.Blue ? new Color(1, 1, 1) : band.Color);
                        if (band.Id == JudgementApi.Blue)
                            XPerfectOutlineMesh.AddRectangle(vh, new Rect(left, -6, band.Width, 12), 1, 0, vanillaStyle);
                    }
                }
                var vertices = vh.Vertices.Select(v => new[] { v.position.x, v.position.y, v.color.r, v.color.g, v.color.b, v.color.a });
                if (!vanillaStyle) panels.Add(new { row, curved, vertices, indices = vh.Indices });
                stylePanels.Add(new { row, curved, vanillaStyle, vertices, indices = vh.Indices });
            }
            string path = Path.Combine(AppContext.BaseDirectory, "native-display-preview.json");
            File.WriteAllText(path, JsonSerializer.Serialize(panels));
            Console.WriteLine("Preview meshes: " + path);
            string stylePath = Path.Combine(AppContext.BaseDirectory, "native-style-preview.json");
            File.WriteAllText(stylePath, JsonSerializer.Serialize(stylePanels));
            Console.WriteLine("Style preview meshes: " + stylePath);
        }
        finally { Persistence.hitMarginPerfectTick = preset; Persistence.enableCompetitiveMode = competitive; }
    }
}
