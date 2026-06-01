using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player health bar. Hooks Health.onDamaged from the Inspector (UnityEvent&lt;float&gt; →
/// UpdateBar): the event fires with the normalized health 0..1 on both damage and heals (see
/// Health.Heal). No Singleton: there's a single Player, references are via the Inspector.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("The Player's Health. Used to read the initial state and for the numeric label.")]
    [SerializeField] private Health playerHealth;

    [Header("Visuals")]
    [Tooltip("Image type=Filled, fillMethod=Horizontal: fillAmount follows the normalized health.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Color gradient as a function of normalized health. Example: red at 0, yellow at 0.5, green at 1. Configurable from the Inspector.")]
    [SerializeField] private Gradient fillGradient;
    [Tooltip("Optional 'X / MaxHealth' label. Leave empty for a bar without numbers.")]
    [SerializeField] private TMP_Text valueLabel;

    [Header("Animation")]
    [Tooltip("Duration of the smooth transition between the old and new value. 0 = instant.")]
    [SerializeField] private float smoothDuration = 0.25f;

    private Coroutine _smoothRoutine;
    private float _displayed;

    void Start()
    {
        if (playerHealth != null)
        {
            _displayed = playerHealth.Normalized;
            Apply(_displayed);
        }
    }

    /// <summary>Wire from Inspector: Health.onDamaged (Float) → PlayerHUD.UpdateBar. Smoothly animates the bar to the new normalized value.</summary>
    public void UpdateBar(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (smoothDuration <= 0f || !isActiveAndEnabled)
        {
            _displayed = normalized;
            Apply(_displayed);
            return;
        }

        if (_smoothRoutine != null) StopCoroutine(_smoothRoutine);
        _smoothRoutine = StartCoroutine(SmoothTo(normalized));
    }

    private IEnumerator SmoothTo(float target)
    {
        float from = _displayed;
        float t = 0f;
        while (t < smoothDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / smoothDuration);
            float eased = k * k * (3f - 2f * k); // smoothstep
            _displayed = Mathf.Lerp(from, target, eased);
            Apply(_displayed);
            yield return null;
        }
        _displayed = target;
        Apply(_displayed);
        _smoothRoutine = null;
    }

    private void Apply(float n)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = n;
            if (fillGradient != null) fillImage.color = fillGradient.Evaluate(n);
        }
        if (valueLabel != null && playerHealth != null)
        {
            int cur = Mathf.RoundToInt(n * playerHealth.MaxHealth);
            int max = Mathf.RoundToInt(playerHealth.MaxHealth);
            valueLabel.text = $"{cur} / {max}";
        }
    }
}
