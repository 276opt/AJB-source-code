namespace AccurateJudgementBar
{
    // AJB modes never become native enum values. Keep existing XML fields for compatibility.
    internal static class MarginLimitState
    {
        internal enum Mode { None, NoCheckpoint, NoMissOnly, PerfectsOnly, PurePerfectOnly, XPerfectOnly }

        internal static bool Enabled => Main.is_mod_enable && Main.settings != null && !TogetherCompatibility.InSession;
        internal static Mode Current => ReadEnabled(GCS.hitMarginLimit);
        // Used to reconcile an old menu snapshot during disable.
        internal static Mode Stored => Read(GCS.hitMarginLimit, Main.settings.XPerfectOnly,
            Main.settings.NoMissOnly, Main.settings.NoCheckpoint);

        internal static Mode Read(HitMarginLimit limit, bool xPerfectOnly, bool noMissOnly,
            bool noCheckpoint = false) => limit switch
        {
            HitMarginLimit.PurePerfectOnly => xPerfectOnly ? Mode.XPerfectOnly : Mode.PurePerfectOnly,
            HitMarginLimit.PerfectsOnly => Mode.PerfectsOnly,
            HitMarginLimit.None => noMissOnly ? Mode.NoMissOnly :
                noCheckpoint ? Mode.NoCheckpoint : Mode.None,
            _ => Mode.None
        };

        internal static Mode ReadEnabled(HitMarginLimit limit)
        {
            bool enabled = Enabled;
            return Read(limit, enabled && Main.settings.XPerfectOnly, enabled && Main.settings.NoMissOnly,
                enabled && Main.settings.NoCheckpoint);
        }

        internal static bool IsSelected(Mode mode) => Enabled && Current == mode;

        internal static HitMarginLimit ToNative(Mode mode) => mode switch
        {
            Mode.PurePerfectOnly or Mode.XPerfectOnly => HitMarginLimit.PurePerfectOnly,
            Mode.PerfectsOnly => HitMarginLimit.PerfectsOnly,
            _ => HitMarginLimit.None
        };

        // Returns whether serialized preferences changed; the caller owns save/retry policy.
        internal static bool SetExtraSelection(Mode mode)
        {
            bool xPerfectOnly = mode == Mode.XPerfectOnly;
            bool noMissOnly = mode == Mode.NoMissOnly;
            bool noCheckpoint = mode == Mode.NoCheckpoint;
            bool changed = Main.settings.XPerfectOnly != xPerfectOnly ||
                Main.settings.NoMissOnly != noMissOnly || Main.settings.NoCheckpoint != noCheckpoint;
            Main.settings.XPerfectOnly = xPerfectOnly;
            Main.settings.NoMissOnly = noMissOnly;
            Main.settings.NoCheckpoint = noCheckpoint;
            return changed;
        }

        internal static void Apply(Mode mode)
        {
            if (TogetherCompatibility.InSession || TogetherCompatibility.MenuResetPending) mode = Mode.None;
            Persistence.hitMarginLimit = GCS.hitMarginLimit = ToNative(mode);
            bool changed = SetExtraSelection(mode);
            // The official menu does not invoke UMM's OnSaveGUI.
            if (changed && Main.Mod != null) Main.settings.Save(Main.Mod);
        }
    }
}
