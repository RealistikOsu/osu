using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Relax
{
    [TestFixture]
    public class RelaxDifficultyTest : DifficultyCalculatorTest
    {
        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        [TestCase("diffcalc-test", 0)]
        [TestCase("diffcalc-test", 1)]
        [TestCase("diffcalc-test", 40)]
        [TestCase("diffcalc-test", 120)]
        [TestCase("801165", 300)]
        [TestCase("801165", 900)]
        public void TestTimedMatchesTruncated(string name, int index)
        {
            var mods = new Mod[] { new OsuModRelax(), new OsuModHidden() };

            var timed = CreateDifficultyCalculator(GetBeatmap(name)).CalculateTimed(mods);

            var truncated = GetBeatmap(name);
            var hitObjects = ((Beatmap)truncated.Beatmap).HitObjects;
            hitObjects.RemoveRange(index + 1, hitObjects.Count - index - 1);

            var expected = CreateDifficultyCalculator(truncated).Calculate(mods);

            Assert.That(timed[index].Attributes, Is.EqualTo(expected).UsingPropertiesComparer());
        }

        [TestCase("diffcalc-test")]
        [TestCase("zero-length-sliders")]
        [TestCase("801165")]
        public void TestRelaxRouting(string name)
        {
            var calc = CreateDifficultyCalculator(GetBeatmap(name));

            Assert.That(calc.Calculate(new Mod[] { new OsuModRelax() }), Is.TypeOf<OsuRelaxDifficultyAttributes>());
            Assert.That(calc.Calculate(), Is.TypeOf<OsuDifficultyAttributes>());
        }

        [TestCase("diffcalc-test")]
        [TestCase("zero-length-sliders")]
        [TestCase("very-fast-slider")]
        public void TestTimedEndsOnCalculate(string name)
        {
            var mods = new Mod[] { new OsuModRelax(), new OsuModDoubleTime() };
            var calc = CreateDifficultyCalculator(GetBeatmap(name));

            Assert.That(calc.CalculateTimed(mods).Last().Attributes, Is.EqualTo(calc.Calculate(mods)).UsingPropertiesComparer());
        }

        [TestCase("diffcalc-test")]
        [TestCase("zero-length-sliders")]
        public void TestOffsetChanges(string name)
        {
            var mods = new Mod[] { new OsuModRelax() };
            var beatmap = GetBeatmap(name);
            var expected = CreateDifficultyCalculator(beatmap).Calculate(mods);

            // Sections align to the first object, so shifting the whole map by a multiple of the section length changes nothing.
            foreach (var hitObject in beatmap.Beatmap.HitObjects)
                hitObject.StartTime += 4000;

            var calc = CreateDifficultyCalculator(beatmap);

            Assert.That(calc.Calculate(mods), Is.EqualTo(expected).UsingPropertiesComparer());
            Assert.That(calc.CalculateTimed(mods).Last().Attributes, Is.EqualTo(expected).UsingPropertiesComparer());
        }

        [TestCase("diffcalc-test")]
        [TestCase("801165")]
        public void TestClassicDoesNotChangeRelaxDifficulty(string name)
        {
            var calc = CreateDifficultyCalculator(GetBeatmap(name));
            var relax = calc.Calculate(new Mod[] { new OsuModRelax() });
            var classic = calc.Calculate(new Mod[] { new OsuModRelax(), new OsuModClassic() });

            relax.Mods = classic.Mods;

            Assert.That(classic, Is.EqualTo(relax).UsingPropertiesComparer());
        }

        [Test]
        public void TestDatabaseRoundTrip()
        {
            var calc = CreateDifficultyCalculator(GetBeatmap("diffcalc-test"));
            var attributes = (OsuRelaxDifficultyAttributes)calc.Calculate(new Mod[] { new OsuModRelax(), new OsuModHidden(), new OsuModHardRock() });

            var values = attributes.ToDatabaseAttributes().ToDictionary(a => a.attributeId, a => Convert.ToDouble(a.value));
            var onlineInfo = new APIBeatmap { CircleCount = attributes.HitCircleCount, SliderCount = attributes.SliderCount, SpinnerCount = attributes.SpinnerCount };

            var restored = new OsuRelaxDifficultyAttributes { Mods = attributes.Mods };
            restored.FromDatabaseAttributes(values, onlineInfo);

            Assert.That(restored, Is.EqualTo(attributes).UsingPropertiesComparer());
        }

        [Test]
        public void TestRelaxIsDifficultyAdjustmentMod()
        {
            var combinations = CreateDifficultyCalculator(GetBeatmap("diffcalc-test")).CreateDifficultyAdjustmentModCombinations();

            Assert.That(combinations.Any(c => c is OsuModRelax || (c is MultiMod multi && multi.Mods.Any(m => m is OsuModRelax))), Is.True);
        }

        [Test]
        public void TestDifficultyAdjust()
        {
            var calc = CreateDifficultyCalculator(GetBeatmap("diffcalc-test"));
            var adjust = new OsuModDifficultyAdjust();
            adjust.ApproachRate.Value = 10;

            var attributes = (OsuRelaxDifficultyAttributes)calc.Calculate(new Mod[] { new OsuModRelax(), adjust });

            Assert.That(attributes.ApproachRate, Is.EqualTo(10).Within(1e-6));
            Assert.That(attributes.EffectiveApproachRate, Is.EqualTo(10).Within(1e-4));
        }

        [TestCaseSource(nameof(edgeCaseBeatmaps))]
        public void TestEdgeCases(string description, HitObject[] hitObjects)
        {
            var beatmap = new Beatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.HitObjects.AddRange(hitObjects);

            var calc = CreateDifficultyCalculator(new TestWorkingBeatmap(beatmap));
            var attributes = (OsuRelaxDifficultyAttributes)calc.Calculate(new Mod[] { new OsuModRelax(), new OsuModHidden() });

            foreach (double value in new[]
                     {
                         attributes.StarRating, attributes.AimDifficulty, attributes.SpeedDifficulty, attributes.ReadingDifficulty, attributes.SliderFactor,
                         attributes.AimDifficultStrainCount, attributes.SpeedDifficultStrainCount, attributes.ReadingDifficultNoteCount, attributes.AimDifficultSliderCount,
                         attributes.EffectiveApproachRate,
                     })
            {
                Assert.That(double.IsFinite(value), Is.True, description);
                Assert.That(value, Is.GreaterThanOrEqualTo(0), description);
            }
        }

        private static IEnumerable<TestCaseData> edgeCaseBeatmaps()
        {
            yield return new TestCaseData("empty", Array.Empty<HitObject>());
            yield return new TestCaseData("single circle", new HitObject[] { circle(0, 0) });
            yield return new TestCaseData("stacked circles", Enumerable.Range(0, 50).Select(i => (HitObject)circle(i * 100, 0)).ToArray());
            yield return new TestCaseData("no circles", Enumerable.Range(0, 20).Select(i => (HitObject)slider(i * 1000, i % 2 * 200)).ToArray());
            yield return new TestCaseData("only spinners", new HitObject[] { new Spinner { StartTime = 0, Duration = 1000 }, new Spinner { StartTime = 2000, Duration = 1000 } });
        }

        private static HitCircle circle(double time, float x) => new HitCircle { StartTime = time, Position = new Vector2(x, 100) };

        private static Slider slider(double time, float x) => new Slider
        {
            StartTime = time,
            Position = new Vector2(x, 100),
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(150, 0) }),
        };

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
