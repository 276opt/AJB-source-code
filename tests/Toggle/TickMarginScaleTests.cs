using System;
using AccurateJudgementBar;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void TickDisplayCall(scrController controller, scrFloor departure,
        scrPlanet planet, Action body)
    {
        TickMarginScaleFix.MeterUpdatePatch.Prefix(controller, departure, planet, out var state);
        try { body(); }
        finally { TickMarginScaleFix.MeterUpdatePatch.Finalizer(state); }
    }

    private static float TickScale(scrHitErrorMeter meter, scrFloor departure, scrPlanet planet, float original)
    {
        TickMarginScaleFix.AddHitPatch.Prefix(meter, ref original, planet, departure);
        return original;
    }

    private static void TickMarginScaleTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Check(Mod.settings.FixTickMarginScale, "Tick correction must default to enabled");
        var meter = new scrHitErrorMeter();
        var controller = new scrController { errorMeter = meter };
        var planet = new scrPlanet();
        var target = new scrFloor { marginScale = 8, speed = 3 };
        var departure = new scrFloor { marginScale = 1, speed = 2, nextfloor = target };

        TickDisplayCall(controller, departure, planet, () =>
            Check(TickScale(meter, departure, planet, 1) == 8, "Default option must correct the target scale"));
        Mod.settings.FixTickMarginScale = false;
        TickDisplayCall(controller, departure, planet, () =>
            Check(TickScale(meter, departure, planet, 1) == 1, "Disabled option must preserve native scale"));
        Mod.settings.FixTickMarginScale = true;
        Check(TickScale(meter, departure, planet, 1) == 1,
            "Direct AddHit calls, including synthetic failure ticks, must retain their supplied scale");

        // Same native display path serves ordinary input, hold release, and either meter shape.
        foreach (double scale in new[] { 8.0, 100.0, 0.25, 1.0 })
        {
            target.marginScale = scale;
            TickDisplayCall(controller, departure, planet, () =>
            {
                float corrected = TickScale(meter, departure, planet, 1);
                Check(corrected == (float)scale, "Tick must use the target scale, including reduced and extreme windows");
                const float angle = -1.3962634f; // Early 80 degrees: XPerfect at 800%.
                meter.onAddHit = (actualAngle, margin, actualScale, actualPlanet, actualFloor) =>
                {
                    Check(actualAngle == angle && margin == HitMargin.XPerfect,
                        "Correction must leave the original angle and judgement untouched");
                    Check(ReferenceEquals(actualFloor, departure) && actualFloor.speed == 2 &&
                        ReferenceEquals(actualPlanet, planet) && actualScale == (float)scale,
                        "Departure speed source and planet must survive correction; only scale changes");
                };
                meter.AddHit(angle, HitMargin.XPerfect, corrected, planet, departure);
                Check(departure.marginScale == 1 && target.marginScale == scale && target.speed == 3,
                    "No gameplay floor fields may be changed");
            });
            Check(TickScale(meter, departure, planet, 1) == 1, "Display scope must end after each call");
        }
        meter.onAddHit = null;
        target.marginScale = 8;

        // Concurrent co-op players and reentrant display calls must never share a target's scale.
        var otherMeter = new scrHitErrorMeter();
        var otherController = new scrController { errorMeter = otherMeter };
        var otherPlanet = new scrPlanet();
        var otherFloor = new scrFloor { nextfloor = new scrFloor { marginScale = .5 } };
        TickDisplayCall(controller, departure, planet, () =>
        {
            Check(TickScale(otherMeter, departure, planet, 1) == 1, "Unrelated meters must remain untouched");
            Check(TickScale(meter, otherFloor, planet, 1) == 1, "Unrelated floors must remain untouched");
            Check(TickScale(meter, departure, otherPlanet, 1) == 1, "Unrelated planets must remain untouched");
            TickDisplayCall(otherController, otherFloor, otherPlanet, () =>
            {
                Check(TickScale(otherMeter, otherFloor, otherPlanet, 1) == .5f, "Nested player must use its own target");
                Check(TickScale(meter, departure, planet, 1) == 1, "Nested call must mask the outer scope");
            });
            Check(TickScale(meter, departure, planet, 1) == 8, "Nested cleanup must restore the outer scope");
            TickDisplayCall(controller, new scrFloor(), planet, () =>
                Check(TickScale(meter, departure, planet, 1) == 1, "Missing-target call must also mask outer scope"));
            Check(TickScale(meter, departure, planet, 1) == 8, "Invalid nested scope must clean up correctly");

            Mod.settings.FixTickMarginScale = false;
            Check(TickScale(meter, departure, planet, 1) == 1, "Switching option off must apply immediately");
            Mod.settings.FixTickMarginScale = true;
            entry.OnToggle(entry, false);
            Check(TickScale(meter, departure, planet, 1) == 1, "Disabling AJB must preserve native placement");
        });
        entry.OnToggle(entry, true);
        Check(Mod.settings.FixTickMarginScale, "Mod toggle must retain preference");
        Check(TickScale(meter, departure, planet, 1) == 1, "Re-enabling must not resurrect an expired scope");

        foreach (double invalid in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            target.marginScale = invalid;
            TickDisplayCall(controller, departure, planet, () =>
                Check(TickScale(meter, departure, planet, 1) == 1, "Invalid target multiplier must keep native display arguments"));
        }
        TickDisplayCall(controller, null!, planet, () =>
            Check(TickScale(meter, departure, planet, 1) == 1, "Missing departure must keep native arguments"));
        TickDisplayCall(controller, departure, null!, () =>
            Check(TickScale(meter, departure, planet, 1) == 1, "Missing planet must keep native arguments"));

        target.marginScale = 8;
        var originalException = new InvalidOperationException("Simulated original display failure");
        Exception? observed = null;
        try
        {
            TickDisplayCall(controller, departure, planet, () => throw originalException);
        }
        catch (Exception ex) { observed = ex; }
        Check(ReferenceEquals(observed, originalException), "Cleanup must preserve the original exception");
        Check(TickScale(meter, departure, planet, 1) == 1, "Exception cleanup must not leak the multiplier");
        TickDisplayCall(controller, departure, planet, () =>
            Check(TickScale(meter, departure, planet, 1) == 8, "Next attempt must still correct normally"));

        Mod.settings.FixTickMarginScale = false;
        entry.OnToggle(entry, false);
    }
}
