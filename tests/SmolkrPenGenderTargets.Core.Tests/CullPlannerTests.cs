// CullPlannerTests.cs
//
// Role in the larger system: pins down the mod's slaughter rule (CullPlanner)
// and target arithmetic (HerdTargets) so a later change cannot silently alter
// which smolkr a villager kills. The parity tests compare the rule against
// VanillaReference, a transcription of the game's own method, for adult-only
// parity. Newborn regression tests cover the intentional mixed-age differences.

using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Runs the planner with the pen's population equal to its animal count and no minimum.</summary>
        private static CullDecision Decide(int limit, int females, params PenAnimal[] pen) =>
            CullPlanner.Decide(pen, HerdTargets.Create(limit, females), pen.Length, minimumPopulation: 0);

        // --- Population gates ---------------------------------------------------

        [Fact]
        public void AtTheLimit_SlaughtersNothing_EvenIfTheRatioIsOff()
        {
            // 6 females against 5F/1M, limit 6: the pen is not OVER the limit, and
            // vanilla slaughters nothing here, so neither does the mod.
            var decision = Decide(6, 5, F, F, F, F, F, F);

            Assert.Equal(CullReason.WithinLimit, decision.Reason);
            Assert.False(decision.HasVictim);
        }

        [Fact]
        public void PupsCountTowardBothTheLimitAndTargets()
        {
            // 5 adults and 2 pups against a limit of 6: over the limit.
            var decision = Decide(6, 5, F, F, F, F, M, FPup, MPup);

            Assert.Equal(CullReason.MaleChosen, decision.Reason);
        }

        [Fact]
        public void PopulationComesFromTheGamesCount_NotTheReadableAnimals()
        {
            // The game counts an owned-pets entry it could not read a gender from;
            // the mod must see the same population as vanilla does.
            var pen = new[] { F, F, F };
            var decision = CullPlanner.Decide(pen, HerdTargets.Create(3, 2), penPopulation: 4, minimumPopulation: 0);

            Assert.True(decision.HasVictim);
        }

        [Fact]
        public void OverTheLimitButOnlyPups_SlaughtersNothing()
        {
            var decision = Decide(1, 1, FPup, MPup);

            Assert.Equal(CullReason.NoAdults, decision.Reason);
        }

        [Fact]
        public void BelowMinimumPopulation_SlaughtersNothing()
        {
            var pen = new[] { M, M, M };
            var decision = CullPlanner.Decide(pen, HerdTargets.Create(1, 1), pen.Length, minimumPopulation: 4);

            Assert.Equal(CullReason.BelowMinimumPopulation, decision.Reason);
        }

        [Fact]
        public void EmptyOrNullPen_SlaughtersNothing()
        {
            Assert.False(Decide(0, 0).HasVictim);
            Assert.False(CullPlanner.Decide(null, HerdTargets.Create(3, 1), 0, 0).HasVictim);
        }

        // --- Which gender: the one thing the mod changes -------------------------

        [Fact]
        public void TargetRatio_ReplacesTheMajorityRule()
        {
            // 5F/1M wanted, 5F/2M in the pen (7 > 6). Vanilla takes a female (the
            // majority); the mod takes the extra male.
            var decision = Decide(6, 5, F, F, F, F, F, M, M);

            Assert.Equal(CullReason.MaleChosen, decision.Reason);
            Assert.Equal(CullReason.FemaleChosen, VanillaReference.Decide(new[] { F, F, F, F, F, M, M }, 6, 7, 0).Reason);
        }

        [Fact]
        public void NewbornPushesPenOverLimit_TheOverTargetGenderGoes()
        {
            // 6 adult females, no male, limit 6 with 5F/1M wanted. A pup puts the
            // pen over the limit; females are 1 over target, males 1 under.
            var decision = Decide(6, 5, F, F, F, F, F, F, MPup);

            Assert.Equal(CullReason.FemaleChosen, decision.Reason);
        }

        [Fact]
        public void BothOver_LargerSurplusGoesFirst()
        {
            // Target 2F/2M; pen 5F (surplus 3) and 3M (surplus 1).
            var decision = Decide(4, 2, F, F, F, F, F, M, M, M);

            Assert.Equal(CullReason.FemaleChosen, decision.Reason);
        }

        [Fact]
        public void FemaleBirthAtTarget_SlaughtersFemaleAndKeepsMale()
        {
            var decision = Decide(6, 5, F, F, F, F, F, M, FPup);

            Assert.Equal(CullReason.FemaleChosen, decision.Reason);
            Assert.Equal(4, decision.VictimIndex);
        }

        [Fact]
        public void FemaleBirths_FromThreeOfEach_SettleAtFiveFemalesAndOneMale()
        {
            var pen = new List<PenAnimal> { F, F, F, M, M, M };
            for (int birth = 0; birth < 10; birth++)
            {
                pen.Add(FPup);
                var decision = CullPlanner.Decide(pen, HerdTargets.Create(6, 5), pen.Count, 0);
                Assert.True(decision.HasVictim);
                pen.RemoveAt(decision.VictimIndex);
                pen[pen.Count - 1] = F; // The newborn matures before the next birth.
                Assert.Contains(pen, a => !a.IsFemale);
            }
            Assert.Equal(5, pen.Count(a => a.IsFemale));
            Assert.Single(pen, a => !a.IsFemale);
        }

        [Fact]
        public void MaleBirthAtTarget_SlaughtersMaleEvenIfHeIsTheOnlyAdultMale()
        {
            var decision = Decide(6, 5, F, F, F, F, F, M, MPup);

            Assert.Equal(CullReason.MaleChosen, decision.Reason);
            Assert.Equal(5, decision.VictimIndex);
        }

        [Fact]
        public void SurplusGenderHasOnlyPups_DoesNotSlaughterOtherGender()
        {
            var decision = Decide(6, 5, F, F, F, F, F, MPup, MPup);

            Assert.Equal(CullReason.NoSurplusAdults, decision.Reason);
            Assert.False(decision.HasVictim);
            // Once one male matures, it can be slaughtered to leave one male pup.
            Assert.Equal(CullReason.MaleChosen, Decide(6, 5, F, F, F, F, F, M, MPup).Reason);
        }

        [Fact]
        public void ZeroFemaleTarget_AllowsLastAdultFemaleToBeSlaughtered()
        {
            var decision = Decide(3, 0, F, MPup, MPup, MPup);

            Assert.Equal(CullReason.FemaleChosen, decision.Reason);
        }

        // --- Which individual: vanilla's --------------------------------------

        [Fact]
        public void Victim_IsTheLastAdultOfTheChosenGender_NeverAPup()
        {
            var decision = Decide(1, 1, F, M, MPup, M, MPup);

            Assert.Equal(CullReason.MaleChosen, decision.Reason);
            Assert.Equal(3, decision.VictimIndex);
        }

        // --- Parity with vanilla -----------------------------------------------

        [Fact]
        public void EvenSplitTargets_MatchVanillaExactly_OnAdultOnlyPens()
        {
            // With an even split the mod must make exactly vanilla's decision -
            // same outcome and same individual - for readable adult-only pens.
            var random = new Random(20260927);
            for (int run = 0; run < 20000; run++)
            {
                int limit = random.Next(0, 7) * 2;
                int minimum = random.Next(0, 6);
                var pen = RandomPen(random).Select(a => new PenAnimal(a.IsFemale, true)).ToArray();
                int population = pen.Length;

                var mod = CullPlanner.Decide(pen, HerdTargets.Create(limit, limit / 2), population, minimum);
                var vanilla = VanillaReference.Decide(pen, limit, population, minimum);

                Assert.Equal(vanilla.VictimIndex, mod.VictimIndex);
            }
        }

        [Fact]
        public void AnyTargets_OnlySlaughterAdultsOfAnOverTargetGender()
        {
            // No selection may reduce a gender that is at or below its target.
            var random = new Random(7);
            for (int run = 0; run < 20000; run++)
            {
                int limit = random.Next(0, 12);
                int minimum = random.Next(0, 6);
                var pen = RandomPen(random);

                var targets = HerdTargets.Create(limit, random.Next(-1, limit + 2));
                var mod = CullPlanner.Decide(pen, targets, pen.Length, minimum);

                if (!mod.HasVictim) continue;
                Assert.True(pen.Length > limit && pen.Length >= minimum);
                var victim = pen[mod.VictimIndex];
                Assert.True(victim.IsMature);
                Assert.True(pen.Count(a => a.IsFemale == victim.IsFemale) > targets.ToKeep(victim.IsFemale));
            }
        }

        private static PenAnimal[] RandomPen(Random random) =>
            Enumerable.Range(0, random.Next(0, 16))
                .Select(_ => new PenAnimal(random.Next(2) == 0, random.Next(4) != 0))
                .ToArray();

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

    /// <summary>
    /// ASKA's own AnimalPen.GetSmolkrReadyForSlaughter, transcribed line by line
    /// from the disassembly in docs/DESIGN.md. It is the yardstick the parity
    /// tests measure the mod against - do not "improve" it.
    /// </summary>
    internal static class VanillaReference
    {
        internal static CullDecision Decide(IReadOnlyList<PenAnimal> ownedPets, int limit, int count, int minimumPetPopulation)
        {
            var census = HerdCensus.Count(ownedPets);
            var targets = HerdTargets.Create(limit, limit / 2);

            if (count < limit + 1) return new CullDecision(-1, CullReason.WithinLimit, census, targets);
            if (count < minimumPetPopulation) return new CullDecision(-1, CullReason.BelowMinimumPopulation, census, targets);

            int females = 0, males = 0, lastFemale = -1, lastMale = -1;
            for (int i = 0; i < ownedPets.Count; i++)
            {
                if (!ownedPets[i].IsMature) continue;
                if (ownedPets[i].IsFemale) { females++; lastFemale = i; }
                else { males++; lastMale = i; }
            }

            int result = females <= males ? lastMale : lastFemale;
            var reason = result < 0 ? CullReason.NoAdults
                : ownedPets[result].IsFemale ? CullReason.FemaleChosen : CullReason.MaleChosen;
            return new CullDecision(result, reason, census, targets);
        }
    }
}
