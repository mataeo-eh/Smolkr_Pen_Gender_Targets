// SlaughterLog.cs
//
// Role in the larger system: the mod's evidence trail. Every line carries the
// marker "SPGT" so ../../scripts/watch-log.ps1 can pull this mod's lines out of
// BepInEx's LogOutput.log.
//
// Two events matter for checking the mod works in game:
//
//   slaughter_selected  - the patch picked a smolkr for a villager to slaughter.
//   slaughter_confirmed - a smolkr the patch picked has actually died.
//
// A selected line followed by a confirmed line for the same pen proves the
// whole chain: the patch ran, the villager accepted the smolkr it was handed,
// and that smolkr is the one that died. A smolkr dying in a pen with targets on
// WITHOUT having been selected is logged as smolkr_died_unselected, which is how
// a gap in the patch (or an ordinary death, such as starvation) would show up.
//
// The slaughter FSM asks for a victim every time a villager starts the task, and
// it can abort and retry (no tool, no carry space) many times over. Repeating
// the same selection would bury the log, so a selection is only written when it
// differs from the last one written for that pen.

using System;
using System.Collections.Generic;
using SmolkrPenGenderTargets.Core;
using SSSGame;

namespace SmolkrPenGenderTargets.Runtime;

internal static class SlaughterLog
{
    /// <summary>The smolkr most recently selected in each pen, by native pointers.</summary>
    private static readonly Dictionary<IntPtr, IntPtr> SelectedSmolkrByPen = new();

    /// <summary>Set once a patch failure has been reported, so it is not repeated every attempt.</summary>
    private static bool _failureReported;

    /// <summary>Writes one tagged line to the BepInEx log.</summary>
    internal static void Write(string eventName, string details) =>
        Plugin.Log?.LogInfo($"SPGT event={eventName} {details}");

    /// <summary>
    /// Names a pen in a log line: its native pointer plus whether it is a smolkr
    /// or kyckling pen. Both are the same AnimalPen class, told apart only by the
    /// game's own kycklingPen flag, so the rest of the mod never needs to know
    /// which kind it has. The kind is logged so each can be checked in game.
    /// Called by: every log line that names a pen.
    /// </summary>
    /// <param name="pen">The pen to describe.</param>
    /// <returns>For example "pen=23F75307540 kind=kyckling".</returns>
    internal static string DescribePen(AnimalPen pen) =>
        $"pen={pen.Pointer.ToInt64():X} kind={(pen.kycklingPen ? "kyckling" : "smolkr")}";

    /// <summary>
    /// Records that the patch chose a smolkr (or chose none) for a pen.
    /// Called by: Patches.SlaughterSelectionPatch.
    /// </summary>
    internal static void Selected(AnimalPen pen, CullDecision decision, DomesticSmolkr victim)
    {
        IntPtr victimPointer = victim?.Pointer ?? IntPtr.Zero;
        bool known = SelectedSmolkrByPen.TryGetValue(pen.Pointer, out IntPtr previous);
        if (known && previous == victimPointer) return;

        SelectedSmolkrByPen[pen.Pointer] = victimPointer;

        var census = decision.Census;
        var targets = decision.Targets;
        Write(victim != null ? "slaughter_selected" : "slaughter_declined",
            $"{DescribePen(pen)} reason={decision.Reason} " +
            $"victim_female={(victim != null ? victim.isFemale.ToString() : "-")} " +
            $"adults_f={census.AdultFemales} adults_m={census.AdultMales} " +
            $"pups_f={census.FemalePups} pups_m={census.MalePups} " +
            $"keep_f={targets.FemalesToKeep} keep_m={targets.MalesToKeep} limit={targets.SlaughterLimit}");
    }

    /// <summary>
    /// Records a smolkr death in a pen that has targets turned on, confirming
    /// whether it was the one the patch selected.
    /// Called by: Patches.SmolkrDeathPatch.
    /// </summary>
    internal static void Died(AnimalPen pen, DomesticSmolkr smolkr)
    {
        bool wasSelected = SelectedSmolkrByPen.TryGetValue(pen.Pointer, out IntPtr selected)
                           && selected == smolkr.Pointer;

        Write(wasSelected ? "slaughter_confirmed" : "smolkr_died_unselected",
            $"{DescribePen(pen)} female={smolkr.isFemale}");

        // Whatever happens next is a fresh selection, so the next one is always written.
        if (wasSelected) SelectedSmolkrByPen.Remove(pen.Pointer);
    }

    /// <summary>
    /// Reports an exception from the selection patch. Only the first is written;
    /// every failing call still falls back to vanilla's choice on its own.
    /// </summary>
    internal static void Failed(Exception exception)
    {
        if (_failureReported) return;
        _failureReported = true;
        Plugin.Log?.LogError(
            "SPGT event=selection_failed - the vanilla slaughter choice is used whenever this " +
            $"happens; later failures are not reported again. Details: {exception}");
    }

    /// <summary>Drops tracking for a destroyed pen.</summary>
    internal static void Forget(IntPtr penPointer) => SelectedSmolkrByPen.Remove(penPointer);
}
