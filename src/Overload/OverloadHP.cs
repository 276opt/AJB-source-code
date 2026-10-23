using System;
using System.Collections.Generic;
using System.Xml;
using ADOFAI.Editor.Actions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    public class OverloadBar : MonoBehaviour
    {
        // ============================================================
        // 1. Shared settings for most appearance options
        // ============================================================

        public sealed class UISettings
        {
            public Vector2 Position = new Vector2(0f, -50f);
            public Vector2 Size = new Vector2(320f, 26f);

            public Vector2 Anchor = new Vector2(0.5f, 1f);
            public Vector2 Pivot = new Vector2(0.5f, 1f);

            public float BorderSize = 3f;

            public Color BackgroundColor = new Color(0f, 0f, 0f, 0.75f);
            public Color FillColor = new Color(0f, 1f, 1f, 1f);
            public Color PreviewColor = new Color(1f, 1f, 1f, 0.30f);

            public bool ShowValueText = true;
            public Vector2 ValueTextOffset = new Vector2(0f, -5f);
            public float ValueTextHeight = 24f;
            public int ValueTextFontSize = 18;
            public Color ValueTextColor = Color.white;
            public TextAnchor ValueTextAlignment = TextAnchor.MiddleRight;

            // Prefer FontOverride when provided.
            // Otherwise, use the Unity built-in font.
            public Font? FontOverride;
            public string FontResourceName = "Arial.ttf";

            // Previewed overload increase for one stray press.
            public float PreviewMissIncrease = 0.5f;

            public Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
            public float MatchWidthOrHeight = 0.5f;
            public int SortingOrder = 10000;
        }


        // ============================================================
        // 2. Runtime / UI references
        // ============================================================

        private const string CanvasName = "AccurateJudgementBarCanvas";

        public float overloadCounter;
        private bool deathTriggered;

        public event Action? OnOverloaded;

        private RectTransform? rootRect;
        private RectTransform? fillRect;
        private Image? fillImage;
        private RectTransform? previewFillRect;
        private Image? previewFillImage;
        private Text? valueText;

        private Text? ebpmText;
        private Text? officialNoFailText;

        private readonly List<Text> managedTexts = new List<Text>();

        public UISettings Settings { get; private set; } = new UISettings();

        public RectTransform? RootRect => rootRect;
        public Text? ValueText => valueText;

        // ============================================================
        // 3. Public creation API
        // ============================================================
    
        // Preserve the original calling convention:
        // OverloadBar.Create(new Vector2(x, y), new Vector2(width, height));

        public static OverloadBar Create(Vector2 position, Vector2 size)
        {
            OverloadBar overload_bar = Create(new UISettings
            {
                Position = position,
                Size = size
            });

            return overload_bar;
        }

        public static OverloadBar Create(UISettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Canvas canvas = GetOrCreateCanvas(settings);
            return CreateBar(canvas.transform, settings);
        }

        private static OverloadBar CreateBar(Transform parent, UISettings settings)
        {
            RectTransform root = CreateRect("OverloadBar", parent);
            SetAnchoredRect(
                root,
                settings.Anchor,
                settings.Pivot,
                settings.Position,
                settings.Size
            );

            OverloadBar bar = root.gameObject.AddComponent<OverloadBar>();
            bar.Settings = settings;
            bar.rootRect = root;

            bar.ebpmText = bar.AddText(
                "EBPM",
                $"EBPM: {bar.GetEBPM():0.00}",

                new Vector2(0f, -5f),
                new Vector2(150f, 24f),

                alignment: TextAnchor.MiddleLeft,
                anchor: new Vector2(0f, 0f),
                pivot: new Vector2(0f, 1f)
            );

            bar.BuildUI();
            bar.Refresh();

            return bar;
        }

        public void DestroyBar()
        {
            if (this == null)
                return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }



        // ============================================================
        // 4. UI construction
        // ============================================================

        private void BuildUI()
        {
            if (rootRect == null)
                return;

            // Background
            Image background = CreateImage(
                "Background",
                rootRect,
                Settings.BackgroundColor
            );
            StretchToParent(background.rectTransform, 0f);

            // Inner
            RectTransform inner = CreateRect("Inner", background.transform);
            StretchToParent(inner, Settings.BorderSize);

            // Current fill
            fillImage = CreateImage("Fill", inner, Settings.FillColor);
            fillRect = fillImage.rectTransform;
            SetupHorizontalFill(fillRect);

            // Preview fill
            previewFillImage = CreateImage(
                "PreviewFill",
                inner,
                Settings.PreviewColor
            );
            previewFillRect = previewFillImage.rectTransform;
            SetupHorizontalFill(previewFillRect);

            // Value text
            if (Settings.ShowValueText)
                valueText = BuildValueText();

            officialNoFailText = AddText(
                "OfficialNoFail",
                "Official No Fail On!",
                new Vector2(0f, 5f),
                new Vector2(Settings.Size.x, Settings.ValueTextHeight),
                anchor: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 0f)
            );
            officialNoFailText.gameObject.SetActive(false);
        }

        private Text BuildValueText()
        {
            if (rootRect == null)
                throw new InvalidOperationException("Root UI has not been created.");

            Text text = CreateTextElement(
                "ValueText",
                rootRect,
                "0.00 / 1.00",
                Settings.ValueTextFontSize,
                Settings.ValueTextColor,
                Settings.ValueTextAlignment,
                ResolveFont(Settings)
            );

            SetAnchoredRect(
                text.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                Settings.ValueTextOffset,
                new Vector2(Settings.Size.x, Settings.ValueTextHeight)
            );

            managedTexts.Add(text);
            return text;
        }


        // ============================================================
        // 5. Shared UI helpers
        //    Reuse GameObject setup when adding Image or Text elements.
        // ============================================================

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image)
            );

            obj.transform.SetParent(parent, false);

            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        private static Text CreateTextElement(
            string name,
            Transform parent,
            string text,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Font font
        )
        {
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text)
            );

            obj.transform.SetParent(parent, false);

            Text result = obj.GetComponent<Text>();
            result.text = text;
            result.fontSize = fontSize;
            result.color = color;
            result.alignment = alignment;
            result.font = font;
            result.raycastTarget = false;

            return result;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size
        )
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void StretchToParent(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetupHorizontalFill(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font ResolveFont(UISettings settings)
        {
            if (settings.FontOverride != null)
                return settings.FontOverride;

            return Resources.GetBuiltinResource<Font>(
                settings.FontResourceName
            );
        }


        // ============================================================
        // 6. Common configuration methods
        // ============================================================

        public void SetPosition(Vector2 position)
        {
            Settings.Position = position;

            if (rootRect != null)
                rootRect.anchoredPosition = position;
        }

        public void SetSize(Vector2 size)
        {
            Settings.Size = size;

            if (rootRect != null)
                rootRect.sizeDelta = size;

            if (valueText != null)
            {
                valueText.rectTransform.sizeDelta =
                    new Vector2(size.x, Settings.ValueTextHeight);
            }

            if (officialNoFailText != null)
                officialNoFailText.rectTransform.sizeDelta =
                    new Vector2(size.x, Settings.ValueTextHeight);
        }

        public void SetValueTextOffset(Vector2 offset)
        {
            Settings.ValueTextOffset = offset;

            if (valueText != null)
                valueText.rectTransform.anchoredPosition = offset;
        }

        public void SetFont(Font font)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            Settings.FontOverride = font;
            ApplyFontToManagedTexts(font);
        }

        public void SetBuiltinFont(string resourceName)
        {
            Settings.FontOverride = null;
            Settings.FontResourceName = resourceName;

            ApplyFontToManagedTexts(
                ResolveFont(Settings)
            );
        }

        public void SetFontSize(int fontSize)
        {
            Settings.ValueTextFontSize = fontSize;

            if (valueText != null)
                valueText.fontSize = fontSize;
        }

        public void SetValueTextVisible(bool visible)
        {
            Settings.ShowValueText = visible;

            if (visible)
            {
                if (valueText == null)
                    valueText = BuildValueText();

                valueText.gameObject.SetActive(true);
            }
            else if (valueText != null)
            {
                valueText.gameObject.SetActive(false);
            }
        }

        private void ApplyFontToManagedTexts(Font font)
        {
            for (int i = 0; i < managedTexts.Count; i++)
            {
                if (managedTexts[i] != null)
                    managedTexts[i].font = font;
            }
        }

        // ============================================================
        // 7. Text creation API
        // ============================================================

        /// <summary>
        /// Adds a text element.
        ///
        /// Minimal example:
        /// AddText(
        ///     "Title",
        ///     "OVERLOAD",
        ///     new Vector2(0f, 20f),
        ///     new Vector2(320f, 24f)
        /// );
        ///
        /// Pass fontSize, color, alignment, anchor, or pivot to customize the element.
        /// </summary>
        public Text AddText(
            string name,
            string text,
            Vector2 position,
            Vector2 size,
            int? fontSize = null,
            Color? color = null,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            Vector2? anchor = null,
            Vector2? pivot = null,
            Font? font = null
        )
        {
            if (rootRect == null)
                throw new InvalidOperationException("Root UI has not been created.");

            Text result = CreateTextElement(
                name,
                rootRect,
                text,
                fontSize ?? Settings.ValueTextFontSize,
                color ?? Settings.ValueTextColor,
                alignment,
                font ?? ResolveFont(Settings)
            );

            SetAnchoredRect(
                result.rectTransform,
                anchor ?? new Vector2(0.5f, 0.5f),
                pivot ?? new Vector2(0.5f, 0.5f),
                position,
                size
            );

            managedTexts.Add(result);
            return result;
        }


        // ============================================================
        // 8. Per-frame updates
        // ============================================================

        public void Tick(double deltaSongPos)
        {
            if (deltaSongPos <= 0)
                return;

            overloadCounter = FailBarAccess.GetOverloadCounter();

            scrConductor? conductor = scrConductor.instance;

            if (conductor == null || conductor.crotchetAtStart <= 0)
                return;

            CheckDeath();
            UpdateVisual();
        }

        private void CheckDeath()
        {
            // The threshold is strictly greater than 1, not greater than or equal to 1.
            if (overloadCounter > 1.0f && !deathTriggered)
            {
                deathTriggered = true;
                OnOverloaded?.Invoke();
            }
        }


        // ============================================================
        // 9. Visual updates
        // ============================================================

        public void UpdateVisual()
        {
            float value = Mathf.Clamp01(overloadCounter);

            float previewValue = Mathf.Clamp01(
                overloadCounter + Settings.PreviewMissIncrease
            );

            UpdatePreview(previewValue);
            UpdateFill(value);
            UpdateValueText();
            UpdateFillColor(value);
            UpdateOfficialNoFailText();
        }

        private void UpdateOfficialNoFailText()
        {
            if (officialNoFailText == null)
                return;

            scrController? controller = ADOBase.controller;
            // Match the official-level protection in scrFailBar.DidFail().
            bool visible = controller != null && ADOBase.isOfficialLevel &&
                (!controller.gameworld || controller.percentComplete >= 0.96f);

            if (officialNoFailText.gameObject.activeSelf != visible)
                officialNoFailText.gameObject.SetActive(visible);
        }

        private void UpdatePreview(float value)
        {
            if (previewFillRect != null)
                previewFillRect.anchorMax = new Vector2(value, 1f);
        }

        private void UpdateFill(float value)
        {
            if (fillRect != null)
                fillRect.anchorMax = new Vector2(value, 1f);
        }

        private void UpdateValueText()
        {
            if (valueText != null)
                valueText.text = $"{overloadCounter:0.00} / 1.00";
            if (ebpmText != null)
                ebpmText.text = $"EBPM: {GetEBPM():0.00}";
        }

        private void UpdateFillColor(float value)
        {
            if (fillImage == null)
                return;

            // Preserve the original color mapping.
            // Keep future color changes in this method.
            if (value < 0.5f)
            {
                fillImage.color = Color.Lerp(
                    new Color(1f, 0f, 0f),
                    new Color(0.98f, 0f, 1f),
                    value
                );
            }
        }


        // ============================================================
        // 10. Refresh / Reset
        // ============================================================

        public void Refresh()
        {
            overloadCounter = FailBarAccess.GetOverloadCounter();
            CheckDeath();
            UpdateVisual();
        }

        public void ResetCounter()
        {
            overloadCounter = 0f;
            deathTriggered = false;
            UpdateVisual();
        }


        // ============================================================
        // 11. GetEBPM scrFailBar private overloadCounter
        // ============================================================

        public double GetSpeedTrail(scrConductor conductor)
        {
            float pitch =
                conductor.song != null
                    ? conductor.song.pitch
                    : 1f;
            return pitch;
        }

        public double? GetEBPM()
        {
            scrConductor? conductor =
                scrConductor.instance;

            if (conductor == null)
                return null;

            if (conductor.crotchetAtStart == 0)
                return null;

            return 60.0 / conductor.crotchetAtStart * GetSpeedTrail(conductor);
        }

        public static class FailBarAccess
        {
            private static scrFailBar? failBar;

            private static readonly AccessTools.FieldRef<scrFailBar, float>
                overloadCounterRef =
                    AccessTools.FieldRefAccess<scrFailBar, float>(
                        "overloadCounter"
                    );

            public static float GetOverloadCounter()
            {
                if (failBar == null)
                    return 0f;

                return overloadCounterRef(failBar);
            }

            public static void SetInstance(scrFailBar instance)
            {
                failBar = instance;
            }
        }

        [HarmonyPatch(typeof(scrFailBar), "Update")]
        public static class ScrFailBarUpdatePatch
        {
            private static void Prefix(scrFailBar __instance)
            {
                FailBarAccess.SetInstance(__instance);
            }
        }


        // ============================================================
        // 12. Canvas
        // ============================================================

        private static Canvas GetOrCreateCanvas(UISettings settings)
        {
            GameObject? existing = GameObject.Find(CanvasName);

            if (existing != null)
            {
                Canvas? existingCanvas = existing.GetComponent<Canvas>();

                if (existingCanvas != null)
                {
                    ApplyCanvasSettings(existingCanvas, settings);
                    return existingCanvas;
                }
            }

            GameObject canvasObject = new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)
            );

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            ApplyCanvasSettings(canvas, settings);

            UnityEngine.Object.DontDestroyOnLoad(canvasObject);

            return canvas;
        }

        private static void ApplyCanvasSettings(
            Canvas canvas,
            UISettings settings
        )
        {
            canvas.sortingOrder = settings.SortingOrder;

            CanvasScaler? scaler = canvas.GetComponent<CanvasScaler>();

            if (scaler == null)
                return;

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                settings.ReferenceResolution;

            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            scaler.matchWidthOrHeight =
                settings.MatchWidthOrHeight;
        }
    }
}
