using System;
using System.Globalization;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using AccurateJudgementBar;
using UnityModManagerNet;
using Mod = AccurateJudgementBar.Main;

static partial class Program
{
    private static void StartingBpmWarningTests(UnityModManager.ModEntry entry)
    {
        entry.OnToggle(entry, true);
        Mod.settings.WarnOnStartingBpmChange = true;
        var editor = new scnEditor();
        scnEditor.instance = editor;
        var control = new PropertyControl_Text();
        control.propertiesPanel.inspectorPanel.selectedEvent = editor.levelData.songSettings;
        control.Setup(true);

        void Edit(string value)
        {
            control.inputField.text = value;
            control.inputField.onEndEdit.Invoke(value);
        }
        void Open(string path = "existing.adofai")
        {
            editor.levelData.LoadLevel(path, out _);
            control.propertiesPanel.inspectorPanel.selectedEvent = editor.levelData.songSettings;
            editor.unsavedChanges = false;
            editor.undo.Clear();
            editor.nativeEdits = 0;
        }
        void AssertPending(string reason)
        {
            Check(StartingBpmWarning.IsBlocking(editor) && editor.notificationPopupContainer.activeSelf &&
                editor.levelData.bpm == 100 && editor.nativeEdits == 0 && editor.undo.Count == 0 &&
                !editor.unsavedChanges && control.inputField.text == "100", reason);
        }

        Edit("150");
        Check(editor.levelData.bpm == 150 && editor.popupCount == 0, "Default new editor map edits freely");
        // Saving has no provenance hook: a newly authored/saved map still edits freely.
        editor.levelData = editor.levelData.Copy();
        control.propertiesPanel.inspectorPanel.selectedEvent = editor.levelData.songSettings;
        Edit("160");
        Check(editor.levelData.bpm == 160 && editor.popupCount == 0, "New/saved map copies stay exempt");
        Open("bad");
        Edit("170");
        Check(editor.levelData.bpm == 170 && editor.popupCount == 0, "Failed open does not mark a new map as opened");

        Open();
        Edit("100.0");
        Check(editor.popupCount == 0, "Unchanged numeric value does not warn");
        Open();
        Edit("50*3");
        AssertPending("Expression edit is deferred before native data, dirty state, path and undo changes");
        Check(editor.actions.Length == 2, "Warning requires an explicit confirm/cancel choice");
        Check(!StartingBpmWarning.KeyboardPatch.Prefix(editor) && !StartingBpmWarning.MousePatch.Prefix(editor) &&
            !StartingBpmWarning.ClosePatch.Prefix(editor), "Escape, shortcuts, tile input and external close are blocked");
        editor.Play();
        Check(editor.plays == 0, "The click that blurs the input cannot start play behind the warning");
        control.inputField.onEndEdit.Invoke("150");
        Check(editor.popupCount == 1 && editor.nativeEdits == 0, "Repeated end-edit cannot duplicate or commit pending input");
        var staleConfirm = editor.actions[0].action;
        editor.actions[1].action();
        staleConfirm();
        Check(editor.levelData.bpm == 100 && editor.undo.Count == 0 && !editor.unsavedChanges &&
            !StartingBpmWarning.IsBlocking(editor) && !editor.notificationPopupContainer.activeSelf,
            "Cancel preserves the original BPM/undo/dirty state and invalidates stale confirm callbacks");

        Edit("150");
        editor.actions[0].action();
        Check(editor.levelData.bpm == 150 && editor.nativeEdits == 1 && editor.undo.Count == 1 &&
            editor.undo[0].bpm == 100 && editor.unsavedChanges, "Confirm replays exactly one native edit and undo snapshot");
        editor.actions[0].action();
        Check(editor.nativeEdits == 1, "Duplicate confirm cannot replay an edit");
        Edit("170");
        Check(StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 150,
            "Every later BPM change still needs confirmation");
        editor.actions[1].action();
        editor.levelData = editor.undo[0].Copy();
        control.propertiesPanel.inspectorPanel.selectedEvent = editor.levelData.songSettings;
        Edit("160");
        Check(StartingBpmWarning.IsBlocking(editor), "Undo/copy retains opened-level provenance");
        editor.actions[1].action();

        Open();
        Edit("150");
        staleConfirm = editor.actions[0].action;
        Mod.settings.WarnOnStartingBpmChange = false;
        Mod.SettingsUpdate();
        staleConfirm();
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100,
            "Disabling the option cancels pending input immediately");
        Edit("180");
        Check(editor.levelData.bpm == 180, "Disabled warning forwards the original callback");
        Mod.settings.WarnOnStartingBpmChange = true;
        Open();
        Edit("150");
        staleConfirm = editor.actions[0].action;
        entry.OnToggle(entry, false);
        staleConfirm();
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100,
            "UMM disable clears modal state and cannot apply a pending edit");
        Edit("180");
        Check(editor.levelData.bpm == 180, "Disabled AJB preserves native editing");
        entry.OnToggle(entry, true);
        Open();
        Edit("150");
        AssertPending("Re-enable still recognizes maps loaded while patches are installed");
        staleConfirm = editor.actions[0].action;
        Open("other.adofai");
        entry.OnUpdate(entry, 0);
        staleConfirm();
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100,
            "Loading another file invalidates the old dialog even with the same LevelData instance");
        Open("bad");
        Edit("150");
        AssertPending("A failed open retains warning protection for the previously opened map");
        editor.actions[1].action();

        Edit("150");
        editor.levelData.Setup();
        control.propertiesPanel.inspectorPanel.selectedEvent = editor.levelData.songSettings;
        entry.OnUpdate(entry, 0);
        Edit("170");
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 170,
            "New Level/Setup clears opened provenance and pending dialog");

        Open();
        var speedEvent = new LevelEvent();
        var speedControl = new PropertyControl_Text();
        speedControl.propertiesPanel.levelEventType = LevelEventType.SetSpeed;
        speedControl.propertiesPanel.inspectorPanel.selectedEvent = speedEvent;
        speedControl.Setup(true);
        speedControl.inputField.text = "200";
        speedControl.inputField.onEndEdit.Invoke("200");
        Check(speedEvent.bpm == 200 && !StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100,
            "SetSpeed events are outside the starting-BPM warning");

        Open();
        control.Setup(false);
        control.Setup(false);
        Edit("150");
        editor.actions[0].action();
        Check(editor.nativeEdits == 1, "Repeated UI setup does not nest wrappers or duplicate native listeners");
        Open();
        Edit("150");
        staleConfirm = editor.actions[0].action;
        control.Setup(false);
        staleConfirm();
        Check(editor.levelData.bpm == 100 && !StartingBpmWarning.IsBlocking(editor),
            "Rebuilding the control invalidates the old dialog");

        editor.popupError = new InvalidOperationException("injected popup failure");
        Edit("150");
        Check(editor.levelData.bpm == 100 && editor.undo.Count == 0 && !StartingBpmWarning.IsBlocking(editor) &&
            !editor.notificationPopupContainer.activeSelf, "Partial popup failure rejects the edit and releases modal state");
        editor.popupError = null;
        editor.notificationPopupContent.measureError = new InvalidOperationException("TMP measurement failure");
        Edit("150");
        Check(!StartingBpmWarning.IsBlocking(editor) && !editor.notificationPopupContainer.activeSelf &&
            editor.levelData.bpm == 100 && editor.undo.Count == 0 &&
            editor.notificationPopupContent.GetComponent<UnityEngine.UI.ContentSizeFitter>()!.enabled && !editor.scroll.enabled,
            "Measurement failure restores partially applied layout, cancels the unconfirmed edit and releases input");
        editor.notificationPopupContent.measureError = null;
        Edit("150");
        editor.notificationPopupContainer.getComponentError = new InvalidOperationException("cleanup failure");
        editor.actions[1].action();
        Check(!editor.notificationPopupContainer.activeSelf && !StartingBpmWarning.IsBlocking(editor),
            "A failed backdrop cleanup still hides the window and releases input");
        editor.notificationPopupContainer.getComponentError = null;
        Edit("150");
        var fault = new InvalidOperationException("native callback failure");
        control.nativeError = fault;
        Exception? caught = null;
        try { editor.actions[0].action(); } catch (Exception ex) { caught = ex; }
        Check(ReferenceEquals(caught, fault) && !StartingBpmWarning.IsBlocking(editor),
            "Original edit exceptions propagate after releasing the dialog");
        control.nativeError = null;

        Open();
        Edit("150");
        staleConfirm = editor.actions[0].action;
        editor.ShowNotificationPopup("native error", Array.Empty<scnEditor.NotificationAction>(), null);
        staleConfirm();
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100 &&
            editor.notificationPopupContainer.activeSelf && !editor.scroll.enabled &&
            editor.notificationPopupContent.GetComponent<UnityEngine.UI.ContentSizeFitter>()!.enabled,
            "Native replacement notifications retain their UI, restore native layout and invalidate our callback");

        Open();
        Edit("150");
        var oldContainer = editor.notificationPopupContainer;
        staleConfirm = editor.actions[0].action;
        editor.notificationPopupContainer = new UnityEngine.GameObject();
        editor.notificationPopupContent = new TMPro.TMP_Text();
        staleConfirm();
        entry.OnUpdate(entry, 0);
        Check(!StartingBpmWarning.IsBlocking(editor) && !oldContainer.activeSelf &&
            editor.notificationPopupContainer.activeSelf && editor.levelData.bpm == 100,
            "Replacing popup UI closes only the old warning and does not hide the new UI");
        editor.notificationPopupContent.rectTransform.parent = editor.notificationPopupScrollviewContent;
        editor.notificationPopupContent.rectTransform.sizeDelta = new(840, 100);
        editor.notificationPopupContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();

        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Open();
            Edit("150,5");
            editor.actions[0].action();
            Check(editor.levelData.bpm == 150.5f, "Decimal parsing/replay follows the native current culture");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Open();
        Edit("150");
        staleConfirm = editor.actions[0].action;
        scnEditor.instance = new scnEditor();
        entry.OnUpdate(entry, 0);
        staleConfirm();
        Check(!StartingBpmWarning.IsBlocking(editor) && editor.levelData.bpm == 100,
            "Editor scene replacement clears old modal state without changing either map");
        scnEditor.instance = null;
        entry.OnToggle(entry, false);
    }
}
