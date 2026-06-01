using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra di vita del Player. Si aggancia a Health.onDamaged dall'Inspector
// (UnityEvent<float> → UpdateBar): l'evento gira con la vita normalizzata 0..1
// sia sui danni sia sugli heal (vedi Health.Heal). Niente Singleton: c'è un
// solo Player, le reference sono via Inspector.
public class PlayerHUD : MonoBehaviour
{
    [Header("Sorgente")]
    [Tooltip("Health del Player. Usato per leggere lo stato iniziale e per il label numerico.")]
    [SerializeField] private Health playerHealth;

    [Header("Visuale")]
    [Tooltip("Image type=Filled, fillMethod=Horizontal: fillAmount segue la vita normalizzata.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Gradient colore in funzione della vita normalizzata. Esempio: rosso a 0, giallo a 0.5, verde a 1. Configurabile dall'Inspector.")]
    [SerializeField] private Gradient fillGradient;
    [Tooltip("Label opzionale 'X / MaxHealth'. Lascia vuoto per barra senza numeri.")]
    [SerializeField] private TMP_Text valueLabel;

    [Header("Animazione")]
    [Tooltip("Durata della transizione fluida tra il vecchio e il nuovo valore. 0 = istantaneo.")]
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

    // Wirare da Inspector: Health.onDamaged (Float) → PlayerHUD.UpdateBar
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
