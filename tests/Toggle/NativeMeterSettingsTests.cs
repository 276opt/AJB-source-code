using System;
using AccurateJudgementBar;
using ADOFAI;
using UnityEngine;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void NativeMeterSettingsTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.CustomErrorMeterSize = false;
        Mod.settings.BarPositionX = 7;
        Mod.settings.BarPositionY = 11;
        var meter = new scrHitErrorMeter();
        var controller = new scrController { errorMeter = meter };
        scrController.instance = controller;
        var position = meter.gameObject.AddComponent<MeterPosition>();
        Persistence.enableCompetitiveMode = true;

        foreach (var shape in Enum.GetValues<ErrorMeterShape>())
        foreach (var size in Enum.GetValues<ErrorMeterSize>())
        {
            Persistence.hitErrorMeterSize = size;
            Persistence.hitErrorMeterShape = shape;
            var scale = ErrorMeterSizeSettings.NativeScale(size);
            var baseline = meter.wrapperRectTransform.anchoredPosition;
            meter.addedHits = 17;
            int resets = meter.layoutResets;
            foreach (var preset in Enum.GetValues<HitMarginPerfectTickPreset>())
            {
                Persistence.hitMarginPerfectTick = preset;
                Check(meter.meterShape == shape && meter.meterScale == scale,
                    "Preset refresh must retain saved shape and size, including an Off baseline");
                Check(Persistence.hitErrorMeterSize == size && Persistence.hitErrorMeterShape == shape,
                    "Display fix must never rewrite saved layout preferences");
                Check(meter.layoutResets == resets && meter.addedHits == 17,
                    "Changing only a tick preset must not clear ticks by temporarily changing shape");
                Check(meter.displayedPreset == preset, "Original layout must still select the new sprite");
                Check(meter.wrapperRectTransform.anchoredPosition == baseline,
                    "Repeated preset refreshes must not accumulate AJB offsets");
                Check(meter.gameObject.activeSelf == (size != ErrorMeterSize.Off),
                    "Preset updates must preserve the meter's Off visibility");
            }
        }

        // Competitive-mode getters change the effective preset, not the saved preference.
        Persistence.hitErrorMeterSize = ErrorMeterSize.Large;
        Persistence.hitErrorMeterShape = ErrorMeterShape.Curved;
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline;
        controller.paused = true;
        meter.addedHits = 19;
        Persistence.enableCompetitiveMode = false;
        Check(meter.displayedPreset == HitMarginPerfectTickPreset.Default && !meter.gameObject.activeSelf,
            "Competitive mode updates the effective sprite on an inactive editor meter");
        Persistence.enableCompetitiveMode = true;
        Check(meter.displayedPreset == HitMarginPerfectTickPreset.ShowXPerfectWithoutOutline &&
            meter.meterShape == ErrorMeterShape.Curved && meter.meterScale == 1.5f && meter.addedHits == 19,
            "Competitive mode must restore the remembered sprite without resetting layout or ticks");
        controller.paused = false;

        Mod.settings.CustomErrorMeterSize = true;
        Mod.settings.CustomErrorMeterScale = 1.37f;
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        Check(meter.meterScale == 1.37f && meter.meterShape == ErrorMeterShape.Curved &&
            Persistence.hitErrorMeterSize == ErrorMeterSize.Large,
            "Preset refresh must retain AJB custom scale and its native fallback");

        // No scope: explicit layout previews must still receive their own arguments.
        Mod.settings.CustomErrorMeterSize = false;
        meter.UpdateLayout(ErrorMeterSize.Small, ErrorMeterShape.Straight);
        Check(meter.meterScale == .75f && meter.meterShape == ErrorMeterShape.Straight,
            "A direct UpdateLayout must not be replaced with the saved layout");

        var originalError = new InvalidOperationException("original preset layout error");
        meter.layoutException = originalError;
        Exception? caught = null;
        try { Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.Default; }
        catch (Exception ex) { caught = ex; }
        Check(ReferenceEquals(caught, originalError), "Native setter exceptions must propagate unchanged");
        meter.layoutException = null;
        meter.UpdateLayout(ErrorMeterSize.Small, ErrorMeterShape.Straight);
        Check(meter.meterScale == .75f && meter.meterShape == ErrorMeterShape.Straight,
            "A throwing setter must not leak its display scope into later layout calls");

        // Reentrant setters must target their own meter and then restore the outer scope.
        NativeMeterSettings.TickPresetPatch.Prefix(out var outer);
        var other = new scrHitErrorMeter();
        scrController.instance = new scrController { errorMeter = other };
        NativeMeterSettings.TickPresetPatch.Prefix(out var inner);
        try
        {
            meter.UpdateLayout(ErrorMeterSize.Small, ErrorMeterShape.Straight);
            Check(meter.meterScale == .75f, "An inner scope must not override an unrelated meter");
            other.UpdateLayout(ErrorMeterSize.Normal, ErrorMeterShape.Straight);
            Check(other.meterScale == 1.5f && other.meterShape == ErrorMeterShape.Curved,
                "Inner scope must use its own meter and saved preferences");
        }
        finally { NativeMeterSettings.TickPresetPatch.Finalizer(inner); }
        try
        {
            meter.UpdateLayout(ErrorMeterSize.Normal, ErrorMeterShape.Straight);
            Check(meter.meterScale == 1.5f && meter.meterShape == ErrorMeterShape.Curved,
                "Returning from a nested setter must restore the outer scope");
        }
        finally { NativeMeterSettings.TickPresetPatch.Finalizer(outer); }
        scrController.instance = controller;

        entry.OnToggle(entry, false);
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        Check(meter.meterScale == 1 && meter.meterShape == ErrorMeterShape.Straight,
            "Disabled AJB must leave the native setter's Normal/Straight behavior intact");
        int calls = meter.layoutCalls;
        Persistence.hitErrorMeterShape = ErrorMeterShape.Curved;
        Persistence.enableCompetitiveMode = false;
        Check(meter.layoutCalls == calls, "Disabled AJB must not add preference refreshes");
        entry.OnToggle(entry, true);
        Check(meter.meterShape == ErrorMeterShape.Curved && meter.meterScale == 1.5f &&
            meter.displayedPreset == HitMarginPerfectTickPreset.Default,
            "Re-enabling must restore saved layout and current effective preset");

        scrController.instance = new scrController();
        Persistence.hitMarginPerfectTick = HitMarginPerfectTickPreset.ShowXPerfect;
        Persistence.enableCompetitiveMode = true;
        Check(scrController.instance.errorMeter == null, "Settings without a meter must not create one");
        var rebuilt = new scrHitErrorMeter();
        scrController.instance.errorMeter = rebuilt;
        Mod.SettingsUpdate();
        Check(rebuilt.meterShape == ErrorMeterShape.Curved && rebuilt.meterScale == 1.5f &&
            rebuilt.displayedPreset == HitMarginPerfectTickPreset.ShowXPerfect,
            "A rebuilt meter must use the saved layout and preset");

        DisplayErrors.Reset();
        int logs = entry.Logger.Errors.Count;
        for (int i = 0; i < 20; i++)
            DisplayErrors.Run("test renderer", () => throw new InvalidOperationException("display failed"));
        Check(entry.Logger.Errors.Count == logs + 1, "Repeated display failures must log once per operation");
        Check(entry.Logger.Errors[^1].Contains("test renderer") && entry.Logger.Errors[^1].Contains("display failed"),
            "Display diagnostics must identify the failed operation and exception");
        bool ran = false;
        DisplayErrors.Run("test renderer", () => ran = true);
        Check(ran, "Reporting a display failure must not prevent later recovery");
        DisplayErrors.Reset();
        DisplayErrors.Run("test renderer", () => throw originalError);
        Check(entry.Logger.Errors.Count == logs + 2, "A new enable session may report a recurring failure");
        entry.Logger.throwOnError = true;
        DisplayErrors.Run("broken diagnostic sink", () => throw originalError);
        entry.Logger.throwOnError = false;
        Mod.settings.BarPositionX = Mod.settings.BarPositionY = 0;
        entry.OnToggle(entry, false);
    }
}
