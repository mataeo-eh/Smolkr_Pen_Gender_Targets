// SmolkrDeathPatch.cs
//
// Role in the larger system: diagnostics only - it changes nothing in game.
// Watches smolkr deaths in pens that have gender targets on, so the log can
// confirm that the smolkr the selection patch picked is the one that died
// (see Runtime/SlaughterLog for how to read the result).
//
// This matters because the selection patch hands its choice back through an
// "out" parameter, which Il2CppInterop has to copy back into native memory. A
// confirmed line is the in-game proof that the copy worked.
//
// DomesticSmolkr._HandleCreatureDead is the smolkr's own death handler. The
// patch is a prefix so the smolkr's link to its pen is read before the handler
// has a chance to clear it.

using System;
using HarmonyLib;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;

namespace SmolkrPenGenderTargets.Patches;

[HarmonyPatch(typeof(DomesticSmolkr), "_HandleCreatureDead")]
internal static class SmolkrDeathPatch
{
    /// <summary>
    /// Logs the death of a smolkr in a pen with gender targets on.
    /// Calls: PenTargetStore.TryGet, SlaughterLog.Died.
    /// </summary>
    /// <param name="__instance">The smolkr that died.</param>
    private static void Prefix(DomesticSmolkr __instance)
    {
        try
        {
            var pen = __instance?._kennel?.TryCast<AnimalPen>();
            if (pen == null || !PenTargetStore.TryGet(pen, out _)) return;
            SlaughterLog.Died(pen, __instance);
        }
        catch (Exception e)
        {
            // Never let a diagnostic break a death.
            Plugin.Log.LogWarning($"SPGT event=death_log_failed details={e.Message}");
        }
    }
}
