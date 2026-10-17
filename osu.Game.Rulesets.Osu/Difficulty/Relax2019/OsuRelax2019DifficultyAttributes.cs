using Newtonsoft.Json;

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
    }
}
