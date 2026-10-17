using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    /// <summary>
    /// 2019 relax pp. Unjudged objects count as 300s, as in the Rust calc; non-finite values (which the Rust calc can produce on degenerate input) become 0.
    /// For non-classic scores, sliders without a SliderTailHit count as dropped, so failed plays should be scored against attributes of the passed objects.
    /// </summary>
    public class Relax2019PerformanceCalculation
    {
        private const float slider_nerf_max_strength = 0.7f;
        private const float slider_nerf_exponent = 1;

        private const float ez_low_ar_threshold = 8;
        private const float ez_low_ar_bonus_per_ar = 0.04f;
        private const float ez_low_ar_full_length = 1000;
        private const float ez_aim_multiplier = 1.10f;

        private readonly OsuRelax2019DifficultyAttributes attributes;
        private readonly IReadOnlyList<Mod> mods;

        private readonly int objectCount;
        private readonly int combo;
        private readonly int countGreat;
        private readonly int countOk;
        private readonly int countMeh;
        private readonly int countMiss;
        private readonly float droppedSliderEstimate;

        private readonly float accuracy;
        private readonly float totalHits;
        private readonly float overallDifficulty;

        public Relax2019PerformanceCalculation(ScoreInfo score, OsuRelax2019DifficultyAttributes attributes)
        {
            this.attributes = attributes;
            mods = score.Mods;

            objectCount = attributes.HitCircleCount + attributes.SliderCount + attributes.SpinnerCount;
            combo = score.MaxCombo;
            countGreat = score.Statistics.GetValueOrDefault(HitResult.Great);
            countOk = score.Statistics.GetValueOrDefault(HitResult.Ok);
            countMeh = score.Statistics.GetValueOrDefault(HitResult.Meh);
            countMiss = score.Statistics.GetValueOrDefault(HitResult.Miss);

            int unjudged = objectCount - countGreat - countOk - countMeh - countMiss;

            if (unjudged > 0)
                countGreat += unjudged;

            totalHits = Math.Min(countGreat + countOk + countMeh + countMiss, objectCount);
            accuracy = objectCount > 0 ? (countMeh + countOk * 2 + countGreat * 6) / (float)objectCount / 6 : 0;
            overallDifficulty = (float)attributes.OverallDifficulty;

            bool classicSliderAccuracy = score.Mods.OfType<OsuModClassic>().Any(m => m.NoSliderHeadAccuracy.Value);

            droppedSliderEstimate = classicSliderAccuracy
                ? MathF.Min(countOk + countMeh + countMiss, attributes.MaxCombo - (float)combo)
                : attributes.SliderCount - score.Statistics.GetValueOrDefault(HitResult.SliderTailHit) + score.Statistics.GetValueOrDefault(HitResult.LargeTickMiss);
        }

        public OsuPerformanceAttributes Calculate()
        {
            if (objectCount == 0)
                return new OsuPerformanceAttributes();

            float multiplier = 1.09f;
            float effectiveMissCount = calculateEffectiveMissCount();

            if (mods.Any(m => m is OsuModSpunOut))
                multiplier *= 1 - MathF.Pow(attributes.SpinnerCount / totalHits, 0.85f);

            float aimValue = computeAimValue(effectiveMissCount);
            float speedValue = computeSpeedValue(effectiveMissCount);
            float accuracyValue = computeAccuracyValue();
            float readingValue = computeReadingValue(effectiveMissCount);

            float accDepression = 1;
            double streamsNerf = Math.Round(attributes.AimDifficulty / attributes.SpeedDifficulty * 100, MidpointRounding.AwayFromZero) / 100;

            if (streamsNerf < 1.09)
            {
                float accFactor = MathF.Abs(1 - accuracy);
                accDepression = MathF.Max(0.83f - accFactor, 0.5f);

                if (accDepression > 0)
                    aimValue *= accDepression;
            }

            float total = MathF.Pow(
                              MathF.Pow(aimValue, 1.185f)
                              + MathF.Pow(speedValue, 0.83f * accDepression)
                              + MathF.Pow(accuracyValue, 1.14f)
                              + MathF.Pow(readingValue, 1.1f), 1 / 1.1f)
                          * multiplier;

            return new OsuPerformanceAttributes
            {
                Aim = finite(aimValue),
                Speed = finite(speedValue),
                Accuracy = finite(accuracyValue),
                Reading = finite(readingValue),
                EffectiveMissCount = finite(effectiveMissCount),
                Total = finite(total),
            };
        }

        private float computeAimValue(float effectiveMissCount)
        {
            float rawAim = mods.Any(m => m is OsuModTouchDevice)
                ? (float)Math.Pow(attributes.AimDifficulty, 0.8)
                : (float)attributes.AimDifficulty;

            float difficultSliders = (float)attributes.AimDifficultSliderCount;

            if (attributes.SliderCount > 0 && difficultSliders > 0)
            {
                float droppedDifficultSliders = Math.Clamp(droppedSliderEstimate, 0, difficultSliders);
                float minFactor = 1 - slider_nerf_max_strength * (1 - (float)attributes.SliderFactor);

                rawAim *= (1 - minFactor) * MathF.Pow(1 - droppedDifficultSliders / difficultSliders, slider_nerf_exponent) + minFactor;
            }

            float aimValue = difficultyToPerformance(rawAim / 0.0675f) / 100000;

            float lengthBonus = 0.79f + 0.59f * MathF.Min(totalHits / 2000, 1) + (totalHits > 2000 ? 1 : 0) * -0.1f * MathF.Log10(totalHits / 2000);

            if (lengthBonus > 1)
                lengthBonus = MathF.Pow(lengthBonus, 0.88f);

            aimValue *= lengthBonus;

            if (effectiveMissCount > 0)
                aimValue *= calculateMissPenalty(effectiveMissCount, (float)attributes.AimDifficultStrainCount);

            if (mods.Any(m => m is OsuModFlashlight))
            {
                aimValue *= 1
                            + 0.3f * MathF.Min(totalHits / 200, 1)
                            + (totalHits > 200 ? 1 : 0) * 0.25f * MathF.Min((totalHits - 200) / 300, 1)
                            + (totalHits > 500 ? 1 : 0) * (totalHits - 500) / 1600;
            }

            aimValue *= MathF.Pow(accuracy, 1.5f) * 0.85f;
            aimValue *= 0.98f + overallDifficulty * overallDifficulty / 2500;

            if (mods.Any(m => m is OsuModEasy))
                aimValue *= easyAimMultiplier((float)attributes.EffectiveApproachRate);

            return aimValue;
        }

        private float computeSpeedValue(float effectiveMissCount)
        {
            float speedValue = difficultyToPerformance((float)attributes.SpeedDifficulty / 0.0690f) / 100000;

            float lengthBonus = 0.81f + 0.55f * MathF.Min(totalHits / 2000, 1) + (totalHits > 2000 ? 1 : 0) * -0.1f * MathF.Log10(totalHits / 2000);

            if (lengthBonus > 1)
                lengthBonus = MathF.Pow(lengthBonus, 0.9f);

            speedValue *= lengthBonus;

            if (effectiveMissCount > 0)
                speedValue *= calculateMissPenalty(effectiveMissCount, (float)attributes.SpeedDifficultStrainCount);

            speedValue *= (0.93f + overallDifficulty * overallDifficulty / 750) * MathF.Pow(accuracy, (14.5f - (float)Math.Max(attributes.OverallDifficulty, 8)) / 2);

            speedValue *= MathF.Pow(0.98f, countMeh < totalHits / 500 ? 0 : countMeh - totalHits / 500);

            return speedValue;
        }

        private float computeAccuracyValue()
        {
            float circles = attributes.HitCircleCount;

            float betterAccuracyPercentage = circles > 0
                ? MathF.Max(((countGreat - (totalHits - circles)) * 6 + countOk * 2 + countMeh) / (circles * 6), 0)
                : 0;

            float accuracyValue = MathF.Pow(1.52163f, overallDifficulty) * MathF.Pow(betterAccuracyPercentage, 24) * 2.83f;

            accuracyValue *= MathF.Min(MathF.Pow(circles / 1000, 0.3f), 1.15f);

            if (mods.Any(m => m is OsuModFlashlight))
                accuracyValue *= 1.02f;

            return accuracyValue;
        }

        private float computeReadingValue(float effectiveMissCount)
        {
            float readingValue = difficultyToPerformance((float)attributes.ReadingDifficulty / 0.0675f) / 265000;

            if (effectiveMissCount > 0)
                readingValue *= calculateMissPenalty(effectiveMissCount, (float)attributes.ReadingDifficultNoteCount);

            float accuracySquared = accuracy * accuracy;
            readingValue *= accuracySquared * accuracySquared;

            return readingValue;
        }

        private float calculateEffectiveMissCount()
        {
            float comboBasedMissCount = 0;

            if (attributes.SliderCount > 0)
            {
                float fullComboThreshold = attributes.MaxCombo - 0.1f * attributes.SliderCount;

                if (combo < fullComboThreshold)
                    comboBasedMissCount = fullComboThreshold / MathF.Max(combo, 1);
            }

            comboBasedMissCount = MathF.Min(comboBasedMissCount, countOk + countMeh + countMiss);
            return MathF.Max(comboBasedMissCount, countMiss);
        }

        private static float calculateMissPenalty(float effectiveMissCount, float difficultStrainCount)
            => 0.96f / (effectiveMissCount / (4 * MathF.Pow(MathF.Log(difficultStrainCount), 0.94f)) + 1);

        /// <summary>
        /// Relax EZ rework: flat compensation for the halved circle size plus a low-AR bonus that grows with map length.
        /// </summary>
        private float easyAimMultiplier(float effectiveApproachRate)
        {
            float arBelow = MathF.Max(ez_low_ar_threshold - effectiveApproachRate, 0);
            float lengthScale = MathF.Min(totalHits / ez_low_ar_full_length, 1);

            return ez_aim_multiplier * (1 + ez_low_ar_bonus_per_ar * arBelow * lengthScale);
        }

        private static float difficultyToPerformance(float scaledStrain)
        {
            float x = 5 * MathF.Max(scaledStrain, 1) - 4;
            return x * x * x;
        }

        private static double finite(float value) => float.IsFinite(value) ? value : 0;
    }
}
