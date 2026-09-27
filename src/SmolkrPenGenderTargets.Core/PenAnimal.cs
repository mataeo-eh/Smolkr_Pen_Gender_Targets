// PenAnimal.cs
//
// Role in the larger system: the only facts about a penned animal that the
// slaughter decision needs. The game-facing project builds one of these per
// smolkr it finds in a pen, in the same order the game stores them, and hands
// the list to CullPlanner.
//
// Keeping this to two booleans is deliberate - it is everything ASKA's own
// selection code looks at (see docs/DESIGN.md), so the mod's rule cannot quietly
// depend on something vanilla ignores.

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>One animal in a pen, reduced to what the slaughter rule reads.</summary>
    public readonly struct PenAnimal
    {
        /// <summary>True for a female, false for a male.</summary>
        public bool IsFemale { get; }

        /// <summary>
        /// True once the animal has grown up. ASKA never slaughters a pup, so only
        /// mature animals can ever be chosen.
        /// </summary>
        public bool IsMature { get; }

        /// <summary>Creates a pen animal description.</summary>
        /// <param name="isFemale">True for a female, false for a male.</param>
        /// <param name="isMature">True for an adult, false for a pup.</param>
        public PenAnimal(bool isFemale, bool isMature)
        {
            IsFemale = isFemale;
            IsMature = isMature;
        }

        /// <summary>Convenience factory for an adult female.</summary>
        public static PenAnimal AdultFemale => new PenAnimal(isFemale: true, isMature: true);

        /// <summary>Convenience factory for an adult male.</summary>
        public static PenAnimal AdultMale => new PenAnimal(isFemale: false, isMature: true);

        /// <summary>Convenience factory for a female pup.</summary>
        public static PenAnimal FemalePup => new PenAnimal(isFemale: true, isMature: false);

        /// <summary>Convenience factory for a male pup.</summary>
        public static PenAnimal MalePup => new PenAnimal(isFemale: false, isMature: false);
    }
}
