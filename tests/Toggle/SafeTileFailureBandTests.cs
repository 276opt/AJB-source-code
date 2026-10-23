using System;
using System.Reflection;
using AccurateJudgementBar;
using UnityEngine;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void SafeTileFailureBandTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.ShowFailureJudgement = true;
        Mod.settings.NoMissOnly = false;
        Mod.settings.AnimationDuration = .4f;
        GCS.hitMarginLimit = HitMarginLimit.None;
        var bands = new[] { new JudgementBand("red", 0, 316, new Color(1, 0, 0, 1)) };
        foreach (int mode in new[] { 1, 2, 3 })
        foreach (var shape in new[] { ADOFAI.ErrorMeterShape.Straight, ADOFAI.ErrorMeterShape.Curved })
        {
            FailureJudgement.Clear();
            Mod.settings.FailureBandDisplayMode = mode;
            var controller = scrController.instance = new scrController();
            var player = new scrPlayer();
            var planet = player.planetarySystem.chosenPlanet;
            var safeFloor = new scrFloor { isSafe = true };
            planet.currfloor = new scrFloor();
            ADOBase.playerManager = new scrPlayerManager { players = new[] { player } };
            Time.unscaledTimeAsDouble = 0;
            if (mode == 3) Record(player.marginTracker);
            Check(!FailureJudgement.Visible && FailureJudgement.AddBand(bands).Count == 1,
                "An unsafe floor must not trigger the NoFail condition");

            planet.currfloor = safeFloor;
            Check(FailureJudgement.Visible && FailureJudgement.Progress == 0,
                "Entering isSafe must start expansion without toggling NoFail");
            Time.unscaledTimeAsDouble = .2;
            Check(Math.Abs(FailureJudgement.Progress - .875f) < .00001,
                "Safe tile expansion must use the configured animation");
            Time.unscaledTimeAsDouble = .5;
            Check(FailureJudgement.AddBand(bands).Count == 2 && FailureJudgement.Progress == 1,
                "Both meter shapes must receive the fully expanded purple band");

            var tick = new ErrorMeterTick();
            Show(tick, HitMargin.FailMiss, out _);
            var follower = tick.gameObject.AddComponent<FailureTickFollower>();
            follower.Configure(tick, shape, 1, "safe-tile");
            safeFloor.isSafe = false;
            Check(!FailureJudgement.Visible && FailureJudgement.Progress == 1,
                "A live isSafe change must stop displaying failure ticks and begin retraction");
            typeof(FailureTickFollower).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(follower, null);
            Check(!follower.enabled && tick.tick.color.a == 0 && tick.tickOutline.color.a == 0,
                "Leaving safety must remove cached failure ticks for both shapes");
            Time.unscaledTimeAsDouble = .7;
            Check(Math.Abs(FailureJudgement.Progress - .125f) < .00001 && FailureJudgement.AddBand(bands).Count == 2,
                "Purple band and background progress must shrink together");
            Time.unscaledTimeAsDouble = 1;
            Check(FailureJudgement.Progress == 0 && FailureJudgement.AddBand(bands).Count == 1,
                "Retraction must remove the band completely");

            safeFloor.isSafe = true;
            Check(FailureJudgement.Visible && FailureJudgement.Progress == 0,
                "Re-entering safety must expand again with retained real failure history");
            Time.unscaledTimeAsDouble = 1.5;
            Check(FailureJudgement.Progress == 1, "Second safe section must finish expanding");
            controller.noFail = true;
            planet.currfloor = new scrFloor();
            Check(FailureJudgement.Visible && FailureJudgement.Progress == 1,
                "NoFail must keep the band expanded after leaving isSafe");
            controller.noFail = false;
            Record(player.marginTracker);
            bool retainsDeath = mode == 2;
            Check(FailureJudgement.Visible == retainsDeath,
                "Only OR mode keeps a recorded failure visible outside protection");
            Check(!controller.noFail && !controller.noFailInfiniteMargin && player.alive &&
                !player.missCooldownActive && player.marginTracker.hitMargins.Count == 0,
                "Display transitions must not write gameplay protection, judgement records or life state");
        }

        FailureJudgement.Clear();
        Mod.settings.FailureBandDisplayMode = 1;
        Mod.settings.AnimationDuration = 0;
        var current = scrController.instance = new scrController();
        foreach (var manager in new scrPlayerManager?[] { null, new(), new() { players = Array.Empty<scrPlayer>() },
            new() { players = new scrPlayer[] { null! } },
            new() { players = new[] { new scrPlayer { planetarySystem = null! } } },
            new() { players = new[] { new scrPlayer { planetarySystem = new() { chosenPlanet = null! } } } },
            new() { players = new[] { new scrPlayer { planetarySystem = new() { chosenPlanet = new() { currfloor = null } } } } } })
        {
            ADOBase.playerManager = manager;
            Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0,
                "Scene setup/teardown must tolerate missing players, planets and floors");
            current.noFail = true;
            Check(FailureJudgement.Visible && FailureJudgement.Progress == 1,
                "NoFail remains effective without initialized floors");
            current.noFail = false;
        }

        var safePlayer = new scrPlayer();
        safePlayer.planetarySystem.chosenPlanet.currfloor!.isSafe = true;
        ADOBase.playerManager = new scrPlayerManager { players = new[] { safePlayer } };
        Check(FailureJudgement.Progress == 1, "Safe tile must display before disable");
        entry.OnToggle(entry, false);
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "Disabling AJB must clear safety display immediately");
        entry.OnToggle(entry, true);
        Check(FailureJudgement.Visible && FailureJudgement.Progress == 1, "Re-enable must read current safety");
        Mod.settings.ShowFailureJudgement = false;
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "Parent option off must clear immediately");
        Mod.settings.ShowFailureJudgement = true;
        scrController.instance = null;
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "Scene exit must hide even if old safe floors remain");
        scrController.instance = new scrController();
        ADOBase.playerManager = null;
        Check(!FailureJudgement.Visible && FailureJudgement.Progress == 0, "New scene must not retain safety state");
        Mod.settings.FailureBandDisplayMode = 0;
        entry.OnToggle(entry, false);
    }
}
