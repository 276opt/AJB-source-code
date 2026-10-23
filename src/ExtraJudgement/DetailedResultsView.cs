using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // Attached to results / native completion Text. The HUD keeps handling visibility normally.
    public sealed class DetailedResultsView : MonoBehaviour
    {
        private Text? text;
        private scrHUDText? hud;
        private Image? background;
        private TextGenerator? backgroundGenerator;
        private bool listeningForFont;
        private bool fontRefreshPending;
        private scrController? completionOwner;
        private bool isolated;
        private bool nativeChangeColor;
        private Color nativeColor;
        private readonly Dictionary<Shadow, Color> nativeShadows = new Dictionary<Shadow, Color>();
        internal bool DisplayFailed { get; private set; }

        internal void BindCompletion(scrController owner)
        {
            if (completionOwner == owner) return;
            RestoreColors();
            completionOwner = owner;
            DisplayFailed = false;
            CaptureNative(GetComponent<scrHUDText>());
        }

        internal bool IsCurrentCompletion(scrController? owner) => completionOwner != null
            && completionOwner == owner && text != null && CompletionResultsText.Owns(completionOwner, text);

        internal void CaptureNative(scrHUDText? sourceHud)
        {
            text = GetComponent<Text>();
            if (hud != sourceHud && isolated) RestoreColors();
            hud = sourceHud;
            // Co-op's ShowForPlayer assigns a new planet color even while isolation is active.
            if (text != null && (!isolated || (hud != null && !hud.enabled)
                || text.color != EnchantedDetailedResults.DefaultTextColor))
                nativeColor = text.color;
        }

        internal void Refresh()
        {
            if (!DisplayErrors.TryRun("Detailed Results view", RefreshView))
            {
                DisplayFailed = true;
                enabled = false; // OnDisable restores any partial display changes.
            }
            else DisplayFailed = false;
        }

        private void LateUpdate()
        {
            DisplayErrors.Run("Detailed Results refresh font", () =>
            {
                if (!fontRefreshPending) return;
                fontRefreshPending = false;
                if (text != null) text.SetVerticesDirty();
            });
            Refresh();
        }

        private void RefreshView()
        {
            if (text == null) text = GetComponent<Text>();
            var settings = Main.settings;
            if (text == null || !text.gameObject.activeInHierarchy || settings == null
                || !EnchantedDetailedResults.Active || !(completionOwner != null
                    ? CompletionResultsText.Owns(completionOwner, text) : XPerfectResultsColor.Owns(text))
                || string.IsNullOrEmpty(text.text))
            {
                enabled = false;
                return;
            }
            if (settings.DetailedResultsDefaultColors)
            {
                if (!isolated)
                {
                    nativeColor = text.color;
                    nativeChangeColor = hud != null && hud.changeColor;
                    isolated = true;
                }
                // Do not disable scrHUDText: its no-HUD/autoplay visibility logic must still run.
                if (hud != null) hud.changeColor = false;
                text.color = EnchantedDetailedResults.DefaultTextColor;
                foreach (var shadow in text.GetComponents<Shadow>())
                {
                    if (!nativeShadows.ContainsKey(shadow)) nativeShadows.Add(shadow, shadow.effectColor);
                    shadow.effectColor = EnchantedDetailedResults.DefaultShadowColor;
                }
            }
            else RestoreColors();

            if (!settings.DetailedResultsBackground || !text.isActiveAndEnabled)
            {
                if (background != null) background.gameObject.SetActive(false);
                return;
            }
            // Populate from the actual wrapped glyphs, including localized text and font sizing.
            // Modern uGUI Text renders every quad; removing four vertices drops the final glyph.
            var rect = text.rectTransform;
            // The native cachedTextGenerator belongs to Text's renderer. A background
            // measurement must not repopulate or invalidate that rendering cache.
            if (backgroundGenerator == null) backgroundGenerator = new TextGenerator();
            if (!listeningForFont)
            {
                Font.textureRebuilt += OnFontTextureRebuilt;
                listeningForFont = true;
            }
            if (!backgroundGenerator.Populate(text.text, text.GetGenerationSettings(rect.rect.size)))
            {
                if (background != null) background.gameObject.SetActive(false);
                return;
            }
            var vertices = backgroundGenerator.verts;
            int count = vertices.Count;
            if (count <= 0)
            {
                if (background != null) background.gameObject.SetActive(false);
                return;
            }
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < count; i++)
            {
                Vector2 point = vertices[i].position / text.pixelsPerUnit;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            if (background == null)
            {
                var obj = new GameObject("AJB Detailed Results Background", typeof(RectTransform), typeof(CanvasRenderer));
                // Keep a reference before adding/configuring components so partial creation is cleanable.
                try { background = obj.AddComponent<Image>(); }
                catch { Object.Destroy(obj); throw; }
                background.raycastTarget = false;
            }
            // A sibling immediately behind Text shares its parent mask/CanvasGroup and draw order.
            var bg = background.rectTransform;
            if (bg.parent != rect.parent) bg.SetParent(rect.parent, false);
            bg.anchorMin = bg.anchorMax = new Vector2(.5f, .5f);
            bg.pivot = new Vector2(.5f, .5f);
            bg.sizeDelta = max - min + new Vector2(24f, 16f);
            bg.localScale = rect.localScale;
            bg.localRotation = rect.localRotation;
            bg.localPosition = rect.localPosition + rect.localRotation * Vector3.Scale((min + max) / 2f, rect.localScale);
            int backgroundIndex = bg.GetSiblingIndex();
            int textIndex = rect.GetSiblingIndex();
            // Moving an earlier sibling removes it before insertion, shifting Text left.
            if (backgroundIndex != textIndex - 1)
                bg.SetSiblingIndex(backgroundIndex < textIndex ? textIndex - 1 : textIndex);
            float alpha = text.canvasRenderer.GetAlpha();
            foreach (var group in text.GetComponents<CanvasGroup>()) alpha *= group.alpha;
            background.color = new Color(0f, 0f, 0f, .6f * alpha);
            background.gameObject.SetActive(true);
        }

        private void RestoreColors()
        {
            if (!isolated) return;
            // With a live single-player HUD, restore the current level colors, not an old frame.
            var scheme = hud != null && hud.enabled && nativeChangeColor && scrVfx.instance != null
                ? scrVfx.instance.currentColourScheme : null;
            DisplayErrors.Run("Detailed Results restore HUD", () => { if (hud != null) hud.changeColor = nativeChangeColor; });
            DisplayErrors.Run("Detailed Results restore color", () =>
            {
                if (text != null) text.color = scheme != null ? scheme.colourText : nativeColor;
            });
            foreach (var pair in nativeShadows)
                DisplayErrors.Run("Detailed Results restore shadow", () =>
                {
                    if (pair.Key != null) pair.Key.effectColor = scheme != null
                        ? scheme.colourTextShadow : pair.Value;
                });
            nativeShadows.Clear();
            isolated = false;
        }

        private void OnDisable()
        {
            RestoreColors();
            fontRefreshPending = false;
            if (listeningForFont)
            {
                Font.textureRebuilt -= OnFontTextureRebuilt;
                listeningForFont = false;
            }
            DisplayErrors.Run("Detailed Results hide background", () =>
            {
                if (background != null) background.gameObject.SetActive(false);
            });
        }

        private void OnDestroy()
        {
            OnDisable();
            DisplayErrors.Run("Detailed Results dispose layout", () =>
            {
                if (backgroundGenerator != null) ((System.IDisposable)backgroundGenerator).Dispose();
                backgroundGenerator = null;
            });
            DisplayErrors.Run("Detailed Results destroy background", () =>
            {
                if (background != null) Object.Destroy(background.gameObject);
            });
        }

        private void OnEnable() => DisplayErrors.Run("Detailed Results enable layout", () => backgroundGenerator?.Invalidate());

        private void OnFontTextureRebuilt(Font font) => DisplayErrors.Run("Detailed Results font rebuild", () =>
        {
            if (text == null || text.font != font) return;
            backgroundGenerator?.Invalidate();
            // The event can run inside Canvas mesh rebuilding. Defer the request
            // until LateUpdate so it cannot reenter that loop or be cleared by it.
            fontRefreshPending = true;
        });
    }
}
