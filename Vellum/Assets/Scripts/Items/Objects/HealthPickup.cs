using System;
using UnityEngine;

// Pickup di salute. Spawnato dal WaveManager alla morte di un nemico con
// probabilità configurabile (vedi WaveManager.healthDropChance). Il prefab
// visivo (mesh/sprite + Collider trigger) è preparato in Editor; lo script
// è agnostico al look. Pool-friendly: SetActive(false) via callback al
// proprietario (niente Destroy, CLAUDE.md §4.3).
[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    // Chi viene curato alla raccolta. In entrambi i casi il pickup si raccoglie
    // camminandoci sopra col Player; cambia solo a chi va la cura.
    private enum HealTarget { Player, Jammo }

    [Header("Pickup")]
    [Tooltip("Player → cura chi lo raccoglie (il Player). Jammo → cura Jammo (kit di riparazione raccolto dal Player).")]
    [SerializeField] private HealTarget healTarget = HealTarget.Player;
    [SerializeField] private float healAmount = 50f;
    [Tooltip("Tag del Player. Il pickup si attiva solo al contatto con questo tag.")]
    [SerializeField] private string playerTag = "Player";

    private Action _onCollected;
    private bool _consumed;

    // Configurato dal WaveManager allo spawn dal pool: la callback rimette
    // il pickup nel pool quando viene raccolto. Resetta anche il flag _consumed
    // per il riuso.
    public void Configure(Action onCollected)
    {
        _onCollected = onCollected;
        _consumed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;
        if (!other.CompareTag(playerTag)) return;

        Health target = ResolveHealTarget(other);
        if (target == null) return;

        target.Heal(healAmount);
        _consumed = true;
        _onCollected?.Invoke();
    }

    private Health ResolveHealTarget(Collider collector)
    {
        if (healTarget == HealTarget.Player)
            return collector.GetComponentInParent<Health>();

        // Jammo: la cura va a Jammo (registrato nel coordinator dei nemici),
        // non al Player che fisicamente raccoglie il pickup.
        var coord = EnemyTargetCoordinator.Instance;
        return coord != null ? coord.JammoHealth : null;
    }
}
