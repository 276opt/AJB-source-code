using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    /// <summary>Texture-free layer1, expressed in the original 400 x 200 design coordinates.</summary>
    public sealed class MeterBackgroundGraphic : MaskableGraphic
    {
        private bool curved;
        private float extensionWidth;
        public static MeterBackgroundGraphic Create(Transform parent, bool curved)
        {
            var obj = new GameObject(curved ? "CurvedLayer1" : "StraightLayer1",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(MeterBackgroundGraphic));
            obj.transform.SetParent(parent, false);
            var graphic = obj.GetComponent<MeterBackgroundGraphic>();
            graphic.curved = curved;
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            graphic.RefreshOpacity();
            return graphic;
        }

        public void RefreshOpacity()
        {
            float opacity = Main.settings.BackgroundOpacity;
            if (float.IsNaN(opacity) || float.IsInfinity(opacity)) opacity = 0.6f;
            color = new Color(0f, 0f, 0f, Mathf.Clamp01(opacity));
        }

        public void RefreshBackground(float failureProgress)
        {
            RefreshOpacity();
            float nextWidth = FailureJudgement.ExtensionWidth * Mathf.Clamp01(failureProgress);
            if (extensionWidth == nextWidth) return;
            extensionWidth = nextWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!curved)
            {
                // Original centered 330 x 66 rectangle on a 400 x 200 canvas.
                float left = 35f - extensionWidth / 2f;
                float right = 365f + extensionWidth / 2f;
                Quad(vh, new Vector2(left, 67f), new Vector2(left, 133f),
                    new Vector2(right, 133f), new Vector2(right, 67f));
                return;
            }
            // Reconstruct the silhouette as a continuous annular sector, without image cutout holes.
            const float inner = 117f, outer = 183f;
            // Same full-width to half-angle conversion used by the judgement bands.
            float halfAngle = 66f + extensionWidth / 5f;
            int steps = Mathf.CeilToInt(halfAngle * 4f); // At most half-degree steps.
            for (int i = 0; i < steps; i++)
            {
                float a = -halfAngle + 2f * halfAngle * i / steps;
                float b = -halfAngle + 2f * halfAngle * (i + 1) / steps;
                Quad(vh, Arc(inner, a), Arc(outer, a), Arc(outer, b), Arc(inner, b));
            }
        }

        private static Vector2 Arc(float radius, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            return new Vector2(200f + Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius);
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int start = vh.currentVertCount;
            Vertex(vh, a); Vertex(vh, b); Vertex(vh, c); Vertex(vh, d);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void Vertex(VertexHelper vh, Vector2 position)
        {
            Rect rect = rectTransform.rect;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin + position.x * rect.width / 400f,
                rect.yMin + position.y * rect.height / 200f, 0f);
            vh.AddVert(vertex);
        }
    }
}


