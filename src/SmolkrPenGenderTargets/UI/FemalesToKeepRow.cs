// FemalesToKeepRow.cs
//
// Role in the larger system: the player's control for a pen's gender target -
// a "Females to keep" row added to ASKA's own task panel, directly under the
// smolkr pen's slaughter limit. Patches/TaskDataPanelPatches calls into it.
//
// How the row is made. ASKA's task panel (SSSGame.UI.TaskDataPanel) draws the
// slaughter limit as its "quantity div": a title text plus an
// IncreaseDecreasePanel (the - value + control). This clones that whole div, so
// the new row looks and behaves exactly like the game's own, then re-labels and
// re-wires the copy. Four details make the copy safe (each was read out of the
// game's code with scripts/disasm.ps1; see docs/DESIGN.md):
//
//   * The slaughter limit's own handler (TaskDataPanel._onQuantityChanged) is
//     attached at runtime with AddListener, and Unity's Instantiate does not copy
//     runtime listeners - so the clone cannot change the slaughter limit. Any
//     listeners serialized into the prefab are switched off on the clone too.
//   * IncreaseDecreasePanel.Awake registers the control with a shared
//     IncreaseDecreaseGroup, and every value change updates that group. The clone
//     is built under an inactive holder so its group link can be cut BEFORE its
//     Awake runs, keeping it out of the original's group.
//   * The control wires its own +/- buttons in Setup(), so the clone's buttons
//     drive the clone.
//   * The control rewrites its number text after every change
//     (RefreshAutomaticText). The row needs to show "Off", so that method is
//     postfixed and the row's own text is written after it.
//
// Values: -1 means "off" (vanilla behaviour for the pen), 0..limit is the number
// of adult females to keep. Stepping below 0 turns targets off; stepping up
// from off turns them on.
//
// Where the row goes: if the div's parent arranges its children with a layout
// group, the row is simply inserted after the div. Otherwise the game places the
// div by position (CreateOrEditTaskPage moves it for markets), and the row is
// positioned one row-height below it. The first time a row is built, the
// surrounding layout is written to the log so a misplacement after a game
// update can be diagnosed; RowOffsetX/Y in the config nudge it without a rebuild.
//
// Depends on: Runtime.PenTargetStore, Runtime.SlaughterLog, Core.HerdTargets.

using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime;
using SandSailorStudio.UI;
using SmolkrPenGenderTargets.Core;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;
using SSSGame.AI;
using SSSGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SmolkrPenGenderTargets.UI;

internal static class FemalesToKeepRow
{
    /// <summary>The control value that means "targets off for this pen".</summary>
    private const int OffValue = -1;

    /// <summary>Gap between the slaughter limit row and this one, when placed by position.</summary>
    private const float RowSpacing = 6f;

    /// <summary>Everything held about one task panel's row.</summary>
    private sealed class RowState
    {
        internal GameObject Row;
        internal IncreaseDecreasePanel Control;
        internal TMP_Text Title;
        internal TaskDataPanel Panel;

        /// <summary>The pen whose slaughter task the row is currently showing; null when hidden.</summary>
        internal AnimalPen Pen;
    }

    /// <summary>Rows by the native pointer of the task panel that owns them.</summary>
    private static readonly Dictionary<IntPtr, RowState> RowsByPanel = new();

    /// <summary>The same rows by the native pointer of their control, for the text postfix.</summary>
    private static readonly Dictionary<IntPtr, RowState> RowsByControl = new();

    private static bool _layoutLogged;

    // =====================================================================
    // Called by the patches
    // =====================================================================

    /// <summary>
    /// Shows, refreshes or hides the row after the game has (re)drawn the
    /// panel's quantity row for a task.
    /// Called by: Patches.TaskDataPanelSetQuantityPatch.
    /// Calls: <see cref="GetOrBuild"/>, <see cref="Show"/>, <see cref="Hide"/>.
    /// </summary>
    /// <param name="panel">The task panel.</param>
    /// <param name="task">The task it is now showing.</param>
    internal static void OnQuantitySet(TaskDataPanel panel, WorkstationTaskData task)
    {
        var pen = task?.TryCast<AnimalSlaughterTaskData>()?.SmolkrPen;

        if (pen == null || !Plugin.ShowRow.Value)
        {
            if (RowsByPanel.TryGetValue(panel.Pointer, out var existing)) Hide(existing);
            return;
        }

        var state = GetOrBuild(panel);
        if (state != null) Show(state, pen);
    }

    /// <summary>
    /// Re-caps the row when the player changes the slaughter limit, so females
    /// to keep can never be shown above the limit.
    /// Called by: Patches.TaskDataPanelQuantityChangedPatch.
    /// </summary>
    /// <param name="panel">The task panel whose limit changed.</param>
    internal static void OnLimitChanged(TaskDataPanel panel)
    {
        if (!RowsByPanel.TryGetValue(panel.Pointer, out var state)) return;
        if (state.Pen == null || state.Row == null) return;
        Show(state, state.Pen);
    }

    /// <summary>
    /// Writes the row's own value text after the control has written its
    /// number, which is what lets the row read "Off".
    /// Called by: Patches.IncreaseDecreaseTextPatch, for every control in the game.
    /// </summary>
    /// <param name="control">The control that just refreshed its text.</param>
    internal static void OnControlTextRefreshed(IncreaseDecreasePanel control)
    {
        if (!RowsByControl.TryGetValue(control.Pointer, out var state)) return;

        string text = control.Value <= OffValue ? "Off" : control.Value.ToString();
        if (control.ValueText != null) control.ValueText.text = text;
        if (control.InputField != null) control.InputField.SetTextWithoutNotify(text);
    }

    // =====================================================================
    // Showing
    // =====================================================================

    /// <summary>
    /// Points the row at a pen and fills it in from the pen's stored target and
    /// the slaughter limit currently on screen.
    /// Calls: PenTargetStore.TryGet, <see cref="Place"/>, <see cref="RefreshTitle"/>.
    /// </summary>
    private static void Show(RowState state, AnimalPen pen)
    {
        state.Pen = pen;
        int limit = CurrentLimit(state);

        int value = PenTargetStore.TryGet(pen, out int stored) ? Math.Min(stored, limit) : OffValue;

        state.Row.SetActive(true);
        Place(state);
        state.Control.MinValue = OffValue;
        state.Control.MaxValue = limit;

        // true = do not raise OnValueChanged: showing the stored value must not
        // be mistaken for the player changing it.
        state.Control.SetValue(value, true);
        RefreshTitle(state);
    }

    /// <summary>Hides the row, e.g. when the panel switches to some other task.</summary>
    private static void Hide(RowState state)
    {
        state.Pen = null;
        if (state.Row != null) state.Row.SetActive(false);
    }

    /// <summary>
    /// The slaughter limit as the player currently sees it: the game's own
    /// control, which may be ahead of the saved task data while being edited.
    /// </summary>
    private static int CurrentLimit(RowState state)
    {
        var limitControl = state.Panel._quantityControlPanel;
        return limitControl != null ? Math.Max(0, limitControl.Value) : 0;
    }

    /// <summary>Titles the row, naming the male count that follows from it.</summary>
    private static void RefreshTitle(RowState state)
    {
        if (state.Title == null) return;

        if (state.Control.Value <= OffValue)
        {
            state.Title.text = "Females to keep  (off - vanilla 50/50)";
            return;
        }

        var targets = HerdTargets.Create(CurrentLimit(state), state.Control.Value);
        state.Title.text = $"Females to keep  (males: {targets.MalesToKeep})";
    }

    /// <summary>
    /// Handles the player changing the row: stores or clears the pen's target.
    /// Attached as the row's only OnValueChanged listener in <see cref="Build"/>.
    /// Calls: PenTargetStore.Set / Clear.
    /// </summary>
    private static void OnValueChanged(RowState state, int value)
    {
        try
        {
            var pen = state.Pen;
            if (pen == null) return;

            if (value <= OffValue) PenTargetStore.Clear(pen);
            else PenTargetStore.Set(pen, value);

            RefreshTitle(state);
            SlaughterLog.Write("targets_changed",
                $"pen={pen.Pointer.ToInt64():X} keep_f={(value <= OffValue ? "off" : value.ToString())} limit={CurrentLimit(state)}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"SPGT event=row_change_failed details={e}");
        }
    }

    // =====================================================================
    // Building
    // =====================================================================

    /// <summary>Returns the panel's row, building it the first time or after it was destroyed.</summary>
    private static RowState GetOrBuild(TaskDataPanel panel)
    {
        if (RowsByPanel.TryGetValue(panel.Pointer, out var state))
        {
            if (state.Row != null && state.Control != null) return state;

            // Destroyed with its menu; drop both index entries and rebuild.
            RowsByPanel.Remove(panel.Pointer);
            if (!ReferenceEquals(state.Control, null)) RowsByControl.Remove(state.Control.Pointer);
        }

        state = Build(panel);
        if (state == null) return null;

        RowsByPanel[panel.Pointer] = state;
        RowsByControl[state.Control.Pointer] = state;
        return state;
    }

    /// <summary>
    /// Clones the slaughter limit's div into a new "Females to keep" row.
    /// See the file header for why each step is there.
    /// Calls: <see cref="RelativePath"/>, <see cref="FindRelative"/>,
    /// <see cref="SilencePersistentListeners"/>, <see cref="LogLayout"/>.
    /// </summary>
    /// <returns>The new row, or null if the panel is not shaped as expected.</returns>
    private static RowState Build(TaskDataPanel panel)
    {
        var templateDiv = panel._quantityDiv;
        var templateControl = panel._quantityControlPanel;
        if (templateDiv == null || templateControl == null)
        {
            SlaughterLog.Write("row_unavailable", "reason=task_panel_has_no_quantity_row");
            return null;
        }

        var parent = templateDiv.transform.parent;
        string controlPath = RelativePath(templateDiv.transform, templateControl.transform);
        string titlePath = panel._quantityDivTitle != null
            ? RelativePath(templateDiv.transform, panel._quantityDivTitle.transform)
            : null;
        if (controlPath == null)
        {
            SlaughterLog.Write("row_unavailable", "reason=limit_control_is_not_inside_its_div");
            return null;
        }

        // Clone under an inactive holder: nothing in the copy wakes up until the
        // group link below has been cut.
        var holder = new GameObject("SPGT_RowHolder");
        holder.SetActive(false);
        holder.transform.SetParent(parent, false);

        var row = UnityEngine.Object.Instantiate(templateDiv, holder.transform);
        row.name = "SPGT_FemalesToKeepRow";

        var control = FindRelative(row.transform, controlPath)?.GetComponent<IncreaseDecreasePanel>();
        var title = titlePath != null ? FindRelative(row.transform, titlePath)?.GetComponent<TMP_Text>() : null;
        if (control == null)
        {
            UnityEngine.Object.Destroy(holder);
            SlaughterLog.Write("row_unavailable", "reason=cloned_control_not_found");
            return null;
        }

        control.group = null;
        SilencePersistentListeners(control.OnValueChanged);
        SilencePersistentListeners(control.OnIncrease);
        SilencePersistentListeners(control.OnDecrease);
        if (title != null) RemoveLocalizers(title.gameObject);

        var state = new RowState { Row = row, Control = control, Title = title, Panel = panel };
        control.OnValueChanged.AddListener(
            DelegateSupport.ConvertDelegate<UnityAction<int>>(new Action<int>(value => OnValueChanged(state, value))));

        // Move into place, right after the limit row, then wake it up.
        row.transform.SetParent(parent, false);
        row.transform.SetSiblingIndex(templateDiv.transform.GetSiblingIndex() + 1);
        UnityEngine.Object.Destroy(holder);

        if (!_layoutLogged)
        {
            _layoutLogged = true;
            LogLayout(templateDiv, title);
        }

        SlaughterLog.Write("row_built", $"panel={panel.Pointer.ToInt64():X}");
        return state;
    }

    /// <summary>
    /// Positions the row. With a layout group in charge the sibling order is
    /// enough; otherwise the row goes one row-height under the limit's div, plus
    /// the config offsets. Re-run on every Show because the game can move the
    /// limit's div (it does so for markets).
    /// </summary>
    private static void Place(RowState state)
    {
        var templateDiv = state.Panel._quantityDiv;
        if (templateDiv == null) return;

        var parent = templateDiv.transform.parent;
        if (parent != null && parent.GetComponent<LayoutGroup>() != null) return;

        var templateRect = templateDiv.GetComponent<RectTransform>();
        var rowRect = state.Row.GetComponent<RectTransform>();
        if (templateRect == null || rowRect == null) return;

        float height = templateRect.rect.height;
        rowRect.anchoredPosition = templateRect.anchoredPosition +
                                   new Vector2(Plugin.RowOffsetX.Value, -(height + RowSpacing) + Plugin.RowOffsetY.Value);
    }

    /// <summary>
    /// Switches off every listener serialized into a UnityEvent, so a cloned
    /// control cannot call anything the original was wired to in the editor.
    /// </summary>
    private static void SilencePersistentListeners(UnityEventBase unityEvent)
    {
        if (unityEvent == null) return;
        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
            unityEvent.SetPersistentListenerState(i, UnityEventCallState.Off);
    }

    /// <summary>
    /// Removes any localization component from the title, which would otherwise
    /// put the slaughter limit's localized title back over the row's own.
    /// </summary>
    private static void RemoveLocalizers(GameObject titleObject)
    {
        foreach (var component in titleObject.GetComponents<Component>())
        {
            string typeName = component.GetIl2CppType().Name;
            if (typeName.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) < 0) continue;

            // Destroy only takes effect at the end of the frame, after the row has
            // been activated; disabling first stops the localizer's OnEnable from
            // writing its text in the meantime.
            var behaviour = component.TryCast<Behaviour>();
            if (behaviour != null) behaviour.enabled = false;
            UnityEngine.Object.Destroy(component);
        }
    }

    /// <summary>
    /// The path from an ancestor to a descendant, in Transform.Find form
    /// ("" for the ancestor itself), or null if it is not a descendant.
    /// </summary>
    private static string RelativePath(Transform ancestor, Transform descendant)
    {
        var names = new List<string>();
        for (var current = descendant; current != null; current = current.parent)
        {
            if (current.Pointer == ancestor.Pointer)
            {
                names.Reverse();
                return string.Join("/", names);
            }
            names.Add(current.name);
        }
        return null;
    }

    /// <summary>Transform.Find, treating "" as the root itself.</summary>
    private static Transform FindRelative(Transform root, string path) =>
        path.Length == 0 ? root : root.Find(path);

    /// <summary>
    /// Writes the surroundings of the slaughter limit's div to the log once, so a
    /// misplaced row after a game update can be fixed from a player's log alone.
    /// </summary>
    private static void LogLayout(GameObject templateDiv, TMP_Text title)
    {
        try
        {
            var parent = templateDiv.transform.parent;
            var layout = parent?.GetComponent<LayoutGroup>();
            var text = new StringBuilder();
            text.Append($"parent={parent?.name} layout_group={(layout != null ? layout.GetIl2CppType().Name : "none")}");

            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    var rect = child.GetComponent<RectTransform>();
                    text.Append($" | [{i}] {child.name} active={child.gameObject.activeSelf}");
                    if (rect != null) text.Append($" pos={rect.anchoredPosition} size={rect.rect.size}");
                }
            }

            if (title != null)
            {
                text.Append(" | title_components=");
                foreach (var component in title.GetComponents<Component>())
                    text.Append(component.GetIl2CppType().Name).Append(',');
            }

            SlaughterLog.Write("row_layout", text.ToString());
        }
        catch (Exception e)
        {
            SlaughterLog.Write("row_layout", $"unavailable={e.Message}");
        }
    }
}
