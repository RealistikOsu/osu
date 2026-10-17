using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    /// <summary>
    /// The RealistikOsu relax calc (osu_2019), run through <see cref="OsuDifficultyCalculator"/> when relax is enabled.
    /// </summary>
    public static class Relax2019DifficultyCalculation
    {
        private const float object_radius = 64;
        private const float normalised_radius = 52;

        public static (float Radius, float ScalingFactor) Geometry(float circleSize)
        {
            float radius = object_radius * (1 - 0.7f * (circleSize - 5) / 5) / 2;
            float scalingFactor = normalised_radius / radius;

            if (radius < 30)
            {
                float smallCircleBonus = MathF.Min(30 - radius, 5) / 50;
                scalingFactor *= 1 + smallCircleBonus;
            }

            return (radius, scalingFactor);
        }

        public static List<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
        {
            var objects = new List<DifficultyHitObject>(beatmap.HitObjects.Count);

            if (beatmap.HitObjects.Count < 2)
                return objects;

            double clockRate = ModUtils.CalculateRateWithMods(mods);
            var (radius, scalingFactor) = Geometry(beatmap.Difficulty.CircleSize);

            var relaxObjects = beatmap.HitObjects.Cast<OsuHitObject>().Select(h => new Relax2019Object(h, radius, scalingFactor)).ToArray();
            float firstObjectTime = relaxObjects[0].Time;

            for (int i = 1; i < relaxObjects.Length; i++)
            {
                objects.Add(new Relax2019DifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], relaxObjects[i], relaxObjects[i - 1], i > 1 ? relaxObjects[i - 2] : null,
                    firstObjectTime, scalingFactor, clockRate, objects, objects.Count));
            }

            return objects;
        }
    }
}
