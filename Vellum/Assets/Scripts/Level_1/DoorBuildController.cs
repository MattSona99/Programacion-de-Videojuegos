using System.Collections;
using UnityEngine;

/// <summary>
/// Drives a "build up" reveal of the Act 1 door by feeding a per-step height into the door
/// material's _BuildHeight shader property via a MaterialPropertyBlock, tweened smoothly.
/// Each completed path step raises the door a bit more.
/// </summary>
public class DoorBuildController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Renderer of the door mesh (e.g. SM_Arc). If empty, it's found via GetComponentInChildren.")]
    [SerializeField] private Renderer doorRenderer;

    [Header("Reveal")]
    [Tooltip("Duration of the tween between one step and the next.")]
    [SerializeField] private float tweenDuration = 0.5f;

    [Tooltip("Vertical margin added above and below the mesh bounds: step 0 hides a touch below, the max step covers a touch above.")]
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
            Debug.LogError($"[DoorBuildController] {name}: no Renderer found. Component disabled.", this);
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

    /// <summary>Tweens the door build height to the fraction <paramref name="step"/>/<paramref name="totalSteps"/>.</summary>
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
