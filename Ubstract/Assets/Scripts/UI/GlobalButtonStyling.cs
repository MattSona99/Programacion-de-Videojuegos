using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Applies a dynamic animated hover effect to all buttons in the scene.
/// Automatically extracts color palettes from button sprites to maintain visual consistency.
/// </summary>
public class GlobalButtonStyling : MonoBehaviour
{
    public static GlobalButtonStyling Instance;

    [Header("Color Extraction Coordinates")]
    [Tooltip("Normalized UV coordinates (0 to 1) for the highlight/hover color. (0.5, 0.95) targets the top center.")]
    public Vector2 hoverPixelUV = new Vector2(0.5f, 0.95f);
    
    [Tooltip("Normalized UV coordinates (0 to 1) for the shadow/pressed color. (0.02, 0.5) targets the left edge.")]
    public Vector2 pressedPixelUV = new Vector2(0.02f, 0.5f);

    [Header("Animation Profiles")]
    public float animationSpeed = 15f;
    
    [Tooltip("Alpha transparency for the hover overlay (0 = transparent, 1 = opaque).")]
    public float hoverAlpha = 0.5f;
    [Tooltip("Alpha transparency for the pressed overlay (0 = transparent, 1 = opaque).")]
    public float pressedAlpha = 0.8f;

    /// <summary>
    /// Establishes the singleton pattern for global access to button styling logic.
    /// </summary>
    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Scans the scene for all Button components (including inactive ones) 
    /// and injects the custom animated hover logic.
    /// </summary>
    void Start()
    {
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button btn in allButtons)
        {
            ApplyAnimatedHoverLogic(btn);
        }
    }

    /// <summary>
    /// Refactors a standard Button to support multi-layered graphics (Pixel Art + Hover Fill)
    /// and attaches a dedicated animator component.
    /// </summary>
    public void ApplyAnimatedHoverLogic(Button btn)
    {
        if (btn.GetComponent<HoverAnimator>() != null) return;

        Image btnImage = btn.GetComponent<Image>();
        if (btnImage == null || btnImage.sprite == null) return;

        Sprite originalPixelArt = btnImage.sprite;
        Color extractedHover = GetColorFromSprite(originalPixelArt, hoverPixelUV, hoverAlpha);
        Color extractedPressed = GetColorFromSprite(originalPixelArt, pressedPixelUV, pressedAlpha);

        // Cache 9-slicing data to replicate it on secondary layers
        Image.Type originalType = btnImage.type;
        float originalMultiplier = btnImage.pixelsPerUnitMultiplier;

        // Disable standard Unity transitions and hide the original root image
        btn.transition = Selectable.Transition.None;
        btnImage.sprite = null;
        btnImage.color = new Color(1f, 1f, 1f, 0f);

        // Layer 1: The Animated Background Fill
        GameObject fillObject = new GameObject("HoverFill_Animated");
        fillObject.transform.SetParent(btn.transform, false);

        Image fillImage = fillObject.AddComponent<Image>();
        fillImage.color = extractedHover;
        fillImage.raycastTarget = false;
        
        // Replicate Sliced image properties for the procedural fill
        if (originalType == Image.Type.Sliced)
        {
            fillImage.sprite = originalPixelArt; 
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = originalMultiplier;
        }

        RectTransform fillRt = fillObject.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fillRt.localScale = Vector3.zero; 

        // Layer 2: The Original Pixel Art Graphic (Topmost layer)
        GameObject artObject = new GameObject("PixelArt_Graphic");
        artObject.transform.SetParent(btn.transform, false);

        Image artImage = artObject.AddComponent<Image>();
        artImage.sprite = originalPixelArt;
        artImage.type = originalType;
        artImage.pixelsPerUnitMultiplier = originalMultiplier;
        artImage.raycastTarget = false;

        RectTransform artRt = artObject.GetComponent<RectTransform>();
        artRt.anchorMin = Vector2.zero;
        artRt.anchorMax = Vector2.one;
        artRt.offsetMin = Vector2.zero;
        artRt.offsetMax = Vector2.zero;

        // Inject the animation controller to handle input events
        HoverAnimator animator = btn.gameObject.AddComponent<HoverAnimator>();
        animator.Setup(fillRt, fillImage, extractedHover, extractedPressed, animationSpeed);
    }

    /// <summary>
    /// Retrieves specific color data from a texture based on UV mapping.
    /// Requires the texture to have 'Read/Write' enabled in Import Settings.
    /// </summary>
    private Color GetColorFromSprite(Sprite sprite, Vector2 uv, float targetAlpha)
    {
        if (sprite == null || sprite.texture == null) return Color.white;
        
        try
        {
            // Map 0-1 UV space to actual pixel dimensions within the sprite rect
            int pixelX = Mathf.FloorToInt(sprite.rect.x + (uv.x * sprite.rect.width));
            int pixelY = Mathf.FloorToInt(sprite.rect.y + (uv.y * sprite.rect.height));
            
            Color pixelColor = sprite.texture.GetPixel(pixelX, pixelY);
            pixelColor.a = targetAlpha; 
            return pixelColor;
        }
        catch (UnityException)
        {
            Debug.LogError($"[GlobalButtonStyling] Read/Write must be enabled on: {sprite.name}!");
            return new Color(1f, 0f, 1f, targetAlpha); // Magenta fallback for errors
        }
    }
}

/// <summary>
/// Handles pointer interaction events to drive the scaling and color transitions of the button layers.
/// </summary>
public class HoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform targetTransform;
    private Image targetImage;
    private Color colorHover;
    private Color colorPress;
    private float speed;

    private Coroutine scaleCoroutine;

    /// <summary>
    /// Configures the animation references and color palette targets.
    /// </summary>
    public void Setup(RectTransform tTransform, Image tImage, Color hover, Color press, float animSpeed)
    {
        targetTransform = tTransform;
        targetImage = tImage;
        colorHover = hover;
        colorPress = press;
        speed = animSpeed;
    }

    /// <summary>
    /// Expands the hover fill layer when the mouse enters the button area.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetImage.color = colorHover;
        AnimateToScale(Vector3.one);
    }

    /// <summary>
    /// Shrinks the hover fill layer when the mouse leaves.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateToScale(Vector3.zero);
    }

    /// <summary>
    /// Updates the fill color to the 'Pressed' state during mouse clicks.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        targetImage.color = colorPress;
    }

    /// <summary>
    /// Reverts the fill color to 'Hover' when the click is released.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        targetImage.color = colorHover;
    }

    /// <summary>
    /// Ensures coroutines are cleaned up and states reset when the UI element is disabled.
    /// </summary>
    void OnDisable()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
        if (targetTransform != null)
        {
            targetTransform.localScale = Vector3.zero;
        }
    }

    /// <summary>
    /// Safely starts the scaling animation coroutine.
    /// </summary>
    private void AnimateToScale(Vector3 targetScale)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        
        if (gameObject.activeInHierarchy)
        {
            scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
        }
        else
        {
            targetTransform.localScale = targetScale;
        }
    }

    /// <summary>
    /// Performs frame-by-frame interpolation of the local scale using unscaled delta time 
    /// to remain functional even when the game is paused.
    /// </summary>
    private IEnumerator ScaleRoutine(Vector3 target)
    {
        while (Vector3.Distance(targetTransform.localScale, target) > 0.01f)
        {
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, target, Time.unscaledDeltaTime * speed);
            yield return null;
        }
        targetTransform.localScale = target;
    }
}