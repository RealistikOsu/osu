using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax
{
    /// <summary>
    /// The RealistikOsu relax calc, ported from akatsuki-pp's osu_2019, run through <see cref="OsuDifficultyCalculator"/> when relax is enabled.
    /// </summary>
    public static class RelaxDifficultyCalculation
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
            var (radius, scalingFactor) = Geometry(RelaxDifficultySettings.Create(beatmap, mods).CircleSize);

            bool hardRock = mods.Any(m => m is OsuModHardRock);

            var relaxObjects = beatmap.HitObjects.Cast<OsuHitObject>().Select(h => new RelaxObject(h, radius, scalingFactor, hardRock)).ToArray();
            float firstObjectTime = relaxObjects[0].Time;

            for (int i = 1; i < relaxObjects.Length; i++)
            {
                objects.Add(new RelaxDifficultyHitObject(beatmap.HitObjects[i], beatmap.HitObjects[i - 1], relaxObjects[i], relaxObjects[i - 1], i > 1 ? relaxObjects[i - 2] : null,
                    firstObjectTime, scalingFactor, clockRate, objects, objects.Count));
            }

            return objects;
        }

        public static Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
        {
            var settings = RelaxDifficultySettings.Create(beatmap, mods);

            var skills = new List<Skill>
            {
                new RelaxAim(mods, true),
                new RelaxAim(mods, false),
                new RelaxSpeed(mods),
                new RelaxReading(mods, readingParameters(settings.ApproachRate, mods)),
            };

            // A custom approach rate is rated at whichever of the allowed and played approach rates gives less reading difficulty.
            if (settings.PlayedApproachRate != settings.ApproachRate)
                skills.Add(new RelaxReading(mods, readingParameters(settings.PlayedApproachRate, mods)));

            return skills.ToArray();
        }

        public static OsuRelaxDifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
        {
            var settings = RelaxDifficultySettings.Create(beatmap, mods);
            var parameters = readingParameters(settings.ApproachRate, mods);

            var attributes = new OsuRelaxDifficultyAttributes
            {
                Mods = mods,
                MaxCombo = beatmap.GetMaxCombo(),
                HitCircleCount = beatmap.HitObjects.Count(h => h is HitCircle),
                SliderCount = beatmap.HitObjects.Count(h => h is Slider),
                SpinnerCount = beatmap.HitObjects.Count(h => h is Spinner),
                OverallDifficulty = settings.OverallDifficulty,
                ApproachRate = settings.ApproachRate,
                EffectiveApproachRate = preemptToApproachRate(parameters.Preempt),
                SliderFactor = 1,
            };

            if (beatmap.HitObjects.Count < 2)
                return attributes;

            var aim = skills.OfType<RelaxAim>().Single(a => a.FollowSliders);
            var aimNoSliders = skills.OfType<RelaxAim>().Single(a => !a.FollowSliders);
            var speed = skills.OfType<RelaxSpeed>().Single();

            float aimStrain = MathF.Sqrt(aim.Difficulty()) * difficulty_multiplier;
            float aimNoSlidersStrain = MathF.Sqrt(aimNoSliders.Difficulty()) * difficulty_multiplier;
            float speedStrain = MathF.Sqrt(speed.Difficulty()) * difficulty_multiplier;

            var readings = skills.OfType<RelaxReading>().ToArray();
            var (readingDifficulty, readingDifficultNoteCount) = readings[0].Evaluate();

            if (readings.Length > 1)
            {
                var played = readings[1].Evaluate();

                if (played.Difficulty < readingDifficulty)
                {
                    (readingDifficulty, readingDifficultNoteCount) = played;
                    attributes.ApproachRate = settings.PlayedApproachRate;
                    attributes.EffectiveApproachRate = preemptToApproachRate(readings[1].Parameters.Preempt);
                }
            }

            float readingStrain = MathF.Sqrt(readingDifficulty) * difficulty_multiplier;

            if (settings.BigCirclePenalty < 1)
            {
                aimStrain *= settings.BigCirclePenalty;
                aimNoSlidersStrain *= settings.BigCirclePenalty;
                speedStrain *= settings.BigCirclePenalty;
                readingStrain *= settings.BigCirclePenalty;
            }

            attributes.AimDifficulty = aimStrain;
            attributes.SpeedDifficulty = speedStrain;
            attributes.SliderFactor = aimStrain > 0 ? MathF.Min(aimNoSlidersStrain / aimStrain, 1) : 1;
            attributes.AimDifficultSliderCount = aim.DifficultSliderCount();
            attributes.AimDifficultStrainCount = aim.CountDifficultStrains();
            attributes.SpeedDifficultStrainCount = speed.CountDifficultStrains();

            float sum = aimStrain + speedStrain + readingStrain;
            float maxComponent = MathF.Max(MathF.Max(MathF.Max(0, aimStrain), speedStrain), readingStrain);

            attributes.ReadingDifficulty = readingStrain;
            attributes.ReadingDifficultNoteCount = readingDifficultNoteCount;
            attributes.StarRating = sum + MathF.Abs(maxComponent - sum / 3) / 2;

            return attributes;
        }

        private static RelaxReadingParameters readingParameters(float approachRate, Mod[] mods)
            => RelaxReadingParameters.Create(approachRate, ModUtils.CalculateRateWithMods(mods), mods.Any(m => m is OsuModHidden));

        private static float preemptToApproachRate(float preempt)
        {
            if (preempt < 1200)
                return 5 + 5 * (1200 - preempt) / (1200 - 450);

            return 5 - 5 * (preempt - 1200) / (1800 - 1200);
        }
    }
}
