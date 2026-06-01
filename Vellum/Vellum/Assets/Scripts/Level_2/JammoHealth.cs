using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Va sul Jammo dell'arena accanto a Health (e JammoCarrier). Specchio di
// PlayerHealth: alla morte fa partire la clip di morte sull'Animator di Jammo,
// attende un attimo per mostrarla, poi invoca onJammoDied. Wira onJammoDied
// dall'Inspector a MainMenuManager.ShowGameOver e WaveManager.StopAndEnd:
// se Jammo muore, il livello è finito (richiesta #5).
[RequireComponent(typeof(Health))]
public class JammoHealth : MonoBehaviour
{
    [Tooltip("Invocato DOPO l'animazione di morte. Collega ShowGameOver e WaveManager.StopAndEnd dall'Inspector.")]
    [SerializeField] private UnityEvent onJammoDied;

    [Header("Animazione di morte")]
    [Tooltip("Animator di Jammo. Se vuoto, cercato in GetComponentInChildren in Awake.")]
    [SerializeField] private Animator jammoAnimator;
    [Tooltip("Nome del trigger di morte nell'Animator di Jammo.")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("Secondi per far vedere la clip di morte prima del Game Over.")]
    [SerializeField] private float deathAnimationDuration = 1.5f;

    private Health _health;
    public Health Health => _health;

    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();
    private int _deathTriggerHash;
    private bool _isDying;

    void Awake()
    {
        _health = GetComponent<Health>();
        if (jammoAnimator == null) jammoAnimator = GetComponentInChildren<Animator>();
        _deathTriggerHash = Animator.StringToHash(deathTriggerName);
    }

    // Da agganciare a Health.onDied nell'Inspector.
    public void HandleDied()
    {
        if (_isDying) return;
        _isDying = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // JammoCarrier ferma già movimento/coroutine alla sua morte (Health.Died).
        if (jammoAnimator != null)
        {
            _animParams.Refresh(jammoAnimator);
            if (_animParams.Has(_deathTriggerHash))
                jammoAnimator.SetTrigger(_deathTriggerHash);
            else
                Debug.LogWarning($"[JammoHealth] Trigger '{deathTriggerName}' assente nell'Animator di Jammo: salto l'animazione di morte.", this);
        }

        // timeScale ancora 1 qui (ShowGameOver lo azzera dopo): la clip gira.
        if (deathAnimationDuration > 0f) yield return new WaitForSecondsRealtime(deathAnimationDuration);

        onJammoDied.Invoke();
    }
}
