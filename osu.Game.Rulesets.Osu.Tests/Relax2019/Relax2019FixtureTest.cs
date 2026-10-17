using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.Relax2019
{
    /// <summary>
    /// Values dumped from the Rust osu_2019 calc (~/rosu-pp-rs) for the test beatmaps.
    /// </summary>
    [TestFixture]
    public class Relax2019FixtureTest : DifficultyCalculatorTest
    {
        private const double relative_tolerance = 1e-4;

        protected override string ResourceAssembly => "osu.Game.Rulesets.Osu.Tests";

        public static IEnumerable<TestCaseData> Fixtures()
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream("osu.Game.Rulesets.Osu.Tests.Resources.Testing.Relax2019.fixtures.json")!;
            using var reader = new StreamReader(stream);

            foreach (var fixture in JsonConvert.DeserializeObject<Fixture[]>(reader.ReadToEnd())!)
                yield return new TestCaseData(fixture).SetName($"{{m}}({fixture})");
        }

        [TestCaseSource(nameof(Fixtures))]
        public void TestAimAndSpeed(Fixture fixture)
        {
            var (_, attributes) = calculate(fixture);
            var expected = fixture.Difficulty;

            assertClose(expected.AimStrain, attributes.AimDifficulty, "aim");
            assertClose(expected.SpeedStrain, attributes.SpeedDifficulty, "speed");
            assertClose(expected.SliderFactor, attributes.SliderFactor, "slider factor");
            assertClose(expected.AimDifficultStrainCount, attributes.AimDifficultStrainCount, "aim difficult strains");
            assertClose(expected.SpeedDifficultStrainCount, attributes.SpeedDifficultStrainCount, "speed difficult strains");
            assertClose(expected.AimDifficultSliderCount, attributes.AimDifficultSliderCount, "difficult sliders");
            assertClose(expected.ApproachRate, attributes.ApproachRate, "ar");
            assertClose(expected.OverallDifficulty, attributes.OverallDifficulty, "od");
            assertClose(expected.EffectiveApproachRate, attributes.EffectiveApproachRate, "effective ar");

            Assert.That(attributes.MaxCombo, Is.EqualTo(expected.MaxCombo), "max combo");
            Assert.That(attributes.HitCircleCount, Is.EqualTo(expected.Circles), "circles");
            Assert.That(attributes.SliderCount, Is.EqualTo(expected.Sliders), "sliders");
            Assert.That(attributes.SpinnerCount, Is.EqualTo(expected.Spinners), "spinners");
        }

        [TestCaseSource(nameof(Fixtures))]
        public void TestReadingAndStarRating(Fixture fixture)
        {
            var (_, attributes) = calculate(fixture);
            var expected = fixture.Difficulty;

            assertClose(expected.ReadingStrain, attributes.ReadingDifficulty, "reading");
            assertClose(expected.ReadingDifficultNoteCount, attributes.ReadingDifficultNoteCount, "reading difficult notes");
            assertClose(expected.Stars, attributes.StarRating, "stars");
        }

        [TestCaseSource(nameof(Fixtures))]
        public void TestPerformance(Fixture fixture)
        {
            var (beatmap, attributes) = calculate(fixture);
            var ruleset = new OsuRuleset();

            foreach (var expected in fixture.Scores)
            {
                // Classic: the Rust calc only knows the combo-based dropped slider estimate.
                var score = new ScoreInfo(beatmap.Beatmap.BeatmapInfo, ruleset.RulesetInfo)
                {
                    Mods = DifficultyMods(fixture).Append(new OsuModClassic()).ToArray(),
                    MaxCombo = expected.Combo,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        [HitResult.Great] = expected.Count300,
                        [HitResult.Ok] = expected.Count100,
                        [HitResult.Meh] = expected.Count50,
                        [HitResult.Miss] = expected.Misses,
                    },
                };

                var performance = (OsuPerformanceAttributes)new OsuPerformanceCalculator().Calculate(score, attributes);

                assertClose(expected.Total, performance.Total, "pp");
                assertClose(expected.Aim, performance.Aim, "aim pp");
                assertClose(expected.Speed, performance.Speed, "speed pp");
                assertClose(expected.Accuracy, performance.Accuracy, "acc pp");
                assertClose(expected.Reading, performance.Reading, "reading pp");
                assertClose(expected.EffectiveMissCount, performance.EffectiveMissCount, "effective misses");
            }
        }

        private (IWorkingBeatmap beatmap, OsuRelax2019DifficultyAttributes attributes) calculate(Fixture fixture)
        {
            var beatmap = GetBeatmap(fixture.Map);

            if (fixture.PassedObjects is int passed)
            {
                var hitObjects = ((Beatmap)beatmap.Beatmap).HitObjects;
                hitObjects.RemoveRange(passed, hitObjects.Count - passed);
            }

            var attributes = CreateDifficultyCalculator(beatmap).Calculate(DifficultyMods(fixture));

            Assert.That(attributes, Is.TypeOf<OsuRelax2019DifficultyAttributes>());
            return (beatmap, (OsuRelax2019DifficultyAttributes)attributes);
        }

        public static Mod[] DifficultyMods(Fixture fixture)
        {
            var ruleset = new OsuRuleset();
            var mods = new List<Mod> { new OsuModRelax() };

            foreach (string acronym in fixture.Mods)
            {
                var mod = ruleset.CreateModFromAcronym(acronym)!;

                if (mod is ModRateAdjust rateAdjust && fixture.Rate is double rate)
                    rateAdjust.SpeedChange.Value = rate;

                mods.Add(mod);
            }

            return mods.ToArray();
        }

        private static void assertClose(double expected, double actual, string field)
            => Assert.That(actual, Is.EqualTo(expected).Within(Math.Max(Math.Abs(expected) * relative_tolerance, 1e-6)), field);

        protected override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, beatmap);

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        public class Fixture
        {
            [JsonProperty("map")]
            public string Map { get; set; } = string.Empty;

            [JsonProperty("mods")]
            public string[] Mods { get; set; } = Array.Empty<string>();

            [JsonProperty("rate")]
            public double? Rate { get; set; }

            [JsonProperty("passed_objects")]
            public int? PassedObjects { get; set; }

            [JsonProperty("difficulty")]
            public FixtureDifficulty Difficulty { get; set; } = new FixtureDifficulty();

            [JsonProperty("scores")]
            public FixtureScore[] Scores { get; set; } = Array.Empty<FixtureScore>();

            public override string ToString()
                => $"{Map} {string.Join(string.Empty, Mods)}{(Rate != null ? $" x{Rate}" : string.Empty)}{(PassedObjects != null ? $" partial {PassedObjects}" : string.Empty)}".Trim();
        }

        public class FixtureDifficulty
        {
            [JsonProperty("stars")]
            public double Stars { get; set; }

            [JsonProperty("aim_strain")]
            public double AimStrain { get; set; }

            [JsonProperty("speed_strain")]
            public double SpeedStrain { get; set; }

            [JsonProperty("reading_strain")]
            public double ReadingStrain { get; set; }

            [JsonProperty("slider_factor")]
            public double SliderFactor { get; set; }

            [JsonProperty("aim_difficult_strain_count")]
            public double AimDifficultStrainCount { get; set; }

            [JsonProperty("speed_difficult_strain_count")]
            public double SpeedDifficultStrainCount { get; set; }

            [JsonProperty("reading_difficult_note_count")]
            public double ReadingDifficultNoteCount { get; set; }

            [JsonProperty("aim_difficult_slider_count")]
            public double AimDifficultSliderCount { get; set; }

            [JsonProperty("ar")]
            public double ApproachRate { get; set; }

            [JsonProperty("od")]
            public double OverallDifficulty { get; set; }

            [JsonProperty("effective_ar")]
            public double EffectiveApproachRate { get; set; }

            [JsonProperty("max_combo")]
            public int MaxCombo { get; set; }

            [JsonProperty("n_circles")]
            public int Circles { get; set; }

            [JsonProperty("n_sliders")]
            public int Sliders { get; set; }

            [JsonProperty("n_spinners")]
            public int Spinners { get; set; }
        }

        public class FixtureScore
        {
            [JsonProperty("n300")]
            public int Count300 { get; set; }

            [JsonProperty("n100")]
            public int Count100 { get; set; }

            [JsonProperty("n50")]
            public int Count50 { get; set; }

            [JsonProperty("misses")]
            public int Misses { get; set; }

            [JsonProperty("combo")]
            public int Combo { get; set; }

            [JsonProperty("pp")]
            public double Total { get; set; }

            [JsonProperty("pp_aim")]
            public double Aim { get; set; }

            [JsonProperty("pp_speed")]
            public double Speed { get; set; }

            [JsonProperty("pp_acc")]
            public double Accuracy { get; set; }

            [JsonProperty("pp_reading")]
            public double Reading { get; set; }

            [JsonProperty("effective_miss_count")]
            public double EffectiveMissCount { get; set; }
        }
    }
}
