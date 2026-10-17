using System;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Skills
{
    /// <summary>
    /// Reading inputs of one object. <see cref="Time"/> is not adjusted for clock rate; <see cref="StrainTime"/> and <see cref="Delta"/> are.
    /// </summary>
    public readonly record struct RelaxReadingObject(float Time, float JumpDistance, float StrainTime, float Delta, float? Angle, float? NormalisedVectorAngle)
    {
        public static RelaxReadingObject First(float time) => new RelaxReadingObject(time, 0, 0, 0, null, null);

        public static RelaxReadingObject From(RelaxDifficultyHitObject obj)
            => new RelaxReadingObject(obj.Time, obj.WithSliders.JumpDistance, obj.StrainTime, obj.Delta, obj.WithSliders.Angle, obj.WithSliders.NormalisedVectorAngle);
    }

    public readonly record struct RelaxReadingParameters(float TimePreemptRaw, float Preempt, float TimeFadeInRaw, float HiddenFadeInRaw, float ClockRate, bool Hidden)
    {
        public static RelaxReadingParameters Create(float approachRate, double clockRate, bool hidden)
        {
            float preempt = (float)(IBeatmapDifficultyInfo.DifficultyRange(approachRate, 1800, 1200, 450) / clockRate);
            float timePreemptRaw = preempt * (float)clockRate;

            return new RelaxReadingParameters(timePreemptRaw, preempt, 400 * MathF.Min(timePreemptRaw / 450, 1), timePreemptRaw * 0.4f, (float)clockRate, hidden);
        }
    }
}
