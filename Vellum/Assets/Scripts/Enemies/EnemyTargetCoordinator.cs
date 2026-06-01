using System.Collections.Generic;
using UnityEngine;

// Coordina a chi puntano i nemici dell'arena. Regola (richieste #1/#2):
// - bersaglio di default = Jammo (i nemici puntano a sabotare la statua);
// - un nemico passa al Player solo se "triggerato dal Player" (lo ha colpito) e
//   ci sono slot Player liberi (maxPlayerChasers). Così, appena uno ingaggia il
//   Player, gli altri della wave restano/vanno su Jammo.
// Singleton leggero: i nemici poolati si registrano via Instance senza wiring
// per-istanza. I riferimenti a Player/Jammo si assegnano in Inspector.
public class EnemyTargetCoordinator : MonoBehaviour
{
    public static EnemyTargetCoordinator Instance { get; private set; }

    [Header("Bersagli")]
    [SerializeField] private Transform player;
    [SerializeField] private Health playerHealth;
    [SerializeField] private Transform jammo;
    [SerializeField] private Health jammoHealth;

    [Header("Regole di ingaggio")]
    [Tooltip("Quanti nemici possono inseguire il Player contemporaneamente. Gli altri vanno su Jammo.")]
    [SerializeField] private int maxPlayerChasers = 1;

    // Liste ORDINATE: l'indice in lista è lo "slot" usato per l'accerchiamento a
    // ventaglio (vedi EnemyAI.ComputeApproachPoint).
    private readonly List<EnemyAI> _playerChasers = new List<EnemyAI>();
    private readonly List<EnemyAI> _jammoChasers = new List<EnemyAI>();

    public Transform Player => player;
    public Health PlayerHealth => playerHealth;
    public Transform Jammo => jammo;
    public Health JammoHealth => jammoHealth;

    public int PlayerChaserCount => _playerChasers.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EnemyTargetCoordinator] Istanza duplicata: ne tengo una sola.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (player != null && playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
        if (jammo != null && jammoHealth == null) jammoHealth = jammo.GetComponentInParent<Health>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool JammoAlive => jammo != null && (jammoHealth == null || !jammoHealth.IsDead);
    public bool PlayerAlive => player != null && (playerHealth == null || !playerHealth.IsDead);

    // Prova a riservare uno slot Player per questo nemico. True se concesso (o se
    // lo possedeva già). False → il nemico deve puntare Jammo.
    public bool TryClaimPlayer(EnemyAI enemy)
    {
        if (enemy == null || !PlayerAlive) return false;
        if (_playerChasers.Contains(enemy)) return true;
        if (_playerChasers.Count >= maxPlayerChasers) return false;

        _jammoChasers.Remove(enemy);
        _playerChasers.Add(enemy);
        return true;
    }

    public void ReleasePlayer(EnemyAI enemy)
    {
        if (enemy != null) _playerChasers.Remove(enemy);
    }

    // Il nemico punta Jammo: entra nella lista jammo (esce da quella player).
    public void RegisterJammo(EnemyAI enemy)
    {
        if (enemy == null) return;
        _playerChasers.Remove(enemy);
        if (!_jammoChasers.Contains(enemy)) _jammoChasers.Add(enemy);
    }

    // Idle / morte / disable: fuori da entrambe le liste.
    public void Unregister(EnemyAI enemy)
    {
        if (enemy == null) return;
        _playerChasers.Remove(enemy);
        _jammoChasers.Remove(enemy);
    }

    public bool IsChasingPlayer(EnemyAI enemy) => enemy != null && _playerChasers.Contains(enemy);

    // Slot (indice in lista) e numero di attaccanti dello stesso bersaglio:
    // usati per disporre i nemici a ventaglio attorno al bersaglio.
    public int SlotIndex(EnemyAI enemy, bool isPlayer)
        => (isPlayer ? _playerChasers : _jammoChasers).IndexOf(enemy);

    public int AttackerCount(bool isPlayer)
        => (isPlayer ? _playerChasers : _jammoChasers).Count;
}
