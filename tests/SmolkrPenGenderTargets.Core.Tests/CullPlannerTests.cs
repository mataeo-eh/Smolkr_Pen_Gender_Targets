// CullPlannerTests.cs
//
// Role in the larger system: pins down the mod's slaughter rule (CullPlanner)
// and target arithmetic (HerdTargets) so a later change cannot silently alter
// which smolkr a villager kills.

using System.Collections.Generic;
using SmolkrPenGenderTargets.Core;
using Xunit;

namespace SmolkrPenGenderTargets.Core.Tests
{
    public class CullPlannerTests
    {
        private static readonly PenAnimal F = PenAnimal.AdultFemale;
        private static readonly PenAnimal M = PenAnimal.AdultMale;
        private static readonly PenAnimal FPup = PenAnimal.FemalePup;
        private static readonly PenAnimal MPup = PenAnimal.MalePup;

        /// <summary>Runs the planner with no minimum population and adults-only counting.</summary>
        private static CullDecision Decide(int limit, int females, params PenAnimal[] pen) =>
            CullPlanner.Decide(pen, HerdTargets.Create(limit, females), minimumPopulation: 0, countPups: false);

        // --- The situation the mod exists for ---------------------------------

        [Fact]
        public void SixFemalesWithFiveOneTarget_SlaughtersAFemale()
        {
            // Vanilla would see 6 animals against a limit of 6 and do nothing,
            // leaving a pen with no male at all. Targets of 5F/1M make one female
            // surplus.
            var decision = Decide(6, 5, F, F, F, F, F, F);

            Assert.Equal(CullReason.SurplusFemale, decision.Reason);
            Assert.True(decision.HasVictim);
        }

        [Fact]
        public void SevenMalesWithFiveOneTarget_SlaughtersAMale()
        {
            var decision = Decide(6, 5, M, M, M, M, M, M, M);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
        }

        [Fact]
        public void ExactlyOnTarget_SlaughtersNothing()
        {
            var decision = Decide(6, 5, F, F, M, F, F, F);

            Assert.Equal(CullReason.WithinTargets, decision.Reason);
            Assert.Equal(-1, decision.VictimIndex);
        }

        [Fact]
        public void UnderTargetInOneGender_DoesNotTakeFromTheOther()
        {
            // 4F/2M against 5F/1M: males are over, so a male goes even though the
            // pen as a whole is at the limit and females are short.
            var decision = Decide(6, 5, F, F, F, F, M, M);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
        }

        [Fact]
        public void NoMalesWanted_EveryMaleIsSurplus()
        {
            var decision = Decide(4, 4, F, F, F, F, M);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
        }

        // --- Which individual ----------------------------------------------------

        [Fact]
        public void Victim_IsTheLastAdultOfTheSurplusGender()
        {
            // Vanilla takes the last matching animal in the pen's list; the mod
            // keeps that so only the GENDER choice changes.
            var decision = Decide(2, 1, F, M, F, M, FPup);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
            Assert.Equal(3, decision.VictimIndex);
        }

        [Fact]
        public void Victim_IsNeverAPup()
        {
            // Keep 1 female and 0 males: the adult male is surplus. The male pup
            // after him in the list is the "last male", but must not be chosen.
            var decision = Decide(1, 1, MPup, M, MPup);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
            Assert.Equal(1, decision.VictimIndex);
        }

        // --- Both genders over target --------------------------------------------

        [Fact]
        public void BothOver_LargerSurplusGoesFirst()
        {
            // Target 2F/2M; pen 5F (surplus 3) and 3M (surplus 1).
            var decision = Decide(4, 2, F, F, F, F, F, M, M, M);

            Assert.Equal(CullReason.SurplusFemale, decision.Reason);
        }

        [Fact]
        public void BothOverEqually_MaleGoesFirst_LikeVanilla()
        {
            var decision = Decide(4, 2, F, F, F, M, M, M);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
        }

        // --- Pups ---------------------------------------------------------------

        [Fact]
        public void PupsAreIgnoredByDefault()
        {
            // A full breeding herd plus a male pup: nothing goes. The pup is
            // judged when it grows up, so the only adult male is not killed to
            // make room for his own offspring.
            var decision = Decide(6, 5, F, F, F, F, F, M, MPup);

            Assert.Equal(CullReason.WithinTargets, decision.Reason);
        }

        [Fact]
        public void CountPups_PupCanMakeItsGenderSurplus()
        {
            var pen = new[] { F, F, F, F, F, M, MPup };
            var decision = CullPlanner.Decide(pen, HerdTargets.Create(6, 5), 0, countPups: true);

            Assert.Equal(CullReason.SurplusMale, decision.Reason);
            Assert.Equal(5, decision.VictimIndex);
        }

        [Fact]
        public void CountPups_SurplusMadeOnlyOfPups_WaitsInsteadOfTakingTheOtherGender()
        {
            // No male should be wanted, but the only males are pups. Slaughtering
            // a female instead would push the pen further from the target.
            var pen = new[] { F, F, MPup };
            var decision = CullPlanner.Decide(pen, HerdTargets.Create(2, 2), 0, countPups: true);

            Assert.Equal(CullReason.SurplusIsOnlyPups, decision.Reason);
            Assert.False(decision.HasVictim);
        }

        // --- Minimum population ------------------------------------------------

        [Fact]
        public void BelowMinimumPopulation_SlaughtersNothing()
        {
            var pen = new[] { M, M, M };
            var decision = CullPlanner.Decide(pen, HerdTargets.Create(1, 1), minimumPopulation: 4, countPups: false);

            Assert.Equal(CullReason.BelowMinimumPopulation, decision.Reason);
        }

        [Fact]
        public void EmptyOrNullPen_SlaughtersNothing()
        {
            Assert.False(Decide(0, 0).HasVictim);
            Assert.False(CullPlanner.Decide(null, HerdTargets.Create(3, 1), 0, false).HasVictim);
        }

        // --- Target arithmetic ---------------------------------------------------

        [Theory]
        [InlineData(6, 5, 5, 1)]
        [InlineData(6, 0, 0, 6)]
        [InlineData(6, 6, 6, 0)]
        [InlineData(6, 9, 6, 0)]   // females above the limit are capped at the limit
        [InlineData(6, -2, 0, 6)]  // negative females read as zero
        [InlineData(-3, 2, 0, 0)]  // a negative limit reads as zero
        public void Targets_AreClampedAndMalesFillTheRemainder(int limit, int females, int expectedFemales, int expectedMales)
        {
            var targets = HerdTargets.Create(limit, females);

            Assert.Equal(expectedFemales, targets.FemalesToKeep);
            Assert.Equal(expectedMales, targets.MalesToKeep);
        }

        [Fact]
        public void Census_CountsEachGroup()
        {
            var census = HerdCensus.Count(new List<PenAnimal> { F, F, M, FPup, MPup, MPup });

            Assert.Equal(2, census.AdultFemales);
            Assert.Equal(1, census.AdultMales);
            Assert.Equal(1, census.FemalePups);
            Assert.Equal(2, census.MalePups);
            Assert.Equal(6, census.Total);
        }
    }
}
