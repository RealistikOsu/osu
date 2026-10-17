using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax
{
    /// <summary>
    /// Cursor movement onto an object, either following sliders to their lazy end or staying on slider heads.
    /// </summary>
    public readonly struct RelaxMovement
    {
        public readonly float JumpDistance;
        public readonly float TravelDistance;
        public readonly float? Angle;

        /// <summary>
        /// Angle of the movement vector folded into the first quadrant, so mirrored vectors compare equal.
        /// </summary>
        public readonly float? NormalisedVectorAngle;

        private RelaxMovement(float jumpDistance, float travelDistance, float? angle, float? normalisedVectorAngle)
        {
            JumpDistance = jumpDistance;
            TravelDistance = travelDistance;
            Angle = angle;
            NormalisedVectorAngle = normalisedVectorAngle;
        }

        public static RelaxMovement Compute(RelaxObject current, RelaxObject previous, RelaxObject? previousPrevious, float scalingFactor, bool followSliders)
        {
            Vector2 cursor(RelaxObject obj) => followSliders ? obj.EndPosition : obj.Position;

            float travelDistance = followSliders ? previous.TravelDistance ?? 0 : 0;
            Vector2 previousCursor = cursor(previous);

            float jumpDistance = current.IsSpinner ? 0 : ((current.Position - previousCursor) * scalingFactor).Length;

            float? normalisedVectorAngle = null;

            if (!current.IsSpinner && previousPrevious != null)
            {
                Vector2 v = current.Position - previousCursor;
                normalisedVectorAngle = MathF.Atan2(MathF.Abs(v.Y), MathF.Abs(v.X));
            }

            float? angle = null;

            if (previousPrevious != null)
            {
                Vector2 v1 = cursor(previousPrevious) - previous.Position;
                Vector2 v2 = current.Position - previousCursor;

                float dot = v1.X * v2.X + v1.Y * v2.Y;
                float det = v1.X * v2.Y - v1.Y * v2.X;

                angle = MathF.Abs(MathF.Atan2(det, dot));
            }

            return new RelaxMovement(jumpDistance, travelDistance, angle, normalisedVectorAngle);
        }
    }
}
