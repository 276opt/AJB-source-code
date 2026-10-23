using System;
using AccurateJudgementBar;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;
using Interaction = SettingsMenu.Interaction;
using Mod = AccurateJudgementBar.Main;

internal static class XPerfectOnlyTests
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new Exception("XPerfectOnly: " + message);
    }

    private static void Reset()
    {
        Mod.is_mod_enable = true;
        Persistence.enableCompetitiveMode = true;
        Mod.settings = new Settings { XPerfectOnly = true, PerfectsOnlyInstantDeath = false };
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.PurePerfectOnly;
        scrController.instance = new scrController();
        UnityEngine.Resources.Objects = Array.Empty<object>();
    }

    private static bool Counted(HitMargin hit, HitMarginLimit? limit = null, bool original = true)
    {
        XPerfectOnly.CountedPatch.Postfix(hit, limit, ref original);
        return original;
    }

    internal static void Run()
    {
        Reset();
        foreach (var hit in Enum.GetValues<HitMargin>())
        {
            Check(Counted(hit) == (hit == HitMargin.XPerfect), "Only XPerfect passes the native hit gate: " + hit);
            Check(Counted(hit, HitMarginLimit.PurePerfectOnly) == (hit == HitMargin.XPerfect), "Explicit native limit: " + hit);
            Check(!Counted(hit, original: false), "Never turn a rejected judgement into an accepted one");
            Check(Counted(hit, HitMarginLimit.None), "Explicit unrestricted queries stay unchanged");
            Check(Counted(hit, HitMarginLimit.PerfectsOnly), "Explicit PerfectsOnly queries stay unchanged");
        }

        foreach (var hit in new[] { HitMargin.PerfectMinus, HitMargin.PerfectPlus })
        {
            Reset();
            var player = new scrPlayer();
            bool result = false;
            XPerfectOnly.DeathPatch.Postfix(player, hit, ref result);
            Check(!player.alive && result && player.deathCalls == 1, "Signed pure perfect must die without PerfectsOnlyInstantDeath");
            XPerfectOnly.DeathPatch.Postfix(player, hit, ref result);
            Check(player.deathCalls == 1, "Do not kill a dead player twice");
        }
        foreach (var hit in new[] { HitMargin.XPerfect, HitMargin.EarlyPerfect, HitMargin.VeryEarly, HitMargin.TooLate, HitMargin.Multipress })
        {
            Reset();
            var player = new scrPlayer();
            bool result = false;
            XPerfectOnly.DeathPatch.Postfix(player, hit, ref result);
            Check(player.alive && !result && player.deathCalls == 0, "Leave XPerfect and vanilla death cases alone");
        }
        Action<scrPlayer>[] guards = {
            p => Mod.is_mod_enable = false,
            p => Mod.settings.XPerfectOnly = false,
            p => Persistence.enableCompetitiveMode = false,
            p => GCS.hitMarginLimit = HitMarginLimit.None,
            p => GCS.hitMarginLimit = HitMarginLimit.PerfectsOnly,
            p => scrController.instance = null,
            p => scrController.instance!.gameworld = false,
            p => scrController.instance!.noFail = true,
            p => p.currFloor = null,
            p => p.currFloor!.freeroam = true,
            p => p.protectedFromDeath = true
        };
        foreach (var guard in guards)
        foreach (bool original in new[] { false, true })
        {
            Reset();
            var player = new scrPlayer();
            guard(player);
            bool result = original;
            XPerfectOnly.DeathPatch.Postfix(player, HitMargin.PerfectPlus, ref result);
            Check(player.alive && result == original, "Respect vanilla protections and preserve result");
        }
        foreach (var limit in Enum.GetValues<HitMarginLimit>())
        foreach (bool enabled in new[] { false, true })
        foreach (bool selected in new[] { false, true })
        foreach (bool competitive in new[] { false, true })
        {
            Reset();
            GCS.hitMarginLimit = limit;
            Mod.is_mod_enable = enabled;
            Mod.settings.XPerfectOnly = selected;
            Persistence.enableCompetitiveMode = competitive;
            Check(Counted(HitMargin.PerfectMinus) == !(enabled && selected && competitive && limit == HitMarginLimit.PurePerfectOnly),
                "Gameplay requires competitive mode, the native value and AJB flag while enabled");
        }

        // Cycle both ways through the actual menu prefix. Persistence rejects invalid enum writes.
        foreach (var direction in new[] { Interaction.Increment, Interaction.Decrement })
        {
            Reset();
            MarginLimitState.Apply(Mode.None);
            var setting = new PauseSettingButton();
            var menu = new SettingsMenu { buttons = new[] { setting }, settingsThatRequireRestart = 4 };
            menu.UpdateSetting(setting, Interaction.Refresh);
            var expected = direction == Interaction.Increment
                ? new[] { Mode.NoCheckpoint, Mode.NoMissOnly, Mode.PerfectsOnly, Mode.PurePerfectOnly, Mode.XPerfectOnly, Mode.None }
                : new[] { Mode.XPerfectOnly, Mode.PurePerfectOnly, Mode.PerfectsOnly, Mode.NoMissOnly, Mode.NoCheckpoint, Mode.None };
            foreach (var mode in expected)
            {
                menu.UpdateSetting(setting, direction);
                Check(MarginLimitState.Current == mode, "Six-state menu order");
                Check(Persistence.hitMarginLimit == GCS.hitMarginLimit && Enum.IsDefined(GCS.hitMarginLimit), "Only original native values are stored");
                Check(menu.settingsThatRequireRestart == (mode == Mode.None ? 4 : 5), "Preserve other settings' restart contributions");
            }
            Check(!Mod.settings.XPerfectOnly, "Leaving XOnly clears its stored flag");
        }

        Reset();
        var button = new PauseSettingButton();
        var settingsMenu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 2 };
        UnityEngine.Resources.Objects = new object[] { settingsMenu };
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        Check(button.valueLabel.text == "XPerfect Only", "Restore XOnly from native PurePerfectOnly plus saved bool");
        Check(Mod.settings.saveCalls == 0, "Opening menu is read-only");
        settingsMenu.UpdateSetting(button, Interaction.Decrement);
        Check(!Mod.settings.XPerfectOnly && Mod.settings.saveCalls == 1, "Explicit Pure selection clears and immediately saves bool");
        Check(settingsMenu.settingsThatRequireRestart == 3, "Pure and XOnly are distinct for restart tracking");
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(button.initialValue is HitMarginLimit.PurePerfectOnly && settingsMenu.settingsThatRequireRestart == 2,
            "Disable restores native UI state and removes only XOnly restart contribution");
        Check(MarginLimitMenu.SettingPatch.Prefix(settingsMenu, button, Interaction.Increment), "Disabled mod lets vanilla own menu");

        Reset();
        button = new PauseSettingButton();
        settingsMenu = new SettingsMenu { buttons = new[] { button } };
        UnityEngine.Resources.Objects = new object[] { settingsMenu };
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(Mod.settings.XPerfectOnly && !XPerfectOnly.Active, "Disable preserves preference but stops enforcement");
        Check(button.valueLabel.text != "XPerfect Only", "Open and hidden menus restore native label");
        Mod.is_mod_enable = true;
        MarginLimitMenu.Refresh();
        Check(button.valueLabel.text == "XPerfect Only", "Re-enable restores existing menu label");
        var rebuilt = new PauseSettingButton();
        settingsMenu.UpdateSetting(rebuilt, Interaction.Refresh);
        Check(rebuilt.valueLabel.text == "XPerfect Only", "Scene/retry menu rebuild restores selection");
        button.name = "unrelated";
        Check(MarginLimitMenu.SettingPatch.Prefix(settingsMenu, button, Interaction.Increment), "Other official settings remain vanilla");

        // DeferUpdate is currently not needed by this setting, but preserve its native contract.
        Reset();
        Mod.settings.XPerfectOnly = false;
        button = new PauseSettingButton { deferUpdate = true };
        settingsMenu = new SettingsMenu { buttons = new[] { button } };
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        settingsMenu.UpdateSetting(button, Interaction.Increment);
        Check(!Mod.settings.XPerfectOnly && button.confirmText.gameObject.active, "Deferred choice cannot change gameplay before confirmation");
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        Check(!Mod.settings.XPerfectOnly && !button.confirmText.gameObject.active, "Refresh cancels unconfirmed choice");
        settingsMenu.UpdateSetting(button, Interaction.Increment);
        settingsMenu.UpdateSetting(button, Interaction.Activate);
        Check(Mod.settings.XPerfectOnly && Mod.settings.saveCalls == 1 && !button.confirmText.gameObject.active, "Confirm saves choice once");
        settingsMenu.UpdateSetting(button, Interaction.Activate);
        Check(Mod.settings.saveCalls == 1 && settingsMenu.settingsThatRequireRestart == 1, "Repeated confirm is inert");

        Reset();
        Mod.settings.XPerfectOnly = false;
        button = new PauseSettingButton { deferUpdate = true };
        settingsMenu = new SettingsMenu { buttons = new[] { button } };
        UnityEngine.Resources.Objects = new object[] { settingsMenu };
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        settingsMenu.UpdateSetting(button, Interaction.Increment);
        Mod.is_mod_enable = false;
        MarginLimitMenu.Refresh();
        Check(button.CachedValue is HitMarginLimit.PurePerfectOnly && button.initialValue is HitMarginLimit.PurePerfectOnly,
            "Disabling converts a pending XPerfect choice back to valid native UI values");
        Check(!button.confirmText.gameObject.active && !Mod.settings.XPerfectOnly,
            "Disabling removes obsolete confirmation without saving an unconfirmed choice");
        Reset();
        button = new PauseSettingButton();
        settingsMenu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 2 };
        UnityEngine.Resources.Objects = new object[] { settingsMenu };
        settingsMenu.UpdateSetting(button, Interaction.Refresh);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            Persistence.enableCompetitiveMode = false;
            MarginLimitMenu.CompetitiveModePatch.Postfix();
            Check(!XPerfectOnly.Active && Counted(HitMargin.PerfectMinus) && Counted(HitMargin.PerfectPlus),
                "Turning competitive mode off immediately restores pure-perfect acceptance");
            Check(button.valueLabel.text == "XPerfect Only (requires Competitive Mode)", "Menu explains the inactive saved choice");
            Check(Mod.settings.XPerfectOnly && Mod.settings.saveCalls == 0 && Persistence.hitMarginLimit == HitMarginLimit.PurePerfectOnly,
                "Competitive toggle preserves both persisted values without saving AJB settings");
            settingsMenu.UpdateSetting(button, Interaction.Refresh);
            Check(button.valueLabel.text.Contains("requires Competitive Mode"), "Reopening the menu still shows the inactive choice");
            Persistence.enableCompetitiveMode = true;
            MarginLimitMenu.CompetitiveModePatch.Postfix();
            Check(XPerfectOnly.Active && !Counted(HitMargin.PerfectPlus) && button.valueLabel.text == "XPerfect Only",
                "Re-enabling competitive mode restores gameplay and existing menu label");
            Check(settingsMenu.settingsThatRequireRestart == 2, "Label refresh preserves other settings' restart contributions");
        }
        Persistence.enableCompetitiveMode = false;
        MarginLimitMenu.CompetitiveModePatch.Postfix();
        settingsMenu.UpdateSetting(button, Interaction.Decrement);
        Check(!Mod.settings.XPerfectOnly && Mod.settings.saveCalls == 1, "An explicit ordinary Pure selection still clears the saved XOnly preference");
        Persistence.enableCompetitiveMode = true;
        MarginLimitMenu.CompetitiveModePatch.Postfix();
        Check(!XPerfectOnly.Active, "Competitive mode alone must not enable XOnly");
        Console.WriteLine($"XPerfectOnly: {checks} checks passed.");
    }
}
