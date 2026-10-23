using System;
using System.Reflection;
using System.Reflection.Emit;
using AccurateJudgementBar;
using UnityEngine;
using UnityModManagerNet;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;
using Interaction = SettingsMenu.Interaction;
using Mod = AccurateJudgementBar.Main;

// Deliberately resides in this test assembly: Together may load its runtime from an
// assembly other than the UMM bootstrap, and AJB has no compile-time Together reference.
namespace Together
{
    internal static class Main
    {
        internal static bool IsInTogether;
        internal static UnityModManager.ModEntry? ModEntry;
    }
}

internal static class TogetherCompatibilityTests
{
    private static int checks;
    private static DateTime utcTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("TogetherCompatibility: " + message);
    }

    private static void Reset()
    {
        UnityModManager.TogetherEntry = null;
        Together.Main.ModEntry = null;
        Together.Main.IsInTogether = false;
        Mod.is_mod_enable = true;
        Mod.Mod = new();
        Mod.settings = new Settings();
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.None;
        Persistence.enableCompetitiveMode = true;
        scrController.instance = new();
        scrLivesCounter.instance = null;
        ADOBase.isExpo = RDC.debug = RDC.useOldAuto = false;
        RDString.failMessage = false;
        Resources.Objects = Array.Empty<object>();
        utcTime = utcTime.AddMinutes(1);
        TogetherCompatibility.UtcNow = () => utcTime;
        TogetherCompatibility.Update();
        DisplayErrors.Reset();
    }

    private static UnityModManager.ModEntry Enter()
    {
        var entry = new UnityModManager.ModEntry { Active = true };
        Together.Main.ModEntry = UnityModManager.TogetherEntry = entry;
        Together.Main.IsInTogether = true;
        return entry;
    }

    private static (SettingsMenu Menu, PauseSettingButton Button) Menu(bool deferred = false)
    {
        var button = new PauseSettingButton { deferUpdate = deferred };
        var menu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 3 };
        Resources.Objects = new object[] { menu };
        menu.UpdateSetting(button, Interaction.Refresh);
        return (menu, button);
    }

    private static void Detection()
    {
        Reset();
        Together.Main.IsInTogether = true;
        Check(!TogetherCompatibility.InSession, "A loaded runtime without an active UMM entry is insufficient");
        var entry = Enter();
        entry.Active = false;
        Check(!TogetherCompatibility.InSession, "An inactive Together entry cannot activate compatibility");
        entry.Active = true;
        Together.Main.IsInTogether = false;
        Check(!TogetherCompatibility.InSession, "Together enabled outside its session preserves single-player modes");
        Together.Main.IsInTogether = true;
        Check(TogetherCompatibility.InSession, "An active entry and its actual runtime session activate compatibility");
        Together.Main.ModEntry = new UnityModManager.ModEntry { Active = true };
        Check(!TogetherCompatibility.InSession, "A runtime belonging to another entry must not be trusted");
        Together.Main.ModEntry = null;
        Check(!TogetherCompatibility.InSession, "An uninitialized runtime must not be trusted");
        Together.Main.ModEntry = entry;
        Check(TogetherCompatibility.InSession, "A corrected runtime identity becomes visible without restarting AJB");

        // A second assembly uses the same type name but belongs to a stale/different mod.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("TogetherStaleRuntime"), AssemblyBuilderAccess.Run);
        var typeBuilder = assembly.DefineDynamicModule("Runtime").DefineType("Together.Main", TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
        typeBuilder.DefineField("IsInTogether", typeof(bool), FieldAttributes.Assembly | FieldAttributes.Static);
        typeBuilder.DefineField("ModEntry", typeof(UnityModManager.ModEntry), FieldAttributes.Assembly | FieldAttributes.Static);
        var staleType = typeBuilder.CreateType()!;
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        staleType.GetField("IsInTogether", flags)!.SetValue(null, true);
        staleType.GetField("ModEntry", flags)!.SetValue(null, new UnityModManager.ModEntry { Active = true });
        Together.Main.IsInTogether = false;
        Check(!TogetherCompatibility.InSession, "A same-named type in another assembly cannot impersonate this UMM entry");
        Together.Main.IsInTogether = true;
        Check(TogetherCompatibility.InSession, "An unrelated runtime cannot hide the correctly identified runtime");
        UnityModManager.TogetherEntry = new UnityModManager.ModEntry { Active = true };
        Check(!TogetherCompatibility.InSession, "Replacing the UMM entry discards a previously matching runtime");
        Together.Main.ModEntry = UnityModManager.TogetherEntry;
        Check(TogetherCompatibility.InSession, "The runtime can bind to a newly loaded UMM entry");
    }

    private static void ImmediateGameplaySuppression()
    {
        foreach (bool original in new[] { false, true })
        {
            Reset();
            MarginLimitState.Apply(Mode.NoMissOnly);
            var player = new scrPlayer();
            player.marginTracker.hitMargins.Add(HitMargin.TooEarly);
            Enter(); // No compatibility Update or menu refresh has run yet.
            NoMissOnly.DeathPatch.Postfix(player, HitMargin.TooEarly, false);
            Check(!NoMissOnly.Active && player.alive && player.deathCalls == 0 && player.syntheticHits == 0,
                "No Miss enforcement stops at the session boundary before the next UMM update");
            Check(player.marginTracker.hitMargins.Count == 1 && player.marginTracker.hitMargins[0] == HitMargin.TooEarly,
                "Together suppression preserves the true miss record");

            Reset();
            MarginLimitState.Apply(Mode.XPerfectOnly);
            player = new scrPlayer();
            Enter();
            bool counted = original;
            XPerfectOnly.CountedPatch.Postfix(HitMargin.PerfectPlus, null, ref counted);
            Check(!XPerfectOnly.Active && counted == original, "The shared IsCounted hook preserves its native result immediately");
            counted = original;
            XPerfectOnly.CountedPatch.Postfix(HitMargin.PerfectMinus, HitMarginLimit.PurePerfectOnly, ref counted);
            Check(counted == original, "Explicit PurePerfectOnly queries remain native in Together");
            bool result = original;
            XPerfectOnly.DeathPatch.Postfix(player, HitMargin.PerfectPlus, ref result);
            Check(player.alive && player.deathCalls == 0 && player.syntheticHits == 0 && result == original,
                "XPerfect death suppression preserves OnDamage's result");

            Reset();
            MarginLimitState.Apply(Mode.PerfectsOnly);
            player = new scrPlayer();
            Enter();
            result = original;
            PerfectsOnlyDeath.Postfix(player, HitMargin.VeryEarly, ref result);
            Check(player.alive && player.deathCalls == 0 && player.syntheticHits == 0 && result == original,
                "Perfects Only patch does not add death during Together initialization");
        }

    }

    private static void ClearAndSave()
    {
        foreach (var mode in Enum.GetValues<Mode>())
        {
            Reset();
            MarginLimitState.Apply(mode);
            int saves = Mod.settings.saveCalls;
            Enter();
            TogetherCompatibility.Update();
            Check(!Mod.settings.NoCheckpoint && !Mod.settings.NoMissOnly && !Mod.settings.XPerfectOnly,
                "Joining clears every AJB-only choice: " + mode);
            Check(GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
                "Joining clears the native restriction with a legal None value: " + mode);
            int expectedSaves = saves + (mode is Mode.NoCheckpoint or Mode.NoMissOnly or Mode.XPerfectOnly ? 1 : 0);
            Check(Mod.settings.saveCalls == expectedSaves, "Only changed persisted AJB flags require one save: " + mode);
            TogetherCompatibility.Update();
            Check(Mod.settings.saveCalls == expectedSaves, "Repeated session updates cannot repeatedly save cleared settings");
            Together.Main.IsInTogether = false;
            TogetherCompatibility.Update();
            Check(!NoMissOnly.Active && !XPerfectOnly.Active &&
                !Mod.settings.NoCheckpoint && !Mod.settings.NoMissOnly && !Mod.settings.XPerfectOnly &&
                GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
                "Leaving Together does not restore a discarded choice: " + mode);
        }

        foreach (bool enabled in new[] { false, true })
        {
            Reset();
            MarginLimitState.Apply(Mode.NoMissOnly);
            int saves = Mod.settings.saveCalls;
            var entry = Enter();
            if (enabled) Together.Main.IsInTogether = false;
            else Mod.is_mod_enable = false;
            TogetherCompatibility.Update();
            Check(Mod.settings.NoMissOnly && Mod.settings.saveCalls == saves,
                "Disabled AJB and Together's ordinary single-player state preserve existing settings");
            entry.Active = false;
            Mod.is_mod_enable = true;
            Together.Main.IsInTogether = true;
            TogetherCompatibility.Update();
            Check(Mod.settings.NoMissOnly && Mod.settings.saveCalls == saves, "Inactive Together cannot erase a preference");
        }

        Reset();
        Mod.settings.XPerfectOnly = Mod.settings.NoMissOnly = true; // Recover an inconsistent legacy file too.
        Enter();
        TogetherCompatibility.Update();
        Check(!Mod.settings.XPerfectOnly && !Mod.settings.NoMissOnly && Mod.settings.saveCalls == 1,
            "Both stale persisted flags are cleared together and saved once");
    }

    private static void Menus()
    {
        foreach (var pending in new[] { Mode.NoCheckpoint, Mode.NoMissOnly, Mode.XPerfectOnly })
        {
            Reset();
            var (menu, button) = Menu(true);
            for (int step = 0; step < (int)pending; step++) menu.UpdateSetting(button, Interaction.Increment);
            Check(button.CachedValue is Mode mode && mode == pending && button.confirmText.gameObject.active,
                "The pre-join preview awaits confirmation: " + pending);
            Enter();
            TogetherCompatibility.Update();
            Check(button.CachedValue == null && button.initialValue is Mode.None && !button.confirmText.gameObject.active,
                "Joining cancels the deferred value and baseline: " + pending);
            foreach (var action in new[] { Interaction.Activate, Interaction.Increment, Interaction.Decrement, Interaction.Refresh })
            {
                menu.UpdateSetting(button, action);
                Check(button.valueLabel.text.EndsWith(".None") && !button.confirmText.gameObject.active &&
                    !Mod.settings.NoCheckpoint && !Mod.settings.NoMissOnly && !Mod.settings.XPerfectOnly &&
                    GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
                    "Together locks every menu interaction to None: " + action);
            }
            Check(menu.settingsThatRequireRestart == 3 && Mod.settings.saveCalls == 0,
                "An unconfirmed choice cannot create a save or restart contribution when canceled");
            Together.Main.IsInTogether = false;
            menu.UpdateSetting(button, Interaction.Activate);
            Check(!NoMissOnly.Active && !XPerfectOnly.Active,
                "Leaving cannot confirm the pre-join deferred choice");
        }

        Reset();
        var (immediateMenu, immediateButton) = Menu();
        immediateMenu.UpdateSetting(immediateButton, Interaction.Increment);
        immediateMenu.UpdateSetting(immediateButton, Interaction.Increment);
        Check(immediateMenu.settingsThatRequireRestart == 4, "A real No Miss change contributes one restart before joining");
        Enter();
        TogetherCompatibility.Update();
        TogetherCompatibility.Update();
        MarginLimitMenu.Refresh();
        Check(immediateMenu.settingsThatRequireRestart == 3 && immediateButton.initialValue is Mode.None,
            "Repeated joining refreshes remove only the observed AJB contribution and retain other restart requests");
        var rebuilt = new PauseSettingButton();
        immediateMenu.UpdateSetting(rebuilt, Interaction.Increment);
        Check(rebuilt.valueLabel.text.EndsWith(".None") && !NoMissOnly.Active, "A rebuilt menu stays locked to None");
        var other = new PauseSettingButton { name = "unrelated" };
        Check(MarginLimitMenu.SettingPatch.Prefix(immediateMenu, other, Interaction.Increment),
            "Together compatibility does not intercept unrelated official settings");
        Together.Main.IsInTogether = false;
        immediateMenu.UpdateSetting(immediateButton, Interaction.Increment);
        immediateMenu.UpdateSetting(immediateButton, Interaction.Increment);
        Check(NoMissOnly.Active, "After leaving, a new explicit selection works normally");
    }

    private static void DisplayBoundary()
    {
        Reset();
        Mod.settings.NoMissOnly = true;
        Mod.settings.XPerfectOnly = true;
        Enter();
        Resources.Objects = new object[] { new object() }; // Force the menu enumeration to fail.
        TogetherCompatibility.Update();
        Check(!Mod.settings.NoCheckpoint && !Mod.settings.NoMissOnly && !Mod.settings.XPerfectOnly && Mod.settings.saveCalls == 1,
            "A failing display refresh cannot retain an armed gameplay restriction or skip its save");
        Check(GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
            "Display failure still leaves native gameplay unrestricted");
        Check(Mod.Mod.Logger.Errors.Count == 1, "Display failure is reported at the AJB-owned boundary");
        TogetherCompatibility.Update();
        Check(Mod.settings.saveCalls == 1 && Mod.Mod.Logger.Errors.Count == 1,
            "Repeated display failures neither repeat the save nor flood diagnostics");
        Resources.Objects = Array.Empty<object>();
        Check(MeterWatcher.GetRed() == new Color32(255, 0, 0, 255) &&
            MeterWatcher.GetOrange() == new Color32(255, 111, 77, 255) &&
            MeterWatcher.GetYellow() == new Color32(252, 255, 77, 255) &&
            MeterWatcher.GetGreen() == new Color32(95, 255, 78, 255),
            "Cleared choices restore the native timing-band colors");
    }

    private static void SaveRecovery()
    {
        Reset();
        MarginLimitState.Apply(Mode.NoMissOnly);
        var settings = Mod.settings;
        int saves = settings.saveCalls, attempts = settings.saveAttempts;
        settings.saveError = new InvalidOperationException("Settings file temporarily locked");
        Enter();
        TogetherCompatibility.Update();
        Check(!settings.NoMissOnly && !settings.XPerfectOnly &&
            GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
            "A failed save still clears the in-memory choices and native restrictions immediately");
        Check(settings.saveAttempts == attempts + 1 && settings.saveCalls == saves && Mod.Mod.Logger.Errors.Count == 1,
            "A failed persistence attempt is recorded once without reporting success");
        TogetherCompatibility.Update();
        utcTime = utcTime.AddMilliseconds(999);
        TogetherCompatibility.Update();
        Check(settings.saveAttempts == attempts + 1, "A locked settings file is not retried every frame");
        utcTime = utcTime.AddMilliseconds(1);
        TogetherCompatibility.Update();
        Check(settings.saveAttempts == attempts + 2 && settings.saveCalls == saves && Mod.Mod.Logger.Errors.Count == 1,
            "A still-failing save retries at one second without repeating diagnostics");

        Together.Main.IsInTogether = false;
        settings.saveError = null;
        TogetherCompatibility.Update();
        Check(settings.saveAttempts == attempts + 2 && !NoMissOnly.Active,
            "Leaving Together retains the pending save and never re-arms the discarded mode");
        utcTime = utcTime.AddSeconds(1);
        TogetherCompatibility.Update();
        Check(settings.saveAttempts == attempts + 3 && settings.saveCalls == saves + 1 &&
            !settings.NoMissOnly && !settings.XPerfectOnly &&
            GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
            "The next retry saves the cleared choices successfully even after leaving Together");
        utcTime = utcTime.AddSeconds(5);
        TogetherCompatibility.Update();
        Check(settings.saveAttempts == attempts + 3 && settings.saveCalls == saves + 1,
            "A successful retry completes the pending work and stops further saves");
    }

    private static void DeferredDisplayRecovery()
    {
        foreach (var pending in new[] { Mode.NoCheckpoint, Mode.NoMissOnly, Mode.XPerfectOnly })
        {
            Reset();
            var (menu, button) = Menu(true);
            for (int step = 0; step < (int)pending; step++) menu.UpdateSetting(button, Interaction.Increment);
            var hidden = new PauseSettingButton { deferUpdate = true, initialValue = Mode.None, CachedValue = pending };
            hidden.confirmText.gameObject.SetActive(true);
            var hiddenMenu = new SettingsMenu { buttons = new[] { hidden }, settingsThatRequireRestart = 7 };
            Enter();
            Resources.Objects = new object[] { new object() };
            TogetherCompatibility.Update();
            Check(TogetherCompatibility.MenuResetPending && button.CachedValue is Mode mode && mode == pending,
                "Enumeration failure keeps the stale preview scheduled for cleanup: " + pending);
            Check(!Mod.settings.NoCheckpoint && !Mod.settings.NoMissOnly && !Mod.settings.XPerfectOnly,
                "A pending display cleanup does not retain armed restrictions");

            Together.Main.IsInTogether = false;
            MarginLimitState.Apply(pending);
            Check(!NoMissOnly.Active && !XPerfectOnly.Active && Mod.settings.saveCalls == 0,
                "A pending cleanup blocks direct restoration of the discarded preview after leaving");
            menu.UpdateSetting(button, Interaction.Activate);
            Check(button.CachedValue == null && button.initialValue is Mode.None && !button.confirmText.gameObject.active &&
                !NoMissOnly.Active && !XPerfectOnly.Active && Mod.settings.saveCalls == 0,
                "Confirming before enumeration recovers cancels the stale choice instead of activating it");
            Check(TogetherCompatibility.MenuResetPending, "Cleaning one button cannot complete a failed whole-menu enumeration");

            Resources.Objects = new object[] { menu, hiddenMenu };
            utcTime = utcTime.AddMilliseconds(999);
            TogetherCompatibility.Update();
            Check(TogetherCompatibility.MenuResetPending && hidden.CachedValue is Mode,
                "Whole-menu recovery follows the one-second retry interval");
            utcTime = utcTime.AddMilliseconds(1);
            TogetherCompatibility.Update();
            Check(!TogetherCompatibility.MenuResetPending && hidden.CachedValue == null && hidden.initialValue is Mode.None &&
                !hidden.confirmText.gameObject.active && hiddenMenu.settingsThatRequireRestart == 7,
                "The delayed retry cleans hidden menus after exit and preserves unrelated restart requests");
            menu.UpdateSetting(button, Interaction.Activate);
            hiddenMenu.UpdateSetting(hidden, Interaction.Activate);
            Check(!NoMissOnly.Active && !XPerfectOnly.Active && Mod.settings.saveCalls == 0 &&
                GCS.hitMarginLimit == HitMarginLimit.None && Persistence.hitMarginLimit == HitMarginLimit.None,
                "Recovered confirmations cannot revive either pre-join preview");
        }
    }

    private static void TrackerResetBoundary()
    {
        foreach (bool inSession in new[] { false, true })
        {
            Reset();
            MarginLimitState.Apply(Mode.XPerfectOnly);
            var tracker = new scrMarginTracker();
            tracker.hitMargins.AddRange(new[] { HitMargin.XPerfect, HitMargin.PerfectPlus, HitMargin.TooEarly });
            var records = tracker.hitMargins;
            int saves = Mod.settings.saveCalls;
            if (inSession) Enter();
            TogetherCompatibility.TrackerResetPatch.Prefix(); // Original Reset has not run yet.
            Check(ReferenceEquals(records, tracker.hitMargins) && tracker.hitMargins.Count == 3 &&
                tracker.hitMargins[0] == HitMargin.XPerfect && tracker.hitMargins[1] == HitMargin.PerfectPlus &&
                tracker.hitMargins[2] == HitMargin.TooEarly,
                "The reset prefix leaves all native judgement records for the original Reset to handle");
            Check(Mod.settings.XPerfectOnly == !inSession && !Mod.settings.NoMissOnly &&
                GCS.hitMarginLimit == (inSession ? HitMarginLimit.None : HitMarginLimit.PurePerfectOnly) &&
                Persistence.hitMarginLimit == GCS.hitMarginLimit && Mod.settings.saveCalls == saves + (inSession ? 1 : 0),
                "The reset boundary clears Together's stale limit before native snapshots while preserving single-player choices");
            bool accepted = HitMargin.PerfectPlus.IsCounted(GCS.hitMarginLimit);
            XPerfectOnly.CountedPatch.Postfix(HitMargin.PerfectPlus, null, ref accepted);
            Check(accepted == inSession,
                "Native Reset's subsequent IsCounted query sees None in Together and the selected XPerfect rule in single-player");
            if (inSession)
            {
                TogetherCompatibility.TrackerResetPatch.Prefix();
                Check(Mod.settings.saveCalls == saves + 1 && tracker.hitMargins.Count == 3,
                    "Repeated tracker reset callbacks neither save twice nor clear the previous records prematurely");
            }
        }
    }

    internal static void Run()
    {
        Detection();
        ImmediateGameplaySuppression();
        ClearAndSave();
        Menus();
        DisplayBoundary();
        SaveRecovery();
        DeferredDisplayRecovery();
        TrackerResetBoundary();
        Reset();
        TogetherCompatibility.UtcNow = () => DateTime.UtcNow;
        Console.WriteLine($"TogetherCompatibility: {checks} checks passed.");
    }
}
