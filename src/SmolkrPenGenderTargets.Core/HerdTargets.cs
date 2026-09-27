// HerdTargets.cs
//
// Role in the larger system: turns the two numbers the player controls - the
// game's own "slaughter limit" and this mod's "females to keep" - into a target
// for each gender.
//
// The player never sets the male count directly. It is always
// "slaughter limit - females to keep", which means the two targets can never
// disagree with the game's own limit: keep 5 females with a limit of 6 and the
// male target is 1, change the limit to 8 and it becomes 3.

using System;

namespace SmolkrPenGenderTargets.Core
{
    /// <summary>How many adults of each gender a pen should keep.</summary>
    public readonly struct HerdTargets
    {
        /// <summary>The game's own slaughter limit for the pen, never negative.</summary>
        public int SlaughterLimit { get; }

        /// <summary>Adult females to keep, clamped to 0..<see cref="SlaughterLimit"/>.</summary>
        public int FemalesToKeep { get; }

        /// <summary>Adult males to keep: whatever the limit has left after the females.</summary>
        public int MalesToKeep => SlaughterLimit - FemalesToKeep;

        /// <summary>
        /// Builds targets from the player's two settings, clamping anything out of
        /// range rather than rejecting it.
        ///
        /// Clamping matters because the two numbers are edited in different places
        /// at different times: a player can lower the game's slaughter limit below
        /// a female count they chose earlier. The stored female count is left alone
        /// and only its effect is capped, so raising the limit again restores it.
        /// </summary>
        /// <param name="slaughterLimit">The pen's slaughter limit from the game.</param>
        /// <param name="femalesToKeep">The player's female target for this pen.</param>
        /// <returns>Targets where 0 &lt;= females &lt;= limit and males = limit - females.</returns>
        public static HerdTargets Create(int slaughterLimit, int femalesToKeep)
        {
            int limit = Math.Max(0, slaughterLimit);
            int females = Math.Min(Math.Max(0, femalesToKeep), limit);
            return new HerdTargets(limit, females);
        }

        private HerdTargets(int slaughterLimit, int femalesToKeep)
        {
            SlaughterLimit = slaughterLimit;
            FemalesToKeep = femalesToKeep;
        }

        /// <summary>The target for one gender.</summary>
        /// <param name="female">True for the female target, false for the male one.</param>
        public int ToKeep(bool female) => female ? FemalesToKeep : MalesToKeep;
    }
}
