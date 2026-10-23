using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // The native notification measures rect.height before enabling its scroll view and
    // configuring action buttons. Measure the complete TMP text only after that work.
    // All changes belong to this warning and are restored before native UI reuse.
    internal sealed class StartingBpmWarningLayout
    {
        private readonly scnEditor editor;
        private readonly TMP_Text text;
        private readonly RectTransform window, content, textRect;
        private readonly GameObject verticalBar, horizontalBar;
        private readonly ScrollRect scroll;
        private readonly ContentSizeFitter? fitter;
        private readonly RectState contentState, textState;
        private readonly float windowHeight, scrollPosition;
        private readonly bool fitEnabled, scrollEnabled, vertical, horizontal;
        private readonly TextWrappingModes wrapping;
        private readonly bool verticalBarActive, horizontalBarActive;
        private readonly TextOverflowModes overflow;
        private readonly ScrollRect.ScrollbarVisibility scrollbarVisibility;
        private readonly ScrollRect.MovementType movement;
        private readonly RectTransform originalScrollContent;
        private bool restored, settle = true;
        private float measuredWidth, measuredParentHeight;

        private readonly struct RectState
        {
            private readonly Vector2 min, max, pivot, position, size;

            internal RectState(RectTransform rect)
            {
                min = rect.anchorMin;
                max = rect.anchorMax;
                pivot = rect.pivot;
                position = rect.anchoredPosition;
                size = rect.sizeDelta;
            }

            internal void Restore(RectTransform rect)
            {
                if (rect == null) return;
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.pivot = pivot;
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
        }

        internal StartingBpmWarningLayout(scnEditor editor)
        {
            this.editor = editor;
            text = editor.notificationPopupContent;
            textRect = text.rectTransform;
            window = editor.notificationPopupWindow;
            content = editor.notificationPopupScrollviewContent;
            verticalBar = editor.notificationPopupScrollviewVertical;
            horizontalBar = editor.notificationPopupScrollviewHorizontal;
            scroll = editor.notificationPopupScrollview.GetComponent<ScrollRect>() ??
                throw new InvalidOperationException("Starting BPM warning is missing its scroll view.");
            fitter = text.GetComponent<ContentSizeFitter>();
            textState = new RectState(textRect);
            contentState = new RectState(content);
            windowHeight = window.rect.height;
            fitEnabled = fitter != null && fitter.enabled;
            wrapping = text.textWrappingMode;
            overflow = text.overflowMode;
            scrollEnabled = scroll.enabled;
            vertical = scroll.vertical;
            horizontal = scroll.horizontal;
            movement = scroll.movementType;
            scrollbarVisibility = scroll.verticalScrollbarVisibility;
            originalScrollContent = scroll.content;
            scrollPosition = scroll.verticalNormalizedPosition;
            verticalBarActive = verticalBar.activeSelf;
            horizontalBarActive = horizontalBar.activeSelf;
        }

        internal bool IsCurrent => text != null && window != null && content != null && scroll != null &&
            editor.notificationPopupContent == text && editor.notificationPopupWindow == window &&
            editor.notificationPopupScrollviewContent == content &&
            editor.notificationPopupScrollviewVertical == verticalBar &&
            editor.notificationPopupScrollviewHorizontal == horizontalBar &&
            editor.notificationPopupScrollview.GetComponent<ScrollRect>() == scroll;

        internal void Apply()
        {
            if (fitter != null) fitter.enabled = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            scroll.content = content;
            scroll.enabled = true;
            scroll.vertical = true;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            // AutoHide keeps the viewport width stable (unlike AutoHideAndExpandViewport).
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            verticalBar.SetActive(true);
            horizontalBar.SetActive(false);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            AnchorAtTop(content);
            if (textRect != content) AnchorAtTop(textRect);
            Reflow(true);
        }

        private static void AnchorAtTop(RectTransform rect)
        {
            float width = rect.rect.width;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        private float ParentHeight => (window.parent as RectTransform)?.rect.height ?? 0f;

        internal void Refresh()
        {
            // One deferred pass accommodates TMP/layout activation. After that, only resize
            // when the canvas or text width changes, leaving ordinary scrolling untouched.
            if (!settle && Math.Abs(textRect.rect.width - measuredWidth) < 0.5f &&
                Math.Abs(ParentHeight - measuredParentHeight) < 0.5f) return;
            Reflow(settle);
            settle = false;
        }

        private void Reflow(bool startAtTop)
        {
            float position = startAtTop ? 1f : scroll.verticalNormalizedPosition;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            var viewport = scroll.viewport != null ? scroll.viewport : editor.notificationPopupScrollview;
            float chrome = Math.Max(110f, window.rect.height - viewport.rect.height);
            float parentHeight = ParentHeight;
            float maxHeight = parentHeight > 0f ? Math.Min(800f, parentHeight - 80f) : 800f;
            maxHeight = Math.Max(chrome + 40f, maxHeight);
            float width = textRect.rect.width;
            if (width <= 0f) throw new InvalidOperationException("Starting BPM warning has no text width.");
            float height = (float)Math.Ceiling(text.GetPreferredValues(text.text, width, float.PositiveInfinity).y) + 8f;
            window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Math.Min(maxHeight, height + chrome));
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (textRect != content) textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(window);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = position;
            measuredWidth = textRect.rect.width;
            measuredParentHeight = parentHeight;
        }

        internal void Restore()
        {
            if (restored) return;
            restored = true;
            DisplayErrors.Run("restore BPM warning text layout", () =>
            {
                if (text == null) return;
                text.overflowMode = overflow;
                text.textWrappingMode = wrapping;
                textState.Restore(textRect);
                if (fitter != null) fitter.enabled = fitEnabled;
            });
            DisplayErrors.Run("restore BPM warning content layout", () => contentState.Restore(content));
            DisplayErrors.Run("restore BPM warning window height", () =>
            {
                if (window != null) window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, windowHeight);
            });
            DisplayErrors.Run("restore BPM warning scrolling", () =>
            {
                if (scroll == null) return;
                scroll.StopMovement();
                scroll.content = originalScrollContent;
                scroll.vertical = vertical;
                scroll.horizontal = horizontal;
                scroll.movementType = movement;
                scroll.verticalScrollbarVisibility = scrollbarVisibility;
                scroll.verticalNormalizedPosition = scrollPosition;
                scroll.enabled = scrollEnabled;
            });
            DisplayErrors.Run("restore BPM warning vertical scrollbar", () =>
            {
                if (verticalBar != null) verticalBar.SetActive(verticalBarActive);
            });
            DisplayErrors.Run("restore BPM warning horizontal scrollbar", () =>
            {
                if (horizontalBar != null) horizontalBar.SetActive(horizontalBarActive);
            });
        }
    }
}
