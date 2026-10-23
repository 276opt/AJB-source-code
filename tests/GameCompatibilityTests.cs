using System;
using AccurateJudgementBar;

internal static class GameCompatibilityTests
{
    private static int gameInitializations;
    private struct Capture { public double margin; }

    private class PredropMeter
    {
        static PredropMeter() { gameInitializations++; }
        public UnityEngine.UI.Image straightMeter = null!, curvedMeter = null!;
    }
    private class LegacyMeter { public object straightMeter = null!, curvedMeter = null!; }
    private class PartialMeter { public UnityEngine.UI.Image straightMeter = null!; }
    private class StaticMeter { public static UnityEngine.UI.Image straightMeter = null!, curvedMeter = null!; }
    private class PredropConstants
    {
        static PredropConstants() { gameInitializations++; }
        public UnityEngine.Sprite[] straightErrorMeterSprites = null!, curvedErrorMeterSprites = null!;
    }
    private class WrongConstants { public object[] straightErrorMeterSprites = null!, curvedErrorMeterSprites = null!; }

    private static bool Supported(string version, Type math, out string reason,
        Type? meter = null, Type? constants = null) =>
        GameCompatibility.IsSupported(version, math, meter ?? typeof(PredropMeter),
            constants ?? typeof(PredropConstants), out reason);

    // Local functions reproduce the old/new compiler-generated metadata without executing game code.
    private static class BeforeHotpatch
    {
        private static double GetAdjustedTimeBoundaries()
        {
            var capture = new Capture { margin = 1 };
            return MinCappedAngleToTime(0, 12.5, true, ref capture);
            static double MinCappedAngleToTime(double min, double angle, bool useAbsoluteTime, ref Capture c) =>
                Math.Max(min, useAbsoluteTime ? angle : angle * c.margin);
        }
    }

    private static class Hotpatch
    {
        static Hotpatch() { gameInitializations++; }
        private static double GetAdjustedTimeBoundaries()
        {
            var capture = new Capture { margin = 1 };
            return MinCappedAngleToTime(0, 12.5, ref capture);
            static double MinCappedAngleToTime(double min, double angle, ref Capture c) => Math.Max(min, angle * c.margin);
        }
    }

    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception("Game compatibility: " + message); }
        foreach (string version in new[] { "2.9.0", "3.3.1", "3.3.99", "", "unknown", "3.4.0-beta" })
        {
            Check(!Supported(version, typeof(Hotpatch), out var reason), "reject old/unrecognized version: " + version);
            Check(reason.Contains("3.4.0") && reason.Contains("update"), "actionable failure message");
        }
        Check(!Supported("3.4.0", typeof(BeforeHotpatch), out var oldReason), "reject original 3.4.0");
        Check(oldReason.Contains("hotpatch was not detected"), "explain same-version hotpatch requirement");
        Check(!Supported("3.4.0", typeof(string), out _), "reject unrecognized 3.4.0 metadata");
        Check(Supported("3.4.0", typeof(Hotpatch), out var supportedReason), "accept predrop 3.4.0");
        Check(supportedReason == string.Empty, "supported version has no error");
        Check(!Supported("3.4.0.0", typeof(BeforeHotpatch), out _), "four-part version must not bypass hotpatch check");
        Check(Supported("3.4.0.0", typeof(Hotpatch), out _), "four-part predrop version accepted");
        foreach (string version in new[] { "3.4.0", "3.4.0.0", "3.4.1", "4.0.0" })
        {
            foreach (Type meter in new[] { typeof(LegacyMeter), typeof(PartialMeter), typeof(StaticMeter) })
            {
                Check(!Supported(version, typeof(Hotpatch), out var reason, meter), "reject incompatible meter: " + version);
                Check(reason.Contains("predrop display API") && reason.Contains("update"), "explain display API requirement");
            }
            Check(!Supported(version, typeof(Hotpatch), out _, constants: typeof(string)), "require sprite arrays");
            Check(!Supported(version, typeof(Hotpatch), out _, constants: typeof(WrongConstants)), "require Sprite[] fields");
        }
        foreach (string version in new[] { "3.4.1", "3.5.0", "4.0.0" })
            Check(Supported(version, typeof(string), out _), "later compatible versions do not depend on compiler-generated helper name");
        Check(gameInitializations == 0, "compatibility check does not initialize or run game code");
        Console.WriteLine($"Passed {checks} game-version compatibility checks.");
    }
}

// Metadata doubles only; production checks never instantiate Unity objects.
namespace UnityEngine { public class Sprite { } }
namespace UnityEngine.UI { public class Image { } }
