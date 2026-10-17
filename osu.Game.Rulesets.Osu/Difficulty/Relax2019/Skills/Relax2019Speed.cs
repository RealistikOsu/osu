using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019.Skills
{
    public class Relax2019Speed : Relax2019StrainSkill
    {
        public Relax2019Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override float SkillMultiplier => 1400;

        protected override float StrainDecayBase => 0.3f;

        protected override float StrainValueOf(Relax2019DifficultyHitObject current) => Relax2019SpeedEvaluator.EvaluateDifficultyOf(current);
    }
}
