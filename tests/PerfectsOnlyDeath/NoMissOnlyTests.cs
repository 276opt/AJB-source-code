using System;
using AccurateJudgementBar;
using UnityEngine;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;
using Interaction = SettingsMenu.Interaction;
using Mod = AccurateJudgementBar.Main;

internal static class NoMissOnlyTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("NoMissOnly: " + message);
    }

    private static scrPlayer Reset()
    {
        Mod.is_mod_enable = true;
        Mod.settings = new Settings { NoMissOnly = true, PerfectsOnlyInstantDeath = false };
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.None;
        Persistence.enableCompetitiveMode = false;
        scrController.instance = new();
        ADOBase.isExpo = false;
        scrLivesCounter.instance = null;
        RDString.failMessage = false;
        Resources.Objects = Array.Empty<object>();
        return new();
    }

    internal static void Run()
    {
        Check(!new Settings().NoMissOnly, "Missing setting must default to off");
        foreach (var hit in Enum.GetValues<HitMargin>())
        {
            var player = Reset();
            bool counted = hit.IsCounted(GCS.hitMarginLimit);
            NoMissOnly.DeathPatch.Postfix(player, hit, false);
            bool isRed = hit is HitMargin.TooEarly or HitMargin.TooLate;
            Check(player.alive == !isRed && player.deathCalls == (isRed ? 1 : 0), "Only red misses add death: " + hit);
            Check(hit.IsCounted(GCS.hitMarginLimit) == counted, "Native acceptance is unchanged: " + hit);
            Check(GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
                "Damage must never change native settings");
            NoMissOnly.DeathPatch.Postfix(player, hit, false);
            Check(player.deathCalls == (isRed ? 1 : 0), "No duplicate death");
        }

        Action<scrPlayer>[] guards =
        {
            p => Mod.is_mod_enable = false,
            p => Mod.settings.NoMissOnly = false,
            p => Mod.settings = null!,
            p => GCS.hitMarginLimit = HitMarginLimit.PerfectsOnly,
            p => GCS.hitMarginLimit = HitMarginLimit.PurePerfectOnly,
            p => scrController.instance = null,
            p => scrController.instance!.gameworld = false,
            p => scrController.instance!.noFail = true,
            p => p.currFloor = null,
            p => p.currFloor!.freeroam = true,
            p => p.currFloor!.isSafe = true,
            p => p.missCooldownActive = true,
            p => { ADOBase.isExpo = true; scrLivesCounter.instance = new() { lives = 2 }; },
            p => scrLivesCounter.instance = new() { lives = 1 }
        };
        foreach (var guard in guards)
        foreach (var hit in new[] { HitMargin.TooEarly, HitMargin.TooLate })
        {
            var player = Reset();
            guard(player);
            NoMissOnly.DeathPatch.Postfix(player, hit, false);
            Check(player.alive && player.deathCalls == 0 && player.syntheticHits == 0,
                "Protected contexts retain native behavior without a synthetic accepted hit");
        }
        foreach (var hit in new[] { HitMargin.TooEarly, HitMargin.TooLate })
        {
            var player = Reset();
            NoMissOnly.DeathPatch.Postfix(player, hit, true);
            Check(player.alive && player.deathCalls == 0, "Multipress retains its separate native penalty");
            player.protectedFromDeath = true;
            NoMissOnly.DeathPatch.Postfix(player, hit, false);
            Check(player.alive && player.deathCalls == 1, "Native autoplay/debug refusal remains respected");
        }

        foreach (int lives in new[] { 0, 1 })
        {
            var player = Reset();
            ADOBase.isExpo = true;
            scrLivesCounter.instance = new() { lives = lives };
            NoMissOnly.DeathPatch.Postfix(player, HitMargin.TooEarly, false);
            Check(!player.alive && player.syntheticHits == 0, "The last expo life still uses native death");
        }
        var missingTextPlayer = Reset();
        RDString.failMessage = true;
        NoMissOnly.DeathPatch.Postfix(missingTextPlayer, HitMargin.TooEarly, false);
        Check(!missingTextPlayer.alive, "Display localization failure must not prevent death");
        var brokenGamePlayer = Reset();
        var originalError = new InvalidOperationException("Original Die failure");
        brokenGamePlayer.deathError = originalError;
        Exception? observedError = null;
        try { NoMissOnly.DeathPatch.Postfix(brokenGamePlayer, HitMargin.TooEarly, false); }
        catch (Exception ex) { observedError = ex; }
        Check(ReferenceEquals(observedError, originalError), "Never swallow an original game exception");

        // All mode pairs must keep both saved flags mutually exclusive and native values legal.
        foreach (var from in new[] { Mode.None, Mode.NoCheckpoint, Mode.NoMissOnly, Mode.PerfectsOnly, Mode.PurePerfectOnly, Mode.XPerfectOnly })
        foreach (var to in new[] { Mode.None, Mode.NoCheckpoint, Mode.NoMissOnly, Mode.PerfectsOnly, Mode.PurePerfectOnly, Mode.XPerfectOnly })
        {
            Reset();
            MarginLimitState.Apply(from);
            int saves = Mod.settings.saveCalls;
            MarginLimitState.Apply(to);
            Check(Mod.settings.NoMissOnly == (to == Mode.NoMissOnly) && Mod.settings.XPerfectOnly == (to == Mode.XPerfectOnly),
                "Explicit selection clears the other extra mode");
            Check(MarginLimitState.Current == to,
                "Explicit native mappings round-trip without relying on enum ordinal values");
            bool flagsChanged = (from == Mode.NoMissOnly) != (to == Mode.NoMissOnly) ||
                (from == Mode.XPerfectOnly) != (to == Mode.XPerfectOnly) ||
                (from == Mode.NoCheckpoint) != (to == Mode.NoCheckpoint);
            Check(Mod.settings.saveCalls == saves + (flagsChanged ? 1 : 0), "Save changed AJB preferences immediately, once");
        }

        Reset();
        var button = new PauseSettingButton();
        var menu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 3 };
        Resources.Objects = new object[] { menu };
        menu.UpdateSetting(button, Interaction.Refresh);
        Check(button.valueLabel.text == "No Miss Only" && Mod.settings.saveCalls == 0, "Restore saved choice without writing");
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(button.initialValue is HitMarginLimit.None && button.valueLabel.text.EndsWith(".None") &&
            Mod.settings.NoMissOnly && !NoMissOnly.Active, "Disabling restores native None and preserves preference");
        Check(MarginLimitMenu.SettingPatch.Prefix(menu, button, Interaction.Increment), "Disabled menu belongs to the game");
        Mod.is_mod_enable = true;
        MarginLimitMenu.Refresh();
        Check(button.valueLabel.text == "No Miss Only" && NoMissOnly.Active, "Re-enable restores saved selection");
        menu.UpdateSetting(new PauseSettingButton(), Interaction.Refresh);
        menu.UpdateSetting(button, Interaction.Decrement);
        Check(!Mod.settings.NoMissOnly && menu.settingsThatRequireRestart == 4, "No Checkpoint and No Miss have distinct restart tracking");
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(menu.settingsThatRequireRestart == 3, "Fallback removes only the vanished extra-mode restart contribution");

        // Stale preferences cannot override a different official selection made while disabled.
        foreach (var native in new[] { HitMarginLimit.PerfectsOnly, HitMarginLimit.PurePerfectOnly })
        {
            Reset();
            GCS.hitMarginLimit = Persistence.hitMarginLimit = native;
            Check(!NoMissOnly.Active && MarginLimitState.Read(native, false, true) != Mode.NoMissOnly,
                "No Miss needs both its flag and native None");
        }

        Reset();
        MarginLimitState.Apply(Mode.None);
        button = new PauseSettingButton { deferUpdate = true };
        menu = new SettingsMenu { buttons = new[] { button } };
        Resources.Objects = new object[] { menu };
        menu.UpdateSetting(button, Interaction.Refresh);
        menu.UpdateSetting(button, Interaction.Increment);
        menu.UpdateSetting(button, Interaction.Increment);
        Check(!NoMissOnly.Active && button.confirmText.gameObject.active, "Pending No Miss must not affect gameplay");
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(button.CachedValue is HitMarginLimit.None && button.initialValue is HitMarginLimit.None &&
            !button.confirmText.gameObject.active, "Disabled deferred values remain safe for the native menu");
        Mod.is_mod_enable = true;
        menu.UpdateSetting(button, Interaction.Refresh);
        menu.UpdateSetting(button, Interaction.Increment);
        menu.UpdateSetting(button, Interaction.Increment);
        int beforeConfirm = Mod.settings.saveCalls;
        menu.UpdateSetting(button, Interaction.Activate);
        menu.UpdateSetting(button, Interaction.Activate);
        Check(NoMissOnly.Active && Mod.settings.saveCalls == beforeConfirm + 1, "Confirmed No Miss saves only once");

        // Verify the real MeterWatcher color getters for every mode and display switch.
        Color red = new Color32(255, 0, 0, 255), redDeath = new Color32(157, 55, 212, 255);
        Color orange = new Color32(255, 111, 77, 255), yellow = new Color32(252, 255, 77, 255);
        Color green = new Color32(95, 255, 78, 255), death = new Color32(204, 119, 249, 255);
        foreach (var mode in new[] { Mode.None, Mode.NoCheckpoint, Mode.NoMissOnly, Mode.PerfectsOnly, Mode.PurePerfectOnly, Mode.XPerfectOnly })
        foreach (bool show in new[] { false, true })
        foreach (bool competitive in new[] { false, true })
        {
            Reset();
            MarginLimitState.Apply(mode);
            Mod.settings.ShowHitMarginLimit = show;
            Persistence.enableCompetitiveMode = competitive;
            Check(MeterWatcher.GetRed() == (show && mode is not Mode.None and not Mode.NoCheckpoint ? redDeath : red), "Red color: " + mode);
            Check(MeterWatcher.GetOrange() == (show && mode is Mode.PerfectsOnly or Mode.PurePerfectOnly or Mode.XPerfectOnly ? death : orange),
                "No Miss must preserve the orange timing band");
            Check(MeterWatcher.GetYellow() == (show && mode is Mode.PurePerfectOnly or Mode.XPerfectOnly ? death : yellow), "Yellow color: " + mode);
            Check(MeterWatcher.GetGreen() == (show && mode == Mode.XPerfectOnly && competitive ? death : green), "Green color: " + mode);
            Check(MeterWatcher.GetBlue() == NativeXPerfect.Color, "XPerfect color remains native");
        }
        Reset();
        Mod.is_mod_enable = false;
        Check(MeterWatcher.GetRed() == red, "Disabling restores red despite saved No Miss flag");
        Console.WriteLine($"NoMissOnly: {checks} checks passed.");
    }
}
