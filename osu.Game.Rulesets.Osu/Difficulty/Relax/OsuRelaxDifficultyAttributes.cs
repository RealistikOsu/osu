using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax
{
    /// <summary>
    /// Relax difficulty. <see cref="OsuPerformanceCalculator"/> computes relax pp for these.
    /// Aim, speed and reading difficulty hold the relax strains.
    /// Inherited modern-only fields (speed note count, top-weighted slider factors, legacy score fields) are not populated.
    /// </summary>
    public class OsuRelaxDifficultyAttributes : OsuDifficultyAttributes
    {
        /// <summary>
        /// Overall difficulty the play is rated at (see <see cref="RelaxDifficultySettings"/>), not adjusted for clock rate.
        /// </summary>
        [JsonProperty("overall_difficulty")]
        public double OverallDifficulty { get; set; }

        /// <summary>
        /// Approach rate the play is rated at (see <see cref="RelaxDifficultySettings"/>), not adjusted for clock rate.
        /// </summary>
        [JsonProperty("approach_rate")]
        public double ApproachRate { get; set; }

        /// <summary>
        /// Approach rate the play is rated at, adjusted for clock rate.
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
