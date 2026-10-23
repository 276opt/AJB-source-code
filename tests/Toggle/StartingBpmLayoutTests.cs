using System;
using AccurateJudgementBar;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

static partial class Program
{
    private static void StartingBpmLayoutTests()
    {
        // Reproduce the native failure: the displayed rect is still 100 high while
        // TMP reports 550 for the full text, including the smaller attribution footer.
        var editor = new scnEditor();
        var text = editor.notificationPopupContent;
        text.text = "Rules\nStarting BPM: 180 → 360\nConfirm or cancel.\n<size=80%>AJB footer</size>";
        var scroll = editor.scroll;
        var content = editor.notificationPopupScrollviewContent;
        var fitter = text.GetComponent<ContentSizeFitter>()!;
        var layout = new StartingBpmWarningLayout(editor);
        layout.Apply();
        Check(text.measuredText == text.text && text.measuredWidth == 840,
            "Measure the entire rich-text warning at the real width, without doubling fixed-width children");
        Check(content.rect.height == 558 && text.rectTransform.rect.height == 558 &&
            editor.notificationPopupWindow.rect.height == 668 && scroll.viewport.rect.height == 558,
            "Stale native text height must not clip the change instructions or the attribution footer");
        Check(!fitter.enabled && text.overflowMode == TextOverflowModes.Overflow &&
            text.textWrappingMode == TextWrappingModes.Normal && content.pivot.y == 1 &&
            content.anchorMin.y == 1 && text.rectTransform.pivot.y == 1,
            "The measured text stays top anchored and wraps without a truncating TMP overflow mode");
        Check(scroll.enabled && scroll.vertical && !scroll.horizontal && scroll.verticalNormalizedPosition == 1 &&
            editor.notificationPopupScrollviewVertical.activeSelf && !editor.notificationPopupScrollviewHorizontal.activeSelf,
            "Warnings start at the top with vertical scrolling available, including after a short native popup");
        layout.Refresh();
        int measurements = text.measurements;
        scroll.verticalNormalizedPosition = 0;
        layout.Refresh();
        Check(text.measurements == measurements && scroll.verticalNormalizedPosition == 0,
            "Ordinary updates must not remeasure the text or snap the reader back to the top");

        var parent = (RectTransform)editor.notificationPopupWindow.parent!;
        parent.sizeDelta.y = 480;
        layout.Refresh();
        Check(editor.notificationPopupWindow.rect.height == 400 && scroll.viewport.rect.height == 290 &&
            content.rect.height == 558 && scroll.verticalNormalizedPosition == 0,
            "Small canvases keep the window onscreen and retain the full content for scrolling to the footer");
        editor.notificationPopupWindow.sizeDelta.x = 680;
        layout.Refresh();
        Check(text.measuredWidth == 640 && text.rectTransform.rect.width == 640,
            "Canvas or editor width changes trigger measurement at the new wrapped width");
        layout.Restore();
        layout.Restore();
        Check(content.sizeDelta == new Vector2(840, 100) && text.rectTransform.sizeDelta == new Vector2(840, 100) &&
            content.anchorMax == Vector2.zero && text.rectTransform.anchorMax == Vector2.zero && fitter.enabled &&
            text.overflowMode == TextOverflowModes.Truncate && text.textWrappingMode == TextWrappingModes.NoWrap,
            "Warning cleanup restores the native text fitting, overflow, anchors and dimensions exactly once");
        Check(!scroll.enabled && !scroll.vertical && scroll.horizontal && scroll.verticalNormalizedPosition == 0.3f &&
            scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.Permanent &&
            !editor.notificationPopupScrollviewVertical.activeSelf && editor.notificationPopupScrollviewHorizontal.activeSelf &&
            editor.notificationPopupWindow.rect.height == 400,
            "Native scrolling, scrollbar visibility and window height are restored before another notification");

        editor = new scnEditor();
        editor.notificationPopupContent.preferredTextHeight = 1200;
        layout = new StartingBpmWarningLayout(editor);
        layout.Apply();
        Check(editor.notificationPopupWindow.rect.height == 800 && editor.scroll.viewport.rect.height == 690 &&
            editor.notificationPopupScrollviewContent.rect.height == 1208,
            "Long translations retain all text while respecting the native 800-unit window cap");
        var oldBar = editor.notificationPopupScrollviewVertical;
        editor.notificationPopupScrollviewVertical = new GameObject();
        Check(!layout.IsCurrent, "Replacing popup UI invalidates the old warning layout");
        layout.Restore();
        Check(!oldBar.activeSelf && editor.notificationPopupScrollviewVertical.activeSelf,
            "Cleanup restores captured UI objects without mutating their replacements");
    }
}
