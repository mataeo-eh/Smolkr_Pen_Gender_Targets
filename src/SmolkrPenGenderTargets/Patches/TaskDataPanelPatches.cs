// TaskDataPanelPatches.cs
//
// Role in the larger system: connects UI/FemalesToKeepRow to ASKA's task panel.
//
// SSSGame.UI.TaskDataPanel is the panel the workstation menu uses to show and
// edit a task (driven by WorkstationMenu.CreateTaskDataPanel and
// CreateOrEditTaskPage.Show). Its _SetQuantity method draws the quantity row -
// for a smolkr pen's slaughter task, that is the slaughter limit - and runs each
// time the panel shows a task or the task changes. _onQuantityChanged runs when
// the player moves the limit.
//
// (0.1.0 hooked WorkstationTaskPanel instead. Nothing in the game calls that
// class any more - it is leftover UI - which is why this moved.)
//
// None of these patches changes what the game does; every one is wrapped so a
// failure in the row can never break the game's own task panel.

using System;
using HarmonyLib;
using SandSailorStudio.Inventory;
using SandSailorStudio.UI;
using SmolkrPenGenderTargets.UI;
using SSSGame.AI;
using SSSGame.UI;

namespace SmolkrPenGenderTargets.Patches;

[HarmonyPatch(typeof(TaskDataPanel), nameof(TaskDataPanel._SetQuantity))]
internal static class TaskDataPanelSetQuantityPatch
{
    /// <summary>
    /// Shows the row for a smolkr pen's slaughter task, hides it for any other.
    /// Calls: FemalesToKeepRow.OnQuantitySet.
    /// </summary>
    /// <param name="__instance">The task panel.</param>
    /// <param name="__0">The task now shown.</param>
    private static void Postfix(TaskDataPanel __instance, WorkstationTaskData __0) =>
        RowGuard.Run(() => FemalesToKeepRow.OnQuantitySet(__instance, __0));
}

[HarmonyPatch(typeof(TaskDataPanel), nameof(TaskDataPanel._onQuantityChanged))]
internal static class TaskDataPanelQuantityChangedPatch
{
    /// <summary>Re-caps the row to the new slaughter limit. Calls: FemalesToKeepRow.OnLimitChanged.</summary>
    /// <param name="__instance">The task panel whose limit changed.</param>
    private static void Postfix(TaskDataPanel __instance) =>
        RowGuard.Run(() => FemalesToKeepRow.OnLimitChanged(__instance));
}

[HarmonyPatch(typeof(IncreaseDecreasePanel), nameof(IncreaseDecreasePanel.RefreshAutomaticText))]
internal static class IncreaseDecreaseTextPatch
{
    /// <summary>
    /// Lets the row write "Off" after the control writes its number. Runs for
    /// every -/+ control in the game and returns at once for all but the row's.
    /// Calls: FemalesToKeepRow.OnControlTextRefreshed.
    /// </summary>
    /// <param name="__instance">The control that refreshed its text.</param>
    private static void Postfix(IncreaseDecreasePanel __instance) =>
        RowGuard.Run(() => FemalesToKeepRow.OnControlTextRefreshed(__instance));
}

/// <summary>Runs row code so that a failure is logged once and never escapes into the game's UI.</summary>
internal static class RowGuard
{
    private static bool _failureReported;

    internal static void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            if (_failureReported) return;
            _failureReported = true;
            Plugin.Log.LogError(
                "SPGT event=row_failed - the 'Females to keep' row may be missing or stale; pens keep " +
                $"their saved targets. Details: {e}");
        }
    }
}
