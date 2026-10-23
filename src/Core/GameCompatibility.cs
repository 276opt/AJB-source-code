using System;
using System.Reflection;

namespace AccurateJudgementBar
{
    internal static class GameCompatibility
    {
        private static readonly Version MinimumVersion = new Version(3, 4, 0);

        internal static bool IsSupported(string gameVersion, Type gameMath, Type errorMeter, Type constants,
            out string reason)
        {
            string requirement = "Accurate Judgement Bar requires ADOFAI 3.4.0 predrop (Image-based ErrorMeter) or newer. " +
                "Detected version: " + gameVersion + ". ";
            if (!Version.TryParse(gameVersion, out var version) || version.CompareTo(MinimumVersion) < 0)
            {
                reason = requirement + "Please update the game before enabling this mod.";
                return false;
            }

            // Original, HOTPATCH and predrop all identify as 3.4.0 / r150.
            // Check the display API on later versions too: a larger version number
            // cannot make an incompatible field signature safe to JIT.
            try
            {
                if (!HasField(errorMeter, "straightMeter", "UnityEngine.UI.Image") ||
                    !HasField(errorMeter, "curvedMeter", "UnityEngine.UI.Image") ||
                    !HasField(constants, "straightErrorMeterSprites", "UnityEngine.Sprite[]") ||
                    !HasField(constants, "curvedErrorMeterSprites", "UnityEngine.Sprite[]"))
                {
                    reason = requirement + "The required predrop display API was not detected. " +
                        "Please update the game or use an AJB release compatible with this game build.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                reason = requirement + "Could not verify the display API (" + ex.GetType().Name + ").";
                return false;
            }

            // Retain the timing-formula fingerprint for this same-version family.
            // Inspect metadata only; do not initialize game objects or run game code.
            if (version.Major == 3 && version.Minor == 4 && version.Build == 0)
            {
                try
                {
                    foreach (var method in gameMath.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
                    {
                        if (!method.Name.StartsWith("<GetAdjustedTimeBoundaries>g__MinCappedAngleToTime|", StringComparison.Ordinal))
                            continue;
                        var parameters = method.GetParameters();
                        if (method.ReturnType == typeof(double) && parameters.Length == 3 &&
                            parameters[0].ParameterType == typeof(double) && parameters[1].ParameterType == typeof(double) &&
                            parameters[2].ParameterType.IsByRef)
                        {
                            reason = string.Empty;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    reason = requirement + "Could not verify the XPerfect hotpatch (" + ex.GetType().Name + ").";
                    return false;
                }
                reason = requirement + "The XPerfect hotpatch was not detected. Please update the game.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool HasField(Type type, string name, string fieldType) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.FieldType.FullName == fieldType;
    }
}
