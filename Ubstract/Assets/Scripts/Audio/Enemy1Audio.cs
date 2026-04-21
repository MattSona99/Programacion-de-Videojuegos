using UnityEngine;

/// <summary>
/// Manages the audio feedback system for the basic Enemy AI.
/// Synchronizes sound effects with specific animation frames using Animation Events,
/// featuring pitch randomization to prevent auditory fatigue.
/// </summary>
public class Enemy1Audio : MonoBehaviour
{
    [Header("Audio Component")]
    [Tooltip("The AudioSource component used to play enemy sound effects.")]
    public AudioSource audioSource;

    [Header("Locomotion SFX")]
    public AudioClip footstepSound_1;
    public AudioClip footstepSound_2;

    [Header("Combat SFX")]
    public AudioClip gunShotSound;
    [Tooltip("Sound played during the reload sequence, coinciding with the invulnerability state.")]
    public AudioClip reloadSound; 

    [Header("Feedback SFX")]
    [Tooltip("Audio cue triggered when the player strikes the enemy during its invulnerable reload phase.")]
    public AudioClip hitShieldSound; 

    /// <summary>
    /// Executes a sound effect with a randomized pitch to provide organic variation.
    /// Uses PlayOneShot to allow multiple sound effects to overlap without interruption.
    /// </summary>
    /// <param name="clip">The target AudioClip to play.</param>
    /// <param name="pitchRange">The maximum deviation from the base pitch (1.0).</param>
    public void PlaySound(AudioClip clip, float pitchRange = 0.1f)
    {
        if (clip != null && audioSource != null)
        {
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            // Apply slight pitch variation for more natural-sounding repetitive effects
            audioSource.pitch = Random.Range(1f - pitchRange, 1f + pitchRange);
            audioSource.PlayOneShot(clip, sfxVol);
        }
    }

    // ========================================================================
    // ANIMATION EVENTS (Synchronized with Pixel-Art Timelines)
    // ========================================================================

    /// <summary>Triggered via Animation Event when the enemy's first foot contacts the ground.</summary>
    public void AnimEvent_EnemyFootstep_1()
    {
        PlaySound(footstepSound_1, 0.15f);
    }

    /// <summary>Triggered via Animation Event when the enemy's second foot contacts the ground.</summary>
    public void AnimEvent_EnemyFootstep_2()
    {
        PlaySound(footstepSound_2, 0.15f);
    }

    /// <summary>Triggered during the active frames of the shooting animation.</summary>
    public void AnimEvent_EnemyShoot()
    {
        PlaySound(gunShotSound, 0.05f);
    }

    /// <summary>Triggered at the start of the reload sequence, alerting the player to the invulnerability window.</summary>
    public void AnimEvent_StartReload()
    {
        PlaySound(reloadSound, 0f);
    }
}