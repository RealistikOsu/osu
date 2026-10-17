using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax
{
    public class RelaxDifficultyHitObject : DifficultyHitObject
    {
        public readonly RelaxObject Object;

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
        public readonly RelaxMovement WithSliders;
        public readonly RelaxMovement WithoutSliders;

        public RelaxDifficultyHitObject(HitObject hitObject, HitObject lastObject, RelaxObject current, RelaxObject previous, RelaxObject? previousPrevious,
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
            WithSliders = RelaxMovement.Compute(current, previous, previousPrevious, scalingFactor, true);
            WithoutSliders = RelaxMovement.Compute(current, previous, previousPrevious, scalingFactor, false);
        }

        public RelaxMovement Movement(bool followSliders) => followSliders ? WithSliders : WithoutSliders;
    }
}
