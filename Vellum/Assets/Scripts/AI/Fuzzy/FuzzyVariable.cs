using System.Collections.Generic;

namespace Vellum.AI.Fuzzy
{
    // Variabile linguistica fuzzy: un nome, un dominio [Min,Max] e un insieme di
    // set etichettati (es. "Near", "Medium", "Far"). Costruita una volta (in
    // EnemyFuzzyBrain); a runtime si interroga per indice → niente lookup per
    // stringa nel ciclo di valutazione.
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

        // Fluente: aggiunge un set etichettato. Usata solo in fase di costruzione.
        public FuzzyVariable Set(string label, MembershipFunction func)
        {
            _labels.Add(label);
            _funcs.Add(func);
            return this;
        }

        public int SetCount => _funcs.Count;

        // -1 se l'etichetta non esiste (risolta a build-time dal controller).
        public int IndexOf(string label) => _labels.IndexOf(label);

        // Grado di appartenenza del valore crisp x al set di indice setIndex.
        public float Membership(int setIndex, float x) => _funcs[setIndex].Evaluate(x);
    }
}
