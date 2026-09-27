// TaskPanelTracker.cs
//
// Role in the larger system: knows when the player is looking at a smolkr pen's
// slaughter task, and which pen it belongs to, so GenderTargetsPanel can appear
// beside it and disappear with it.
//
// ASKA shows a workstation task in a WorkstationTaskPanel. Patches/TaskPanelPatches
// reports every panel that is (re)filled; when one is showing an
// AnimalSlaughterTaskData, that task data names its pen. Rather than also hooking
// the panel closing, IsShowing() re-checks each frame that the panel is still
// active and still showing the same task - so the mod's panel cannot be left
// stranded on screen by a close path nobody patched.

using System;
using SSSGame;
using SSSGame.AI;

namespace SmolkrPenGenderTargets.UI;

internal static class TaskPanelTracker
{
    private static WorkstationTaskPanel _panel;
    private static IntPtr _taskDataPointer;
    private static AnimalPen _pen;

    /// <summary>
    /// Records what a task panel is now showing.
    /// Called by: Patches.TaskPanelSetPatch and Patches.TaskPanelRefreshPatch.
    /// </summary>
    /// <param name="panel">The panel that was just filled in.</param>
    internal static void Observe(WorkstationTaskPanel panel)
    {
        if (panel == null) return;

        var taskData = panel.GetTaskData();
        var slaughterTask = taskData?.TryCast<AnimalSlaughterTaskData>();
        var pen = slaughterTask?.SmolkrPen;

        if (pen != null)
        {
            _panel = panel;
            _taskDataPointer = taskData.Pointer;
            _pen = pen;
        }
        else if (_panel != null && _panel.Pointer == panel.Pointer)
        {
            // The tracked panel has been re-used for some other task.
            Clear();
        }
    }

    /// <summary>
    /// Whether a pen's slaughter task is on screen right now, and which pen.
    /// Called every frame by GenderTargetsPanel.
    /// </summary>
    /// <param name="pen">The pen whose slaughter task is showing.</param>
    internal static bool IsShowing(out AnimalPen pen)
    {
        pen = null;
        if (ReferenceEquals(_panel, null)) return false;

        // Unity's overloaded == null is also true once the native object behind
        // a still-referenced wrapper has been destroyed (menu closed for good,
        // pen dismantled).
        if (_panel == null || _pen == null)
        {
            Clear();
            return false;
        }

        if (!_panel.isActiveAndEnabled) return false;

        var current = _panel.GetTaskData();
        if (current == null || current.Pointer != _taskDataPointer) return false;

        pen = _pen;
        return true;
    }

    /// <summary>Stops tracking a pen that is being destroyed.</summary>
    internal static void Forget(IntPtr penPointer)
    {
        if (_pen != null && _pen.Pointer == penPointer) Clear();
    }

    private static void Clear()
    {
        _panel = null;
        _taskDataPointer = IntPtr.Zero;
        _pen = null;
    }
}
