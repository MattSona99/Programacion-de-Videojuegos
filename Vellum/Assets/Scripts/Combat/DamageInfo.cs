using UnityEngine;

/// <summary>
/// Lightweight payload describing a single instance of damage as it travels
/// through the combat pipeline (IDamageable / IDamageFilter / IDamageReaction).
/// </summary>
public struct DamageInfo
{
    public float amount;
    public Vector3 sourcePosition; // used by the frontal shield block and by knockback
    public GameObject source;      // who dealt the damage

    public DamageInfo(float amount, Vector3 sourcePosition, GameObject source)
    {
        this.amount = amount;
        this.sourcePosition = sourcePosition;
        this.source = source;
    }
}
