using UnityEngine;

// Va sul Player, accanto a Health. Annulla il danno se lo scudo è alzato
// (PlayerCombat.IsDefending) e l'attaccante è entro l'angolo frontale.
[RequireComponent(typeof(Health))]
public class FrontalShieldBlock : MonoBehaviour, IDamageFilter
{
    [Tooltip("Ampiezza totale del cono frontale di blocco, in gradi.")]
    [SerializeField] private float blockAngle = 120f;

    private PlayerCombat _combat;

    void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        if (_combat == null) _combat = GetComponentInParent<PlayerCombat>();
    }

    public bool ShouldBlock(DamageInfo info)
    {
        if (_combat == null || !_combat.IsDefending) return false;

        Vector3 toSource = info.sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f) return true; // sopra il player: consideralo frontale

        return Vector3.Angle(transform.forward, toSource.normalized) <= blockAngle * 0.5f;
    }
}
