// PenReader.cs
//
// Role in the larger system: the one place that reads a live AnimalPen out of
// the running game. It walks the pen's animals the same way ASKA's own
// GetSmolkrReadyForSlaughter does, turns each into a Core.PenAnimal, and runs
// the Core planner over them.
//
// The walk mirrors vanilla deliberately (see docs/DESIGN.md):
//   * the pen's population is Kennel._ownedPets.Count - every entry, pups
//     included, exactly the number vanilla compares with the slaughter limit;
//   * animals come from _ownedPets, in list order;
//   * an entry whose pet or GameObject is gone is skipped;
//   * an entry without a DomesticSmolkr component is skipped;
//   * maturity is Pet._mature and gender is DomesticSmolkr.isFemale.
// Pups are recorded as well as adults so the log can show a full head count;
// the planner itself only ever chooses an adult, as vanilla does.
//
// Kyckling pens are read by exactly this code. A kyckling pen is the same
// AnimalPen class (only its kycklingPen flag differs), and vanilla's selection
// method - the one this mirrors - is the only slaughter selection any pen has,
// so kycklings carry the same DomesticSmolkr component that holds isFemale.

using System.Collections.Generic;
using SmolkrPenGenderTargets.Core;
using SSSGame;

namespace SmolkrPenGenderTargets.Runtime;

internal static class PenReader
{
    /// <summary>
    /// Everything read out of a pen for one decision. <see cref="Smolkr"/> runs
    /// parallel to <see cref="Animals"/>: index i in one is index i in the other.
    /// </summary>
    internal sealed class PenSnapshot
    {
        internal readonly List<PenAnimal> Animals = new();
        internal readonly List<DomesticSmolkr> Smolkr = new();

        /// <summary>The game's slaughter limit for this pen.</summary>
        internal int SlaughterLimit;

        /// <summary>The pen's authored minimum population.</summary>
        internal int MinimumPopulation;

        /// <summary>Entries in _ownedPets, including any skipped as unusable.</summary>
        internal int OwnedPetCount;
    }

    /// <summary>
    /// Reads a pen's animals and settings.
    /// Called by: <see cref="Plan"/>.
    /// </summary>
    /// <param name="pen">The pen to read.</param>
    /// <returns>
    /// The snapshot, or null when the pen has no slaughter task or no pet list
    /// yet - the same cases where vanilla refuses to slaughter.
    /// </returns>
    internal static PenSnapshot Read(AnimalPen pen)
    {
        var ownedPets = pen._ownedPets;
        var quantity = pen._slaughterTaskData?.itemInfoQuantity;
        if (ownedPets == null || quantity == null) return null;

        var snapshot = new PenSnapshot
        {
            SlaughterLimit = quantity.quantity,
            MinimumPopulation = pen.minimumPetPopulation,
            OwnedPetCount = ownedPets.Count,
        };

        for (int i = 0; i < ownedPets.Count; i++)
        {
            var pet = ownedPets[i]?.pet;
            if (pet == null || pet.gameObject == null) continue;

            var smolkr = pet.GetComponent<DomesticSmolkr>();
            if (smolkr == null) continue;

            snapshot.Animals.Add(new PenAnimal(smolkr.isFemale, pet._mature));
            snapshot.Smolkr.Add(smolkr);
        }

        return snapshot;
    }

    /// <summary>
    /// Decides the next slaughter for a pen that has gender targets turned on.
    /// Calls: <see cref="Read"/>, CullPlanner.Decide.
    /// </summary>
    /// <param name="pen">The pen.</param>
    /// <param name="femalesToKeep">The pen's stored female target.</param>
    /// <param name="victim">The smolkr to slaughter, or null for none.</param>
    /// <returns>The decision, or null when the pen could not be read.</returns>
    internal static CullDecision? Plan(AnimalPen pen, int femalesToKeep, out DomesticSmolkr victim)
    {
        victim = null;
        var snapshot = Read(pen);
        if (snapshot == null) return null;

        var targets = HerdTargets.Create(snapshot.SlaughterLimit, femalesToKeep);
        var decision = CullPlanner.Decide(
            snapshot.Animals, targets, snapshot.OwnedPetCount, snapshot.MinimumPopulation);

        if (decision.HasVictim) victim = snapshot.Smolkr[decision.VictimIndex];
        return decision;
    }
}
