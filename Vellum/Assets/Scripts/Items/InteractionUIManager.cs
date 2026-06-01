using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Singleton that manages the screen-space interaction prompt ("bubble"). It follows the
/// Player via Camera.WorldToScreenPoint and fades in/out via a coroutine, swapping the prompt
/// icon per interactable.
/// </summary>
public class InteractionUIManager : MonoBehaviour
{
    public static InteractionUIManager Instance;

    [Header("UI components")]
    public CanvasGroup promptCanvasGroup;
    public RectTransform promptRectTransform;
    public Image promptImage; // The image container

    [Header("Camera and position settings")]
    public Camera mainCamera;
    public Vector2 screenOffset = new Vector2(150f, 0f);
    public float fadeSpeed = 8f;

    private Coroutine _fadeCoroutine;
    private Transform _playerTransform;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (promptCanvasGroup != null) promptCanvasGroup.alpha = 0;
        if (mainCamera == null) mainCamera = Camera.main;
    }

    void Update()
    {
        if (promptCanvasGroup.alpha > 0 && _playerTransform != null && mainCamera != null)
        {
            Vector3 worldPosition = _playerTransform.position + Vector3.up * 1.0f;
            Vector2 screenPos = mainCamera.WorldToScreenPoint(worldPosition);
            promptRectTransform.position = screenPos + screenOffset;
        }
    }

    /// <summary>Shows the prompt following <paramref name="playerTarget"/> with the given icon, fading in.</summary>
    public void ShowPrompt(Transform playerTarget, Sprite promptSprite)
    {
        _playerTransform = playerTarget;

        // Swap the image dynamically and resize to fit
        if (promptImage != null && promptSprite != null)
        {
            promptImage.sprite = promptSprite;
            promptImage.SetNativeSize();
        }

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(1f));
    }

    /// <summary>Hides the prompt, fading out.</summary>
    public void HidePrompt()
    {
        _playerTransform = null;
        
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(0f));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        while (!Mathf.Approximately(promptCanvasGroup.alpha, targetAlpha))
        {
            promptCanvasGroup.alpha = Mathf.MoveTowards(promptCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        promptCanvasGroup.alpha = targetAlpha;
    }
}