using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Door portal at the end of Act 1: once the puzzle is completed and the Player enters (or on
/// the completion event), it fully locks the Player and plays a "vortex" transition (camera
/// spin + lens distortion + desaturation) before loading the next scene.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorPortal : MonoBehaviour
{
    [Header("Puzzle references")]
    [Tooltip("PathPuzzleManager: the portal only activates when IsPuzzleCompleted is true.")]
    [SerializeField] private PathPuzzleManager puzzleManager;

    [Tooltip("CinematicFallManager: used to FULLY lock the player (movement, look, combat, input) when the vortex transition starts.")]
    [SerializeField] private CinematicFallManager cinematicManager;

    [Header("Destination")]
    [Tooltip("Exact name of the scene to load.")]
    [SerializeField] private string sceneName = "Act_02";

    [Header("Transition effect (Vortex)")]
    [Tooltip("Drag your scene's Global Volume here")]
    public Volume globalVolume;
    [Tooltip("How long the effect lasts before switching scene (in seconds)")]
    public float transitionDuration = 4f;

    private bool _triggered = false;

    // References to the two post-process overrides
    private LensDistortion _lensDistortion;
    private ColorAdjustments _colorAdjustments;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // On start, grab the two overrides from the Volume
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _lensDistortion);
            globalVolume.profile.TryGet(out _colorAdjustments);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        if (puzzleManager == null || !puzzleManager.IsPuzzleCompleted) return;

        TriggerTransition();
    }

    /// <summary>
    /// External entry point (called by PathPuzzleManager on completion) for cases where the door
    /// is geometrically BEFORE the last tile: OnTriggerEnter already fired uselessly because
    /// IsPuzzleCompleted was false then, so an alternative completion-event trigger is needed.
    /// </summary>
    public void TriggerTransition()
    {
        if (_triggered) return;
        _triggered = true;

        // Player fully locked for the whole transition: no movement, look, combat or
        // input while the distorted B/W effect plays.
        if (cinematicManager != null) cinematicManager.SetPlayerMovement(false, keepLookActive: false);

        StartCoroutine(VortexTransitionRoutine());
    }

    /// <summary>Spins the camera and ramps lens distortion + desaturation over the duration, then loads the scene.</summary>
    private IEnumerator VortexTransitionRoutine()
    {
        float time = 0f;

        // Make sure the overrides are active before modifying them
        if (_lensDistortion != null) _lensDistortion.active = true;
        if (_colorAdjustments != null) _colorAdjustments.active = true;

        // Save the starting values
        float startDistortion = _lensDistortion != null ? _lensDistortion.intensity.value : 0f;
        float startScale = _lensDistortion != null ? _lensDistortion.scale.value : 1f;
        float startSaturation = _colorAdjustments != null ? _colorAdjustments.saturation.value : 0f;

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = time / transitionDuration;

            // Curve to make the effect soft at the start and violent at the end
            float curve = t * t * t;

            if (Camera.main != null)
            {
                float spinSpeed = Mathf.Lerp(0f, 1000f, curve);
                Camera.main.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
            }

            if (_lensDistortion != null)
            {
                // Intensity at -1 creates the "suck-in" effect
                _lensDistortion.intensity.value = Mathf.Lerp(startDistortion, -1f, curve);
                // Scaling toward 0.01 squeezes the image to the center, making it vanish
                _lensDistortion.scale.value = Mathf.Lerp(startScale, 0.01f, curve);
            }

            if (_colorAdjustments != null)
            {
                // Drain all color by taking it to -100 (grayscale)
                _colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, -100f, curve);
            }

            yield return null;
        }

        // Vortex done, instantly load the new scene!
        SceneManager.LoadScene(sceneName);
    }
}