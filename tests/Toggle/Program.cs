using System;
using System.Reflection;
using AccurateJudgementBar;
using DG.Tweening;
using UnityEngine;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Record(scrMarginTracker tracker) => typeof(FailureJudgement)
        .GetMethod("RecordFailure", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { tracker });
    private static object? Patch(string type, string method, params object?[] args) => typeof(FailureJudgement)
        .GetNestedType(type, BindingFlags.NonPublic)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    private static bool Show(ErrorMeterTick tick, HitMargin margin, out Color? color)
    {
        object?[] args = { tick, margin, null, "visibility" };
        bool run = (bool)Patch("ColorPatch", "Prefix", args)!;
        color = (Color?)args[2];
        if (run) tick.tick.color = color ?? new Color(1, 0, 0, 1);
        return run;
    }

    private static void FailureVisibility(UnityModManager.ModEntry entry)
    {
        var tick = new ErrorMeterTick();
        var planet = new scrPlanet();
        foreach (bool option in new[] { false, true })
        {
            Mod.settings.ShowFailureJudgement = option;
            foreach (var margin in new[] { HitMargin.FailMiss, HitMargin.FailOverload })
            {
                Check(Show(tick, margin, out var color) && color == null, "AJB off must preserve native failure hits regardless of option");
                Mod.SettingsUpdate();
                Check(tick.tick.color.a == 1, "Off-state settings must not erase a new native hit");
            }
        }
        entry.OnToggle(entry, true);
        Check(tick.tick.color.a == 0, "Enabling AJB must clear an already visible native failure");
        scrController.instance = new scrController { errorMeter = new scrHitErrorMeter() };
        scrConductor.instance = new scrConductor();
        foreach (bool option in new[] { false, true })
        foreach (var shape in new[] { ADOFAI.ErrorMeterShape.Straight, ADOFAI.ErrorMeterShape.Curved })
        foreach (var margin in new[] { HitMargin.FailMiss, HitMargin.FailOverload })
        {
            Mod.settings.ShowFailureJudgement = option;
            Mod.SettingsUpdate();
            Record(new scrMarginTracker());
            object?[] args = { -61f, margin, planet, 0, null };
            bool run = (bool)Patch("DrawPatch", "Prefix", args)!;
            Check(run == option, "Failure draw must follow AJB's option for both shapes and failure types");
            if (run)
            {
                Check((float)args[0]! < -63f && args[2] == null, "AJB must replace native position and co-op color");
                Check(Show(tick, margin, out var color) && color.HasValue && color.Value.b == FailureJudgement.Purple.b,
                    "Enabled failure must use AJB's purple");
                Patch("DrawPatch", "Postfix", new[] { tick }, new[] { "visibility" }, shape, args[4]);
                Check(tick.GetComponent<FailureTickFollower>()?.enabled == true, "Replacement hit must follow AJB's band");
            }
            else
            {
                // A skipped draw must not touch the old slot (nor even access its arrays).
                Patch("DrawPatch", "Postfix", Array.Empty<ErrorMeterTick>(), Array.Empty<string>(), shape, args[4]);
                Check(!Show(tick, margin, out _) && tick.tick.color.a == 0, "Direct native Show must also be hidden");
            }
            object?[] normal = { 10f, HitMargin.Perfect, planet, 0, null };
            Check((bool)Patch("DrawPatch", "Prefix", normal)! && (float)normal[0]! == 10f && ReferenceEquals(normal[2], planet),
                "Ordinary hits must keep native position and player color");
            Check(Show(tick, HitMargin.Perfect, out var normalColor) && normalColor == null, "Ordinary hit color must be untouched");
            Mod.settings.ShowFailureJudgement = false;
            Mod.SettingsUpdate();
            Check(tick.tick.color.a == 1, "Clearing failure state must not erase a tick reused for a normal hit");
        }

        var player = new scrPlayer();
        scrController.instance.noFail = true;
        foreach (bool option in new[] { false, true })
        {
            Mod.settings.ShowFailureJudgement = option;
            Mod.SettingsUpdate();
            Patch("HitPatch", "Prefix", player);
            try
            {
                Patch("MarginPatch", "Postfix", player.marginTracker, HitMargin.FailOverload);
                object?[] noFailDraw = { 2f, HitMargin.Perfect, planet, 0, null };
                Check((bool)Patch("DrawPatch", "Prefix", noFailDraw)! == option,
                    "A NoFail failure carried by a normal margin must also obey the option");
                Check(Show(tick, HitMargin.Perfect, out var noFailColor) == option && (noFailColor != null) == option,
                    "NoFail failure must be replaced with AJB color or hidden");
            }
            finally { Patch("HitPatch", "Finalizer"); }
        }
        scrController.instance.noFail = false;
        Mod.settings.ShowFailureJudgement = true;
        object?[] death = { player, null };
        Patch("DeathPatch", "Prefix", death);
        Record(player.marginTracker);
        Show(tick, HitMargin.FailMiss, out _);
        player.alive = false;
        Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
        Check(scrController.instance.errorMeter!.addedHits == 0, "Native failure converted during Die must not create a duplicate synthetic hit");
        player.alive = true;
        Patch("DeathPatch", "Prefix", death);
        player.alive = false;
        Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
        Check(scrController.instance.errorMeter.addedHits == 1, "A death without a native hit must still get AJB's synthetic hit");
        entry.OnToggle(entry, false);
        Check(Show(tick, HitMargin.FailOverload, out var restored) && restored == null, "Disabling AJB must restore native failure display");
    }

    private static void FailureDisplayModes(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.AnimationDuration = 0;
        Check(new Settings().FailureBandDisplayMode == 0, "Death Band must default to after death");
        // Columns: alive/NoFail off, alive/NoFail on, failed/NoFail off, failed/NoFail on.
        bool[,] visible = { { false, false, true, true }, { false, true, false, true },
            { false, true, true, true }, { false, false, false, true }, { true, true, true, true } };
        foreach (bool parent in new[] { false, true })
        foreach (int mode in new[] { 0, 1, 2, 3, 4 })
        foreach (bool noFail in new[] { false, true })
        foreach (bool isSafe in new[] { false, true })
        foreach (var limit in new[] { HitMarginLimit.None, HitMarginLimit.PerfectsOnly, HitMarginLimit.PurePerfectOnly })
        foreach (bool noMiss in new[] { false, true })
        foreach (var margin in new[] { HitMargin.FailMiss, HitMargin.FailOverload })
        {
            FailureJudgement.Clear();
            scrController.instance = new scrController { noFail = noFail };
            var floor = new scrFloor { isSafe = isSafe };
            var player = new scrPlayer();
            player.planetarySystem.chosenPlanet.currfloor = floor;
            ADOBase.playerManager = new scrPlayerManager { players = new[] { player } };
            Mod.settings.ShowFailureJudgement = parent;
            Mod.settings.FailureBandDisplayMode = mode;
            GCS.hitMarginLimit = limit;
            Mod.settings.NoMissOnly = noMiss;
            bool allowed = parent && limit == HitMarginLimit.None && !noMiss;
            bool protectedNow = noFail || isSafe;
            bool beforeFailure = allowed && visible[mode, protectedNow ? 1 : 0];
            bool afterFailure = allowed && visible[mode, protectedNow ? 3 : 2];
            Check((FailureJudgement.Progress == 1) == beforeFailure && FailureJudgement.Visible == beforeFailure,
                $"Mode {mode} before failure must respect NoFail, parent and judgement limit");
            var tracker = new scrMarginTracker();
            Patch("MarginPatch", "Postfix", tracker, HitMargin.TooEarly);
            Patch("MarginPatch", "Postfix", tracker, HitMargin.TooLate);
            Check(FailureJudgement.Visible == beforeFailure, "Ordinary red judgements must not count as a death");
            Patch("MarginPatch", "Postfix", tracker, margin);
            Check((FailureJudgement.Progress == 1) == afterFailure && FailureJudgement.Visible == afterFailure,
                $"Mode {mode} after {margin} must follow its visibility condition");
            Check(scrController.instance.noFail == noFail && floor.isSafe == isSafe && player.alive,
                "Display evaluation must not change NoFail, safe tiles or the player's life state");
            scrController.instance.noFail = !noFail;
            bool afterNoFailChange = allowed && visible[mode, !noFail || isSafe ? 3 : 2];
            Check((FailureJudgement.Progress == 1) == afterNoFailChange && FailureJudgement.Visible == afterNoFailChange,
                "Changing NoFail must use its current value without losing the recorded failure");
            scrController.instance.noFail = noFail;
            Patch("AttemptResetPatch", "Postfix", tracker);
            Check((FailureJudgement.Progress == 1) == beforeFailure && FailureJudgement.Visible == beforeFailure,
                "Retry must clear death history while retaining the selected mode and current NoFail state");
        }

        ADOBase.playerManager = null;
        Mod.settings.NoMissOnly = false;
        GCS.hitMarginLimit = HitMarginLimit.None;
        Mod.settings.ShowFailureJudgement = true;
        Mod.settings.FailureBandDisplayMode = 1;
        Mod.settings.AnimationDuration = .4f;
        scrController.instance = new scrController { noFail = true };
        FailureJudgement.Clear();
        Time.unscaledTimeAsDouble = 0;
        Check(FailureJudgement.Progress == 0 && FailureJudgement.Visible, "NoFail mode must start the configured animation without a failure");
        Time.unscaledTimeAsDouble = .2;
        Check(Math.Abs(FailureJudgement.Progress - .875f) < .00001, "NoFail visibility must retain the easing animation");
        Time.unscaledTimeAsDouble = .4;
        Check(FailureJudgement.Progress == 1, "NoFail animation must finish without a failure");
        Mod.settings.FailureBandDisplayMode = 2;
        Check(FailureJudgement.Progress == 1, "Switching between satisfied conditions must not restart the animation");
        Mod.settings.FailureBandDisplayMode = 0;
        Check(FailureJudgement.Progress == 1 && !FailureJudgement.Visible, "An unmet condition must begin retracting the band");
        Time.unscaledTimeAsDouble = .9;
        Check(FailureJudgement.Progress == 0, "Retraction must finish");
        Record(new scrMarginTracker());
        Mod.settings.FailureBandDisplayMode = 3;
        Check(FailureJudgement.Visible, "AND mode must retain a failure recorded while another mode was selected");
        Time.unscaledTimeAsDouble = 1.5;
        Check(FailureJudgement.Progress == 1, "Death-triggered animation must finish");
        Record(new scrMarginTracker());
        Check(FailureJudgement.Progress == 1, "Later failures must not replay the animation");
        scrController.instance.noFail = false;
        Check(FailureJudgement.Progress == 1 && !FailureJudgement.Visible, "AND mode must retract when NoFail is turned off");
        Mod.settings.FailureBandDisplayMode = 2;
        Check(FailureJudgement.Progress == 1 && FailureJudgement.Visible, "OR mode must reverse retraction using the retained death history");
        scrController.instance = new scrController { noFail = true };
        Check(FailureJudgement.Progress == 0 && FailureJudgement.Visible, "A new scene must restart NoFail-triggered animation");
        Mod.settings.FailureBandDisplayMode = 3;
        Check(FailureJudgement.Progress == 0 && !FailureJudgement.Visible, "A new scene must clear prior death history in AND mode");
        Mod.settings.FailureBandDisplayMode = 1;
        entry.OnToggle(entry, false);
        Check(FailureJudgement.Progress == 0 && !FailureJudgement.Visible, "Disabling the mod must suppress NoFail-triggered visibility");
        Mod.settings.FailureBandDisplayMode = 0;
        Mod.settings.AnimationDuration = 0;
    }

    private static void EditorTrackerReplacement(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.ShowFailureJudgement = true;
        Mod.settings.AnimationDuration = 0;
        GCS.hitMarginLimit = HitMarginLimit.None;
        foreach (int mode in new[] { 0, 1, 2, 3 })
        foreach (bool noFail in new[] { false, true })
        foreach (var shape in new[] { ADOFAI.ErrorMeterShape.Straight, ADOFAI.ErrorMeterShape.Curved })
        {
            FailureJudgement.Clear();
            Mod.settings.FailureBandDisplayMode = mode;
            var controller = scrController.instance = new scrController { noFail = noFail };
            var oldTracker = scrMistakesManager.marginTrackers[0];
            Record(oldTracker);
            var tick = new ErrorMeterTick();
            Show(tick, HitMargin.FailMiss, out _);
            var follower = tick.gameObject.AddComponent<FailureTickFollower>();
            follower.Configure(tick, shape, 1, "visibility");
            tick.gameObject.SetActive(false);
            Check(FailureJudgement.Visible == (mode == 0 || mode == 2 || noFail),
                "Workshop death visibility must follow the selected mode before entering the editor");

            // scnEditor.Awake calls SetPlayerCount(1) and reuses the loaded game scene.
            scrMistakesManager.SetPlayerCount(1);
            Patch("TrackerReplacementPatch", "Postfix");
            var editorTracker = scrMistakesManager.marginTrackers[0];
            Patch("AttemptResetPatch", "Postfix", editorTracker);
            Check(ReferenceEquals(controller, scrController.instance) && !ReferenceEquals(oldTracker, editorTracker),
                "Editor entry must reproduce a new tracker with the same controller");
            bool visibleWithoutDeath = noFail && (mode == 1 || mode == 2);
            Check(FailureJudgement.Visible == visibleWithoutDeath && FailureJudgement.Progress == (visibleWithoutDeath ? 1 : 0),
                "Editor entry must discard the workshop failure even when its tracker never resets");
            Check(!follower.enabled && tick.tick.color.a == 0 && tick.tickOutline.color.a == 0,
                "Tracker replacement must clear cached failure ticks even on inactive meters");
            for (int retry = 0; retry < 3; retry++)
            {
                Patch("AttemptResetPatch", "Postfix", editorTracker);
                Check(FailureJudgement.Visible == visibleWithoutDeath, "Repeated editor attempts must not revive the workshop failure");
            }
            Mod.settings.FailureBandDisplayMode = 0;
            Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0,
                "Selecting after death in the editor must not expose an orphaned workshop failure");
            Record(editorTracker);
            Check(FailureJudgement.Visible, "A fresh editor failure must still trigger the band");
            Patch("AttemptResetPatch", "Postfix", editorTracker);
            Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0,
                "Retrying or resetting for another editor level must clear its actual failure");
        }
        // Individual co-op resets must keep the other player's failure;
        // replacing the complete roster must clear both old trackers.
        scrMistakesManager.SetPlayerCount(2);
        Patch("TrackerReplacementPatch", "Postfix");
        Record(scrMistakesManager.marginTrackers[0]);
        Record(scrMistakesManager.marginTrackers[1]);
        Patch("AttemptResetPatch", "Postfix", scrMistakesManager.marginTrackers[0]);
        Check(FailureJudgement.Visible, "One player's reset must retain the other player's failure");
        scrMistakesManager.SetPlayerCount(1);
        Patch("TrackerReplacementPatch", "Postfix");
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "Roster replacement must clear all old player failures");

        entry.OnToggle(entry, false);
        var nativeTick = new ErrorMeterTick();
        Show(nativeTick, HitMargin.FailMiss, out _);
        scrMistakesManager.SetPlayerCount(1);
        Patch("TrackerReplacementPatch", "Postfix");
        Check(nativeTick.tick.color.a == 1, "Tracker replacement while AJB is disabled must preserve native hits");
    }

    public static void Main()
    {
        var entry = new UnityModManager.ModEntry();
        Mod.Load(entry);
        var meter = new scrHitErrorMeter { sensitivity = .35f, tickLife = 4.5f };
        entry.OnToggle(entry, true);
        Check(OverloadBar.Created == 0, "Enable must respect the disabled HP option");
        Check(meter.sensitivity == .6f && meter.tickLife == 9f, "Enable must update an existing meter");
        Mod.settings.EnableOverloadHP = true;
        Mod.SettingsUpdate();
        Mod.checkOverloadBar();
        Check(OverloadBar.Created == 1, "Repeated updates must not duplicate the HP bar");
        var second = new scrHitErrorMeter { sensitivity = .15f, tickLife = 2f };
        BetterHit.UpdateLayoutPatch.Postfix(second);
        Mod.settings.LifeOfHit = 8f;
        Mod.SettingsUpdate();
        Check(second.tickLife == 8f && meter.tickLife == 8f, "All meters must receive settings changes");

        scrController.instance = new scrController();
        Record(new scrMarginTracker());
        var tick = new ErrorMeterTick();
        var follower = tick.gameObject.AddComponent<FailureTickFollower>();
        follower.Configure(tick, ADOFAI.ErrorMeterShape.Straight, 1, "failure");
        tick.gameObject.SetActive(false);
        Check(FailureJudgement.Visible, "Failure should be visible before toggle off");
        entry.OnToggle(entry, false);
        Check(!follower.enabled && tick.tick.color.a == 0 && tick.tickOutline.color.a == 0,
            "Disable must immediately clear failures even on inactive meter objects");
        Check(DOTween.Killed.Contains("failure") && DOTween.Killed.Contains("failureo"), "Both failure tweens must stop");
        Check(OverloadBar.Destroyed == 1, "Disable must remove the HP bar");
        Check(meter.sensitivity == .35f && meter.tickLife == 4.5f && second.sensitivity == .15f && second.tickLife == 2f,
            "Disable must restore each meter's actual original settings");
        foreach (var watcher in UnityEngine.Object.FindObjectsByType<StraightMeterWatcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Check(!watcher.gameObject.activeSelf, "Watchers must stop before deferred destruction");
        Mod.enableOverloadBar(true);
        Mod.SettingsUpdate();
        entry.OnUpdate(entry, 1);
        BetterHit.UpdateLayoutPatch.Postfix(meter);
        Check(OverloadBar.Created == 1 && meter.tickLife == 4.5f, "Off-state callbacks must not reapply features");
        Record(new scrMarginTracker());
        entry.OnToggle(entry, true);
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "Re-enable must not revive old or disabled-session failures");
        Check(OverloadBar.Created == 2, "Re-enable must create exactly one new HP bar");

        Record(new scrMarginTracker());
        tick.tick.color = tick.tickOutline.color = new Color(1, 1, 1, 1);
        follower.Configure(tick, ADOFAI.ErrorMeterShape.Curved, -1, "second");
        Mod.settings.ShowFailureJudgement = false;
        Mod.SettingsUpdate();
        Check(!follower.enabled && tick.tick.color.a == 0, "Disabling just the failure option must also clear cached ticks");
        Mod.settings.ShowFailureJudgement = true;
        Mod.SettingsUpdate();
        Check(!FailureJudgement.Visible, "Re-enabling the failure option must not revive a cleared failure");
        Mod.settings.EnableOverloadHP = false;
        Mod.SettingsUpdate();
        entry.OnToggle(entry, false);
        entry.OnToggle(entry, false);
        Check(OverloadBar.Destroyed == 2 && meter.tickLife == 4.5f, "Repeated cleanup must be harmless");
        FailureVisibility(entry);
        FailureDisplayModes(entry);
        SafeTileFailureBandTests(entry);
        StrictOnlyTests(entry);
        EditorTrackerReplacement(entry);
        LimitFailureTicks(entry);
        TickMarginScaleTests(entry);
        ErrorMeterSizeTests(entry);
        NativeMeterSettingsTests(entry);
        DisplayIsolationTests(entry);
        AprilFoolsLifecycleTests();
        TogetherLifecycleTests();
        StartingBpmWarningTests(entry);
        StartingBpmLayoutTests();
        Console.WriteLine($"PASS: {checks} toggle lifecycle checks");
    }
}
