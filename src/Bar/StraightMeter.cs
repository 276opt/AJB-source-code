

using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UI;


namespace AccurateJudgementBar 
{
    public class StraightMeterWatcher : MeterWatcher
    {
        private GameObject? uiRoot;
        private MeterPosition? meterPosition;

        private Image? originalImage;
        private bool originalImageEnabled;
        private double retryAfter;

        private MeterBackgroundGraphic? layer1Image;

        private readonly Dictionary<string, Image> bandImages = new Dictionary<string, Image>();

        private Image? layer3Image;


        // =========================================================
        // Resource
        // =========================================================


        private Sprite? layer3Sprite;


        // =========================================================
        // Original texture dimensions
        // =========================================================

        private const float TextureWidth = 400f;
        private const float TextureHeight = 200f;

        private const float BarHeight = 12f;


        // =========================================================
        // Colors
        // =========================================================




        // =========================================================
        // BPM
        // =========================================================

        // =========================================================
        // Load embedded sprite
        // =========================================================

        private Sprite? LoadEmbeddedSprite(
            string resourceName
        )
        {
            Assembly assembly =
                typeof(StraightMeterWatcher)
                    .Assembly;


            using Stream stream =
                assembly
                    .GetManifestResourceStream(
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


            if (
                !texture.LoadImage(
                    memory.ToArray()
                )
            )
            {
                UnityEngine.Object.Destroy(
                    texture
                );

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


        // =========================================================
        // Create an Image that fills its parent
        // =========================================================

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
        // Create a colored bar
        // =========================================================

        private Image CreateBar(
            Transform parent,
            string name,
            Color color
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


            Image image =
                obj.GetComponent<Image>();


            // With sprite = null,
            // a Unity Image still renders a solid-color rectangle.
            image.sprite =
                null;

            image.color =
                color;

            image.raycastTarget =
                false;


            RectTransform rect =
                obj.GetComponent<RectTransform>();


            // Keep the bar vertically centered in the image.
            float halfHeight =
                BarHeight
                / TextureHeight
                / 2f;


            rect.anchorMin =
                new Vector2(
                    0.5f,
                    0.5f - halfHeight
                );

            rect.anchorMax =
                new Vector2(
                    0.5f,
                    0.5f + halfHeight
                );


            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;


            return image;
        }


        // =========================================================
        // Set the bar width
        // =========================================================

        private void SetBarWidth(
            Image? image,
            float width
        )
        {
            if (image == null)
                return;


            width =
                Mathf.Clamp(
                    width,
                    0f,
                    TextureWidth + FailureJudgement.ExtensionWidth
                );


            float halfWidth =
                width
                / TextureWidth
                / 2f;


            RectTransform rect =
                image.rectTransform;


            rect.anchorMin =
                new Vector2(
                    0.5f - halfWidth,
                    rect.anchorMin.y
                );

            rect.anchorMax =
                new Vector2(
                    0.5f + halfWidth,
                    rect.anchorMax.y
                );


            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;
        }

        private void SetBarColor(
            Image? image,
            Color new_color
        )
        {
            if (image == null)
                return;
            
            image.color = new_color;
        }

        // =========================================================
        // Build the complete UI
        // =========================================================

        private void BuildMeterUI()
        {
            if (
                meter == null ||
                meter.straightMeter == null
            )
            {
                throw new System.InvalidOperationException("The straight ErrorMeter Image is missing.");
            }


            // Predrop exposes the Image itself, rather than its GameObject.
            originalImage = meter.straightMeter;


            if (originalImage == null)
                return;

            ResetBandAnimation();
            originalImageEnabled = originalImage.enabled;


            // =========================
            // Remove the previous UI
            // =========================

            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(
                    uiRoot
                );
            }


            // =========================
            // Root
            // =========================

            uiRoot =
                new GameObject(
                    "AccurateJudgementBar_UI",
                    typeof(RectTransform)
                );


            uiRoot.transform.SetParent(
                meter.straightMeter.transform,
                false
            );


            RectTransform rootRect =
                uiRoot
                    .GetComponent<RectTransform>();


            rootRect.anchorMin =
                Vector2.zero;

            rootRect.anchorMax =
                Vector2.one;

            rootRect.offsetMin =
                Vector2.zero;

            rootRect.offsetMax =
                Vector2.zero;


            // =========================
            // Load the image
            // =========================




            if (layer3Sprite == null)
            {
                layer3Sprite =
                    LoadEmbeddedSprite(
                        "AccurateJudgementBar.straight_layer3.png"
                    );
            }


            // =========================
            // Layer order
            //
            // Later elements render above earlier elements.
            // =========================

            layer1Image = MeterBackgroundGraphic.Create(uiRoot.transform, false);


            bandImages.Clear();

            layer3Image =
                CreateFullImage(
                    uiRoot.transform,
                    "Layer3",
                    layer3Sprite
                );


            // =========================
            // Hide the original sprite
            //
            // Keep the GameObject
            // so its RectTransform and children remain unchanged.
            // =========================

            originalImage.enabled =
                false;
        }


        // =========================================================
        // Update the yellow and green band widths
        // =========================================================

        private void UpdateBars(float bpm, Difficulty difficulty)
        {
            if (bpm <= 0f || uiRoot == null) return;

            var bands = EvaluateAnimatedBands(bpm, difficulty);
            layer1Image?.RefreshBackground(bands.Count > 0 ? FailureJudgement.Progress : 0f);
            var active = new HashSet<string>();
            foreach (var band in bands)
            {
                active.Add(band.Id);
                if (!bandImages.TryGetValue(band.Id, out var image) || image == null)
                {
                    image = CreateBar(uiRoot.transform, band.Id, band.Color);
                    if (band.Id == JudgementApi.Blue) image.gameObject.AddComponent<XPerfectGradient>();
                    bandImages[band.Id] = image;
                }
                image.transform.SetAsLastSibling();
                SetBarWidth(image, band.Width);
                SetBarColor(image, band.Color);
            }
            foreach (var id in new List<string>(bandImages.Keys))
            {
                if (active.Contains(id)) continue;
                bandImages[id].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(bandImages[id].gameObject);
                bandImages.Remove(id);
            }
            layer3Image?.transform.SetAsLastSibling();
        }
        private void Update()
        {
            if (!Main.is_mod_enable || Time.unscaledTimeAsDouble < retryAfter) return;
            try { UpdateDisplay(); }
            catch (System.Exception ex)
            {
                DisplayErrors.Report("straight meter", ex);
                DisplayErrors.Run("straight meter cleanup", Cleanup);
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
            if (uiRoot == null || originalImage != meter.straightMeter)
            {
                Cleanup();
                BuildMeterUI();
                meterPosition = meter.GetComponent<MeterPosition>();
                if (meterPosition == null)
                {
                    meterPosition = meter.gameObject.AddComponent<MeterPosition>();
                }
                meterPosition.enabled = true;
                meterPosition.CaptureLayout(meter);
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
            DisplayErrors.Run("straight meter cleanup", Cleanup);
        }
        private void OnDestroy() => DisplayErrors.Run("straight meter cleanup", Cleanup);

        private void Cleanup()
        {
            // Restore vanilla first, including a failed or externally destroyed AJB UI.
            if (originalImage != null)
            {
                originalImage.enabled = originalImageEnabled;
                originalImage = null;
            }
            bandImages.Clear();
            layer1Image = null;
            layer3Image = null;
            ResetBandAnimation();
            if (meterPosition != null)
            {
                meterPosition.RestorePosition();
                meterPosition.enabled = false;
                meterPosition = null;
            }

            if (uiRoot != null)
            {
                uiRoot.SetActive(false);
                UnityEngine.Object.Destroy(
                    uiRoot
                );
                uiRoot = null;
            }


            // Release resources owned by AJB
            // that were created by LoadEmbeddedSprite.


            if (layer3Sprite is not null) {
                DestroySprite(
                    ref layer3Sprite
                );
            }
        }


        private void DestroySprite(
            ref Sprite? sprite
        )
        {
            if (sprite == null)
                return;


            Texture2D texture =
                sprite.texture;


            UnityEngine.Object.Destroy(
                sprite
            );


            if (texture != null)
            {
                UnityEngine.Object.Destroy(
                    texture
                );
            }


            sprite =
                null;
        }
    }
}





