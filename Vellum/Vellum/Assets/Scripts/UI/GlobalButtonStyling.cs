using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Applies a dynamic animated hover and scale effect to all buttons in the scene.
/// Automatically extracts color palettes from button sprites to maintain visual consistency.
/// </summary>
public class GlobalButtonStyling : MonoBehaviour
{
    public static GlobalButtonStyling Instance;

    [Header("Color Extraction Coordinates")]
    [Tooltip("Normalized UV coordinates (0 to 1) for the highlight/hover color.")]
    public Vector2 hoverPixelUV = new Vector2(0.5f, 0.95f);
    
    [Tooltip("Normalized UV coordinates (0 to 1) for the shadow/pressed color.")]
    public Vector2 pressedPixelUV = new Vector2(0.02f, 0.5f);

    [Header("Animation Profiles")]
    [Tooltip("Higher values make the animation faster and snappier.")]
    public float animationSpeed = 25f; // Increased for a faster, fluid pop effect
    
    [Tooltip("Distance in pixels between the fill animation and the outer edge of the button.")]
    public float fillPadding = 6f; 
    
    [Tooltip("Target scale multiplier for the entire button when hovered (e.g., 1.05 = 5% larger).")]
    public float buttonHoverScale = 1.05f;
    
    [Tooltip("Target scale multiplier for the entire button when pressed (e.g., 0.95 = 5% smaller).")]
    public float buttonPressedScale = 0.95f;

    [Tooltip("Alpha transparency for the hover overlay (0 = transparent, 1 = opaque).")]
    public float hoverAlpha = 0.5f;
    
    [Tooltip("Alpha transparency for the pressed overlay (0 = transparent, 1 = opaque).")]
    public float pressedAlpha = 0.8f;

    /// <summary>
    /// Establishes the singleton pattern for global access.
    /// </summary>
    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Scans the scene for all Button components and injects the custom animated hover logic.
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
    /// Refactors a standard Button to support multi-layered graphics and attaches a dedicated animator.
    /// </summary>
    public void ApplyAnimatedHoverLogic(Button btn)
    {
        if (btn.GetComponent<HoverAnimator>() != null) return;

        ButtonStyleOverride styleOverride = btn.GetComponent<ButtonStyleOverride>();
        if (styleOverride != null)
        {
            if (styleOverride.keepHoverScale)
            {
                ApplyScaleOnlyHover(btn, styleOverride);
            }
            return;
        }

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
        
        if (originalType == Image.Type.Sliced)
        {
            fillImage.sprite = originalPixelArt; 
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = originalMultiplier;
        }

        RectTransform fillRt = fillObject.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        
        // Apply padding offsets to prevent the fill from overlapping the button borders
        fillRt.offsetMin = new Vector2(fillPadding, fillPadding); 
        fillRt.offsetMax = new Vector2(-fillPadding, -fillPadding); 
        
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

        // Inject the animation controller to handle input events and pass scale multipliers
        RectTransform rootRt = btn.GetComponent<RectTransform>();
        HoverAnimator animator = btn.gameObject.AddComponent<HoverAnimator>();
        animator.Setup(fillRt, rootRt, fillImage, extractedHover, extractedPressed, animationSpeed, buttonHoverScale, buttonPressedScale);
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
            return new Color(1f, 0f, 1f, targetAlpha); 
        }
    }

    /// <summary>
    /// Applies only the scale-on-hover effect, without the colored fill rectangle.
    /// Used for buttons that have their own custom artwork (portraits, icons, etc.).
    /// </summary>
    private void ApplyScaleOnlyHover(Button btn, ButtonStyleOverride config)
    {
        btn.transition = Selectable.Transition.None;

        RectTransform rootRt = btn.GetComponent<RectTransform>();
        SimpleHoverScale scaler = btn.gameObject.AddComponent<SimpleHoverScale>();
        scaler.Setup(rootRt, config.hoverScale, config.animationSpeed);
    }
}

/// <summary>
/// Handles pointer interaction events to drive the scaling of both the inner fill layer and the root button.
/// </summary>
public class HoverAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform fillTransform;
    private RectTransform rootTransform;
    private Image fillImage;
    private Color colorHover;
    private Color colorPress;
    private float speed;
    private float hoverScaleMultiplier;
    private float pressScaleMultiplier;

    // Caches the original local scale defined in the Unity Inspector
    private Vector3 initialRootScale; 

    private Coroutine scaleCoroutine;

    /// <summary>
    /// Configures the animation references, target scales, and color palette.
    /// Memorizes the base scale of the button.
    /// </summary>
    public void Setup(RectTransform fTransform, RectTransform rTransform, Image fImage, Color hover, Color press, float animSpeed, float hScale, float pScale)
    {
        fillTransform = fTransform;
        rootTransform = rTransform;
        fillImage = fImage;
        colorHover = hover;
        colorPress = press;
        speed = animSpeed;
        hoverScaleMultiplier = hScale;
        pressScaleMultiplier = pScale;

        // Store the original scale assigned in the editor (e.g., 1.5, 1.5, 1.5)
        initialRootScale = rootTransform.localScale;
    }

    /// <summary>
    /// Triggers the hover fill expansion and scales up the root button based on its initial scale.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        fillImage.color = colorHover;
        AnimateTransforms(Vector3.one, initialRootScale * hoverScaleMultiplier);
    }

    /// <summary>
    /// Triggers the hover fill collapse and returns the root button to its original scale.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTransforms(Vector3.zero, initialRootScale);
    }

    /// <summary>
    /// Updates the fill color to 'Pressed' state and slightly shrinks the root button for impact.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        fillImage.color = colorPress;
        AnimateTransforms(Vector3.one, initialRootScale * pressScaleMultiplier);
    }

    /// <summary>
    /// Reverts the fill color to 'Hover' and scales the root button back to the hover scale.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        fillImage.color = colorHover;
        
        // Ensures the button returns to hover scale only if the cursor is still over it
        if (RectTransformUtility.RectangleContainsScreenPoint(rootTransform, eventData.position, eventData.pressEventCamera))
        {
            AnimateTransforms(Vector3.one, initialRootScale * hoverScaleMultiplier);
        }
        else
        {
            AnimateTransforms(Vector3.zero, initialRootScale);
        }
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
        if (fillTransform != null) fillTransform.localScale = Vector3.zero;
        if (rootTransform != null) rootTransform.localScale = initialRootScale; // Restores the original base scale
    }

    /// <summary>
    /// Safely starts the dual-scaling animation coroutine.
    /// </summary>
    private void AnimateTransforms(Vector3 targetFillScale, Vector3 targetRootScale)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        
        if (gameObject.activeInHierarchy)
        {
            scaleCoroutine = StartCoroutine(ScaleRoutine(targetFillScale, targetRootScale));
        }
        else
        {
            fillTransform.localScale = targetFillScale;
            rootTransform.localScale = targetRootScale;
        }
    }

    /// <summary>
    /// Performs frame-by-frame interpolation of both the fill scale and root scale using unscaled delta time.
    /// </summary>
    private IEnumerator ScaleRoutine(Vector3 targetFill, Vector3 targetRoot)
    {
        while (Vector3.Distance(fillTransform.localScale, targetFill) > 0.001f || 
               Vector3.Distance(rootTransform.localScale, targetRoot) > 0.001f)
        {
            fillTransform.localScale = Vector3.Lerp(fillTransform.localScale, targetFill, Time.unscaledDeltaTime * speed);
            rootTransform.localScale = Vector3.Lerp(rootTransform.localScale, targetRoot, Time.unscaledDeltaTime * speed);
            yield return null;
        }
        
        fillTransform.localScale = targetFill;
        rootTransform.localScale = targetRoot;
    }
}

/// <summary>
/// Lightweight hover animator: only scales the button up on hover, no color fill.
/// </summary>
public class SimpleHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rootTransform;
    private Vector3 initialScale;
    private float hoverMultiplier;
    private float speed;
    private Coroutine scaleCoroutine;

    public void Setup(RectTransform rt, float hover, float animSpeed)
    {
        rootTransform = rt;
        hoverMultiplier = hover;
        speed = animSpeed;
        initialScale = rootTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => AnimateTo(initialScale * hoverMultiplier);
    public void OnPointerExit(PointerEventData eventData) => AnimateTo(initialScale);
    public void OnPointerDown(PointerEventData eventData) => AnimateTo(initialScale * 0.97f);
    public void OnPointerUp(PointerEventData eventData)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(rootTransform, eventData.position, eventData.pressEventCamera))
            AnimateTo(initialScale * hoverMultiplier);
        else
            AnimateTo(initialScale);
    }

    void OnDisable()
    {
        if (scaleCoroutine != null) { StopCoroutine(scaleCoroutine); scaleCoroutine = null; }
        if (rootTransform != null) rootTransform.localScale = initialScale;
    }

    private void AnimateTo(Vector3 target)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        if (gameObject.activeInHierarchy)
            scaleCoroutine = StartCoroutine(ScaleRoutine(target));
        else
            rootTransform.localScale = target;
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        while (Vector3.Distance(rootTransform.localScale, target) > 0.001f)
        {
            rootTransform.localScale = Vector3.Lerp(rootTransform.localScale, target, Time.unscaledDeltaTime * speed);
            yield return null;
        }
        rootTransform.localScale = target;
    }
}