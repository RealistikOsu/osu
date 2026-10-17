using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
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
    [TestFixture]
    public class RelaxPerformanceTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        private IWorkingBeatmap beatmap = null!;
        private OsuRelaxDifficultyAttributes attributes = null!;

        [SetUp]
        public void SetUp()
        {
            beatmap = GetBeatmap("801165");
            attributes = (OsuRelaxDifficultyAttributes)CreateDifficultyCalculator(beatmap).Calculate(new Mod[] { new OsuModRelax() });
        }

        [Test]
        public void TestUnjudgedObjectsCountAsGreats()
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;

            var full = calculate(objects - 30, 20, 10, 0, attributes.MaxCombo);
            var missingGreats = calculate(objects - 130, 20, 10, 0, attributes.MaxCombo);

            Assert.That(missingGreats.Total, Is.EqualTo(full.Total).Within(1e-6));
        }

        [Test]
        public void TestNonClassicIgnoresBrokenComboForSliderNerf()
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;
            var tails = new Dictionary<HitResult, int> { [HitResult.SliderTailHit] = attributes.SliderCount };

            // Same statistics and combo; classic estimates dropped sliders from the missing combo, non-classic uses the (all hit) tails.
            var lazer = calculate(objects - 10, 10, 0, 0, attributes.MaxCombo / 2, classic: false, extra: tails);
            var stable = calculate(objects - 10, 10, 0, 0, attributes.MaxCombo / 2, classic: true);

            Assert.That(lazer.Aim, Is.GreaterThan(stable.Aim));
            Assert.That(lazer.Speed, Is.EqualTo(stable.Speed));
        }

        [Test]
        public void TestNonClassicSliderNerfWithDroppedTails()
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;

            var allHit = calculate(objects, 0, 0, 0, attributes.MaxCombo, classic: false,
                extra: new Dictionary<HitResult, int> { [HitResult.SliderTailHit] = attributes.SliderCount });
            var dropped = calculate(objects, 0, 0, 0, attributes.MaxCombo, classic: false,
                extra: new Dictionary<HitResult, int> { [HitResult.SliderTailHit] = attributes.SliderCount - 20, [HitResult.LargeTickMiss] = 5 });

            Assert.That(attributes.AimDifficultSliderCount, Is.GreaterThan(0));
            Assert.That(dropped.Aim, Is.LessThan(allHit.Aim));
        }

        [Test]
        public void TestClassicIgnoresTailStatistics()
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;

            var a = calculate(objects, 0, 0, 0, attributes.MaxCombo, classic: true);
            var b = calculate(objects, 0, 0, 0, attributes.MaxCombo, classic: true,
                extra: new Dictionary<HitResult, int> { [HitResult.SliderTailHit] = 0, [HitResult.LargeTickMiss] = 50 });

            Assert.That(b.Total, Is.EqualTo(a.Total));
        }

        [Test]
        public void TestRelaxScoreWithPlainAttributesThrows()
        {
            var values = attributes.ToDatabaseAttributes().ToDictionary(a => a.attributeId, a => Convert.ToDouble(a.value));
            var onlineInfo = new APIBeatmap { CircleCount = attributes.HitCircleCount, SliderCount = attributes.SliderCount, SpinnerCount = attributes.SpinnerCount };

            var plain = new OsuDifficultyAttributes();
            plain.FromDatabaseAttributes(values, onlineInfo);

            var score = new ScoreInfo(beatmap.Beatmap.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = new Mod[] { new OsuModRelax() },
                MaxCombo = 1,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Great] = 1 },
            };

            Assert.Throws<InvalidOperationException>(() => new OsuPerformanceCalculator().Calculate(score, plain));
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(0, 0, 0, -1, 0)]
        [TestCase(1, 0, 0, 0, 1)]
        public void TestDegenerateScoresAreFinite(int greats, int oks, int mehs, int misses, int combo)
        {
            int objects = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;

            // misses == -1 means "every object missed".
            var performance = calculate(greats, oks, mehs, misses < 0 ? objects : misses, combo);

            foreach (double value in new[] { performance.Total, performance.Aim, performance.Speed, performance.Accuracy, performance.Reading, performance.EffectiveMissCount })
                Assert.That(double.IsFinite(value), Is.True);
        }

        [Test]
        public void TestTinyMapIsFinite()
        {
            var tiny = GetBeatmap("very-fast-slider");
            var tinyAttributes = CreateDifficultyCalculator(tiny).Calculate(new Mod[] { new OsuModRelax() });

            var score = new ScoreInfo(tiny.Beatmap.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = new Mod[] { new OsuModRelax(), new OsuModClassic() },
                MaxCombo = 1,
                Statistics = new Dictionary<HitResult, int> { [HitResult.Miss] = 1 },
            };

            var performance = (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, tinyAttributes);

            Assert.That(double.IsFinite(performance.Total), Is.True);
        }

        private OsuPerformanceAttributes calculate(int greats, int oks, int mehs, int misses, int combo, bool classic = true, Dictionary<HitResult, int>? extra = null)
        {
            var statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = greats,
                [HitResult.Ok] = oks,
                [HitResult.Meh] = mehs,
                [HitResult.Miss] = misses,
            };

            if (extra != null)
            {
                foreach (var (result, count) in extra)
                    statistics[result] = count;
            }

            var mods = classic ? new Mod[] { new OsuModRelax(), new OsuModClassic() } : new Mod[] { new OsuModRelax() };

            var score = new ScoreInfo(beatmap.Beatmap.BeatmapInfo, new OsuRuleset().RulesetInfo)
            {
                Mods = mods,
                MaxCombo = combo,
                Statistics = statistics,
            };

            return (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, attributes);
        }

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
