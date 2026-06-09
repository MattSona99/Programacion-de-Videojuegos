using UnityEngine;

/// <summary>
/// Plays a looping background track for the scene on <c>Start</c> (through <see cref="SoundManager"/>,
/// so it respects the music volume). Drop one per level and assign the track.
/// </summary>
public class LevelMusic : MonoBehaviour
{
    [SerializeField] private AudioClip music;

    private void Start()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayMusic(music);
        else Debug.LogWarning("[LevelMusic] No SoundManager in the scene/run: music won't play. Place a SoundManager in Act_01.", this);
    }
}
