using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Goes on the Player next to <see cref="Health"/>. Wire HandleDied to Health.onDied and
/// ShowGameOver to onPlayerDied from the Inspector. On death it plays the death clip on the
/// active mesh's Animator, waits, then invokes onPlayerDied.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHealth : MonoBehaviour
{
    [Tooltip("Wire MainMenuManager.ShowGameOver here from the Inspector. Invoked AFTER the death animation.")]
    [SerializeField] private UnityEvent onPlayerDied;

    [Header("Death animation")]
    [Tooltip("Name of the death trigger: both Animator controllers (male and female) must use it identically.")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("Seconds to show the death clip before the Game Over screen.")]
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

    /// <summary>Wire this to Health.onDied in the Inspector. Starts the death sequence once.</summary>
    public void HandleDied()
    {
        if (_isDying) return;
        _isDying = true;
        StartCoroutine(DeathRoutine());
    }

    /// <summary>Locks the player, triggers the death clip on the active Animator, waits, then fires onPlayerDied.</summary>
    private IEnumerator DeathRoutine()
    {
        // Lock movement/combat but keep the Animator alive (the DialogueManager lock does
        // NOT disable the Animator) so the death clip can play.
        if (DialogueManager.Instance != null) DialogueManager.Instance.LockPlayer();

        // Animator of the active mesh: inactive geometries are SetActive(false),
        // so GetComponentInChildren returns the current one (male or female).
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            _animParams.Refresh(animator);
            if (_animParams.Has(_deathTriggerHash))
                animator.SetTrigger(_deathTriggerHash);
            else
                Debug.LogWarning($"[PlayerHealth] Trigger '{deathTriggerName}' missing on the active Animator: skipping the death animation.", this);
        }

        // timeScale is still 1 here (ShowGameOver zeroes it afterwards): the clip plays.
        if (deathAnimationDuration > 0f) yield return new WaitForSecondsRealtime(deathAnimationDuration);

        onPlayerDied.Invoke();
    }
}
