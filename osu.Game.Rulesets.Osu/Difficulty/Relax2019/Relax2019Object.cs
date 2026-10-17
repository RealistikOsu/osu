using System;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    /// <summary>
    /// A hit object as seen by the 2019 calc: unstacked position, lazy slider end and slider travel.
    /// </summary>
    public class Relax2019Object
    {
        private const double legacy_last_tick_offset = 36;

        public readonly float Time;
        public readonly Vector2 Position;
        public readonly Vector2 EndPosition;

        /// <summary>
        /// Scaled slider travel distance. 0 for circles, null for spinners.
        /// </summary>
        public readonly float? TravelDistance;

        public bool IsSpinner => TravelDistance == null;

        /// <param name="hitObject">The hit object.</param>
        /// <param name="radius">Circle radius from CS.</param>
        /// <param name="scalingFactor">Scale to normalised distances.</param>
        /// <param name="undoVerticalReflection">Undo hard rock's reflection. The 2019 calc uses the unreflected positions, which matters for the sign of degenerate (stacked) angles.</param>
        public Relax2019Object(OsuHitObject hitObject, float radius, float scalingFactor, bool undoVerticalReflection = false)
        {
            Time = (float)hitObject.StartTime;
            Position = hitObject.Position;
            EndPosition = hitObject.Position;

            switch (hitObject)
            {
                case Spinner:
                    TravelDistance = null;
                    break;

                case Slider slider:
                    float travel = computeLazyEnd(slider, radius, ref EndPosition);
                    TravelDistance = travel * scalingFactor;
                    break;

                default:
                    TravelDistance = 0;
                    break;
            }

            if (undoVerticalReflection)
            {
                Position.Y = OsuPlayfield.BASE_SIZE.Y - Position.Y;
                EndPosition.Y = OsuPlayfield.BASE_SIZE.Y - EndPosition.Y;
            }
        }

        private static float computeLazyEnd(Slider slider, float radius, ref Vector2 endPosition)
        {
            float followRadius = radius * 3;
            float travel = 0;
            var end = endPosition;

            void addVertex(double time)
            {
                double progress = (time - slider.StartTime) / slider.SpanDuration;

                if (progress % 2 >= 1)
                    progress = 1 - progress % 1;
                else
                    progress %= 1;

                Vector2 diff = slider.Position + slider.Path.PositionAt(progress) - end;
                float dist = diff.Length;

                if (dist > followRadius)
                {
                    dist -= followRadius;
                    end += Vector2.Normalize(diff) * dist;
                    travel += dist;
                }
            }

            foreach (var nested in slider.NestedHitObjects)
            {
                if (nested is SliderTick or SliderRepeat)
                    addVertex(nested.StartTime);
            }

            double finalSpanStartTime = slider.StartTime + slider.RepeatCount * slider.SpanDuration;
            addVertex(Math.Max(slider.StartTime + slider.Duration / 2, finalSpanStartTime + slider.SpanDuration - legacy_last_tick_offset));

            endPosition = end;
            return travel;
        }
    }
}
