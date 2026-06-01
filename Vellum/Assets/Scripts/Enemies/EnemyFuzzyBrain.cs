using Vellum.AI.Fuzzy;

/// <summary>
/// What the enemy "sees" at an instant: distance to the target, its own health (0..1)
/// and how many allies are nearby. Crisp values fed into the fuzzy controller.
/// </summary>
public struct EnemyPerception
{
    public float distance;
    public float healthPct;
    public float allyCount;
}

/// <summary>
/// What the fuzzy controller "decides": aggression 0..1 = how much it wants to push and hit.
/// EnemyAI uses it to modulate the attack rate (higher → shorter cooldown).
/// </summary>
public struct EnemyDecision
{
    public float aggression;
}

/// <summary>
/// Mamdani fuzzy-logic controller for the enemies. This is the AI technique that models the
/// characters' decisions: linguistic rules over distance/health/crowding produce aggression.
/// The FuzzyController is shared and immutable (built once); each enemy only keeps its
/// input/output buffers → no allocation per decision.
/// </summary>
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

    /// <summary>Runs one fuzzy inference pass from the perception and returns the decided aggression.</summary>
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

    /// <summary>
    /// Builds the shared fuzzy system. Distance thresholds are in metres: "Near" ~ attack
    /// range, "Far" = still to be chased.
    /// </summary>
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
            // Close and healthy: assault.
            .Rule().If(DISTANCE, "Near").And(HEALTH, "High").Then(AGGRESSION, "High")
            // Close but wounded: attacks less often (longer cooldown).
            .Rule().If(DISTANCE, "Near").And(HEALTH, "Low").Then(AGGRESSION, "Low")
            // Close, medium health: moderate aggression.
            .Rule().If(DISTANCE, "Near").And(HEALTH, "Medium").Then(AGGRESSION, "Medium")
            // Medium distance / far: keep chasing.
            .Rule().If(DISTANCE, "Medium").Then(AGGRESSION, "Medium")
            .Rule().If(DISTANCE, "Far").Then(AGGRESSION, "Medium")
            // Grouped and close: pushes harder (coordinated assault).
            .Rule().If(CROWDING, "High").And(DISTANCE, "Near").Then(AGGRESSION, "High")
            .Build();

        return _shared;
    }
}
