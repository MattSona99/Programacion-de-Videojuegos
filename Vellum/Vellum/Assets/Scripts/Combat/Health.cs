using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class HealthChangedEvent : UnityEvent<float> { }

public class Health : MonoBehaviour, IDamageable
{
    [Header("Vita")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("Finestra di invulnerabilità dopo un colpo. Player ~0.5, nemici ~0.1, statua 0.")]
    [SerializeField] private float invulnerabilityDuration = 0f;

    [Header("Eventi")]
    [Tooltip("Invocato a ogni danno subìto. Passa la vita normalizzata (0..1) per l'HUD.")]
    [SerializeField] private HealthChangedEvent onDamaged;

    [Tooltip("Invocato una sola volta quando la vita arriva a 0.")]
    [SerializeField] private UnityEvent onDied;

    // Evento C# (oltre all'UnityEvent onDied) per agganci a runtime: WaveManager,
    // pooling, restart arena. Si sottoscrive una volta sola e sopravvive al riuso.
    public event Action Died;

    // Evento C# SOLO-danno (non scatta su Heal, a differenza di onDamaged che fa
    // anche da "health changed" per la HUD). Usato dal duello finale per spawnare i
    // pickup di vita a ogni colpo a segno senza innescare un loop col Heal.
    public event Action<DamageInfo> Damaged;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float MaxHealth => maxHealth;
    public float Normalized => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

    private float _lastHitTime = float.NegativeInfinity;
    private IDamageFilter[] _filters;
    private IDamageReaction[] _reactions;

    // Regolatori runtime (default disattivi → nessun impatto su Player/nemici/
    // statua). Usati dal duello finale: floor = vita minima per fase (Boss non
    // sotto il 50% in Fase 1); cap = danno massimo per colpo (1 mentre Jammo
    // trasporta i pezzi).
    private float _damageFloor = 0f;        // vita minima assoluta
    private float _maxDamagePerHit = 0f;    // 0 = nessun cap

    public void SetDamageFloor(float minHealth) => _damageFloor = Mathf.Clamp(minHealth, 0f, maxHealth);
    public void SetMaxDamagePerHit(float cap) => _maxDamagePerHit = Mathf.Max(0f, cap);

    void Awake()
    {
        CurrentHealth = maxHealth;
        _filters = GetComponents<IDamageFilter>();
        _reactions = GetComponents<IDamageReaction>();
    }

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

    // Riporta in vita l'entità (pooling nemici / restart arena).
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        _lastHitTime = float.NegativeInfinity;
        _damageFloor = 0f;
        _maxDamagePerHit = 0f;
    }

    // Pickup di vita / regen. Niente revive: se morto, non guarisce (il revive
    // arriverà col restart arena, sotto-progetto #6). onDamaged fa anche da
    // "health changed" per la HUD: l'evento gira con la nuova vita normalizzata.
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        onDamaged.Invoke(Normalized);
    }
}
