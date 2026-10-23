using System;
using AccurateJudgementBar;

public static class Persistence
{
    public static bool enableCompetitiveMode;
    private static HitMarginLimit value;
    public static HitMarginLimit hitMarginLimit
    {
        get => value;
        set
        {
            if (!Enum.IsDefined(value)) throw new Exception("Attempt to persist an unknown native mode");
            Persistence.value = value;
        }
    }
}
namespace UnityEngine
{
    public static class Resources
    {
        public static object[] Objects = Array.Empty<object>();
        public static T[] FindObjectsOfTypeAll<T>() => Array.ConvertAll(Objects, item => (T)item);
    }
    public class GameObject { public bool active; public void SetActive(bool value) => active = value; }
    public class Text { public string text = ""; public GameObject gameObject = new(); }
}
public enum SfxSound { MenuIncrement, MenuDecrement }
public class PauseMenu { public void PlayMenuSfx(SfxSound sound, float volume) {} }
public class PauseSettingButton
{
    public string name = "hitMarginLimit";
    public UnityEngine.Text valueLabel = new(), confirmText = new();
    public object? initialValue, CachedValue;
    public bool restartOnChange = true, deferUpdate;
    public void PlayArrowAnimation(bool right) {}
}
public class SettingsMenu
{
    public enum Interaction { Refresh, Activate, ActivateInfo, Increment, Decrement }
    public int settingsThatRequireRestart;
    public PauseSettingButton[] buttons = Array.Empty<PauseSettingButton>();
    public T[] GetComponentsInChildren<T>(bool includeInactive) => Array.ConvertAll(buttons, item => (T)(object)item);
    public void UpdateSetting(PauseSettingButton button, Interaction action) =>
        MarginLimitMenu.SettingPatch.Prefix(this, button, action);
}
namespace AccurateJudgementBar
{
    public class Localization
    {
        public static Localization Current = new();
        public string Get(string key) => key switch
        {
            "XPerfectOnly" => "XPerfect Only",
            "NoMissOnly" => "No Miss Only",
            "NoCheckpoint" => "No Checkpoint",
            "XPerfectOnlyInactive" => "XPerfect Only (requires Competitive Mode)",
            _ => key
        };
    }
}
