using System.Collections;
using UnityEngine;

/// <summary>
/// "Celestial" transition of the final level. NO more world flipping (old two-CinemachineCamera
/// approach): with the mirror setup (UpWorld/DownWorld layers + MirrorShader/MirrorCameraSync) the
/// actors stay in the normal world; only the SKY changes. FlipTo rotates the celestial bodies,
/// crossfades the lights, and swaps the skybox + the two bodies' layers, so Phase 1 (sun, blue sky)
/// gradually becomes Phase 2 (red moon, red sky) and vice versa.
///
/// Two distinct skyboxes can't crossfade without a blend shader: here they're swapped at the
/// midpoint, masked by the rotation and the light lerp. Keeps the API used by MirrorDuelDirector:
/// ApplyImmediate(moon) and FlipTo(moon). Follows the CLAUDE.md §3.4 cinematic pattern
/// (lock input → coroutine sequence → restore), without Cinemachine.
/// </summary>
public class MirrorFlipDirector : MonoBehaviour
{
    [Header("Celestial bodies (rotation)")]
    [Tooltip("Common pivot of sun+moon: if assigned, this is rotated (for an 'orbit' effect make them children, offset from the center). Alternatively or additionally use sunObject/moonObject.")]
    [SerializeField] private Transform celestialPivot;
    [SerializeField] private Transform sunObject;
    [SerializeField] private Transform moonObject;
    [Tooltip("Rotation (degrees) applied when switching to Moon; switching back to Sun unwinds it. E.g. (0,0,180). Leave (0,0,0) for no rotation.")]
    [SerializeField] private Vector3 flipRotation = new Vector3(0f, 0f, 180f);

    [Header("Atmosphere (two skyboxes)")]
    [SerializeField] private Material sunSkybox;
    [SerializeField] private Material moonSkybox;
    [Tooltip("Ambient (Flat mode) of the two worlds: lerped gradually during the transition.")]
    [SerializeField] private Color sunAmbient = new Color(0.6f, 0.7f, 0.9f);
    [SerializeField] private Color moonAmbient = new Color(0.5f, 0.1f, 0.1f);

    [Header("World swap (celestial bodies' layers)")]
    [Tooltip("If on, at the midpoint it swaps sunObject/moonObject's layers between UpWorld and DownWorld, so above/below the mirror invert. Does NOT touch the actors.")]
    [SerializeField] private bool swapWorldLayers = true;
    [SerializeField] private string upWorldLayer = "UpWorld";
    [SerializeField] private string downWorldLayer = "DownWorld";

    [Header("Lights (crossfade)")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [Tooltip("Full intensity of the respective light (the other goes to 0 during the crossfade).")]
    [SerializeField] private float sunLightIntensity = 1f;
    [SerializeField] private float moonLightIntensity = 1f;

    [Header("Timing")]
    [SerializeField] private float blendDuration = 2f;

    private Coroutine _routine;
    private Quaternion _pivotBaseRot, _sunBaseRot, _moonBaseRot;

    void Awake()
    {
        if (celestialPivot != null) _pivotBaseRot = celestialPivot.localRotation;
        if (sunObject != null) _sunBaseRot = sunObject.localRotation;
        if (moonObject != null) _moonBaseRot = moonObject.localRotation;
    }

    /// <summary>Initial state with no transition (called in the director's Start: ApplyImmediate(false)).</summary>
    public void ApplyImmediate(bool moon)
    {
        SetRotation(moon ? 1f : 0f);
        SetLights(moon ? 1f : 0f);
        RenderSettings.ambientLight = moon ? moonAmbient : sunAmbient;
        SetWorldLayers(moon);
        ApplySkybox(moon);
    }

    /// <summary>Switches to Moon (moon = true) or Sun (moon = false). Locks the Player for the whole transition.</summary>
    public IEnumerator FlipTo(bool moon)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlipRoutine(moon));
        yield return _routine;
    }

    private IEnumerator FlipRoutine(bool moon)
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.LockPlayer();

        float dur = Mathf.Max(0.0001f, blendDuration);
        bool swappedAtHalf = false;
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = k * k * (3f - 2f * k); // smoothstep

            // Moon-world weight (0 = sun, 1 = moon), consistent with the flip direction
            float moonW = moon ? e : 1f - e;

            SetRotation(moonW);
            SetLights(moonW);
            RenderSettings.ambientLight = Color.Lerp(sunAmbient, moonAmbient, moonW);

            // two skyboxes + layer swap: at the midpoint, masked by rotation and lights
            if (!swappedAtHalf && k >= 0.5f)
            {
                ApplySkybox(moon);
                SetWorldLayers(moon);
                swappedAtHalf = true;
            }

            yield return null;
        }

        SetRotation(moon ? 1f : 0f);
        SetLights(moon ? 1f : 0f);
        RenderSettings.ambientLight = moon ? moonAmbient : sunAmbient;
        if (!swappedAtHalf) { ApplySkybox(moon); SetWorldLayers(moon); }

        if (DialogueManager.Instance != null) DialogueManager.Instance.UnlockPlayer();
        _routine = null;
    }

    // moonWeight: 0 = Sun pose (base), 1 = Moon pose (base * flipRotation).
    private void SetRotation(float moonWeight)
    {
        Quaternion flip = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(flipRotation), moonWeight);
        if (celestialPivot != null) celestialPivot.localRotation = _pivotBaseRot * flip;
        if (sunObject != null) sunObject.localRotation = _sunBaseRot * flip;
        if (moonObject != null) moonObject.localRotation = _moonBaseRot * flip;
    }

    private void SetLights(float moonWeight)
    {
        if (sunLight != null) sunLight.intensity = sunLightIntensity * (1f - moonWeight);
        if (moonLight != null) moonLight.intensity = moonLightIntensity * moonWeight;
    }

    private void ApplySkybox(bool moon)
    {
        Material sky = moon ? moonSkybox : sunSkybox;
        if (sky == null) return;
        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment();
    }

    // Sun and Moon swap between UpWorld/DownWorld: above/below the mirror invert. In Sun
    // phase the sun is in UpWorld; in Moon phase the moon goes there.
    private void SetWorldLayers(bool moon)
    {
        if (!swapWorldLayers) return;
        int up = LayerMask.NameToLayer(upWorldLayer);
        int down = LayerMask.NameToLayer(downWorldLayer);
        if (up < 0 || down < 0) return;

        SetLayerRecursive(sunObject, moon ? down : up);
        SetLayerRecursive(moonObject, moon ? up : down);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
