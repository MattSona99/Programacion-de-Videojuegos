using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Regia di Act_02 (#6 del piano arena). Sequenza lineare:
//   1) prologo: camera in alto sulla statua + dialogo introduttivo (narratore al passato).
//   2) la camera torna sul player, WaveManager.StartNextWave() → le 4 wave si concatenano
//      da sole (autoAdvance del WaveManager) mentre Jammo raccoglie i pezzi (#5).
//   3) epilogo: alla statua completa, camera torna sull'overview, dialogo finale,
//      poi SceneManager.LoadScene("Act_03").
// L'epilogo è triggerato da StatueAssemblyDirector.onAssemblyFinished (UnityEvent,
// wired in Inspector verso OnAssemblyFinished()).
public class Act02Director : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WaveManager waveManager;
    // CinemachineVirtualCameraBase è la classe base condivisa da Cinemachine 2
    // (CinemachineVirtualCamera) e Cinemachine 3 (CinemachineCamera). Usarla qui
    // permette di trascinare nel slot la PlayerFollowCamera di StarterAssets,
    // che potrebbe essere ancora la versione legacy del prefab.
    [SerializeField] private CinemachineVirtualCameraBase prologueCamera;
    [SerializeField] private CinemachineVirtualCameraBase gameplayCamera;

    [Header("Dialoghi")]
    [SerializeField] private DialogueAsset prologueDialogue;
    [SerializeField] private DialogueAsset epilogueDialogue;

    [Header("Tempi")]
    [Tooltip("Pausa tra la fine del dialogo prologo e l'inizio della prima wave.")]
    [SerializeField] private float pauseBeforeWavesStart = 0.5f;
    [Tooltip("Pausa tra la fine del dialogo epilogo e il caricamento della scena successiva.")]
    [SerializeField] private float pauseBeforeNextScene = 1.5f;

    [Header("Scena successiva")]
    [SerializeField] private string nextSceneName = "Act_03";

    [Header("Priorità Cinemachine 3")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 5;

    private CinemachineBrain _brain;
    private bool _epilogueQueued;

    void Awake()
    {
        if (Camera.main != null) _brain = Camera.main.GetComponent<CinemachineBrain>();
    }

    void Start()
    {
        StartCoroutine(RunPrologue());
    }

    // Da agganciare a StatueAssemblyDirector.onAssemblyFinished dall'Inspector.
    public void OnAssemblyFinished()
    {
        if (_epilogueQueued) return;
        _epilogueQueued = true;
        StartCoroutine(RunEpilogue());
    }

    private IEnumerator RunPrologue()
    {
        SetActiveCamera(prologueCamera);
        yield return WaitForBlend();

        // DialogueManager.PlayDialogue blocca il player via SetPlayerLocked(true)
        // e lo rilascia a fine dialogo. Niente da fare manualmente qui.
        if (prologueDialogue != null && DialogueManager.Instance != null)
        {
            bool done = false;
            DialogueManager.Instance.PlayDialogue(prologueDialogue, () => done = true);
            yield return new WaitUntil(() => done);
        }
        else
        {
            Debug.LogWarning("[Act02Director] prologueDialogue o DialogueManager.Instance mancante: salto il prologo.", this);
        }

        SetActiveCamera(gameplayCamera);
        yield return WaitForBlend();

        if (pauseBeforeWavesStart > 0f) yield return new WaitForSeconds(pauseBeforeWavesStart);

        if (waveManager != null) waveManager.StartNextWave();
        else Debug.LogWarning("[Act02Director] waveManager non assegnato: le wave non partiranno.", this);
    }

    private IEnumerator RunEpilogue()
    {
        SetActiveCamera(prologueCamera);
        yield return WaitForBlend();

        if (epilogueDialogue != null && DialogueManager.Instance != null)
        {
            bool done = false;
            DialogueManager.Instance.PlayDialogue(epilogueDialogue, () => done = true);
            yield return new WaitUntil(() => done);
        }
        else
        {
            Debug.LogWarning("[Act02Director] epilogueDialogue o DialogueManager.Instance mancante: carico comunque la scena successiva.", this);
        }

        if (pauseBeforeNextScene > 0f) yield return new WaitForSeconds(pauseBeforeNextScene);

        // Defensive: se entriamo qui da uno stato di pausa (improbabile, il pause menu
        // ha guard su DialogueManager.IsPlaying), ripristiniamo timeScale e cursore.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!string.IsNullOrEmpty(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    private void SetActiveCamera(CinemachineVirtualCameraBase target)
    {
        if (target != null) target.Priority = activePriority;
        if (prologueCamera != null && prologueCamera != target) prologueCamera.Priority = idlePriority;
        if (gameplayCamera != null && gameplayCamera != target) gameplayCamera.Priority = idlePriority;
    }

    private IEnumerator WaitForBlend()
    {
        if (_brain == null) yield break;
        yield return new WaitWhile(() => _brain.IsBlending);
    }
}
