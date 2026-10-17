using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Skills;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Evaluators
{
    /// <summary>
    /// kwotaq's reading evaluator as used by the relax calc: note density, hidden, preempt and constant angle nerf.
    /// </summary>
    public static class RelaxReadingEvaluator
    {
        public const float READING_WINDOW_SIZE = 3000;

        private const float normalised_radius = 52;
        private const float normalised_diameter = normalised_radius * 2;
        private const float distance_influence_threshold = normalised_diameter * 1.5f;
        private const float hidden_multiplier = 0.28f;
        private const float density_multiplier = 2.4f;
        private const float density_difficulty_base = 2.5f;
        private const float preempt_balancing_factor = 140000;
        private const float preempt_starting_point = 500;
        private const float minimum_angle_relevancy_time = 2000;
        private const float maximum_angle_relevancy_time = 200;
        private const float hidden_fade_out_duration_multiplier = 0.3f;

        public static float EvaluateDifficultyOf(IReadOnlyList<RelaxReadingObject> objects, int index, RelaxReadingParameters parameters)
        {
            if (index == 0)
                return 0;

            var current = objects[index];
            float velocity = MathF.Max(1, current.JumpDistance / current.StrainTime);

            float currentVisibleDensity = retrieveCurrentVisibleObjectDensity(objects, index, parameters);
            float pastInfluence = getPastObjectDifficultyInfluence(objects, index, parameters);
            float constantAngleNerf = getConstantAngleNerfFactor(objects, index);

            RelaxReadingObject? next = index + 1 < objects.Count ? objects[index + 1] : null;

            float densityDifficulty = calculateDensityDifficulty(next, velocity, constantAngleNerf, pastInfluence, currentVisibleDensity);

            float hiddenDifficulty = parameters.Hidden
                ? calculateHiddenDifficulty(objects, index, parameters, pastInfluence, currentVisibleDensity, velocity, constantAngleNerf)
                : 0;

            float preemptDifficulty = calculatePreemptDifficulty(velocity, constantAngleNerf, parameters.Preempt);

            const float p = 1.5f;
            return MathF.Pow(MathF.Pow(preemptDifficulty, p) + MathF.Pow(hiddenDifficulty, p) + MathF.Pow(densityDifficulty, p), 1 / p);
        }

        private static float calculateDensityDifficulty(RelaxReadingObject? next, float velocity, float constantAngleNerf, float pastInfluence, float currentVisibleDensity)
        {
            float futureInfluence = MathF.Sqrt(currentVisibleDensity);

            if (next is RelaxReadingObject nextObject)
                futureInfluence *= RelaxMath.Smootherstep(nextObject.JumpDistance, 15, distance_influence_threshold);

            float densityDifficulty = MathF.Pow(pastInfluence + futureInfluence, 1.7f) * 0.4f * constantAngleNerf * velocity;

            densityDifficulty = MathF.Max(densityDifficulty - density_difficulty_base, 0);

            return MathF.Pow(densityDifficulty, 0.45f) * density_multiplier;
        }

        private static float calculatePreemptDifficulty(float velocity, float constantAngleNerf, float preempt)
        {
            float difference = preempt_starting_point - preempt;
            float halfRectified = (difference + MathF.Abs(difference)) / 2;
            float preemptDifficulty = MathF.Pow(halfRectified, 2.5f) / preempt_balancing_factor;

            return preemptDifficulty * constantAngleNerf * velocity;
        }

        private static float calculateHiddenDifficulty(IReadOnlyList<RelaxReadingObject> objects, int index, RelaxReadingParameters parameters, float pastInfluence,
                                                       float currentVisibleDensity, float velocity, float constantAngleNerf)
        {
            float timeSpentInvisible = (parameters.HiddenFadeInRaw + parameters.TimePreemptRaw * hidden_fade_out_duration_multiplier) / parameters.ClockRate;
            float timeInvisibleFactor = MathF.Pow(timeSpentInvisible, 2.2f) * 0.022f;
            float densityFactor = MathF.Pow(currentVisibleDensity + pastInfluence, 3.3f) * 3;

            float hiddenDifficulty = (timeInvisibleFactor + densityFactor) * constantAngleNerf * velocity * 0.01f;

            hiddenDifficulty = MathF.Pow(hiddenDifficulty, 0.4f) * hidden_multiplier;

            var current = objects[index];
            var previous = objects[index - 1];

            if (current.JumpDistance == 0)
            {
                float previousClickWithPreempt = previous.Time + parameters.TimePreemptRaw;
                float currentOpacity = opacityAt(current.Time, previousClickWithPreempt, parameters, true);

                if (currentOpacity == 0 && previousClickWithPreempt > current.Time)
                    hiddenDifficulty += hidden_multiplier * 7500 / MathF.Pow(current.StrainTime, 1.5f);
            }

            return hiddenDifficulty;
        }

        private static float getPastObjectDifficultyInfluence(IReadOnlyList<RelaxReadingObject> objects, int index, RelaxReadingParameters parameters)
        {
            var current = objects[index];
            float influence = 0;

            for (int i = index - 1; i >= 0; i--)
            {
                var loopObject = objects[i];
                float timeGap = current.Time - loopObject.Time;

                if (timeGap > READING_WINDOW_SIZE)
                    break;

                if (loopObject.Time + parameters.TimePreemptRaw < current.Time)
                    break;

                float loopDifficulty = opacityAt(current.Time, loopObject.Time, parameters, false);
                float distanceFactor = RelaxMath.Smootherstep(loopObject.JumpDistance, 15, distance_influence_threshold);
                float timeNerf = getTimeNerfFactor(timeGap);

                influence += loopDifficulty * distanceFactor * timeNerf;
            }

            return influence;
        }

        private static float retrieveCurrentVisibleObjectDensity(IReadOnlyList<RelaxReadingObject> objects, int index, RelaxReadingParameters parameters)
        {
            var current = objects[index];
            float density = 0;

            for (int i = index + 1; i < objects.Count; i++)
            {
                var hitObject = objects[i];
                float timeGap = hitObject.Time - current.Time;

                if (timeGap > READING_WINDOW_SIZE)
                    break;

                if (current.Time + parameters.TimePreemptRaw < hitObject.Time)
                    break;

                density += opacityAt(hitObject.Time, current.Time, parameters, false) * getTimeNerfFactor(timeGap);
            }

            return density;
        }

        private static float getConstantAngleNerfFactor(IReadOnlyList<RelaxReadingObject> objects, int index)
        {
            var current = objects[index];
            float constantAngleCount = 0;
            float currentTimeGap = 0;
            int i = 0;

            while (currentTimeGap < minimum_angle_relevancy_time)
            {
                if (i >= index)
                    break;

                var loopObject = objects[index - 1 - i];

                float longIntervalFactor = 1 - reverseLerp(loopObject.StrainTime, maximum_angle_relevancy_time, minimum_angle_relevancy_time);

                if (current.Angle is float currentAngle && loopObject.Angle is float loopAngle)
                {
                    float angleDifference = MathF.Abs(currentAngle - loopAngle);
                    float stackFactor = RelaxMath.Smootherstep(loopObject.JumpDistance, 0, normalised_radius);

                    float clampedAngle = MathF.Min(angleDifference * stackFactor, RelaxMath.Radians(30));
                    constantAngleCount += MathF.Cos(3 * clampedAngle) * longIntervalFactor;
                }

                currentTimeGap = current.Time - loopObject.Time;
                i++;
            }

            return Math.Clamp(2 / constantAngleCount, 0.2f, 1);
        }

        private static float opacityAt(float objectTime, float queryTime, RelaxReadingParameters parameters, bool hidden)
        {
            float fadeInStart = objectTime - parameters.TimePreemptRaw;
            float fadeInDuration = parameters.TimeFadeInRaw;

            if (!hidden)
                return Math.Clamp((queryTime - fadeInStart) / fadeInDuration, 0, 1);

            float fadeOutStart = objectTime - parameters.TimePreemptRaw + fadeInDuration;
            float fadeOutDuration = parameters.TimePreemptRaw * hidden_fade_out_duration_multiplier;

            float fadeIn = Math.Clamp((queryTime - fadeInStart) / fadeInDuration, 0, 1);
            float fadeOut = Math.Clamp((queryTime - fadeOutStart) / fadeOutDuration, 0, 1);

            return fadeIn * (1 - fadeOut);
        }

        private static float getTimeNerfFactor(float deltaTime) => Math.Clamp(2 - deltaTime / (READING_WINDOW_SIZE / 2), 0, 1);

        private static float reverseLerp(float x, float start, float end) => Math.Clamp((x - start) / (end - start), 0, 1);
    }
}
