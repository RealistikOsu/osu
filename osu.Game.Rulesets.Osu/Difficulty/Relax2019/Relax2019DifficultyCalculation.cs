using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019.Skills;
using osu.Game.Rulesets.Osu.Mods;
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
        private const float difficulty_multiplier = 0.0675f;

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

            bool hardRock = mods.Any(m => m is OsuModHardRock);

            var relaxObjects = beatmap.HitObjects.Cast<OsuHitObject>().Select(h => new Relax2019Object(h, radius, scalingFactor, hardRock)).ToArray();
            float firstObjectTime = relaxObjects[0].Time;

            for (int i = 1; i < relaxObjects.Length; i++)
            {
                objects.Add(new Relax2019DifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], relaxObjects[i], relaxObjects[i - 1], i > 1 ? relaxObjects[i - 2] : null,
                    firstObjectTime, scalingFactor, clockRate, objects, objects.Count));
            }

            return objects;
        }

        public static Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => new Skill[]
        {
            new Relax2019Aim(mods, true),
            new Relax2019Aim(mods, false),
            new Relax2019Speed(mods),
            new Relax2019Reading(mods, Relax2019ReadingParameters.Create(beatmap.Difficulty.ApproachRate, ModUtils.CalculateRateWithMods(mods), mods.Any(m => m is OsuModHidden))),
        };

        public static OsuRelax2019DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            var parameters = Relax2019ReadingParameters.Create(beatmap.Difficulty.ApproachRate, ModUtils.CalculateRateWithMods(mods), mods.Any(m => m is OsuModHidden));

            var attributes = new OsuRelax2019DifficultyAttributes
            {
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle),
                SliderCount = beatmap.HitObjects.Count(h => h is Slider),
                SpinnerCount = beatmap.HitObjects.Count(h => h is Spinner),
                OverallDifficulty = beatmap.Difficulty.OverallDifficulty,
                ApproachRate = beatmap.Difficulty.ApproachRate,
                EffectiveApproachRate = preemptToApproachRate(parameters.Preempt),
                SliderFactor = 1,
            };

            if (beatmap.HitObjects.Count < 2)
                return attributes;

            var aim = skills.OfType<Relax2019Aim>().Single(a => a.FollowSliders);
            var aimNoSliders = skills.OfType<Relax2019Aim>().Single(a => !a.FollowSliders);
            var speed = skills.OfType<Relax2019Speed>().Single();

            float aimStrain = MathF.Sqrt(aim.Difficulty()) * difficulty_multiplier;
            float aimNoSlidersStrain = MathF.Sqrt(aimNoSliders.Difficulty()) * difficulty_multiplier;
            float speedStrain = MathF.Sqrt(speed.Difficulty()) * difficulty_multiplier;

            attributes.AimDifficulty = aimStrain;
            attributes.SpeedDifficulty = speedStrain;
            attributes.SliderFactor = aimStrain > 0 ? MathF.Min(aimNoSlidersStrain / aimStrain, 1) : 1;
            attributes.AimDifficultSliderCount = aim.DifficultSliderCount();
            attributes.AimDifficultStrainCount = aim.CountDifficultStrains();
            attributes.SpeedDifficultStrainCount = speed.CountDifficultStrains();

            var reading = skills.OfType<Relax2019Reading>().Single();
            var (readingDifficulty, readingDifficultNoteCount) = reading.Evaluate();
            float readingStrain = MathF.Sqrt(readingDifficulty) * difficulty_multiplier;

            float sum = aimStrain + speedStrain + readingStrain;
            float maxComponent = MathF.Max(MathF.Max(MathF.Max(0, aimStrain), speedStrain), readingStrain);

            attributes.ReadingDifficulty = readingStrain;
            attributes.ReadingDifficultNoteCount = readingDifficultNoteCount;
            attributes.StarRating = sum + MathF.Abs(maxComponent - sum / 3) / 2;

            return attributes;
        }

        private static float preemptToApproachRate(float preempt)
        {
            if (preempt < 1200)
                return 5 + 5 * (1200 - preempt) / (1200 - 450);

            return 5 - 5 * (preempt - 1200) / (1800 - 1200);
        }
    }
}
