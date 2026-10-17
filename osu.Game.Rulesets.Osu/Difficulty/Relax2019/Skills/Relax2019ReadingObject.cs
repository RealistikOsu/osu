using System;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019.Skills
{
    /// <summary>
    /// Reading inputs of one object. <see cref="Time"/> is not adjusted for clock rate; <see cref="StrainTime"/> and <see cref="Delta"/> are.
    /// </summary>
    public readonly record struct Relax2019ReadingObject(float Time, float JumpDistance, float StrainTime, float Delta, float? Angle, float? NormalisedVectorAngle)
    {
        public static Relax2019ReadingObject First(float time) => new Relax2019ReadingObject(time, 0, 0, 0, null, null);

        public static Relax2019ReadingObject From(Relax2019DifficultyHitObject obj)
            => new Relax2019ReadingObject(obj.Time, obj.WithSliders.JumpDistance, obj.StrainTime, obj.Delta, obj.WithSliders.Angle, obj.WithSliders.NormalisedVectorAngle);
    }

    public readonly record struct Relax2019ReadingParameters(float TimePreemptRaw, float Preempt, float TimeFadeInRaw, float HiddenFadeInRaw, float ClockRate, bool Hidden)
    {
        public static Relax2019ReadingParameters Create(float approachRate, double clockRate, bool hidden)
        {
            float preempt = (float)(IBeatmapDifficultyInfo.DifficultyRange(approachRate, 1800, 1200, 450) / clockRate);
            float timePreemptRaw = preempt * (float)clockRate;

            return new Relax2019ReadingParameters(timePreemptRaw, preempt, 400 * MathF.Min(timePreemptRaw / 450, 1), timePreemptRaw * 0.4f, (float)clockRate, hidden);
        }
    }
}
