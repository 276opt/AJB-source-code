using System;
using AccurateJudgementBar;
using UnityEngine;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;
using Interaction = SettingsMenu.Interaction;
using Mod = AccurateJudgementBar.Main;

internal static class AprilFoolsTests
{
    private static int checks;
    private static DateTime localTime;
    private static readonly DateTime[] boundaries = {
        new(2026, 3, 31, 23, 59, 59), new(2026, 4, 1),
        new(2026, 4, 1, 23, 59, 59), new(2026, 4, 2)
    };

    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("AprilFools: " + message); }

    private static void Reset()
    {
        localTime = boundaries[0];
        AprilFools.LocalNow = () => localTime;
        Mod.is_mod_enable = true;
        Mod.settings = new Settings();
        Mod.Mod = new();
        AprilFools.Initialize(Mod.Mod);
        GCS.hitMarginLimit = Persistence.hitMarginLimit = HitMarginLimit.None;
        Persistence.enableCompetitiveMode = true;
        scrController.instance = new();
        Resources.Objects = Array.Empty<object>();
    }

    private static (SettingsMenu Menu, PauseSettingButton Button) Menu(bool deferred = false)
    {
        var button = new PauseSettingButton { deferUpdate = deferred };
        var menu = new SettingsMenu { buttons = new[] { button }, settingsThatRequireRestart = 3 };
        Resources.Objects = new object[] { menu };
        menu.UpdateSetting(button, Interaction.Refresh);
        return (menu, button);
    }

    private static (Color, Color, Color, Color, Color) Colors() =>
        (MeterWatcher.GetRed(), MeterWatcher.GetOrange(), MeterWatcher.GetYellow(),
            MeterWatcher.GetGreen(), MeterWatcher.GetBlue());

    private static (Mode, HitMarginLimit, HitMarginLimit, bool, bool, bool, bool, bool, bool) Selection() =>
        (MarginLimitState.Current, GCS.hitMarginLimit, Persistence.hitMarginLimit,
            Mod.settings.NoCheckpoint, Mod.settings.NoMissOnly, Mod.settings.XPerfectOnly,
            NoCheckpoint.Active, NoMissOnly.Active, XPerfectOnly.Active);

    internal static void Run()
    {
        foreach (int year in new[] { 2026, 2027, 2028 })
        {
            Check(AprilFools.IsAprilFirst(new DateTime(year, 4, 1, 0, 0, 0)), "Local midnight starts rename");
            Check(AprilFools.IsAprilFirst(new DateTime(year, 4, 1, 23, 59, 59)), "All of April 1 is included");
            Check(!AprilFools.IsAprilFirst(new DateTime(year, 3, 31, 23, 59, 59)), "March 31 is excluded");
            Check(!AprilFools.IsAprilFirst(new DateTime(year, 4, 2)), "April 2 is excluded");
            Check(!AprilFools.IsAprilFirst(new DateTime(year, 10, 1)), "Other first days are excluded");
        }

        Reset();
        foreach (var date in boundaries)
        {
            localTime = date;
            Check(AprilFools.SettingsTitle("Localized normal title") == (date.Month == 4 && date.Day == 1
                ? "Inaccurate Judgement Bar by opt276" : "Localized normal title"),
                "Title changes only the name on the holiday");
            Mod.is_mod_enable = false;
            Check(AprilFools.SettingsTitle("Localized normal title") == "Localized normal title",
                "Disabled title preserves localization on every date");
            Mod.is_mod_enable = true;
        }

        // Date changes must not add a seventh choice, even when traversing backwards.
        foreach (var date in boundaries)
        foreach (var direction in new[] { Interaction.Increment, Interaction.Decrement })
        {
            Reset();
            localTime = date;
            AprilFools.Update();
            var (menu, button) = Menu();
            var expected = direction == Interaction.Increment
                ? new[] { Mode.NoCheckpoint, Mode.NoMissOnly, Mode.PerfectsOnly, Mode.PurePerfectOnly, Mode.XPerfectOnly, Mode.None }
                : new[] { Mode.XPerfectOnly, Mode.PurePerfectOnly, Mode.PerfectsOnly, Mode.NoMissOnly, Mode.NoCheckpoint, Mode.None };
            foreach (var mode in expected)
            {
                menu.UpdateSetting(button, direction);
                Check(MarginLimitState.Current == mode, "Six-mode menu order on " + date);
                Check(Persistence.hitMarginLimit == MarginLimitState.ToNative(mode) && Enum.IsDefined(GCS.hitMarginLimit),
                    "Only native enum values are saved");
                Check(menu.settingsThatRequireRestart == (mode == Mode.None ? 3 : 4),
                    "Cycling back preserves unrelated restart requests");
            }
        }

        // Keep active choices and unconfirmed previews across both midnights. Refresh
        // also exercises language/competitive callbacks after the date branch removal.
        foreach (var mode in Enum.GetValues<Mode>())
        foreach (bool deferred in new[] { false, true })
        foreach (bool show in new[] { false, true })
        foreach (bool competitive in new[] { false, true })
        {
            Reset();
            Mod.settings.ShowHitMarginLimit = show;
            Persistence.enableCompetitiveMode = competitive;
            MarginLimitState.Apply(mode);
            var (menu, button) = Menu(deferred);
            menu.UpdateSetting(button, Interaction.Increment);
            var selection = Selection();
            var colors = Colors();
            var initial = button.initialValue;
            var cached = button.CachedValue;
            string label = button.valueLabel.text;
            bool confirm = button.confirmText.gameObject.active;
            int saves = Mod.settings.saveCalls, restarts = menu.settingsThatRequireRestart;
            foreach (var date in boundaries)
            {
                localTime = date;
                AprilFools.Update();
                MarginLimitMenu.Refresh();
                Check(Selection() == selection && Mod.settings.saveCalls == saves,
                    "Rename/refresh leaves selections and checkpoint enforcement unchanged");
                Check(Colors() == colors, "Rename never overrides normal or selected-mode colors");
                Check(Equals(button.initialValue, initial) && Equals(button.CachedValue, cached) &&
                    button.valueLabel.text == label && button.confirmText.gameObject.active == confirm &&
                    menu.settingsThatRequireRestart == restarts,
                    "Date changes preserve pending confirmation and restart bookkeeping");
            }
        }
        Reset();
        AprilFools.LocalNow = () => DateTime.Now;
        Console.WriteLine($"AprilFools: {checks} checks passed.");
    }
}
