using UnityEngine;

/// <summary>
/// Reusable SFX trigger. Holds one or more clips and plays one through <see cref="SoundManager"/>.
/// Wire <see cref="Play"/> to ANY UnityEvent (interactions, statue, tiles, death, phase, …), or call
/// <see cref="Play"/> / <see cref="PlayIndex"/> from an Animation Event (put this on the mesh that owns
/// the Animator so the event can find it). The fastest way to attach a sound anywhere.
/// </summary>
public class AudioCue : MonoBehaviour
{
    [Tooltip("One or more clips. With 'randomize' on, a random one plays each time (variation).")]
    [SerializeField] private AudioClip[] clips;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;
    [Tooltip("Pick a random clip on each Play (otherwise always the first).")]
    [SerializeField] private bool randomize = true;

    /// <summary>Plays a clip (random if <c>randomize</c>). Wire to UnityEvents / Animation Events.</summary>
    public void Play()
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = randomize ? clips[Random.Range(0, clips.Length)] : clips[0];
        PlayClip(clip);
    }

    /// <summary>Plays a specific clip by index (for Animation Events that pass an int, e.g. footstep=0 / attack=1).</summary>
    public void PlayIndex(int index)
    {
        if (clips == null || index < 0 || index >= clips.Length) return;
        PlayClip(clips[index]);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(clip, volume);
        else AudioSource.PlayClipAtPoint(clip, transform.position, volume); // fallback if no SoundManager
    }
}
