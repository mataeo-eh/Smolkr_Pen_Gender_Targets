// PenTargetStore.cs
//
// Role in the larger system: remembers, for each smolkr pen in the loaded world,
// whether the player turned gender targets on and how many females to keep.
//
// A pen with no entry here is left entirely to vanilla. Entries are created by
// the "Females to keep" row (UI/FemalesToKeepRow), restored from the save by
// Patches/PenSavePatches, and dropped when the pen is destroyed
// (Patches/PenLifetimePatches).
//
// Pens are keyed by their native IL2CPP pointer. Il2CppInterop hands out a new
// managed wrapper object each time a pen is fetched from the game, so the
// wrapper itself cannot be used as a key; the pointer is what stays the same for
// the life of the pen. The destroy hook is what stops a later pen that happens
// to be allocated at the same address from inheriting a stale entry.

using System;
using System.Collections.Generic;
using SSSGame;

namespace SmolkrPenGenderTargets.Runtime;

internal static class PenTargetStore
{
    /// <summary>Females to keep, by native pen pointer. Absent = targets off.</summary>
    private static readonly Dictionary<IntPtr, int> FemalesToKeepByPen = new();

    /// <summary>
    /// Reads a pen's female target.
    /// </summary>
    /// <param name="pen">The pen; null reads as "targets off".</param>
    /// <param name="femalesToKeep">The stored target, unclamped.</param>
    /// <returns>True when the pen has gender targets turned on.</returns>
    internal static bool TryGet(AnimalPen pen, out int femalesToKeep)
    {
        femalesToKeep = 0;
        return pen != null && FemalesToKeepByPen.TryGetValue(pen.Pointer, out femalesToKeep);
    }

    /// <summary>Turns targets on for a pen, or changes its female target.</summary>
    internal static void Set(AnimalPen pen, int femalesToKeep)
    {
        if (pen == null) return;
        FemalesToKeepByPen[pen.Pointer] = Math.Max(0, femalesToKeep);
    }

    /// <summary>Turns targets off for a pen, returning it to vanilla behaviour.</summary>
    internal static void Clear(AnimalPen pen)
    {
        if (pen == null) return;
        FemalesToKeepByPen.Remove(pen.Pointer);
    }

    /// <summary>Drops any entry for a pen that is being destroyed.</summary>
    /// <param name="penPointer">The native pointer of the destroyed pen.</param>
    internal static void Forget(IntPtr penPointer) => FemalesToKeepByPen.Remove(penPointer);
}
