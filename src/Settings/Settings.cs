using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityModManagerNet;

namespace AccurateJudgementBar
{
    public class Settings : UnityModManager.ModSettings
    {
        private const float DefaultAnimationDuration = 0.4f;
        private const float DefaultBackgroundOpacity = 0.6f;
        private const float DefaultCustomErrorMeterScale = 1f;
        private const float DefaultSensitivityOfHit = 0.2f;
        private const float DefaultLifeOfHit = 3f;

        public string Language = "en";

        public float AnimationDuration = DefaultAnimationDuration;

        public float BackgroundOpacity = DefaultBackgroundOpacity;

        public bool EnableAccurateJudgementBar = true;

        // Native presets remain exclusively in Persistence.hitErrorMeterSize.
        public bool CustomErrorMeterSize = false;
        public float CustomErrorMeterScale = DefaultCustomErrorMeterScale;

        public bool EnableOverloadHP = false;

        // Missing fields in existing Settings.xml also retain this default.
        public bool HideOverloadHPWithMarginLimit = true;

        public bool ShowHitMarginLimit = true;

        public bool ShowXPerfectJudgement = true;

        // Existing Settings.xml files without these fields retain AJB appearance.
        public bool VanillaXPerfectStyle = false;
        public bool VanillaXPerfectOutline = true;

        // -1 = Off; preserve saved values: 0 = Thin, 1 = Normal (+0.75 UI units inward), 2 = Thick (+1.5).
        // Applies only to AJB style; Vanilla style keeps this saved preference unchanged.
        public int XPerfectOutlineWidth = 1;

        public bool ShowAllDifficulties = true;

        // Missing fields in existing Settings.xml also retain this default.
        public bool ShowAllDifficultiesExcludeOfficial = true;

        // Enabled for new/missing settings; an explicitly saved false remains respected.
        public bool PerfectsOnlyInstantDeath = true;

        // Editor warning only; missing fields in existing Settings.xml retain this default.
        public bool WarnOnStartingBpmChange = true;

        // Selected through the game's judgement-limit menu; the game still saves PurePerfectOnly.
        public bool XPerfectOnly = false;

        // Selected through the same menu; the game still saves None.
        public bool NoMissOnly = false;

        // Standalone checkpoint restriction; native judgement rules remain None.
        public bool NoCheckpoint = false;

        public bool BlueXPerfectResults = true;

        // Missing fields in existing Settings.xml files retain these defaults.
        public bool EnchantedDetailedResults = true;
        public bool DetailedResultsSignedPerfects = true;
        public bool DetailedResultsBackground = true;
        public bool DetailedResultsDefaultColors = true;

        // Enabled for new/missing settings; an explicitly saved false remains respected.
        public bool FixTickMarginScale = true;

        public bool ShowFailureJudgement = true;

        // 0 = death, 1 = No Fail / safe tile, 2 = death OR protection, 3 = death AND protection, 4 = always.
        // XmlSerializer ignores the removed AlwaysShowFailureJudgement element;
        // old settings without this selection use the new default (after death).
        public int FailureBandDisplayMode = 0;

        public Vector2i Position = new Vector2i(0, 0);

        public float BarPositionX = 0f;
        public float BarPositionY = 0f;
        private string barPositionXText = "0";
        private string barPositionYText = "0";

        public float overloadHPX = 0f;
        public float overloadHPY = -500f;

        private string overloadHPXText = "0";
        private string overloadHPYText = "-500";

        public float SensitivityOfHit = DefaultSensitivityOfHit;

        public float LifeOfHit = DefaultLifeOfHit;
        public bool UseLegacyHit = false;
        // XmlSerializer ignores the removed PlainWhiteXPerfectHit element in old
        // Settings.xml files; tick appearance now follows the game's own setting.

        private void Label(string text)
        {
            GUILayout.Label(text);
        }


        // UI state only; private fields are not serialized to Settings.xml.
        private bool barExpanded;
        private bool overloadExpanded;
        private bool judgementBarExpanded;
        private bool extraJudgementExpanded;
        private bool detailedResultsExpanded;
        private bool languageExpanded;
        private GUIStyle? foldoutStyle;
        private GUIStyle? hitLifeWarningStyle;
        private readonly Dictionary<string, SliderInput> sliderInputs = new Dictionary<string, SliderInput>();

        private sealed class SliderInput
        {
            internal string Text = "";
            internal float Value;
        }

        private bool Section(string text, ref bool expanded, ref bool enabled)
        {
            bool wasEnabled = enabled;
            DrawSectionHeader(text, ref expanded, true, ref enabled);
            if (enabled && !wasEnabled)
                expanded = true;
            return expanded && enabled;
        }

        private bool Section(string text, ref bool expanded)
        {
            bool enabled = true;
            DrawSectionHeader(text, ref expanded, false, ref enabled);
            return expanded;
        }

        private void DrawSectionHeader(string text, ref bool expanded,
            bool hasToggle, ref bool enabled)
        {
            foldoutStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft
            };
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded && enabled ? "◢" : "▶",
                foldoutStyle, GUILayout.Width(20f)))
                expanded = !expanded;
            if (hasToggle)
                enabled = GUILayout.Toggle(enabled, text);
            else if (GUILayout.Button(text, foldoutStyle))
                expanded = !expanded;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void SectionContent(System.Action draw)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            GUILayout.BeginVertical();
            try
            {
                draw();
            }
            finally
            {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }


        private bool Toggle(
            bool value,
            string text
        )
        {
            return GUILayout.Toggle(
                value,
                text
            );
        }


        private float Slider(
            string id,
            string label,
            float value,
            float min,
            float max,
            float defaultValue,
            out bool edited,
            float step = 0.01f,
            float width = 300f,
            string format = "F2"
        )
        {
            edited = false;
            // Keep partial input (such as "0.") while editing, and synchronize when
            // a preset or another settings view changes the underlying value.
            if (!sliderInputs.TryGetValue(id, out var input) || input.Value != value)
            {
                input = new SliderInput { Text = value.ToString(format), Value = value };
                sliderInputs[id] = input;
            }

            string controlName = "AJB.Slider." + id;
            bool submit = GUI.GetNameOfFocusedControl() == controlName &&
                Event.current.type == EventType.KeyDown &&
                (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.ExpandWidth(false));
            GUI.SetNextControlName(controlName);
            string text = GUILayout.TextField(input.Text, GUILayout.Width(80f));
            if (text != input.Text)
            {
                input.Text = text;
                // Do not interpret a decimal separator as a thousands separator.
                bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out float number) ||
                    float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
                if (parsed && !float.IsNaN(number) && !float.IsInfinity(number))
                {
                    value = RoundSliderValue(number, min, max, step);
                    edited = true;
                }
            }

            if (GUILayout.Button(Localization.Current.Get("Default"), GUILayout.ExpandWidth(false)))
            {
                value = defaultValue;
                edited = true;
                input.Text = value.ToString(format);
                if (GUI.GetNameOfFocusedControl() == controlName) GUI.FocusControl(null);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (submit)
            {
                GUI.FocusControl(null);
                Event.current.Use();
            }
            if (GUI.GetNameOfFocusedControl() != controlName)
                input.Text = value.ToString(format);

            float next = GUILayout.HorizontalSlider(
                value,
                min,
                max,
                GUILayout.Width(width)
            );

            if (next != value)
            {
                value = RoundSliderValue(next, min, max, step);
                edited = true;
                input.Text = value.ToString(format);
            }

            input.Value = value;
            return value;
        }

        private static float RoundSliderValue(float value, float min, float max, float step)
        {
            return Mathf.Clamp(Mathf.Round(Mathf.Clamp(value, min, max) / step) * step, min, max);
        }


        private float FloatField(
            string label,
            ref string text,
            float value,
            float labelWidth = 50f,
            float fieldWidth = 100f
        )
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(
                label,
                GUILayout.Width(labelWidth)
            );

            text = GUILayout.TextField(
                text,
                GUILayout.Width(fieldWidth)
            );

            GUILayout.EndHorizontal();

            if (float.TryParse(text, out float result) &&
                !float.IsNaN(result) && !float.IsInfinity(result))
            {
                return result;
            }

            return value;
        }

        public void OnGUI(
            UnityModManager.ModEntry modEntry
        )
        {
            Label(AprilFools.SettingsTitle(Localization.Current.Get("Title")));
            DrawLanguageSettings();

            if (Section(Localization.Current.Get("BarSettings"), ref barExpanded))
                SectionContent(DrawBarSettings);

            if (Section(Localization.Current.Get("OverloadSettings"), ref overloadExpanded,
                ref EnableOverloadHP))
                SectionContent(DrawOverloadSettings);

            if (Section(Localization.Current.Get("TickSettings"), ref judgementBarExpanded))
                SectionContent(DrawJudgementBarSettings);

            if (Section(Localization.Current.Get("ExtraJudgementSettings"), ref extraJudgementExpanded))
                SectionContent(DrawExtraJudgementSettings);
        }

        private void DrawLanguageSettings()
        {
            var localization = Localization.Current;
            string[] names = localization.LanguageNames;
            int selected = System.Array.IndexOf(localization.LanguageIds, localization.SelectedLanguage);
            string currentName = selected >= 0 ? names[selected] : localization.SelectedLanguage;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(localization.Get("Language") + ": " + currentName +
                (languageExpanded ? " ▲" : " ▼"), GUILayout.Width(320f)))
                languageExpanded = !languageExpanded;
            if (GUILayout.Button(localization.Get("ReloadLanguages"), GUILayout.ExpandWidth(false)))
            {
                localization.Reload(Language);
                languageExpanded = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (languageExpanded)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(320f));
                for (int i = 0; i < names.Length; i++)
                {
                    if (GUILayout.Button((i == selected ? "✓ " : "") + names[i]))
                    {
                        localization.Select(localization.LanguageIds[i]);
                        Language = localization.SelectedLanguage;
                        languageExpanded = false;
                    }
                }
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            if (localization.HasLoadErrors)
                Label(localization.Get("LanguageLoadError"));
        }

        private void DrawBarSettings()
        {
            EnableAccurateJudgementBar = Toggle(
                EnableAccurateJudgementBar,
                Localization.Current.Get("EnableBarWidth")
            );

            GUILayout.Space(10f);

            AnimationDuration = Slider(
                nameof(AnimationDuration),
                Localization.Current.Get("AnimationDuration"),
                AnimationDuration,
                0f,
                1f,
                DefaultAnimationDuration,
                out _
            );

            GUILayout.Space(10f);

            ShowHitMarginLimit = Toggle(
                ShowHitMarginLimit,
                Localization.Current.Get("ShowHitMarginLimit")
            );

            ShowFailureJudgement = Toggle(
                ShowFailureJudgement,
                Localization.Current.Get("ShowFailureJudgement")
            );

            if (ShowFailureJudgement)
                SectionContent(() =>
                {
                    if (FailureBandDisplayMode < 0 || FailureBandDisplayMode > 4)
                        FailureBandDisplayMode = 0;
                    FailureBandDisplayMode = GUILayout.SelectionGrid(FailureBandDisplayMode, new[]
                    {
                        Localization.Current.Get("FailureBandAfterDeath"),
                        Localization.Current.Get("FailureBandNoFail"),
                        Localization.Current.Get("FailureBandDeathOrNoFail"),
                        Localization.Current.Get("FailureBandDeathAndNoFail"),
                        Localization.Current.Get("FailureBandAlways")
                    }, 1);
                });

            ShowXPerfectJudgement = Toggle(
                ShowXPerfectJudgement,
                Localization.Current.Get("ShowXPerfectJudgement")
            );

            Label(Localization.Current.Get("XPerfectStyle"));
            VanillaXPerfectStyle = GUILayout.SelectionGrid(VanillaXPerfectStyle ? 1 : 0, new[]
            {
                Localization.Current.Get("XPerfectAJBStyle"),
                Localization.Current.Get("XPerfectVanillaStyle")
            }, 2, GUILayout.Width(400f)) == 1;

            if (VanillaXPerfectStyle)
            {
                VanillaXPerfectOutline = Toggle(
                    VanillaXPerfectOutline,
                    Localization.Current.Get("VanillaXPerfectOutline")
                );
            }
            else
            {
                Label(Localization.Current.Get("XPerfectOutlineWidth"));
                XPerfectOutlineWidth = GUILayout.SelectionGrid(XPerfectOutlineWidth + 1, new[]
                {
                    Localization.Current.Get("XPerfectOutlineOff"),
                    Localization.Current.Get("XPerfectOutlineThin"),
                    Localization.Current.Get("XPerfectOutlineNormal"),
                    Localization.Current.Get("XPerfectOutlineThick")
                }, 4, GUILayout.Width(400f)) - 1;
            }

        }

        private void DrawErrorMeterSize()
        {
            Label(Localization.Current.Get("ErrorMeterSize"));
            int selected = ErrorMeterSizeSettings.SelectedIndex;
            int next = GUILayout.SelectionGrid(selected, new[]
            {
                ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.Off),
                ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.Small),
                ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.Normal),
                ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.Large),
                ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.ExtraLarge),
                Localization.Current.Get("ErrorMeterSizeCustom")
            }, 6, GUILayout.Width(600f));
            if (next != selected) ErrorMeterSizeSettings.Select(next);

            float scale = ErrorMeterSizeSettings.SliderScale;
            string normal = ErrorMeterSizeSettings.SettingsLabel(ErrorMeterSize.Normal);
            float percent = Mathf.Round(scale * 100f);
            float nextPercent = Slider(nameof(CustomErrorMeterScale),
                string.Format(Localization.Current.Get("ErrorMeterScaleInput"), normal),
                percent, ErrorMeterSizeSettings.MinScale * 100f, ErrorMeterSizeSettings.MaxScale * 100f,
                DefaultCustomErrorMeterScale * 100f, out bool edited, step: 1f, width: 400f, format: "F0");
            // Only actual interaction activates custom mode; merely drawing a preset does not.
            if (edited)
                ErrorMeterSizeSettings.SetScale(nextPercent / 100f);
            Label(string.Format(Localization.Current.Get("ErrorMeterSizeHint"), normal));
        }

        private void DrawOverloadSettings()
        {
            HideOverloadHPWithMarginLimit = Toggle(HideOverloadHPWithMarginLimit,
                Localization.Current.Get("HideOverloadHPWithMarginLimit"));

            overloadHPX = FloatField(
                Localization.Current.Get("AxisX"),
                ref overloadHPXText,
                overloadHPX
            );

            overloadHPY = FloatField(
                Localization.Current.Get("AxisY"),
                ref overloadHPYText,
                overloadHPY
            );

        }

        private void DrawJudgementBarSettings()
        {
            const float groupSpacing = 17f;

            GUILayout.Space(5f);

            DrawErrorMeterSize();

            GUILayout.Space(groupSpacing);

            Label(Localization.Current.Get("PositionOffset"));
            BarPositionX = FloatField(Localization.Current.Get("AxisX"), ref barPositionXText, BarPositionX);
            BarPositionY = FloatField(Localization.Current.Get("AxisY"), ref barPositionYText, BarPositionY);
            if (GUILayout.Button(Localization.Current.Get("ResetPosition"), GUILayout.Width(200f)))
            {
                BarPositionX = BarPositionY = 0f;
                barPositionXText = barPositionYText = "0";
            }

            GUILayout.Space(groupSpacing);

            BackgroundOpacity = Slider(
                nameof(BackgroundOpacity),
                Localization.Current.Get("BackgroundOpacity"),
                BackgroundOpacity, 0f, 1f, DefaultBackgroundOpacity, out _
            );

            GUILayout.Space(groupSpacing);

            bool previousLegacyHit = UseLegacyHit;
            UseLegacyHit = Toggle(UseLegacyHit, Localization.Current.Get("UseLegacyHit"));
            if (UseLegacyHit != previousLegacyHit) LegacyHitVisual.RefreshAll();

            SensitivityOfHit = Slider(
                nameof(SensitivityOfHit),
                Localization.Current.Get("HitSensitivity"),
                SensitivityOfHit,
                0f, 1f, DefaultSensitivityOfHit, out _
            );

            LifeOfHit = Slider(
                nameof(LifeOfHit),
                Localization.Current.Get("HitLife"),
                LifeOfHit,
                0f, 10f, DefaultLifeOfHit, out _, 0.1f
            );

            if (LifeOfHit != DefaultLifeOfHit)
            {
                hitLifeWarningStyle ??= new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    normal = { textColor = Color.yellow }
                };
                GUILayout.Label(Localization.Current.Get("HitLifeWarning"),
                    hitLifeWarningStyle, GUILayout.MaxWidth(600f));
            }

        }


        private void DrawExtraJudgementSettings()
        {
            Label(Localization.Current.Get("SuggestedSettings"));

            FixTickMarginScale = Toggle(
                FixTickMarginScale,
                Localization.Current.Get("FixTickMarginScale")
            );

            bool previousBlueXPerfectResults = BlueXPerfectResults;
            BlueXPerfectResults = Toggle(
                BlueXPerfectResults,
                Localization.Current.Get("BlueXPerfectResults")
            );
            if (BlueXPerfectResults != previousBlueXPerfectResults) XPerfectResultsColor.Refresh();

            GUILayout.Space(10f);
            Label(Localization.Current.Get("TufSuggestedSettings"));

            bool previousBpmWarning = WarnOnStartingBpmChange;
            WarnOnStartingBpmChange = Toggle(WarnOnStartingBpmChange,
                Localization.Current.Get("WarnOnStartingBpmChange"));
            if (previousBpmWarning != WarnOnStartingBpmChange) StartingBpmWarning.Update();

            ShowAllDifficulties = Toggle(
                ShowAllDifficulties,
                Localization.Current.Get("ShowAllDifficulties")
            );

            if (ShowAllDifficulties)
                SectionContent(() => ShowAllDifficultiesExcludeOfficial = Toggle(
                    ShowAllDifficultiesExcludeOfficial,
                    Localization.Current.Get("ShowAllDifficultiesExcludeOfficial")
                ));

            PerfectsOnlyInstantDeath = Toggle(
                PerfectsOnlyInstantDeath,
                Localization.Current.Get("PerfectsOnlyInstantDeath")
            );

            bool previousResults = EnchantedDetailedResults;
            if (Section(Localization.Current.Get("EnchantedDetailedResults"),
                ref detailedResultsExpanded, ref EnchantedDetailedResults))
                SectionContent(DrawDetailedResultsSettings);
            if (previousResults != EnchantedDetailedResults) XPerfectResultsColor.Refresh();
        }

        private void DrawDetailedResultsSettings()
        {
            bool signed = DetailedResultsSignedPerfects;
            bool background = DetailedResultsBackground;
            bool colors = DetailedResultsDefaultColors;
            DetailedResultsSignedPerfects = Toggle(signed, Localization.Current.Get("DetailedResultsSignedPerfects"));
            DetailedResultsBackground = Toggle(background, Localization.Current.Get("DetailedResultsBackground"));
            DetailedResultsDefaultColors = Toggle(colors, Localization.Current.Get("DetailedResultsDefaultColors"));
            if (signed != DetailedResultsSignedPerfects || background != DetailedResultsBackground
                || colors != DetailedResultsDefaultColors) XPerfectResultsColor.Refresh();
        }

        // Save
        public void OnSaveGUI(
            UnityModManager.ModEntry modEntry
        )
        {
            Main.SettingsUpdate();
            Save(modEntry);
        }


        public override void Save(
            UnityModManager.ModEntry modEntry
        )
        {
            Save(
                this,
                modEntry
            );
        }


        public static Settings Load(
            UnityModManager.ModEntry modEntry
        )
        {
            Settings settings =
                Load<Settings>(modEntry);

            if (settings.XPerfectOutlineWidth < -1 || settings.XPerfectOutlineWidth > 2)
                settings.XPerfectOutlineWidth = 1;

            if (settings.FailureBandDisplayMode < 0 || settings.FailureBandDisplayMode > 4)
                settings.FailureBandDisplayMode = 0;

            settings.CustomErrorMeterScale = ErrorMeterSizeSettings.Sanitize(settings.CustomErrorMeterScale);

            settings.overloadHPXText =
                settings.overloadHPX.ToString();

            if (float.IsNaN(settings.BarPositionX) || float.IsInfinity(settings.BarPositionX))
                settings.BarPositionX = 0f;
            if (float.IsNaN(settings.BarPositionY) || float.IsInfinity(settings.BarPositionY))
                settings.BarPositionY = 0f;
            settings.barPositionXText = settings.BarPositionX.ToString();
            settings.barPositionYText = settings.BarPositionY.ToString();

            settings.overloadHPYText =
                settings.overloadHPY.ToString();

            return settings;
        }
    }
}


