using Vellum.AI.Fuzzy;

// Cosa "vede" il nemico in un istante: distanza dal bersaglio, vita propria
// (0..1) e quanti alleati ha vicino. Valori crisp dati in pasto al fuzzy.
public struct EnemyPerception
{
    public float distance;
    public float healthPct;
    public float allyCount;
}

// Cosa "decide" il fuzzy: aggression 0..1 = quanto vuole spingere e colpire.
// EnemyAI la usa per modulare la cadenza d'attacco (più alta → cooldown breve).
public struct EnemyDecision
{
    public float aggression;
}

// Controllore a Logica Difusa (Mamdani) dei nemici. È la tecnica di IA che
// modella le decisioni dei personaggi richiesta dalla consegna: regole
// linguistiche su distanza/vita/affollamento producono aggressività e voglia
// di ritirata. Il FuzzyController è condiviso e immutabile (costruito una volta);
// ogni nemico tiene solo i buffer di input/output → nessuna alloc per decisione.
public sealed class EnemyFuzzyBrain
{
    private const string DISTANCE = "distance";
    private const string HEALTH = "health";
    private const string CROWDING = "crowding";
    private const string AGGRESSION = "aggression";

    private static FuzzyController _shared;

    private readonly FuzzyController _controller;
    private readonly float[] _in;
    private readonly float[] _out;
    private readonly int _iDistance, _iHealth, _iCrowding, _oAggression;

    public EnemyFuzzyBrain()
    {
        _controller = GetShared();
        _in = new float[_controller.InputCount];
        _out = new float[_controller.OutputCount];

        _iDistance = _controller.InputIndex(DISTANCE);
        _iHealth = _controller.InputIndex(HEALTH);
        _iCrowding = _controller.InputIndex(CROWDING);
        _oAggression = _controller.OutputIndex(AGGRESSION);
    }

    public EnemyDecision Decide(in EnemyPerception p)
    {
        _in[_iDistance] = p.distance;
        _in[_iHealth] = p.healthPct;
        _in[_iCrowding] = p.allyCount;

        _controller.Evaluate(_in, _out);

        return new EnemyDecision
        {
            aggression = _out[_oAggression]
        };
    }

    // Costruzione del sistema fuzzy. Le soglie di distanza sono in metri: "Near"
    // ~ raggio d'attacco, "Far" = ancora da inseguire.
    private static FuzzyController GetShared()
    {
        if (_shared != null) return _shared;

        var distance = new FuzzyVariable(DISTANCE, 0f, 16f)
            .Set("Near", MembershipFunction.LeftShoulder(1.8f, 3.5f))
            .Set("Medium", MembershipFunction.Triangle(2.5f, 6f, 10f))
            .Set("Far", MembershipFunction.RightShoulder(8f, 12f));

        var health = new FuzzyVariable(HEALTH, 0f, 1f)
            .Set("Low", MembershipFunction.LeftShoulder(0.25f, 0.5f))
            .Set("Medium", MembershipFunction.Triangle(0.3f, 0.5f, 0.7f))
            .Set("High", MembershipFunction.RightShoulder(0.5f, 0.8f));

        var crowding = new FuzzyVariable(CROWDING, 0f, 5f)
            .Set("Low", MembershipFunction.LeftShoulder(0f, 2f))
            .Set("High", MembershipFunction.RightShoulder(1f, 3f));

        var aggression = new FuzzyVariable(AGGRESSION, 0f, 1f)
            .Set("Low", MembershipFunction.LeftShoulder(0.2f, 0.45f))
            .Set("Medium", MembershipFunction.Triangle(0.3f, 0.5f, 0.7f))
            .Set("High", MembershipFunction.RightShoulder(0.55f, 0.85f));

        _shared = new FuzzyController.Builder()
            .Samples(32)
            .Input(distance).Input(health).Input(crowding)
            .Output(aggression)
            // Vicino e in salute: assalto.
            .Rule().If(DISTANCE, "Near").And(HEALTH, "High").Then(AGGRESSION, "High")
            // Vicino ma ferito: attacca meno spesso (cooldown più lungo).
            .Rule().If(DISTANCE, "Near").And(HEALTH, "Low").Then(AGGRESSION, "Low")
            // Vicino, vita media: aggressività moderata.
            .Rule().If(DISTANCE, "Near").And(HEALTH, "Medium").Then(AGGRESSION, "Medium")
            // Distanza media / lontano: continua a inseguire.
            .Rule().If(DISTANCE, "Medium").Then(AGGRESSION, "Medium")
            .Rule().If(DISTANCE, "Far").Then(AGGRESSION, "Medium")
            // In gruppo e vicino: spinge di più (assalto coordinato).
            .Rule().If(CROWDING, "High").And(DISTANCE, "Near").Then(AGGRESSION, "High")
            .Build();

        return _shared;
    }
}
