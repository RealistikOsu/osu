using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Relax2019
{
    [TestFixture]
    public class Relax2019DifficultyTest : DifficultyCalculatorTest
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

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();
    }
}
