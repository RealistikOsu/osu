using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Relax
{
    /// <summary>
    /// Custom circle size, approach rate, overall difficulty and drain rate (<see cref="OsuModDifficultyAdjust"/>) on relax.
    /// 801165 is CS 4, AR 9.3, OD 8: easy gives CS 2, hard rock gives CS 5.2, AR 10, OD 10.
    /// </summary>
    [TestFixture]
    public class RelaxCustomSettingsTest : DifficultyCalculatorTest
    {
        private const string beatmap_name = "801165";

        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        [TestCase("diffcalc-test")]
        [TestCase("801165")]
        public void TestBeatmapSettingsChangeNothing(string name)
        {
            var difficulty = GetBeatmap(name).BeatmapInfo.Difficulty;
            var adjust = new OsuModDifficultyAdjust();

            adjust.CircleSize.Value = difficulty.CircleSize;
            adjust.ApproachRate.Value = difficulty.ApproachRate;
            adjust.OverallDifficulty.Value = difficulty.OverallDifficulty;

            assertSameAttributes(calculate(name, new OsuModHidden()), calculate(name, new OsuModHidden(), adjust));
        }

        [TestCase(0)]
        [TestCase(10)]
        public void TestDrainRateChangesNothing(float drainRate)
        {
            var adjust = new OsuModDifficultyAdjust();
            adjust.DrainRate.Value = drainRate;

            assertSameAttributes(calculate(beatmap_name), calculate(beatmap_name, adjust));
        }

        [Test]
        public void TestCircleSizeAndOverallDifficultyAboveHardRockAreRatedAtHardRock()
        {
            var slightlyAbove = calculate(beatmap_name, custom(cs: 6, od: 10));
            var farAbove = calculate(beatmap_name, custom(cs: 11, od: 11));

            Assert.That(farAbove.OverallDifficulty, Is.EqualTo(10));
            assertSameAttributes(slightlyAbove, farAbove);
        }

        [Test]
        public void TestApproachRateAboveHardRockIsNotRewarded([Values] bool doubleTime)
        {
            var mods = doubleTime ? new Mod[] { new OsuModDoubleTime() } : Array.Empty<Mod>();

            var atHardRock = calculate(beatmap_name, mods.Append(custom(ar: 10)).ToArray());
            var aboveHardRock = calculate(beatmap_name, mods.Append(custom(ar: 11)).ToArray());

            Assert.That(aboveHardRock.ReadingDifficulty, Is.LessThanOrEqualTo(atHardRock.ReadingDifficulty));
            Assert.That(aboveHardRock.StarRating, Is.LessThanOrEqualTo(atHardRock.StarRating));
        }

        [Test]
        public void TestLowerApproachRateIsNotRewarded([Values(-10, 0, 5, 9)] float approachRate, [Values] bool hidden, [Values] bool doubleTime)
        {
            var mods = new List<Mod>();

            if (hidden)
                mods.Add(new OsuModHidden());

            if (doubleTime)
                mods.Add(new OsuModDoubleTime());

            var beatmap = calculate(beatmap_name, mods.ToArray());
            var lower = calculate(beatmap_name, mods.Append(custom(ar: approachRate)).ToArray());

            Assert.That(lower.ReadingDifficulty, Is.LessThanOrEqualTo(beatmap.ReadingDifficulty));
            Assert.That(lower.StarRating, Is.LessThanOrEqualTo(beatmap.StarRating));
            Assert.That(performance(lower).Total, Is.LessThanOrEqualTo(performance(beatmap).Total));
        }

        [Test]
        public void TestBigCirclePenalty()
        {
            Assert.That(penalty(custom(cs: 4)), Is.EqualTo(1));
            Assert.That(penalty(custom(cs: 2)), Is.EqualTo(1), "easy's circles are not penalised");

            float easyRadius = RelaxDifficultyCalculation.Geometry(2).Radius;

            Assert.That(penalty(custom(cs: 1)), Is.EqualTo(MathF.Pow(easyRadius / RelaxDifficultyCalculation.Geometry(1).Radius, 2)).Within(1e-6));
            Assert.That(penalty(custom(cs: 0)), Is.EqualTo(MathF.Pow(easyRadius / RelaxDifficultyCalculation.Geometry(0).Radius, 2)).Within(1e-6));

            float penalty(params Mod[] mods)
            {
                var allMods = mods.Prepend(new OsuModRelax()).ToArray();
                var playable = GetBeatmap(beatmap_name).GetPlayableBeatmap(new OsuRuleset().RulesetInfo, allMods);
                return RelaxDifficultySettings.Create(playable, allMods).BigCirclePenalty;
            }
        }

        [Test]
        public void TestSmallerCircleSizeGivesLessPp()
        {
            double previous = performance(calculate(beatmap_name)).Total;

            foreach (float circleSize in new[] { 3f, 2, 1, 0 })
            {
                double total = performance(calculate(beatmap_name, custom(cs: circleSize))).Total;

                Assert.That(total, Is.LessThan(previous), $"CS {circleSize}");
                previous = total;
            }
        }

        [Test]
        public void TestTimedEndsOnCalculate()
        {
            var mods = new Mod[] { new OsuModRelax(), new OsuModHidden(), new OsuModDoubleTime(), custom(cs: 0, ar: 0, od: 11) };
            var calc = CreateDifficultyCalculator(GetBeatmap(beatmap_name));

            Assert.That(calc.CalculateTimed(mods).Last().Attributes, Is.EqualTo(calc.Calculate(mods)).UsingPropertiesComparer());
        }

        private OsuRelaxDifficultyAttributes calculate(string name, params Mod[] mods)
            => (OsuRelaxDifficultyAttributes)CreateDifficultyCalculator(GetBeatmap(name)).Calculate(mods.Prepend(new OsuModRelax()).ToArray());

        private OsuPerformanceAttributes performance(OsuRelaxDifficultyAttributes attributes)
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;

            var score = new ScoreInfo(GetBeatmap(beatmap_name).Beatmap.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = attributes.Mods.Append(new OsuModClassic()).ToArray(),
                MaxCombo = attributes.MaxCombo,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = objects },
            };

            return (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, attributes);
        }

        private static OsuModDifficultyAdjust custom(float? cs = null, float? ar = null, float? od = null)
        {
            var adjust = new OsuModDifficultyAdjust { ExtendedLimits = { Value = true } };

            adjust.CircleSize.Value = cs;
            adjust.ApproachRate.Value = ar;
            adjust.OverallDifficulty.Value = od;

            return adjust;
        }

        private static void assertSameAttributes(OsuRelaxDifficultyAttributes expected, OsuRelaxDifficultyAttributes actual)
        {
            expected.Mods = actual.Mods;
            Assert.That(actual, Is.EqualTo(expected).UsingPropertiesComparer());
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
