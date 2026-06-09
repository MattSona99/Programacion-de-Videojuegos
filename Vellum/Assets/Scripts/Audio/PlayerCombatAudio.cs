using UnityEngine;

/// <summary>
/// Plays the Player's combat SFX by subscribing to the melee/shield C# events (which aren't
/// Inspector-wireable): a hit-landed cue and a block cue. Drop it in any scene with the Player
/// (Act_02, Act_03) and assign the references. Subscribes in OnEnable, unsubscribes in OnDisable.
/// </summary>
public class PlayerCombatAudio : MonoBehaviour
{
    [SerializeField] private PlayerMeleeAttack melee;
    [SerializeField] private FrontalShieldBlock shield;
    [Tooltip("Cue played when one of the Player's swings actually lands on a target.")]
    [SerializeField] private AudioCue hitCue;
    [Tooltip("Cue played when the Player's shield blocks an incoming hit.")]
    [SerializeField] private AudioCue blockCue;

    private void OnEnable()
    {
        if (melee != null) melee.HitLanded += OnHit;
        if (shield != null) shield.Blocked += OnBlock;
    }

    private void OnDisable()
    {
        if (melee != null) melee.HitLanded -= OnHit;
        if (shield != null) shield.Blocked -= OnBlock;
    }

    private void OnHit() { if (hitCue != null) hitCue.Play(); }
    private void OnBlock() { if (blockCue != null) blockCue.Play(); }
}
