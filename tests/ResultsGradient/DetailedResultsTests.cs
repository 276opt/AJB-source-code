using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class DetailedResultsTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("DetailedResults: " + message);
    }
    private static void Show(DetailedResults results, string native, int player = 0)
    {
        results.textComponent.text = native;
        typeof(XPerfectResultsColor).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { results, player });
    }
    private static Image? Background(DetailedResultsView view) => (Image?)typeof(DetailedResultsView)
        .GetField("background", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
    internal static void Run()
    {
        var settings = AccurateJudgementBar.Main.settings = new Settings();
        AccurateJudgementBar.Main.is_mod_enable = true;
        RDString.Label = "XPerfect";
        var tracker = ADOBase.playerManager!.players[0].marginTracker;
        tracker.Counts[HitMargin.PerfectMinus] = 2;
        tracker.Counts[HitMargin.PerfectPlus] = 7;
        tracker.Counts[HitMargin.XPerfect] = 123;
        tracker.Counts[HitMargin.Auto] = 4;
        var snapshot = tracker.Counts.ToArray();
        const string native = "Perfect: <color=#00ff00>9</color>     XPerfect: <color=#00ff00>123(4)</color>\nEPerfect: <color=#ffff00>1</color>     LPerfect: <color=#ffff00>3</color>\nMiss: <color=#ff0000>5</color>\nXAccuracy: 99.12%     XScore: 1234 (MAX-12)\nCheckpoints: 2\n";
        const string signed = "Perfect-: <color=#00FF00>2</color>     XPerfect: <color=#BBBAFF>123(4)</color>     Perfect+: <color=#00FF00>7</color>\n";
        var results = new DetailedResults();
        var text = results.textComponent;
        text.color = new Color(1, 1, 1, 0);
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(1, 1, 1, 0);
        // Modern uGUI emits exactly the glyph quads, without an extra sentinel quad.
        text.cachedTextGenerator.verts.AddRange(new[] {
            new UIVertex { position = new(-80, 20, 0) }, new UIVertex { position = new(80, 20, 0) },
            new UIVertex { position = new(80, -40, 0) }, new UIVertex { position = new(-80, -40, 0) },
            new UIVertex { position = new(90, 20, 0) }, new UIVertex { position = new(100, 20, 0) },
            new UIVertex { position = new(100, -40, 0) }, new UIVertex { position = new(90, -40, 0) } });

        Persistence.hitMarginPerfectText = HitMarginPerfectTextPreset.ShowXPerfect;
        Show(results, native);
        Check(text.text.StartsWith(signed), "Unsigned XPerfect preset displays true directional counts and Auto");
        Check(text.text.Substring(signed.Length) == native.Substring(native.IndexOf('\n') + 1), "Accuracy, score, misses and checkpoint rows are unchanged");
        Check(tracker.Counts.SequenceEqual(snapshot), "All native statistics remain untouched");
        var view = text.GetComponent<DetailedResultsView>()!;
        Check(view.enabled && !results.hudText.changeColor && results.hudText.enabled, "Color isolation preserves HUD visibility updates");
        Check(text.color == RDConstants.data.worldColourScheme[0].colourText
            && shadow.effectColor == RDConstants.data.worldColourScheme[0].colourTextShadow, "Zero-alpha level text/shadow use default opaque text");
        var bg = Background(view)!;
        Check(bg.gameObject.activeSelf && !bg.raycastTarget && bg.color == new Color(0, 0, 0, .6f), "Black translucent panel does not intercept input");
        Check(bg.rectTransform.sizeDelta.x == 204 && bg.rectTransform.sizeDelta.y == 76
            && bg.rectTransform.localPosition.x == 10 && bg.rectTransform.localPosition.y == -10, "Panel covers actual glyph bounds with padding");
        text.enabled = false;
        view.Refresh();
        Check(!bg.gameObject.activeSelf, "No-HUD hides the panel along with the Text");
        text.enabled = true;
        text.canvasRenderer.Alpha = .5f;
        view.Refresh();
        Check(bg.color.a == .3f, "Panel follows result fade");
        text.canvasRenderer.Alpha = 1;

        settings.DetailedResultsSignedPerfects = false;
        XPerfectResultsColor.Refresh();
        Check(!text.text.Contains("Perfect-:") && view.enabled && bg.gameObject.activeSelf, "Directional switch is independent of background and color isolation");
        settings.DetailedResultsBackground = false;
        XPerfectResultsColor.Refresh();
        Check(!bg.gameObject.activeSelf && text.color.a == 1 && !results.hudText.changeColor, "Background switch is independent of colors");
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(!view.enabled && results.hudText.changeColor && text.color.a == 0 && shadow.effectColor.a == 0, "Turning off color isolation restores captured text/shadow");

        settings.DetailedResultsSignedPerfects = settings.DetailedResultsBackground = settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        settings.EnchantedDetailedResults = false;
        XPerfectResultsColor.Refresh();
        Check(!text.text.Contains("Perfect-:") && text.text.Contains("#BBBAFF") && !view.enabled
            && !bg.gameObject.activeSelf && results.hudText.changeColor, "Master switch restores enhancements and preserves independent gradient setting");
        settings.EnchantedDetailedResults = true;
        XPerfectResultsColor.Refresh();
        AccurateJudgementBar.Main.is_mod_enable = false;
        XPerfectResultsColor.Refresh();
        Check(text.text == native && !view.enabled && !bg.gameObject.activeSelf && text.color.a == 0, "Mod disable restores all native output");
        AccurateJudgementBar.Main.is_mod_enable = true;
        XPerfectResultsColor.Refresh();
        Check(text.text.StartsWith(signed) && view.enabled && Background(view) == bg
            && text.gameObject.Components.OfType<DetailedResultsView>().Count() == 1, "Reenable reuses one view and background");

        settings.BlueXPerfectResults = false;
        foreach (var preset in Enum.GetValues<HitMarginPerfectTextPreset>())
        {
            Persistence.hitMarginPerfectText = preset;
            NativeXPerfect.Enabled = false;
            Show(results, native);
            Check(text.text.Contains("Perfect-:") == preset.IsShowXPerfect(), "Text preset is read without mutation: " + preset);
            Check(Persistence.hitMarginPerfectText == preset, "Display does not change official text preset");
        }
        Persistence.hitMarginPerfectText = HitMarginPerfectTextPreset.Default;
        NativeXPerfect.Enabled = true;
        const string defaultNative = "EPerfect: <color=#ffff00>1</color>     Perfect: <color=#00ff00>132(4)</color>     LPerfect: <color=#ffff00>3</color>\nAccuracy: 99.00%\n";
        Show(results, defaultNative);
        Check(text.text.StartsWith("Perfect-:") && text.text.Contains("XPerfect: <color=#00FF00>123(4)</color>")
            && text.text.Contains("\nEPerfect: <color=#ffff00>1</color>     LPerfect: <color=#ffff00>3</color>\nAccuracy: 99.00%"), "XPerfect ticks plus default text keep E/L counts and add directional row");
        const string alreadySigned = "Perfect-: <color=#00ff00>2</color>     XPerfect: <color=#00ff00>123(4)</color>     Perfect+: <color=#00ff00>7</color>\nOther: 1";
        Show(results, alreadySigned);
        Check(text.text == alreadySigned, "Native signed output is never duplicated");
        foreach (var unknown in new[] { "Perfect: 9", "Perfect: <color=#fff><b>9</b></color>", "Unknown: <color=#fff>1</color>" })
        {
            Show(results, unknown);
            Check(text.text == unknown, "Unknown markup is left intact");
        }
        RDString.Label = "完美無暇";
        RDString.Minus = "完美-"; RDString.Plus = "完美+"; RDString.Perfect = "完美";
        Show(results, "完美: <color=#00ff00>9</color>     完美無暇: <color=#00ff00>123(4)</color>\n準確率: 99.12%");
        Check(text.text.StartsWith("完美-: <color=#00FF00>2</color>     完美無暇: <color=#00FF00>123(4)</color>     完美+: <color=#00FF00>7</color>\n準確率: 99.12%"), "Localized labels split correctly and keep the native accuracy row");
        RDString.Label = "XPerfect";
        RDString.Minus = "Perfect-"; RDString.Plus = "Perfect+"; RDString.Perfect = "Perfect";
        text.supportRichText = false;
        Show(results, native);
        Check(text.text == native, "Rich text disabled never adds display markup");
        text.supportRichText = true;

        var zeros = new DetailedResultsCounts(new scrMarginTracker());
        string zeroResults = EnchantedDetailedResults.SplitPerfects(native, zeros);
        Check(zeroResults.StartsWith("Perfect-: <color=#00FF00>0</color>     XPerfect: <color=#00FF00>0</color>     Perfect+: <color=#00FF00>0</color>"), "Zero directional counts remain visible without a synthetic Auto count");

        var coopTracker = ADOBase.playerManager.players[1].marginTracker;
        coopTracker.Counts[HitMargin.PerfectMinus] = 8;
        coopTracker.Counts[HitMargin.PerfectPlus] = 1;
        coopTracker.Counts[HitMargin.XPerfect] = 42;
        results.hudText.enabled = false; // Original co-op ShowForPlayer does this.
        text.color = new Color(.3f, .4f, .5f, .7f);
        Show(results, native, 1);
        Check(text.text.Contains("Perfect-: <color=#00FF00>8</color>") && text.text.Contains(">42</color>"), "Co-op uses the displayed player's tracker");
        coopTracker.Counts[HitMargin.PerfectMinus] = 99;
        settings.DetailedResultsSignedPerfects = false;
        XPerfectResultsColor.Refresh();
        settings.DetailedResultsSignedPerfects = true;
        XPerfectResultsColor.Refresh();
        Check(text.text.Contains(">8</color>"), "Changing tracker after Show cannot rewrite a cached result snapshot");
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(text.color == new Color(.3f, .4f, .5f, .7f) && !results.hudText.enabled, "Co-op restores current planet color without enabling native HUD");
        settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        text.color = Color.white;
        Show(results, native, 0);
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(text.color == Color.white, "Co-op white planet does not restore the previous player's color");

        results.hudText.enabled = true;
        settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        scrVfx.instance = new scrVfx { currentColourScheme = new ColourScheme {
            colourText = new Color(.2f, .5f, .1f, .4f), colourTextShadow = new Color(.8f, .3f, 0, .2f) } };
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(text.color == scrVfx.instance.currentColourScheme.colourText
            && shadow.effectColor == scrVfx.instance.currentColourScheme.colourTextShadow, "Color restore uses the latest live level scheme");
        scrVfx.instance = null;

        settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        text.text = "";
        view.Refresh();
        Check(!view.enabled && !bg.gameObject.activeSelf && results.hudText.changeColor, "Retry/clear releases HUD and hides sibling background");
        Show(results, native);
        var rebuilt = new DetailedResults();
        Show(rebuilt, native);
        Check(text.text == native && !view.enabled && !bg.gameObject.activeSelf, "UI replacement restores and cleans up old display");
        var rebuiltView = rebuilt.textComponent.GetComponent<DetailedResultsView>()!;
        rebuilt.textComponent.text = "third-party replacement";
        XPerfectResultsColor.Refresh();
        Check(rebuilt.textComponent.text == "third-party replacement" && !rebuiltView.enabled, "External text replacement is not overwritten");

        GameObject.ThrowOnImage = true;
        var broken = new DetailedResults();
        broken.textComponent.color = new Color(1, 1, 1, 0);
        broken.textComponent.cachedTextGenerator.verts.AddRange(text.cachedTextGenerator.verts);
        Show(broken, native);
        Check(!broken.textComponent.GetComponent<DetailedResultsView>()!.enabled && broken.hudText.changeColor
            && broken.textComponent.color.a == 0, "Background creation failure restores partially applied HUD/color changes");
        GameObject.ThrowOnImage = false;
        XPerfectResultsColor.Refresh();
        Check(broken.textComponent.GetComponent<DetailedResultsView>()!.enabled, "Display can recover on explicit refresh");
        tracker.ThrowOnRead = true;
        Show(broken, native);
        Check(broken.textComponent.text == native, "Tracker read failure falls back to unmodified native results");
        tracker.ThrowOnRead = false;
        AccurateJudgementBar.Main.is_mod_enable = false;
        XPerfectResultsColor.Refresh();
        Check(broken.textComponent.text == native && broken.hudText.changeColor
            && !Background(broken.textComponent.GetComponent<DetailedResultsView>()!)!.gameObject.activeSelf, "Disable also cleans up a recovered failure");
        Console.WriteLine($"EnchantedDetailedResults: {checks} checks passed.");
    }
}
