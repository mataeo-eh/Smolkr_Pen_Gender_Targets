// PenSavePatches.cs
//
// Role in the larger system: keeps each pen's gender target in the world save,
// alongside the pen's own slaughter limit.
//
// AnimalPen.SerializeTaskData returns the object that holds the pen's task
// settings - vanilla writes the slaughter limit into it as "tdCount" - and
// AnimalPen.DeserializeTaskData receives that same object back on load. This
// adds one integer key to it and reads that key back.
//
// Why this is safe for the save:
//   * vanilla reads its keys by name, so an extra key is ignored - the save
//     still loads if the mod is later removed, the pen simply goes back to
//     vanilla behaviour;
//   * a pen with targets off writes nothing, so a save made without ever
//     turning targets on is byte-for-byte what vanilla would write.

using System;
using HarmonyLib;
using SandSailorStudio.Storage;
using SmolkrPenGenderTargets.Runtime;
using SSSGame;

namespace SmolkrPenGenderTargets.Patches;

/// <summary>Names the save key both patches use.</summary>
internal static class PenSaveKeys
{
    /// <summary>
    /// The females-to-keep target. Prefixed with the mod's initials so it cannot
    /// collide with one of ASKA's own "td..." keys.
    /// </summary>
    internal const string FemalesToKeep = "spgt_femalesToKeep";
}

[HarmonyPatch(typeof(AnimalPen), nameof(AnimalPen.SerializeTaskData))]
internal static class PenSerializePatch
{
    /// <summary>
    /// Adds the pen's female target to its saved task data.
    /// Calls: PenTargetStore.TryGet, DataObject.AddElement.
    /// </summary>
    /// <param name="__instance">The pen being saved.</param>
    /// <param name="__result">The task data object vanilla just filled in.</param>
    private static void Postfix(AnimalPen __instance, DataObject __result)
    {
        try
        {
            if (__result == null) return;
            if (!PenTargetStore.TryGet(__instance, out int femalesToKeep)) return;

            __result.AddElement(PenSaveKeys.FemalesToKeep, new DataElementInt(femalesToKeep));
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"SPGT event=save_failed - this pen's gender target was not saved. Details: {e}");
        }
    }
}

[HarmonyPatch(typeof(AnimalPen), nameof(AnimalPen.DeserializeTaskData))]
internal static class PenDeserializePatch
{
    /// <summary>
    /// Restores a pen's female target from its saved task data.
    ///
    /// A missing key leaves the pen's current state alone rather than clearing
    /// it. That way, if the game ever calls this with partial data (a network
    /// update rather than a save load), a target the player just set survives.
    /// Calls: DataObject.TryGetElement, PenTargetStore.Set.
    /// </summary>
    /// <param name="__instance">The pen being loaded.</param>
    /// <param name="__0">The saved task data object.</param>
    private static void Postfix(AnimalPen __instance, DataObject __0)
    {
        try
        {
            if (__0 == null) return;
            if (!__0.TryGetElement(PenSaveKeys.FemalesToKeep, out DataElement element)) return;

            var stored = element?.TryCast<DataElementInt>();
            if (stored == null) return;

            PenTargetStore.Set(__instance, stored.value);
            SlaughterLog.Write("targets_loaded", $"pen={__instance.Pointer.ToInt64():X} keep_f={stored.value}");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"SPGT event=load_failed - this pen will use vanilla slaughtering. Details: {e}");
        }
    }
}
