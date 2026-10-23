using System;
using System.Linq;
using System.Reflection;
using AccurateJudgementBar;
using ADOFAI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static readonly Exception DisplayFault = new InvalidOperationException("injected AJB display fault");
    private static void LegacyShow(ErrorMeterTick tick, HitMargin margin = HitMargin.Perfect) =>
        typeof(LegacyHitVisual).GetNestedType("ShowPatch", BindingFlags.NonPublic)!
            .GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { tick, margin });
    private static void ForgetLegacySprite() => typeof(LegacyHitVisual)
        .GetField("legacySprite", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    private static bool SyntheticFailure => (bool)typeof(FailureJudgement)
        .GetField("syntheticFailure", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private sealed class NativeTickSnapshot
    {
        internal readonly Image Image;
        private readonly Transform Parent = new RectTransform();
        private readonly Sprite Sprite = new(), Override = new();
        private readonly Material Material = new();
        internal NativeTickSnapshot(ErrorMeterTick tick)
        {
            Image = tick.tick;
            Image.sprite = Sprite;
            Image.overrideSprite = Override;
            Image.material = Material;
            Image.type = Image.Type.Sliced;
            Image.preserveAspect = Image.useSpriteMesh = true;
            var rect = Image.rectTransform;
            rect.SetParent(Parent, false);
            rect.SetSiblingIndex(3);
            rect.anchorMin = new(.1f, .2f);
            rect.anchorMax = new(.7f, .8f);
            rect.pivot = new(.2f, .6f);
            rect.sizeDelta = new(8, 12);
            rect.anchoredPosition3D = new(1, 2, 3);
            rect.localScale = new(2, 3, 4);
        }
        internal void CheckRestored()
        {
            var rect = Image.rectTransform;
            Check(ReferenceEquals(rect.parent, Parent) && rect.GetSiblingIndex() == 3, "Restore original native parent and sibling");
            Check(rect.anchorMin == new Vector2(.1f, .2f) && rect.anchorMax == new Vector2(.7f, .8f)
                && rect.pivot == new Vector2(.2f, .6f) && rect.sizeDelta == new Vector2(8, 12), "Restore native anchors and size after partial failure");
            Check(rect.anchoredPosition3D.Equals(new Vector3(1, 2, 3)) && rect.localScale.Equals(new Vector3(2, 3, 4)), "Restore native position and scale");
            Check(ReferenceEquals(Image.sprite, Sprite) && ReferenceEquals(Image.overrideSprite, Override)
                && ReferenceEquals(Image.material, Material), "Restore native sprites and material");
            Check(Image.type == Image.Type.Sliced && Image.preserveAspect && Image.useSpriteMesh, "Restore native Image options");
        }
        internal void CheckIndependentRestores() => Check(ReferenceEquals(Image.overrideSprite, Override)
            && ReferenceEquals(Image.material, Material) && Image.type == Image.Type.Sliced
            && Image.preserveAspect && Image.useSpriteMesh, "Independent appearance restore steps continue after the sprite setter fails");
    }

    private static void DisplayIsolationTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.NoMissOnly = Mod.settings.XPerfectOnly = false;
        Mod.settings.ShowFailureJudgement = true;
        Mod.settings.FailureBandDisplayMode = 0;
        Mod.settings.AnimationDuration = 0;
        GCS.hitMarginLimit = HitMarginLimit.None;
        Persistence.enableCompetitiveMode = true;
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.Default;
        DisplayErrors.Reset();
        LegacyIsolation(entry);
        FailureIsolation(entry);
        entry.OnToggle(entry, false);
    }

    private static void LegacyIsolation(UnityModManager.ModEntry entry)
    {
        Mod.settings.UseLegacyHit = false;
        var untouched = new ErrorMeterTick();
        LegacyShow(untouched);
        Check(untouched.GetComponent<LegacyHitVisual>() == null, "Disabled legacy option must not initialize extra components");

        Mod.settings.UseLegacyHit = true;
        ForgetLegacySprite();
        foreach (bool decodeFails in new[] { true, false })
        {
            Texture2D.decodeSucceeds = !decodeFails;
            Sprite.createError = decodeFails ? null : DisplayFault;
            var tick = new ErrorMeterTick();
            var original = new NativeTickSnapshot(tick);
            int destroyed = UnityEngine.Object.DestroyRequested.Count;
            int decodes = Texture2D.decodes;
            LegacyShow(tick);
            original.CheckRestored();
            Check(UnityEngine.Object.DestroyRequested.Count == destroyed + 1, "Discard partially created texture on decode or Sprite.Create failure");
            LegacyShow(tick);
            Check(Texture2D.decodes == decodes + 1, "A faulted pooled tick must not load the same broken resource every hit");
            Texture2D.decodeSucceeds = true;
            Sprite.createError = null;
            LegacyHitVisual.RefreshAll();
            Check(ReferenceEquals(tick.tick.rectTransform.parent, tick.transform), "Explicit refresh permits recovery after resource failure");
            Mod.settings.UseLegacyHit = false;
            LegacyHitVisual.RefreshAll();
            original.CheckRestored();
            Mod.settings.UseLegacyHit = true;
            ForgetLegacySprite();
        }

        var partial = new ErrorMeterTick();
        var partialOriginal = new NativeTickSnapshot(partial);
        int writes = 0;
        partial.tick.onSetSprite = () => { if (++writes == 1) throw DisplayFault; };
        LegacyShow(partial); // Parent and layout already changed when the sprite setter throws.
        partialOriginal.CheckRestored();
        int afterRollback = writes;
        LegacyShow(partial);
        Check(writes == afterRollback, "Do not reapply a faulted visual on the next pooled hit");
        partial.tick.onSetSprite = null;
        LegacyHitVisual.RefreshAll();
        Check(ReferenceEquals(partial.tick.rectTransform.parent, partial.transform), "Recovered visual can be reapplied");

        // One permanently broken restore step must not block other properties or other instances.
        partial.tick.onSetSprite = () => throw DisplayFault;
        var healthy = new ErrorMeterTick();
        var healthyOriginal = new NativeTickSnapshot(healthy);
        LegacyShow(healthy);
        Mod.settings.UseLegacyHit = false;
        LegacyHitVisual.RefreshAll();
        partialOriginal.CheckIndependentRestores();
        healthyOriginal.CheckRestored();
        partial.tick.onSetSprite = null;
        LegacyHitVisual.RefreshAll();
        partialOriginal.CheckRestored();

        Mod.settings.UseLegacyHit = true;
        var pendingRollback = new ErrorMeterTick();
        var pendingOriginal = new NativeTickSnapshot(pendingRollback);
        pendingRollback.tick.onSetSprite = () => throw DisplayFault;
        LegacyShow(pendingRollback); // Both the partial apply and its immediate rollback fail.
        pendingRollback.tick.onSetSprite = null;
        LegacyHitVisual.RefreshAll(); // Keep the option enabled: rollback must precede reapplication.
        Check(ReferenceEquals(pendingRollback.tick.rectTransform.parent, pendingRollback.transform)
            && ReferenceEquals(pendingRollback.tick.sprite, pendingRollback.tick.overrideSprite)
            && pendingRollback.tick.material == null && pendingRollback.tick.type == Image.Type.Simple,
            "Retry after failed rollback must fully reapply instead of accepting partial state as applied");
        Mod.settings.UseLegacyHit = false;
        LegacyHitVisual.RefreshAll();
        pendingOriginal.CheckRestored();
        Mod.settings.UseLegacyHit = true;
        var invalidImage = new ErrorMeterTick();
        var invalidOriginal = new NativeTickSnapshot(invalidImage);
        invalidImage.tick.rectError = DisplayFault;
        LegacyShow(invalidImage);
        invalidImage.tick.rectError = null;
        LegacyShow(invalidImage);
        invalidOriginal.CheckRestored();
        LegacyHitVisual.RefreshAll();
        Check(ReferenceEquals(invalidImage.tick.rectTransform.parent, invalidImage.transform), "Incomplete snapshot can retry on explicit refresh");
        typeof(LegacyHitVisual).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(invalidImage.GetComponent<LegacyHitVisual>(), null);
        invalidOriginal.CheckRestored();

        var rebuilt = new ErrorMeterTick();
        var oldImage = new NativeTickSnapshot(rebuilt);
        LegacyShow(rebuilt);
        rebuilt.tick = new Image();
        var newImage = new NativeTickSnapshot(rebuilt);
        LegacyShow(rebuilt);
        oldImage.CheckRestored();
        Check(ReferenceEquals(rebuilt.tick.rectTransform.parent, rebuilt.transform), "Rebuilt Image receives its own snapshot");
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        LegacyShow(rebuilt, HitMargin.XPerfect);
        newImage.CheckRestored();
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.Default;
        LegacyShow(rebuilt);
        entry.OnToggle(entry, false);
        newImage.CheckRestored();
        entry.OnToggle(entry, true);
        Mod.settings.UseLegacyHit = false;
        LegacyHitVisual.RefreshAll();

        // Component setup failures and a broken diagnostic sink cannot escape to Hit.
        entry.Logger.throwOnError = true;
        var missingComponent = new ErrorMeterTick();
        missingComponent.gameObject.getComponentError = DisplayFault;
        LegacyShow(missingComponent);
        missingComponent.gameObject.getComponentError = null;
        Mod.settings.UseLegacyHit = true;
        missingComponent.gameObject.addComponentError = DisplayFault;
        LegacyShow(missingComponent);
        missingComponent.gameObject.addComponentError = null;
        entry.Logger.throwOnError = false;
        Mod.settings.UseLegacyHit = false;
        Check(missingComponent.GetComponent<LegacyHitVisual>() == null, "Failed setup leaves no incomplete component");
    }

    // Replay the relevant native ordering: Draw -> Show -> AJB postfix -> tick index -> follower.
    // The native actions intentionally sit outside AJB's catches.
    private static void ReplayDraw(ErrorMeterTick tick, HitMargin margin, ref int index, Action? nativeShow = null)
    {
        object?[] draw = { 25f, margin, new scrPlanet(), index, null };
        if ((bool)Patch("DrawPatch", "Prefix", draw)!)
        {
            object?[] show = { tick, margin, null, "replay" };
            if ((bool)Patch("ColorPatch", "Prefix", show)!)
            {
                nativeShow?.Invoke();
                tick.tick.color = (Color?)show[2] ?? new Color(1, 1, 1, 1);
            }
            LegacyShow(tick, (HitMargin)show[1]!);
            index++;
        }
        Patch("DrawPatch", "Postfix", new[] { tick }, new[] { "replay" }, ErrorMeterShape.Straight, draw[4]);
    }

    private static void FailureIsolation(UnityModManager.ModEntry entry)
    {
        Mod.settings.UseLegacyHit = false;
        Mod.settings.ShowFailureJudgement = true;
        DisplayErrors.Reset();
        int colorErrors = entry.Logger.Errors.Count(s => s.Contains("(Failure tick color)"));
        foreach (int fault in new[] { 0, 1, 2 })
        {
            FailureJudgement.Clear();
            var controller = new scrController { errorMeter = new scrHitErrorMeter() };
            scrController.instance = controller;
            var player = new scrPlayer();
            var tick = new ErrorMeterTick();
            if (fault == 0) tick.gameObject.getComponentError = DisplayFault;
            if (fault == 1) tick.gameObject.addComponentError = DisplayFault;
            if (fault == 2) { Mod.settings.UseLegacyHit = true; ForgetLegacySprite(); Texture2D.decodeSucceeds = false; }
            int index = 0, continuedHit = 0;
            controller.noFailInfiniteMargin = true;
            Patch("HitPatch", "Prefix", player);
            try
            {
                Patch("MarginPatch", "Postfix", player.marginTracker, HitMargin.FailMiss);
                ReplayDraw(tick, HitMargin.FailMiss, ref index);
                continuedHit++; // Native camera/input follow-up after UpdateHitErrorMeter.
            }
            finally { Patch("HitPatch", "Finalizer"); }
            // This is deliberately not in a finally: the predrop Die implementation isn't either.
            controller.noFailInfiniteMargin = false;
            Check(index == 1 && continuedHit == 1 && !controller.noFailInfiniteMargin,
                "AJB display failure must allow native Hit and Die's flag restoration to complete");
            Check(player.alive && !controller.noFail, "Display recovery must not write gameplay state");
            tick.gameObject.getComponentError = tick.gameObject.addComponentError = null;
            Texture2D.decodeSucceeds = true;
            Mod.settings.UseLegacyHit = false;
            LegacyHitVisual.RefreshAll();
        }
        Check(entry.Logger.Errors.Count(s => s.Contains("(Failure tick color)")) == colorErrors + 1, "Log a failing callback once per enabled session");

        FailureJudgement.Clear();
        var originalPlanet = new scrPlanet();
        object?[] failedDraw = { 12f, HitMargin.FailMiss, originalPlanet, 0, null };
        Mod.settings.FailureBandDisplayMode = 1;
        scrController.instance!.noFail = true;
        Time.readError = DisplayFault;
        Check((bool)Patch("DrawPatch", "Prefix", failedDraw)! && (float)failedDraw[0]! == 12f
            && ReferenceEquals(failedDraw[2], originalPlanet), "Failed layout calculation must pass through original arguments");
        Time.readError = null;
        Mod.settings.FailureBandDisplayMode = 0;
        scrController.instance.noFail = false;

        // An exception after substituting a display grade must roll back both ref arguments.
        var limitedPlayer = new scrPlayer();
        GCS.hitMarginLimit = HitMarginLimit.PurePerfectOnly;
        Patch("HitPatch", "Prefix", limitedPlayer);
        Patch("DamagePatch", "Prefix", limitedPlayer, HitMargin.XPerfect);
        object?[] death = { limitedPlayer, null };
        Patch("DeathPatch", "Prefix", death);
        limitedPlayer.alive = false;
        Patch("DeathPatch", "Postfix", limitedPlayer, false, false, false, death[1]);
        MeterWatcher.blueError = DisplayFault;
        var nativeColor = new Color(.2f, .3f, .4f, 1);
        object?[] failedColor = { new ErrorMeterTick(), HitMargin.FailMiss, nativeColor, "bad-color" };
        Check((bool)Patch("ColorPatch", "Prefix", failedColor)! && (HitMargin)failedColor[1]! == HitMargin.FailMiss
            && ((Color)failedColor[2]!).Equals(nativeColor), "Color failure restores caller's grade and override color");
        MeterWatcher.blueError = null;
        Patch("DamagePatch", "Finalizer");
        Patch("HitPatch", "Finalizer");
        GCS.hitMarginLimit = HitMarginLimit.None;

        // Failure hiding can fail midway; native Show must still run, and cleanup continues per tick.
        FailureJudgement.Clear();
        Mod.settings.ShowFailureJudgement = false;
        DOTween.onKill = _ => throw DisplayFault;
        Check(Show(new ErrorMeterTick(), HitMargin.FailMiss, out _), "Failed hiding must fall back to native Show");
        DOTween.onKill = null;
        Mod.settings.ShowFailureJudgement = true;
        Record(new scrMarginTracker());
        var broken = new ErrorMeterTick();
        var healthy = new ErrorMeterTick();
        Show(broken, HitMargin.FailMiss, out _);
        Show(healthy, HitMargin.FailMiss, out _);
        var brokenFollower = broken.gameObject.AddComponent<FailureTickFollower>();
        var healthyFollower = healthy.gameObject.AddComponent<FailureTickFollower>();
        brokenFollower.Configure(broken, ErrorMeterShape.Straight, 1, "bad");
        healthyFollower.Configure(healthy, ErrorMeterShape.Curved, -1, "good");
        broken.tick.colorError = DisplayFault;
        DOTween.onKill = id => { if (id == "bad") throw DisplayFault; };
        FailureJudgement.Clear();
        Check(!brokenFollower.enabled && !healthyFollower.enabled && healthy.tick.color.a == 0
            && healthy.tickOutline.color.a == 0 && broken.tickOutline.color.a == 0, "Broken cleanup must not block other images, followers or ticks");
        Check(!FailureJudgement.Visible && !SyntheticFailure, "Display bookkeeping clears even when a Unity cleanup operation fails");
        broken.tick.colorError = null;
        DOTween.onKill = null;
        healthyFollower.enabled = true;
        broken.gameObject.getComponentError = DisplayFault;
        Patch("ResetPatch", "Postfix", (object)new[] { broken, healthy });
        Check(!healthyFollower.enabled, "Reset continues after an invalid cached tick");
        broken.gameObject.getComponentError = null;
        object?[] staleCache = { 12f, HitMargin.Perfect, new scrPlanet(), 4, null };
        Patch("DrawPatch", "Prefix", staleCache);
        Patch("DrawPatch", "Postfix", Array.Empty<ErrorMeterTick>(), Array.Empty<string>(), ErrorMeterShape.Straight, staleCache[4]);
        Check(healthy.tick.color.a == 0, "A stale cache must not cause AJB to modify an unrelated tick");
        Record(new scrMarginTracker());
        healthy.tick.color = new(1, 1, 1, 1);
        healthy.rectError = DisplayFault;
        healthyFollower.Configure(healthy, ErrorMeterShape.Curved, -1, "follow-error");
        Check(!healthyFollower.enabled, "Follower disables itself after a per-frame Unity exception");
        healthy.rectError = null;

        DeathDisplayIsolation();

        // A native exception must not be mistaken for an AJB failure or suppressed by finalizers.
        var nativeError = new InvalidOperationException("original game Show failed");
        Exception? observed = null;
        var playerWithNativeError = new scrPlayer();
        FailureJudgement.Clear();
        Patch("HitPatch", "Prefix", playerWithNativeError);
        Patch("DamagePatch", "Prefix", playerWithNativeError, HitMargin.Perfect);
        int nativeIndex = 0;
        try { ReplayDraw(new ErrorMeterTick(), HitMargin.Perfect, ref nativeIndex, () => throw nativeError); }
        catch (Exception ex) { observed = ex; }
        finally { Patch("DamagePatch", "Finalizer"); Patch("HitPatch", "Finalizer"); }
        Check(ReferenceEquals(observed, nativeError) && nativeIndex == 0, "Original Show exception propagates unchanged");
        Check(typeof(FailureJudgement).GetNestedType("HitPatch", BindingFlags.NonPublic)!
            .GetMethod("Finalizer", BindingFlags.NonPublic | BindingFlags.Static)!.ReturnType == typeof(void), "Hit cleanup cannot replace an original exception");
        Check(Show(new ErrorMeterTick(), HitMargin.Perfect, out var normal) && normal == null, "Unwinding an original exception leaves no failure scope on later hits");
    }

    private static void DeathDisplayIsolation()
    {
        FailureJudgement.Clear();
        var meter = new scrHitErrorMeter();
        scrController.instance = new scrController { errorMeter = meter };
        scrConductor.instance = new scrConductor();
        Persistence.hitErrorMeterSize = ErrorMeterSize.Normal;
        var invalidSnapshot = new scrPlayer { floorReadError = DisplayFault };
        object?[] invalidDeath = { invalidSnapshot, null };
        Patch("DeathPatch", "Prefix", invalidDeath);
        invalidSnapshot.alive = false; // Native death remains free to execute.
        Patch("DeathPatch", "Postfix", invalidSnapshot, false, false, false, invalidDeath[1]);
        Check(!invalidSnapshot.alive && meter.addedHits == 0, "Failed display snapshot must neither block death nor manufacture a tick");

        var player = new scrPlayer();
        object?[] death = { player, null };
        Patch("DeathPatch", "Prefix", death);
        player.alive = false;
        meter.onAddHit = (_, _, _, _, _) => { Check(SyntheticFailure, "Only AJB's extra display call enters synthetic scope"); throw DisplayFault; };
        Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
        Check(!player.alive && !SyntheticFailure && meter.addedHits == 1, "A failed synthetic display preserves completed death and restores its scope");
        meter.onAddHit = null;
        Check(Show(new ErrorMeterTick(), HitMargin.Perfect, out var ordinary) && ordinary == null, "Failed death display must not color the next ordinary tick as a failure");

        var outer = new scrPlayer();
        var inner = new scrPlayer();
        object?[] outerDeath = { outer, null };
        Patch("DeathPatch", "Prefix", outerDeath);
        outer.alive = false;
        int depth = 0;
        meter.onAddHit = (_, _, _, _, _) =>
        {
            if (++depth > 1) throw DisplayFault;
            object?[] innerDeath = { inner, null };
            Patch("DeathPatch", "Prefix", innerDeath);
            inner.alive = false;
            Patch("DeathPatch", "Postfix", inner, false, false, false, innerDeath[1]);
            Check(SyntheticFailure, "Nested failed synthetic display must restore its outer scope");
        };
        Patch("DeathPatch", "Postfix", outer, false, false, false, outerDeath[1]);
        Check(!SyntheticFailure && !outer.alive && !inner.alive, "Nested display cleanup preserves both native death outcomes");
        meter.onAddHit = null;
    }
}
