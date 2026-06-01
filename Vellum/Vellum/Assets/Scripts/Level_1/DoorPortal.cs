using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Collider))]
public class DoorPortal : MonoBehaviour
{
    [Header("Riferimenti Puzzle")]
    [Tooltip("PathPuzzleManager: il portal si attiva solo quando IsPuzzleCompleted è true.")]
    [SerializeField] private PathPuzzleManager puzzleManager;

    [Tooltip("CinematicFallManager: usato per bloccare COMPLETAMENTE il player (movimento, look, combat, input) quando parte la transizione a vortice.")]
    [SerializeField] private CinematicFallManager cinematicManager;

    [Header("Destinazione")]
    [Tooltip("Nome esatto della scena da caricare.")]
    [SerializeField] private string sceneName = "Act_02";

    [Header("Effetto Transizione (Vortice)")]
    [Tooltip("Trascina qui il tuo Global Volume della scena")]
    public Volume globalVolume;
    [Tooltip("Quanto dura l'effetto prima di cambiare scena? (in secondi)")]
    public float transitionDuration = 4f;

    private bool _triggered = false;
    
    // Variabili per conservare i due filtri
    private LensDistortion _lensDistortion;
    private ColorAdjustments _colorAdjustments;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // Quando il gioco parte, andiamo a "pescare" i due filtri dentro il Volume
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

    // Punto di ingresso esterno (chiamato da PathPuzzleManager quando il puzzle
    // si completa) per i casi in cui la porta è geometricamente PRIMA dell'ultima
    // tile: l'OnTriggerEnter è già scattato a vuoto perché allora IsPuzzleCompleted
    // era false, quindi serve un trigger alternativo basato sull'evento di completamento.
    public void TriggerTransition()
    {
        if (_triggered) return;
        _triggered = true;

        // Player completamente bloccato per tutta la transizione: niente
        // movimento, look, combat o input mentre parte l'effetto B/N distorto.
        if (cinematicManager != null) cinematicManager.SetPlayerMovement(false, keepLookActive: false);

        StartCoroutine(VortexTransitionRoutine());
    }

    private IEnumerator VortexTransitionRoutine()
    {
        float time = 0f;

        // Assicuriamoci che i filtri siano attivi prima di modificarli
        if (_lensDistortion != null) _lensDistortion.active = true;
        if (_colorAdjustments != null) _colorAdjustments.active = true;

        // Salviamo i valori di partenza
        float startDistortion = _lensDistortion != null ? _lensDistortion.intensity.value : 0f;
        float startScale = _lensDistortion != null ? _lensDistortion.scale.value : 1f;
        float startSaturation = _colorAdjustments != null ? _colorAdjustments.saturation.value : 0f;

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = time / transitionDuration;

            // Curva matematica per rendere l'effetto morbido all'inizio e violento alla fine
            float curve = t * t * t; 

            if (Camera.main != null)
            {
                float spinSpeed = Mathf.Lerp(0f, 1000f, curve); 
                Camera.main.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
            }

            if (_lensDistortion != null)
            {
                // Intensità a -1 crea l'effetto "risucchio"
                _lensDistortion.intensity.value = Mathf.Lerp(startDistortion, -1f, curve); 
                // Scalare verso 0.01 stringe l'immagine al centro facendola scomparire nel nulla
                _lensDistortion.scale.value = Mathf.Lerp(startScale, 0.01f, curve); 
            }

            if (_colorAdjustments != null)
            {
                // Togliamo tutto il colore portandolo a -100 (Scala di grigi)
                _colorAdjustments.saturation.value = Mathf.Lerp(startSaturation, -100f, curve);
            }

            yield return null;
        }

        // Finito il vortice, carichiamo istantaneamente la nuova scena!
        SceneManager.LoadScene(sceneName);
    }
}