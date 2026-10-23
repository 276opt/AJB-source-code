using System;
using System.Collections.Generic;
using AccurateJudgementBar;

// Minimal game doubles: exercise the production postfix without loading Unity.
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method, params Type[] arguments) {} }
    public class HarmonyPostfix : Attribute {}
    public class HarmonyPrefix : Attribute {}
}
public enum HitMarginLimit { None, PerfectsOnly, PurePerfectOnly }
public enum HitMargin { XPerfect, PerfectMinus, PerfectPlus, EarlyPerfect, LatePerfect, VeryEarly, VeryLate, TooEarly, TooLate, Multipress, FailMiss, FailOverload, Auto, Midspin, OverPress, FailedFloor }
public static class GCS { public static HitMarginLimit hitMarginLimit; public static float currentSpeedTrial; public static Difficulty difficulty; public static int checkpointNum; public static bool speedTrialMode, practiceMode; }
public static class HitMarginHelper
{
    // 3.4 HOTPATCH HitMarginHelper.IsCounted, including the non-player perfect grades.
    public static bool IsCounted(this HitMargin hit, HitMarginLimit limit) =>
        hit is HitMargin.XPerfect or HitMargin.PerfectMinus or HitMargin.PerfectPlus or HitMargin.Auto or HitMargin.Midspin or HitMargin.Multipress ||
        (limit != HitMarginLimit.PurePerfectOnly && hit is HitMargin.EarlyPerfect or HitMargin.LatePerfect) ||
        (limit == HitMarginLimit.None && hit is HitMargin.VeryEarly or HitMargin.VeryLate);
    public static string ToLocalized(this HitMargin hit, object? preset = null) => hit.ToString();
}
public static class RDString
{
    public static bool failMessage;
    public static string Get(string key, Dictionary<string, object>? parameters)
    {
        if (failMessage && parameters != null) throw new InvalidOperationException("Missing display string");
        return parameters == null ? key : $"No \"{parameters["judgement"]}\" allowed";
    }
}
public class scrFloor { public bool freeroam, isSafe, auto; public scrFloor? nextfloor; public double marginScale = 1; }
public static class RDC { public static bool debug, useOldAuto; }
public static class ADOBase { public static bool isExpo; }
public class scrLivesCounter { public static scrLivesCounter? instance; public int lives; }
public class scrController
{
    public static scrController? instance;
    public bool gameworld = true, noFail, noFailInfiniteMargin;
    public PauseMenu pauseMenu = new();
    public scrFloor? currFloor = new();
    public PlanetarySystem? planetarySystem = new();
}
public class scrPlayer
{
    public scrMarginTracker marginTracker = new();
    public scrFloor? currFloor = new scrFloor();
    public bool alive = true, protectedFromDeath, missCooldownActive, auto;
    public int deathCalls, syntheticHits;
    public Exception? deathError;
    public bool OnDamage() => false;
    public void Die(bool overload, bool multipress, string message, bool hitbox)
    {
        if (overload || multipress || hitbox || string.IsNullOrEmpty(message)) throw new Exception("Wrong death arguments");
        if (deathError != null) throw deathError;
        deathCalls++;
        if (protectedFromDeath) return;
        // Mirror the dangerous native branch so a missing guard cannot silently pass.
        if (scrController.instance!.noFail || (currFloor!.isSafe && GCS.hitMarginLimit == HitMarginLimit.None) ||
            missCooldownActive || (scrLivesCounter.instance?.lives ?? 0) > (ADOBase.isExpo ? 1 : 0))
        { syntheticHits++; return; }
        alive = false;
    }
}
public class scrMarginTracker
{
    public List<HitMargin> hitMargins = new();
    public void Reset() => hitMargins.Clear();
}
namespace AccurateJudgementBar
{
    public class Settings
    {
        public bool PerfectsOnlyInstantDeath = true, XPerfectOnly, NoMissOnly, NoCheckpoint;
        public bool ShowHitMarginLimit = true, EnableAccurateJudgementBar = true;
        public float AnimationDuration;
        public int saveCalls, saveAttempts;
        public Exception? saveError;
        public void Save(object entry)
        {
            saveAttempts++;
            if (saveError != null) throw saveError;
            saveCalls++;
        }
    }
    public static class Main
    {
        public static bool is_mod_enable;
        public static Settings settings = new Settings();
        public static UnityModManagerNet.UnityModManager.ModEntry Mod = new();
    }
}
class Program
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    static scrPlayer Reset()
    {
        AccurateJudgementBar.Main.is_mod_enable = true;
        AccurateJudgementBar.Main.settings = new Settings();
        GCS.hitMarginLimit = HitMarginLimit.PerfectsOnly;
        scrController.instance = new scrController();
        return new scrPlayer();
    }
    static void MainTest()
    {
        foreach (var hit in new[] { HitMargin.VeryEarly, HitMargin.VeryLate, HitMargin.TooEarly, HitMargin.TooLate })
        {
            var player = Reset(); bool result = false;
            PerfectsOnlyDeath.Postfix(player, hit, ref result);
            Check(!player.alive && result && player.deathCalls == 1, "Rejected hit must kill: " + hit);
            PerfectsOnlyDeath.Postfix(player, hit, ref result);
            Check(player.deathCalls == 1, "Do not kill twice");
        }
        foreach (var hit in new[] { HitMargin.XPerfect, HitMargin.EarlyPerfect, HitMargin.LatePerfect })
        {
            var player = Reset(); bool result = false;
            PerfectsOnlyDeath.Postfix(player, hit, ref result);
            Check(player.alive && !result && player.deathCalls == 0, "Allowed hit must survive: " + hit);
        }
        Action<scrPlayer>[] guards = {
            p => AccurateJudgementBar.Main.is_mod_enable = false,
            p => AccurateJudgementBar.Main.settings.PerfectsOnlyInstantDeath = false,
            p => GCS.hitMarginLimit = HitMarginLimit.None,
            p => GCS.hitMarginLimit = HitMarginLimit.PurePerfectOnly,
            p => scrController.instance!.noFail = true,
            p => scrController.instance!.gameworld = false,
            p => p.currFloor!.freeroam = true,
            p => p.currFloor = null,
            p => scrController.instance = null
        };
        foreach (var guard in guards)
        foreach (bool originalResult in new[] { false, true })
        {
            var player = Reset(); guard(player); bool result = originalResult;
            PerfectsOnlyDeath.Postfix(player, HitMargin.VeryEarly, ref result);
            Check(player.alive && player.deathCalls == 0 && result == originalResult, "Guard must preserve vanilla state");
        }
        foreach (bool originalResult in new[] { false, true })
        {
            var player = Reset(); player.protectedFromDeath = true; bool result = originalResult;
            PerfectsOnlyDeath.Postfix(player, HitMargin.VeryEarly, ref result);
            Check(player.alive && result == originalResult, "Declined death must preserve vanilla result");
        }
        Console.WriteLine($"PerfectsOnlyDeath: {checks} checks passed.");
    }
    static void Main() { AprilFools.LocalNow = () => new DateTime(2026, 4, 1); MainTest(); XPerfectOnlyTests.Run(); NoMissOnlyTests.Run(); AprilFoolsTests.Run(); TogetherCompatibilityTests.Run(); NoCheckpointTests.Run(); }
}
