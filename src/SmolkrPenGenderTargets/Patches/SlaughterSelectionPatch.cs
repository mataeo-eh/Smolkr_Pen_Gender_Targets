// SlaughterSelectionPatch.cs
//
// Role in the larger system: the one patch that changes gameplay.
//
// AnimalPen.GetSmolkrReadyForSlaughter is the single gate for pen slaughtering.
// The villager's slaughter quest (SmolkrPenSlaughterQuest) is offered whenever
// they are working the pen - its priority never looks at the animals - and the
// quest's FSM_Slaughter state calls this method at its START stage. If it
// returns false the attempt ends with nothing killed; if it returns true, the
// smolkr in its out parameter is the one the villager chases and kills. So this
// method decides both WHETHER a slaughter happens and WHICH smolkr dies, and
// nothing else in the game makes either decision (docs/DESIGN.md has the
// disassembly this is based on).
//
// For a pen with gender targets on, the prefix answers in vanilla's place using
// PenReader.Plan. For every other pen - and whenever anything goes wrong - it
// returns true and the untouched vanilla method runs.

using System;
using HarmonyLib;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;

namespace SmolkrPenGenderTargets.Patches;

[HarmonyPatch(typeof(AnimalPen), nameof(AnimalPen.GetSmolkrReadyForSlaughter))]
internal static class SlaughterSelectionPatch
{
    /// <summary>
    /// Replaces vanilla's slaughter choice for pens with gender targets on.
    /// Calls: PenTargetStore.TryGet, PenReader.Plan, SlaughterLog.Selected.
    /// </summary>
    /// <param name="__instance">The pen being asked for a smolkr to slaughter.</param>
    /// <param name="__0">The method's out parameter: the smolkr to slaughter.</param>
    /// <param name="__result">The method's return value: true when a smolkr was chosen.</param>
    /// <returns>False to skip vanilla (the mod answered), true to run vanilla.</returns>
    private static bool Prefix(AnimalPen __instance, ref DomesticSmolkr __0, ref bool __result)
    {
        if (!Plugin.Enabled.Value) return true;
        if (!PenTargetStore.TryGet(__instance, out int femalesToKeep)) return true;

        try
        {
            var decision = PenReader.Plan(__instance, femalesToKeep, out DomesticSmolkr victim);

            // No slaughter task or pet list yet: vanilla returns false here too,
            // so let it run rather than guess.
            if (decision == null) return true;

            __0 = victim;
            __result = victim != null;
            SlaughterLog.Selected(__instance, decision.Value, victim);
            return false;
        }
        catch (Exception e)
        {
            SlaughterLog.Failed(e);
            return true;
        }
    }
}
