using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.UI;

namespace AccurateJudgementBar
{
    // OnLandOnPortal fills Strict Clear after DetailedResults.Show: decorate only after
    // the entire native completion routine succeeds. Never generate or force a message.
    [HarmonyPatch(typeof(scrController), "OnLandOnPortal")]
    internal static class CompletionResultsText
    {
        private static readonly List<DetailedResultsView> views = new List<DetailedResultsView>();

        [HarmonyPostfix]
        private static void Postfix() => Refresh();

        internal static bool Owns(scrController owner, Text text) => owner == ADOBase.controller
            && (States)owner.stateMachine.GetState() == States.Won
            && (text == owner.txtCongrats || text == owner.txtAllStrictClear || text == owner.txtAprilCongrats);

        internal static void Refresh() => DisplayErrors.Run("Completion Results refresh", () => RefreshDisplay(true));

        // ChangeState(Won) may be queued, and native endscreen Text can appear after
        // OnLandOnPortal returns. Discover it on the mod update without rebuilding live views.
        internal static void Update() => DisplayErrors.Run("Completion Results update", () => RefreshDisplay(false));

        private static void RefreshDisplay(bool refreshExisting)
        {
            var owner = ADOBase.controller;
            var settings = Main.settings;
            bool active = EnchantedDetailedResults.Active && settings != null
                && (settings.DetailedResultsBackground || settings.DetailedResultsDefaultColors);
            for (int i = views.Count - 1; i >= 0; i--)
            {
                var view = views[i];
                if (view == null) { views.RemoveAt(i); continue; }
                // Each restoration is isolated, so one broken UI does not strand other HUD flags.
                DisplayErrors.Run("Completion Results cleanup", () =>
                {
                    if (!active || !view.IsCurrentCompletion(owner)) view.enabled = false;
                });
            }
            if (!active || owner == null || (States)owner.stateMachine.GetState() != States.Won) return;
            Apply(owner, owner.txtCongrats, refreshExisting);
            Apply(owner, owner.txtAllStrictClear, refreshExisting);
            Apply(owner, owner.txtAprilCongrats, refreshExisting);
        }

        private static void Apply(scrController owner, Text? text, bool refreshExisting)
        {
            if (text == null || !text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) return;
            // Do not take over the separately managed detailed-results Text if a mod aliases the fields.
            if (owner.detailedResults != null && text == owner.detailedResults.textComponent) return;
            DetailedResultsView? view = null;
            if (!DisplayErrors.TryRun("Completion Results view", () =>
            {
                view = text.GetComponent<DetailedResultsView>();
                if (!refreshExisting && view != null && view.IsCurrentCompletion(owner)
                    && (view.enabled || view.DisplayFailed)) return;
                view = view ?? text.gameObject.AddComponent<DetailedResultsView>();
                if (!views.Contains(view)) views.Add(view);
                view.BindCompletion(owner);
                view.enabled = true;
                view.Refresh();
            }))
                DisplayErrors.Run("Completion Results rollback", () => { if (view != null) view.enabled = false; });
        }
    }
}
