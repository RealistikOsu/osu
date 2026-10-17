using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.Skills
{
    /// <summary>
    /// Relax strain skill: 400ms sections aligned to the first object, strain starting at 1,
    /// backload spike penalty and (optionally) isolated spike penalty on the section peaks.
    /// Penalties are applied to copies so the skill can be evaluated repeatedly (timed calculation).
    /// </summary>
    public abstract class RelaxStrainSkill : Skill
    {
        private const float section_length = 400;
        private const float decay_weight = 0.9f;

        private const float backload_section_share = 0.85f;
        private const float backload_spike_ratio = 1.35f;
        private const float backload_spike_keep = 0.62f;

        private const float isolated_spike_baseline = 0.10f;
        private const float isolated_spike_ratio = 2.4f;
        private const float isolated_spike_keep = 0.35f;

        private readonly List<float> strainPeaks = new List<float>();
        private readonly List<float> objectStrains = new List<float>();

        private float currentStrain = 1;
        private float currentSectionPeak = 1;
        private float currentSectionEnd;
        private float previousTime;

        protected RelaxStrainSkill(Mod[] mods)
            : base(mods)
        {
        }

        protected abstract float SkillMultiplier { get; }

        protected abstract float StrainDecayBase { get; }

        protected virtual bool PenaliseIsolatedSpikes => false;

        protected abstract float StrainValueOf(RelaxDifficultyHitObject current);

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            var obj = (RelaxDifficultyHitObject)current;
            float length = section_length * obj.Rate;

            if (objectStrains.Count == 0)
            {
                currentSectionEnd = MathF.Ceiling(obj.FirstObjectTime / length) * length;

                while (obj.Time > currentSectionEnd)
                    currentSectionEnd += length;
            }
            else
            {
                while (obj.Time > currentSectionEnd)
                {
                    strainPeaks.Add(currentSectionPeak);
                    currentSectionPeak = currentStrain * strainDecay(currentSectionEnd - previousTime);
                    currentSectionEnd += length;
                }
            }

            currentStrain *= strainDecay(obj.Delta);
            currentStrain += StrainValueOf(obj) * SkillMultiplier;

            objectStrains.Add(currentStrain);

            currentSectionPeak = MathF.Max(currentSectionPeak, currentStrain);
            previousTime = obj.Time;

            return currentStrain;
        }

        public override double DifficultyValue() => Difficulty();

        public float Difficulty()
        {
            if (objectStrains.Count == 0)
                return 0;

            return weightedSum(penalise(peaks()));
        }

        /// <summary>
        /// Weighted count of objects close to the hardest strain.
        /// </summary>
        public float CountDifficultStrains()
        {
            if (objectStrains.Count == 0)
                return 0;

            float singleStrain = Difficulty() / 10;
            float count = 0;

            foreach (float strain in objectStrains)
                count += 1.1f / (1 + MathF.Exp(-10 * (strain / singleStrain - 0.88f)));

            return count;
        }

        private List<float> peaks() => new List<float>(strainPeaks) { currentSectionPeak };

        private List<float> penalise(List<float> input)
        {
            var peaks = new List<float>(input);
            int sectionCount = peaks.Count;
            int earlySectionCount = (int)MathF.Floor(sectionCount * backload_section_share);

            if (sectionCount > 5 && earlySectionCount > 0)
            {
                float earlyMax = 0;

                for (int i = 0; i < earlySectionCount; i++)
                    earlyMax = MathF.Max(earlyMax, peaks[i]);

                float threshold = MathF.Max(earlyMax * backload_spike_ratio, 1);

                for (int i = earlySectionCount; i < sectionCount; i++)
                {
                    if (peaks[i] > threshold)
                        peaks[i] = threshold + (peaks[i] - threshold) * backload_spike_keep;
                }
            }

            peaks.Sort((a, b) => b.CompareTo(a));

            if (PenaliseIsolatedSpikes && sectionCount > 5)
            {
                float baseline = peaks[(int)(sectionCount * isolated_spike_baseline)];
                float threshold = MathF.Max(baseline * isolated_spike_ratio, 1);

                for (int i = 0; i < sectionCount && peaks[i] > threshold; i++)
                    peaks[i] = threshold + (peaks[i] - threshold) * isolated_spike_keep;
            }

            return peaks;
        }

        private static float weightedSum(List<float> sortedPeaks)
        {
            float difficulty = 0;
            float weight = 1;

            foreach (float strain in sortedPeaks)
            {
                difficulty += strain * weight;
                weight *= decay_weight;
            }

            return difficulty;
        }

        private float strainDecay(float ms) => MathF.Pow(StrainDecayBase, ms / 1000);
    }
}
