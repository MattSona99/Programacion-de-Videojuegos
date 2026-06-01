using Vellum.AI.Fuzzy;

/// <summary>
/// Fuzzy logic for the final-level boss (the required AI technique). Two simple decisions used in
/// Phase 2 (Moon world): how much the boss wants to break off from the Player to INTERCEPT Jammo
/// while he carries a piece (inputs: distance to Jammo + whether carrying; output 'intercept'
/// 0..1), and how much it wants to go grab a health pickup when wounded (output 'seekHealth').
///
/// This is a DEDICATED controller (not the arena's EnemyFuzzyBrain, which is a shared singleton
/// with different inputs); keeping them separate avoids altering each other's behavior.
/// </summary>
public sealed class BossFuzzyBrain
{
    private const string JAMMO_DISTANCE = "jammoDistance";
    private const string JAMMO_CARRYING = "jammoCarrying";
    private const string INTERCEPT = "intercept";

    private const string SELF_HEALTH = "selfHealth";
    private const string PICKUP_DISTANCE = "pickupDistance";
    private const string SEEK_HEALTH = "seekHealth";

    private static FuzzyController _sharedIntercept;
    private static FuzzyController _sharedSeekHealth;

    private readonly FuzzyController _intercept;
    private readonly float[] _inI, _outI;
    private readonly int _iDistance, _iCarrying, _oIntercept;

    private readonly FuzzyController _seek;
    private readonly float[] _inS, _outS;
    private readonly int _iSelfHealth, _iPickupDistance, _oSeekHealth;

    public BossFuzzyBrain()
    {
        _intercept = GetInterceptController();
        _inI = new float[_intercept.InputCount];
        _outI = new float[_intercept.OutputCount];
        _iDistance = _intercept.InputIndex(JAMMO_DISTANCE);
        _iCarrying = _intercept.InputIndex(JAMMO_CARRYING);
        _oIntercept = _intercept.OutputIndex(INTERCEPT);

        _seek = GetSeekHealthController();
        _inS = new float[_seek.InputCount];
        _outS = new float[_seek.OutputCount];
        _iSelfHealth = _seek.InputIndex(SELF_HEALTH);
        _iPickupDistance = _seek.InputIndex(PICKUP_DISTANCE);
        _oSeekHealth = _seek.OutputIndex(SEEK_HEALTH);
    }

    /// <summary>Desire to intercept Jammo (0..1). <paramref name="carrying"/> passed as 0/1.</summary>
    public float Intercept(float jammoDistance, bool carrying)
    {
        _inI[_iDistance] = jammoDistance;
        _inI[_iCarrying] = carrying ? 1f : 0f;
        _intercept.Evaluate(_inI, _outI);
        return _outI[_oIntercept];
    }

    /// <summary>
    /// Desire to go grab a health pickup (0..1): high only when wounded (low selfHealth) AND the
    /// pickup is near. When healthy it ignores it (the Player takes it freely); when wounded it
    /// won't cross the arena for a far one.
    /// </summary>
    public float SeekHealth(float selfHealthNormalized, float pickupDistance)
    {
        _inS[_iSelfHealth] = selfHealthNormalized;
        _inS[_iPickupDistance] = pickupDistance;
        _seek.Evaluate(_inS, _outS);
        return _outS[_oSeekHealth];
    }

    private static FuzzyController GetInterceptController()
    {
        if (_sharedIntercept != null) return _sharedIntercept;

        var distance = new FuzzyVariable(JAMMO_DISTANCE, 0f, 16f)
            .Set("Near", MembershipFunction.LeftShoulder(4f, 8f))
            .Set("Far", MembershipFunction.RightShoulder(7f, 12f));

        var carrying = new FuzzyVariable(JAMMO_CARRYING, 0f, 1f)
            .Set("No", MembershipFunction.LeftShoulder(0.25f, 0.5f))
            .Set("Yes", MembershipFunction.RightShoulder(0.5f, 0.75f));

        var intercept = new FuzzyVariable(INTERCEPT, 0f, 1f)
            .Set("Low", MembershipFunction.LeftShoulder(0.2f, 0.45f))
            .Set("High", MembershipFunction.RightShoulder(0.55f, 0.85f));

        _sharedIntercept = new FuzzyController.Builder()
            .Samples(32)
            .Input(distance).Input(carrying)
            .Output(intercept)
            // Jammo in reach and carrying a piece: go stop him.
            .Rule().If(JAMMO_DISTANCE, "Near").And(JAMMO_CARRYING, "Yes").Then(INTERCEPT, "High")
            // Carrying but far: not worth leaving the Player.
            .Rule().If(JAMMO_DISTANCE, "Far").Then(INTERCEPT, "Low")
            // Empty-handed: ignore him, stay on the Player.
            .Rule().If(JAMMO_CARRYING, "No").Then(INTERCEPT, "Low")
            .Build();

        return _sharedIntercept;
    }

    private static FuzzyController GetSeekHealthController()
    {
        if (_sharedSeekHealth != null) return _sharedSeekHealth;

        var selfHealth = new FuzzyVariable(SELF_HEALTH, 0f, 1f)
            .Set("Low", MembershipFunction.LeftShoulder(0.3f, 0.55f))
            .Set("High", MembershipFunction.RightShoulder(0.45f, 0.7f));

        var pickupDistance = new FuzzyVariable(PICKUP_DISTANCE, 0f, 16f)
            .Set("Near", MembershipFunction.LeftShoulder(3f, 7f))
            .Set("Far", MembershipFunction.RightShoulder(6f, 11f));

        var seek = new FuzzyVariable(SEEK_HEALTH, 0f, 1f)
            .Set("Low", MembershipFunction.LeftShoulder(0.2f, 0.45f))
            .Set("High", MembershipFunction.RightShoulder(0.55f, 0.85f));

        _sharedSeekHealth = new FuzzyController.Builder()
            .Samples(32)
            .Input(selfHealth).Input(pickupDistance)
            .Output(seek)
            // Wounded and with a pickup nearby: go heal.
            .Rule().If(SELF_HEALTH, "Low").And(PICKUP_DISTANCE, "Near").Then(SEEK_HEALTH, "High")
            // Healthy: don't waste time on the pickup, stay in the fight.
            .Rule().If(SELF_HEALTH, "High").Then(SEEK_HEALTH, "Low")
            // Pickup far: don't cross the arena for it.
            .Rule().If(PICKUP_DISTANCE, "Far").Then(SEEK_HEALTH, "Low")
            .Build();

        return _sharedSeekHealth;
    }
}
