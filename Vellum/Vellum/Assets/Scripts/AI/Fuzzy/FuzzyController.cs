using System.Collections.Generic;
using UnityEngine;

namespace Vellum.AI.Fuzzy
{
    // Motore di inferenza fuzzy Mamdani:
    //   1) fuzzifica gli input e calcola la forza di ogni regola (AND = min);
    //   2) per ogni variabile di output, aggrega i set conseguenti tagliati alla
    //      forza della regola (aggregazione = max);
    //   3) defuzzifica col centroide campionato sul dominio dell'output.
    // Costruito una volta (vedi Builder), poi Evaluate() gira senza allocazioni:
    // input/output passati come float[] allineati all'ordine di dichiarazione, e
    // il buffer delle forze è preallocato.
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

        // crispInputs allineato all'ordine degli input; crispOutputs riceve i
        // valori defuzzificati allineato all'ordine degli output.
        public void Evaluate(float[] crispInputs, float[] crispOutputs)
        {
            // 1) forza di ogni regola = min dei gradi di appartenenza degli antecedenti.
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

            // 2)+3) per ogni output, centroide dell'aggregato (max dei set tagliati).
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

        // ---- Builder leggibile (etichette risolte in indici a build-time) ----

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
                Debug.LogError($"[FuzzyController] Variabile '{name}' non dichiarata.");
                return 0;
            }

            // Bozza fluente di una regola: If/And accumulano antecedenti, Then fissa
            // il conseguente. Risolta in FuzzyRule (indici) da Resolve().
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

                // Termina la regola e torna al Builder per concatenare la prossima.
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
                            Debug.LogError($"[FuzzyController] Set '{_antSetLabels[i]}' assente in input '{_antVarNames[i]}'.");
                    }
                    int outVar = VarIndex(outputs, _outVarName);
                    int outSet = outputs[outVar].IndexOf(_outSetLabel);
                    if (outSet < 0)
                        Debug.LogError($"[FuzzyController] Set '{_outSetLabel}' assente in output '{_outVarName}'.");
                    return new FuzzyRule(antVar, antSet, outVar, Mathf.Max(0, outSet));
                }
            }
        }
    }
}
