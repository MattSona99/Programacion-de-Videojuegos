using System;
using System.Collections;
using UnityEngine;

// Pickup di vita NEUTRO del duello finale: cura chiunque dei due contendenti lo
// raccolga (Player o Boss), camminandoci sopra. Diverso da HealthPickup (che è
// legato al tag Player / coordinator dell'arena): qui i due Health validi sono
// iniettati dallo spawner, così non cura Jammo né triggera sui pezzi di scena.
// Pool-friendly (niente Destroy, CLAUDE.md §4.3); il look è in Editor.
[RequireComponent(typeof(Collider))]
public class DuelHealthPickup : MonoBehaviour
{
    private Action _onRemoved;
    private Health _a;
    private Health _b;
    private float _healAmount;
    private bool _consumed;
    private Coroutine _lifetimeRoutine;

    // Istante (Time.time) in cui il pickup è stato attivato: il boss lo "nota" solo
    // dopo un ritardo di reazione (vedi BossDuelAI.pickupReactionDelay).
    public float SpawnTime { get; private set; }

    // Configurato dallo spawner allo spawn dal pool. onRemoved rimette il pickup nel
    // pool e lo deregistra (vale sia per raccolta sia per scadenza lifetime).
    public void Configure(Action onRemoved, Health a, Health b, float healAmount, float lifetime)
    {
        _onRemoved = onRemoved;
        _a = a;
        _b = b;
        _healAmount = healAmount;
        _consumed = false;
        SpawnTime = Time.time;

        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        if (lifetime > 0f) _lifetimeRoutine = StartCoroutine(LifetimeRoutine(lifetime));
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        _lifetimeRoutine = null;
        Remove();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        Health entered = other.GetComponentInParent<Health>();
        if (entered == null || entered.IsDead) return;
        if (entered != _a && entered != _b) return; // solo Player o Boss

        entered.Heal(_healAmount);
        Remove();
    }

    private void Remove()
    {
        if (_consumed) return;
        _consumed = true;
        if (_lifetimeRoutine != null) { StopCoroutine(_lifetimeRoutine); _lifetimeRoutine = null; }
        _onRemoved?.Invoke();
    }
}
