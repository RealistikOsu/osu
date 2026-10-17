using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019.Skills
{
    public class Relax2019Aim : Relax2019StrainSkill
    {
        private const int max_recent_objects = 8;

        public readonly bool FollowSliders;

        // Newest first.
        private readonly List<Relax2019RecentObject> recentObjects = new List<Relax2019RecentObject>(max_recent_objects + 1);

        // Strains of objects right after a slider, i.e. ones that include its travel.
        private readonly List<float> sliderStrains = new List<float>();

        public Relax2019Aim(Mod[] mods, bool followSliders)
            : base(mods)
        {
            FollowSliders = followSliders;
        }

        protected override float SkillMultiplier => 26.25f;

        protected override float StrainDecayBase => 0.15f;

        protected override bool PenaliseIsolatedSpikes => true;

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            double strain = base.ProcessInternal(current);

            if (FollowSliders && ((Relax2019DifficultyHitObject)current).WithSliders.TravelDistance > 0)
                sliderStrains.Add((float)strain);

            return strain;
        }

        protected override float StrainValueOf(Relax2019DifficultyHitObject current)
        {
            var movement = current.Movement(FollowSliders);

            (float, float)? previous = null;

            if (current.Previous(0) is Relax2019DifficultyHitObject previousObject)
                previous = (previousObject.Movement(FollowSliders).JumpDistance, previousObject.StrainTime);

            float strain = Relax2019AimEvaluator.EvaluateDifficultyOf(current, movement, previous, recentObjects);

            recentObjects.Insert(0, new Relax2019RecentObject(movement.NormalisedVectorAngle, current.NormalisedPosition, current.StrainTime, movement.JumpDistance, movement.Angle));

            if (recentObjects.Count > max_recent_objects)
                recentObjects.RemoveAt(max_recent_objects);

            return strain;
        }

        /// <summary>
        /// Weighted count of sliders whose aim strain is close to the hardest slider's.
        /// </summary>
        public float DifficultSliderCount()
        {
            float maxSliderStrain = 0;

            foreach (float strain in sliderStrains)
                maxSliderStrain = MathF.Max(maxSliderStrain, strain);

            if (maxSliderStrain <= 0)
                return 0;

            float count = 0;

            foreach (float strain in sliderStrains)
                count += 1 / (1 + MathF.Exp(-(strain / maxSliderStrain * 12 - 6)));

            return count;
        }
    }
}
