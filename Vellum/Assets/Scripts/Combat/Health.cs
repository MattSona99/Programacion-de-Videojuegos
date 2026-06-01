using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>UnityEvent carrying a normalized health value (0..1), for HUD wiring.</summary>
[System.Serializable]
public class HealthChangedEvent : UnityEvent<float> { }

/// <summary>
/// Shared health component for every combat actor (Player, enemies, Jammo, boss, statue).
/// Implements <see cref="IDamageable"/> and runs the damage pipeline: pre-damage
/// <see cref="IDamageFilter"/> vetoes, damage application (with optional floor/cap),
/// then <see cref="IDamageReaction"/> callbacks and damage/death events.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Invulnerability window after a hit. Player ~0.5, enemies ~0.1, statue 0.")]
    [SerializeField] private float invulnerabilityDuration = 0f;

    [Header("Events")]
    [Tooltip("Invoked on every hit taken. Passes the normalized health (0..1) for the HUD.")]
    [SerializeField] private HealthChangedEvent onDamaged;

    [Tooltip("Invoked exactly once when health reaches 0.")]
    [SerializeField] private UnityEvent onDied;

    /// <summary>
    /// C# event (in addition to the <see cref="onDied"/> UnityEvent) for runtime hooks:
    /// WaveManager, pooling, arena restart. Subscribed once and survives reuse.
    /// </summary>
    public event Action Died;

    /// <summary>
    /// Damage-ONLY C# event (does not fire on Heal, unlike onDamaged which doubles as a
    /// "health changed" signal for the HUD). Used by the final duel to spawn health
    /// pickups on every landed hit without triggering a loop with Heal.
    /// </summary>
    public event Action<DamageInfo> Damaged;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float MaxHealth => maxHealth;
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    private float _lastHitTime = float.NegativeInfinity;
    private IDamageFilter[] _filters;
    private IDamageReaction[] _reactions;

    // Runtime regulators (disabled by default → no impact on Player/enemies/statue).
    // Used by the final duel: floor = minimum health per phase (Boss never drops
    // below 50% in Phase 1); cap = maximum damage per hit (1 while Jammo carries pieces).
    private float _damageFloor = 0f;        // absolute minimum health
    private float _maxDamagePerHit = 0f;    // 0 = no cap

    /// <summary>Sets the absolute minimum health this entity cannot drop below.</summary>
    public void SetDamageFloor(float minHealth) => _damageFloor = Mathf.Clamp(minHealth, 0f, maxHealth);

    /// <summary>Caps the damage applied per single hit (0 = uncapped).</summary>
    public void SetMaxDamagePerHit(float cap) => _maxDamagePerHit = Mathf.Max(0f, cap);

    void Awake()
    {
        CurrentHealth = maxHealth;
        _filters = GetComponents<IDamageFilter>();
        _reactions = GetComponents<IDamageReaction>();
    }

    /// <summary>
    /// Applies damage through the full pipeline: invulnerability window, filter vetoes,
    /// damage cap/floor, reaction callbacks, then HUD/damage/death events.
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        if (IsDead) return;

        if (Time.time - _lastHitTime < invulnerabilityDuration) return;

        for (int i = 0; i < _filters.Length; i++)
        {
            if (_filters[i].ShouldBlock(info)) return;
        }

        _lastHitTime = Time.time;

        float dmg = _maxDamagePerHit > 0f ? Mathf.Min(info.amount, _maxDamagePerHit) : info.amount;
        CurrentHealth = Mathf.Max(_damageFloor, CurrentHealth - dmg);

        for (int i = 0; i < _reactions.Length; i++)
        {
            _reactions[i].OnDamaged(info);
        }

        onDamaged.Invoke(maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f);
        Damaged?.Invoke(info);

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            onDied.Invoke();
            Died?.Invoke();
        }
    }

    /// <summary>Restores the entity to full health and clears the dead flag (enemy pooling / arena restart).</summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        _lastHitTime = float.NegativeInfinity;
        _damageFloor = 0f;
        _maxDamagePerHit = 0f;
    }

    /// <summary>
    /// Health pickup / regen. No revive: a dead entity does not heal (revive happens on
    /// arena restart). onDamaged doubles as a "health changed" signal for the HUD, so it
    /// fires here with the new normalized health.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        onDamaged.Invoke(Normalized);
    }
}
