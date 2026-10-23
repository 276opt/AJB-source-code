using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public Vector2 normalized { get { float length = MathF.Sqrt(x * x + y * y); return length > .00001f ? this / length : new Vector2(); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.x / b, a.y / b);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => MathF.Sqrt(Dot(a - b, a - b));
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);
    }
    public struct Vector3
    {
        public float x, y, z;
        public static implicit operator Vector3(Vector2 v) => new Vector3 { x = v.x, y = v.y };
    }
    public struct Rect
    {
        public float xMin, yMin, width, height;
        public float xMax => xMin + width;
        public float yMax => yMin + height;
        public Rect(float x, float y, float w, float h) { xMin = x; yMin = y; width = w; height = h; }
    }
    public partial struct Color
    {
        public static Color Lerp(Color a, Color b, float t)
        { t = Math.Clamp(t, 0, 1); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
    }
    public struct Color32
    {
        private byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }
    public static partial class Mathf
    {
        public const float Deg2Rad = MathF.PI / 180f;
        public static float Sin(float v) => MathF.Sin(v);
        public static float Cos(float v) => MathF.Cos(v);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int CeilToInt(float a) => (int)MathF.Ceiling(a);
        public static float InverseLerp(float a, float b, float v) => a == b ? 0 : Math.Clamp((v - a) / (b - a), 0, 1);
    }
    public struct UIVertex
    {
        public Vector3 position;
        public Color color;
        public static UIVertex simpleVert => new UIVertex();
    }
    public class RectTransform
    {
        public Rect rect;
    }
}
namespace UnityEngine.UI
{
    public class Graphic
    {
        public RectTransform rectTransform = new RectTransform();
        public Color color = new Color(1, 1, 1);
        public int DirtyCount;
        public void SetVerticesDirty() { DirtyCount++; }
    }
    public abstract class BaseMeshEffect
    {
        public Graphic graphic = new Graphic();
        public bool enabled = true;
        protected bool IsActive() => enabled;
        public abstract void ModifyMesh(VertexHelper vh);
    }
    public class VertexHelper
    {
        public readonly List<UIVertex> Vertices = new List<UIVertex>();
        public readonly List<int> Indices = new List<int>();
        public int currentVertCount => Vertices.Count;
        public void AddVert(UIVertex v) => Vertices.Add(v);
        public void PopulateUIVertex(ref UIVertex vertex, int index) { vertex = Vertices[index]; }
        public void SetUIVertex(UIVertex vertex, int index) { Vertices[index] = vertex; }
        public void AddTriangle(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }
    }
}
