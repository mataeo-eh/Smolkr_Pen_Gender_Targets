// CullDecision.cs
//
// Role in the larger system: the answer CullPlanner gives. The game-facing patch
// reads VictimIndex to know which smolkr to hand the slaughtering villager; the
// log reads Reason and Describe() to explain the choice.

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>Why a slaughter was or was not chosen.</summary>
    public enum CullReason
    {
        /// <summary>
        /// The pen holds no more animals than its slaughter limit (pups
        /// included, as in vanilla), so nothing needs to go.
        /// </summary>
        WithinLimit,

        /// <summary>
        /// The pen holds fewer animals than its authored minimum population, a
        /// floor ASKA's own selection code also refuses to go below.
        /// </summary>
        BelowMinimumPopulation,

        /// <summary>
        /// The pen is over its limit, but every animal is a pup. Pups cannot be
        /// slaughtered, in vanilla or here.
        /// </summary>
        NoAdults,

        /// <summary>Adult females are furthest above their target; an adult female was chosen.</summary>
        FemaleChosen,

        /// <summary>Adult males are furthest above their target (or tied); an adult male was chosen.</summary>
        MaleChosen,
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

        /// <summary>A one-line, player-readable explanation of the decision.</summary>
        public string Describe()
        {
            switch (Reason)
            {
                case CullReason.WithinLimit:
                    return $"Nothing - the pen holds {Census.Total} of {Targets.SlaughterLimit} allowed.";
                case CullReason.BelowMinimumPopulation:
                    return "Nothing - the pen is below its minimum population.";
                case CullReason.NoAdults:
                    return "Nothing - over the limit, but only pups are left.";
                case CullReason.FemaleChosen:
                    return $"An adult female ({Census.AdultFemales} adult females for {Targets.FemalesToKeep} wanted, " +
                           $"{Census.AdultMales} adult males for {Targets.MalesToKeep} wanted).";
                case CullReason.MaleChosen:
                    return $"An adult male ({Census.AdultMales} adult males for {Targets.MalesToKeep} wanted, " +
                           $"{Census.AdultFemales} adult females for {Targets.FemalesToKeep} wanted).";
                default:
                    return Reason.ToString();
            }
        }
    }
}
