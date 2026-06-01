using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class JammoActivationTrigger : MonoBehaviour
{
    [Tooltip("Invocato la prima volta che il player entra nel trigger")]
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
