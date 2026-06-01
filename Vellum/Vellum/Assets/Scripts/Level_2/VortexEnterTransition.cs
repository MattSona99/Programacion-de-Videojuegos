using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VortexEnterTransition : MonoBehaviour
{
    [Header("Effetto Entrata (Vortice Inverso)")]
    [Tooltip("Trascina qui il Global Volume della scena Act_02")]
    public Volume globalVolume;
    
    [Tooltip("Quanto dura l'apertura? (in secondi)")]
    public float transitionDuration = 4f;

    private LensDistortion _lensDistortion;
    private ColorAdjustments _colorAdjustments;

    private void Start()
    {
        // 1. All'avvio della scena, cerchiamo i filtri
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _lensDistortion);
            globalVolume.profile.TryGet(out _colorAdjustments);

            // 2. Facciamo partire l'animazione di sblocco!
            StartCoroutine(ReverseVortexRoutine());
        }
    }

    private IEnumerator ReverseVortexRoutine()
    {
        if (_lensDistortion != null) _lensDistortion.active = true;
        if (_colorAdjustments != null) _colorAdjustments.active = true;

        // Partiamo dai valori estremi (il buco nero)
        float startDistortion = -1f;
        float startScale = 0.01f;
        float startSaturation = -100f;

        // Arriviamo ai valori normali (mondo reale)
        float endDistortion = 0f;
        float endScale = 1f;
        float endSaturation = 0f; // Assumendo che il colore base dell'arena sia 0

        float time = 0f;

        while (time < transitionDuration)
        {
            time += Time.deltaTime;
            float t = time / transitionDuration;

            // Curva "Ease Out": parte veloce e decelera dolcemente verso la fine
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

        // Sicurezza finale: forziamo i valori a 0 precisi alla fine dell'animazione
        if (_lensDistortion != null)
        {
            _lensDistortion.intensity.value = 0f;
            _lensDistortion.scale.value = 1f;
            _lensDistortion.active = false; // Spegniamo la distorsione per risparmiare performance
        }
        
        if (_colorAdjustments != null)
        {
            _colorAdjustments.saturation.value = 0f;
            // Teniamo attivo ColorAdjustments se lo usi per l'estetica generale dell'arena
        }
    }
}