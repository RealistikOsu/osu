using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    public class Relax2019DifficultyHitObject : DifficultyHitObject
    {
        public readonly Relax2019Object Object;

        /// <summary>
        /// Start time, not adjusted for clock rate.
        /// </summary>
        public readonly float Time;

        public readonly float Delta;
        public readonly float StrainTime;
        public readonly float Rate;

        /// <summary>
        /// Start time of the beatmap's first object, not adjusted for clock rate.
        /// </summary>
        public readonly float FirstObjectTime;

        public readonly Vector2 NormalisedPosition;
        public readonly Relax2019Movement WithSliders;
        public readonly Relax2019Movement WithoutSliders;

        public Relax2019DifficultyHitObject(HitObject hitObject, HitObject lastObject, Relax2019Object current, Relax2019Object previous, Relax2019Object? previousPrevious,
                                            float firstObjectTime, float scalingFactor, double clockRate, List<DifficultyHitObject> objects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            Object = current;
            Time = current.Time;
            Rate = (float)clockRate;
            Delta = (current.Time - previous.Time) / Rate;
            StrainTime = MathF.Max(Delta, 50);
            FirstObjectTime = firstObjectTime;
            NormalisedPosition = current.Position * scalingFactor;
            WithSliders = Relax2019Movement.Compute(current, previous, previousPrevious, scalingFactor, true);
            WithoutSliders = Relax2019Movement.Compute(current, previous, previousPrevious, scalingFactor, false);
        }

        public Relax2019Movement Movement(bool followSliders) => followSliders ? WithSliders : WithoutSliders;
    }
}
