using UnityEngine;

/// <summary>
/// Manages the audio feedback system for the third Boss type.
/// Synchronizes complex aerial maneuvers, wall impacts, and vertical phase 
/// transitions with specific animation frames via Animation Events.
/// </summary>
public class Enemy3Audio : MonoBehaviour
{
    [Header("Audio Component")]
    [Tooltip("The primary AudioSource for this enemy's sound effects.")]
    public AudioSource audioSource;

    [Header("Locomotion SFX")]
    public AudioClip footstepSound_1;
    public AudioClip footstepSound_2;
    [Tooltip("Sound played during jumps or when launching off a wall.")]
    public AudioClip jumpTakeoffSound;

    [Header("Combat SFX (Attacks 1 & 2)")]
    [Tooltip("Triggered when the boss makes contact with the arena boundary wall.")]
    public AudioClip wallHitSound;     
    [Tooltip("Whoosh sound effect played during high-velocity dives.")]
    public AudioClip diveWhoshSound;   
    [Tooltip("High-impact sound played when the boss slams into the ground.")]
    public AudioClip impactSlamSound;  

    [Header("Phase Transition SFX")]
    [Tooltip("Audio played while the boss ascends to the ceiling.")]
    public AudioClip ascendSound;      
    [Tooltip("Ambient hiss or static played while the boss is attached to the ceiling.")]
    public AudioClip ceilingHiss;      
    [Tooltip("Audio played during the final drop back to the arena floor.")]
    public AudioClip descendSound;     

    /// <summary>
    /// Plays a sound effect with pitch randomization to add variety to repetitive actions.
    /// Uses PlayOneShot to ensure sounds can overlap without cutting each other off.
    /// </summary>
    /// <param name="clip">The target AudioClip.</param>
    /// <param name="pitchRange">The maximum pitch deviation (base is 1.0).</param>
    /// <param name="volume">Local volume multiplier for the clip.</param>
    public void PlaySound(AudioClip clip, float pitchRange = 0.1f, float volume = 1f)
    {
        if (clip != null && audioSource != null)
        {
            float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
            audioSource.pitch = Random.Range(1f - pitchRange, 1f + pitchRange);
            audioSource.PlayOneShot(clip, volume);
        }
    }

    // ========================================================================
    // ANIMATION EVENTS (Synchronized with Aerial & Phase Logic)
    // ========================================================================

    /// <summary>Triggered when the boss's first foot contacts the ground during walking.</summary>
    public void AnimEvent_Footstep_1() => PlaySound(footstepSound_1, 0.7f);
    
    /// <summary>Triggered when the boss's second foot contacts the ground during walking.</summary>
    public void AnimEvent_Footstep_2() => PlaySound(footstepSound_2, 0.7f);

    /// <summary>Triggered at the initial frame of jump-based attacks (Attack1 & Attack2).</summary>
    public void AnimEvent_AudioJump() => PlaySound(jumpTakeoffSound, 0.1f);

    /// <summary>Synchronized with the frame where the boss hits the wall boundary in Attack 2.</summary>
    public void AnimEvent_AudioWallHit() => PlaySound(wallHitSound, 0.05f);

    /// <summary>Triggered at the start of the Wall Strike picchiata (high-speed dive).</summary>
    public void AnimEvent_AudioDive() => PlaySound(diveWhoshSound, 0.1f);

    /// <summary>Synchronized with the ground impact frame of any slam attack.</summary>
    public void AnimEvent_AudioImpact() => PlaySound(impactSlamSound, 0f);

    /// <summary>Ambient loop or cue triggered while the boss is positioned on the ceiling.</summary>
    public void AnimEvent_AudioCeiling() => PlaySound(ceilingHiss, 0f);
}