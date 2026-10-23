using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using TMPro;

namespace AccurateJudgementBar
{
    // Editor-only confirmation. Never write LevelData, timing, judgement or score here:
    // confirmation resumes the original end-edit event (including its SaveStateScope).
    internal static class StartingBpmWarning
    {
        private static readonly ConditionalWeakTable<LevelData, object> openedLevels = new();
        private static readonly ConditionalWeakTable<PropertyControl_Text, Binding> bindings = new();
        private static PendingEdit? pending;
        private static bool showingWarning;

        private static bool Enabled => Main.is_mod_enable && Main.settings.WarnOnStartingBpmChange;

        internal sealed class Binding
        {
            internal readonly PropertyControl_Text Control;
            internal readonly TMP_InputField.SubmitEvent Original;
            internal readonly TMP_InputField.SubmitEvent Intercepted = new();

            internal Binding(PropertyControl_Text control)
            {
                Control = control;
                Original = control.inputField.onEndEdit;
                Intercepted.AddListener(Submit);
            }

            internal void Submit(string text)
            {
                if (!TryDefer(this))
                    Original.Invoke(text); // Original exceptions must still propagate.
            }
        }

        private sealed class PendingEdit
        {
            internal readonly Binding Binding;
            internal readonly scnEditor Editor;
            internal readonly LevelData Data;
            internal readonly LevelEvent Song;
            internal readonly object Origin;
            internal readonly float Previous;
            internal readonly string Requested;
            internal readonly UnityEngine.GameObject PopupContainer;
            internal readonly UnityEngine.RectTransform PopupWindow;
            internal StartingBpmWarningLayout? Layout;

            internal PendingEdit(Binding binding, scnEditor editor, object origin, string requested)
            {
                Binding = binding;
                Editor = editor;
                Data = editor.levelData;
                Song = Data.songSettings;
                Origin = origin;
                Previous = Data.bpm;
                Requested = requested;
                PopupContainer = editor.notificationPopupContainer;
                PopupWindow = editor.notificationPopupWindow;
            }

            internal bool IsCurrent => Editor != null && Editor == scnEditor.instance &&
                !Editor.playMode && !Editor.isLoading &&
                Binding.Control != null && Binding.Control.inputField != null &&
                ReferenceEquals(Editor.levelData, Data) && ReferenceEquals(Data.songSettings, Song) &&
                ReferenceEquals(Binding.Control.propertiesPanel.inspectorPanel.selectedEvent, Song) &&
                openedLevels.TryGetValue(Data, out var origin) && ReferenceEquals(origin, Origin) &&
                Data.bpm.Equals(Previous) && Editor.notificationPopupContainer == PopupContainer &&
                Editor.notificationPopupWindow == PopupWindow && (Layout == null || Layout.IsCurrent);
        }

        internal static bool IsBlocking(scnEditor editor) => pending != null && pending.Editor == editor;

        private static bool TryDefer(Binding binding)
        {
            if (pending != null && ReferenceEquals(pending.Binding, binding)) return true;
            if (!Enabled) return false;
            var control = binding.Control;
            var editor = scnEditor.instance;
            try
            {
                if (editor == null || editor.playMode || editor.isLoading ||
                    control.propertyInfo.name != "bpm" ||
                    !ReferenceEquals(control.propertiesPanel.inspectorPanel.selectedEvent, editor.levelData.songSettings) ||
                    !openedLevels.TryGetValue(editor.levelData, out var origin)) return false;
                // Match native expression parsing, clamping and culture before comparing.
                string requested = control.Validate();
                if (!float.TryParse(requested, out float bpm) || bpm.Equals(editor.levelData.bpm)) return false;
                if (pending != null) return true;
                var edit = new PendingEdit(binding, editor, origin, requested);
                pending = edit;
                control.inputField.SetTextWithoutNotify(edit.Previous.ToString(CultureInfo.CurrentCulture));

                var loc = Localization.Current;
                string body = loc.Get("StartingBpmWarningBody") + "\n\n" + string.Format(
                    CultureInfo.CurrentCulture, loc.Get("StartingBpmWarningChange"), edit.Previous, bpm) +
                    "\n\n<size=80%>" + string.Format(CultureInfo.CurrentCulture,
                        loc.Get("StartingBpmWarningFooter"), loc.Get("ExtraJudgementSettings")) + "</size>";
                showingWarning = true;
                try
                {
                    // No title argument: native notification layout keeps its scroll view visible.
                    editor.ShowNotificationPopup(body, new[]
                    {
                        new scnEditor.NotificationAction(loc.Get("StartingBpmWarningConfirm"), () => Resolve(edit, true)),
                        new scnEditor.NotificationAction(loc.Get("StartingBpmWarningCancel"), () => Resolve(edit, false))
                    }, null);
                    edit.Layout = new StartingBpmWarningLayout(editor);
                    edit.Layout.Apply();
                }
                finally { showingWarning = false; }
                return true;
            }
            catch (Exception ex)
            {
                // A broken warning must not commit an unconfirmed edit or leave input blocked.
                DisplayErrors.Report("starting BPM warning", ex);
                CancelPending();
                return true;
            }
        }

        private static void Resolve(PendingEdit edit, bool confirm)
        {
            if (!ReferenceEquals(pending, edit)) return;
            bool apply = false;
            DisplayErrors.Run("validate starting BPM confirmation", () => apply = confirm && Enabled && edit.IsCurrent);
            pending = null;
            edit.Layout?.Restore();
            Close(edit);
            if (!apply) return;
            if (!DisplayErrors.TryRun("restore confirmed BPM input", () =>
                edit.Binding.Control.inputField.SetTextWithoutNotify(edit.Requested))) return;
            edit.Binding.Original.Invoke(edit.Requested);
        }

        private static void Close(PendingEdit edit)
        {
            // Hide immediately so no delayed close tween can dismiss the next native
            // notification. Each cleanup is isolated so an animation error cannot trap the UI.
            DisplayErrors.Run("stop starting BPM window tween", () =>
            {
                if (edit.PopupWindow != null) DG.Tweening.DOTween.Kill(edit.PopupWindow, false);
            });
            DisplayErrors.Run("stop starting BPM backdrop tween", () =>
            {
                if (edit.PopupContainer != null)
                    DG.Tweening.DOTween.Kill(edit.PopupContainer.GetComponent<UnityEngine.UI.Image>(), false);
            });
            DisplayErrors.Run("close starting BPM warning", () =>
            {
                if (edit.PopupContainer != null) edit.PopupContainer.SetActive(false);
            });
        }

        internal static void CancelPending(bool close = true)
        {
            var edit = pending;
            pending = null;
            edit?.Layout?.Restore();
            if (edit != null && close) Close(edit);
        }

        internal static void Update()
        {
            if (pending == null) return;
            try
            {
                if (!Enabled || !pending.IsCurrent)
                    CancelPending();
                else
                    pending.Layout?.Refresh();
            }
            catch (Exception ex)
            {
                DisplayErrors.Report("refresh starting BPM warning", ex);
                CancelPending();
            }
        }

        [HarmonyPatch(typeof(LevelData), nameof(LevelData.LoadLevel))]
        internal static class LoadedPatch
        {
            internal static void Postfix(LevelData __instance, bool __result)
            {
                if (!__result) return;
                openedLevels.Remove(__instance);
                openedLevels.Add(__instance, new object());
            }
        }

        [HarmonyPatch(typeof(LevelData), nameof(LevelData.Setup))]
        internal static class NewLevelPatch
        {
            internal static void Postfix(LevelData __instance) => openedLevels.Remove(__instance);
        }

        [HarmonyPatch(typeof(LevelData), nameof(LevelData.Copy))]
        internal static class CopyPatch
        {
            internal static void Postfix(LevelData __instance, LevelData __result)
            {
                // Undo/Redo uses LevelData.Copy; saving a new map must not mark it as opened.
                openedLevels.Remove(__result);
                if (openedLevels.TryGetValue(__instance, out var origin)) openedLevels.Add(__result, origin);
            }
        }

        [HarmonyPatch(typeof(PropertyControl_Text), nameof(PropertyControl_Text.Setup))]
        internal static class SetupPatch
        {
            internal static void Prefix(PropertyControl_Text __instance)
            {
                DisplayErrors.Run("unbind starting BPM input", () =>
                {
                    if (!bindings.TryGetValue(__instance, out var binding)) return;
                    if (ReferenceEquals(__instance.inputField.onEndEdit, binding.Intercepted))
                        __instance.inputField.onEndEdit = binding.Original;
                    bindings.Remove(__instance);
                    if (pending != null && ReferenceEquals(pending.Binding, binding)) CancelPending();
                });
            }

            internal static void Postfix(PropertyControl_Text __instance)
            {
                DisplayErrors.Run("bind starting BPM input", () =>
                {
                    if (__instance.propertyInfo.name != "bpm" ||
                        __instance.propertiesPanel.levelEventType != LevelEventType.SongSettings) return;
                    var binding = new Binding(__instance);
                    bindings.Add(__instance, binding);
                    __instance.inputField.onEndEdit = binding.Intercepted;
                });
            }
        }

        // The notification overlay blocks pointer UI; also block editor shortcuts, tile input
        // and Play from the same click that caused the BPM input to lose focus. Escape cannot
        // dismiss the warning. Native input returns immediately after either button is chosen.
        [HarmonyPatch(typeof(scnEditor), "HandleKeyboardActions")]
        internal static class KeyboardPatch
        {
            internal static bool Prefix(scnEditor __instance) => !IsBlocking(__instance);
        }

        [HarmonyPatch(typeof(scnEditor), "HandleMouseActions")]
        internal static class MousePatch
        {
            internal static bool Prefix(scnEditor __instance) => !IsBlocking(__instance);
        }

        [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.Play))]
        internal static class PlayPatch
        {
            internal static bool Prefix(scnEditor __instance) => !IsBlocking(__instance);
        }

        [HarmonyPatch(typeof(scnEditor), "CloseNotificationPopup")]
        internal static class ClosePatch
        {
            internal static bool Prefix(scnEditor __instance) => !IsBlocking(__instance);
        }

        [HarmonyPatch(typeof(scnEditor), "ShowNotificationPopupBase")]
        internal static class ReplacementPopupPatch
        {
            internal static void Prefix(scnEditor __instance)
            {
                // A native error/notification owns its own buttons; invalidate our old callbacks.
                if (!showingWarning && IsBlocking(__instance)) CancelPending(false);
            }
        }
    }
}
