using System;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace AccurateJudgementBar
{
    // Only the custom preference is owned by AJB. The game always retains a native enum.
    internal static class ErrorMeterSizeSettings
    {
        internal const float MinScale = 0.25f;
        internal const float MaxScale = 4f;
        internal const int CustomIndex = 5;

        internal static bool CustomSelected => Main.settings.CustomErrorMeterSize &&
            Persistence.hitErrorMeterSize != ErrorMeterSize.Off;
        internal static bool Active => Main.is_mod_enable && Main.settings != null && CustomSelected;
        internal static int SelectedIndex => CustomSelected ? CustomIndex : (int)Persistence.hitErrorMeterSize;

        internal static float Sanitize(float scale) => float.IsNaN(scale) || float.IsInfinity(scale)
            ? 1f : Mathf.Clamp(scale, MinScale, MaxScale);

        internal static float NativeScale(ErrorMeterSize size) => size switch
        {
            ErrorMeterSize.Small => 0.75f,
            ErrorMeterSize.Large => 1.5f,
            ErrorMeterSize.ExtraLarge => 2f,
            _ => 1f
        };

        internal static float SliderScale => CustomSelected
            ? Sanitize(Main.settings.CustomErrorMeterScale)
            : NativeScale(Persistence.hitErrorMeterSize);

        // Use the game's current translations, including its wording for Normal/ExtraLarge.
        internal static string NativeLabel(ErrorMeterSize size) =>
            RDString.Get("pauseMenu.settings.ErrorMeterSize." + size, null);

        // Read the official wording in AJB's selected language without changing the game language.
        // Other language packs use English, like AJB's embedded fallback.
        internal static string SettingsLabel(ErrorMeterSize size)
        {
            var language = string.Equals(Localization.Current.SelectedLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase)
                ? SystemLanguage.ChineseSimplified : SystemLanguage.English;
            return SA.GoogleDoc.Localization.GetLocalizedString("pauseMenu.settings.ErrorMeterSize." + size, language);
        }

        internal static void Select(int index)
        {
            if (index < 0 || index > CustomIndex) return;
            Main.settings.CustomErrorMeterSize = index == CustomIndex;
            if (index == CustomIndex)
            {
                // Vanilla skips both visibility and AddHit when Off. Keep that path native.
                // A non-Off selection remains the fallback when AJB is disabled/uninstalled.
                if (Persistence.hitErrorMeterSize == ErrorMeterSize.Off)
                    Persistence.hitErrorMeterSize = ErrorMeterSize.Normal;
            }
            else Persistence.hitErrorMeterSize = (ErrorMeterSize)index;
            Refresh();
        }

        internal static void SetScale(float scale)
        {
            Main.settings.CustomErrorMeterScale = Sanitize(scale);
            Select(CustomIndex);
        }

        // Match all four native positions, interpolating between them for continuous sizing.
        internal static float PositionY(float scale) => scale < 0.75f ? -40f * scale :
            scale < 1f ? -30f - (scale - 0.75f) * 72f : -48f - (scale - 1f) * 46f;

        internal static void Refresh()
        {
            Guard(() =>
            {
                var controller = scrController.instance;
                var meter = controller?.errorMeter;
                if (meter == null) return;
                var size = Persistence.hitErrorMeterSize;
                // Off has no layout branch in vanilla; reset a previously customized wrapper.
                meter.UpdateLayout(size == ErrorMeterSize.Off ? ErrorMeterSize.Normal : size,
                    Persistence.hitErrorMeterShape);
                if (size == ErrorMeterSize.Off) meter.gameObject.SetActive(false);
                else controller!.UpdateErrorMeterVisibility();
            });
            RefreshMenus();
        }

        internal static void RefreshMenus()
        {
            foreach (var menu in Resources.FindObjectsOfTypeAll<SettingsMenu>())
            foreach (var setting in menu.GetComponentsInChildren<PauseSettingButton>(true))
                Guard(() => UpdateLabel(setting));
        }

        private static void UpdateLabel(PauseSettingButton setting)
        {
            if (setting.name != "hitErrorMeterSize" || setting.valueLabel == null) return;
            var displayed = setting.deferUpdate && setting.CachedValue is ErrorMeterSize cached
                ? cached : Persistence.hitErrorMeterSize;
            setting.valueLabel.text = Active && displayed == Persistence.hitErrorMeterSize
                ? Localization.Current.Get("ErrorMeterSizeCustomViaAJB") : NativeLabel(displayed);
        }

        private static void Guard(Action displayAction)
        {
            try { displayAction(); }
            catch (Exception ex) { Main.Mod?.Logger.Error("AJB error meter size display: " + ex); }
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), nameof(scrHitErrorMeter.UpdateLayout))]
        internal static class LayoutPatch
        {
            // Runs before MeterPositionLayoutPatch (Priority.Last), so offsets use this baseline.
            [HarmonyPostfix]
            internal static void Postfix(scrHitErrorMeter __instance, ErrorMeterSize size)
            {
                Guard(() =>
                {
                    if (!Active || size == ErrorMeterSize.Off || __instance.wrapperRectTransform == null) return;
                    float scale = Sanitize(Main.settings.CustomErrorMeterScale);
                    var wrapper = __instance.wrapperRectTransform;
                    wrapper.anchoredPosition = new Vector2(wrapper.anchoredPosition.x, PositionY(scale));
                    __instance.meterScale = scale;
                    wrapper.localScale = new Vector3(scale, scale, scale);
                });
            }
        }

        [HarmonyPatch(typeof(SettingsMenu), nameof(SettingsMenu.UpdateSetting))]
        internal static class MenuPatch
        {
            // Preserve the native menu, enum, deferred confirmation and restart accounting.
            // This prefix never skips the original method or catches its exceptions.
            [HarmonyPrefix]
            internal static void Prefix(PauseSettingButton setting, SettingsMenu.Interaction action)
            {
                Guard(() =>
                {
                    if (!Main.is_mod_enable || Main.settings == null ||
                        !Main.settings.CustomErrorMeterSize || setting.name != "hitErrorMeterSize") return;
                    bool commit = !setting.deferUpdate
                        ? action == SettingsMenu.Interaction.Increment || action == SettingsMenu.Interaction.Decrement
                        : action == SettingsMenu.Interaction.Activate && setting.CachedValue is ErrorMeterSize &&
                            !Equals(setting.initialValue, setting.CachedValue);
                    if (!commit) return;
                    Main.settings.CustomErrorMeterSize = false;
                    // The game's settings page does not call UMM OnSaveGUI.
                    if (Main.Mod != null) Main.settings.Save(Main.Mod);
                });
            }

            [HarmonyPostfix]
            internal static void Postfix(PauseSettingButton setting)
            {
                if (Main.is_mod_enable) Guard(() => UpdateLabel(setting));
            }
        }
    }
}
