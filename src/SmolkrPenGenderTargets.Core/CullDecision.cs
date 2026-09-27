// CullDecision.cs
//
// Role in the larger system: the answer CullPlanner gives. The game-facing patch
// reads VictimIndex to know which smolkr to hand the slaughtering villager; the
// in-game panel and the log read Reason and Describe() to explain the choice.

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>Why a slaughter was or was not chosen.</summary>
    public enum CullReason
    {
        /// <summary>
        /// The pen holds fewer animals than its authored minimum population, a
        /// floor ASKA's own selection code also refuses to go below.
        /// </summary>
        BelowMinimumPopulation,

        /// <summary>Neither gender is over its target; nothing needs to go.</summary>
        WithinTargets,

        /// <summary>
        /// A gender is over its target only because of pups. Pups cannot be
        /// slaughtered, so the pen waits for them to grow up rather than killing
        /// an adult of the OTHER gender, which would break the ratio.
        /// </summary>
        SurplusIsOnlyPups,

        /// <summary>Adult females are over target; an adult female was chosen.</summary>
        SurplusFemale,

        /// <summary>Adult males are over target; an adult male was chosen.</summary>
        SurplusMale,
    }

    /// <summary>The outcome of one slaughter decision.</summary>
    public readonly struct CullDecision
    {
        /// <summary>
        /// Index into the list given to <see cref="CullPlanner.Decide"/> of the
        /// animal to slaughter, or -1 when nothing should be slaughtered.
        /// </summary>
        public int VictimIndex { get; }

        /// <summary>Why this decision was reached.</summary>
        public CullReason Reason { get; }

        /// <summary>The head count the decision was made from.</summary>
        public HerdCensus Census { get; }

        /// <summary>The per-gender targets the decision was made against.</summary>
        public HerdTargets Targets { get; }

        /// <summary>True when an animal was chosen for slaughter.</summary>
        public bool HasVictim => VictimIndex >= 0;

        /// <summary>Creates a decision. Used by <see cref="CullPlanner"/>.</summary>
        public CullDecision(int victimIndex, CullReason reason, HerdCensus census, HerdTargets targets)
        {
            VictimIndex = victimIndex;
            Reason = reason;
            Census = census;
            Targets = targets;
        }

        /// <summary>
        /// A one-line, player-readable explanation, shown in the in-game panel.
        /// </summary>
        public string Describe()
        {
            switch (Reason)
            {
                case CullReason.BelowMinimumPopulation:
                    return "Nothing - the pen is below its minimum population.";
                case CullReason.WithinTargets:
                    return "Nothing - both genders are at or under target.";
                case CullReason.SurplusIsOnlyPups:
                    return "Nothing yet - the extra animals are pups; waiting for them to grow up.";
                case CullReason.SurplusFemale:
                    return $"An adult female ({Census.AdultFemales} adult females, keeping {Targets.FemalesToKeep}).";
                case CullReason.SurplusMale:
                    return $"An adult male ({Census.AdultMales} adult males, keeping {Targets.MalesToKeep}).";
                default:
                    return Reason.ToString();
            }
        }
    }
}
