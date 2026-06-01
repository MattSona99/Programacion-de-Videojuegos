using System.Collections;
using UnityEngine;

/// <summary>
/// Goes ONLY on enemies (never on the Player): as an <see cref="IDamageReaction"/>,
/// when the entity takes damage it is pushed back along a Minecraft-style arc
/// (backward dash + small hop, then fall and land at the start height or on real ground).
/// </summary>
public class KnockbackReceiver : MonoBehaviour, IDamageReaction
{
    [SerializeField] private float horizontalSpeed = 6f;
    [SerializeField] private float upwardSpeed = 4.5f;
    [SerializeField] private float knockbackGravity = -22f;
    [Tooltip("Safety: force the end if it never lands (uneven ground).")]
    [SerializeField] private float maxAirTime = 1.5f;
    [Tooltip("Minimum time between two knockbacks: prevents rapid clicks from chaining arcs and shoving the enemy very far away.")]
    [SerializeField] private float knockbackCooldown = 0.35f;

    private CharacterController _controller;
    private Coroutine _currentKnockback;
    private float _lastKnockbackEnd = float.NegativeInfinity;

    /// <summary>True while a knockback arc is currently playing.</summary>
    public bool IsKnockbackActive => _currentKnockback != null;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        _currentKnockback = null;
    }

    void OnDisable()
    {
        _currentKnockback = null;
    }

    /// <summary>Immediately stops any active knockback arc.</summary>
    public void CancelKnockback()
    {
        if (_currentKnockback != null)
        {
            StopCoroutine(_currentKnockback);
            _currentKnockback = null;
        }
    }

    /// <summary>Starts a knockback arc away from the damage source (respecting the cooldown).</summary>
    public void OnDamaged(DamageInfo info)
    {
        if (_currentKnockback != null) return;
        if (Time.time - _lastKnockbackEnd < knockbackCooldown) return;

        Vector3 dir = transform.position - info.sourcePosition;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        dir.Normalize();

        _currentKnockback = StartCoroutine(KnockbackArcRoutine(dir));
    }

    /// <summary>Integrates the backward+upward arc each frame until landing or <see cref="maxAirTime"/>.</summary>
    private IEnumerator KnockbackArcRoutine(Vector3 horizontalDir)
    {
        float vY = upwardSpeed;
        float elapsed = 0f;

        while (true)
        {
            float dt = Time.deltaTime;
            vY += knockbackGravity * dt;

            if (_controller == null || !_controller.enabled) break;

            Vector3 delta = horizontalDir * horizontalSpeed * dt + Vector3.up * vY * dt;
            _controller.Move(delta);

            elapsed += dt;

            // Correct landing detection:
            // isGrounded is native to the CharacterController and understands real collisions.
            // We only check it while vY < 0 (i.e. when the enemy is falling back down from the arc).
            if (vY < 0f && _controller.isGrounded)
            {
                break;
            }

            if (elapsed >= maxAirTime) break;

            yield return null;
        }

        _currentKnockback = null;
        _lastKnockbackEnd = Time.time;
    }
}