using System.Collections;
using UnityEngine;

// Transizione "celeste" del livello finale. NIENTE più capovolgimento del mondo
// (vecchio approccio a due CinemachineCamera): col setup a specchio (layer
// UpWorld/DownWorld + MirrorShader/MirrorCameraSync) gli attori restano nel
// mondo normale, a cambiare è solo il CIELO. FlipTo fa ruotare i corpi celesti,
// incrocia le luci e scambia skybox + layer dei due corpi, così la Fase 1 (sole,
// cielo azzurro) diventa gradualmente Fase 2 (luna rossa, cielo rosso) e viceversa.
//
// Due skybox distinti non si possono crossfadere senza uno shader di blend:
// qui si scambiano a metà transizione, mascherati dalla rotazione e dal lerp
// delle luci (scelta concordata). Mantiene l'API usata da MirrorDuelDirector:
// ApplyImmediate(moon) e FlipTo(moon). Segue lo schema cinematic di CLAUDE.md
// §3.4 (lock input → sequenza a coroutine → restore), senza Cinemachine.
public class MirrorFlipDirector : MonoBehaviour
{
    [Header("Corpi celesti (rotazione)")]
    [Tooltip("Pivot comune di sole+luna: se assegnato ruota questo (per un effetto 'orbita' mettili come figli, offset dal centro). In alternativa o in aggiunta usa sunObject/moonObject.")]
    [SerializeField] private Transform celestialPivot;
    [SerializeField] private Transform sunObject;
    [SerializeField] private Transform moonObject;
    [Tooltip("Rotazione (gradi) applicata passando a Luna; tornando a Sole si srotola. Es. (0,0,180). Lascia (0,0,0) per non ruotare.")]
    [SerializeField] private Vector3 flipRotation = new Vector3(0f, 0f, 180f);

    [Header("Atmosfera (due skybox)")]
    [SerializeField] private Material sunSkybox;
    [SerializeField] private Material moonSkybox;
    [Tooltip("Ambient (modalità Flat) dei due mondi: lerpato gradualmente durante la transizione.")]
    [SerializeField] private Color sunAmbient = new Color(0.6f, 0.7f, 0.9f);
    [SerializeField] private Color moonAmbient = new Color(0.5f, 0.1f, 0.1f);

    [Header("Scambio mondi (layer dei corpi celesti)")]
    [Tooltip("Se attivo, a metà transizione scambia i layer di sunObject/moonObject tra UpWorld e DownWorld, così sopra/sotto lo specchio si invertono. NON tocca gli attori.")]
    [SerializeField] private bool swapWorldLayers = true;
    [SerializeField] private string upWorldLayer = "UpWorld";
    [SerializeField] private string downWorldLayer = "DownWorld";

    [Header("Luci (crossfade)")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [Tooltip("Intensità piena della rispettiva luce (l'altra va a 0 durante il crossfade).")]
    [SerializeField] private float sunLightIntensity = 1f;
    [SerializeField] private float moonLightIntensity = 1f;

    [Header("Tempi")]
    [SerializeField] private float blendDuration = 2f;

    private Coroutine _routine;
    private Quaternion _pivotBaseRot, _sunBaseRot, _moonBaseRot;

    void Awake()
    {
        if (celestialPivot != null) _pivotBaseRot = celestialPivot.localRotation;
        if (sunObject != null) _sunBaseRot = sunObject.localRotation;
        if (moonObject != null) _moonBaseRot = moonObject.localRotation;
    }

    // Stato iniziale senza transizione (in Start del director: ApplyImmediate(false)).
    public void ApplyImmediate(bool moon)
    {
        SetRotation(moon ? 1f : 0f);
        SetLights(moon ? 1f : 0f);
        RenderSettings.ambientLight = moon ? moonAmbient : sunAmbient;
        SetWorldLayers(moon);
        ApplySkybox(moon);
    }

    // Passa a Luna (moon = true) o a Sole (moon = false). Blocca il Player per
    // tutta la transizione.
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

            // peso del mondo Luna (0 = sole, 1 = luna), coerente col verso del flip
            float moonW = moon ? e : 1f - e;

            SetRotation(moonW);
            SetLights(moonW);
            RenderSettings.ambientLight = Color.Lerp(sunAmbient, moonAmbient, moonW);

            // due skybox + scambio layer: a metà, mascherati da rotazione e luci
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

    // moonWeight: 0 = posa Sole (base), 1 = posa Luna (base * flipRotation).
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

    // Sole e Luna si scambiano tra UpWorld/DownWorld: sopra/sotto lo specchio si
    // invertono. In Fase Sole il sole è in UpWorld; in Fase Luna ci va la luna.
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
