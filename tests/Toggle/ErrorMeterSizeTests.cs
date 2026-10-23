using System;
using System.Reflection;
using AccurateJudgementBar;
using ADOFAI;
using UnityEngine;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;
using Size = AccurateJudgementBar.ErrorMeterSizeSettings;
using Interaction = SettingsMenu.Interaction;

static partial class Program
{
    private static void ErrorMeterSizeTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.CustomErrorMeterSize = false;
        Mod.settings.CustomErrorMeterScale = 1f;
        var controller = new scrController { errorMeter = new scrHitErrorMeter() };
        scrController.instance = controller;
        var meter = controller.errorMeter;
        var setting = new PauseSettingButton();
        var menu = new SettingsMenu { buttons = new[] { setting }, settingsThatRequireRestart = 3 };
        Resources.Objects = new object[] { menu };

        foreach (var shape in Enum.GetValues<ErrorMeterShape>())
        foreach (var native in Enum.GetValues<ErrorMeterSize>())
        {
            Persistence.hitErrorMeterShape = shape;
            Size.Select((int)native);
            Check(Persistence.hitErrorMeterSize == native && !Mod.settings.CustomErrorMeterSize,
                "Presets store only a valid native size and clear the custom flag");
            Check(Size.SelectedIndex == (int)native, "Exactly one preset is selected");
            Check(meter.gameObject.activeSelf == (native != ErrorMeterSize.Off), "Off hides the meter immediately");
            float scale = native switch { ErrorMeterSize.Small => .75f, ErrorMeterSize.Large => 1.5f,
                ErrorMeterSize.ExtraLarge => 2f, _ => 1f };
            Check(Size.SliderScale == scale, "Inactive custom slider starts at the current native size");
            Size.SetScale(1.37f);
            var fallback = native == ErrorMeterSize.Off ? ErrorMeterSize.Normal : native;
            Check(Persistence.hitErrorMeterSize == fallback, "Custom preserves the native fallback (Off becomes Normal)");
            Check(Size.SelectedIndex == 5 && Mod.settings.CustomErrorMeterScale == 1.37f, "Moving slider selects custom");
            Check(meter.meterScale == 1.37f && meter.wrapperRectTransform.localScale.x == 1.37f &&
                meter.wrapperRectTransform.localScale.y == 1.37f && meter.meterShape == shape,
                "Custom scale applies to the shared wrapper for both shapes");
            Check(meter.gameObject.activeSelf && setting.valueLabel.text.Contains("via AJB"), "Custom is visible and labelled");
            meter.addedHits = 12;
            Size.SetScale(1.38f);
            Check(meter.addedHits == 12, "Scale-only changes preserve existing tick history");
            menu.UpdateSetting(setting, Interaction.Refresh);
            Check(setting.initialValue is ErrorMeterSize && setting.CachedValue == null, "Official UI never holds an AJB enum");
            entry.OnToggle(entry, false);
            Check(!Size.Active && Mod.settings.CustomErrorMeterSize && Persistence.hitErrorMeterSize == fallback,
                "Disable preserves the AJB preference and native fallback");
            Check(meter.meterScale == (native == ErrorMeterSize.Off ? 1f : scale) &&
                setting.valueLabel.text == Size.NativeLabel(fallback), "Disable restores scale and native label immediately");
            entry.OnToggle(entry, true);
            Check(meter.meterScale == 1.38f && Size.Active, "Re-enable restores custom scale");
        }

        // New/rebuilt meters receive the override through UpdateLayout, even while inactive.
        controller.errorMeter = meter = new scrHitErrorMeter();
        meter.gameObject.SetActive(false);
        meter.UpdateLayout(ErrorMeterSize.Normal, ErrorMeterShape.Straight);
        Check(meter.meterScale == 1.38f && !meter.gameObject.activeSelf, "Rebuilt inactive meter layout does not force visibility");
        controller.paused = true;
        Size.SetScale(1.6f);
        Check(!meter.gameObject.activeSelf, "Custom sizing respects pause visibility");
        controller.paused = false;
        controller.hideEditorUI = true;
        Size.Refresh();
        Check(!meter.gameObject.activeSelf, "Custom sizing respects editor auto-play UI hiding");
        controller.hideEditorUI = false;

        var position = meter.gameObject.AddComponent<MeterPosition>();
        Mod.settings.BarPositionX = 13;
        Mod.settings.BarPositionY = 7;
        foreach (var pair in new[] { (.75f, -30f), (1f, -48f), (1.5f, -71f), (2f, -94f) })
        {
            Size.SetScale(pair.Item1);
            Check(meter.wrapperRectTransform.anchoredPosition == new Vector2(13, pair.Item2 + 7),
                "Custom native-equivalent scales match native positions, plus AJB offsets once");
            Size.Refresh();
            Check(meter.wrapperRectTransform.anchoredPosition == new Vector2(13, pair.Item2 + 7),
                "Repeated layout does not accumulate position offsets");
        }
        Size.Select((int)ErrorMeterSize.Small);
        Size.SetScale(3);
        entry.OnToggle(entry, false);
        typeof(MeterPosition).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(position, null);
        Check(meter.wrapperRectTransform.anchoredPosition == new Vector2(0, -30) && meter.meterScale == .75f,
            "Disable restores native layout without restoring an old custom position");
        Mod.settings.BarPositionX = Mod.settings.BarPositionY = 0;
        entry.OnToggle(entry, true);

        foreach (var direction in new[] { Interaction.Increment, Interaction.Decrement })
        {
            Size.Select((int)ErrorMeterSize.Normal);
            Size.SetScale(1.37f);
            int saves = Mod.settings.saveCalls;
            menu.UpdateSetting(setting, Interaction.Refresh);
            int calls = menu.nativeCalls;
            setting.restartOnChange = true;
            menu.UpdateSetting(setting, direction);
            var expected = direction == Interaction.Increment ? ErrorMeterSize.Large : ErrorMeterSize.Small;
            Check(menu.nativeCalls == calls + 1 && Persistence.hitErrorMeterSize == expected && !Mod.settings.CustomErrorMeterSize,
                "Official arrows run vanilla and exit custom mode");
            Check(Mod.settings.saveCalls == saves + 1, "Official selection immediately saves the cleared AJB flag");
            Check(menu.settingsThatRequireRestart == 4, "Native restart tracking preserves unrelated settings");
            for (int i = 0; i < 4; i++) menu.UpdateSetting(setting, direction);
            Check(Persistence.hitErrorMeterSize == ErrorMeterSize.Normal && menu.settingsThatRequireRestart == 3,
                "Official menu retains the original five-option cycle and restart bookkeeping");
        }

        setting.deferUpdate = true;
        Size.SetScale(1.7f);
        menu.UpdateSetting(setting, Interaction.Refresh);
        menu.UpdateSetting(setting, Interaction.Increment);
        Check(Size.Active && Persistence.hitErrorMeterSize == ErrorMeterSize.Normal &&
            setting.CachedValue is ErrorMeterSize.Large && setting.confirmText.gameObject.activeSelf,
            "Deferred preview neither saves nor disables custom mode");
        Check(setting.valueLabel.text == Size.NativeLabel(ErrorMeterSize.Large), "Preview displays the pending native choice");
        entry.OnToggle(entry, false);
        Check(setting.initialValue is ErrorMeterSize.Normal && setting.CachedValue is ErrorMeterSize.Large &&
            setting.confirmText.gameObject.activeSelf && menu.settingsThatRequireRestart == 3,
            "Disable leaves native pending confirmation and restart state intact");
        entry.OnToggle(entry, true);
        menu.UpdateSetting(setting, Interaction.Refresh);
        Check(Size.Active && setting.valueLabel.text.Contains("via AJB") && !setting.confirmText.gameObject.activeSelf,
            "Cancelling deferred preview restores the custom indicator");
        menu.UpdateSetting(setting, Interaction.Activate);
        Check(Size.Active, "Confirming an unchanged value does not clear custom mode");
        menu.UpdateSetting(setting, Interaction.Increment);
        menu.UpdateSetting(setting, Interaction.Activate);
        Check(!Size.Active && Persistence.hitErrorMeterSize == ErrorMeterSize.Large && meter.meterScale == 1.5f &&
            menu.settingsThatRequireRestart == 4, "Deferred commit clears override before native layout runs");

        Size.SetScale(2.33f);
        var rebuilt = new PauseSettingButton();
        menu.UpdateSetting(rebuilt, Interaction.Refresh);
        Check(rebuilt.valueLabel.text.Contains("via AJB"), "New settings menus show the saved custom status");
        entry.OnToggle(entry, false);
        Persistence.hitErrorMeterSize = ErrorMeterSize.Off;
        entry.OnToggle(entry, true);
        Check(!Size.Active && Size.SelectedIndex == 0 && !meter.gameObject.activeSelf,
            "Choosing Off while AJB is disabled remains Off after re-enabling");
        Size.Select(5);
        Check(Size.Active && Mod.settings.CustomErrorMeterScale == 2.33f, "Re-selecting custom remembers its size");

        foreach (float invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity, -5, 0, 500 })
        {
            Mod.settings.CustomErrorMeterScale = invalid;
            meter.UpdateLayout(ErrorMeterSize.Normal, ErrorMeterShape.Straight);
            Check(float.IsFinite(meter.meterScale) && meter.meterScale >= .25f && meter.meterScale <= 4,
                "Invalid serialized values cannot produce a nonfinite or unbounded UI scale");
        }
        meter.throwOnLayout = true;
        bool threw = false;
        try { meter.UpdateLayout(ErrorMeterSize.Normal, ErrorMeterShape.Straight); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw, "Native layout exceptions outside AJB refresh are not swallowed by a finalizer");
        meter.throwOnLayout = false;
        Size.Select((int)ErrorMeterSize.Normal);
        entry.OnToggle(entry, false);
        Resources.Objects = Array.Empty<object>();
    }
}
