using System.Collections.Generic;

namespace Vellum.AI.Fuzzy
{
    /// <summary>
    /// Fuzzy linguistic variable: a name, a domain [Min,Max] and a set of labelled
    /// fuzzy sets (e.g. "Near", "Medium", "Far"). Built once (e.g. in EnemyFuzzyBrain);
    /// at runtime it is queried by index → no string lookups inside the evaluation loop.
    /// </summary>
    public sealed class FuzzyVariable
    {
        public string Name { get; }
        public float Min { get; }
        public float Max { get; }

        private readonly List<string> _labels = new List<string>();
        private readonly List<MembershipFunction> _funcs = new List<MembershipFunction>();

        public FuzzyVariable(string name, float min, float max)
        {
            Name = name;
            Min = min;
            Max = max;
        }

        /// <summary>Fluent: adds a labelled fuzzy set. Used only during construction.</summary>
        public FuzzyVariable Set(string label, MembershipFunction func)
        {
            _labels.Add(label);
            _funcs.Add(func);
            return this;
        }

        public int SetCount => _funcs.Count;

        /// <summary>Index of the labelled set, or -1 if missing (resolved at build-time by the controller).</summary>
        public int IndexOf(string label) => _labels.IndexOf(label);

        /// <summary>Membership degree of the crisp value <paramref name="x"/> in the set at <paramref name="setIndex"/>.</summary>
        public float Membership(int setIndex, float x) => _funcs[setIndex].Evaluate(x);
    }
}
