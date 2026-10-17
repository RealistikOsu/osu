using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Relax2019.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax2019.Skills
{
    /// <summary>
    /// Harmonic reading skill. Object difficulties depend on following objects, so they are evaluated lazily;
    /// those whose look-ahead window is complete are cached for repeated (timed) evaluation.
    /// </summary>
    public class Relax2019Reading : Skill
    {
        private const float skill_multiplier = 2.5f;
        private const float strain_decay_base = 0.8f;
        private const float harmonic_scale = 1;
        private const float decay_exponent = 0.9f;

        public readonly Relax2019ReadingParameters Parameters;

        private readonly List<Relax2019ReadingObject> objects = new List<Relax2019ReadingObject>();
        private readonly List<float> finalDifficulties = new List<float>();
        private float finalStrain;

        public Relax2019Reading(Mod[] mods, Relax2019ReadingParameters parameters)
            : base(mods)
        {
            Parameters = parameters;
        }

        protected override double ProcessInternal(DifficultyHitObject current)
        {
            var obj = (Relax2019DifficultyHitObject)current;

            if (objects.Count == 0)
                objects.Add(Relax2019ReadingObject.First(obj.FirstObjectTime));

            objects.Add(Relax2019ReadingObject.From(obj));

            return 0;
        }

        public override double DifficultyValue() => Evaluate().Difficulty;

        public (float Difficulty, float DifficultNoteCount) Evaluate()
        {
            var objectDifficulties = computeObjectDifficulties();

            if (objectDifficulties.Count == 0)
                return (0, 0);

            var difficulties = objectDifficulties.FindAll(d => d > 0);

            if (difficulties.Count == 0)
                return (0, 0);

            // Memorisation ramp; the Rust calc spans it over every object of the map.
            int reducedNoteCount = objectDifficulties.Count;

            for (int i = 0; i < Math.Min(difficulties.Count, reducedNoteCount); i++)
            {
                float t = Math.Clamp(i / (float)reducedNoteCount, 0, 1);
                difficulties[i] *= MathF.Log10(1 + 9 * t);
            }

            difficulties.Sort((a, b) => b.CompareTo(a));

            float difficulty = 0;
            float noteWeightSum = 0;

            for (int i = 0; i < difficulties.Count; i++)
            {
                float index = i;
                float harmonicTerm = harmonic_scale / (1 + index);
                float weight = (1 + harmonicTerm) / (MathF.Pow(index, decay_exponent) + 1 + harmonicTerm);

                noteWeightSum += weight;
                difficulty += difficulties[i] * weight;
            }

            return (difficulty, countTopWeightedObjectDifficulties(objectDifficulties, difficulty, noteWeightSum));
        }

        private static float countTopWeightedObjectDifficulties(List<float> objectDifficulties, float difficulty, float noteWeightSum)
        {
            if (noteWeightSum == 0)
                return 0;

            float consistentTop = difficulty / noteWeightSum;

            if (consistentTop == 0)
                return 0;

            float count = 0;

            foreach (float d in objectDifficulties)
                count += 1.1f / (1 + MathF.Exp(5 * (1.15f - d / consistentTop)));

            return count;
        }

        private List<float> computeObjectDifficulties()
        {
            while (finalDifficulties.Count < objects.Count && isFinal(finalDifficulties.Count))
            {
                finalStrain = step(finalStrain, finalDifficulties.Count);
                finalDifficulties.Add(finalStrain);
            }

            var difficulties = new List<float>(finalDifficulties);
            float strain = finalStrain;

            for (int i = finalDifficulties.Count; i < objects.Count; i++)
            {
                strain = step(strain, i);
                difficulties.Add(strain);
            }

            return difficulties;
        }

        private float step(float strain, int index)
        {
            if (index > 0)
                strain *= MathF.Pow(strain_decay_base, objects[index].Delta / 1000);

            return strain + Relax2019ReadingEvaluator.EvaluateDifficultyOf(objects, index, Parameters) * skill_multiplier;
        }

        /// <summary>
        /// Whether an object's difficulty can no longer change: the next object exists and the last processed object is beyond its look-ahead window.
        /// </summary>
        private bool isFinal(int index)
        {
            if (index + 1 >= objects.Count)
                return false;

            float time = objects[index].Time;
            float lastTime = objects[^1].Time;

            return lastTime - time > Relax2019ReadingEvaluator.READING_WINDOW_SIZE || time + Parameters.TimePreemptRaw < lastTime;
        }
    }
}
