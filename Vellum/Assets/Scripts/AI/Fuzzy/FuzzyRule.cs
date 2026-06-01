namespace Vellum.AI.Fuzzy
{
    // Regola fuzzy risolta in indici (no stringhe a runtime):
    //   IF input[antVar[i]] È antSet[i]  (AND = min su tutti gli antecedenti)
    //   THEN output[OutputVar] È OutputSet.
    // Immutabile; gli array vengono costruiti una sola volta dal builder.
    public readonly struct FuzzyRule
    {
        public readonly int[] AntecedentVar;
        public readonly int[] AntecedentSet;
        public readonly int OutputVar;
        public readonly int OutputSet;

        public FuzzyRule(int[] antecedentVar, int[] antecedentSet, int outputVar, int outputSet)
        {
            AntecedentVar = antecedentVar;
            AntecedentSet = antecedentSet;
            OutputVar = outputVar;
            OutputSet = outputSet;
        }
    }
}
