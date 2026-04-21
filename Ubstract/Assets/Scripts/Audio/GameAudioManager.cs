using UnityEngine;
using System.Collections;

/// <summary>
/// Persistent singleton manager handling background music transitions.
/// Implements a dual-source crossfade system to smoothly transition between audio clips
/// while respecting global volume settings and unscaled time.
/// </summary>
public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager instance;

    [Header("Dual Source Setup")]
    public AudioSource sourceA;
    public AudioSource sourceB;

    [Header("Transition Settings")]
    [Tooltip("Duration in seconds for the crossfade transition between two music tracks.")]
    public float fadeDuration = 1.5f;

    private bool isSourceAActive = true;

    /// <summary>
    /// Initializes the singleton instance and ensures the object persists across scene loads.
    /// </summary>
    void Awake()
    {
        instance = this;
        
        // Ensure the audio manager remains active during scene transitions to prevent music cutouts
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Initiates a crossfade transition to a new music track. 
    /// Validates if the requested clip is already playing to prevent redundant restarts.
    /// </summary>
    /// <param name="newClip">The AudioClip to transition to.</param>
    public void ChangeMusic(AudioClip newClip)
    {
        if (newClip == null) return;
        
        // Redundancy check: Do not restart if the active source is already playing the target clip
        if (isSourceAActive && sourceA.clip == newClip) return;
        if (!isSourceAActive && sourceB.clip == newClip) return;

        StartCoroutine(FadeMusic(newClip));
    }

    /// <summary>
    /// Immediately halts all active music tracks and cancels any ongoing crossfade coroutines.
    /// </summary>
    public void StopAllMusic()
    {
        StopAllCoroutines();
        sourceA.Stop();
        sourceB.Stop();
    }

    /// <summary>
    /// Coroutine that manages the mathematical crossfade between two audio sources.
    /// Uses unscaledDeltaTime to ensure transitions occur correctly even during game pauses.
    /// </summary>
    /// <param name="newClip">The target audio clip for the incoming source.</param>
    private IEnumerator FadeMusic(AudioClip newClip)
    {
        // Identify which source is currently playing and which one will be the new active source
        AudioSource activeSource = isSourceAActive ? sourceA : sourceB;
        AudioSource newSource = isSourceAActive ? sourceB : sourceA;

        // Prepare the new source
        newSource.clip = newClip;
        newSource.Play();
        newSource.volume = 0;

        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float v = t / fadeDuration;
            
            // Apply linear interpolation to volumes, scaled by the GameManager's master volume setting
            newSource.volume = v * GameManager.instance.globalVolume;
            activeSource.volume = (1 - v) * GameManager.instance.globalVolume;
            
            yield return null;
        }

        // Finalize the transition by stopping the old source and flipping the active flag
        activeSource.Stop();
        isSourceAActive = !isSourceAActive;
    }
}