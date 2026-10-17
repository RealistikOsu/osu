using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Evaluators
{
    public static class RelaxAimEvaluator
    {
        private const float timing_threshold = 107;
        private const float flow_density_timing_threshold = 52;
        private const float flow_density_max_bonus = 15;

        private const float maximum_repetition_nerf = 0.08f;
        private const float repetition_threshold = 1.3f;
        private const float maximum_vector_influence = 1;
        private const float normalised_diameter = 52 * 2;
        private const int note_limit = 6;

        private static readonly float angle_bonus_begin = (float)(Math.PI / 3);

        public static float EvaluateDifficultyOf(RelaxDifficultyHitObject current, RelaxMovement movement, (float JumpDistance, float StrainTime)? previous,
                                                 IReadOnlyList<RelaxRecentObject> recentObjects)
        {
            if (current.Object.IsSpinner)
                return 0;

            float result = 0;

            if (previous is var (previousJumpDistance, previousStrainTime) && movement.Angle is float angle && angle > angle_bonus_begin)
            {
                const float scale = 90;

                float sin = MathF.Sin(angle - angle_bonus_begin);
                float angleBonus = MathF.Sqrt(sin * sin * MathF.Max(previousJumpDistance - scale, 0) * MathF.Max(movement.JumpDistance - scale, 0));

                result = 1.5f * applyDiminishingExp(MathF.Max(angleBonus, 0)) / MathF.Max(timing_threshold, previousStrainTime);
            }

            float jumpDistanceExp = applyDiminishingExp(movement.JumpDistance);
            float travelDistanceExp = applyDiminishingExp(movement.TravelDistance);

            float distanceExp = jumpDistanceExp + travelDistanceExp + MathF.Sqrt(travelDistanceExp * jumpDistanceExp);

            float aimStrain = MathF.Max(result + distanceExp / MathF.Max(current.StrainTime, timing_threshold), distanceExp / current.StrainTime);

            aimStrain *= vectorAngleRepetition(current, movement, recentObjects);
            aimStrain *= flowDensityBonus(current, movement);

            return aimStrain;
        }

        /// <summary>
        /// Penalises repeated angle patterns (kwotaq's vectorAngleRepetition).
        /// </summary>
        private static float vectorAngleRepetition(RelaxDifficultyHitObject current, RelaxMovement movement, IReadOnlyList<RelaxRecentObject> recentObjects)
        {
            if (movement.Angle is not float currentAngle || movement.NormalisedVectorAngle is not float currentVectorAngle)
                return 1;

            if (recentObjects.Count == 0)
                return 1;

            var previous = recentObjects[0];

            if (previous.Angle is not float lastAngle)
                return 1;

            float constantAngleCount = 0;
            int limit = Math.Min(note_limit, recentObjects.Count);

            for (int i = 0; i < limit; i++)
            {
                var loopObject = recentObjects[i];

                float maxDeltaTime = MathF.Max(current.StrainTime, loopObject.StrainTime);
                float minDeltaTime = MathF.Min(current.StrainTime, loopObject.StrainTime);

                if (maxDeltaTime > 1.1f * minDeltaTime)
                    break;

                if (loopObject.NormalisedVectorAngle is float loopVectorAngle)
                {
                    float angleDifference = MathF.Abs(currentVectorAngle - loopVectorAngle);
                    constantAngleCount += MathF.Cos(8 * MathF.Min(angleDifference, RelaxMath.Radians(11.25f)));
                }
            }

            float vectorRepetition = MathF.Min(repetition_threshold / MathF.Max(constantAngleCount, 0), 1);
            vectorRepetition *= vectorRepetition;

            float stackFactor = RelaxMath.Smootherstep(movement.JumpDistance, 0, normalised_diameter);

            float angleDifferenceAdjusted = MathF.Cos(2 * MathF.Min(MathF.Abs(currentAngle - lastAngle) * stackFactor, RelaxMath.Radians(45)));

            float overlappedNotesWeight = 3;

            if (recentObjects.Count >= 2)
            {
                var previousPrevious = recentObjects[1];

                float o1 = overlapness(current.NormalisedPosition, previous.NormalisedPosition);
                float o2 = overlapness(current.NormalisedPosition, previousPrevious.NormalisedPosition);
                float o3 = overlapness(previous.NormalisedPosition, previousPrevious.NormalisedPosition);

                overlappedNotesWeight = 1 - o1 * o2 * o3;
            }

            float acuteBonus = calcAcuteAngleBonus(lastAngle) * overlappedNotesWeight;

            float baseNerf = 1 - maximum_repetition_nerf * acuteBonus * angleDifferenceAdjusted;

            float nerf = baseNerf + (1 - baseNerf) * vectorRepetition * maximum_vector_influence * stackFactor;
            return nerf * nerf;
        }

        /// <summary>
        /// Buffs fast, acute flow aim without broadly buffing jumps.
        /// </summary>
        private static float flowDensityBonus(RelaxDifficultyHitObject current, RelaxMovement movement)
        {
            if (movement.Angle is not float angle)
                return 1;

            float fastness = Math.Clamp((flow_density_timing_threshold - current.StrainTime) / flow_density_timing_threshold, 0, 1);
            float acute = calcAcuteAngleBonus(angle);
            float jumpControl = Math.Clamp(movement.JumpDistance / 120, 0, 1);

            return 1 + flow_density_max_bonus * fastness * acute * jumpControl;
        }

        private static float calcAcuteAngleBonus(float angle) => RelaxMath.Smoothstep(angle, RelaxMath.Radians(140), RelaxMath.Radians(40));

        private static float overlapness(Vector2 a, Vector2 b)
        {
            float distance = (a - b).Length;
            const float object_radius = normalised_diameter / 2;
            float normalised = MathF.Max(distance - object_radius, 0) / object_radius;

            return Math.Clamp(1 - normalised * normalised, 0, 1);
        }

        private static float applyDiminishingExp(float value) => MathF.Pow(value, 0.99f);
    }

    /// <summary>
    /// What the aim skill remembers about a recent object for angle repetition lookback.
    /// </summary>
    public readonly record struct RelaxRecentObject(float? NormalisedVectorAngle, Vector2 NormalisedPosition, float StrainTime, float JumpDistance, float? Angle);
}
