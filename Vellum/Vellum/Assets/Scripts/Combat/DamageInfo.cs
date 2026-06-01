using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public Vector3 sourcePosition; // serve al blocco frontale dello scudo e al knockback
    public GameObject source;      // chi ha inflitto il danno

    public DamageInfo(float amount, Vector3 sourcePosition, GameObject source)
    {
        this.amount = amount;
        this.sourcePosition = sourcePosition;
        this.source = source;
    }
}
