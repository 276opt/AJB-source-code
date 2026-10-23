using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class VanillaXPerfectStyleTests
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Vanilla XPerfect style: " + message); }
        bool SameMesh(VertexHelper a, VertexHelper b) => a.Vertices.SequenceEqual(b.Vertices) && a.Indices.SequenceEqual(b.Indices);
        void ValidMesh(VertexHelper mesh, string shape)
        {
            Check(mesh.Vertices.Count > 0, shape + " generates a visible outline");
            Check(mesh.Vertices.All(v => float.IsFinite(v.position.x) && float.IsFinite(v.position.y) &&
                float.IsFinite(v.color.r) && float.IsFinite(v.color.g) && float.IsFinite(v.color.b) && float.IsFinite(v.color.a)),
                shape + " has finite coordinates and colors");
            Check(mesh.Indices.Count % 3 == 0 && mesh.Indices.All(i => i >= 0 && i < mesh.Vertices.Count),
                shape + " has valid triangle indices");
        }

        // Exercise the production effective-option helper without rewriting the
        // stored AJB thickness. Returning to AJB must recover even an Off choice.
        foreach (int savedWidth in new[] { -1, 0, 1, 2, 99 })
        {
            Check(XPerfectOutlineMesh.EffectiveWidth(false, false, savedWidth) == savedWidth &&
                XPerfectOutlineMesh.EffectiveWidth(false, true, savedWidth) == savedWidth,
                "AJB keeps its stored thickness independently of the Vanilla outline checkbox");
            Check(XPerfectOutlineMesh.EffectiveWidth(true, true, savedWidth) == 0,
                "Vanilla uses one fixed outline regardless of stored AJB thickness");
            Check(XPerfectOutlineMesh.EffectiveWidth(true, false, savedWidth) == -1,
                "Vanilla can turn its outline off independently");
        }

        void CheckShape(Action<VertexHelper, int, bool, float> draw, string shape)
        {
            var vanilla = new VertexHelper();
            draw(vanilla, 0, true, .5f);
            ValidMesh(vanilla, shape);
            Check(vanilla.Vertices.All(v => Math.Abs(v.color.a - .2f) < .00001f),
                shape + " multiplies the native 40% outline opacity by band alpha");
            foreach (int width in new[] { 1, 2, 99 })
            {
                var alternate = new VertexHelper();
                draw(alternate, width, true, .5f);
                Check(SameMesh(vanilla, alternate), shape + " ignores adjustable thickness in Vanilla style");
            }
            var off = new VertexHelper();
            draw(off, -1, true, .5f);
            Check(off.Vertices.Count == 0 && off.Indices.Count == 0, shape + " honors outline Off");
            var transparent = new VertexHelper();
            draw(transparent, 2, true, 0f);
            Check(transparent.Vertices.Count == 0 && transparent.Indices.Count == 0, shape + " skips fully transparent bands");
        }
        foreach (float width in new[] { 40f, 2f, .0001f })
        {
            var rect = new Rect(-width / 2, -6, width, 12);
            CheckShape((vh, thickness, vanilla, alpha) => XPerfectOutlineMesh.AddRectangle(vh, rect, alpha, thickness, vanilla),
                "rectangle " + width);
        }
        foreach (var range in new[] { (-20f, 20f), (45f, 50f), (-50f, -45f), (0f, .0001f) })
            CheckShape((vh, thickness, vanilla, alpha) => XPerfectOutlineMesh.AddArc(vh, range.Item1, range.Item2, 147, 159,
                alpha, thickness, vanilla), "arc " + range);

        var vanillaRect = new VertexHelper();
        XPerfectOutlineMesh.AddRectangle(vanillaRect, new Rect(-20, -6, 40, 12), 1, 2, true);
        Check(vanillaRect.Vertices.Min(v => v.position.x) == -22 && vanillaRect.Vertices.Max(v => v.position.x) == 22 &&
            vanillaRect.Vertices.Min(v => v.position.y) == -8 && vanillaRect.Vertices.Max(v => v.position.y) == 8,
            "native rectangle has a fixed 2 px exterior rim");
        Check(vanillaRect.Vertices.All(v => Math.Abs(v.position.x) >= 20 || Math.Abs(v.position.y) >= 6),
            "native thickness never adds an inset over the white fill");

        float Radius(UIVertex vertex) => MathF.Sqrt(vertex.position.x * vertex.position.x + vertex.position.y * vertex.position.y);
        var radial = new VertexHelper();
        XPerfectOutlineMesh.AddArc(radial, -50, 50, 147, 159, 1, 0, true);
        var innerBody = radial.Vertices.Where(v => Math.Abs(Radius(v) - 147) < .0001f).ToArray();
        var outerBody = radial.Vertices.Where(v => Math.Abs(Radius(v) - 159) < .0001f).ToArray();
        Check(innerBody.Length > 20 && outerBody.Length > 20 &&
            innerBody.Max(v => v.color.r) - innerBody.Min(v => v.color.r) < .00001f &&
            outerBody.Max(v => v.color.b) - outerBody.Min(v => v.color.b) < .00001f,
            "native curved gradient has the same color at equal radii across the arc");
        var innerEdge = radial.Vertices.OrderBy(Radius).First().color;
        var outerEdge = radial.Vertices.OrderByDescending(Radius).First().color;
        Check(Math.Abs(innerEdge.r - .6f) < .001f && Math.Abs(innerEdge.b - 1) < .001f &&
            Math.Abs(outerEdge.r - (.6f + .4f * 247 / 255)) < .001f &&
            Math.Abs(outerEdge.b - (.6f + .4f * 79 / 255)) < .001f,
            "native curved rim follows the sprite's blue inner edge and red outer edge");
        var legacy = new VertexHelper();
        XPerfectOutlineMesh.AddArc(legacy, -50, 50, 147, 159, 1);
        var legacyOuter = legacy.Vertices.Where(v => Math.Abs(Radius(v) - 159) < .0001f && v.color.a > .9f).ToArray();
        Check(legacyOuter.Max(v => v.color.b) - legacyOuter.Min(v => v.color.b) > .05f,
            "default AJB curved gradient retains its original global Y direction");
        foreach (int width in new[] { -1, 0, 1, 2, 99 })
        {
            var defaultRect = new VertexHelper();
            var explicitRect = new VertexHelper();
            XPerfectOutlineMesh.AddRectangle(defaultRect, new Rect(-20, -6, 40, 12), .5f, width);
            XPerfectOutlineMesh.AddRectangle(explicitRect, new Rect(-20, -6, 40, 12), .5f, width, false);
            Check(SameMesh(defaultRect, explicitRect), "omitted style preserves AJB rectangle behavior");
            var defaultArc = new VertexHelper();
            var explicitArc = new VertexHelper();
            XPerfectOutlineMesh.AddArc(defaultArc, -20, 20, 147, 159, .5f, width);
            XPerfectOutlineMesh.AddArc(explicitArc, -20, 20, 147, 159, .5f, width, false);
            Check(SameMesh(defaultArc, explicitArc), "omitted style preserves AJB curved behavior");
        }

        foreach (var ranges in new[] {
            new[] { new XPerfectOutlineMesh.ArcRange(-20, 0, .5f), new XPerfectOutlineMesh.ArcRange(0, 20, .5f) },
            new[] { new XPerfectOutlineMesh.ArcRange(-20, 10, .5f), new XPerfectOutlineMesh.ArcRange(0, 20, .5f) }
        })
        {
            var joined = new VertexHelper();
            var direct = new VertexHelper();
            XPerfectOutlineMesh.AddArcs(joined, ranges, 147, 159, 2, true);
            XPerfectOutlineMesh.AddArc(direct, -20, 20, 147, 159, .5f, 0, true);
            Check(SameMesh(joined, direct), "touching and overlapping transparent arcs merge without a doubled center seam");
        }
        var separated = new VertexHelper();
        XPerfectOutlineMesh.AddArcs(separated, new[] {
            new XPerfectOutlineMesh.ArcRange(-50, -45, .5f), new XPerfectOutlineMesh.ArcRange(45, 50, .5f)
        }, 147, 159, 0, true);
        Check(separated.Vertices.All(v => Math.Abs(v.position.x) > 100), "separate white arcs keep the intervening band visible");
        var noArcs = new VertexHelper();
        XPerfectOutlineMesh.AddArcs(noArcs, new[] { new XPerfectOutlineMesh.ArcRange(-20, 20, 1) }, 147, 159, -1, true);
        Check(noArcs.Vertices.Count == 0 && noArcs.Indices.Count == 0, "arc collection honors outline Off");

        var savedSettings = Main.settings;
        bool savedCompetitive = Persistence.enableCompetitiveMode;
        var savedPreset = Persistence.hitMarginPerfectTick;
        try
        {
            Main.settings = new Settings();
            Check(!Main.settings.VanillaXPerfectStyle && Main.settings.VanillaXPerfectOutline && Main.settings.XPerfectOutlineWidth == 1,
                "new settings begin with the existing AJB medium outline");

            var contexts = new[] {
                new JudgementContext(85, Difficulty.Normal, 1, 1, .25f, true),
                new JudgementContext(600, Difficulty.Strict, 2, 2, 1, false)
            };
            foreach (bool competitive in new[] { false, true })
            foreach (var preset in new[] { HitMarginPerfectTickPreset.Default, HitMarginPerfectTickPreset.ShowXPerfect,
                HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline })
            foreach (bool show in new[] { false, true })
            foreach (bool accurateWidths in new[] { false, true })
            foreach (var context in contexts)
            {
                Persistence.enableCompetitiveMode = competitive;
                Persistence.hitMarginPerfectTick = preset;
                Main.settings.ShowXPerfectJudgement = show;
                Main.settings.VanillaXPerfectStyle = false;
                var baseline = JudgementApi.Evaluate(context, accurateWidths);
                JudgementApi.TryGet(JudgementApi.Blue, out var definition);
                bool enabled = definition.Enabled(context);
                Main.settings.VanillaXPerfectStyle = true;
                Main.settings.VanillaXPerfectOutline = false;
                var vanilla = JudgementApi.Evaluate(context, accurateWidths);
                Check(baseline.Select(b => (b.Id, b.WindowMs, b.Width, b.Color)).SequenceEqual(
                    vanilla.Select(b => (b.Id, b.WindowMs, b.Width, b.Color))) && enabled == definition.Enabled(context),
                    "style and outline do not change API windows, widths, colors or enabled predicates");
                Check(Persistence.enableCompetitiveMode == competitive && Persistence.hitMarginPerfectTick ==
                    (competitive ? preset : HitMarginPerfectTickPreset.Default), "display style preserves the official preset");
            }

            Persistence.enableCompetitiveMode = true;
            Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
            Main.settings.ShowXPerfectJudgement = true;
            const string customId = "test.vanilla-style-inner";
            var customColor = new Color(.1f, .2f, .3f);
            JudgementApi.Register(new JudgementDefinition(customId, _ => 8f, _ => customColor));
            try
            {
                var slices = MeterBandLayout.CurvedSlices(JudgementApi.Evaluate(contexts[0]));
                var white = slices.Where(s => s.XPerfect).ToArray();
                Check(white.Length == 2 && white.All(s => s.End <= 0 || s.Start >= 0),
                    "a custom inner band splits the visible XPerfect arcs");
                Check(slices.Single(s => s.Start < 0 && s.End > 0).Color.Equals(customColor),
                    "Vanilla style keeps the custom center color");
                var outline = new VertexHelper();
                XPerfectOutlineMesh.AddArcs(outline, white.Select(s => new XPerfectOutlineMesh.ArcRange(s.Start / 2.5f,
                    s.End / 2.5f, s.Color.a)), 147, 159, 0, true);
                Check(outline.Vertices.All(v => Math.Abs(v.position.x) > 1), "split outlines leave the custom band center visible");
            }
            finally { JudgementApi.Remove(customId); }

            // Production straight-bar component: changing style must dirty its
            // mesh even when both styles resolve to the same integer width.
            var effect = new XPerfectGradient();
            effect.graphic.rectTransform.rect = new Rect(-20, -6, 40, 12);
            var update = typeof(XPerfectGradient).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Refresh() => update.Invoke(effect, null);
            VertexHelper Fill()
            {
                var mesh = new VertexHelper();
                foreach (var point in new[] { new Vector2(-20, -6), new Vector2(20, -6), new Vector2(20, 6), new Vector2(-20, 6) })
                    mesh.AddVert(new UIVertex { position = point, color = new Color(.2f, .4f, .6f, 1) });
                mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(0, 2, 3);
                return mesh;
            }
            Main.settings.XPerfectOutlineWidth = 0;
            Main.settings.VanillaXPerfectStyle = false;
            Refresh();
            int dirty = effect.graphic.DirtyCount;
            Refresh();
            Check(effect.graphic.DirtyCount == dirty, "stable straight-bar options do not rebuild the mesh every frame");
            Main.settings.VanillaXPerfectStyle = true;
            Main.settings.VanillaXPerfectOutline = true;
            Refresh();
            Check(effect.graphic.DirtyCount == dirty + 1, "style switch dirties straight-bar mesh with unchanged effective width");
            effect.graphic.color = new Color(1, 1, 1, .5f);
            var nativeMesh = Fill();
            effect.ModifyMesh(nativeMesh);
            Check(nativeMesh.Vertices.Skip(4).All(v => Math.Abs(v.color.a - .2f) < .00001f),
                "straight component applies native outline opacity");
            Main.settings.XPerfectOutlineWidth = 2;
            var storedThick = Fill();
            effect.ModifyMesh(storedThick);
            Check(SameMesh(nativeMesh, storedThick), "straight Vanilla mesh ignores the saved AJB thickness");
            Main.settings.VanillaXPerfectOutline = false;
            dirty = effect.graphic.DirtyCount;
            Refresh();
            Check(effect.graphic.DirtyCount == dirty + 1, "outline checkbox immediately dirties straight-bar mesh");
            var noOutline = Fill();
            effect.ModifyMesh(noOutline);
            Check(noOutline.Vertices.Count == 4 && noOutline.Indices.Count == 6, "outline Off retains the original white fill only");
            Check(noOutline.Vertices.All(v => v.color.r == 1 && v.color.g == 1 && v.color.b == 1),
                "outline Off retains the white XPerfect fill");
            Main.settings.VanillaXPerfectStyle = false;
            Refresh();
            Check(Main.settings.XPerfectOutlineWidth == 2 && !Main.settings.VanillaXPerfectOutline,
                "style switching preserves both independent saved outline choices");
            var restored = Fill();
            effect.ModifyMesh(restored);
            Check(restored.Vertices.Count > nativeMesh.Vertices.Count, "returning to AJB restores the saved thick outline");
        }
        finally
        {
            Main.settings = savedSettings;
            Persistence.hitMarginPerfectTick = savedPreset;
            Persistence.enableCompetitiveMode = savedCompetitive;
        }
        Console.WriteLine($"Passed {checks} Vanilla XPerfect style checks (production mesh, straight refresh and API isolation).");
    }
}
