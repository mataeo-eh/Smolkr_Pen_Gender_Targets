// TaskPanelPatches.cs
//
// Role in the larger system: tells UI/TaskPanelTracker whenever ASKA fills in a
// workstation task panel, so the gender targets panel knows when a smolkr pen's
// slaughter task is on screen.
//
// Both SetPanel (a task is assigned to the panel) and RefreshPanel (the panel
// redraws, e.g. after the limit changes) are observed. Either one alone would
// probably do; watching both means one of them being skipped by some menu path
// does not leave the mod's panel missing.
//
// Read-only: neither patch changes what the game shows or does.

using System;
using HarmonyLib;
using SmolkrPenGenderTargets.UI;
using SSSGame;

namespace SmolkrPenGenderTargets.Patches;

[HarmonyPatch(typeof(WorkstationTaskPanel), nameof(WorkstationTaskPanel.SetPanel))]
internal static class TaskPanelSetPatch
{
    /// <summary>Reports the newly assigned task. Calls: TaskPanelObserver.Observe.</summary>
    private static void Postfix(WorkstationTaskPanel __instance) => TaskPanelObserver.Observe(__instance);
}

[HarmonyPatch(typeof(WorkstationTaskPanel), nameof(WorkstationTaskPanel.RefreshPanel))]
internal static class TaskPanelRefreshPatch
{
    /// <summary>Reports the redrawn task. Calls: TaskPanelObserver.Observe.</summary>
    private static void Postfix(WorkstationTaskPanel __instance) => TaskPanelObserver.Observe(__instance);
}

/// <summary>Shared, exception-safe forwarding for both panel patches.</summary>
internal static class TaskPanelObserver
{
    private static bool _failureReported;

    /// <summary>
    /// Forwards to the tracker, making sure a failure here can never break the
    /// game's own task panel.
    /// </summary>
    internal static void Observe(WorkstationTaskPanel panel)
    {
        try
        {
            TaskPanelTracker.Observe(panel);
        }
        catch (Exception e)
        {
            if (_failureReported) return;
            _failureReported = true;
            Plugin.Log.LogError($"SPGT event=task_panel_failed - the gender targets panel may not appear. Details: {e}");
        }
    }
}
