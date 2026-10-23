using System;
using System.Linq;
using AccurateJudgementBar;
using ADOFAI;

namespace UnityEngine
{
    public enum SystemLanguage { English, ChineseSimplified }
    public static class Resources
    {
        public static object[] Objects = Array.Empty<object>();
        public static T[] FindObjectsOfTypeAll<T>() => Objects.OfType<T>().ToArray();
    }
    public class Text { public string text = ""; public GameObject gameObject = new(); }
}
namespace SA.GoogleDoc
{
    public static class Localization
    {
        public static string GetLocalizedString(string key, UnityEngine.SystemLanguage language) => key;
    }
}
public static class RDString { public static string Get(string key, object? args) => key; }
public class PauseSettingButton
{
    public string name = "hitErrorMeterSize";
    public UnityEngine.Text valueLabel = new(), confirmText = new();
    public object? initialValue, CachedValue;
    public bool restartOnChange, deferUpdate;
}
public class SettingsMenu
{
    public enum Interaction { Refresh, Activate, ActivateInfo, Increment, Decrement }
    public int settingsThatRequireRestart, nativeCalls;
    public PauseSettingButton[] buttons = Array.Empty<PauseSettingButton>();
    public T[] GetComponentsInChildren<T>(bool inactive) => buttons.Cast<T>().ToArray();

    // Native ErrorMeterSize branch and enum/deferred/restart logic, with production hooks.
    public void UpdateSetting(PauseSettingButton setting, Interaction action)
    {
        ErrorMeterSizeSettings.MenuPatch.Prefix(setting, action);
        nativeCalls++;
        if (action == Interaction.Refresh) setting.CachedValue = null;
        var current = setting.deferUpdate && setting.CachedValue != null
            ? (ErrorMeterSize)setting.CachedValue : Persistence.hitErrorMeterSize;
        bool wasChanged = !Equals(setting.initialValue, current);
        if (action is Interaction.Increment or Interaction.Decrement)
            current = (ErrorMeterSize)(((int)current + (action == Interaction.Increment ? 1 : 4)) % 5);
        if (action == Interaction.Refresh) setting.initialValue = current;
        setting.valueLabel.text = ErrorMeterSizeSettings.NativeLabel(current);
        if (setting.deferUpdate)
        {
            setting.CachedValue = current;
            setting.confirmText.gameObject.SetActive(!Equals(setting.initialValue, current));
        }
        if (action is Interaction.Increment or Interaction.Decrement && !setting.deferUpdate)
        {
            Apply(current);
            if (setting.restartOnChange)
                settingsThatRequireRestart += (!Equals(setting.initialValue, current) ? 1 : 0) - (wasChanged ? 1 : 0);
        }
        if (action == Interaction.Activate && setting.deferUpdate && !Equals(setting.initialValue, current))
        {
            Apply(current);
            setting.initialValue = current;
            setting.confirmText.gameObject.SetActive(false);
            if (setting.restartOnChange) settingsThatRequireRestart++;
        }
        ErrorMeterSizeSettings.MenuPatch.Postfix(setting);
    }
    private static void Apply(ErrorMeterSize size)
    {
        scrController.instance?.errorMeter?.UpdateLayout(size, Persistence.hitErrorMeterShape);
        Persistence.hitErrorMeterSize = size;
    }
}
