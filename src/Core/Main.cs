using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using HarmonyLib;

namespace AccurateJudgementBar
{
    public static class Main
    {
        public static UnityModManager.ModEntry? Mod
        {
            get;
            private set;
        }

        public static Settings settings
        {
            get;
            private set;
        } = null!;


        private static GameObject? straight_watcherObject;

        private static GameObject? curve_watcherObject;

        private static OverloadBar? overload_bar;

        private static bool overload_bar_enable = false;

        public static bool is_mod_enable = false;

        public static Harmony? harmony;

        // Read the selected mode only; hiding the UI never changes gameplay or saved choices.
        private static bool ShouldShowOverloadBar => is_mod_enable && settings.EnableOverloadHP &&
            (!settings.HideOverloadHPWithMarginLimit ||
                MarginLimitState.Current is MarginLimitState.Mode.None or MarginLimitState.Mode.NoCheckpoint);

        public static void checkOverloadBar()
        {
            enableOverloadBar();
            disableOverloadBar();
        }

        public static void enableOverloadBar(bool force = false)
        {
            if (!ShouldShowOverloadBar)
                return;
            if (force) disableOverloadBar(true);
            if (overload_bar == null) {
                overload_bar_enable = true;
                overload_bar = OverloadBar.Create(
                    new Vector2(
                        settings.overloadHPX,
                        settings.overloadHPY
                    ),
                    new Vector2(
                        320f,
                        26f
                    )
                );
            }
        }
        
        public static void disableOverloadBar(bool force = false)
        {
            if (force || !ShouldShowOverloadBar) {
                overload_bar_enable = false;
                overload_bar?.DestroyBar();
                overload_bar = null;
            }
        }


        public static bool Load(
            UnityModManager.ModEntry modEntry
        )
        {
            try { return LoadCore(modEntry); }
            catch (System.Exception ex)
            {
                // A failed initialization must not leave partially installed AJB patches behind.
                harmony?.UnpatchAll(modEntry.Info.Id);
                modEntry.Logger.Error("AJB initialization failed: " + ex);
                return false;
            }
        }

        private static bool LoadCore(UnityModManager.ModEntry modEntry)
        {

            Mod = modEntry;
            if (!GameCompatibility.IsSupported(Application.version, typeof(scrMisc), typeof(scrHitErrorMeter),
                typeof(RDConstants), out string incompatibility))
            {
                modEntry.Logger.Error(incompatibility);
                return false;
            }

            settings = Settings.Load(modEntry);
            Localization.Initialize(modEntry.Path, settings.Language, message => modEntry.Logger.Error(message));

            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            MethodInfo[] methods =
                typeof(scrHitErrorMeter).GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            foreach (MethodInfo method in methods)
            {
                if (method.Name != "UpdateLayout")
                    continue;

                var info = Harmony.GetPatchInfo(method);

                if (info == null)
                {
                    continue;
                }

                foreach (var patch in info.Postfixes)
                {
                    if (patch.owner == "AccurateJudgementBar")
                    {
                        continue;
                    }
                    modEntry.Logger.Log(
                        $"  Postfix owner={patch.owner}, " +
                        $"method={patch.PatchMethod.DeclaringType?.FullName}." +
                        $"{patch.PatchMethod.Name}"
                    );
                }
            }

            modEntry.OnToggle = OnToggle;
            modEntry.OnUpdate = OnUpdate;
            modEntry.OnGUI = settings.OnGUI;
            modEntry.OnSaveGUI = settings.OnSaveGUI;

            AprilFools.Initialize(modEntry);

            modEntry.Logger.Log(
                "Accurate Judgement Bar loaded (ADOFAI " + Application.version + "; predrop Image ErrorMeter API verified)"
            );

            return true;
        }


        private static bool OnToggle(
            UnityModManager.ModEntry modEntry,
            bool enabled
        )
        {

            is_mod_enable = enabled;
            if (!enabled) StartingBpmWarning.CancelPending();
            TogetherCompatibility.Update();
            
            if (enabled) {
                DisplayErrors.Reset();
                AprilFools.Update();
                if (EnableMod(modEntry)) return true;
                is_mod_enable = false;
                try { DisableMod(modEntry); }
                finally { AprilFools.Disable(); }
                return false;
            }

            try { DisableMod(modEntry); }
            finally { AprilFools.Disable(); }

            return true;
        }


        private static void OnUpdate(
            UnityModManager.ModEntry modEntry,
            float timedelta
        )
        {
            TogetherCompatibility.Update();
            checkOverloadBar();
            StartingBpmWarning.Update();
            if (!is_mod_enable) return;
            AprilFools.Update();
            CompletionResultsText.Update();
            if (overload_bar_enable) {
                overload_bar?.SetPosition(
                    new Vector2(
                        settings.overloadHPX,
                        settings.overloadHPY
                    )
                );
                overload_bar?.Refresh();
            }
        }

        public static void SettingsUpdate()
        {
            StartingBpmWarning.Update();
            TogetherCompatibility.Update();
            checkOverloadBar();
            if (!FailureJudgement.Allowed) FailureJudgement.Clear();
            BetterHit.Update();
            XPerfectResultsColor.Refresh();
            MarginLimitMenu.Refresh();
            ErrorMeterSizeSettings.Refresh();
        }

        private static bool EnableMod(
            UnityModManager.ModEntry modEntry
        )
        {
            // Avoid creating duplicate watcher objects.
            if (straight_watcherObject != null)
            {
                return true;
            }
            if (curve_watcherObject != null)
            {
                return true;
            }

            FailureJudgement.Clear();

            straight_watcherObject =
                new GameObject(
                    "StraightAccurateJudgementBarWatcher"
                );


            StraightMeterWatcher straight_watcher =
                straight_watcherObject.AddComponent<
                    StraightMeterWatcher
                >();

            curve_watcherObject =
                new GameObject(
                    "CurveAccurateJudgementBarWatcher"
                );

            CurvedMeterWatcher curve_watcher = 
                curve_watcherObject.AddComponent<
                    CurvedMeterWatcher
                >();


            if (straight_watcherObject == null)
            {
                modEntry.Logger.Error(
                    "Failed to create StraightMeterWatcher"
                );

                Object.Destroy(
                    curve_watcherObject
                );

                curve_watcherObject = null;

                return false;
            }

            if (curve_watcherObject == null)
            {
                modEntry.Logger.Error(
                    "Failed to create CurveMeterWatcher"
                );

                Object.Destroy(
                    straight_watcherObject
                );

                straight_watcherObject = null;

                return false;
            }


            Object.DontDestroyOnLoad(
                straight_watcherObject
            );
            Object.DontDestroyOnLoad(
                curve_watcherObject
            );

            SettingsUpdate();

            return true;

        }


        private static void DisableMod(
            UnityModManager.ModEntry modEntry
        )
        {
            if (straight_watcherObject != null)
            {
                straight_watcherObject.SetActive(false);
                Object.Destroy(
                    straight_watcherObject
                );

                straight_watcherObject = null;
            }
            if (curve_watcherObject != null)
            {
                curve_watcherObject.SetActive(false);
                Object.Destroy(
                    curve_watcherObject
                );

                curve_watcherObject = null;
            }


            modEntry.Logger.Log(
                "Accurate Judgement Bar unloaded"
            );

            disableOverloadBar(true);
            FailureJudgement.Clear();
            BetterHit.Reversed();
            XPerfectResultsColor.Refresh();
            MarginLimitMenu.Refresh();
            ErrorMeterSizeSettings.Refresh();

        }
    }
}
