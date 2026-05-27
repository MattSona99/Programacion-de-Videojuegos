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
    [Tooltip("Quanto resta inquadrato il player all'avvio della scena prima che la camera inizi a salire sulla statua.")]
    [SerializeField] private float initialPauseOnPlayer = 1.0f;
    [Tooltip("Durata del blend lento dalla camera del player a quella della statua (e ritorno).")]
    [SerializeField] private float prologueBlendDuration = 2.0f;
    [Tooltip("Pausa tra la fine del dialogo prologo e l'inizio della prima wave.")]
    [SerializeField] private float pauseBeforeWavesStart = 0.5f;
    [Tooltip("Pausa tra la fine del dialogo epilogo e il caricamento della scena successiva.")]
    [SerializeField] private float pauseBeforeNextScene = 1.5f;

    [Header("Scena successiva")]
    [SerializeField] private string nextSceneName = "Act_03";

    [Header("Priorità Cinemachine 3")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 5;

    [Header("HUD")]
    [Tooltip("Elementi UI da rivelare alla fine del prologo (slide+fade) e nascondere all'inizio dell'epilogo. Tipicamente: HUDPlayer e StatueProgressBar.")]
    [SerializeField] private HudReveal[] hudReveals;
    [Tooltip("Attesa post-Hide HUD prima del blend camera epilogo, così l'animazione di uscita ha tempo di completarsi.")]
    [SerializeField] private float hudHideWait = 0.6f;

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
        // Frame 0: camera SUL PLAYER (PlayerFollowCamera vince con priority più alta),
        // player bloccato manualmente: il blend iniziale verso la statua deve avvenire
        // mentre il player è fermo, e PlayDialogue partirà solo dopo.
        SetActiveCamera(gameplayCamera);
        if (DialogueManager.Instance != null) DialogueManager.Instance.LockPlayer();

        if (initialPauseOnPlayer > 0f) yield return new WaitForSeconds(initialPauseOnPlayer);

        // Blend lento camera -> statua. Sovrascriviamo il DefaultBlend del Brain per
        // la durata richiesta, poi lo ripristiniamo (pattern come MainMenuManager).
        CinemachineBlendDefinition prevBlend = default;
        bool blendOverridden = false;
        if (_brain != null && prologueBlendDuration > 0f)
        {
            prevBlend = _brain.DefaultBlend;
            _brain.DefaultBlend = new CinemachineBlendDefinition(prevBlend.Style, prologueBlendDuration);
            blendOverridden = true;
        }

        SetActiveCamera(prologueCamera);
        yield return WaitForBlend();

        if (blendOverridden && _brain != null) _brain.DefaultBlend = prevBlend;

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

        // Mostra l'HUD prima che parta il combat: l'utente vede la sua
        // healthbar e la statua salire mentre i nemici cominciano ad arrivare.
        RevealHud();

        if (pauseBeforeWavesStart > 0f) yield return new WaitForSeconds(pauseBeforeWavesStart);

        if (waveManager != null) waveManager.StartNextWave();
        else Debug.LogWarning("[Act02Director] waveManager non assegnato: le wave non partiranno.", this);
    }

    private IEnumerator RunEpilogue()
    {
        // Simmetria: l'HUD scompare prima del blend epilogo, con la stessa animazione al contrario.
        HideHud();
        if (hudHideWait > 0f) yield return new WaitForSeconds(hudHideWait);

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

    private void RevealHud()
    {
        if (hudReveals == null) return;
        for (int i = 0; i < hudReveals.Length; i++)
            if (hudReveals[i] != null) hudReveals[i].Reveal();
    }

    private void HideHud()
    {
        if (hudReveals == null) return;
        for (int i = 0; i < hudReveals.Length; i++)
            if (hudReveals[i] != null) hudReveals[i].Hide();
    }
}
