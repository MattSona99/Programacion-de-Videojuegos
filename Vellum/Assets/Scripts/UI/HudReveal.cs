using System.Collections;
using UnityEngine;

/// <summary>
/// Slide-from-bottom + fade for a UI element. Goes on the root GameObject of the UI to reveal
/// (e.g. HUDPlayer, StatueProgressBar) together with a CanvasGroup. Reveal() / Hide() are public
/// and idempotent: callable by the scene director (Act02Director). hiddenOnStart=true prepares the
/// "off-screen" state in Awake so the Editor pose matches the final visible pose.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HudReveal : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Leave empty to use the CanvasGroup on this GameObject (if missing, alpha is not animated).")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [Tooltip("How many pixels below the Editor pose the element starts/ends during the animation.")]
    [SerializeField] private float slideDistance = 120f;
    [Tooltip("Transition duration (both Reveal and Hide).")]
    [SerializeField] private float duration = 0.6f;
    [Tooltip("If true, in Awake the element is set to the hidden state (alpha 0, offset below). Disable if the initial pose is already the visible one and it's only revealed at runtime.")]
    [SerializeField] private bool hiddenOnStart = true;

    private RectTransform _rect;
    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _routine;
    private bool _isVisible;

    /// <summary>Visible state (intent, set immediately in Reveal/Hide). Used by the pause menu to restore only the bars that were visible on resume.</summary>
    public bool IsVisible => _isVisible;

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

    /// <summary>Slides the element in and fades it to alpha 1.</summary>
    public void Reveal()
    {
        if (_isVisible && _routine == null) return;
        _isVisible = true;
        Run(targetAlpha: 1f, targetPos: _shownPos);
    }

    /// <summary>Slides the element out and fades it to alpha 0.</summary>
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
            // unscaledDeltaTime: the Hide on Player death runs with the game paused
            // (timeScale=0). The prologue Reveal/Hide run at timeScale=1 anyway,
            // where scaled and unscaled coincide.
            t += Time.unscaledDeltaTime;
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
