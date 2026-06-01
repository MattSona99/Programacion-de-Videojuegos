using System.Collections.Generic;
using UnityEngine;

namespace Vellum.AI.Fuzzy
{
    /// <summary>
    /// Mamdani fuzzy inference engine:
    ///   1) fuzzifies the inputs and computes each rule's strength (AND = min);
    ///   2) for every output variable, aggregates the consequent sets clipped to the
    ///      rule strength (aggregation = max);
    ///   3) defuzzifies with a centroid sampled over the output domain.
    /// Built once (see <see cref="Builder"/>), then <see cref="Evaluate"/> runs
    /// allocation-free: inputs/outputs are passed as float[] aligned to declaration
    /// order, and the rule-strength buffer is preallocated.
    /// </summary>
    public sealed class FuzzyController
    {
        private readonly FuzzyVariable[] _inputs;
        private readonly FuzzyVariable[] _outputs;
        private readonly FuzzyRule[] _rules;
        private readonly int _samples;
        private readonly float[] _strengths;

        public int InputCount => _inputs.Length;
        public int OutputCount => _outputs.Length;

        private FuzzyController(FuzzyVariable[] inputs, FuzzyVariable[] outputs, FuzzyRule[] rules, int samples)
        {
            _inputs = inputs;
            _outputs = outputs;
            _rules = rules;
            _samples = Mathf.Max(2, samples);
            _strengths = new float[rules.Length];
        }

        public int InputIndex(string name)
        {
            for (int i = 0; i < _inputs.Length; i++) if (_inputs[i].Name == name) return i;
            return -1;
        }

        public int OutputIndex(string name)
        {
            for (int i = 0; i < _outputs.Length; i++) if (_outputs[i].Name == name) return i;
            return -1;
        }

        /// <summary>
        /// Runs one inference pass. <paramref name="crispInputs"/> is aligned to the input
        /// order; <paramref name="crispOutputs"/> receives the defuzzified values aligned
        /// to the output order. Allocation-free.
        /// </summary>
        public void Evaluate(float[] crispInputs, float[] crispOutputs)
        {
            // 1) each rule's strength = min of the antecedents' membership degrees.
            for (int r = 0; r < _rules.Length; r++)
            {
                FuzzyRule rule = _rules[r];
                float strength = 1f;
                for (int a = 0; a < rule.AntecedentVar.Length; a++)
                {
                    int v = rule.AntecedentVar[a];
                    float m = _inputs[v].Membership(rule.AntecedentSet[a], crispInputs[v]);
                    if (m < strength) strength = m;
                }
                _strengths[r] = strength;
            }

            // 2)+3) for each output, centroid of the aggregate (max of the clipped sets).
            for (int o = 0; o < _outputs.Length; o++)
            {
                FuzzyVariable outVar = _outputs[o];
                float min = outVar.Min, max = outVar.Max;
                float step = (max - min) / (_samples - 1);

                float num = 0f, den = 0f;
                for (int k = 0; k < _samples; k++)
                {
                    float x = min + step * k;
                    float agg = 0f;
                    for (int r = 0; r < _rules.Length; r++)
                    {
                        if (_rules[r].OutputVar != o || _strengths[r] <= 0f) continue;
                        float clipped = Mathf.Min(_strengths[r], outVar.Membership(_rules[r].OutputSet, x));
                        if (clipped > agg) agg = clipped;
                    }
                    num += x * agg;
                    den += agg;
                }

                crispOutputs[o] = den > 1e-6f ? num / den : (min + max) * 0.5f;
            }
        }

        // ---- Readable builder (labels resolved into indices at build-time) ----

        /// <summary>Fluent builder for a <see cref="FuzzyController"/>: declare inputs/outputs and rules, then Build().</summary>
        public sealed class Builder
        {
            private readonly List<FuzzyVariable> _inputs = new List<FuzzyVariable>();
            private readonly List<FuzzyVariable> _outputs = new List<FuzzyVariable>();
            private readonly List<RuleDraft> _rules = new List<RuleDraft>();
            private int _samples = 32;

            public Builder Samples(int n) { _samples = n; return this; }

            public Builder Input(FuzzyVariable v) { _inputs.Add(v); return this; }
            public Builder Output(FuzzyVariable v) { _outputs.Add(v); return this; }

            public RuleDraft Rule()
            {
                var draft = new RuleDraft(this);
                _rules.Add(draft);
                return draft;
            }

            public FuzzyController Build()
            {
                var inputs = _inputs.ToArray();
                var outputs = _outputs.ToArray();
                var rules = new FuzzyRule[_rules.Count];
                for (int i = 0; i < _rules.Count; i++) rules[i] = _rules[i].Resolve(inputs, outputs);
                return new FuzzyController(inputs, outputs, rules, _samples);
            }

            private static int VarIndex(FuzzyVariable[] vars, string name)
            {
                for (int i = 0; i < vars.Length; i++) if (vars[i].Name == name) return i;
                Debug.LogError($"[FuzzyController] Variable '{name}' is not declared.");
                return 0;
            }

            /// <summary>
            /// Fluent draft of a rule: If/And accumulate antecedents, Then fixes the
            /// consequent. Resolved into a <see cref="FuzzyRule"/> (indices) by Resolve().
            /// </summary>
            public sealed class RuleDraft
            {
                private readonly Builder _owner;
                private readonly List<string> _antVarNames = new List<string>();
                private readonly List<string> _antSetLabels = new List<string>();
                private string _outVarName;
                private string _outSetLabel;

                internal RuleDraft(Builder owner) { _owner = owner; }

                public RuleDraft If(string variable, string set)
                {
                    _antVarNames.Add(variable);
                    _antSetLabels.Add(set);
                    return this;
                }

                public RuleDraft And(string variable, string set) => If(variable, set);

                // Ends the rule and returns to the Builder to chain the next one.
                public Builder Then(string variable, string set)
                {
                    _outVarName = variable;
                    _outSetLabel = set;
                    return _owner;
                }

                internal FuzzyRule Resolve(FuzzyVariable[] inputs, FuzzyVariable[] outputs)
                {
                    var antVar = new int[_antVarNames.Count];
                    var antSet = new int[_antVarNames.Count];
                    for (int i = 0; i < _antVarNames.Count; i++)
                    {
                        antVar[i] = VarIndex(inputs, _antVarNames[i]);
                        antSet[i] = inputs[antVar[i]].IndexOf(_antSetLabels[i]);
                        if (antSet[i] < 0)
                            Debug.LogError($"[FuzzyController] Set '{_antSetLabels[i]}' missing in input '{_antVarNames[i]}'.");
                    }
                    int outVar = VarIndex(outputs, _outVarName);
                    int outSet = outputs[outVar].IndexOf(_outSetLabel);
                    if (outSet < 0)
                        Debug.LogError($"[FuzzyController] Set '{_outSetLabel}' missing in output '{_outVarName}'.");
                    return new FuzzyRule(antVar, antSet, outVar, Mathf.Max(0, outSet));
                }
            }
        }
    }
}
