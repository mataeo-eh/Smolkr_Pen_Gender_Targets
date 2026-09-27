// CullPlanner.cs
//
// Role in the larger system: the mod's whole behavioural rule. The game-facing
// patch on AnimalPen.GetSmolkrReadyForSlaughter calls Decide() instead of running
// ASKA's own selection, for any pen where the player has turned gender targets on.
//
// How vanilla decides, for comparison (read out of the game binary; see
// docs/DESIGN.md): once the pen holds MORE animals than the slaughter limit, ASKA
// counts the grown-up females and males and slaughters one from whichever group
// is larger, a tie going to the males. That steers every pen toward a 50/50 split
// and has no notion of a target ratio - which is exactly what this rule replaces.
//
// The rule here:
//
//   1. Respect the pen's minimum population, as vanilla does.
//   2. Count each gender - adults only by default, pups too when countPups is on.
//   3. A gender with more animals than its target has a surplus.
//   4. Slaughter an ADULT of a surplus gender. If both have one, take the larger
//      surplus; on a tie, a male, mirroring vanilla's tie rule.
//   5. If the only surplus is made of pups, slaughter nothing and wait. Killing
//      an adult of the other gender instead would move the pen further from the
//      ratio the player asked for.
//
// Within the chosen gender the LAST matching animal in the pen's list is taken,
// again mirroring vanilla, so turning targets on changes which gender goes and
// nothing else about which individual.

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
        /// Every animal in the pen, in the game's own order. Null is an empty pen.
        /// </param>
        /// <param name="targets">Adults of each gender the player wants to keep.</param>
        /// <param name="minimumPopulation">
        /// The pen's authored floor. Nothing is slaughtered while the pen holds
        /// fewer animals than this, matching vanilla.
        /// </param>
        /// <param name="countPups">
        /// False (the default behaviour): targets count grown-ups only, so pups are
        /// ignored until they mature and a breeding herd is never thinned to make
        /// room for its own young. True: pups count toward their gender's target
        /// too, closer to how vanilla's slaughter limit counts every animal.
        /// </param>
        /// <returns>The decision, including the census and targets it was made from.</returns>
        public static CullDecision Decide(
            IReadOnlyList<PenAnimal> animals, HerdTargets targets, int minimumPopulation, bool countPups)
        {
            var census = HerdCensus.Count(animals);

            if (census.Total < minimumPopulation)
                return None(CullReason.BelowMinimumPopulation, census, targets);

            int femaleSurplus = Counted(census, female: true, countPups) - targets.FemalesToKeep;
            int maleSurplus = Counted(census, female: false, countPups) - targets.MalesToKeep;

            // A surplus only helps if there is a grown-up of that gender to take.
            bool femaleActionable = femaleSurplus > 0 && census.AdultFemales > 0;
            bool maleActionable = maleSurplus > 0 && census.AdultMales > 0;

            if (!femaleActionable && !maleActionable)
            {
                bool anySurplus = femaleSurplus > 0 || maleSurplus > 0;
                return None(anySurplus ? CullReason.SurplusIsOnlyPups : CullReason.WithinTargets, census, targets);
            }

            // Both genders over target: the bigger surplus goes first. Equal
            // surpluses go to the males, the same tie-break vanilla uses.
            bool takeFemale = femaleActionable && (!maleActionable || femaleSurplus > maleSurplus);

            int victim = FindLastAdult(animals, takeFemale);
            var reason = takeFemale ? CullReason.SurplusFemale : CullReason.SurplusMale;
            return new CullDecision(victim, reason, census, targets);
        }

        /// <summary>
        /// How many animals of a gender count toward its target.
        /// Called by: <see cref="Decide"/>.
        /// </summary>
        private static int Counted(HerdCensus census, bool female, bool countPups)
        {
            int count = census.Adults(female);
            if (countPups) count += census.Pups(female);
            return count;
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
