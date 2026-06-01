using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Goes on the arena Jammo next to Health (and JammoCarrier). Mirror of PlayerHealth: on death
/// it plays the death clip on Jammo's Animator, waits to show it, then invokes onJammoDied. Wire
/// onJammoDied in the Inspector to MainMenuManager.ShowGameOver and WaveManager.StopAndEnd:
/// if Jammo dies, the level is over.
/// </summary>
[RequireComponent(typeof(Health))]
public class JammoHealth : MonoBehaviour
{
    [Tooltip("Invoked AFTER the death animation. Wire ShowGameOver and WaveManager.StopAndEnd from the Inspector.")]
    [SerializeField] private UnityEvent onJammoDied;

    [Header("Death animation")]
    [Tooltip("Jammo's Animator. If empty, found via GetComponentInChildren in Awake.")]
    [SerializeField] private Animator jammoAnimator;
    [Tooltip("Name of the death trigger in Jammo's Animator.")]
    [SerializeField] private string deathTriggerName = "Die";
    [Tooltip("Seconds to show the death clip before Game Over.")]
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

    /// <summary>Wire this to Health.onDied in the Inspector. Starts the death sequence once.</summary>
    public void HandleDied()
    {
        if (_isDying) return;
        _isDying = true;
        StartCoroutine(DeathRoutine());
    }

    /// <summary>Triggers Jammo's death clip, waits, then fires onJammoDied (Game Over + stop waves).</summary>
    private IEnumerator DeathRoutine()
    {
        // JammoCarrier already stops movement/coroutines on its death (Health.Died).
        if (jammoAnimator != null)
        {
            _animParams.Refresh(jammoAnimator);
            if (_animParams.Has(_deathTriggerHash))
                jammoAnimator.SetTrigger(_deathTriggerHash);
            else
                Debug.LogWarning($"[JammoHealth] Trigger '{deathTriggerName}' missing in Jammo's Animator: skipping the death animation.", this);
        }

        // timeScale is still 1 here (ShowGameOver zeroes it afterwards): the clip plays.
        if (deathAnimationDuration > 0f) yield return new WaitForSecondsRealtime(deathAnimationDuration);

        onJammoDied.Invoke();
    }
}
