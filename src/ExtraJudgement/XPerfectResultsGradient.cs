using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // DetailedResults uses legacy Text, not TMP. Offset copies of its own glyph
    // mesh retain the vanilla font/atlas/material, including localized fonts.
    public sealed class XPerfectResultsGradient : BaseMeshEffect
    {
        private readonly List<UIVertex> source = new List<UIVertex>();
        private readonly List<UIVertex> output = new List<UIVertex>();
        // Same top-to-bottom pastel palette as Quartz's XPerfectBorder:
        // https://github.com/PrismMods/Quartz/blob/main/modules/Judgement/XPerfectBorder.cs
        private static readonly Color[] Stops = {
            new Color32(247, 79, 79, 255), new Color32(255, 137, 199, 255),
            new Color32(185, 148, 255, 255), new Color32(139, 173, 255, 255),
            new Color32(0, 132, 255, 255)
        };

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || !(graphic is Text text) || !XPerfectResultsColor.AppliesTo(text)) return;
            source.Clear();
            vh.GetUIVertexStream(source);
            if (source.Count == 0 || source.Count % 6 != 0) return;
            output.Clear();
            for (int i = 0; i < source.Count; i += 6)
            {
                bool marked = true;
                for (int j = 0; j < 6; j++) marked &= IsMarker(source[i + j].color);
                if (marked)
                {
                    // Text emits TL, TR, BR, BR, BL, TL for each glyph. Existing
                    // Shadow/Outline copies have their own colors and pass through.
                    AddBorder(source[i], source[i + 1], source[i + 2], source[i + 4]);
                }
                for (int j = 0; j < 6; j++)
                {
                    UIVertex vertex = source[i + j];
                    if (marked) vertex.color = new Color32(255, 255, 255, vertex.color.a);
                    output.Add(vertex);
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(output);
        }

        private static bool IsMarker(Color32 color) => color.r == 187 && color.g == 186 && color.b == 255;

        private void AddBorder(UIVertex tl, UIVertex tr, UIVertex br, UIVertex bl)
        {
            float height = tl.position.y - bl.position.y;
            if (height <= 0f) return;
            float width = height * .06f;
            // Four strips preserve all five gradient stops within each glyph.
            // UVs are interpolated with positions, so the atlas is never stretched.
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * width, Mathf.Sin(angle) * width, 0f);
                for (int strip = 0; strip < 4; strip++)
                {
                    float top = strip / 4f, bottom = (strip + 1) / 4f;
                    UIVertex a = Slice(tl, bl, top, offset);
                    UIVertex b = Slice(tr, br, top, offset);
                    UIVertex c = Slice(tr, br, bottom, offset);
                    UIVertex d = Slice(tl, bl, bottom, offset);
                    output.Add(a); output.Add(b); output.Add(c);
                    output.Add(c); output.Add(d); output.Add(a);
                }
            }
        }

        private static UIVertex Slice(UIVertex top, UIVertex bottom, float t, Vector3 offset)
        {
            UIVertex vertex = top;
            vertex.position = Vector3.Lerp(top.position, bottom.position, t) + offset;
            vertex.uv0 = Vector4.Lerp(top.uv0, bottom.uv0, t);
            vertex.uv1 = Vector4.Lerp(top.uv1, bottom.uv1, t);
            vertex.uv2 = Vector4.Lerp(top.uv2, bottom.uv2, t);
            vertex.uv3 = Vector4.Lerp(top.uv3, bottom.uv3, t);
            int index = Mathf.Min((int)(t * 4f), 3);
            Color color = Color.Lerp(Color.white, Color.Lerp(Stops[index], Stops[index + 1], t * 4f - index), .4f);
            color.a = Mathf.Lerp(top.color.a, bottom.color.a, t) / 255f;
            vertex.color = color;
            return vertex;
        }
    }
}
