using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Skills
{
    public class RelaxSpeed : RelaxStrainSkill
    {
        public RelaxSpeed(Mod[] mods)
            : base(mods)
        {
        }

        protected override float SkillMultiplier => 1400;

        protected override float StrainDecayBase => 0.3f;

        protected override float StrainValueOf(RelaxDifficultyHitObject current) => RelaxSpeedEvaluator.EvaluateDifficultyOf(current);
    }
}
