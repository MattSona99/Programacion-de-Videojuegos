using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Va sul Player accanto a Health. Aggancia HandleDied a Health.onDied
// dall'Inspector e ShowGameOver a onPlayerDied. Alla morte fa partire la clip
// di morte sull'Animator del mesh attivo, attende, poi invoca onPlayerDied.
[RequireComponent(typeof(Health))]
public class PlayerHealth : MonoBehaviour
{
    [Tooltip("Collega qui MainMenuManager.ShowGameOver dall'Inspector. Invocato DOPO l'animazione di morte.")]
    [SerializeField] private UnityEvent onPlayerDied;

    [Header("Animazione di morte")]
    [Tooltip("Nome del trigger di morte: i due Animator controller (maschio e femmina) devono usarlo identico.")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("Secondi per far vedere la clip di morte prima della schermata di Game Over.")]
    [SerializeField] private float deathAnimationDuration = 1.5f;

    private Health _health;
    public Health Health => _health;

    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();
    private int _deathTriggerHash;
    private bool _isDying;

    void Awake()
    {
        _health = GetComponent<Health>();
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
        // Blocca movimento/combat ma lascia vivo l'Animator (il lock del
        // DialogueManager NON disabilita l'Animator) così la clip di morte gira.
        if (DialogueManager.Instance != null) DialogueManager.Instance.LockPlayer();

        // Animator del mesh attivo: le geometrie inattive sono SetActive(false),
        // quindi GetComponentInChildren torna quella corrente (maschio o femmina).
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            _animParams.Refresh(animator);
            if (_animParams.Has(_deathTriggerHash))
                animator.SetTrigger(_deathTriggerHash);
            else
                Debug.LogWarning($"[PlayerHealth] Trigger '{deathTriggerName}' assente nell'Animator attivo: salto l'animazione di morte.", this);
        }

        // Qui timeScale è ancora 1 (ShowGameOver lo azzera dopo): la clip gira.
        if (deathAnimationDuration > 0f) yield return new WaitForSecondsRealtime(deathAnimationDuration);

        onPlayerDied.Invoke();
    }
}
