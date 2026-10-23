using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using ADOFAI;

namespace AccurateJudgementBar
{
    internal static class FailureJudgement
    {
        internal static readonly Color Purple = new Color32(204, 119, 249, 255);
        private static readonly FailureBandAnimation animation = new FailureBandAnimation();
        private static readonly HashSet<scrMarginTracker> failedTrackers = new HashSet<scrMarginTracker>();
        private static scrController? lastController;
        private static readonly Dictionary<ErrorMeterTick, (string TweenId, bool Custom)> displayedHits =
            new Dictionary<ErrorMeterTick, (string TweenId, bool Custom)>();
        internal static float InnerWidth = 316f;
        internal const float ExtensionWidth = 16f; // 8 px per side, like the default red band.
        internal static bool Allowed => Main.is_mod_enable && Main.settings != null &&
            Main.settings.ShowFailureJudgement && !NoMissOnly.Active &&
            GCS.hitMarginLimit != HitMarginLimit.PerfectsOnly &&
            GCS.hitMarginLimit != HitMarginLimit.PurePerfectOnly;

        internal static float Progress
        {
            get
            {
                var controller = scrController.instance;
                if (controller != lastController)
                {
                    lastController = controller;
                    failedTrackers.Clear();
                    animation.Reset();
                    InnerWidth = 316f;
                }

                bool allowed = Allowed && controller != null;
                return animation.Sample(allowed, allowed && Triggered,
                    Time.unscaledTimeAsDouble, Main.settings?.AnimationDuration ?? 0f);
            }
        }

        private static bool NoFailOrSafeTile
        {
            get
            {
                var controller = scrController.instance;
                if (controller == null) return false;
                if (controller.noFail) return true;
                // Match the native Die protection, without including cooldown / extra lives.
                if (GCS.hitMarginLimit != HitMarginLimit.None) return false;

                // The shared meter follows player one, just like controller.currFloor.
                // Its native getter assumes initialized players/planets, so guard scene setup/teardown.
                var manager = ADOBase.playerManager;
                if (manager == null || manager.players == null || manager.players.Length == 0) return false;
                var player = manager.players[0];
                if (player == null || player.planetarySystem == null) return false;
                var planet = player.planetarySystem.chosenPlanet;
                return planet != null && planet.currfloor != null && planet.currfloor.isSafe;
            }
        }

        private static bool Triggered
        {
            get
            {
                // Read live protection each time: safe sections can begin/end mid-attempt.
                // Keep actual failure history independent of that temporary protection.
                bool failed = failedTrackers.Count > 0;
                switch (Main.settings?.FailureBandDisplayMode ?? 0)
                {
                    case 1: return NoFailOrSafeTile;
                    case 2: return failed || NoFailOrSafeTile;
                    case 3: return failed && NoFailOrSafeTile;
                    case 4: return true;
                    default: return failed;
                }
            }
        }
        internal static bool Visible => Allowed && scrController.instance != null && Triggered;

        internal static void Clear()
        {
            FailureTickFollower.ClearAll();
            foreach (var entry in new List<KeyValuePair<ErrorMeterTick, (string TweenId, bool Custom)>>(displayedHits))
            {
                // While disabled, settings callbacks must leave the game's new hits alone.
                if (entry.Key != null && !Main.is_mod_enable && !entry.Value.Custom) continue;
                if (entry.Key != null) HideTick(entry.Key, entry.Value.TweenId);
                displayedHits.Remove(entry.Key!); // Unity's destroyed objects still have managed dictionary keys.
            }
            failedTrackers.Clear();
            animation.Reset();
            lastController = null;
            InnerWidth = 316f;
            scopes.Clear();
            damageScopes.Clear();
            syntheticFailure = false;
        }

        private static void RecordFailure(scrMarginTracker tracker)
        {
            if (!Main.is_mod_enable) return;
            try
            {
                _ = Progress; // Reset stale scene state before recording this attempt's failure.
                failedTrackers.Add(tracker);
                _ = Progress; // Begin the animation before this hit's tick is drawn.
            }
            catch (Exception ex) { DisplayErrors.Report("Failure display record", ex); }
        }

        [HarmonyPatch(typeof(scrMistakesManager), "SetPlayerCount")]
        private static class TrackerReplacementPatch
        {
            // Entering the editor can reuse scrController while replacing every tracker.
            // Those old managed trackers will never receive Reset/RevertToLastCheckpoint.
            [HarmonyPostfix]
            private static void Postfix() => Clear();
        }

        [HarmonyPatch]
        private static class AttemptResetPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(scrMarginTracker), "Reset");
                yield return AccessTools.Method(typeof(scrMarginTracker), "RevertToLastCheckpoint");
            }
            [HarmonyPostfix]
            private static void Postfix(scrMarginTracker __instance)
            {
                failedTrackers.Remove(__instance);
                if (failedTrackers.Count == 0) animation.Reset();
            }
        }
        internal static IReadOnlyList<JudgementBand> AddBand(IReadOnlyList<JudgementBand> bands)
        {
            // Extend outside all registered windows; this also respects API-modified red widths.
            InnerWidth = bands.Count > 0 ? 0f : 316f;
            foreach (var band in bands)
                InnerWidth = Mathf.Max(InnerWidth, band.Width);
            float progress = Progress;
            if (progress <= 0f || bands.Count == 0) return bands;
            var result = new List<JudgementBand>(bands.Count + 1)
            {
                new JudgementBand("AccurateJudgementBar.failure", 0f,
                    InnerWidth + ExtensionWidth * progress, Purple)
            };
            result.AddRange(bands);
            return result;
        }

        // A scoped stack is necessary: NoFail Die() can call Hit() recursively.
        internal sealed class HitScope
        {
            internal readonly scrPlayer Player;
            internal bool Failed;
            internal HitMargin? FailureMargin;
            internal HitScope(scrPlayer player) { Player = player; }
        }
        private static readonly Stack<HitScope> scopes = new Stack<HitScope>();
        private static readonly Stack<(scrPlayer Player, HitMargin Margin)> damageScopes =
            new Stack<(scrPlayer Player, HitMargin Margin)>();
        private static bool syntheticFailure;
        private static ulong failureDisplayCount;
        private static bool IsFailure => syntheticFailure || (scopes.Count > 0 && scopes.Peek().Failed);
        private static bool IsFailureHit(HitMargin hit) =>
            hit == HitMargin.FailMiss || hit == HitMargin.FailOverload || (Main.is_mod_enable && IsFailure);

        private static bool LimitActive => Main.is_mod_enable &&
            (NoMissOnly.Active || GCS.hitMarginLimit == HitMarginLimit.PerfectsOnly || GCS.hitMarginLimit == HitMarginLimit.PurePerfectOnly);

        private static Color LimitColor(HitMargin hit)
        {
            switch (hit)
            {
                case HitMargin.VeryEarly:
                case HitMargin.VeryLate: return MeterWatcher.GetOrange();
                case HitMargin.EarlyPerfect:
                case HitMargin.LatePerfect: return MeterWatcher.GetYellow();
                case HitMargin.PerfectMinus:
                case HitMargin.PerfectPlus: return MeterWatcher.GetGreen();
                case HitMargin.XPerfect: return MeterWatcher.GetBlue();
                default: return MeterWatcher.GetRed();
            }
        }

        [HarmonyPatch(typeof(scrPlayer), "OnDamage")]
        private static class DamagePatch
        {
            // PerfectsOnly can record FailOverload after Die even though the rejected
            // input was VeryEarly/VeryLate. Capture the judgement before that conversion.
            // Keep it through all OnDamage postfixes (including AJB's instant death).
            [HarmonyPrefix]
            private static void Prefix(scrPlayer __instance, HitMargin hitMargin) =>
                damageScopes.Push((__instance, hitMargin));
            [HarmonyFinalizer]
            private static void Finalizer() { if (damageScopes.Count > 0) damageScopes.Pop(); }
        }

        internal static bool HideTick(ErrorMeterTick tick, string tweenId)
        {
            // Each cleanup step is independent, including the native tween's OnKill callback.
            bool hidden = DisplayErrors.TryRun("Failure tick stop tween", () => DOTween.Kill(tweenId, false));
            hidden &= DisplayErrors.TryRun("Failure tick stop outline tween", () => DOTween.Kill(tweenId + "o", false));
            hidden &= DisplayErrors.TryRun("Failure tick hide image", () => { if (tick != null && tick.tick != null) tick.tick.color = Color.clear; });
            hidden &= DisplayErrors.TryRun("Failure tick hide outline", () => { if (tick != null && tick.tickOutline != null) tick.tickOutline.color = Color.clear; });
            hidden &= DisplayErrors.TryRun("Failure tick stop follower", () =>
            {
                if (tick == null) return;
                var follower = tick.GetComponent<FailureTickFollower>();
                if (follower != null) follower.enabled = false;
            });
            return hidden;
        }

        [HarmonyPatch(typeof(scrPlayer), "Hit")]
        private static class HitPatch
        {
            [HarmonyPrefix]
            private static void Prefix(scrPlayer __instance) => scopes.Push(new HitScope(__instance));
            [HarmonyFinalizer]
            private static void Finalizer() { if (scopes.Count > 0) scopes.Pop(); }
        }

        [HarmonyPatch(typeof(scrMarginTracker), "AddHit")]
        private static class MarginPatch
        {
            [HarmonyPostfix]
            private static void Postfix(scrMarginTracker __instance, HitMargin hit)
            {
                try
                {
                    if (hit == HitMargin.FailMiss || hit == HitMargin.FailOverload) RecordFailure(__instance);
                    if (scopes.Count == 0 || scopes.Peek().Player.marginTracker != __instance) return;
                    if (hit == HitMargin.FailMiss || hit == HitMargin.FailOverload) scopes.Peek().Failed = true;
                }
                catch (Exception ex) { DisplayErrors.Report("Failure display margin", ex); }
            }
        }

        [HarmonyPatch(typeof(scrPlayer), "Die")]
        private static class DeathPatch
        {
            private struct DeathState
            {
                internal bool Alive;
                internal ulong DisplayCount;
                internal HitMargin? Margin;
                internal float Angle;
                internal scrFloor? Floor;
                internal scrPlanet? Planet;
            }
            [HarmonyPrefix]
            private static void Prefix(scrPlayer __instance, out DeathState __state)
            {
                __state = default;
                try { __state = Capture(__instance); }
                catch (Exception ex) { DisplayErrors.Report("Failure display death snapshot", ex); }
            }

            private static DeathState Capture(scrPlayer player)
            {
                var floor = player.currFloor;
                var planet = player.planetarySystem?.chosenPlanet;
                return new DeathState
                {
                    Alive = player.alive,
                    DisplayCount = failureDisplayCount,
                    Margin = damageScopes.Count > 0 && damageScopes.Peek().Player == player
                        ? damageScopes.Peek().Margin : (HitMargin?)null,
                    Floor = floor,
                    Planet = planet,
                    // Death outside Hit (e.g. a missed tile) has no fresh cachedAngle.
                    Angle = planet != null && floor != null
                        ? (float)(planet.angle - planet.targetExitAngle) * (floor.isCCW ? -1f : 1f) : 0f
                };
            }
            [HarmonyPostfix]
            private static void Postfix(scrPlayer __instance, bool overload, bool multipress, bool hitbox, DeathState __state)
            {
                // The original Die has already returned. Guard only AJB's extra display request.
                try { Display(__instance, overload, multipress, hitbox, __state); }
                catch (Exception ex) { DisplayErrors.Report("Failure display death tick", ex); }
            }

            private static void Display(scrPlayer __instance, bool overload, bool multipress, bool hitbox, DeathState __state)
            {
                if (!Main.is_mod_enable || !__state.Alive || __instance.alive) return;
                RecordFailure(__instance.marginTracker);
                if (multipress || hitbox) return;
                if (scopes.Count > 0 && scopes.Peek().Player == __instance)
                {
                    scopes.Peek().Failed = true;
                    scopes.Peek().FailureMargin = __state.Margin;
                    return;
                }
                // 3.4 can already draw a failure during Die; it was converted to an AJB hit above.
                if (failureDisplayCount != __state.DisplayCount) return;
                var controller = scrController.instance;
                if ((!Visible && !LimitActive) || controller == null || !controller.gameworld || controller.errorMeter == null ||
                    Persistence.hitErrorMeterSize == ErrorMeterSize.Off || scrConductor.instance == null ||
                    scrConductor.instance.song == null || __state.Floor == null ||
                    (LimitActive && __state.Planet == null)) return;
                // Supply a tick only for a death path which did not already display one.
                bool previousSyntheticFailure = syntheticFailure;
                syntheticFailure = true;
                try
                {
                    controller.errorMeter.AddHit(LimitActive ? __state.Angle : (overload ? -Mathf.PI : Mathf.PI),
                        LimitActive ? (__state.Margin ?? (overload ? HitMargin.TooEarly : HitMargin.TooLate))
                            : (overload ? HitMargin.FailOverload : HitMargin.FailMiss),
                        (float)__state.Floor.marginScale, __state.Planet!, __state.Floor);
                }
                finally { syntheticFailure = previousSyntheticFailure; }
            }
        }

        [HarmonyPatch(typeof(ErrorMeterTick), "Show")]
        private static class ColorPatch
        {
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static bool Prefix(ErrorMeterTick __instance, ref HitMargin hitMargin, ref Color? overrideColor, string tweenId)
            {
                var originalMargin = hitMargin;
                var originalColor = overrideColor;
                try { return Apply(__instance, ref hitMargin, ref overrideColor, tweenId); }
                catch (Exception ex)
                {
                    // Fall back to the caller's display arguments, not a half-applied AJB grade/color.
                    hitMargin = originalMargin;
                    overrideColor = originalColor;
                    if (__instance != null) displayedHits.Remove(__instance);
                    DisplayErrors.Report("Failure tick color", ex);
                    return true;
                }
            }

            private static bool Apply(ErrorMeterTick __instance, ref HitMargin hitMargin, ref Color? overrideColor, string tweenId)
            {
                displayedHits.Remove(__instance); // A pooled tick may now represent a normal hit.
                var follower = __instance.GetComponent<FailureTickFollower>();
                if (follower != null) follower.enabled = false;
                if (!IsFailureHit(hitMargin)) return true;
                if (Main.is_mod_enable && !Visible && !LimitActive)
                {
                    // Failed cleanup must let the original Show redraw the tick normally.
                    return !HideTick(__instance, tweenId);
                }
                // Track native hits too, so enabling AJB can clear an existing native display.
                foreach (var tick in new List<ErrorMeterTick>(displayedHits.Keys))
                    if (tick == null) displayedHits.Remove(tick!);
                displayedHits[__instance] = (tweenId, Main.is_mod_enable);
                if (!Main.is_mod_enable) return true;
                failureDisplayCount++;
                if (LimitActive)
                {
                    if (!syntheticFailure && scopes.Count > 0 && scopes.Peek().FailureMargin.HasValue)
                        hitMargin = scopes.Peek().FailureMargin!.Value;
                    overrideColor = LimitColor(hitMargin);
                    return true;
                }
                overrideColor = Purple;
                // This argument only controls this tick's display. A NoFail hit can
                // carry XPerfect, whose white outline must not decorate a failure.
                if (hitMargin != HitMargin.FailOverload) hitMargin = HitMargin.FailMiss;
                return true;
            }
        }

        [HarmonyPatch]
        private static class DrawPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(scrHitErrorMeter), "DrawStraightTick");
                yield return AccessTools.Method(typeof(scrHitErrorMeter), "DrawCurvedTick");
            }
            internal struct DrawState { internal int Index; internal bool Failed; internal float Sign; internal bool Skipped; }
            [HarmonyPrefix, HarmonyPriority(Priority.Last)]
            private static bool Prefix(ref float angle, HitMargin hitMargin, ref scrPlanet planet, int ___tickIndex, out DrawState __state)
            {
                float originalAngle = angle;
                var originalPlanet = planet;
                try { return Prepare(ref angle, hitMargin, ref planet, ___tickIndex, out __state); }
                catch (Exception ex)
                {
                    angle = originalAngle;
                    planet = originalPlanet;
                    __state = new DrawState { Skipped = true };
                    DisplayErrors.Report("Failure tick position", ex);
                    return true; // Run the game's draw; only our follower postfix is skipped.
                }
            }

            private static bool Prepare(ref float angle, HitMargin hitMargin, ref scrPlanet planet, int ___tickIndex, out DrawState __state)
            {
                // Limit failures belong at the actual input position inside the meter,
                // independently of the optional outer miss/overload band.
                bool failed = Main.is_mod_enable && IsFailureHit(hitMargin) && !LimitActive;
                float sign = angle < 0f ? -1f : 1f;
                __state = new DrawState { Index = ___tickIndex, Failed = failed, Sign = sign, Skipped = failed && !Visible };
                // Stop before the game consumes a cached tick or starts a native failure tween.
                if (__state.Skipped) return false;
                if (!failed) return true;
                angle = sign * (InnerWidth + ExtensionWidth * Progress * 0.5f) / 5f;
                planet = null!; // Use failure color even in co-op instead of the player's planet color.
                return true;
            }
            [HarmonyPostfix]
            private static void Postfix(ErrorMeterTick[] ___cachedTicks, string[] ___cachedTweenIds, ErrorMeterShape ___meterShape, DrawState __state)
            {
                if (__state.Skipped) return;
                FailureTickFollower? follower = null;
                try
                {
                    var tick = ___cachedTicks[__state.Index];
                    if (tick == null) return;
                    follower = tick.GetComponent<FailureTickFollower>();
                    if (!__state.Failed)
                    {
                        if (follower != null) follower.enabled = false;
                        return;
                    }
                    if (follower == null) follower = tick.gameObject.AddComponent<FailureTickFollower>();
                    follower.Configure(tick, ___meterShape, __state.Sign, ___cachedTweenIds[__state.Index]);
                }
                catch (Exception ex)
                {
                    DisplayErrors.Report("Failure tick follower", ex);
                    DisplayErrors.Run("Failure tick follower recovery", () => { if (follower != null) follower.enabled = false; });
                }
            }
        }

        [HarmonyPatch(typeof(scrHitErrorMeter), "Reset")]
        private static class ResetPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ErrorMeterTick[] ___cachedTicks)
            {
                if (___cachedTicks == null) return;
                foreach (var tick in ___cachedTicks)
                {
                    DisplayErrors.Run("Failure tick reset", () =>
                    {
                        if (tick == null) return;
                        var follower = tick.GetComponent<FailureTickFollower>();
                        if (follower != null) follower.enabled = false;
                    });
                }

            }
        }
    }

    // Follow the growing band while the original game's color tween handles tick lifetime.
    public sealed class FailureTickFollower : MonoBehaviour
    {
        private static readonly HashSet<FailureTickFollower> instances = new HashSet<FailureTickFollower>();
        private ErrorMeterTick tick = null!;
        private ErrorMeterShape shape;
        private float sign;
        private string tweenId = string.Empty;
        internal void Configure(ErrorMeterTick target, ErrorMeterShape meterShape, float direction, string id)
        { instances.Add(this); tick = target; shape = meterShape; sign = direction; tweenId = id; enabled = true; LateUpdate(); }

        internal static void ClearAll()
        {
            // Inactive meter objects do not receive LateUpdate, but still own visible cached ticks.
            foreach (var follower in new List<FailureTickFollower>(instances))
                DisplayErrors.Run("Failure follower cleanup", () =>
                {
                    if (follower != null && follower.enabled) follower.ClearTick();
                });
        }

        private void ClearTick()
        {
            enabled = false;
            FailureJudgement.HideTick(tick, tweenId);
        }

        private void OnDestroy() => instances.Remove(this);

        private void LateUpdate()
        {
            try { Follow(); }
            catch (Exception ex)
            {
                DisplayErrors.Report("Failure tick follow", ex);
                DisplayErrors.Run("Failure tick follow recovery", () => enabled = false);
            }
        }

        private void Follow()
        {
            if (!FailureJudgement.Visible)
            {
                ClearTick();
                return;
            }
            if (tick == null || tick.tick.color.a <= 0f) { enabled = false; return; }
            float angle = sign * (FailureJudgement.InnerWidth + FailureJudgement.ExtensionWidth * FailureJudgement.Progress * 0.5f) / 5f;
            if (shape == ErrorMeterShape.Curved)
                tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            else
                tick.rectTransform.anchoredPosition = new Vector2(-angle * 2.5f, -62f);
        }
    }
}




