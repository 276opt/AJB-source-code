using System;
using System.Collections.Generic;
using System.Data;
using ADOFAI;
using AccurateJudgementBar;

namespace TMPro
{
    public enum TextOverflowModes { Truncate, Overflow }
    public enum TextWrappingModes { NoWrap, Normal }
    public class TMP_Text : UnityEngine.MonoBehaviour
    {
        public string text = "";
        public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
        public TextWrappingModes textWrappingMode;
        public TextOverflowModes overflowMode;
        public float preferredTextHeight = 550f;
        public int measurements;
        public float measuredWidth;
        public string measuredText = "";
        public Exception? measureError;
        public UnityEngine.Vector2 GetPreferredValues(string value, float width, float height)
        {
            if (measureError != null) throw measureError;
            measurements++;
            measuredWidth = width;
            measuredText = value;
            // Font shaping is not simulated: tests supply the full TMP measurement,
            // independently of the stale rendered rectangle.
            return new(width, preferredTextHeight);
        }
    }
    public class TMP_InputField
    {
        public string text = "100";
        public SubmitEvent onEndEdit = new();
        public void SetTextWithoutNotify(string value) => text = value;
        public class SubmitEvent
        {
            private readonly List<Action<string>> callbacks = new();
            public void AddListener(Action<string> callback) => callbacks.Add(callback);
            public void Invoke(string value)
            {
                foreach (var callback in callbacks.ToArray()) callback(value);
            }
        }
    }
}

namespace ADOFAI
{
    public enum LevelEventType { SongSettings, SetSpeed }
    public class LevelEvent { public float bpm = 100; }
    public class LevelData
    {
        public LevelEvent songSettings = new();
        public float bpm => songSettings.bpm;
        public bool LoadLevel(string path, out bool success)
        {
            success = path != "bad";
            if (success) songSettings = new();
            StartingBpmWarning.LoadedPatch.Postfix(this, success);
            return success;
        }
        public void Setup()
        {
            songSettings = new();
            StartingBpmWarning.NewLevelPatch.Postfix(this);
        }
        public LevelData Copy()
        {
            var copy = new LevelData { songSettings = new() { bpm = bpm } };
            StartingBpmWarning.CopyPatch.Postfix(this, copy);
            return copy;
        }
    }
}

namespace ADOFAI.LevelEditor.Controls
{
    public class BpmPropertyInfo { public string name = "bpm"; }
    public class BpmInspector { public LevelEvent selectedEvent = new(); }
    public class BpmPropertiesPanel
    {
        public LevelEventType levelEventType = LevelEventType.SongSettings;
        public BpmInspector inspectorPanel = new();
    }
    public class PropertyControl_Text
    {
        public TMPro.TMP_InputField inputField = new();
        public BpmPropertyInfo propertyInfo = new();
        public BpmPropertiesPanel propertiesPanel = new();
        public Exception? nativeError, validationError;
        public string Validate()
        {
            if (validationError != null) throw validationError;
            float value;
            if (!float.TryParse(inputField.text, out value))
            {
                try { value = Convert.ToSingle(new DataTable().Compute(inputField.text, "")); }
                catch { value = 100; }
            }
            return Math.Clamp(value, 1, 10000).ToString();
        }
        public void Setup(bool addListener)
        {
            StartingBpmWarning.SetupPatch.Prefix(this);
            if (addListener) inputField.onEndEdit.AddListener(_ =>
            {
                var editor = scnEditor.instance!;
                editor.undo.Add(editor.levelData.Copy()); // Native SaveStateScope runs before validation.
                editor.unsavedChanges = true;
                editor.nativeEdits++;
                if (nativeError != null) throw nativeError;
                inputField.text = Validate();
                propertiesPanel.inspectorPanel.selectedEvent.bpm = float.Parse(inputField.text);
                editor.pathRefreshes++;
            });
            StartingBpmWarning.SetupPatch.Postfix(this);
        }
    }
}

public class scnEditor
{
    public static scnEditor? instance;
    public LevelData levelData = new();
    public bool playMode, isLoading, unsavedChanges;
    public int nativeEdits, pathRefreshes, popupCount, plays;
    public List<LevelData> undo = new();
    public UnityEngine.GameObject notificationPopupContainer = new();
    public UnityEngine.RectTransform notificationPopupWindow = new();
    public UnityEngine.RectTransform notificationPopupScrollview = new();
    public UnityEngine.RectTransform notificationPopupScrollviewContent = new();
    public TMPro.TMP_Text notificationPopupContent = new();
    public UnityEngine.GameObject notificationPopupScrollviewVertical = new();
    public UnityEngine.GameObject notificationPopupScrollviewHorizontal = new();
    public UnityEngine.UI.ScrollRect scroll = new();
    public NotificationAction[] actions = Array.Empty<NotificationAction>();
    public Exception? popupError;
    public string body = "";
    public scnEditor()
    {
        notificationPopupContainer.SetActive(false);
        notificationPopupWindow.parent = new UnityEngine.RectTransform { sizeDelta = new(1000, 1080) };
        notificationPopupWindow.sizeDelta = new(880, 400);
        notificationPopupScrollview.parent = notificationPopupWindow;
        notificationPopupScrollview.anchorMax = new(1, 1);
        notificationPopupScrollview.sizeDelta = new(-40, -110);
        notificationPopupScrollview.layoutComponents[typeof(UnityEngine.UI.ScrollRect)] = scroll;
        notificationPopupScrollviewContent.parent = notificationPopupScrollview;
        notificationPopupScrollviewContent.sizeDelta = new(840, 100);
        notificationPopupContent.rectTransform.parent = notificationPopupScrollviewContent;
        notificationPopupContent.rectTransform.sizeDelta = new(840, 100);
        notificationPopupContent.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        scroll.viewport = notificationPopupScrollview;
        scroll.content = notificationPopupScrollviewContent;
        scroll.enabled = false;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.verticalNormalizedPosition = 0.3f;
        notificationPopupScrollviewVertical.SetActive(false);
    }
    public sealed class NotificationAction
    {
        public readonly string text;
        public readonly Action action;
        public NotificationAction(string text, Action action) { this.text = text; this.action = action; }
    }
    public void ShowNotificationPopup(string text, NotificationAction[] notificationActions, string? title)
    {
        StartingBpmWarning.ReplacementPopupPatch.Prefix(this);
        notificationPopupContainer.SetActive(true);
        if (popupError != null) throw popupError;
        popupCount++;
        body = text;
        notificationPopupContent.text = text;
        actions = notificationActions;
    }
    public void Play() { if (StartingBpmWarning.PlayPatch.Prefix(this)) plays++; }
}

namespace UnityEngine
{
    public static class Canvas { public static void ForceUpdateCanvases() { } }
}

namespace UnityEngine.UI
{
    public class ContentSizeFitter : UnityEngine.MonoBehaviour { }
    public class ScrollRect : UnityEngine.MonoBehaviour
    {
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public RectTransform content = null!, viewport = null!;
        public bool vertical, horizontal;
        public float verticalNormalizedPosition;
        public ScrollbarVisibility verticalScrollbarVisibility;
        public MovementType movementType;
        public void StopMovement() { }
    }
    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform rect) { }
    }
}
