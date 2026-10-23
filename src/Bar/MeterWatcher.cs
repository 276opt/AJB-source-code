
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI.Editor.Actions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityModManagerNet;


namespace AccurateJudgementBar 
{
    public class MeterWatcher : MonoBehaviour
    {
        private readonly JudgementBandAnimation bandAnimation = new JudgementBandAnimation();

        public scrHitErrorMeter? meter;

        public float searchTimer = 0f;

        protected void ResetBandAnimation() => bandAnimation.Reset();

        protected IReadOnlyList<JudgementBand> EvaluateAnimatedBands(float bpm, Difficulty difficulty)
        {
            var context = CreateJudgementContext(bpm, difficulty);
            var targets = JudgementApi.Evaluate(context, Main.settings.EnableAccurateJudgementBar);
            var bands = bandAnimation.Sample(targets, Time.unscaledTimeAsDouble, Main.settings.AnimationDuration);
            // The failure border already has its own animation and follows the displayed outer width.
            return FailureJudgement.AddBand(bands);
        }

        private static readonly Color Death = 
            new Color32(
                204,
                119,
                249,
                255
            );

        private static readonly Color RedDeath = 
            new Color32(
                157,
                55,
                212,
                255
            );


        private static readonly Color Red =
            new Color32(
                255,
                0,
                0,
                255
            );

        private static readonly Color Orange =
            new Color32(
                255,
                111,
                77,
                255
            );

        private static readonly Color Yellow =
            new Color32(
                252,
                255,
                77,
                255
            );

        private static readonly Color Green =
            new Color32(
                95,
                255,
                78,
                255
            );

        public static Color GetRed()
        {
            if (
                (
                    NoMissOnly.Active ||
                    GCS.hitMarginLimit == HitMarginLimit.PerfectsOnly ||
                    GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly
                ) && Main.settings.ShowHitMarginLimit
            ) {
                return RedDeath;
            }
            return Red;
        }

        public static Color GetOrange()
        {
            if (
                (
                    GCS.hitMarginLimit == HitMarginLimit.PerfectsOnly ||
                    GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly
                ) && Main.settings.ShowHitMarginLimit
            ) {
                return Death;
            }
            return Orange;
        }

        public static Color GetYellow()
        {
            if (
                GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly &&
                Main.settings.ShowHitMarginLimit
            ) {
                return Death;
            }
            return Yellow;
        }

        public static Color GetGreen()
        {
            if (XPerfectOnly.Active && Main.settings.ShowHitMarginLimit)
                return Death;
            return Green;
        }

        public static Color GetBlue()
        {
            return NativeXPerfect.Color;
        }

        protected JudgementContext CreateJudgementContext(float bpm, Difficulty difficulty)
        {
            float pitch = scrConductor.instance != null && scrConductor.instance.song != null
                ? scrConductor.instance.song.pitch : 1f;
            float marginScale = (float)(scrController.instance?.currFloor?.marginScale ?? 1.0);
            return new JudgementContext(bpm, difficulty, GetOfficalSpeedTrail(), pitch, marginScale);
        }

        public static float GetOfficalSpeedTrail()
        {
            scrController controller = scrController.instance;

            float speedTrial =
                GCS.currentSpeedTrial > 0f
                    ? GCS.currentSpeedTrial
                    : 1f;
            
            return speedTrial;
        }

        public float GetWindowBpm()
        {
            scrConductor conductor = scrConductor.instance;
            scrController controller = scrController.instance;

            if (conductor == null || controller == null)
                return -1f;

            float pitch =
                conductor.song != null
                    ? conductor.song.pitch
                    : 1f;

            float planetSpeed = 1f;

            if (controller.planetarySystem != null)
            {
                planetSpeed =
                    (float)controller
                        .planetarySystem
                        .speed;
            }

            float marginScale =
                (float)(controller.currFloor?.marginScale ?? 1);

            float bpm =
                (float)conductor.bpm
                * pitch
                * planetSpeed;
            float speedTrial = GetOfficalSpeedTrail();

            bpm /= marginScale;
            bpm /= speedTrial;
            // no speed trial effect in offical level

            return bpm;
        }

        public static float CalcMs(
            float bpm,
            float pangle,
            float baseWindowMs,
            float hardMinimumMs = 25f
        )
        {

            if (bpm <= 0f)
                return -1f;

            return new JudgementContext(bpm, GCS.difficulty, GetOfficalSpeedTrail())
                .CalculateWindow(pangle, baseWindowMs, hardMinimumMs);
        }
        public static float DiffToMinimum(
            Difficulty difficulty
        )
        {
            switch (difficulty)
            {
                case Difficulty.Lenient:
                    return 91f;

                case Difficulty.Normal:
                    return 65f;

                case Difficulty.Strict:
                    return 40f;

                default:
                    return 65f;
            }
        }

        // Legacy helpers retained for callers; the renderers no longer clamp their input BPM.
        public float DiffToMinimumBPM(
            Difficulty difficulty
        )
        {
            // Native XPerfect has a maximum time window, so it keeps shrinking
            // relative to the outer band at low BPM. Do not clamp that motion.
            if (NativeXPerfect.Enabled) return 0f;
            float minimumBpm;

            switch (difficulty)
            {
                case Difficulty.Lenient:
                    minimumBpm = 219f;
                    break;

                case Difficulty.Normal:
                    minimumBpm = 307f;
                    break;

                case Difficulty.Strict:
                    minimumBpm = 400f;
                    break;

                default:
                    minimumBpm = 307f;
                    break;
            }

            return minimumBpm / GetOfficalSpeedTrail();
        }

        public float DiffToMaximumBPM(
            Difficulty difficulty
        )
        {
            if (NativeXPerfect.Enabled) return float.MaxValue;
            float maximumbpm = 500f;
            return maximumbpm;
        }

        public static bool haveMod(string mod_name) {

            var mod = UnityModManager.FindMod(mod_name);

            if (mod == null) {
                return false;
            }
            else {
                return mod.Active;
            }
        }
    }
}
