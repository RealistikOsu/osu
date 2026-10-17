using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    /// <summary>
    /// Relax difficulty from the 2019 calc. <see cref="OsuPerformanceCalculator"/> computes 2019 pp for these.
    /// Aim, speed and reading difficulty hold the 2019 strains.
    /// </summary>
    public class OsuRelax2019DifficultyAttributes : OsuDifficultyAttributes
    {
        /// <summary>
        /// Overall difficulty after mods, not adjusted for clock rate.
        /// </summary>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        /// <summary>
        /// Approach rate after mods, not adjusted for clock rate.
        /// </summary>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// Approach rate after mods and clock rate.
        /// </summary>
        [JsonProperty("effective_approach_rate")]
        public double EffectiveApproachRate { get; set; }

        private const int attrib_id_overall_difficulty = 101;
        private const int attrib_id_approach_rate = 103;
        private const int attrib_id_effective_approach_rate = 105;

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var v in base.ToDatabaseAttributes())
                yield return v;

            yield return (attrib_id_overall_difficulty, OverallDifficulty);
            yield return (attrib_id_approach_rate, ApproachRate);
            yield return (attrib_id_effective_approach_rate, EffectiveApproachRate);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);

            OverallDifficulty = values[attrib_id_overall_difficulty];
            ApproachRate = values[attrib_id_approach_rate];
            EffectiveApproachRate = values[attrib_id_effective_approach_rate];
        }
    }
}
