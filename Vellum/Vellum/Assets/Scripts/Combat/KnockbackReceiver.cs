using System.Collections;
using UnityEngine;

// Va SOLO sui nemici (mai sul Player): quando l'entità subisce danno viene
// spinta indietro con un arco stile Minecraft (scatto indietro + saltello su,
// poi ricaduta e atterraggio alla quota di partenza o sul terreno reale).
public class KnockbackReceiver : MonoBehaviour, IDamageReaction
{
    [SerializeField] private float horizontalSpeed = 6f;
    [SerializeField] private float upwardSpeed = 4.5f;
    [SerializeField] private float knockbackGravity = -22f;
    [Tooltip("Safety: fine forzata se non atterra (terreno non piano).")]
    [SerializeField] private float maxAirTime = 1.5f;
    [Tooltip("Tempo minimo tra due knockback: evita che click ravvicinati concatenino archi e spingano il nemico lontanissimo.")]
    [SerializeField] private float knockbackCooldown = 0.35f;

    private CharacterController _controller;
    private Coroutine _currentKnockback;
    private float _lastKnockbackEnd = float.NegativeInfinity;

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

    public void CancelKnockback()
    {
        if (_currentKnockback != null)
        {
            StopCoroutine(_currentKnockback);
            _currentKnockback = null;
        }
    }

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

            // ATTERRAGGIO CORRETTO: 
            // isGrounded è nativo del CharacterController e capisce le vere collisioni.
            // Lo controlliamo solo quando vY < 0 (cioè quando il nemico è in fase di ricaduta dall'arco).
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