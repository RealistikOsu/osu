using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax
{
    /// <summary>
    /// The circle size, approach rate and overall difficulty a relax play is rated at.
    /// <para>
    /// Without custom settings these are the beatmap's settings after mods, untouched.
    /// Custom settings (<see cref="OsuModDifficultyAdjust"/>) are rated within what stable mods reach on the beatmap, from <see cref="OsuModEasy"/> to <see cref="OsuModHardRock"/>:
    /// <list type="bullet">
    /// <item>circle size and overall difficulty above hard rock's are rated at hard rock's,</item>
    /// <item>approach rate is rated within the beatmap's and hard rock's, as lowering it adds reading difficulty;
    /// the played approach rate is used instead if it gives less reading difficulty (see <see cref="PlayedApproachRate"/>),</item>
    /// <item>circles bigger than easy's are rated at their size, with aim, speed and reading difficulty penalised on top.</item>
    /// </list>
    /// Drain rate does not affect relax difficulty or pp.
    /// </para>
    /// </summary>
    /// <param name="CircleSize">Circle size to rate at.</param>
    /// <param name="ApproachRate">Approach rate to rate at, not adjusted for clock rate.</param>
    /// <param name="PlayedApproachRate">Approach rate played at. When it differs from <see cref="ApproachRate"/>, reading is rated at whichever of the two gives less difficulty.</param>
    /// <param name="OverallDifficulty">Overall difficulty to rate at, not adjusted for clock rate.</param>
    /// <param name="BigCirclePenalty">Multiplier on aim, speed and reading difficulty for circles bigger than easy's; 1 otherwise.</param>
    public readonly record struct RelaxDifficultySettings(float CircleSize, float ApproachRate, float PlayedApproachRate, float OverallDifficulty, float BigCirclePenalty)
    {
        /// <summary>
        /// The big circle penalty is the ratio of easy's circle radius to the played one, to this power.
        /// </summary>
        private const float big_circle_penalty_exponent = 2;

        public static RelaxDifficultySettings Create(IBeatmap beatmap, IEnumerable<Mod> mods)
        {
            var played = beatmap.Difficulty;

            if (!mods.OfType<OsuModDifficultyAdjust>().Any(isCustom))
                return new RelaxDifficultySettings(played.CircleSize, played.ApproachRate, played.ApproachRate, played.OverallDifficulty, 1);

            // Mods change the playable beatmap's difficulty, not its BeatmapInfo's, which keeps the beatmap's own settings.
            var original = beatmap.BeatmapInfo.Difficulty;

            var easy = original.Clone();
            new OsuModEasy().ApplyToDifficulty(easy);

            var hardRock = original.Clone();
            new OsuModHardRock().ApplyToDifficulty(hardRock);

            float circleSize = Math.Min(played.CircleSize, hardRock.CircleSize);
            float approachRate = Math.Clamp(played.ApproachRate, original.ApproachRate, Math.Max(original.ApproachRate, hardRock.ApproachRate));
            float overallDifficulty = Math.Min(played.OverallDifficulty, hardRock.OverallDifficulty);

            float bigCirclePenalty = 1;

            if (circleSize < easy.CircleSize)
            {
                float easyRadius = RelaxDifficultyCalculation.Geometry(easy.CircleSize).Radius;
                float radius = RelaxDifficultyCalculation.Geometry(circleSize).Radius;

                bigCirclePenalty = MathF.Pow(easyRadius / radius, big_circle_penalty_exponent);
            }

            return new RelaxDifficultySettings(circleSize, approachRate, played.ApproachRate, overallDifficulty, bigCirclePenalty);
        }

        private static bool isCustom(OsuModDifficultyAdjust adjust)
            => adjust.CircleSize.Value != null || adjust.ApproachRate.Value != null || adjust.OverallDifficulty.Value != null;
    }
}
