using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static void Show(DetailedResults results, string text)
    {
        results.textComponent.text = text;
        typeof(XPerfectResultsColor).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { results, 0 });
    }
    private static UIVertex[] Quad(Color32 color, float x = 0, float height = 20)
    {
        UIVertex V(float px, float py, float u, float v) => new UIVertex {
            position = new Vector3(px + x, py, 0), color = color,
            uv0 = new Vector4(u, v, 0, 0), uv1 = new Vector4(u, v, 1, 1),
            uv2 = new Vector4(u, v, 2, 2), uv3 = new Vector4(u, v, 3, 3)
        };
        var tl = V(0, height, .2f, .8f); var tr = V(10, height, .4f, .8f);
        var br = V(10, 0, .4f, .3f); var bl = V(0, 0, .2f, .3f);
        return new[] { tl, tr, br, br, bl, tl };
    }
    private static void Main()
    {
        const string native = "Perfect: <color=#00ff00>9</color>     XPerfect: <color=#00ff00>123(4)</color>\nLate: <color=#ff0000>1</color>";
        var results = new DetailedResults();
        Show(results, native);
        string marked = results.textComponent.text;
        Check(marked == native.Replace("<color=#00ff00>123(4)", "<color=#BBBAFF>123(4)"), "Only the XPerfect count span is marked");
        var effect = results.textComponent.GetComponent<XPerfectResultsGradient>()!;
        Check(effect != null && effect.enabled, "Mesh effect is attached and active");

        var glyph = Quad(new Color32(187, 186, 255, 128));
        var neighbor = Quad(new Color32(0, 255, 0, 128), 30);
        var shadow = Quad(new Color32(0, 0, 0, 60));
        var mesh = new VertexHelper();
        mesh.Stream.AddRange(shadow); mesh.Stream.AddRange(glyph); mesh.Stream.AddRange(neighbor);
        effect!.ModifyMesh(mesh);
        Check(mesh.Stream.Take(6).SequenceEqual(shadow), "Existing shadow geometry stays intact");
        Check(mesh.Stream.TakeLast(6).SequenceEqual(neighbor), "Other judgements keep geometry, UVs and colors");
        var face = mesh.Stream.Skip(mesh.Stream.Count - 12).Take(6).ToArray();
        Check(face.Select(v => v.position).SequenceEqual(glyph.Select(v => v.position)), "Original glyph shape is unchanged");
        Check(face.Select(v => v.uv0).SequenceEqual(glyph.Select(v => v.uv0)), "Original font atlas coordinates are unchanged");
        Check(face.All(v => v.color.r == 255 && v.color.g == 255 && v.color.b == 255 && v.color.a == 128), "Face is white and retains alpha");
        var border = mesh.Stream.Skip(6).Take(mesh.Stream.Count - 18).ToArray();
        Check(border.Length > 0 && border.All(v => v.color.a == 128), "Border retains fade alpha");
        Check(border.Min(v => v.position.x) < 0 && border.Max(v => v.position.x) > 10
            && border.Min(v => v.position.y) < 0 && border.Max(v => v.position.y) > 20, "Border surrounds all sides");
        Check(border.All(v => v.uv0.x >= .2f && v.uv0.x <= .4f && v.uv0.y >= .3f && v.uv0.y <= .8f), "Sliced glyphs stay inside their atlas rectangle");
        var colors = border.Select(v => v.color).Distinct().OrderBy(c => c.r).ToArray();
        Check(colors.Length == 5, "All five pastel stops survive within one digit");
        Check(border.Where(v => v.uv0.y > .79f).All(v => v.color.r > v.color.b), "Top is warm red");
        Check(border.Where(v => v.uv0.y < .31f).All(v => v.color.b > v.color.r), "Bottom is blue");
        Check(border.All(v => v.uv1.z == 1 && v.uv2.z == 2 && v.uv3.z == 3), "Additional UV channels survive slicing");

        AccurateJudgementBar.Main.settings!.BlueXPerfectResults = false;
        XPerfectResultsColor.Refresh();
        Check(results.textComponent.text == native && !effect.enabled, "Setting off restores original rich text");
        AccurateJudgementBar.Main.settings.BlueXPerfectResults = true;
        XPerfectResultsColor.Refresh();
        Check(results.textComponent.text == marked && effect.enabled, "Setting on reapplies current results");
        Check(results.textComponent.gameObject.Components.OfType<XPerfectResultsGradient>().Count() == 1, "Toggling reuses one effect");
        AccurateJudgementBar.Main.is_mod_enable = false;
        XPerfectResultsColor.Refresh();
        Check(results.textComponent.text == native && !effect.enabled, "Disabling mod restores native output");
        AccurateJudgementBar.Main.is_mod_enable = true;
        XPerfectResultsColor.Refresh();

        const string second = "XPerfect: <color=#00ff00>0</color>";
        Show(results, second);
        Check(results.textComponent.text.Contains(">0</color>"), "Co-op switches to new player's count");
        AccurateJudgementBar.Main.settings.BlueXPerfectResults = false;
        XPerfectResultsColor.Refresh();
        Check(results.textComponent.text == second, "Restoration uses current player's results");
        AccurateJudgementBar.Main.settings.BlueXPerfectResults = true;
        XPerfectResultsColor.Refresh();
        results.textComponent.text = "";
        XPerfectResultsColor.Refresh();
        Check(results.textComponent.text == "" && !effect.enabled, "Retry/cleared results are never resurrected");
        Show(results, native);
        results.textComponent.text = "replacement from game or another mod";
        var untouched = new VertexHelper(); untouched.Stream.AddRange(glyph);
        effect.ModifyMesh(untouched);
        Check(untouched.Stream.SequenceEqual(glyph), "Stale result ownership never colors unrelated meshes");

        Show(results, native);
        var replacement = new DetailedResults();
        Show(replacement, second);
        Check(results.textComponent.text == native && !effect.enabled, "UI replacement restores and disables previous effect");
        Check(replacement.textComponent.GetComponent<XPerfectResultsGradient>()!.enabled, "Rebuilt UI receives its own effect");
        RDString.Label = "完美無暇";
        Show(replacement, "早完美: <color=#123456>1</color>\n完美無暇: <color=#ABCDEF>789</color>");
        Check(replacement.textComponent.text.Contains("完美無暇: <color=#BBBAFF>789</color>"), "Localized labels and multiline results are supported");
        replacement.textComponent.supportRichText = false;
        XPerfectResultsColor.Refresh();
        Check(!replacement.textComponent.text.Contains("#BBBAFF"), "Rich text disabled does not expose marker");
        foreach (string input in new[] { "", "Perfect: <color=#fff>1</color>", "XPerfect: 12", "XPerfect: <color=#fff", "XPerfect: <color=#fff>1", "XPerfect: <color=#fff><b>1</b></color>" })
            Check(XPerfectResultsColor.Colorize(input, "XPerfect: ") == input, "Unrecognized results remain untouched: " + input);
        DetailedResultsTests.Run();
        CompletionResultsTests.Run();
        VisibilityRegressionTests.Run();
        Console.WriteLine($"ResultsGradient: {checks} checks passed.");
    }
}
