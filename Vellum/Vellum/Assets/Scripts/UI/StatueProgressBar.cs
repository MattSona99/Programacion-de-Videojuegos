using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Barra verticale di completamento della statua di Jammo (Act_02). Si aggancia
// allo `StatueRig.onPartRevealed` dall'Inspector e legge il completamento
// normalizzato dal rig. Animazione lerp con smoothstep, come PlayerHUD.
public class StatueProgressBar : MonoBehaviour
{
    [Header("Sorgente")]
    [Tooltip("Statua di cui mostrare il completamento.")]
    [SerializeField] private StatueRig statueRig;

    [Header("Visuale")]
    [Tooltip("Image type=Filled, fillMethod=Vertical, fillOrigin=Bottom: fillAmount segue il completamento normalizzato.")]
    [SerializeField] private Image fillImage;
    [Tooltip("Gradient colore del liquido in funzione del completamento (es. blu freddo a 0, dorato a 1).")]
    [SerializeField] private Gradient fillGradient;
    [Tooltip("Ellisse 'cap' del livello del liquido (opzionale). Va figlio dello stesso parent del fill, anchor X stretch + Y bottom, pivot (0.5, 0.5). Si sposta dinamicamente per dare al taglio la forma curva del cilindro.")]
    [SerializeField] private RectTransform meniscusRect;

    [Header("Etichetta (opzionale)")]
    [Tooltip("TMP_Text opzionale: numero di pezzi inseriti / totale, o percentuale.")]
    [SerializeField] private TMP_Text valueLabel;
    [Tooltip("True → '75%'. False → 'X / Y'.")]
    [SerializeField] private bool showPercentage = false;

    [Header("Animazione")]
    [Tooltip("Durata della transizione fluida tra valore precedente e nuovo. 0 = istantaneo.")]
    [SerializeField] private float smoothDuration = 0.4f;

    private Coroutine _smoothRoutine;
    private float _displayed;

    void Start()
    {
        if (meniscusRect != null)
        {
            // Forziamo anchor X stretch + Y bottom, pivot center: il calcolo
            // di Apply assume questa configurazione (anchoredPosition.y = 0
            // significa "fondo del cilindro" e cresce verso l'alto).
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

    // Wirare da Inspector: StatueRig.onPartRevealed → StatueProgressBar.UpdateBar
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
                // Ellisse centrata sul livello del liquido (metà sotto, metà
                // sopra il taglio): il taglio retto del fillAmount diventa
                // visivamente una superficie curva. Si nasconde quando il
                // livello tocca il fondo o il top.
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
