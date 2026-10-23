using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;
using static AccurateJudgementBar.MarginLimitState;
using Mode = AccurateJudgementBar.MarginLimitState.Mode;

namespace AccurateJudgementBar
{
    // Extra states exist only in the UI. Never cast them into GCS or Persistence.
    internal static class MarginLimitMenu
    {
        private sealed class RestartState { public bool Contributed; }
        private static readonly ConditionalWeakTable<PauseSettingButton, RestartState> restartStates = new();

        private static Mode ReadUI(object? value) => value is Mode mode
            ? mode
            : ReadEnabled(value is HitMarginLimit limit ? limit : GCS.hitMarginLimit);

        private static string Label(Mode mode) => mode switch
        {
            Mode.NoCheckpoint => Localization.Current.Get("NoCheckpoint"),
            Mode.NoMissOnly => Localization.Current.Get("NoMissOnly"),
            Mode.XPerfectOnly => Localization.Current.Get(Persistence.enableCompetitiveMode ? "XPerfectOnly" : "XPerfectOnlyInactive"),
            _ => RDString.Get("pauseMenu.settings.HitMarginLimit." + ToNative(mode), null)
        };

        // Called on toggle and language changes, including menus that are currently hidden.
        internal static void Refresh()
        {
            if (Main.is_mod_enable && (TogetherCompatibility.InSession || TogetherCompatibility.MenuResetPending))
            {
                TogetherCompatibility.Update();
                DisplayErrors.Run("Together limit menu refresh", ResetForTogether);
                return;
            }
            foreach (var menu in Resources.FindObjectsOfTypeAll<SettingsMenu>())
            foreach (var setting in menu.GetComponentsInChildren<PauseSettingButton>(true))
            {
                if (setting.name != "hitMarginLimit" || setting.valueLabel == null) continue;
                Mode current = Current;
                if (!Main.is_mod_enable)
                {
                    if (setting.initialValue is Mode initial)
                    {
                        // Remove only the restart contribution that disappears when an AJB
                        // mode falls back to its native value; preserve other settings' changes.
                        if (setting.restartOnChange && !setting.deferUpdate)
                        {
                            bool wasChanged = initial != Stored;
                            bool isChanged = ToNative(initial) != GCS.hitMarginLimit;
                            menu.settingsThatRequireRestart += (isChanged ? 1 : 0) - (wasChanged ? 1 : 0);
                            if (restartStates.TryGetValue(setting, out var restart)) restart.Contributed = isChanged;
                        }
                        setting.initialValue = ToNative(initial);
                    }
                    if (setting.CachedValue is Mode cached) setting.CachedValue = ToNative(cached);
                }
                else if (setting.initialValue is HitMarginLimit initial)
                {
                    setting.initialValue = ReadEnabled(initial);
                }
                var displayed = setting.deferUpdate && setting.CachedValue != null
                    ? ReadUI(setting.CachedValue) : current;
                if (!Main.is_mod_enable) displayed = Read(ToNative(displayed), false, false);
                setting.valueLabel.text = Label(displayed);
                if (setting.deferUpdate && setting.initialValue != null)
                    setting.confirmText.gameObject.SetActive(ReadUI(setting.initialValue) != displayed);
            }
        }

        internal static void ResetForTogether()
        {
            foreach (var menu in Resources.FindObjectsOfTypeAll<SettingsMenu>())
            foreach (var setting in menu.GetComponentsInChildren<PauseSettingButton>(true))
            {
                if (setting.name != "hitMarginLimit") continue;
                DisplayErrors.Run("Together limit menu reset setting", () => ResetTogetherSetting(menu, setting));
            }
        }

        private static void ResetTogetherSetting(SettingsMenu menu, PauseSettingButton setting)
        {
            // Cancel deferred choices as well as already displayed AJB enum values.
            // Remove only a restart contribution observed in our own immediate-mode
            // callback. Native scene resets can erase the current mode before we see it.
            if (restartStates.TryGetValue(setting, out var restart) && restart.Contributed)
            {
                if (menu.settingsThatRequireRestart > 0) menu.settingsThatRequireRestart--;
                restart.Contributed = false;
            }
            setting.initialValue = Mode.None;
            setting.CachedValue = null;
            if (setting.valueLabel != null) setting.valueLabel.text = Label(Mode.None);
            if (setting.confirmText != null) setting.confirmText.gameObject.SetActive(false);
        }

        [HarmonyPatch(typeof(Persistence), "set_enableCompetitiveMode")]
        internal static class CompetitiveModePatch
        {
            [HarmonyPostfix]
            internal static void Postfix()
            {
                // Refresh the effective label without saving or discarding the user's preference.
                if (Main.is_mod_enable && Main.settings != null) Refresh();
            }
        }

        [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
        internal static class SettingPatch
        {
            [HarmonyPrefix]
            internal static bool Prefix(SettingsMenu __instance, PauseSettingButton setting, SettingsMenu.Interaction action)
            {
                if (!Main.is_mod_enable || Main.settings == null || setting.name != "hitMarginLimit") return true;
                if (TogetherCompatibility.InSession || TogetherCompatibility.MenuResetPending)
                {
                    TogetherCompatibility.Update();
                    DisplayErrors.Run("Together limit menu setting", () => ResetTogetherSetting(__instance, setting));
                    return false;
                }

                bool refresh = action == SettingsMenu.Interaction.Refresh;
                if (refresh && restartStates.TryGetValue(setting, out var restart)) restart.Contributed = false;
                bool increment = action == SettingsMenu.Interaction.Increment;
                bool decrement = action == SettingsMenu.Interaction.Decrement;
                Mode mode = Current;
                if (refresh) setting.CachedValue = null;
                else if (setting.deferUpdate && setting.CachedValue != null) mode = ReadUI(setting.CachedValue);
                Mode initial = refresh ? mode : ReadUI(setting.initialValue);
                bool wasChanged = initial != mode;
                const int count = 6;
                if (increment || decrement) mode = (Mode)(((int)mode + (increment ? 1 : count - 1)) % count);
                setting.initialValue = initial;
                setting.valueLabel.text = Label(mode);

                if (setting.deferUpdate)
                {
                    setting.CachedValue = mode;
                    setting.confirmText.gameObject.SetActive(initial != mode);
                }
                if (increment || decrement)
                {
                    if (!setting.deferUpdate)
                    {
                        Apply(mode);
                        if (setting.restartOnChange)
                        {
                            __instance.settingsThatRequireRestart += (initial != mode ? 1 : 0) - (wasChanged ? 1 : 0);
                            restartStates.GetValue(setting, _ => new RestartState()).Contributed = initial != mode;
                        }
                    }
                    setting.PlayArrowAnimation(increment);
                    scrController.instance?.pauseMenu?.PlayMenuSfx(increment ? SfxSound.MenuIncrement : SfxSound.MenuDecrement, 1f);
                }
                if (action == SettingsMenu.Interaction.Activate && setting.deferUpdate && initial != mode)
                {
                    Apply(mode);
                    setting.initialValue = mode;
                    setting.confirmText.gameObject.SetActive(false);
                    if (setting.restartOnChange) __instance.settingsThatRequireRestart++;
                }
                return false;
            }
        }
    }
}
