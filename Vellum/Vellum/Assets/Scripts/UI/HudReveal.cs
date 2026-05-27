using System.Collections;
using UnityEngine;

// Slide-from-bottom + fade per un elemento UI. Va sul GameObject root dell'UI
// da rivelare (es. HUDPlayer, StatueProgressBar) insieme a un CanvasGroup.
// Reveal() / Hide() sono pubblici e idempotenti: chiamabili dal regista di
// scena (Act02Director). hiddenOnStart=true prepara lo stato "fuori schermo"
// in Awake così la posa Editor coincide con la posa visibile finale.
[RequireComponent(typeof(RectTransform))]
public class HudReveal : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Lasciare vuoto per usare il CanvasGroup su questo GameObject (se manca, l'alpha non viene animato).")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animazione")]
    [Tooltip("Quanti pixel sotto la posa Editor parte/finisce l'elemento durante l'animazione.")]
    [SerializeField] private float slideDistance = 120f;
    [Tooltip("Durata della transizione (sia Reveal che Hide).")]
    [SerializeField] private float duration = 0.6f;
    [Tooltip("Se true, in Awake l'elemento viene messo in stato nascosto (alpha 0, offset sotto). Disattivare se la posa iniziale è già quella visibile e si rivela solo a runtime.")]
    [SerializeField] private bool hiddenOnStart = true;

    private RectTransform _rect;
    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _routine;
    private bool _isVisible;

    void Awake()
    {
        _rect = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        _shownPos = _rect.anchoredPosition;
        _hiddenPos = _shownPos + new Vector2(0f, -slideDistance);

        if (hiddenOnStart)
        {
            _rect.anchoredPosition = _hiddenPos;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            _isVisible = false;
        }
        else
        {
            _isVisible = true;
        }
    }

    public void Reveal()
    {
        if (_isVisible && _routine == null) return;
        _isVisible = true;
        Run(targetAlpha: 1f, targetPos: _shownPos);
    }

    public void Hide()
    {
        if (!_isVisible && _routine == null) return;
        _isVisible = false;
        Run(targetAlpha: 0f, targetPos: _hiddenPos);
    }

    private void Run(float targetAlpha, Vector2 targetPos)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(targetAlpha, targetPos));
    }

    private IEnumerator Animate(float targetAlpha, Vector2 targetPos)
    {
        float fromAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        Vector2 fromPos = _rect.anchoredPosition;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = k * k * (3f - 2f * k);
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(fromAlpha, targetAlpha, eased);
            _rect.anchoredPosition = Vector2.Lerp(fromPos, targetPos, eased);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = targetAlpha;
        _rect.anchoredPosition = targetPos;
        _routine = null;
    }
}
