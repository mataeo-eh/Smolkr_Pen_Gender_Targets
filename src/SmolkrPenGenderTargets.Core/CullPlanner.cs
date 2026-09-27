// CullPlanner.cs
//
// Role in the larger system: the mod's whole behavioural rule. The game-facing
// patch on AnimalPen.GetSmolkrReadyForSlaughter calls Decide() instead of running
// ASKA's own selection, for any pen where the player has turned gender targets on.
//
// The rule is vanilla's rule with exactly one thing changed. Vanilla, read out of
// the game binary (docs/DESIGN.md):
//
//   1. Slaughter only while the pen holds MORE animals than the slaughter limit.
//      Every animal counts here, pups included.
//   2. ...and never below the pen's minimum population.
//   3. Only a grown-up can be slaughtered. Count the adult females and adult
//      males (pups are not part of this comparison) and take one from the gender
//      that has more; a tie goes to the males.
//   4. Within that gender, take the LAST adult in the pen's list.
//
// This mod keeps 1, 2 and 4 word for word. Step 3 becomes: take one from the
// gender that is furthest ABOVE ITS TARGET ("surplus" = adults - adults wanted),
// a tie still going to the males. When the targets are an even split, the surplus
// comparison is the same comparison vanilla makes (females - keep > males - keep
// exactly when females > males), so an even split reproduces vanilla exactly -
// the unit tests check this against a transcription of the vanilla method. The
// only thing the player's targets change is where the pen settles: a chosen
// ratio instead of 50/50.

using System.Collections.Generic;

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>Chooses which animal, if any, a pen should slaughter next.</summary>
    public static class CullPlanner
    {
        /// <summary>
        /// Decides the next slaughter for a pen.
        /// Calls: <see cref="HerdCensus.Count"/>, <see cref="FindLastAdult"/>.
        /// </summary>
        /// <param name="animals">
        /// The pen's smolkr, in the game's own order. Null is an empty pen.
        /// </param>
        /// <param name="targets">Adults of each gender the player wants to keep.</param>
        /// <param name="penPopulation">
        /// How many animals the pen holds by the game's own count (its owned-pets
        /// list, pups included). Usually animals.Count; passed separately because
        /// the game also counts list entries it cannot read a gender from.
        /// </param>
        /// <param name="minimumPopulation">The pen's authored population floor.</param>
        /// <returns>The decision, including the census and targets it was made from.</returns>
        public static CullDecision Decide(
            IReadOnlyList<PenAnimal> animals, HerdTargets targets, int penPopulation, int minimumPopulation)
        {
            var census = HerdCensus.Count(animals);

            // Steps 1 and 2, unchanged from vanilla and in vanilla's order.
            if (penPopulation <= targets.SlaughterLimit)
                return None(CullReason.WithinLimit, census, targets);
            if (penPopulation < minimumPopulation)
                return None(CullReason.BelowMinimumPopulation, census, targets);

            // Step 3: vanilla compares adult counts; the mod compares each adult
            // count against its target. Only a gender with an adult can be chosen.
            bool femaleAvailable = census.AdultFemales > 0;
            bool maleAvailable = census.AdultMales > 0;
            if (!femaleAvailable && !maleAvailable)
                return None(CullReason.NoAdults, census, targets);

            int femaleSurplus = census.AdultFemales - targets.FemalesToKeep;
            int maleSurplus = census.AdultMales - targets.MalesToKeep;
            bool takeFemale = femaleAvailable && (!maleAvailable || femaleSurplus > maleSurplus);

            // Step 4, unchanged.
            int victim = FindLastAdult(animals, takeFemale);
            var reason = takeFemale ? CullReason.FemaleChosen : CullReason.MaleChosen;
            return new CullDecision(victim, reason, census, targets);
        }

        /// <summary>
        /// Index of the last grown-up of the given gender, or -1 if there is none.
        /// Called by: <see cref="Decide"/>, only after the census has confirmed at
        /// least one such adult exists.
        /// </summary>
        private static int FindLastAdult(IReadOnlyList<PenAnimal> animals, bool female)
        {
            for (int i = animals.Count - 1; i >= 0; i--)
            {
                if (animals[i].IsMature && animals[i].IsFemale == female) return i;
            }
            return -1;
        }

        /// <summary>Builds a "slaughter nothing" decision.</summary>
        private static CullDecision None(CullReason reason, HerdCensus census, HerdTargets targets) =>
            new CullDecision(-1, reason, census, targets);
    }
}
