using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages full-screen and element-specific transitions using shader-based cutoffs.
/// Handles level announcements with specific reveal and scale animations during scene loading.
/// </summary>
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager instance;

    [Header("UI References")]
    public Image transitionImage;
    public TextMeshProUGUI levelText;
    [Tooltip("The cutout-shader based overlay used to reveal or hide the level text.")]
    public Image textCover; 
    
    [Header("Timing Settings")]
    [Tooltip("Global speed for the screen transition. Lower values result in slower transitions.")]
    public float transitionSpeed = 0.5f;      
    [Tooltip("Speed of the shader-based bite/reveal effect for the text.")]
    public float textRevealSpeed = 0.5f; 
    [Tooltip("Speed of the subtle scaling animation while the text is visible.")]
    public float textScaleSpeed = 0.5f;     

    private RectTransform textRect;

    /// <summary>
    /// Initializes singleton instance and caches UI component references.
    /// </summary>
    void Awake()
    {
        instance = this;
        if (levelText != null) textRect = levelText.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Sets initial shader states based on the active scene to ensure proper screen visibility on start.
    /// </summary>
    void Start()
    {
        if (SceneManager.GetActiveScene().name != "01_MainMenu")
        {
            // Start from black in gameplay scenes
            if (transitionImage != null) transitionImage.material.SetFloat("_Cutoff", 0f);
        }
        else
        {
            // Start transparent in the main menu
            if (transitionImage != null) transitionImage.material.SetFloat("_Cutoff", 1.05f);
        }

        if (levelText != null)
        {
            levelText.gameObject.SetActive(false);
            if (textCover != null) textCover.gameObject.SetActive(false);
            textRect.anchoredPosition = Vector2.zero;
        }
    }

    public void FadeOut() => StartCoroutine(AnimateTransition(1.05f, 0f));
    public void FadeIn() => StartCoroutine(AnimateTransition(0f, 1.05f));

    public IEnumerator FadeOutRoutine() => AnimateTransition(1.05f, 0f);
    public IEnumerator FadeInRoutine() => AnimateTransition(0f, 1.05f);

    /// <summary>
    /// Interpolates the _Cutoff property of the transition material to create a visual wipe effect.
    /// Uses unscaledDeltaTime to ensure transitions work even while the game is paused.
    /// </summary>
    private IEnumerator AnimateTransition(float start, float end)
    {
        Material mat = transitionImage.material;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * transitionSpeed;
            mat.SetFloat("_Cutoff", Mathf.Lerp(start, end, t));
            yield return null;
        }
        mat.SetFloat("_Cutoff", end);
    }

    /// <summary>
    /// Orchestrates the level text animation sequence: bite-reveal, subtle scale-up, and bite-hide.
    /// </summary>
    /// <param name="levelNumber">The current level index to display.</param>
    public IEnumerator AnimateLevelTextRoutine(int levelNumber)
    {
        if (levelText == null || textCover == null) yield break; 

        levelText.text = "LEVEL " + levelNumber;
        levelText.gameObject.SetActive(true);
        textCover.gameObject.SetActive(true);
        
        textRect.localScale = Vector3.one;
        Material coverMat = textCover.material;

        // Phase 1: Bite Reveal (Transitioning shader cutoff from 0 to 1)
        float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * textRevealSpeed;
            coverMat.SetFloat("_Cutoff", Mathf.Lerp(0f, 1.05f, t));
            yield return null;
        }

        // Phase 2: Pause and Subtle Scaling (Enhancing visual impact)
        t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * textScaleSpeed; 
            textRect.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.1f, 1.1f, 1.1f), t);
            yield return null;
        }

        // Phase 3: Bite Hide (Transitioning shader cutoff back to 0)
        t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * textRevealSpeed;
            coverMat.SetFloat("_Cutoff", Mathf.Lerp(1.05f, 0f, t));
            yield return null;
        }

        levelText.gameObject.SetActive(false);
        textCover.gameObject.SetActive(false);
    }
}