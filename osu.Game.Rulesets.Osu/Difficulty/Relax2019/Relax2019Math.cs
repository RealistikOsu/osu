using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019
{
    public static class Relax2019Math
    {
        private static readonly float radians_per_degree = MathF.PI / 180;

        public static float Radians(float degrees) => degrees * radians_per_degree;

        public static float Smoothstep(float x, float start, float end)
        {
            x = Math.Clamp((x - start) / (end - start), 0, 1);
            return x * x * (3 - 2 * x);
        }

        public static float Smootherstep(float x, float start, float end)
        {
            x = Math.Clamp((x - start) / (end - start), 0, 1);
            return x * x * x * (x * (6 * x - 15) + 10);
        }
    }
}
