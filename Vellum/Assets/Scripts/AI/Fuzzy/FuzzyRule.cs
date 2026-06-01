namespace Vellum.AI.Fuzzy
{
    /// <summary>
    /// Fuzzy rule resolved into indices (no strings at runtime):
    ///   IF input[AntecedentVar[i]] IS AntecedentSet[i]  (AND = min over all antecedents)
    ///   THEN output[OutputVar] IS OutputSet.
    /// Immutable; the arrays are built once by the controller's builder.
    /// </summary>
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
