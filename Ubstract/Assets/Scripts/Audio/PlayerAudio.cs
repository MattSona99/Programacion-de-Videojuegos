using UnityEngine;

/// <summary>
/// Manages the player's audio feedback system. 
/// Orchestrates sound effect playback via Animation Events to ensure 
/// perfect synchronization with pixel-art movement and combat frames.
/// </summary>
public class PlayerAudio : MonoBehaviour
{
    [Header("Audio Component")]
    [Tooltip("The primary AudioSource used to play player-related sound effects.")]
    public AudioSource audioSource;

    [Header("Movement SFX")]
    public AudioClip footstep_1;
    public AudioClip footstep_2;
    public AudioClip footstep_3;
    public AudioClip footstep_4;
    public AudioClip jumpSound;

    [Header("Combat SFX")]
    public AudioClip punch1Sound;
    public AudioClip punch2Sound;

    [Header("Defense SFX")]
    public AudioClip parrySound;
    public AudioClip perfectParrySound;

    [Header("Consumable SFX")]
    public AudioClip potionSound;

    /// <summary>
    /// Executes a sound effect using PlayOneShot to prevent overlapping 
    /// sounds from cutting each other off.
    /// </summary>
    /// <param name="clip">The target AudioClip to be played.</param>
    public void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            audioSource.PlayOneShot(clip, sfxVol);
        }
    }

    // ========================================================================
    // ANIMATION EVENTS (Combat & Movement Synchronization)
    // ========================================================================

    /// <summary>Triggered via Animation Event when the player's foot makes contact with the ground.</summary>
    public void AnimEvent_WalkStep_1() => PlaySound(footstep_1);
    public void AnimEvent_WalkStep_2() => PlaySound(footstep_2);
    public void AnimEvent_WalkStep_3() => PlaySound(footstep_3);
    public void AnimEvent_WalkStep_4() => PlaySound(footstep_4);

    /// <summary>Triggered via Animation Event during the active frames of the first punch combo.</summary>
    public void AnimEvent_Punch1() => PlaySound(punch1Sound);
    
    /// <summary>Triggered via Animation Event during the active frames of the second punch combo.</summary>
    public void AnimEvent_Punch2() => PlaySound(punch2Sound);

    /// <summary>Initiates the audio feedback for consuming a health potion or picking up an item.</summary>
    public void PlayPotionSound() => PlaySound(potionSound);
}