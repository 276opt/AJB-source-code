// Small game doubles for exercising the production toggle callbacks without a Unity player.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public static readonly List<Object> Objects = new();
        public Object() => Objects.Add(this);
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) => Objects.OfType<T>().ToArray();
        public static readonly List<Object> DestroyRequested = new();
        public static void Destroy(Object? value) { if (value != null) DestroyRequested.Add(value); } // Destruction is deferred.
        public static void DontDestroyOnLoad(Object value) {}
    }
    public enum FindObjectsInactive { Include }
    public enum FindObjectsSortMode { None }
    public class GameObject : Object
    {
        public bool activeSelf = true;
        private readonly Dictionary<Type, MonoBehaviour> components = new();
        public Exception? getComponentError, addComponentError;
        public Transform transform = new RectTransform();
        public GameObject(string name = "") {}
        public void SetActive(bool value) => activeSelf = value;
        public T AddComponent<T>() where T : MonoBehaviour, new()
        { if (addComponentError != null) throw addComponentError; var c = new T { gameObject = this }; components[typeof(T)] = c; return c; }
        public T? GetComponent<T>() where T : class
        { if (getComponentError != null) throw getComponentError; return components.GetValueOrDefault(typeof(T)) as T; }
    }
    public class MonoBehaviour : Object
    {
        public bool enabled = true;
        public GameObject gameObject = new();
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 one => new(1, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static bool operator ==(Vector2 a, Vector2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object? o) => o is Vector2 v && this == v;
        public override int GetHashCode() => HashCode.Combine(x, y);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new(1, 1, 1);
    }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) => default; public static Quaternion identity => default; }
    public class Transform
    {
        public Transform? parent;
        public Action? onSetParent;
        private int sibling;
        public Vector3 localScale;
        public Quaternion localRotation;
        public void SetParent(Transform? value, bool worldPositionStays) { onSetParent?.Invoke(); parent = value; }
        public int GetSiblingIndex() => sibling;
        public void SetSiblingIndex(int value) => sibling = value;
    }
    public class RectTransform : Transform
    {
        public enum Axis { Horizontal, Vertical }
        public readonly Dictionary<Type, object> layoutComponents = new();
        public T? GetComponent<T>() where T : class => layoutComponents.GetValueOrDefault(typeof(T)) as T;
        public Vector2 anchoredPosition, anchorMin, anchorMax, pivot, sizeDelta;
        public Vector3 anchoredPosition3D;
        public Rect rect => new(0, 0,
            sizeDelta.x + ((parent as RectTransform)?.rect.width ?? 0) * (anchorMax.x - anchorMin.x),
            sizeDelta.y + ((parent as RectTransform)?.rect.height ?? 0) * (anchorMax.y - anchorMin.y));
        public void SetSizeWithCurrentAnchors(Axis axis, float size)
        {
            if (axis == Axis.Horizontal)
                sizeDelta.x = size - ((parent as RectTransform)?.rect.width ?? 0) * (anchorMax.x - anchorMin.x);
            else
                sizeDelta.y = size - ((parent as RectTransform)?.rect.height ?? 0) * (anchorMax.y - anchorMin.y);
        }
    }
    public struct Rect
    {
        public float width, height;
        public Rect(float x, float y, float width, float height) { this.width = width; this.height = height; }
    }
    public class Material : Object {}
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public class Texture2D : Object
    {
        public static bool decodeSucceeds = true;
        public static int decodes;
        public string name = "";
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D(int w, int h, TextureFormat format, bool mip) {}
        public bool LoadImage(byte[] bytes) { decodes++; return decodeSucceeds; }
    }
    public class Sprite : Object
    {
        public static Exception? createError;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        { if (createError != null) throw createError; return new(); }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color clear => default;
    }
    public struct Color32
    {
        private Color color;
        public Color32(byte r, byte g, byte b, byte a) => color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        public static implicit operator Color(Color32 c) => c.color;
    }
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
    public static class Time
    {
        private static double now;
        public static Exception? readError;
        public static double unscaledTimeAsDouble { get { if (readError != null) throw readError; return now; } set => now = value; }
    }
    public static class Application { public static string version = "3.4.0"; }
}
namespace UnityEngine.UI
{
    public class Image
    {
        public enum Type { Simple, Sliced }
        public Exception? colorError, rectError;
        public Action? onSetSprite;
        private UnityEngine.Color tint = new(1, 1, 1, 1);
        public UnityEngine.Color color { get => tint; set { if (colorError != null) throw colorError; tint = value; } }
        private readonly UnityEngine.RectTransform rect = new();
        public UnityEngine.RectTransform rectTransform { get { if (rectError != null) throw rectError; return rect; } }
        private UnityEngine.Sprite? currentSprite;
        public UnityEngine.Sprite? sprite { get => currentSprite; set { onSetSprite?.Invoke(); currentSprite = value; } }
        public UnityEngine.Sprite? overrideSprite;
        public UnityEngine.Material? material;
        public Type type;
        public bool preserveAspect, useSpriteMesh;
    }
}
namespace ADOFAI { public enum ErrorMeterShape { Straight, Curved } public enum ErrorMeterSize { Off, Small, Normal, Large, ExtraLarge } }
namespace DG.Tweening
{
    public static class DOTween
    {
        public static readonly List<string> Killed = new();
        public static Action<string>? onKill;
        public static void Kill(string id, bool complete) { onKill?.Invoke(id); Killed.Add(id); }
        public static void Kill(object? target, bool complete) { }
    }
}
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch() {} public HarmonyPatch(Type type, string method) {} }
    public class HarmonyPostfix : Attribute {}
    public class HarmonyPrefix : Attribute {}
    public class HarmonyFinalizer : Attribute {}
    public class HarmonyPriority : Attribute { public HarmonyPriority(int value) {} }
    public static class Priority { public const int Last = 0; }
    public static class AccessTools { public static MethodInfo Method(Type type, string name) => type.GetMethod(name)!; }
    public class Harmony
    {
        public Harmony(string id) {}
        public void PatchAll() {}
        public void UnpatchAll(string id) {}
        public static Patches? GetPatchInfo(MethodInfo method) => null;
    }
    public class Patches { public List<Patch> Postfixes = new(); }
    public class Patch { public string owner = ""; public MethodInfo PatchMethod = null!; }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    {
        public class ModEntry
        {
            public bool Active;
            public Logger Logger = new(); public Info Info = new(); public string Path = "";
            public Func<ModEntry, bool, bool> OnToggle = null!;
            public Action<ModEntry, float> OnUpdate = null!;
            public Action<ModEntry> OnGUI = null!, OnSaveGUI = null!;
        }
        public class Logger
        {
            public readonly List<string> Errors = new();
            public bool throwOnError;
            public void Error(string message) { if (throwOnError) throw new Exception("logger failure"); Errors.Add(message); }
            public void Log(string message) {}
        }
        public class Info { public string Id = "AccurateJudgementBar", DisplayName = "AccurateJudgementBar"; }
        public static ModEntry? TogetherEntry;
        public static ModEntry? FindMod(string name) => name == "Together" ? TogetherEntry : null;
    }
}
public class scrMisc {}
public class RDConstants {}
public enum HitMarginPerfectTickPreset { Default, ShowXPerfect, ShowXPerfectWithoutOutline }
public enum HitMarginLimit { None, PerfectsOnly, PurePerfectOnly }
public enum HitMargin { FailMiss, FailOverload, Perfect, TooEarly, VeryEarly, EarlyPerfect, PerfectMinus, XPerfect, PerfectPlus, LatePerfect, VeryLate, TooLate }
public enum Difficulty { Normal }
public enum DifficultyUIMode { DontShow, ShowLenientAndNormal, ShowAll }
public static class GCS { public static HitMarginLimit hitMarginLimit; }
public static class Persistence
{
    public static HitMarginLimit hitMarginLimit;
    private static ADOFAI.ErrorMeterSize size = ADOFAI.ErrorMeterSize.Normal;
    public static ADOFAI.ErrorMeterSize hitErrorMeterSize
    {
        get => size;
        set
        {
            if (!Enum.IsDefined(value)) throw new Exception("Invalid native ErrorMeterSize persisted");
            size = value;
            AccurateJudgementBar.NativeMeterSettings.PreferencePatch.Postfix();
        }
    }
    private static ADOFAI.ErrorMeterShape shape;
    public static ADOFAI.ErrorMeterShape hitErrorMeterShape
    {
        get => shape;
        set { shape = value; AccurateJudgementBar.NativeMeterSettings.PreferencePatch.Postfix(); }
    }
    private static bool competitive;
    public static bool enableCompetitiveMode
    {
        get => competitive;
        set { competitive = value; AccurateJudgementBar.NativeMeterSettings.PreferencePatch.Postfix(); }
    }
    private static HitMarginPerfectTickPreset preset;
    public static HitMarginPerfectTickPreset hitMarginPerfectTick
    {
        get => competitive ? preset : HitMarginPerfectTickPreset.Default;
        set
        {
            AccurateJudgementBar.NativeMeterSettings.TickPresetPatch.Prefix(out var state);
            try
            {
                preset = value;
                // Replay the predrop setter's original, incorrect arguments.
                scrController.instance?.errorMeter?.UpdateLayout(ADOFAI.ErrorMeterSize.Normal, ADOFAI.ErrorMeterShape.Straight);
            }
            finally { AccurateJudgementBar.NativeMeterSettings.TickPresetPatch.Finalizer(state); }
        }
    }
}
public class scrHitErrorMeter : UnityEngine.MonoBehaviour
{
    public float sensitivity = .2f, tickLife = 3f;
    public int addedHits;
    public float meterScale = 1f;
    public ADOFAI.ErrorMeterShape meterShape;
    public UnityEngine.RectTransform wrapperRectTransform = new();
    public bool throwOnLayout;
    public int layoutCalls, layoutResets;
    public Exception? layoutException;
    public HitMarginPerfectTickPreset displayedPreset;
    // Replay predrop layout and production AJB hooks in priority order.
    public void UpdateLayout(ADOFAI.ErrorMeterSize size, ADOFAI.ErrorMeterShape shape)
    {
        AccurateJudgementBar.NativeMeterSettings.LayoutPatch.Prefix(this, ref size, ref shape);
        layoutCalls++;
        if (layoutException != null) throw layoutException;
        if (throwOnLayout) throw new InvalidOperationException("native layout failure");
        (float scale, float y) = size switch
        {
            ADOFAI.ErrorMeterSize.Small => (.75f, -30f),
            ADOFAI.ErrorMeterSize.Large => (1.5f, -71f),
            ADOFAI.ErrorMeterSize.ExtraLarge => (2f, -94f),
            _ => (1f, -48f)
        };
        if (size != ADOFAI.ErrorMeterSize.Off)
        {
            meterScale = scale;
            wrapperRectTransform.anchoredPosition = new(0, y);
        }
        if (meterShape != shape) { meterShape = shape; addedHits = 0; layoutResets++; }
        displayedPreset = Persistence.hitMarginPerfectTick;
        wrapperRectTransform.localScale = new(meterScale, meterScale, meterScale);
        AccurateJudgementBar.ErrorMeterSizeSettings.LayoutPatch.Postfix(this, size);
        typeof(AccurateJudgementBar.MeterPositionLayoutPatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { this });
    }
    public Action<float, HitMargin, float, scrPlanet, scrFloor>? onAddHit;
    public void AddHit(float angle, HitMargin margin, float scale, scrPlanet planet, scrFloor floor)
    { addedHits++; onAddHit?.Invoke(angle, margin, scale, planet, floor); }
}
public class ErrorMeterTick : UnityEngine.MonoBehaviour
{
    public UnityEngine.UI.Image tick = new(), tickOutline = new();
    public Exception? rectError;
    public UnityEngine.RectTransform rectTransform
    { get { if (rectError != null) throw rectError; return (UnityEngine.RectTransform)transform; } }
}
public class scrController : UnityEngine.Object
{
    public static scrController? instance;
    public bool noFail, noFailInfiniteMargin, gameworld = true;
    public bool paused, hideEditorUI;
    public scrHitErrorMeter? errorMeter;
    public void UpdateErrorMeterVisibility()
    {
        if (hideEditorUI) { errorMeter!.gameObject.SetActive(false); return; }
        if (errorMeter != null && gameworld && Persistence.hitErrorMeterSize != ADOFAI.ErrorMeterSize.Off)
            errorMeter.gameObject.SetActive(!paused);
    }
}
public class scrMarginTracker { public List<HitMargin> hitMargins = new(); public void Reset() => hitMargins.Clear(); }
public static class ADOBase
{
    public static bool isExpo, isOfficialLevel;
    public static scrPlayerManager? playerManager;
}
public class scrPlayerManager { public scrPlayer[]? players; }
public static class RDC { public static bool debug, useOldAuto; }
public class scrLivesCounter { public static scrLivesCounter? instance; public int lives; }
public static class scrMistakesManager
{
    public static scrMarginTracker[] marginTrackers = { new() };
    public static void SetPlayerCount(int count)
    {
        // The editor replaces these managed objects without replacing scrController.
        marginTrackers = Enumerable.Range(0, count).Select(_ => new scrMarginTracker()).ToArray();
    }
}
public class scrPlanet { public double angle, targetExitAngle; public scrFloor? currfloor = new(); }
public class scrFloor { public double marginScale = 1; public float speed = 1; public scrFloor? nextfloor; public bool isCCW, freeroam, isSafe, auto; }
public class PlanetarySystem { public scrPlanet chosenPlanet = new(); }
public class scrPlayer
{
    public bool missCooldownActive, auto;
    public int keyLimiterOverCounter;
    public void Die(bool overload, bool multipress, string message, bool hitbox) { alive = false; }
    public bool alive = true;
    public scrMarginTracker marginTracker = new();
    public Exception? floorReadError;
    private scrFloor floor = new();
    public scrFloor currFloor { get { if (floorReadError != null) throw floorReadError; return floor; } set => floor = value; }
    public PlanetarySystem planetarySystem = new();
}
public class scrConductor { public static scrConductor? instance; public object song = new(); }
namespace AccurateJudgementBar
{
    public class Settings
    {
        public bool EnableOverloadHP, ShowFailureJudgement = true, UseLegacyHit;
        public bool HideOverloadHPWithMarginLimit = true;
        public bool ShowAllDifficulties = true, ShowAllDifficultiesExcludeOfficial = true;
        public int FailureBandDisplayMode;
        public bool FixTickMarginScale = true;
        public bool WarnOnStartingBpmChange = true;
        public bool ShowHitMarginLimit = true, XPerfectOnly, NoMissOnly, NoCheckpoint;
        public bool CustomErrorMeterSize;
        public float CustomErrorMeterScale = 1f, BarPositionX, BarPositionY;
        public int saveCalls;
        public void Save(UnityModManagerNet.UnityModManager.ModEntry entry) => saveCalls++;
        public float overloadHPX, overloadHPY, SensitivityOfHit = .6f, LifeOfHit = 9f, AnimationDuration;
        public string Language = "en";
        public static Settings Load(UnityModManagerNet.UnityModManager.ModEntry entry) => new();
        public void OnGUI(UnityModManagerNet.UnityModManager.ModEntry entry) {}
        public void OnSaveGUI(UnityModManagerNet.UnityModManager.ModEntry entry) {}
    }
    public static class GameCompatibility
    { public static bool IsSupported(string v, Type t, Type meter, Type constants, out string error) { error = ""; return true; } }
    public class Localization
    {
        public static Localization Current = new();
        public string SelectedLanguage = "en";
        public string Get(string key) => key == "ErrorMeterSizeCustomViaAJB" ? "Custom\n(via AJB Settings)" : key;
        public static void Initialize(string path, string language, Action<string> log) {}
    }
    public class StraightMeterWatcher : UnityEngine.MonoBehaviour {}
    public class CurvedMeterWatcher : UnityEngine.MonoBehaviour {}
    public class OverloadBar : UnityEngine.MonoBehaviour
    {
        public static int Created, Destroyed;
        public static OverloadBar Create(UnityEngine.Vector2 pos, UnityEngine.Vector2 size) { Created++; return new(); }
        public void DestroyBar() { Destroyed++; gameObject.SetActive(false); }
        public void SetPosition(UnityEngine.Vector2 pos) {}
        public void Refresh() {}
    }
    public static class XPerfectResultsColor { public static void Refresh() {} }
    public static class CompletionResultsText { public static void Update() {} }
    public static class MarginLimitMenu
    {
        public static int togetherResets;
        public static void Refresh() {}
        public static void ResetForTogether() => togetherResets++;
    }
    public static class NoMissOnly
    {
        public static bool Active => Main.is_mod_enable && Main.settings.NoMissOnly && GCS.hitMarginLimit == HitMarginLimit.None;
    }
    public static class MeterWatcher
    {
        public static Exception? blueError;
        private static bool Limited => GCS.hitMarginLimit != HitMarginLimit.None && Main.settings.ShowHitMarginLimit;
        public static UnityEngine.Color GetRed() => Limited || (NoMissOnly.Active && Main.settings.ShowHitMarginLimit)
            ? new(.6f, .2f, .8f, 1) : new(1, 0, 0, 1);
        public static UnityEngine.Color GetOrange() => Limited ? FailureJudgement.Purple : new(1, .4f, .3f, 1);
        public static UnityEngine.Color GetYellow() => Limited && GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly
            ? FailureJudgement.Purple : new(1, 1, .3f, 1);
        public static UnityEngine.Color GetGreen() => Limited && Main.settings.XPerfectOnly
            ? FailureJudgement.Purple : new(.3f, 1, .3f, 1);
        public static UnityEngine.Color GetBlue() { if (blueError != null) throw blueError; return new(.7f, .7f, 1, 1); }
    }
    public class JudgementBand
    {
        public string Id;
        public float WindowMs;
        public UnityEngine.Color Color;
        public float Width;
        public JudgementBand(string id, float ms, float width, UnityEngine.Color color)
        { Id = id; WindowMs = ms; Width = width; Color = color; }
    }
    public static class JudgementApi { public const string Red = "red"; }
}
