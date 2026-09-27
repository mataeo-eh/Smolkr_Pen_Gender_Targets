// GenderTargetsPanel.cs
//
// Role in the larger system: the player's control for a pen's gender targets.
// It appears on its own whenever a smolkr pen's slaughter task is open in ASKA's
// workstation menu (TaskPanelTracker decides that) and disappears when it closes.
//
// It is drawn with Unity's immediate-mode GUI rather than injected into ASKA's
// own task panel, following AskaEnemyScaling's precedent: IMGUI does not depend
// on the game's UI prefabs or menu hierarchy, so a game update that reshuffles
// the task panel cannot break it. Native integration into the task panel is a
// possible follow-up once this version has been confirmed in game.
//
// What it shows, top to bottom:
//   * an on/off toggle for the pen (off = vanilla slaughtering);
//   * the game's own slaughter limit, females to keep (editable), and males to
//     keep (limit minus females - not separately editable, so the two can never
//     disagree with the limit);
//   * a head count of the pen, and what will be slaughtered next and why - the
//     same PenReader.Plan call the villager's patch makes, so the prediction and
//     the behaviour cannot drift apart.
//
// Depends on: TaskPanelTracker, Runtime.PenTargetStore, Runtime.PenReader.
// Created by: Plugin.CreatePanelHost.

using System;
using Il2CppInterop.Runtime.Attributes;
using SmolkrPenGenderTargets.Core;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;
using UnityEngine;

namespace SmolkrPenGenderTargets.UI;

/// <summary>An IMGUI window shown beside a smolkr pen's slaughter task.</summary>
public sealed class GenderTargetsPanel : MonoBehaviour
{
    /// <summary>Required by Il2CppInterop when Unity constructs the component.</summary>
    public GenderTargetsPanel(IntPtr pointer) : base(pointer) { }

    private const int WindowId = 0x53504754; // "SPGT"; any value unique among windows
    private const float WindowWidth = 340f;
    private const float WindowHeight = 250f;
    private const float StepButtonWidth = 30f;
    private const float LabelWidth = 130f;

    private Rect _bounds;
    private bool _positioned;

    /// <summary>Set once GUI.Window has been found unusable, after which the fallback draws instead.</summary>
    private bool _useFallbackWindow;

    // The pen being drawn this frame, and a plan cached for that frame. Unity
    // calls OnGUI several times per frame (layout, repaint, input), and the plan
    // walks every animal in the pen, so it is worked out once per frame.
    private AnimalPen _pen;
    private CullDecision? _plan;
    private int _planFrame = -1;
    private IntPtr _planPen;

    /// <summary>
    /// Unity IMGUI callback. Draws the window while a pen's slaughter task is open.
    ///
    /// Must be public and must NOT carry [HideFromIl2Cpp]: Unity finds it on the
    /// IL2CPP class Il2CppInterop generates for this type, and a hidden OnGUI is
    /// silently never called.
    /// Calls: <see cref="DrawWindow"/> through GUI.Window, or <see cref="DrawFallbackWindow"/>.
    /// </summary>
    public void OnGUI()
    {
        if (!Plugin.ShowPanel.Value) return;
        if (!TaskPanelTracker.IsShowing(out _pen)) return;

        if (!_positioned)
        {
            // Right-hand side, clear of ASKA's centred workstation menu. The
            // player can drag it anywhere; that position then holds for the session.
            _bounds = new Rect(Screen.width - WindowWidth - 40f, 140f, WindowWidth, WindowHeight);
            _positioned = true;
        }

        if (_useFallbackWindow)
        {
            DrawFallbackWindow();
            return;
        }

        // GUI.Window needs the managed callback marshalled into IL2CPP as a
        // delegate. If that conversion is unavailable, draw a fixed box instead
        // of leaving the player with nothing.
        try
        {
            _bounds = GUI.Window(WindowId, _bounds, (GUI.WindowFunction)DrawWindow, "Smolkr gender targets");
        }
        catch (Exception e)
        {
            _useFallbackWindow = true;
            Plugin.Log.LogWarning(
                "GUI.Window is not usable in this build, so the gender targets panel will be drawn " +
                $"in a fixed position and cannot be dragged. Details: {e.Message}");
        }
    }

    /// <summary>
    /// Draws the panel without GUI.Window. Used only when the window callback
    /// cannot be marshalled; the panel then cannot be dragged.
    /// Calls: <see cref="DrawWindow"/>.
    /// </summary>
    [HideFromIl2Cpp]
    private void DrawFallbackWindow()
    {
        GUI.Box(_bounds, "Smolkr gender targets");
        var inner = new Rect(_bounds.x + 10f, _bounds.y + 24f, _bounds.width - 20f, _bounds.height - 34f);
        GUILayout.BeginArea(inner);
        DrawWindow(WindowId);
        GUILayout.EndArea();
    }

    /// <summary>
    /// Draws the window contents for the pen in <see cref="_pen"/>.
    /// Calls: <see cref="DrawToggle"/>, <see cref="DrawTargets"/>, <see cref="DrawStatus"/>.
    /// </summary>
    /// <param name="id">Window id supplied by Unity; unused.</param>
    [HideFromIl2Cpp]
    private void DrawWindow(int id)
    {
        try
        {
            if (!Plugin.Enabled.Value)
            {
                GUILayout.Label("The mod is switched off in its config file (General > Enabled), " +
                                "so every pen is using vanilla slaughtering.");
            }

            bool on = DrawToggle();
            GUILayout.Space(6f);

            if (on) DrawTargets();
            else GUILayout.Label("Off: this pen uses the game's own choice, which slaughters from " +
                                 "whichever gender has more adults.");

            GUILayout.Space(6f);
            DrawStatus(on);
        }
        catch (Exception e)
        {
            // A drawing error must not spam the log every frame, nor take the
            // game's own menu down with it - show it once and stop drawing.
            GUILayout.Label("Panel error - see the BepInEx log.");
            Plugin.Log.LogError($"SPGT event=panel_failed details={e}");
            Plugin.ShowPanel.Value = false;
        }

        if (!_useFallbackWindow) GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
    }

    /// <summary>
    /// Draws the on/off toggle and applies a change. Turning targets on starts
    /// from "keep one male, the rest female", the usual breeding setup.
    /// Calls: PenTargetStore.TryGet / Set / Clear.
    /// </summary>
    /// <returns>True when targets are on for the pen after this frame.</returns>
    [HideFromIl2Cpp]
    private bool DrawToggle()
    {
        bool wasOn = PenTargetStore.TryGet(_pen, out _);
        bool isOn = GUILayout.Toggle(wasOn, " Use gender targets for this pen");

        if (isOn && !wasOn)
        {
            int limit = PenReader.Read(_pen)?.SlaughterLimit ?? 0;
            PenTargetStore.Set(_pen, Math.Max(0, limit - 1));
        }
        else if (!isOn && wasOn)
        {
            PenTargetStore.Clear(_pen);
        }

        return isOn;
    }

    /// <summary>
    /// Draws the limit, the editable female target and the derived male target.
    /// Calls: PenTargetStore.TryGet / Set, HerdTargets.Create.
    /// </summary>
    [HideFromIl2Cpp]
    private void DrawTargets()
    {
        PenTargetStore.TryGet(_pen, out int storedFemales);
        int limit = PenReader.Read(_pen)?.SlaughterLimit ?? 0;
        var targets = HerdTargets.Create(limit, storedFemales);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Slaughter limit", GUILayout.Width(LabelWidth));
        GUILayout.Label($"{limit}   (the game's own setting)");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Females to keep", GUILayout.Width(LabelWidth));
        if (GUILayout.Button("-", GUILayout.Width(StepButtonWidth)))
            PenTargetStore.Set(_pen, Math.Max(0, targets.FemalesToKeep - 1));
        GUILayout.Label($"  {targets.FemalesToKeep}  ", GUILayout.ExpandWidth(false));
        if (GUILayout.Button("+", GUILayout.Width(StepButtonWidth)))
            PenTargetStore.Set(_pen, Math.Min(limit, targets.FemalesToKeep + 1));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Males to keep", GUILayout.Width(LabelWidth));
        GUILayout.Label($"{targets.MalesToKeep}   (limit minus females)");
        GUILayout.EndHorizontal();

        // The stored number is kept when the limit is lowered below it, and only
        // its effect is capped - say so, or the display looks like it ignored a click.
        if (storedFemales > limit)
            GUILayout.Label($"Set to keep {storedFemales} females; capped at the limit of {limit}.");
    }

    /// <summary>
    /// Draws the pen's head count and what happens next.
    /// Calls: <see cref="GetPlan"/>.
    /// </summary>
    /// <param name="on">Whether targets are on; when off, only the head count is shown.</param>
    [HideFromIl2Cpp]
    private void DrawStatus(bool on)
    {
        var snapshot = PenReader.Read(_pen);
        if (snapshot == null)
        {
            GUILayout.Label("This pen has no slaughter task yet.");
            return;
        }

        var census = HerdCensus.Count(snapshot.Animals);
        GUILayout.Label($"In the pen: {census.AdultFemales} adult females, {census.AdultMales} adult males, " +
                        $"{census.FemalePups + census.MalePups} pups");

        if (!on || !Plugin.Enabled.Value) return;

        var plan = GetPlan();
        if (plan.HasValue) GUILayout.Label($"Next slaughter: {plan.Value.Describe()}");
    }

    /// <summary>
    /// The pen's current plan, worked out at most once per frame.
    /// Calls: PenReader.Plan.
    /// </summary>
    [HideFromIl2Cpp]
    private CullDecision? GetPlan()
    {
        if (_planFrame == Time.frameCount && _planPen == _pen.Pointer) return _plan;

        _plan = PenTargetStore.TryGet(_pen, out int females) ? PenReader.Plan(_pen, females, out _) : null;
        _planFrame = Time.frameCount;
        _planPen = _pen.Pointer;
        return _plan;
    }
}
