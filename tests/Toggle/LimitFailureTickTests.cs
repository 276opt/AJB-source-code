using System;
using AccurateJudgementBar;
using UnityEngine;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static Color ExpectedLimitColor(HitMargin margin) => margin switch
    {
        HitMargin.VeryEarly or HitMargin.VeryLate => MeterWatcher.GetOrange(),
        HitMargin.EarlyPerfect or HitMargin.LatePerfect => MeterWatcher.GetYellow(),
        HitMargin.PerfectMinus or HitMargin.PerfectPlus => MeterWatcher.GetGreen(),
        _ => MeterWatcher.GetRed()
    };

    private static ErrorMeterTick DrawLimitTick(HitMargin recorded, HitMargin original, ADOFAI.ErrorMeterShape shape, float angle)
    {
        var planet = new scrPlanet();
        object?[] draw = { angle, recorded, planet, 0, null };
        Check((bool)Patch("DrawPatch", "Prefix", draw)! && (float)draw[0]! == angle,
            "Limit death must draw at the native input position, never the outer failure band");
        var tick = new ErrorMeterTick();
        object?[] show = { tick, recorded, new Color(0, 0, 0, 1), "limit" };
        Check((bool)Patch("ColorPatch", "Prefix", show)!, "Limit death must remain visible even when outer failure band is hidden");
        Color color = (Color)show[2]!;
        Check(color.Equals(ExpectedLimitColor(original)), "Use the matching meter getter, overriding native judgement and co-op colors");
        Check((HitMargin)show[1]! == original, "Restore the original rejected grade for display only");
        tick.tick.color = color;
        Patch("DrawPatch", "Postfix", new[] { tick }, new[] { "limit" }, shape, draw[4]);
        Check(tick.GetComponent<FailureTickFollower>()?.enabled != true, "Limit death ticks must not follow the outer band");
        return tick;
    }

    private static void LimitFailureTicks(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        scrConductor.instance = new scrConductor();
        foreach (var mode in new[] { 3, 0, 1, 2 })
        foreach (bool showLimit in new[] { false, true })
        foreach (bool showFailure in new[] { false, true })
        foreach (var shape in new[] { ADOFAI.ErrorMeterShape.Straight, ADOFAI.ErrorMeterShape.Curved })
        {
            GCS.hitMarginLimit = mode == 3 ? HitMarginLimit.None : mode == 0 ? HitMarginLimit.PerfectsOnly : HitMarginLimit.PurePerfectOnly;
            Mod.settings.XPerfectOnly = mode == 2;
            Mod.settings.NoMissOnly = mode == 3;
            Mod.settings.ShowHitMarginLimit = showLimit;
            Mod.settings.ShowFailureJudgement = showFailure;
            var rejected = mode switch
            {
                3 => new[] { HitMargin.TooEarly, HitMargin.TooLate },
                0 => new[] { HitMargin.TooEarly, HitMargin.VeryEarly, HitMargin.VeryLate, HitMargin.TooLate },
                1 => new[] { HitMargin.EarlyPerfect, HitMargin.LatePerfect },
                _ => new[] { HitMargin.PerfectMinus, HitMargin.PerfectPlus }
            };
            foreach (var margin in rejected)
            {
                FailureJudgement.Clear();
                var meter = new scrHitErrorMeter();
                scrController.instance = new scrController { errorMeter = meter };
                var player = new scrPlayer();
                Patch("HitPatch", "Prefix", player);
                try
                {
                    Patch("DamagePatch", "Prefix", player, margin);
                    object?[] death = { player, null };
                    Patch("DeathPatch", "Prefix", death);
                    player.alive = false;
                    Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
                    Patch("DamagePatch", "Finalizer");
                    Check(meter.addedHits == 0, "An input death must defer to the native UpdateHitErrorMeter call");
                    var recorded = mode == 0 ? HitMargin.FailOverload : margin;
                    Patch("MarginPatch", "Postfix", player.marginTracker, recorded);
                    var tick = DrawLimitTick(recorded, margin, shape, margin.ToString().Contains("Early") ? 42f : -27f);
                    Check(!FailureJudgement.Visible, "Fixing the tick must not turn on the outer failure band");
                    // Reusing the same pooled tick for a normal hit must remove custom tracking.
                    Patch("HitPatch", "Finalizer");
                    Check(Show(tick, HitMargin.Perfect, out var normal) && normal == null, "Following ordinary hits retain native colors");
                    FailureJudgement.Clear();
                    Check(tick.tick.color.a == 1, "Cleanup must not erase a tick reused by a normal hit");
                }
                finally { Patch("HitPatch", "Finalizer"); Patch("DamagePatch", "Finalizer"); }
            }
        }

        // Damage can be nested (NoFail re-enters Hit); another player's damage must not leak.
        FailureJudgement.Clear();
        var first = new scrPlayer();
        var second = new scrPlayer();
        Patch("HitPatch", "Prefix", first);
        Patch("DamagePatch", "Prefix", first, HitMargin.PerfectPlus);
        Patch("DamagePatch", "Prefix", second, HitMargin.VeryEarly);
        Patch("DamagePatch", "Finalizer");
        object?[] state = { first, null };
        Patch("DeathPatch", "Prefix", state);
        first.alive = false;
        Patch("DeathPatch", "Postfix", first, false, false, false, state[1]);
        Patch("DamagePatch", "Finalizer");
        var displayed = DrawLimitTick(HitMargin.PerfectPlus, HitMargin.PerfectPlus, ADOFAI.ErrorMeterShape.Straight, 12f);
        Patch("HitPatch", "Finalizer");
        entry.OnToggle(entry, false);
        Check(displayed.tick.color.a == 0, "Disabling clears the custom limit tick");
        Check(Show(displayed, HitMargin.PerfectPlus, out var restored) && restored == null, "Disabled mod restores native color");
        entry.OnToggle(entry, true);

        // Missed-tile deaths have no Hit scope. Capture angle/floor before Die changes them.
        foreach (bool ccw in new[] { false, true })
        {
            FailureJudgement.Clear();
            GCS.hitMarginLimit = HitMarginLimit.PurePerfectOnly;
            Mod.settings.ShowFailureJudgement = false;
            var meter = new scrHitErrorMeter();
            scrController.instance = new scrController { errorMeter = meter };
            var player = new scrPlayer();
            var planet = player.planetarySystem.chosenPlanet;
            var floor = player.currFloor;
            planet.angle = 2;
            planet.targetExitAngle = 1.25;
            floor.isCCW = ccw;
            floor.marginScale = 1.5f;
            ErrorMeterTick? missed = null;
            meter.onAddHit = (angle, margin, scale, capturedPlanet, capturedFloor) =>
            {
                Check(angle == (ccw ? -.75f : .75f) && scale == 1.5f && ReferenceEquals(floor, capturedFloor) &&
                    ReferenceEquals(planet, capturedPlanet), "Synthetic miss uses captured angle, direction, floor and scale");
                missed = DrawLimitTick(margin, HitMargin.TooLate, ADOFAI.ErrorMeterShape.Curved, angle);
            };
            object?[] death = { player, null };
            Patch("DeathPatch", "Prefix", death);
            planet.angle = 10;
            player.currFloor = new scrFloor();
            player.alive = false;
            Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
            Check(meter.addedHits == 1 && missed != null, "Missed-tile death draws once without a Hit callback");
            FailureJudgement.Clear();
            Check(missed!.tick.color.a == 0, "Tracker replacement/cleanup clears synthetic limit ticks");

            // Protected death and an existing native failure must not add extra ticks.
            player.alive = true;
            Patch("DeathPatch", "Prefix", death);
            Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
            Check(meter.addedHits == 1, "A declined Die call must not fabricate a failure");
            Patch("DeathPatch", "Prefix", death);
            Show(new ErrorMeterTick(), HitMargin.FailMiss, out _);
            player.alive = false;
            Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
            Check(meter.addedHits == 1, "Existing native limit failure prevents a duplicate synthetic tick");
        }
        foreach (var guard in new Action<scrPlayer>[] {
            p => scrController.instance!.gameworld = false,
            p => scrController.instance!.errorMeter = null,
            p => Persistence.hitErrorMeterSize = ADOFAI.ErrorMeterSize.Off,
            p => scrConductor.instance = null,
            p => p.currFloor = null!,
            p => p.planetarySystem.chosenPlanet = null!,
            p => Mod.is_mod_enable = false
        })
        {
            FailureJudgement.Clear();
            var meter = new scrHitErrorMeter();
            scrController.instance = new scrController { errorMeter = meter };
            scrConductor.instance = new scrConductor();
            Persistence.hitErrorMeterSize = ADOFAI.ErrorMeterSize.Normal;
            Mod.is_mod_enable = true;
            var player = new scrPlayer();
            guard(player);
            object?[] death = { player, null };
            Patch("DeathPatch", "Prefix", death);
            player.alive = false;
            Patch("DeathPatch", "Postfix", player, false, false, false, death[1]);
            Check(meter.addedHits == 0, "Missing context, disabled meter/mod and non-game scenes must not synthesize a tick");
        }
        Mod.is_mod_enable = true;
        Persistence.hitErrorMeterSize = ADOFAI.ErrorMeterSize.Normal;
        GCS.hitMarginLimit = HitMarginLimit.None;
        Mod.settings.ShowHitMarginLimit = true;
        Mod.settings.XPerfectOnly = false;
        Mod.settings.ShowFailureJudgement = true;
        FailureJudgement.Clear();
        entry.OnToggle(entry, false);
    }
}
