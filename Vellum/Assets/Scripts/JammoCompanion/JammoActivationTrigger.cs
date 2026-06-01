using UnityEngine;
using UnityEngine.Events;

/// <summary>One-shot trigger volume: invokes a UnityEvent the first time the Player enters it (e.g. to wake Jammo).</summary>
[RequireComponent(typeof(Collider))]
public class JammoActivationTrigger : MonoBehaviour
{
    [Tooltip("Invoked the first time the player enters the trigger")]
    public UnityEvent onPlayerEnter;

    private bool _hasFired = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFired) return;
        if (!other.CompareTag("Player")) return;

        _hasFired = true;
        onPlayerEnter.Invoke();
    }
}
