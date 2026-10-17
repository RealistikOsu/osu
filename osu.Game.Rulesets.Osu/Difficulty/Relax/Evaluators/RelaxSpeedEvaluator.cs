using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Evaluators
{
    public static class RelaxSpeedEvaluator
    {
        private const float single_spacing_threshold = 125;
        private const float min_speed_bonus = 75;
        private const float max_speed_bonus = 45;
        private const float speed_balancing_factor = 40;
        private const float rate_penalty_min = 0.7f;

        private static readonly float pi_over_4 = (float)(Math.PI / 4);
        private static readonly float pi_over_2 = (float)(Math.PI / 2);
        private static readonly float speed_angle_bonus_begin = 5 * (float)(Math.PI / 6);

        public static float EvaluateDifficultyOf(RelaxDifficultyHitObject current)
        {
            if (current.Object.IsSpinner)
                return 0;

            var movement = current.WithSliders;

            float distance = MathF.Min(single_spacing_threshold, movement.TravelDistance + movement.JumpDistance);
            float deltaTime = MathF.Max(max_speed_bonus, current.Delta);

            float speedBonus = 1;

            if (deltaTime < min_speed_bonus)
            {
                float expBase = (min_speed_bonus - deltaTime) / speed_balancing_factor;
                speedBonus += expBase * expBase;
            }

            float rate = current.Rate;

            float ratePenalty = rate < 1
                ? 1 - 0.9f * (1 - rate)
                : rate < 1.5f
                    ? 1 - 0.7f * (rate - 1)
                    : 1.5f - 0.01f * (rate - 1.5f);

            ratePenalty = Math.Clamp(ratePenalty, rate_penalty_min, 1);

            float angleBonus = 1;

            if (movement.Angle is float angle && angle < speed_angle_bonus_begin)
            {
                float expBase = MathF.Sin(1.5f * (speed_angle_bonus_begin - angle));
                angleBonus = 1 + expBase * expBase / 3.57f;

                if (angle < pi_over_2)
                {
                    angleBonus = 1.28f;

                    if (distance < 90 && angle < pi_over_4)
                        angleBonus += (1 - angleBonus) * MathF.Min((90 - distance) / 10, 1);
                    else if (distance < 90)
                        angleBonus += (1 - angleBonus) * MathF.Min((90 - distance) / 10, 1) * MathF.Sin((pi_over_2 - angle) / pi_over_4);
                }
            }

            return (1 + (speedBonus - 1) * 0.75f)
                   * angleBonus
                   * (0.95f + speedBonus * MathF.Pow(distance / single_spacing_threshold, 3.5f))
                   / current.StrainTime
                   * ratePenalty;
        }
    }
}
