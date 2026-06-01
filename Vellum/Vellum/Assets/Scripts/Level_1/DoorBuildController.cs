using System.Collections;
using UnityEngine;

public class DoorBuildController : MonoBehaviour
{
    [Header("Riferimenti")]
    [Tooltip("Renderer del mesh della porta (es. SM_Arc). Se vuoto, viene cercato in GetComponentInChildren.")]
    [SerializeField] private Renderer doorRenderer;

    [Header("Reveal")]
    [Tooltip("Durata del tween tra uno step e il successivo.")]
    [SerializeField] private float tweenDuration = 0.5f;

    [Tooltip("Margine verticale aggiunto sopra e sotto i bounds del mesh: lo step 0 nasconde anche un pelo sotto, lo step max copre anche un pelo sopra.")]
    [SerializeField] private float verticalPadding = 0.1f;

    private static readonly int BuildHeightProp = Shader.PropertyToID("_BuildHeight");

    private MaterialPropertyBlock _propBlock;
    private float _baseY;
    private float _topY;
    private float _currentHeight;
    private Coroutine _tween;

    void Awake()
    {
        if (doorRenderer == null) doorRenderer = GetComponentInChildren<Renderer>();
        if (doorRenderer == null)
        {
            Debug.LogError($"[DoorBuildController] {name}: nessun Renderer trovato. Componente disabilitato.", this);
            enabled = false;
            return;
        }

        Bounds b = doorRenderer.bounds;
        _baseY = b.min.y - verticalPadding;
        _topY = b.max.y + verticalPadding;
        _currentHeight = _baseY;

        _propBlock = new MaterialPropertyBlock();
        ApplyHeight();
    }

    public void SetProgressStep(int step, int totalSteps)
    {
        if (totalSteps <= 0) return;
        float target = Mathf.Lerp(_baseY, _topY, (float)step / totalSteps);
        if (_tween != null) StopCoroutine(_tween);
        _tween = StartCoroutine(TweenBuildHeight(target));
    }

    private IEnumerator TweenBuildHeight(float target)
    {
        float start = _currentHeight;
        float t = 0f;
        while (t < tweenDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / tweenDuration);
            _currentHeight = Mathf.Lerp(start, target, u);
            ApplyHeight();
            yield return null;
        }
        _currentHeight = target;
        ApplyHeight();
        _tween = null;
    }

    private void ApplyHeight()
    {
        if (doorRenderer == null) return;
        doorRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(BuildHeightProp, _currentHeight);
        doorRenderer.SetPropertyBlock(_propBlock);
    }
}
