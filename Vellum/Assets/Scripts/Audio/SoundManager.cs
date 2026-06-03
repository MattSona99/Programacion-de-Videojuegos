using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Central audio playback: one-shot SFX and looping music, routed through the AudioMixer groups so the
/// Settings sliders (MusicVol/SFXVol) control them. Place ONE in Act_01 and assign the two groups; it
/// survives scene loads (<c>DontDestroyOnLoad</c>). <see cref="AudioCue"/> and <see cref="LevelMusic"/>
/// call into it.
/// </summary>
public class SoundManager : MonoBehaviour
{
    /// <summary>Global access point (set by the instance placed in the first scene).</summary>
    public static SoundManager Instance { get; private set; }

    [Header("Mixer routing")]
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerGroup musicGroup;

    private AudioSource _sfx;   // 2D one-shots
    private AudioSource _music; // looping background track

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f; // 2D
        _sfx.outputAudioMixerGroup = sfxGroup;

        _music = gameObject.AddComponent<AudioSource>();
        _music.playOnAwake = false;
        _music.loop = true;
        _music.spatialBlend = 0f;
        _music.outputAudioMixerGroup = musicGroup;
    }

    /// <summary>Plays a one-shot SFX (2D) through the SFX mixer group. No-op if the clip is null.</summary>
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null) _sfx.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>Starts (or replaces) the looping music track through the Music mixer group.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (_music.clip == clip && _music.isPlaying) return; // already playing this track
        _music.clip = clip;
        _music.Play();
    }

    /// <summary>Stops the current music track.</summary>
    public void StopMusic() => _music.Stop();
}
