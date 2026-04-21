using UnityEngine;
using System.Collections;

/// <summary>
/// Dedicated audio controller for the menu system. Handles sound effect playback 
/// and background music volume transitions via coroutines.
/// </summary>
public class MenuAudioManager : MonoBehaviour
{
    public static MenuAudioManager instance;

    [Header("Music Settings")]
    public AudioSource bgmSource;

    [Header("SFX Output")]
    [Tooltip("The AudioSource component used to play one-shot sound effects.")]
    public AudioSource sfxSource;

    [Header("Audio Clips")]
    public AudioClip clickSound;
    public AudioClip backSound;
    public AudioClip accordionSound;
    public AudioClip battleSound;
    public AudioClip battleSoundError;

    /// <summary>
    /// Initializes the singleton instance for menu-wide audio access.
    /// </summary>
    void Awake()
    {
        if (instance == null) instance = this;
    }

    /// <summary>
    /// Plays the standard UI interaction sound. Typically triggered by generic buttons (Start, Settings).
    /// </summary>
    public void PlayClick()
    {
        if (sfxSource != null && clickSound != null)
        {
            sfxSource.PlayOneShot(clickSound);
        }
    }

    /// <summary>
    /// Initiates a smooth volume reduction for the background music over a specified period.
    /// </summary>
    /// <param name="duration">Total time in seconds for the volume to reach zero.</param>
    public void FadeOutMusic(float duration)
    {
        if (bgmSource != null)
        {
            StartCoroutine(FadeMusicRoutine(duration));
        }
    }

    /// <summary>
    /// Internal coroutine using linear interpolation to decrease BGM volume relative to time.
    /// </summary>
    private IEnumerator FadeMusicRoutine(float duration)
    {
        float startVolume = bgmSource.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            // Linearly interpolate the volume from its current value to zero
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        // Finalize volume at absolute zero to ensure silence
        bgmSource.volume = 0f;
    }

    /// <summary>
    /// Plays the navigation sound specifically designated for back or return actions.
    /// </summary>
    public void PlayBackSound()
    {
        if (sfxSource != null && backSound != null)
        {
            sfxSource.PlayOneShot(backSound);
        }
    }

    /// <summary>
    /// Plays a stylized accordion audio clip used for specific menu transitions.
    /// </summary>
    public void PlayAccordionSound()
    {
        if (sfxSource != null && accordionSound != null)
        {
            sfxSource.PlayOneShot(accordionSound);
        }
    }

    /// <summary>
    /// Triggers the audio cue for a successful battle start or level entry.
    /// </summary>
    public void PlayBattleSound()
    {
        if (sfxSource != null && battleSound != null)
        {
            sfxSource.PlayOneShot(battleSound);
        }
    }

    /// <summary>
    /// Triggers the error feedback sound for invalid battle-related interactions (e.g., locked levels).
    /// </summary>
    public void PlayBattleSoundError()
    {
        if (sfxSource != null && battleSoundError != null)
        {
            sfxSource.PlayOneShot(battleSoundError);
        }
    }
}