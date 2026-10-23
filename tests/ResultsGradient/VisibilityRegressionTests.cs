using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class VisibilityRegressionTests
{
    private static int checks;
    private const string NativeResults = "Accuracy: 99.12%\nXAccuracy: 91.23%\nXScore: 1234 (MAX-12)\nCheckpoints: 2\n";

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("Results visibility: " + message);
    }

    private static void Show(DetailedResults results, int player = 0)
    {
        results.textComponent.text = NativeResults;
        typeof(XPerfectResultsColor).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { results, player });
    }

    private static Image Background(Text text) => (Image)typeof(DetailedResultsView)
        .GetField("background", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(text.GetComponent<DetailedResultsView>()!)!;

    private static TextGenerator BackgroundGenerator(DetailedResultsView view) => (TextGenerator)typeof(DetailedResultsView)
        .GetField("backgroundGenerator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;

    private static void LateUpdate(DetailedResultsView view) => typeof(DetailedResultsView)
        .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);

    private static UIVertex[] Glyphs() => new[] {
        new UIVertex { position = new(-80, 20, 0), color = new(12, 34, 56, 78), uv0 = new(.2f, .8f, 0, 0) },
        new UIVertex { position = new(80, 20, 0), color = new(12, 34, 56, 78), uv0 = new(.4f, .8f, 0, 0) },
        new UIVertex { position = new(80, -40, 0), color = new(12, 34, 56, 78), uv0 = new(.4f, .3f, 0, 0) },
        new UIVertex { position = new(-80, -40, 0), color = new(12, 34, 56, 78), uv0 = new(.2f, .3f, 0, 0) }
    };

    private static Text CompletionMessage(string message, Transform parent)
    {
        var text = new Text { text = message };
        text.rectTransform.SetParent(parent, false);
        text.gameObject.AddComponent<scrHUDText>();
        text.gameObject.AddComponent<Shadow>();
        text.cachedTextGenerator.verts.AddRange(Glyphs());
        return text;
    }

    private static void CheckPanelOrder(Text text, string context)
    {
        var background = Background(text);
        Check(background.rectTransform.parent == text.rectTransform.parent
            && background.rectTransform.GetSiblingIndex() + 1 == text.rectTransform.GetSiblingIndex(),
            context + ": the panel is immediately before its Text in the shared parent");
    }

    internal static void Run()
    {
        var previousSettings = AccurateJudgementBar.Main.settings;
        bool previousEnabled = AccurateJudgementBar.Main.is_mod_enable;
        var previousController = ADOBase.controller;
        var previousVfx = scrVfx.instance;
        var previousConstants = RDConstants.data;
        var previousPreset = Persistence.hitMarginPerfectText;
        bool previousXPerfect = NativeXPerfect.Enabled;
        try
        {
            AccurateJudgementBar.Main.settings = new Settings { BlueXPerfectResults = false, DetailedResultsSignedPerfects = false };
            AccurateJudgementBar.Main.is_mod_enable = true;
            ADOBase.controller = null;
            scrVfx.instance = null;
            RDConstants.data = new RDConstants.Data();
            Persistence.hitMarginPerfectText = HitMarginPerfectTextPreset.Default;
            NativeXPerfect.Enabled = false;
            BackgroundOrderAndNativeCache();
            FixedColorsAndNativeRestore();
            DynamicFontLifecycle();
        }
        finally
        {
            AccurateJudgementBar.Main.is_mod_enable = false;
            XPerfectResultsColor.Refresh();
            ADOBase.controller = null;
            CompletionResultsText.Refresh();
            AccurateJudgementBar.Main.settings = previousSettings;
            AccurateJudgementBar.Main.is_mod_enable = previousEnabled;
            ADOBase.controller = previousController;
            scrVfx.instance = previousVfx;
            RDConstants.data = previousConstants;
            Persistence.hitMarginPerfectText = previousPreset;
            NativeXPerfect.Enabled = previousXPerfect;
        }
        Console.WriteLine($"Results visibility regressions: {checks} checks passed.");
    }

    private static void BackgroundOrderAndNativeCache()
    {
        var parent = new GameObject("Original result parent").transform;
        var results = new DetailedResults();
        var text = results.textComponent;
        text.rectTransform.SetParent(parent, false);
        var spacer = new Text { text = "Unrelated sibling" };
        spacer.rectTransform.SetParent(parent, false);
        var nativeGenerator = text.cachedTextGenerator;
        nativeGenerator.verts.AddRange(Glyphs());
        var nativeGlyphs = nativeGenerator.verts.ToArray();
        int nativePopulates = nativeGenerator.PopulateCalls;
        // Rejecting Populate on the native cache must not stop AJB's own background generator.
        nativeGenerator.RejectPopulate = true;
        Show(results);
        var view = text.GetComponent<DetailedResultsView>()!;
        Check(view.enabled && Background(text).gameObject.activeSelf,
            "Native text generator rejection does not hide accuracy or its panel");
        CheckPanelOrder(text, "Initial background");
        Check(nativeGenerator.PopulateCalls == nativePopulates && nativeGenerator.verts.SequenceEqual(nativeGlyphs),
            "Initial background generation leaves native Populate state and all glyph data intact");

        // Reproduce a background already before the Text, with an intervening native sibling.
        spacer.rectTransform.SetSiblingIndex(text.rectTransform.GetSiblingIndex());
        Check(Background(text).rectTransform.GetSiblingIndex() + 1 != text.rectTransform.GetSiblingIndex(),
            "The fixture has an intervening sibling before refreshing");
        view.Refresh();
        CheckPanelOrder(text, "Panel moves down past an intervening sibling");
        Check(spacer.text == "Unrelated sibling" && spacer.enabled,
            "Repairing draw order preserves unrelated native text");

        var nextParent = new GameObject("Rebuilt result parent").transform;
        var nextSpacer = new Text { text = "New parent sibling" };
        nextSpacer.rectTransform.SetParent(nextParent, false);
        text.rectTransform.SetParent(nextParent, false);
        view.Refresh();
        CheckPanelOrder(text, "Native Text moves to another parent");
        Check(Background(text).gameObject.activeSelf && Background(text).rectTransform.sizeDelta.x == 184,
            "Reparented results keep a correctly measured panel");

        var controller = ADOBase.controller = new scrController {
            detailedResults = results,
            txtCongrats = CompletionMessage("恭喜!", nextParent),
            txtAllStrictClear = CompletionMessage("嚴格模式通關", nextParent),
            txtAprilCongrats = CompletionMessage("額外原版提示", nextParent)
        };
        controller.stateMachine.State = States.Won;
        CompletionResultsText.Refresh();
        var allTexts = new[] { text, controller.txtCongrats!, controller.txtAllStrictClear!, controller.txtAprilCongrats! };
        foreach (var item in allTexts) CheckPanelOrder(item, "Multiple result panels: " + item.text);
        // Reorder a native message while its old panel stays near another result Text.
        controller.txtCongrats!.rectTransform.SetSiblingIndex(0);
        foreach (var item in allTexts) item.GetComponent<DetailedResultsView>()!.Refresh();
        foreach (var item in allTexts)
        {
            CheckPanelOrder(item, "Native result siblings reordered: " + item.text);
            Check(Background(item).gameObject.activeSelf && item.enabled,
                "Every result stays visible after simultaneous backgrounds are reordered");
        }
        view.Refresh();
        view.Refresh();
        Check(ReferenceEquals(text.cachedTextGenerator, nativeGenerator)
            && nativeGenerator.PopulateCalls == nativePopulates && nativeGenerator.verts.SequenceEqual(nativeGlyphs),
            "Repeated and reparented background refreshes never replace, populate or mutate the native cache");
        Check(text.text == NativeResults && controller.txtCongrats.text == "恭喜!"
            && controller.txtAllStrictClear!.text == "嚴格模式通關" && controller.stateMachine.State == States.Won,
            "Order/cache repairs preserve accuracy, native completion wording and Won state");
        ADOBase.controller = null;
        CompletionResultsText.Refresh();
    }

    private static void FixedColorsAndNativeRestore()
    {
        var settings = AccurateJudgementBar.Main.settings!;
        settings.DetailedResultsDefaultColors = settings.DetailedResultsBackground = true;
        var pollutedDefault = RDConstants.data.worldColourScheme[0];
        pollutedDefault.colourText = new Color(.2f, .4f, .6f, 0);
        pollutedDefault.colourTextShadow = new Color(.8f, .3f, .1f, 0);
        var level = new ColourScheme {
            colourText = new Color(.6f, .2f, .4f, 0),
            colourTextShadow = new Color(.1f, .7f, .3f, 0)
        };
        scrVfx.instance = new scrVfx { currentColourScheme = level };
        var parent = new GameObject("Transparent level result parent").transform;
        var results = new DetailedResults();
        var text = results.textComponent;
        text.rectTransform.SetParent(parent, false);
        text.color = level.colourText;
        text.gameObject.AddComponent<Shadow>().effectColor = level.colourTextShadow;
        text.cachedTextGenerator.verts.AddRange(Glyphs());
        var controller = ADOBase.controller = new scrController {
            detailedResults = results,
            txtCongrats = CompletionMessage("恭喜!", parent),
            txtAllStrictClear = CompletionMessage("嚴格模式通關", parent),
            txtAprilCongrats = CompletionMessage("原版額外提示", parent)
        };
        controller.stateMachine.State = States.Won;
        var allTexts = new[] { text, controller.txtCongrats!, controller.txtAllStrictClear!, controller.txtAprilCongrats! };
        foreach (var item in allTexts.Skip(1))
        {
            item.color = level.colourText;
            item.GetComponent<Shadow>()!.effectColor = level.colourTextShadow;
        }
        var statistics = ADOBase.playerManager!.players.Select(p => p.marginTracker.Counts.ToArray()).ToArray();
        Show(results);
        foreach (var item in allTexts)
        {
            Check(item.color == Color.white && item.GetComponent<Shadow>()!.effectColor == new Color(0, 0, 0, .5f),
                "Transparent mutable default scheme cannot hide result text/shadow: " + item.text);
            Check(!item.GetComponent<scrHUDText>()!.changeColor && item.GetComponent<scrHUDText>()!.enabled,
                "Color isolation leaves native HUD visibility enabled");
        }
        Check(text.text == NativeResults && ADOBase.playerManager.players.Select((p, i) =>
            p.marginTracker.Counts.SequenceEqual(statistics[i])).All(unchanged => unchanged),
            "Visibility repair preserves all native result values and both players' judgement records");

        // A new level color can arrive while isolation is active; disable restores that live scheme.
        level.colourText = new Color(.7f, .5f, .3f, .2f);
        level.colourTextShadow = new Color(.9f, .4f, .2f, .1f);
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(allTexts.All(item => item.color == level.colourText
            && item.GetComponent<Shadow>()!.effectColor == level.colourTextShadow
            && item.GetComponent<scrHUDText>()!.changeColor),
            "Turning isolation off restores current native level colors for summary and every completion message");
        Check(allTexts.All(item => Background(item).gameObject.activeSelf),
            "Disabling color isolation does not disable independently enabled result panels");
        settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        Check(allTexts.All(item => item.color == Color.white
            && item.GetComponent<Shadow>()!.effectColor == new Color(0, 0, 0, .5f)),
            "Reenabling isolation consistently restores fixed opaque defaults");
        controller.txtAllStrictClear!.enabled = false;
        controller.txtAllStrictClear.GetComponent<DetailedResultsView>()!.Refresh();
        Check(!controller.txtAllStrictClear.enabled && !Background(controller.txtAllStrictClear).gameObject.activeSelf,
            "Fixed colors never force a natively disabled completion message visible");
        controller.txtAllStrictClear.enabled = true;

        // Co-op's disabled HUD carries a planet color instead of the live level scheme.
        var planetColor = new Color(.3f, .8f, .6f, .4f);
        results.hudText.enabled = false;
        text.color = planetColor;
        Show(results, 1);
        Check(text.color == Color.white && !results.hudText.enabled,
            "Co-op accuracy receives fixed text colors without enabling the native HUD");
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(text.color == planetColor && !results.hudText.enabled,
            "Co-op restores its own planet color rather than a transparent default or another player's color");
        settings.DetailedResultsDefaultColors = true;
        XPerfectResultsColor.Refresh();
        var oldPanel = Background(text);
        var replacement = new DetailedResults();
        replacement.textComponent.cachedTextGenerator.verts.AddRange(Glyphs());
        replacement.textComponent.color = level.colourText;
        replacement.textComponent.gameObject.AddComponent<Shadow>().effectColor = level.colourTextShadow;
        Show(replacement);
        Check(!text.GetComponent<DetailedResultsView>()!.enabled && !oldPanel.gameObject.activeSelf
            && text.color == planetColor && text.text == NativeResults,
            "Replacing result UI restores the old co-op Text and hides its panel without rewriting results");
        Check(replacement.textComponent.color == Color.white
            && replacement.textComponent.GetComponent<Shadow>()!.effectColor == new Color(0, 0, 0, .5f),
            "Replacement result UI also ignores a transparent mutable default scheme");
        settings.EnchantedDetailedResults = false;
        XPerfectResultsColor.Refresh();
        Check(replacement.textComponent.color == level.colourText && allTexts.Skip(1).All(item =>
            item.color == level.colourText && !Background(item).gameObject.activeSelf),
            "Master disable restores current native colors and hides panels across rebuilt and completion UI");
    }

    private static void DynamicFontLifecycle()
    {
        var settings = AccurateJudgementBar.Main.settings!;
        settings.EnchantedDetailedResults = settings.DetailedResultsDefaultColors = settings.DetailedResultsBackground = true;
        ADOBase.controller = null;
        CompletionResultsText.Refresh();
        scrVfx.instance = null;
        var results = new DetailedResults();
        var text = results.textComponent;
        var font = text.font = new Font();
        var native = text.cachedTextGenerator;
        native.verts.AddRange(Glyphs());
        var nativeGlyphs = native.verts.ToArray();
        int nativePopulates = native.PopulateCalls;
        int nativeInvalidates = native.InvalidateCalls;
        Show(results);
        var view = text.GetComponent<DetailedResultsView>()!;
        var generator = BackgroundGenerator(view);
        var panel = Background(text);
        int invalidates = generator.InvalidateCalls;
        int dirty = text.VerticesDirtyCalls;
        Font.Rebuild(new Font());
        Check(generator.InvalidateCalls == invalidates && text.VerticesDirtyCalls == dirty,
            "Rebuilding an unrelated font does not invalidate this result layout or native mesh");
        Font.Rebuild(font);
        Check(generator.InvalidateCalls == invalidates + 1 && text.VerticesDirtyCalls == dirty,
            "Matching font rebuild invalidates owned layout without dirtying native Text during Canvas rebuild");
        LateUpdate(view);
        Check(text.VerticesDirtyCalls == dirty + 1,
            "The next LateUpdate requests a fresh native mesh after matching font rebuild");
        Check(native.PopulateCalls == nativePopulates && native.InvalidateCalls == nativeInvalidates
            && native.verts.SequenceEqual(nativeGlyphs),
            "Font rebuild leaves the native generator state and complete glyph vertices untouched");

        // Dynamic-font atlases can rebuild while the background is requesting first-use glyphs.
        generator.OnPopulate = () => Font.Rebuild(font);
        invalidates = generator.InvalidateCalls;
        dirty = text.VerticesDirtyCalls;
        view.Refresh();
        generator.OnPopulate = null;
        Check(generator.InvalidateCalls == invalidates + 1 && text.VerticesDirtyCalls == dirty
            && view.enabled && panel.gameObject.activeSelf && text.text == NativeResults,
            "Atlas rebuild during background Populate preserves results and queues a fresh native render");
        LateUpdate(view);
        Check(text.VerticesDirtyCalls == dirty + 1 && text.text == NativeResults,
            "First-use atlas rebuild only dirties native Text on its next LateUpdate");

        generator.RejectPopulate = true;
        view.Refresh();
        Check(view.enabled && !view.DisplayFailed && !panel.gameObject.activeSelf
            && text.enabled && text.color == Color.white && text.text == NativeResults,
            "Owned layout Populate rejection only hides its panel, leaving accuracy text and color isolation active");
        Check(native.PopulateCalls == nativePopulates && native.InvalidateCalls == nativeInvalidates
            && native.verts.SequenceEqual(nativeGlyphs),
            "Owned layout Populate rejection cannot corrupt the native cache");
        generator.RejectPopulate = false;
        view.Refresh();
        Check(panel.gameObject.activeSelf && view.enabled && BackgroundGenerator(view) == generator,
            "A later successful Populate restores the same background without a settings toggle");

        view.enabled = false;
        invalidates = generator.InvalidateCalls;
        dirty = text.VerticesDirtyCalls;
        Font.Rebuild(font);
        Check(generator.InvalidateCalls == invalidates && text.VerticesDirtyCalls == dirty
            && !panel.gameObject.activeSelf && results.hudText.changeColor,
            "Disable removes font subscriptions and restores native HUD color handling");
        view.enabled = true;
        Check(generator.InvalidateCalls == invalidates + 1,
            "OnEnable invalidates an existing background layout before its next Populate");
        view.Refresh();
        view.Refresh();
        view.Refresh();
        invalidates = generator.InvalidateCalls;
        dirty = text.VerticesDirtyCalls;
        Font.Rebuild(font);
        Check(generator.InvalidateCalls == invalidates + 1 && text.VerticesDirtyCalls == dirty,
            "Reenable and repeated refreshes restore exactly one matching-font subscription");
        LateUpdate(view);
        Check(text.VerticesDirtyCalls == dirty + 1,
            "Reenabled view still defers font mesh refresh until LateUpdate");

        Font.Rebuild(font); // Leave one refresh pending when the display is disabled.
        dirty = text.VerticesDirtyCalls;
        view.enabled = false;
        view.enabled = true;
        LateUpdate(view);
        Check(text.VerticesDirtyCalls == dirty,
            "Disable clears a pending font refresh instead of dirtying stale native UI after reenable");

        bool laterSubscriberRan = false;
        bool escapedFontCallback = false;
        Action<Font> laterSubscriber = rebuiltFont => { if (rebuiltFont == font) laterSubscriberRan = true; };
        invalidates = generator.InvalidateCalls;
        generator.ThrowOnInvalidate = true;
        Font.textureRebuilt += laterSubscriber;
        try
        {
            try { Font.Rebuild(font); }
            catch { escapedFontCallback = true; }
            Check(!escapedFontCallback && laterSubscriberRan && generator.InvalidateCalls == invalidates + 1,
                "AJB font callback failures stay isolated so later native event subscribers can still execute");
            Check(native.PopulateCalls == nativePopulates && native.InvalidateCalls == nativeInvalidates
                && native.verts.SequenceEqual(nativeGlyphs) && text.text == NativeResults && text.enabled,
                "AJB font callback failure leaves native generator state, accuracy text and visibility intact");
        }
        finally
        {
            Font.textureRebuilt -= laterSubscriber;
            generator.ThrowOnInvalidate = false;
        }

        view.enabled = false;
        generator.ThrowOnInvalidate = true;
        invalidates = generator.InvalidateCalls;
        bool escapedEnable = false;
        try { view.enabled = true; }
        catch { escapedEnable = true; }
        Check(!escapedEnable && generator.InvalidateCalls == invalidates + 1
            && native.PopulateCalls == nativePopulates && native.verts.SequenceEqual(nativeGlyphs),
            "AJB OnEnable layout failures stay inside its display boundary and leave the native cache intact");
        generator.ThrowOnInvalidate = false;
        view.Refresh();
        Check(panel.gameObject.activeSelf && text.text == NativeResults && !view.DisplayFailed,
            "Explicit display refresh recovers after an OnEnable layout failure");

        typeof(DetailedResultsView).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, null);
        invalidates = generator.InvalidateCalls;
        dirty = text.VerticesDirtyCalls;
        Font.Rebuild(font);
        Check(generator.DisposeCalls == 1 && generator.InvalidateCalls == invalidates
            && text.VerticesDirtyCalls == dirty && !panel.gameObject.activeSelf,
            "Destroy disposes the owned generator and removes its font callback");
        typeof(DetailedResultsView).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, null);
        Check(generator.DisposeCalls == 1 && native.DisposeCalls == 0,
            "Repeated cleanup disposes owned layout once and never disposes the native generator");
        Check(native.PopulateCalls == nativePopulates && native.InvalidateCalls == nativeInvalidates
            && native.verts.SequenceEqual(nativeGlyphs) && text.text == NativeResults && text.enabled,
            "Font lifecycle, rejection and cleanup preserve complete native accuracy text and cache state");
    }
}
