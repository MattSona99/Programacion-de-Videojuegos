using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Plays a "reverse vortex" intro on the Act_02 scene: starts from the extreme black-hole values
/// (lens distortion + desaturation) and eases them back to normal, mirroring the DoorPortal exit
/// effect of Act 1.
/// </summary>
public class VortexEnterTransition : MonoBehaviour
{
    [Header("Entry effect (reverse vortex)")]
    [Tooltip("Drag the Act_02 scene's Global Volume here")]
    public Volume globalVolume;

    [Tooltip("How long the opening lasts (in seconds)")]
    public float transitionDuration = 4f;

    private LensDistortion _lensDistortion;
    private ColorAdjustments _colorAdjustments;

    private void Start()
    {
        // 1. On scene start, grab the overrides
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _lensDistortion);
            globalVolume.profile.TryGet(out _colorAdjustments);

            // 2. Start the un-distort animation!
            StartCoroutine(ReverseVortexRoutine());
        }
    }

    /// <summary>Eases lens distortion and saturation from the black-hole values back to normal, then disables distortion.</summary>
    private IEnumerator ReverseVortexRoutine()
    {
        if (_lensDistortion != null) _lensDistortion.active = true;
        if (_colorAdjustments != null) _colorAdjustments.active = true;

        // Start from the extreme values (the black hole)
        float startDistortion = -1f;
        float startScale = 0.01f;
        float startSaturation = -100f;

        // Arrive at the normal values (real world)
        float endDistortion = 0f;
        float endScale = 1f;
        float endSaturation = 0f; // Assuming the arena's base color is 0

        float time = 0f;

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = time / transitionDuration;

            // "Ease Out" curve: starts fast and gently decelerates toward the end
            float curve = 1f - Mathf.Pow(1f - t, 3f);

            if (_lensDistortion != null)
            {
                _lensDistortion.intensity.value = Mathf.Lerp(startDistortion, endDistortion, curve);
                _lensDistortion.scale.value = Mathf.Lerp(startScale, endScale, curve);
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, endSaturation, curve);
            }

            yield return null;
        }

        // Final safety: force exact 0 values at the end of the animation
        if (_lensDistortion != null)
        {
            _lensDistortion.intensity.value = 0f;
            _lensDistortion.scale.value = 1f;
            _lensDistortion.active = false; // Disable distortion to save performance
        }

        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = 0f;
            // Keep ColorAdjustments active if you use it for the arena's overall look
        }
    }
}