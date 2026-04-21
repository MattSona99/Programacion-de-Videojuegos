using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Audio;

/// <summary>
/// Handles the initial application boot sequence, managing splash screen animations 
/// and scene transitions before delegating control to the main menu.
/// </summary>
public class BootManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Reference to the splash screen or studio logo UI Image.")]
    public Image loadingImage;
    
    [Tooltip("Reference to the full-screen overlay Image used for fade transitions.")]
    public Image blackScreen;

    [Header("Audio")]
    public AudioMixer mainMixer;

    [Header("Transition Timings")]
    [Tooltip("Total duration (in seconds) for the logo presentation sequence.")]
    public float bootTime = 5f;
    
    [Tooltip("Duration (in seconds) of the fade-to-black sequence before loading the next scene.")]
    public float transitionTime = 1f;

    [Header("Animation Curves")]
    [Tooltip("Easing curve applied to the logo's alpha channel during the boot sequence.")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// Initializes the boot sequence coroutine immediately upon scene load.
    /// </summary>
    void Start()
    {
        StartCoroutine(BootSequence());
    }

    /// <summary>
    /// Processes player preferences and applies them to the audio system.
    /// </summary>
    void Awake()
    {
        float music = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 1f);
        bool isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        AudioListener.volume = isMuted ? 0f : 1f;

        if (mainMixer != null)
        {
            mainMixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Max(0.0001f, music)) * 20f);
            mainMixer.SetFloat("SFXVol", Mathf.Log10(Mathf.Max(0.0001f, sfx)) * 20f);
        }
    }

    /// <summary>
    /// Coroutine driving the visual boot sequence: fade-in logo, fade-to-black, and scene load.
    /// </summary>
    IEnumerator BootSequence()
    {
        // Force initialize UI elements to fully transparent to prevent frame-1 popping
        Color logoColor = loadingImage.color;
        logoColor.a = 0f;
        loadingImage.color = logoColor;

        Color blackColor = blackScreen.color;
        blackColor.a = 0f;
        blackScreen.color = blackColor;

        float timer = 0f;
        
        // Phase 1: Animate logo alpha using the defined easing curve
        while (timer < bootTime)
        {
            timer += Time.deltaTime;
            
            float normalizedTime = timer / bootTime;
            float smoothedAlpha = fadeCurve.Evaluate(normalizedTime);
            
            logoColor.a = smoothedAlpha;
            loadingImage.color = logoColor;
            
            // Yield execution to synchronize with Unity's render loop
            yield return null;
        }

        timer = 0f;
        
        // Phase 2: Linear interpolation to fade the screen to black
        while (timer < transitionTime)
        {
            timer += Time.deltaTime;
            
            blackColor.a = timer / transitionTime; 
            blackScreen.color = blackColor;
            
            yield return null;
        }

        // Phase 3: Transition to the main menu scene
        SceneManager.LoadScene("01_MainMenu");
    }
}