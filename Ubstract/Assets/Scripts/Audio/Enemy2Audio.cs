using UnityEngine;

/// <summary>
/// Manages the audio feedback system for the second Boss type.
/// Handles sound synchronization for complex combo sequences, environmental impacts,
/// and phase-specific triggers (Crazy Mode) using Animation Events and direct code calls.
/// </summary>
public class Enemy2Audio : MonoBehaviour
{
    [Header("Audio Component")]
    [Tooltip("The primary AudioSource used for enemy sound effects.")]
    public AudioSource audioSource;

    [Header("Movement & Entry SFX")]
    public AudioClip footstepSound_1;
    public AudioClip footstepSound_2;
    public AudioClip spawnSound;

    [Header("Melee Combo SFX")]
    public AudioClip lightSwingSound_1;
    public AudioClip lightSwingSound_2;
    public AudioClip heavySwingSound_1;
    public AudioClip heavySwingSound_2;
    [Tooltip("Triggered during the unblockable ground slam attack.")]
    public AudioClip earthquakeSlamSound;

    [Header("Phase Transition: Crazy Mode SFX")]
    [Tooltip("Explosive sound played during the initial knockback phase.")]
    public AudioClip crazyActivationExplosion;
    [Tooltip("Rapid attack sound played during the repetitive crazy loop.")]
    public AudioClip crazyAttackSound;
    [Tooltip("Feedback sound played when the player successfully shatters the boss's shield.")]
    public AudioClip shieldBrokenSound;

    /// <summary>
    /// Executes a sound effect with optional pitch randomization and volume control.
    /// Uses PlayOneShot to allow multiple sound layers to coexist without clipping.
    /// </summary>
    /// <param name="clip">The target AudioClip.</param>
    /// <param name="pitchRange">The variance applied to the base pitch (1.0).</param>
    /// <param name="volume">The local volume multiplier for this specific play instance.</param>
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
    // ANIMATION EVENTS (Synchronized with Combat & Locomotion)
    // ========================================================================

    /// <summary>Triggered during the enemy's initial spawn or entry animation.</summary>
    public void AnimEvent_Spawn() => PlaySound(spawnSound);

    /// <summary>Triggered when the boss's first foot makes contact with the ground.</summary>
    public void AnimEvent_Footstep_1() => PlaySound(footstepSound_1, 0.7f);
    
    /// <summary>Triggered when the boss's second foot makes contact with the ground.</summary>
    public void AnimEvent_Footstep_2() => PlaySound(footstepSound_2, 0.7f);

    /// <summary>Triggered during the swing frames of light melee combo attacks.</summary>
    public void AnimEvent_AudioLightSwing_1() => PlaySound(lightSwingSound_1, 0.1f);
    public void AnimEvent_AudioLightSwing_2() => PlaySound(lightSwingSound_2, 0.1f);

    /// <summary>Triggered during the swing frames of high-damage heavy combo attacks.</summary>
    public void AnimEvent_AudioHeavySwing_1() => PlaySound(heavySwingSound_1, 0.05f);
    public void AnimEvent_AudioHeavySwing_2() => PlaySound(heavySwingSound_2, 0.05f);
    
    /// <summary>Synchronized with the ground impact frame of the unblockable earthquake strike.</summary>
    public void AnimEvent_AudioEarthquake() => PlaySound(earthquakeSlamSound, 0f);

    // ========================================================================
    // EXTERNAL AI TRIGGERED METHODS
    // ========================================================================

    /// <summary>Invoked by the AI Controller when entering the Crazy Mode state.</summary>
    public void PlayCrazyActivation() => PlaySound(crazyActivationExplosion, 0f);

    /// <summary>Invoked iteratively by the AI during the Crazy Mode attack loop.</summary>
    public void PlayCrazyAttack() => PlaySound(crazyAttackSound, 0.1f);

    /// <summary>Invoked when the shield health reaches zero, signaling a successful phase end.</summary>
    public void PlayShieldBroken() => PlaySound(shieldBrokenSound, 0f);
}