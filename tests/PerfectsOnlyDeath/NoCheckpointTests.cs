using System;
using AccurateJudgementBar;
using UnityEngine;
using UnityModManagerNet;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;
using Interaction = SettingsMenu.Interaction;
using Mod = AccurateJudgementBar.Main;

public class scrPlanet { }

// Replay predrop ffxCheckpoint.StartEffect's native guards and observable side effects.
// Production prefix is called at entry; this is not a substitute for Harmony/Unity testing.
public class ffxCheckpoint
{
    public int floorID = 10, checkpointTileOffset = 2, nativeCalls, marks, revives, flashes;
    public scrPlayer deadPlayer = new() { alive = false };
    public Exception? nativeError;
    public void StartEffect() => StartEffect(null!);
    public void StartEffect(scrPlanet planet)
    {
        if (!NoCheckpoint.ActivationPatch.Prefix()) return;
        nativeCalls++;
        if (GCS.speedTrialMode || GCS.practiceMode || GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly) return;
        int next = floorID + checkpointTileOffset;
        if (next <= GCS.checkpointNum) return;
        if (nativeError != null) throw nativeError;
        if (!deadPlayer.alive) { deadPlayer.alive = true; revives++; }
        flashes++;
        GCS.checkpointNum = next;
        marks++;
    }
}

internal static class NoCheckpointTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("NoCheckpoint: " + message); }

    private static void Reset()
    {
        UnityModManager.TogetherEntry = Together.Main.ModEntry = null;
        Together.Main.IsInTogether = false;
        Mod.is_mod_enable = true;
        Mod.settings = new();
        Mod.Mod = new();
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.None;
        GCS.checkpointNum = 3;
        GCS.speedTrialMode = GCS.practiceMode = false;
        Persistence.enableCompetitiveMode = true;
        scrController.instance = new();
        scrLivesCounter.instance = null;
        ADOBase.isExpo = RDC.debug = RDC.useOldAuto = RDString.failMessage = false;
        Resources.Objects = Array.Empty<object>();
        TogetherCompatibility.Update();
    }

    private static (SettingsMenu menu, PauseSettingButton button) Menu(bool deferred = false)
    {
        var button = new PauseSettingButton { deferUpdate = deferred };
        var menu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 4 };
        Resources.Objects = new object[] { menu };
        menu.UpdateSetting(button, Interaction.Refresh);
        return (menu, button);
    }

    internal static void Run()
    {
        // All four Only choices plus No Checkpoint, independent of display, repair,
        // competitive mode and No Fail. Existing progress and records are never cleared.
        foreach (var mode in Enum.GetValues<Mode>())
        foreach (bool competitive in new[] { false, true })
        foreach (bool noFail in new[] { false, true })
        {
            Reset();
            Persistence.enableCompetitiveMode = competitive;
            scrController.instance!.noFail = noFail;
            Mod.settings.PerfectsOnlyInstantDeath = Mod.settings.ShowHitMarginLimit = Mod.settings.EnableAccurateJudgementBar = false;
            MarginLimitState.Apply(mode);
            Check(GCS.checkpointNum == 3, "Selecting a mode preserves earlier progress: " + mode);
            var checkpoint = new ffxCheckpoint();
            checkpoint.deadPlayer.marginTracker.hitMargins.Add(HitMargin.TooEarly);
            checkpoint.StartEffect(new scrPlanet());
            checkpoint.StartEffect(); // Native wrapper must dispatch through the same entry.
            bool blocked = mode != Mode.None;
            Check(checkpoint.nativeCalls == (blocked ? 0 : 2), "Only explicit Off permits native activation: " + mode);
            Check(GCS.checkpointNum == (blocked ? 3 : 12) && checkpoint.marks == (blocked ? 0 : 1), "No checkpoint save/snapshot: " + mode);
            Check(checkpoint.revives == (blocked ? 0 : 1) && checkpoint.deadPlayer.alive == !blocked &&
                checkpoint.flashes == (blocked ? 0 : 1), "No checkpoint revival/flash: " + mode);
            Check(checkpoint.deadPlayer.marginTracker.hitMargins.Count == 1 && checkpoint.deadPlayer.deathCalls == 0 &&
                checkpoint.deadPlayer.syntheticHits == 0, "Real judgement/death records are untouched: " + mode);
        }

        foreach (var from in Enum.GetValues<Mode>())
        foreach (var to in Enum.GetValues<Mode>())
        {
            Reset();
            MarginLimitState.Apply(from);
            int saves = Mod.settings.saveCalls;
            MarginLimitState.Apply(to);
            Check(MarginLimitState.Current == to && NoCheckpoint.Active == (to != Mode.None), "Switch all mode pairs without stale enforcement");
            Check(Mod.settings.NoCheckpoint == (to == Mode.NoCheckpoint) && Mod.settings.NoMissOnly == (to == Mode.NoMissOnly) &&
                Mod.settings.XPerfectOnly == (to == Mode.XPerfectOnly), "Exclusive selection across stored fields");
            bool changed = (from == Mode.NoCheckpoint) != (to == Mode.NoCheckpoint) ||
                (from == Mode.NoMissOnly) != (to == Mode.NoMissOnly) || (from == Mode.XPerfectOnly) != (to == Mode.XPerfectOnly);
            Check(Mod.settings.saveCalls == saves + (changed ? 1 : 0), "Persist changed extra choices exactly once");
            Check(Enum.IsDefined(GCS.hitMarginLimit) && GCS.hitMarginLimit == Persistence.hitMarginLimit, "Native enum remains valid");
        }

        // No Checkpoint uses None and cannot add judgement rejection, death, or display colors.
        Reset();
        Check(!new Settings().NoCheckpoint, "Missing XML field defaults to off");
        MarginLimitState.Apply(Mode.NoCheckpoint);
        foreach (var hit in Enum.GetValues<HitMargin>())
        foreach (bool originalResult in new[] { false, true })
        {
            var player = new scrPlayer();
            player.marginTracker.hitMargins.Add(hit);
            bool accepted = hit.IsCounted(HitMarginLimit.None), actual = accepted, result = originalResult;
            XPerfectOnly.CountedPatch.Postfix(hit, null, ref actual);
            PerfectsOnlyDeath.Postfix(player, hit, ref result);
            XPerfectOnly.DeathPatch.Postfix(player, hit, ref result);
            NoMissOnly.DeathPatch.Postfix(player, hit, false);
            Check(actual == accepted && result == originalResult && player.alive && player.deathCalls == 0 &&
                player.syntheticHits == 0 && player.marginTracker.hitMargins[0] == hit,
                "No Checkpoint preserves all native judgement/damage results: " + hit);
        }
        Check(GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
            "Standalone mode cannot tighten native hit acceptance");

        // Disable/re-enable and UI rebuild keep preferences but never retain enforcement.
        Reset();
        MarginLimitState.Apply(Mode.NoCheckpoint);
        var (menu, button) = Menu();
        Check(button.valueLabel.text == "No Checkpoint", "Localized label is restored from saved preference");
        int saved = Mod.settings.saveCalls;
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        var resumed = new ffxCheckpoint();
        resumed.StartEffect();
        Check(!NoCheckpoint.Active && resumed.marks == 1 && Mod.settings.NoCheckpoint &&
            button.initialValue is HitMarginLimit.None && button.valueLabel.text.EndsWith(".None"), "Disable restores vanilla and preserves preference");
        Mod.is_mod_enable = true;
        MarginLimitMenu.Refresh();
        var rebuilt = new PauseSettingButton();
        menu.UpdateSetting(rebuilt, Interaction.Refresh);
        Check(NoCheckpoint.Active && rebuilt.valueLabel.text == "No Checkpoint" && Mod.settings.saveCalls == saved, "Rebuild/re-enable restores selection without saving");
        menu.UpdateSetting(button, Interaction.Decrement);
        Check(!NoCheckpoint.Active && !Mod.settings.NoCheckpoint && menu.settingsThatRequireRestart == 5, "Off releases restriction and requires restart");
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(menu.settingsThatRequireRestart == 4, "Disable only removes this setting's vanished contribution");

        Reset();
        (menu, button) = Menu(true);
        menu.UpdateSetting(button, Interaction.Increment);
        Check(button.CachedValue is Mode.NoCheckpoint && !NoCheckpoint.Active && Mod.settings.saveCalls == 0, "Unconfirmed preview cannot block a checkpoint");
        menu.UpdateSetting(button, Interaction.Refresh);
        Check(!button.confirmText.gameObject.active && !NoCheckpoint.Active, "Cancel keeps native activation");
        menu.UpdateSetting(button, Interaction.Increment);
        menu.UpdateSetting(button, Interaction.Activate);
        menu.UpdateSetting(button, Interaction.Activate);
        Check(NoCheckpoint.Active && Mod.settings.saveCalls == 1 && menu.settingsThatRequireRestart == 5, "Confirm applies and saves once");

        Reset();
        (menu, button) = Menu(true);
        menu.UpdateSetting(button, Interaction.Increment);
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(button.CachedValue is HitMarginLimit.None && !button.confirmText.gameObject.active && !Mod.settings.NoCheckpoint,
            "Disable discards an unconfirmed AJB enum without saving it");

        Reset();
        MarginLimitState.Apply(Mode.NoCheckpoint);
        var together = new UnityModManager.ModEntry { Active = true };
        Together.Main.ModEntry = UnityModManager.TogetherEntry = together;
        Together.Main.IsInTogether = true;
        Check(!NoCheckpoint.Active, "Together immediately suppresses AJB checkpoint restriction before Update");
        TogetherCompatibility.Update();
        Check(!Mod.settings.NoCheckpoint && Mod.settings.saveCalls == 2, "Together clears and persists standalone selection");
        Together.Main.IsInTogether = false;
        TogetherCompatibility.Update();
        Check(!NoCheckpoint.Active, "Leaving Together does not restore discarded restrictions");

        // Restore all native gates (including PurePerfectOnly) and exceptions on disable.
        foreach (var mode in Enum.GetValues<Mode>())
        {
            Reset();
            MarginLimitState.Apply(mode);
            Mod.is_mod_enable = false;
            var checkpoint = new ffxCheckpoint();
            checkpoint.StartEffect();
            Check(checkpoint.nativeCalls == 1 && checkpoint.marks == (mode is Mode.PurePerfectOnly or Mode.XPerfectOnly ? 0 : 1),
                "Disabled prefix leaves original native checkpoint policy: " + mode);
        }
        foreach (bool practice in new[] { false, true })
        {
            Reset();
            GCS.practiceMode = practice;
            GCS.speedTrialMode = !practice;
            var checkpoint = new ffxCheckpoint();
            checkpoint.StartEffect();
            Check(checkpoint.nativeCalls == 1 && GCS.checkpointNum == 3 && checkpoint.marks == 0, "Native speed/practice guards remain intact");
        }
        Reset();
        var broken = new ffxCheckpoint { nativeError = new InvalidOperationException("native checkpoint") };
        Exception? observed = null;
        try { broken.StartEffect(); } catch (Exception ex) { observed = ex; }
        Check(ReferenceEquals(observed, broken.nativeError), "Original checkpoint exceptions propagate");
        Reset();
        Console.WriteLine($"NoCheckpoint: {checks} checks passed.");
    }
}
