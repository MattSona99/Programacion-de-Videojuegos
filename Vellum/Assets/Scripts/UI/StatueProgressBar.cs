using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vertical completion bar for Jammo's statue (Act_02). Hooks `StatueRig.onPartRevealed` from the
/// Inspector and reads the normalized completion from the rig. Lerp animation with smoothstep,
/// like PlayerHUD.
/// </summary>
public class StatueProgressBar : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Statue whose completion to show.")]
    [SerializeField] private StatueRig statueRig;

    [Header("Visuals")]
    [Tooltip("Image type=Filled, fillMethod=Vertical, fillOrigin=Bottom: fillAmount follows the normalized completion.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Color gradient of the liquid as a function of completion (e.g. cold blue at 0, golden at 1).")]
    [SerializeField] private Gradient fillGradient;
    [Tooltip("Liquid-level 'cap' ellipse (optional). Make it a child of the fill's parent, anchor X stretch + Y bottom, pivot (0.5, 0.5). Moves dynamically to give the cut the cylinder's curved shape.")]
    [SerializeField] private RectTransform meniscusRect;

    [Header("Label (optional)")]
    [Tooltip("Optional TMP_Text: number of inserted pieces / total, or percentage.")]
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("True → '75%'. False → 'X / Y'.")]
    [SerializeField] private bool showPercentage = false;

    [Header("Animation")]
    [Tooltip("Duration of the smooth transition between the previous and new value. 0 = instant.")]
    [SerializeField] private float smoothDuration = 0.4f;

    private Coroutine _smoothRoutine;
    private float _displayed;

    void Start()
    {
        if (meniscusRect != null)
        {
            // Force anchor X stretch + Y bottom, pivot center: Apply's calculation
            // assumes this configuration (anchoredPosition.y = 0 means "bottom of the
            // cylinder" and grows upward).
            meniscusRect.anchorMin = new Vector2(0f, 0f);
            meniscusRect.anchorMax = new Vector2(1f, 0f);
            meniscusRect.pivot = new Vector2(0.5f, 0.5f);
        }

        if (statueRig != null)
        {
            _displayed = statueRig.Normalized;
            Apply(_displayed);
        }
        else
        {
            Apply(0f);
        }
    }

    /// <summary>Wire from Inspector: StatueRig.onPartRevealed → StatueProgressBar.UpdateBar. Smoothly animates the bar to the new completion.</summary>
    public void UpdateBar()
    {
        if (statueRig == null) return;
        float target = Mathf.Clamp01(statueRig.Normalized);

        if (smoothDuration <= 0f || !isActiveAndEnabled)
        {
            _displayed = target;
            Apply(_displayed);
            return;
        }

        if (_smoothRoutine != null) StopCoroutine(_smoothRoutine);
        _smoothRoutine = StartCoroutine(SmoothTo(target));
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

            if (meniscusRect != null)
            {
                // Ellipse centered on the liquid level (half below, half above the
                // cut): the straight fillAmount cut visually becomes a curved
                // surface. Hidden when the level reaches the bottom or the top.
                float h = fillImage.rectTransform.rect.height;
                Vector2 pos = meniscusRect.anchoredPosition;
                pos.y = n * h;
                meniscusRect.anchoredPosition = pos;
                meniscusRect.gameObject.SetActive(n > 0.001f && n < 0.999f);
            }
        }
        if (valueLabel != null && statueRig != null)
        {
            if (showPercentage)
            {
                valueLabel.text = $"{Mathf.RoundToInt(n * 100f)}%";
            }
            else
            {
                int cur = Mathf.RoundToInt(n * statueRig.TotalSlots);
                valueLabel.text = $"{cur} / {statueRig.TotalSlots}";
            }
        }
    }
}
