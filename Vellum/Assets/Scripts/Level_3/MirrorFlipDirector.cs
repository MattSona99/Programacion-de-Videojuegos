using System.Collections;
using UnityEngine;

/// <summary>
/// "Celestial" transition of the final level. NO more world flipping (old two-CinemachineCamera
/// approach): with the mirror setup (UpWorld/DownWorld layers + MirrorShader/MirrorCameraSync) the
/// actors stay in the normal world; only the SKY changes. FlipTo rotates the celestial bodies,
/// crossfades the lights, and swaps the skybox + the two bodies' layers, so Phase 1 (sun, blue sky)
/// gradually becomes Phase 2 (red moon, red sky) and vice versa.
///
/// Both sky materials use the SAME Skybox/Procedural shader, so the sky is crossfaded gradually by
/// lerping the procedural properties on a single runtime blend material (no hard swap). Keeps the API
/// used by MirrorDuelDirector:
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
    [Tooltip("The Skybox component on the mirror camera (the reflected 'below' sky). Blended in the OPPOSITE direction, so above goes Sun→Moon while the reflection goes Moon→Sun.")]
    [SerializeField] private Skybox reflectionSkybox;
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
    private Quaternion _pivotBaseRot;

    // Runtime procedural-skybox blend: both materials share Skybox/Procedural, so instead of swapping
    // them at the midpoint we lerp the properties of a single cloned material for a gradual sky.
    private Material _skyBlend;
    private Material _skyBlendReflection; // the reflected sky, blended Moon→Sun (opposite of above)
    private bool _skyBlendReady;
    private bool _reflectionReady;
    private Color _sunTint, _moonTint, _sunGround, _moonGround;
    private float _sunSunSize, _moonSunSize, _sunAtmo, _moonAtmo, _sunExposure, _moonExposure;

    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
    private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    // Global blend (0 = Sun textures, 1 = Moon textures) read by the SkyBodyBlend shader on the
    // Sun/Moon spheres: crossfades their textures in place (no movement, no layer swap).
    private static readonly int BodyBlendId = Shader.PropertyToID("_BodyBlend");

    void Awake()
    {
        if (celestialPivot != null) _pivotBaseRot = celestialPivot.localRotation;
        SetupSkyBlend();
    }

    void OnDestroy()
    {
        if (_skyBlend != null) Destroy(_skyBlend);
        if (_skyBlendReflection != null) Destroy(_skyBlendReflection);
    }

    /// <summary>
    /// Caches the Sun/Moon values of the procedural-skybox properties and creates a single runtime
    /// blend material (clone of the Sun skybox) used as the active skybox. No-op (falls back to hard
    /// swap) if either material is missing.
    /// </summary>
    private void SetupSkyBlend()
    {
        if (sunSkybox == null || moonSkybox == null) return;

        _sunTint = sunSkybox.GetColor(SkyTintId);     _moonTint = moonSkybox.GetColor(SkyTintId);
        _sunGround = sunSkybox.GetColor(GroundColorId); _moonGround = moonSkybox.GetColor(GroundColorId);
        _sunSunSize = sunSkybox.GetFloat(SunSizeId);   _moonSunSize = moonSkybox.GetFloat(SunSizeId);
        _sunAtmo = sunSkybox.GetFloat(AtmosphereThicknessId); _moonAtmo = moonSkybox.GetFloat(AtmosphereThicknessId);
        _sunExposure = sunSkybox.GetFloat(ExposureId); _moonExposure = moonSkybox.GetFloat(ExposureId);

        _skyBlend = new Material(sunSkybox); // clone keeps the Skybox/Procedural shader
        RenderSettings.skybox = _skyBlend;
        _skyBlendReady = true;

        // Reflected sky ("below"): a second blend material on the mirror camera's Skybox component,
        // driven in the OPPOSITE direction (Moon→Sun) so the two skies cross at the flip.
        if (reflectionSkybox != null)
        {
            _skyBlendReflection = new Material(moonSkybox);
            reflectionSkybox.material = _skyBlendReflection;
            _reflectionReady = true;
        }
    }

    /// <summary>Initial state with no transition (called in the director's Start: ApplyImmediate(false)).</summary>
    public void ApplyImmediate(bool moon)
    {
        SetRotation(moon ? 1f : 0f);
        SetLights(moon ? 1f : 0f);
        RenderSettings.ambientLight = moon ? moonAmbient : sunAmbient;
        // Fixed layers (Sun→UpWorld=sky, Moon→DownWorld=water). No swap: the texture crossfade below
        // handles the Sun↔Moon change, keeping both bodies above the water so the reflection stays valid.
        SetWorldLayers(false);
        Shader.SetGlobalFloat(BodyBlendId, moon ? 1f : 0f);
        ApplySky(moon ? 1f : 0f);
        DynamicGI.UpdateEnvironment();
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
            ApplySky(moonW);                            // gradual procedural sky
            Shader.SetGlobalFloat(BodyBlendId, moonW);  // texture crossfade on the Sun/Moon spheres
            RenderSettings.ambientLight = Color.Lerp(sunAmbient, moonAmbient, moonW);

            yield return null;
        }

        SetRotation(moon ? 1f : 0f);
        SetLights(moon ? 1f : 0f);
        ApplySky(moon ? 1f : 0f);
        Shader.SetGlobalFloat(BodyBlendId, moon ? 1f : 0f);
        RenderSettings.ambientLight = moon ? moonAmbient : sunAmbient;
        DynamicGI.UpdateEnvironment(); // refresh ambient/reflections once at the end (not per frame)

        if (DialogueManager.Instance != null) DialogueManager.Instance.UnlockPlayer();
        _routine = null;
    }

    // moonWeight: 0 = Sun pose (base), 1 = Moon pose (base * flipRotation). Rotates ONLY the pivot so
    // the Sun/Moon (offset children) orbit it along an arc — NOT their own axis (that was an in-place
    // spin). With the bodies at the same position and no pivot, nothing moves (assign a pivot).
    private void SetRotation(float moonWeight)
    {
        if (celestialPivot == null) return;
        Quaternion flip = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(flipRotation), moonWeight);
        celestialPivot.localRotation = _pivotBaseRot * flip;
    }

    private void SetLights(float moonWeight)
    {
        if (sunLight != null) sunLight.intensity = sunLightIntensity * (1f - moonWeight);
        if (moonLight != null) moonLight.intensity = moonLightIntensity * moonWeight;
    }

    /// <summary>
    /// Sets the sky for the given Sun(0)→Moon(1) weight by lerping the procedural-skybox properties on
    /// the runtime blend material — a gradual sky instead of a hard swap. Falls back to assigning the
    /// nearest full skybox if the blend material couldn't be created.
    /// </summary>
    private void ApplySky(float moonWeight)
    {
        if (!_skyBlendReady)
        {
            Material sky = moonWeight >= 0.5f ? moonSkybox : sunSkybox;
            if (sky != null) RenderSettings.skybox = sky;
            return;
        }

        _skyBlend.SetColor(SkyTintId, Color.Lerp(_sunTint, _moonTint, moonWeight));
        _skyBlend.SetColor(GroundColorId, Color.Lerp(_sunGround, _moonGround, moonWeight));
        _skyBlend.SetFloat(SunSizeId, Mathf.Lerp(_sunSunSize, _moonSunSize, moonWeight));
        _skyBlend.SetFloat(AtmosphereThicknessId, Mathf.Lerp(_sunAtmo, _moonAtmo, moonWeight));
        _skyBlend.SetFloat(ExposureId, Mathf.Lerp(_sunExposure, _moonExposure, moonWeight));

        // Reflected sky: OPPOSITE direction (Moon at 0 → Sun at 1), so above and below cross.
        if (_reflectionReady)
        {
            _skyBlendReflection.SetColor(SkyTintId, Color.Lerp(_moonTint, _sunTint, moonWeight));
            _skyBlendReflection.SetColor(GroundColorId, Color.Lerp(_moonGround, _sunGround, moonWeight));
            _skyBlendReflection.SetFloat(SunSizeId, Mathf.Lerp(_moonSunSize, _sunSunSize, moonWeight));
            _skyBlendReflection.SetFloat(AtmosphereThicknessId, Mathf.Lerp(_moonAtmo, _sunAtmo, moonWeight));
            _skyBlendReflection.SetFloat(ExposureId, Mathf.Lerp(_moonExposure, _sunExposure, moonWeight));
        }
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
