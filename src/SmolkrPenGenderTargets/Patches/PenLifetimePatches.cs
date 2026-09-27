// PenLifetimePatches.cs
//
// Role in the larger system: housekeeping. Pens are tracked by native pointer
// (see Runtime/PenTargetStore), so when a pen is destroyed - dismantled, or its
// world unloaded - its entries must go, or a pen built later at the same address
// would inherit them.
//
// AnimalPen does not override OnDestroy, so the patch sits on its base class
// Kennel and simply does nothing for kennels that are not pens.

using HarmonyLib;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;

namespace SmolkrPenGenderTargets.Patches;

[HarmonyPatch(typeof(Kennel), "OnDestroy")]
internal static class KennelDestroyPatch
{
    /// <summary>
    /// Forgets everything held about a pen that is being destroyed.
    /// Calls: PenTargetStore.Forget, SlaughterLog.Forget.
    /// </summary>
    /// <param name="__instance">The kennel (possibly a pen) being destroyed.</param>
    private static void Prefix(Kennel __instance)
    {
        if (__instance == null) return;
        PenTargetStore.Forget(__instance.Pointer);
        SlaughterLog.Forget(__instance.Pointer);
    }
}
