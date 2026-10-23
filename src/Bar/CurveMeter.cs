using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // =============================================================
    // Curved Meter Watcher
    //
    // Responsibilities:
    // - Locate scrHitErrorMeter
    // - BPM / Difficulty
    // - Animate BPM changes
    // - Calculate displayed judgement ranges
    // - Request mesh updates from CurvedMeterGraphic
    // =============================================================

    public class CurvedMeterWatcher : MeterWatcher
    {
        private GameObject? uiRoot;

        private Image? originalImage;
        private bool originalImageEnabled;
        private double retryAfter;

        private MeterBackgroundGraphic? layer1Image;
        private Image? layer3Image;


        private Sprite? layer3Sprite;

        private CurvedMeterGraphic? graphic;

        // =========================================================
        // Curved meter geometry
        // =========================================================

        private Sprite? LoadEmbeddedSprite(string resourceName)
        {
            Assembly assembly =
                typeof(CurvedMeterWatcher).Assembly;

            using Stream stream =
                assembly.GetManifestResourceStream(
                    resourceName
                );

            if (stream == null)
            {
                throw new FileNotFoundException("Embedded meter sprite is missing: " + resourceName);
            }

            using MemoryStream memory =
                new MemoryStream();

            stream.CopyTo(memory);

            Texture2D texture =
                new Texture2D(
                    2,
                    2,
                    TextureFormat.RGBA32,
                    false
                );

            if (!texture.LoadImage(memory.ToArray()))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException("Cannot decode meter sprite: " + resourceName);
            }

            texture.wrapMode =
                TextureWrapMode.Clamp;

            texture.filterMode =
                FilterMode.Bilinear;

            return Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    texture.width,
                    texture.height
                ),
                new Vector2(
                    0.5f,
                    0.5f
                ),
                100f
            );
        }

        private Image CreateFullImage(
            Transform parent,
            string name,
            Sprite? sprite
        )
        {
            GameObject obj =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            obj.transform.SetParent(
                parent,
                false
            );

            RectTransform rect =
                obj.GetComponent<RectTransform>();

            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;

            Image image =
                obj.GetComponent<Image>();

            image.sprite =
                sprite;

            image.color =
                Color.white;

            image.raycastTarget =
                false;

            return image;
        }

        // =========================================================
        // Build the curved meter UI
        // =========================================================

        private void BuildMeterUI()
        {
            if (
                meter == null ||
                meter.curvedMeter == null
            )
            {
                throw new InvalidOperationException("The curved ErrorMeter Image is missing.");
            }


            // Predrop exposes the Image itself, rather than its GameObject.
            originalImage = meter.curvedMeter;


            if (originalImage == null)
                return;

            ResetBandAnimation();
            originalImageEnabled = originalImage.enabled;


            // =====================================================
            // Remove the previous AJB UI
            // =====================================================

            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(
                    uiRoot
                );
            }


            // =====================================================
            // Root
            // =====================================================

            uiRoot =
                new GameObject(
                    "AccurateJudgementBar_Curved_UI",
                    typeof(RectTransform)
                );


            uiRoot.transform.SetParent(
                meter.curvedMeter.transform,
                false
            );


            RectTransform rootRect =
                uiRoot.GetComponent<RectTransform>();


            rootRect.anchorMin =
                Vector2.zero;

            rootRect.anchorMax =
                Vector2.one;

            rootRect.offsetMin =
                Vector2.zero;

            rootRect.offsetMax =
                Vector2.zero;


            // =====================================================
            // Graphic Object
            // =====================================================

            // =====================================================
            // Load the curved meter layer image
            // =====================================================



            if (layer3Sprite == null)
            {
                layer3Sprite =
                    LoadEmbeddedSprite(
                        "AccurateJudgementBar.curved_layer3.png"
                    );
            }


            // =====================================================
            // Layer 1
            // =====================================================

            layer1Image = MeterBackgroundGraphic.Create(uiRoot.transform, true);

            GameObject graphicObject =
                new GameObject(
                    "CurvedMeterGraphic",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(CurvedMeterGraphic)
                );

            graphicObject.transform.SetParent(
                uiRoot.transform,
                false
            );


            RectTransform graphicRect =
                graphicObject
                    .GetComponent<RectTransform>();


            // Fill the entire 400x200 area.
            graphicRect.anchorMin =
                Vector2.zero;

            graphicRect.anchorMax =
                Vector2.one;

            graphicRect.offsetMin =
                Vector2.zero;

            graphicRect.offsetMax =
                Vector2.zero;


            // Required pivot:
            //
            // The curved meter rotates around the bottom center of its image.
            //
            // Match the native meter:
            // pivot = (0.5, 0)
            graphicRect.pivot =
                new Vector2(
                    0.5f,
                    0f
                );


            graphic =
                graphicObject
                    .GetComponent<CurvedMeterGraphic>();


            graphic.raycastTarget =
                false;
                
            // =====================================================
            // Layer 3
            // =====================================================

            layer3Image =
                CreateFullImage(
                    uiRoot.transform,
                    "CurvedLayer3",
                    layer3Sprite
                );


            // =====================================================
            // Hide the original sprite
            //
            // Keep the GameObject and its children
            // so native ticks remain intact.
            // =====================================================

            originalImage.enabled =
                false;


            Debug.Log(
                "[AccurateJudgementBar] " +
                "CurvedMeter UI created"
            );
        }


        // =========================================================
        // Update the curved meter
        // =========================================================

        private void UpdateBars(float bpm, Difficulty difficulty)
        {
            if (bpm <= 0f || graphic == null) return;

            var bands = EvaluateAnimatedBands(bpm, difficulty);
            layer1Image?.RefreshBackground(bands.Count > 0 ? FailureJudgement.Progress : 0f);
            var segments = new List<CurvedMeterGraphic.ArcSegment>();
            foreach (var slice in MeterBandLayout.CurvedSlices(bands))
            {
                segments.Add(new CurvedMeterGraphic.ArcSegment(slice.Start / 2.5f, slice.End / 2.5f,
                    slice.Color, slice.XPerfect));
            }
            graphic.SetSegments(segments);
        }
        private void Update()
        {
            if (!Main.is_mod_enable || Time.unscaledTimeAsDouble < retryAfter) return;
            try { UpdateDisplay(); }
            catch (Exception ex)
            {
                DisplayErrors.Report("curved meter", ex);
                DisplayErrors.Run("curved meter cleanup", Cleanup);
                retryAfter = Time.unscaledTimeAsDouble + 1;
            }
        }

        private void UpdateDisplay()
        {
            var currentMeter = scrController.instance?.errorMeter;
            if (currentMeter != null && currentMeter != meter)
            {
                Cleanup();
                meter = currentMeter;
            }
            if (meter == null)
            {
                searchTimer -= Time.unscaledDeltaTime;
                if (searchTimer > 0f) return;
                searchTimer = 0.25f;
                Cleanup();
                meter = UnityEngine.Object.FindFirstObjectByType<scrHitErrorMeter>();
                if (meter == null) return;
            }
            if (uiRoot == null || originalImage != meter.curvedMeter)
            {
                Cleanup();
                BuildMeterUI();
            }

            float bpm = GetWindowBpm();
            if (!JudgementApi.IsPositive(bpm)) return;
            UpdateBars(bpm, GCS.difficulty);
        }

        // =========================================================
        // Cleanup
        // =========================================================

        private void OnDisable()
        {
            retryAfter = 0;
            DisplayErrors.Run("curved meter cleanup", Cleanup);
        }
        private void OnDestroy() => DisplayErrors.Run("curved meter cleanup", Cleanup);

        private void Cleanup()
        {
            if (originalImage != null)
            {
                originalImage.enabled = originalImageEnabled;
                originalImage = null;
            }
            graphic = null;
            layer1Image = null;
            layer3Image = null;
            ResetBandAnimation();
            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
                UnityEngine.Object.Destroy(
                    uiRoot
                );
                uiRoot = null;
            }


            if (layer3Sprite != null)
            {
                UnityEngine.Object.Destroy(layer3Sprite.texture);
                UnityEngine.Object.Destroy(layer3Sprite);
                layer3Sprite = null;
            }
        }
    }



    // =============================================================
    // Curved Meter Graphic
    //
    // Renders the curved segments.
    // =============================================================

    public class CurvedMeterGraphic : MaskableGraphic
    {
        public struct ArcSegment
        {
            public float startAngle;
            public float endAngle;
            public Color color;
            public bool xPerfectGradient;


            public ArcSegment(
                float startAngle,
                float endAngle,
                Color color
            )
            {
                this.startAngle =
                    startAngle;

                this.endAngle =
                    endAngle;

                this.color =
                    color;
                this.xPerfectGradient = false;
            }

            public ArcSegment(float startAngle, float endAngle, Color color, bool xPerfectGradient)
                : this(startAngle, endAngle, color)
            {
                this.xPerfectGradient = xPerfectGradient;
            }
        }


        // =========================================================
        // Geometry
        // =========================================================

        public float innerRadius =
            147f;

        public float outerRadius =
            159f;


        // One degree per segment produces a smooth arc.
        public float degreesPerStep =
            1f;


        private readonly List<ArcSegment>
            segments =
                new();

        private int outlineWidth = -1;
        private bool vanillaStyle;


        // =========================================================
        // Update
        // =========================================================

        public void SetSegments(
            IEnumerable<ArcSegment> newSegments
        )
        {
            var next = new List<ArcSegment>(newSegments);
            var settings = Main.settings;
            int width = XPerfectOutlineMesh.EffectiveWidth(settings.VanillaXPerfectStyle,
                settings.VanillaXPerfectOutline, settings.XPerfectOutlineWidth);
            bool unchanged = next.Count == segments.Count && outlineWidth == width &&
                vanillaStyle == settings.VanillaXPerfectStyle;
            for (int i = 0; unchanged && i < next.Count; i++)
                unchanged = next[i].startAngle == segments[i].startAngle &&
                    next[i].endAngle == segments[i].endAngle && next[i].color == segments[i].color &&
                    next[i].xPerfectGradient == segments[i].xPerfectGradient;
            if (unchanged) return;
            outlineWidth = width;
            vanillaStyle = settings.VanillaXPerfectStyle;
            segments.Clear();

            segments.AddRange(
                next
            );


            SetVerticesDirty();
        }


        // =========================================================
        // Build Mesh
        // =========================================================

        protected override void OnPopulateMesh(
            VertexHelper vh
        )
        {
            vh.Clear();

            var outlines = new List<XPerfectOutlineMesh.ArcRange>();


            foreach (
                ArcSegment segment
                in segments
            )
            {
                if (segment.xPerfectGradient)
                {
                    outlines.Add(new XPerfectOutlineMesh.ArcRange(segment.startAngle, segment.endAngle, segment.color.a));
                }
                DrawArcSegment(
                    vh,
                    segment.startAngle,
                    segment.endAngle,
                    segment.color,
                    segment.xPerfectGradient
                );
            }
            // Merge touching halves, but never bridge a gap occupied by another color.
            var settings = Main.settings;
            XPerfectOutlineMesh.AddArcs(vh, outlines, innerRadius, outerRadius,
                XPerfectOutlineMesh.EffectiveWidth(settings.VanillaXPerfectStyle,
                    settings.VanillaXPerfectOutline, settings.XPerfectOutlineWidth), settings.VanillaXPerfectStyle);
        }


        private void DrawArcSegment(
            VertexHelper vh,
            float startAngle,
            float endAngle,
            Color color,
            bool xPerfectGradient
        )
        {
            float angleLength =
                endAngle
                - startAngle;


            int steps =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        Mathf.Abs(
                            angleLength
                        )
                        / degreesPerStep
                    )
                );


            float step =
                angleLength
                / steps;

            // Both mirrored halves share the same vertical range, so the
            // gradient stays continuous across the center as the band resizes.
            float gradientBottom = innerRadius * Mathf.Cos(
                Mathf.Max(Mathf.Abs(startAngle), Mathf.Abs(endAngle)) * Mathf.Deg2Rad);


            for (
                int i = 0;
                i < steps;
                i++
            )
            {
                float a0 =
                    startAngle
                    + step * i;


                float a1 =
                    startAngle
                    + step * (i + 1);


                Vector2 inner0 =
                    AngleToPosition(
                        a0,
                        innerRadius
                    );


                Vector2 outer0 =
                    AngleToPosition(
                        a0,
                        outerRadius
                    );


                Vector2 inner1 =
                    AngleToPosition(
                        a1,
                        innerRadius
                    );


                Vector2 outer1 =
                    AngleToPosition(
                        a1,
                        outerRadius
                    );


                AddQuad(
                    vh,
                    inner0,
                    outer0,
                    outer1,
                    inner1,
                    color,
                    xPerfectGradient,
                    gradientBottom,
                    outerRadius
                );
            }
        }


        // =========================================================
        // Meter Angle → Position
        // =========================================================

        private static Vector2 AngleToPosition(
            float meterAngle,
            float radius
        )
        {
            float rad =
                meterAngle
                * Mathf.Deg2Rad;


            // Zero degrees points upward.
            return new Vector2(
                Mathf.Sin(rad)
                    * radius,

                Mathf.Cos(rad)
                    * radius
            );
        }


        // =========================================================
        // Quad
        // =========================================================

        private static void AddQuad(
            VertexHelper vh,
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            Color color,
            bool xPerfectGradient,
            float gradientBottom,
            float gradientTop
        )
        {
            int index =
                vh.currentVertCount;


            UIVertex vertex =
                UIVertex.simpleVert;


            vertex.color =
                color;


            vertex.position = p0;
            if (xPerfectGradient) vertex.color = XPerfectGradient.Sample(p0.y, gradientBottom, gradientTop, color.a);
            vh.AddVert(vertex);

            vertex.position = p1;
            if (xPerfectGradient) vertex.color = XPerfectGradient.Sample(p1.y, gradientBottom, gradientTop, color.a);
            vh.AddVert(vertex);

            vertex.position = p2;
            if (xPerfectGradient) vertex.color = XPerfectGradient.Sample(p2.y, gradientBottom, gradientTop, color.a);
            vh.AddVert(vertex);

            vertex.position = p3;
            if (xPerfectGradient) vertex.color = XPerfectGradient.Sample(p3.y, gradientBottom, gradientTop, color.a);
            vh.AddVert(vertex);


            vh.AddTriangle(
                index,
                index + 1,
                index + 2
            );


            vh.AddTriangle(
                index,
                index + 2,
                index + 3
            );
        }
    }
}





