using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLib
{
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    public sealed class HarmonyPostfix : Attribute { }
}

public sealed class DetailedResults
{
    public UnityEngine.UI.Text textComponent = new UnityEngine.UI.Text();
    public scrHUDText hudText;
    public DetailedResults() { hudText = textComponent.gameObject.AddComponent<scrHUDText>(); }
}
public enum HitMargin { PerfectMinus, XPerfect, PerfectPlus, Auto }
public enum HitMarginPerfectTextPreset { Default, ShowXPerfect, ShowXPerfectWithParticle, ShowXPerfectAndSignedPerfects, ShowXPerfectWithParticleAndSignedPerfects, ShowSignedPerfects }
public static class HitMarginHelper
{
    public static bool IsShowXPerfect(this HitMarginPerfectTextPreset preset) => preset != HitMarginPerfectTextPreset.Default && preset != HitMarginPerfectTextPreset.ShowSignedPerfects;
}
public static class Persistence { public static HitMarginPerfectTextPreset hitMarginPerfectText; }
public sealed class scrMarginTracker
{
    public readonly Dictionary<HitMargin, int> Counts = new();
    public bool ThrowOnRead;
    public int GetHits(HitMargin hit) => ThrowOnRead ? throw new Exception("Tracker failure") : Counts.GetValueOrDefault(hit);
}
public sealed class scrPlayer { public scrMarginTracker marginTracker = new(); }
public sealed class scrPlayerManager { public scrPlayer[] players = { new scrPlayer(), new scrPlayer() }; }
public static class ADOBase
{
    public static scrPlayerManager? playerManager = new();
    public static scrController? controller;
}
public enum States { PlayerControl, Won }
public sealed class StateEngine { public States State; public object GetState() => State; }
public sealed class scrController
{
    public StateEngine stateMachine = new();
    public UnityEngine.UI.Text? txtCongrats, txtAllStrictClear, txtAprilCongrats;
    public DetailedResults? detailedResults;
}
public class scrHUDText : UnityEngine.MonoBehaviour { public bool changeColor = true; }
public class ColourScheme
{
    public UnityEngine.Color colourText = UnityEngine.Color.white;
    public UnityEngine.Color colourTextShadow = new(0, 0, 0, .5f);
}
public class scrVfx
{
    public static scrVfx? instance;
    public ColourScheme currentColourScheme = new();
}
public static class RDConstants
{
    public static Data data = new();
    public class Data
    {
        public ColourScheme[] worldColourScheme = { new() };
        public HitColors hitMarginColoursUI = new();
    }
    public class HitColors { public UnityEngine.Color colourPerfect = new(0, 1, 0); }
}
public static class ColorExtensions { public static string ToHex(this UnityEngine.Color c, bool a, bool hash) => "#00FF00"; }
public static class RDString
{
    public static string Label = "XPerfect";
    public static string Minus = "Perfect-", Plus = "Perfect+", Perfect = "Perfect";
    public static string Get(string key, object? args) => key switch
    {
        "status.results.xPerfect" => Label,
        "status.results.perfectM" => Minus,
        "status.results.perfectP" => Plus,
        "status.results.perfect" => Perfect,
        _ => key
    };
}
namespace AccurateJudgementBar
{
    public static class Main
    {
        public static bool is_mod_enable = true;
        public static Settings? settings = new Settings();
        public static ModEntry? Mod = new();
    }
    public class ModEntry { public Logger Logger = new(); }
    public class Logger { public readonly List<string> Errors = new(); public void Error(string s) => Errors.Add(s); }
    public static class NativeXPerfect { public static bool Enabled; }
    public sealed class Settings
    {
        public bool BlueXPerfectResults = true;
        public bool EnchantedDetailedResults = true;
        public bool DetailedResultsSignedPerfects = true;
        public bool DetailedResultsBackground = true;
        public bool DetailedResultsDefaultColors = true;
    }
}

namespace UnityEngine
{
    public class GameObject
    {
        public readonly List<Component> Components = new List<Component>();
        public RectTransform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public static bool ThrowOnImage;
        public static int ImageAttempts;
        public GameObject(string name = "", params Type[] types)
        {
            transform = new RectTransform { gameObject = this };
            Components.Add(transform);
        }
        public void SetActive(bool value) => activeSelf = value;
        public T? GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault();
        public T AddComponent<T>() where T : Component, new()
        {
            if (typeof(T) == typeof(UnityEngine.UI.Image))
            {
                ImageAttempts++;
                if (ThrowOnImage) throw new Exception("Image failure");
            }
            var value = new T { gameObject = this };
            Components.Add(value);
            return value;
        }
    }
    public class Component
    {
        public GameObject gameObject = null!;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponents<T>() where T : Component => gameObject.Components.OfType<T>().ToArray();
    }
    public class MonoBehaviour : Component
    {
        private bool active = true;
        public bool enabled
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                GetType().GetMethod(value ? "OnEnable" : "OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(this, null);
            }
        }
    }
    public static class Object
    {
        public static void Destroy(GameObject obj) { obj.SetActive(false); }
    }
    public class CanvasRenderer : Component { public float Alpha = 1; public float GetAlpha() => Alpha; }
    public class CanvasGroup : Component { public float alpha = 1; }
    public class Transform : Component { internal readonly List<RectTransform> Children = new(); }
    public class RectTransform : Transform
    {
        private static readonly List<RectTransform> Roots = new();
        public Transform? parent { get; private set; }
        public RectTransform() { Roots.Add(this); }
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        public Vector3 localScale = new(1, 1, 1), localPosition;
        public Quaternion localRotation;
        public Rect rect => new() { size = sizeDelta };
        private List<RectTransform> Siblings => parent == null ? Roots : parent.Children;
        public void SetParent(Transform? p, bool world)
        {
            if (parent == p) return;
            Siblings.Remove(this);
            parent = p;
            Siblings.Add(this);
        }
        public int GetSiblingIndex() => Siblings.IndexOf(this);
        public void SetSiblingIndex(int i)
        {
            var siblings = Siblings;
            siblings.Remove(this);
            siblings.Insert(Math.Clamp(i, 0, siblings.Count), this);
        }
    }
    public struct Rect { public Vector2 size; }
    public struct Quaternion { public static Vector3 operator *(Quaternion q, Vector3 v) => v; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 Min(Vector2 a, Vector2 b) => new(Math.Min(a.x,b.x), Math.Min(a.y,b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new(Math.Max(a.x,b.x), Math.Max(a.y,b.y));
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y);
        public static Vector2 operator /(Vector2 a, float b) => new(a.x/b,a.y/b);
        public static implicit operator Vector3(Vector2 a) => new(a.x,a.y,0);
    }
    public class Font
    {
        public static Action<Font>? textureRebuilt;
        public static void Rebuild(Font font) => textureRebuilt?.Invoke(font);
    }
    public sealed class TextGenerationSettings { public UIVertex[] Glyphs = Array.Empty<UIVertex>(); }
    public class TextGenerator : IDisposable
    {
        public readonly List<UIVertex> verts = new();
        public int PopulateCalls, InvalidateCalls, DisposeCalls;
        public bool RejectPopulate;
        public bool ThrowOnInvalidate;
        public Action? OnPopulate;
        public bool Populate(string text, TextGenerationSettings settings)
        {
            PopulateCalls++;
            OnPopulate?.Invoke();
            if (RejectPopulate) return false;
            verts.Clear();
            verts.AddRange(settings.Glyphs);
            return true;
        }
        public void Invalidate()
        {
            InvalidateCalls++;
            if (ThrowOnInvalidate) throw new Exception("Layout invalidate failure");
        }
        public void Dispose() { DisposeCalls++; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x/b, a.y/b, a.z/b);
        public static implicit operator Vector2(Vector3 a) => new(a.x, a.y);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x*b.x,a.y*b.y,a.z*b.z);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => new Vector3(Mathf.Lerp(a.x,b.x,t), Mathf.Lerp(a.y,b.y,t), Mathf.Lerp(a.z,b.z,t));
    }
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => new Vector4(Mathf.Lerp(a.x,b.x,t), Mathf.Lerp(a.y,b.y,t), Mathf.Lerp(a.z,b.z,t), Mathf.Lerp(a.w,b.w,t));
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static bool operator ==(Color a, Color b) => a.Equals(b);
        public static bool operator !=(Color a, Color b) => !a.Equals(b);
        public override bool Equals(object? o) => o is Color c && r == c.r && g == c.g && b == c.b && a == c.a;
        public override int GetHashCode() => HashCode.Combine(r,g,b,a);
        public static Color Lerp(Color a, Color b, float t) => new Color(Mathf.Lerp(a.r,b.r,t), Mathf.Lerp(a.g,b.g,t), Mathf.Lerp(a.b,b.b,t), Mathf.Lerp(a.a,b.a,t));
    }
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r/255f, c.g/255f, c.b/255f, c.a/255f);
        public static implicit operator Color32(Color c) => new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(c.a));
        private static byte ToByte(float value) => (byte)MathF.Round(Math.Clamp(value, 0, 1) * 255f);
    }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Sin(float v) => MathF.Sin(v);
        public static float Cos(float v) => MathF.Cos(v);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Lerp(float a, float b, float t) => a + (b-a) * Math.Clamp(t, 0, 1);
    }
    public struct UIVertex
    {
        public Vector3 position;
        public Vector4 uv0, uv1, uv2, uv3;
        public Color32 color;
    }
}

namespace UnityEngine.UI
{
    public class Graphic : MonoBehaviour
    {
        public Color color = Color.white;
        public bool raycastTarget = true;
        public RectTransform rectTransform => gameObject.transform;
        public CanvasRenderer canvasRenderer = new();
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public class Image : Graphic { }
    public class Shadow : MonoBehaviour { public Color effectColor; }
    public class Text : Graphic
    {
        public string text = "";
        public bool supportRichText = true;
        public TextGenerator cachedTextGenerator = new();
        public float pixelsPerUnit = 1;
        public Font font = new();
        public int VerticesDirtyCalls;
        public void SetVerticesDirty() => VerticesDirtyCalls++;
        public TextGenerationSettings GetGenerationSettings(Vector2 size) => new() { Glyphs = cachedTextGenerator.verts.ToArray() };
        public Text() { gameObject = new GameObject(); gameObject.Components.Add(this); }
    }
    public abstract class BaseMeshEffect : Component
    {
        public bool enabled = true;
        protected Graphic? graphic => GetComponent<Graphic>();
        protected bool IsActive() => enabled;
        public abstract void ModifyMesh(VertexHelper vh);
    }
    public sealed class VertexHelper
    {
        public readonly List<UIVertex> Stream = new List<UIVertex>();
        public void GetUIVertexStream(List<UIVertex> destination) => destination.AddRange(Stream);
        public void Clear() => Stream.Clear();
        public void AddUIVertexTriangleStream(List<UIVertex> stream) => Stream.AddRange(stream);
    }
}
