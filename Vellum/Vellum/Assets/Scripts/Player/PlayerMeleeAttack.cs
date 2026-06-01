using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sta sulla root del Player. PlayerCombat chiama BeginSwing() al click sinistro.
// Rilevamento colpi guidato dall'input (NON da Animation Event): indipendente
// da clip/Animator e identico per mesh maschile e femminile.
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Danno")]
    [SerializeField] private float attackDamage = 15f;

    [Header("Tempi swing (secondi)")]
    [Tooltip("Ritardo dal click prima che la hitbox diventi attiva (carica del colpo).")]
    [SerializeField] private float windupTime = 0.15f;
    [Tooltip("Durata in cui la hitbox resta attiva e può colpire.")]
    [SerializeField] private float activeTime = 0.15f;

    [Header("Hitbox (relativa al Player)")]
    [SerializeField] private Vector3 hitboxHalfExtents = new Vector3(0.6f, 0.7f, 0.8f);
    [SerializeField] private float forwardOffset = 1.0f;
    [SerializeField] private float verticalOffset = 1.0f;

    [Tooltip("Imposta sui layer dei nemici/statua. Lascia tutto se non usi layer dedicati.")]
    [SerializeField] private LayerMask hittableLayers = ~0;

    private readonly Collider[] _hits = new Collider[16];
    private readonly List<IDamageable> _damagedThisSwing = new List<IDamageable>(8);
    private Coroutine _swing;

    // True durante windup+active di uno swing. Letto dal Boss (BossDuelAI) per la
    // difesa reattiva: vede partire il colpo e può alzare la guardia in anticipo.
    public bool IsSwinging { get; private set; }

    // Chiamato da PlayerCombat al click sinistro. damageOverride > 0 lo usa al
    // posto del default (utile in futuro per i colpi della combo).
    public void BeginSwing(float damageOverride = 0f)
    {
        if (_swing != null) StopCoroutine(_swing);
        _swing = StartCoroutine(SwingRoutine(damageOverride > 0f ? damageOverride : attackDamage));
    }

    // Annulla lo swing in corso (es. quando il Player alza lo scudo): evita che
    // il colpo "in canna", il cui danno è su timer indipendente dall'animazione,
    // parta comunque dopo la difesa.
    public void CancelSwing()
    {
        if (_swing != null) { StopCoroutine(_swing); _swing = null; }
        IsSwinging = false;
    }

    private IEnumerator SwingRoutine(float dmg)
    {
        _damagedThisSwing.Clear();
        IsSwinging = true;

        if (windupTime > 0f) yield return new WaitForSeconds(windupTime);

        float elapsed = 0f;
        while (elapsed < activeTime)
        {
            ApplyHit(dmg);
            elapsed += Time.deltaTime;
            yield return null;
        }

        IsSwinging = false;
        _swing = null;
    }

    private void ApplyHit(float dmg)
    {
        Vector3 center = transform.position
                         + Vector3.up * verticalOffset
                         + transform.forward * forwardOffset;

        int count = Physics.OverlapBoxNonAlloc(
            center, hitboxHalfExtents, _hits, transform.rotation,
            hittableLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue; // mai se stesso

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            if (_damagedThisSwing.Contains(target)) continue; // un solo colpo per bersaglio per swing

            _damagedThisSwing.Add(target);
            target.TakeDamage(new DamageInfo(dmg, transform.position, gameObject));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position
                         + Vector3.up * verticalOffset
                         + transform.forward * forwardOffset;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, hitboxHalfExtents * 2f);
    }
}
