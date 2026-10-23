using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // Shared white fill for the straight Image and curved band mesh.
    // Keep the component name for compatibility with the earlier gradient version.
    public sealed class XPerfectGradient : BaseMeshEffect
    {
        private int outlineWidth = -1;
        private bool vanillaStyle;

        private void LateUpdate()
        {
            var settings = Main.settings;
            int width = XPerfectOutlineMesh.EffectiveWidth(settings.VanillaXPerfectStyle,
                settings.VanillaXPerfectOutline, settings.XPerfectOutlineWidth);
            if (outlineWidth == width && vanillaStyle == settings.VanillaXPerfectStyle) return;
            outlineWidth = width;
            vanillaStyle = settings.VanillaXPerfectStyle;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        internal static Color Sample(float y, float bottom, float top, float alpha)
        {
            return new Color(1f, 1f, 1f, alpha);
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Rect rect = graphic.rectTransform.rect;
            var vertex = UIVertex.simpleVert;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.color = Sample(vertex.position.y, rect.yMin, rect.yMax, vertex.color.a / 255f);
                vh.SetUIVertex(vertex, i);
            }
            var settings = Main.settings;
            XPerfectOutlineMesh.AddRectangle(vh, rect, graphic.color.a,
                XPerfectOutlineMesh.EffectiveWidth(settings.VanillaXPerfectStyle,
                    settings.VanillaXPerfectOutline, settings.XPerfectOutlineWidth), settings.VanillaXPerfectStyle);
        }
    }
}
