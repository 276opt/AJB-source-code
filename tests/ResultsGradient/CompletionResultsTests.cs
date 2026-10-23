using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityEngine.UI;

internal static class CompletionResultsTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("CompletionResults: " + message);
    }
    private static Image? Background(DetailedResultsView view) => (Image?)typeof(DetailedResultsView)
        .GetField("background", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view);
    private static Text Message(string value)
    {
        var text = new Text { text = value, color = new Color(1, 1, 1, 0) };
        text.gameObject.AddComponent<scrHUDText>();
        text.gameObject.AddComponent<Shadow>().effectColor = new Color(1, 1, 1, 0);
        text.cachedTextGenerator.verts.AddRange(new[] {
            new UIVertex { position = new(-60, 20, 0) }, new UIVertex { position = new(60, 20, 0) },
            new UIVertex { position = new(60, -20, 0) }, new UIVertex { position = new(-60, -20, 0) } });
        return text;
    }
    internal static void Run()
    {
        var settings = AccurateJudgementBar.Main.settings = new Settings();
        AccurateJudgementBar.Main.is_mod_enable = true;
        var controller = ADOBase.controller = new scrController {
            txtCongrats = Message("完美通關！"), txtAllStrictClear = Message("嚴格模式通關"),
            txtAprilCongrats = Message("原版額外通關提示") };
        var texts = new[] { controller.txtCongrats!, controller.txtAllStrictClear!, controller.txtAprilCongrats! };
        var native = texts.Select(t => t.text).ToArray();
        var tracker = ADOBase.playerManager!.players[0].marginTracker;
        var stats = tracker.Counts.ToArray();
        var unrelated = Message("遊玩中的其他 HUD");
        CompletionResultsText.Refresh();
        Check(texts.All(t => t.GetComponent<DetailedResultsView>() == null), "No decoration before native completion");
        typeof(CompletionResultsText).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
        Check(texts.All(t => t.GetComponent<DetailedResultsView>() == null), "Portal postfix does not fabricate completion while Won transition is queued");
        controller.stateMachine.State = States.Won;
        CompletionResultsText.Update();
        Check(texts.All(t => t.GetComponent<DetailedResultsView>() != null), "Deferred native Won gets visible messages on the next mod update without toggling settings");
        foreach (var text in texts)
        {
            var view = text.GetComponent<DetailedResultsView>()!;
            Check(view.enabled && text.color.a == 1 && text.GetComponent<Shadow>()!.effectColor.a == .5f,
                "Each native completion message gets default text and shadow colors");
            Check(!text.GetComponent<scrHUDText>()!.changeColor && text.GetComponent<scrHUDText>()!.enabled,
                "Completion HUD continues handling native visibility");
            Check(Background(view)!.gameObject.activeSelf && !Background(view)!.raycastTarget,
                "Each native completion message gets a background without intercepting input");
        }
        Check(texts.Select(t => t.text).SequenceEqual(native) && tracker.Counts.SequenceEqual(stats)
            && controller.stateMachine.State == States.Won, "Messages, native judgement records and completion state stay unchanged");
        Check(unrelated.GetComponent<DetailedResultsView>() == null && unrelated.color.a == 0,
            "Unrelated HUD text is never affected");
        var strict = controller.txtAllStrictClear!;
        var strictView = strict.GetComponent<DetailedResultsView>()!;
        var strictBg = Background(strictView)!;
        CompletionResultsText.Update();
        Check(Background(strictView) == strictBg && strict.gameObject.Components.OfType<DetailedResultsView>().Count() == 1,
            "Normal mod updates reuse the existing view and panel");
        settings.DetailedResultsBackground = false;
        XPerfectResultsColor.Refresh();
        Check(texts.All(t => !Background(t.GetComponent<DetailedResultsView>()!)!.gameObject.activeSelf && t.color.a == 1),
            "Existing background switch applies to all completion messages independently");
        settings.DetailedResultsBackground = true;
        settings.DetailedResultsDefaultColors = false;
        XPerfectResultsColor.Refresh();
        Check(texts.All(t => Background(t.GetComponent<DetailedResultsView>()!)!.gameObject.activeSelf && t.color.a == 0
            && t.GetComponent<Shadow>()!.effectColor.a == 0 && t.GetComponent<scrHUDText>()!.changeColor),
            "Existing color switch restores all native message colors while keeping backgrounds");
        settings.DetailedResultsDefaultColors = true;
        settings.DetailedResultsSignedPerfects = false;
        XPerfectResultsColor.Refresh();
        Check(strictBg.gameObject.activeSelf && strict.color.a == 1, "Directional-count switch does not control completion messages");
        settings.EnchantedDetailedResults = false;
        XPerfectResultsColor.Refresh();
        Check(texts.All(t => !t.GetComponent<DetailedResultsView>()!.enabled && t.color.a == 0
            && !Background(t.GetComponent<DetailedResultsView>()!)!.gameObject.activeSelf), "Master switch restores all completion messages");
        settings.EnchantedDetailedResults = true;
        XPerfectResultsColor.Refresh();
        AccurateJudgementBar.Main.is_mod_enable = false;
        XPerfectResultsColor.Refresh();
        Check(!strictView.enabled && !strictBg.gameObject.activeSelf && strict.GetComponent<scrHUDText>()!.changeColor,
            "Disabling AJB restores completion HUD and background");
        AccurateJudgementBar.Main.is_mod_enable = true;
        XPerfectResultsColor.Refresh();
        Check(strictView.enabled && Background(strictView) == strictBg
            && strict.gameObject.Components.OfType<DetailedResultsView>().Count() == 1, "Reenable reuses one completion view and background");

        strict.enabled = false;
        strictView.Refresh();
        Check(!strictBg.gameObject.activeSelf && !strict.enabled, "No-HUD keeps native Text disabled and hides its background");
        strict.enabled = true;
        strict.text = "";
        strictView.Refresh();
        Check(!strictView.enabled && !strictBg.gameObject.activeSelf && strict.text == "", "Empty native messages are never fabricated");
        strict.text = native[1];
        strict.gameObject.SetActive(false);
        CompletionResultsText.Refresh();
        Check(!strict.gameObject.activeSelf && !strictView.enabled && !strictBg.gameObject.activeSelf,
            "Native hidden Strict Clear is never forced visible");
        strict.gameObject.SetActive(true);
        CompletionResultsText.Update();
        Check(strictView.enabled && strictBg.gameObject.activeSelf, "Mod update applies to a newly visible native message without a setting toggle");

        strict.text = "恭喜!";
        strict.cachedTextGenerator.verts.Clear();
        // Three real glyph quads; the final exclamation mark extends beyond the first two.
        strict.cachedTextGenerator.verts.AddRange(new[] {
            new UIVertex { position = new(-60, 20, 0) }, new UIVertex { position = new(-20, 20, 0) },
            new UIVertex { position = new(-20, -20, 0) }, new UIVertex { position = new(-60, -20, 0) },
            new UIVertex { position = new(-20, 20, 0) }, new UIVertex { position = new(20, 20, 0) },
            new UIVertex { position = new(20, -20, 0) }, new UIVertex { position = new(-20, -20, 0) },
            new UIVertex { position = new(25, 20, 0) }, new UIVertex { position = new(35, 20, 0) },
            new UIVertex { position = new(35, -20, 0) }, new UIVertex { position = new(25, -20, 0) } });
        strictView.Refresh();
        Check(strictBg.rectTransform.sizeDelta.x == 119 && strictBg.rectTransform.localPosition.x == -12.5f,
            "Background covers the final exclamation mark in 恭喜!");
        strict.text = "!";
        strict.cachedTextGenerator.verts.RemoveRange(0, 8);
        strictView.Refresh();
        Check(strictBg.gameObject.activeSelf && strictBg.rectTransform.sizeDelta.x == 34,
            "A single-glyph message gets a background instead of losing its only quad");

        controller.txtCongrats!.text = "原版稍後更新的自訂通關提示";
        controller.txtCongrats.GetComponent<DetailedResultsView>()!.Refresh();
        Check(controller.txtCongrats.text == "原版稍後更新的自訂通關提示" && controller.txtCongrats.color.a == 1,
            "Native message updates retain their exact wording while keeping display enhancements");
        var replacement = Message("重建後的嚴格通關提示");
        controller.txtAllStrictClear = replacement;
        strictView.Refresh();
        CompletionResultsText.Refresh();
        Check(!strictView.enabled && !strictBg.gameObject.activeSelf && strict.GetComponent<scrHUDText>()!.changeColor,
            "UI replacement restores the old completion Text");
        Check(replacement.GetComponent<DetailedResultsView>()!.enabled && replacement.color.a == 1,
            "Rebuilt completion UI receives its own view");
        controller.stateMachine.State = States.PlayerControl;
        foreach (var text in new[] { controller.txtCongrats!, replacement, controller.txtAprilCongrats! })
            text.GetComponent<DetailedResultsView>()!.Refresh();
        Check(!replacement.GetComponent<DetailedResultsView>()!.enabled && replacement.color.a == 0
            && replacement.GetComponent<scrHUDText>()!.changeColor,
            "Retry / leaving Won releases enhancements even if old completion text is still active");

        var next = ADOBase.controller = new scrController { txtAllStrictClear = Message("另一個場景的原版提示") };
        next.stateMachine.State = States.Won;
        CompletionResultsText.Refresh();
        Check(next.txtAllStrictClear!.GetComponent<DetailedResultsView>()!.enabled,
            "Controller replacement applies only to the new scene");
        next.txtAllStrictClear.text = "";
        next.txtAllStrictClear.GetComponent<DetailedResultsView>()!.Refresh();
        next.txtCongrats = Message("背景建立失敗測試");
        next.txtAprilCongrats = Message("其他提示仍要處理");
        GameObject.ThrowOnImage = true;
        CompletionResultsText.Refresh();
        int failedAttempts = GameObject.ImageAttempts;
        CompletionResultsText.Update();
        CompletionResultsText.Update();
        Check(GameObject.ImageAttempts == failedAttempts, "Per-frame discovery does not repeatedly recreate failed UI");
        GameObject.ThrowOnImage = false;
        Check(new[] { next.txtCongrats, next.txtAprilCongrats }.All(t => !t!.GetComponent<DetailedResultsView>()!.enabled
            && t.color.a == 0 && t.GetComponent<scrHUDText>()!.changeColor),
            "Each failing completion view rolls back its own colors and HUD flags");
        CompletionResultsText.Refresh();
        Check(next.txtCongrats.GetComponent<DetailedResultsView>()!.enabled
            && next.txtAprilCongrats.GetComponent<DetailedResultsView>()!.enabled,
            "Explicit refresh recovers failed views independently");
        ADOBase.controller = null;
        CompletionResultsText.Refresh();
        Check(!next.txtCongrats.GetComponent<DetailedResultsView>()!.enabled && next.txtCongrats.GetComponent<scrHUDText>()!.changeColor,
            "Leaving gameplay cleans up all registered completion views");
        Console.WriteLine($"CompletionResults: {checks} checks passed.");
    }
}
