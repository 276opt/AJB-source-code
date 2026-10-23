using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace AccurateJudgementBar
{
    // Optional integration: Together is loaded by a bootstrap assembly, so its
    // Main type need not live in ModEntry.Assembly. Never require its DLL at load time.
    internal static class TogetherCompatibility
    {
        private static UnityModManager.ModEntry? boundEntry;
        private static FieldInfo? sessionField, entryField;
        private static bool wasInSession, reportedReadError, reportedSaveError;
        private static Settings? pendingSettings;
        private static DateTime nextSaveAttempt, nextMenuAttempt;
        internal static bool MenuResetPending { get; private set; }
        internal static Func<DateTime> UtcNow = () => DateTime.UtcNow;

        internal static bool InSession
        {
            get
            {
                try
                {
                    var mod = UnityModManager.FindMod("Together");
                    if (mod == null || !mod.Active) return false;
                    if (!ReferenceEquals(boundEntry, mod) || sessionField == null || entryField == null)
                    {
                        boundEntry = mod;
                        sessionField = entryField = null;
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            var type = assembly.GetType("Together.Main", false);
                            if (type == null) continue;
                            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                            var session = type.GetField("IsInTogether", flags);
                            var entry = type.GetField("ModEntry", flags);
                            if (session?.FieldType != typeof(bool) || entry == null ||
                                !ReferenceEquals(entry.GetValue(null), mod)) continue;
                            sessionField = session;
                            entryField = entry;
                            break;
                        }
                    }
                    // Both checks are live: a disabled mod or a replaced UMM entry
                    // must not retain the previous session's suppression.
                    return sessionField != null && entryField != null &&
                        ReferenceEquals(entryField.GetValue(null), mod) && (bool)sessionField.GetValue(null)!;
                }
                catch (Exception ex)
                {
                    if (!reportedReadError)
                    {
                        reportedReadError = true;
                        Report("Cannot read Together session state", ex);
                    }
                    return false;
                }
            }
        }

        internal static void Update()
        {
            if (!Main.is_mod_enable || Main.settings == null)
            {
                wasInSession = false;
                return;
            }
            if (!ReferenceEquals(pendingSettings, Main.settings)) pendingSettings = null;
            if (!InSession)
            {
                wasInSession = false;
                RetryPendingWork();
                return;
            }

            bool flagsChanged = MarginLimitState.SetExtraSelection(MarginLimitState.Mode.None);
            bool changed = flagsChanged || GCS.hitMarginLimit != HitMarginLimit.None ||
                Persistence.hitMarginLimit != HitMarginLimit.None;
            // Match Together's native reset, including a late AJB enable or a menu
            // callback after scene initialization. Do not restore limits on exit.
            if (Persistence.hitMarginLimit != HitMarginLimit.None) Persistence.hitMarginLimit = HitMarginLimit.None;
            GCS.hitMarginLimit = HitMarginLimit.None;
            if (flagsChanged)
            {
                pendingSettings = Main.settings;
                nextSaveAttempt = default;
            }
            if (!wasInSession || changed)
            {
                MenuResetPending = true;
                nextMenuAttempt = default;
            }
            wasInSession = true;
            RetryPendingWork();
        }

        private static void RetryPendingWork()
        {
            var now = UtcNow();
            if (pendingSettings != null && Main.Mod != null && now >= nextSaveAttempt)
            {
                nextSaveAttempt = now.AddSeconds(1);
                try
                {
                    pendingSettings.Save(Main.Mod);
                    pendingSettings = null;
                    reportedSaveError = false;
                }
                catch (Exception ex)
                {
                    if (!reportedSaveError)
                    {
                        reportedSaveError = true;
                        Report("Together limits were cleared in memory but AJB settings could not be saved; will retry", ex);
                    }
                }
            }
            if (MenuResetPending && now >= nextMenuAttempt)
            {
                nextMenuAttempt = now.AddSeconds(1);
                MenuResetPending = !DisplayErrors.TryRun("Together limit menu reset", MarginLimitMenu.ResetForTogether);
            }
        }

        private static void Report(string message, Exception error)
        {
            try { Main.Mod?.Logger.Error(message + ": " + error); }
            catch { } // Integration diagnostics must not become gameplay exceptions.
        }

        [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.Reset))]
        internal static class TrackerResetPatch
        {
            [HarmonyPrefix]
            internal static void Prefix()
            {
                // Reset snapshots IsCounted into the native statistics cache. Clear
                // Together limits before that snapshot, even before the next UMM frame.
                // The original Reset still performs all record and score bookkeeping.
                Update();
            }
        }
    }
}
