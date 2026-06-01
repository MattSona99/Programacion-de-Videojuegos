using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Act_02 director. Linear sequence:
///   1) prologue: camera up on the statue + intro dialogue (past-tense narrator).
///   2) camera returns to the player, WaveManager.StartNextWave() → the waves chain
///      themselves (WaveManager autoAdvance) while Jammo collects the pieces.
///   3) epilogue: on statue completion, camera returns to the overview, final dialogue,
///      then SceneManager.LoadScene("Act_03").
/// The epilogue is triggered by StatueAssemblyDirector.onAssemblyFinished (UnityEvent,
/// wired in the Inspector to OnAssemblyFinished()).
/// </summary>
public class Act02Director : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WaveManager waveManager;
    // CinemachineVirtualCameraBase is the base class shared by Cinemachine 2
    // (CinemachineVirtualCamera) and Cinemachine 3 (CinemachineCamera). Using it here
    // lets us drag StarterAssets' PlayerFollowCamera into the slot, which may still be
    // the legacy version of the prefab.
    [SerializeField] private CinemachineVirtualCameraBase prologueCamera;
    [SerializeField] private CinemachineVirtualCameraBase gameplayCamera;

    [Header("Dialogues")]
    [SerializeField] private DialogueAsset prologueDialogue;
    [SerializeField] private DialogueAsset epilogueDialogue;

    [Header("Timing")]
    [Tooltip("How long the player stays framed at scene start before the camera begins rising onto the statue.")]
    [SerializeField] private float initialPauseOnPlayer = 1.0f;
    [Tooltip("Duration of the slow blend from the player camera to the statue camera (and back).")]
    [SerializeField] private float prologueBlendDuration = 2.0f;
    [Tooltip("Pause between the end of the prologue dialogue and the start of the first wave.")]
    [SerializeField] private float pauseBeforeWavesStart = 0.5f;
    [Tooltip("Pause between the end of the epilogue dialogue and loading the next scene.")]
    [SerializeField] private float pauseBeforeNextScene = 1.5f;

    [Header("Next scene")]
    [SerializeField] private string nextSceneName = "Act_03";

    [Header("Cinemachine 3 priorities")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 5;

    [Header("HUD")]
    [Tooltip("UI elements to reveal at the end of the prologue (slide+fade) and hide at the start of the epilogue. Typically: HUDPlayer and StatueProgressBar.")]
    [SerializeField] private HudReveal[] hudReveals;
    [Tooltip("Wait after hiding the HUD before the epilogue camera blend, so the exit animation has time to finish.")]
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

    /// <summary>Wire this to StatueAssemblyDirector.onAssemblyFinished in the Inspector. Starts the epilogue once.</summary>
    public void OnAssemblyFinished()
    {
        if (_epilogueQueued) return;
        _epilogueQueued = true;
        StartCoroutine(RunEpilogue());
    }

    /// <summary>Prologue: frame the player, slow-blend up to the statue, play the intro dialogue, blend back, reveal the HUD, then start the waves.</summary>
    private IEnumerator RunPrologue()
    {
        // Frame 0: camera ON THE PLAYER (PlayerFollowCamera wins with higher priority),
        // player locked manually: the initial blend to the statue must happen while the
        // player is still, and PlayDialogue starts only afterwards.
        SetActiveCamera(gameplayCamera);
        if (DialogueManager.Instance != null) DialogueManager.Instance.LockPlayer();

        if (initialPauseOnPlayer > 0f) yield return new WaitForSeconds(initialPauseOnPlayer);

        // Slow camera blend -> statue. Override the Brain's DefaultBlend for the
        // requested duration, then restore it (same pattern as MainMenuManager).
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
            Debug.LogWarning("[Act02Director] prologueDialogue or DialogueManager.Instance missing: skipping the prologue.", this);
        }

        SetActiveCamera(gameplayCamera);
        yield return WaitForBlend();

        // Show the HUD before combat starts: the user sees their health bar
        // and the statue rising as the enemies begin to arrive.
        RevealHud();

        if (pauseBeforeWavesStart > 0f) yield return new WaitForSeconds(pauseBeforeWavesStart);

        if (waveManager != null) waveManager.StartNextWave();
        else Debug.LogWarning("[Act02Director] waveManager not assigned: the waves won't start.", this);
    }

    /// <summary>Epilogue: hide the HUD, blend to the overview, play the final dialogue, then load the next scene.</summary>
    private IEnumerator RunEpilogue()
    {
        // Symmetry: the HUD disappears before the epilogue blend, with the same animation reversed.
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
            Debug.LogWarning("[Act02Director] epilogueDialogue or DialogueManager.Instance missing: loading the next scene anyway.", this);
        }

        if (pauseBeforeNextScene > 0f) yield return new WaitForSeconds(pauseBeforeNextScene);

        // Defensive: if we get here from a paused state (unlikely, the pause menu
        // guards on DialogueManager.IsPlaying), restore timeScale and cursor.
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
