// HerdCensus.cs
//
// Role in the larger system: a head count of a pen by gender and age. CullPlanner
// decides from it, and the log (Runtime/SlaughterLog) records it with every
// decision so a slaughter - or the lack of one - can be explained afterwards.

using System.Collections.Generic;

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>Counts of the animals in a pen, split by gender and maturity.</summary>
    public readonly struct HerdCensus
    {
        /// <summary>Grown-up females.</summary>
        public int AdultFemales { get; }

        /// <summary>Grown-up males.</summary>
        public int AdultMales { get; }

        /// <summary>Female pups.</summary>
        public int FemalePups { get; }

        /// <summary>Male pups.</summary>
        public int MalePups { get; }

        /// <summary>Every animal counted, of any age or gender.</summary>
        public int Total => AdultFemales + AdultMales + FemalePups + MalePups;

        private HerdCensus(int adultFemales, int adultMales, int femalePups, int malePups)
        {
            AdultFemales = adultFemales;
            AdultMales = adultMales;
            FemalePups = femalePups;
            MalePups = malePups;
        }

        /// <summary>Counts a list of pen animals.</summary>
        /// <param name="animals">The pen's animals; null is treated as an empty pen.</param>
        /// <returns>The head count.</returns>
        public static HerdCensus Count(IReadOnlyList<PenAnimal> animals)
        {
            int adultFemales = 0, adultMales = 0, femalePups = 0, malePups = 0;
            if (animals == null) return new HerdCensus(0, 0, 0, 0);

            foreach (var animal in animals)
            {
                if (animal.IsMature && animal.IsFemale) adultFemales++;
                else if (animal.IsMature) adultMales++;
                else if (animal.IsFemale) femalePups++;
                else malePups++;
            }

            return new HerdCensus(adultFemales, adultMales, femalePups, malePups);
        }

        /// <summary>Grown-up animals of one gender.</summary>
        /// <param name="female">True for females, false for males.</param>
        public int Adults(bool female) => female ? AdultFemales : AdultMales;

        /// <summary>Pups of one gender.</summary>
        /// <param name="female">True for females, false for males.</param>
        public int Pups(bool female) => female ? FemalePups : MalePups;
    }
}
