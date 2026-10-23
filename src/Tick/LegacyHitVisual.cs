using System;
using System.Collections.Generic;
using System.IO;
using ADOFAI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    public sealed class LegacyHitVisual : MonoBehaviour
    {
        private static readonly HashSet<LegacyHitVisual> instances = new HashSet<LegacyHitVisual>();
        private static Sprite? legacySprite;
        private ErrorMeterTick tick = null!;
        private Image image = null!;
        private Transform? originalParent;
        private int originalSibling;
        private Sprite? originalSprite;
        private Sprite? originalOverrideSprite;
        private Material? originalMaterial;
        private Image.Type originalType;
        private bool originalPreserveAspect, originalUseSpriteMesh;
        private Vector2 anchorMin, anchorMax, pivot, sizeDelta;
        private Vector3 position, scale;
        private Quaternion rotation;
        private bool initialized, applied, faulted;
        private HitMargin lastMargin;

        private void Initialize(ErrorMeterTick target)
        {
            if (target == null || target.tick == null)
                throw new InvalidOperationException("Legacy tick has no native Image to preserve.");
            tick = target;
            image = target.tick;
            var rect = image.rectTransform;
            originalParent = rect.parent;
            originalSibling = rect.GetSiblingIndex();
            originalSprite = image.sprite;
            originalOverrideSprite = image.overrideSprite;
            originalMaterial = image.material;
            originalType = image.type;
            originalPreserveAspect = image.preserveAspect;
            originalUseSpriteMesh = image.useSpriteMesh;
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            sizeDelta = rect.sizeDelta;
            position = rect.anchoredPosition3D;
            scale = rect.localScale;
            rotation = rect.localRotation;
            initialized = true;
        }

        private static Sprite LoadSprite()
        {
            if (legacySprite != null) return legacySprite;
            using var stream = typeof(LegacyHitVisual).Assembly.GetManifestResourceStream("AccurateJudgementBar.legacy_tick.png");
            if (stream == null) throw new FileNotFoundException("Embedded legacy tick sprite is missing.");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(buffer.ToArray()))
                    throw new InvalidDataException("Cannot decode the legacy tick sprite.");
                texture.name = "AccurateJudgementBar.LegacyTick";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                legacySprite = Sprite.Create(texture, new Rect(0, 0, 8, 182), new Vector2(.5f, 0f), 100f);
                if (legacySprite == null) throw new InvalidDataException("Cannot create the legacy tick sprite.");
                return legacySprite;
            }
            finally
            {
                if (legacySprite == null)
                    DisplayErrors.Run("Legacy tick texture cleanup", () => Destroy(texture));
            }
        }

        internal static void RefreshAll()
        {
            // A broken pooled tick must not prevent the other ticks from restoring.
            foreach (var instance in new List<LegacyHitVisual>(instances))
            {
                if (instance == null) continue;
                // Settings refresh / re-enable permits an explicit retry, after any pending rollback.
                instance.RefreshSafely(instance.tick, instance.lastMargin, retry: true);
            }
        }

        private void RefreshSafely(ErrorMeterTick target, HitMargin margin, bool retry = false)
        {
            try
            {
                lastMargin = margin;
                bool replaced = tick != target || image != target.tick;
                if (faulted)
                {
                    if (!retry && !replaced) return;
                    if (!Restore()) return;
                    faulted = false;
                }
                if (!initialized || replaced)
                {
                    if (!Restore()) { faulted = true; return; }
                    initialized = false;
                    Initialize(target);
                    faulted = false;
                }
                Refresh();
            }
            catch (Exception ex)
            {
                faulted = true;
                DisplayErrors.Report("Legacy tick display", ex);
                Restore();
            }
        }

        private void Refresh()
        {
            // Follow the game's effective preset, including competitive mode being off.
            bool useLegacy = Main.is_mod_enable && Main.settings != null && Main.settings.UseLegacyHit &&
                (lastMargin != HitMargin.XPerfect ||
                    Persistence.hitMarginPerfectTick != HitMarginPerfectTickPreset.ShowXPerfect);
            if (useLegacy == applied || image == null) return;
            var rect = image.rectTransform;
            if (useLegacy)
            {
                var sprite = LoadSprite();
                // Mark ownership before the first mutation, so partial application also restores.
                applied = true;
                // The old Image occupied the entire 8 x 182 tick root. Its
                // transparent stem positions the visible mark at the meter's rim.
                rect.SetParent(tick.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(.5f, 0f);
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition3D = Vector3.zero;
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                image.sprite = sprite;
                image.overrideSprite = sprite;
                image.material = null;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.useSpriteMesh = false;
            }
            else
            {
                if (!Restore()) faulted = true;
            }
        }

        private bool Restore()
        {
            if (!initialized || !applied) return true;
            // Independent restore steps: a destroyed parent must not prevent sprite restoration.
            bool restored = DisplayErrors.TryRun("Legacy tick restore parent", () =>
            {
                if (image == null) return;
                image.rectTransform.SetParent(originalParent, false);
                image.rectTransform.SetSiblingIndex(originalSibling);
            });
            restored &= DisplayErrors.TryRun("Legacy tick restore layout", () =>
            {
                if (image == null) return;
                var rect = image.rectTransform;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.sizeDelta = sizeDelta;
                rect.anchoredPosition3D = position;
                rect.localScale = scale;
                rect.localRotation = rotation;
            });
            restored &= DisplayErrors.TryRun("Legacy tick restore sprite", () => { if (image != null) image.sprite = originalSprite; });
            restored &= DisplayErrors.TryRun("Legacy tick restore override", () => { if (image != null) image.overrideSprite = originalOverrideSprite; });
            restored &= DisplayErrors.TryRun("Legacy tick restore material", () => { if (image != null) image.material = originalMaterial; });
            restored &= DisplayErrors.TryRun("Legacy tick restore type", () => { if (image != null) image.type = originalType; });
            restored &= DisplayErrors.TryRun("Legacy tick restore aspect", () => { if (image != null) image.preserveAspect = originalPreserveAspect; });
            restored &= DisplayErrors.TryRun("Legacy tick restore mesh", () => { if (image != null) image.useSpriteMesh = originalUseSpriteMesh; });
            if (restored) applied = false;
            return restored;
        }

        private void OnDestroy()
        {
            Restore();
            instances.Remove(this);
        }

        [HarmonyPatch(typeof(ErrorMeterTick), "Show")]
        private static class ShowPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ErrorMeterTick __instance, HitMargin hitMargin)
            {
                // Only AJB's postfix is guarded; the game's Show / Hit exceptions stay untouched.
                try
                {
                    if (__instance == null) return;
                    var visual = __instance.GetComponent<LegacyHitVisual>();
                    if (visual == null)
                    {
                        if (!Main.is_mod_enable || Main.settings == null || !Main.settings.UseLegacyHit) return;
                        visual = __instance.gameObject.AddComponent<LegacyHitVisual>();
                        visual.tick = __instance;
                        instances.Add(visual);
                    }
                    visual.RefreshSafely(__instance, hitMargin);
                }
                catch (Exception ex) { DisplayErrors.Report("Legacy tick component", ex); }
            }
        }
    }
}
