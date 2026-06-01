using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Drives the main menu, the in-game pause menu, and the Game Over screen. Handles the
/// camera/UI transitions (Cinemachine blend + slide/fade), locks/unlocks the Player, manages the
/// settings/leaderboard sub-panels, hides/restores the HUD bars, and triggers the intro dialogue
/// on first play. Esc toggles pause during gameplay (gated while dialogue/book/cinematics run).
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject menuCamera;
    [Tooltip("Drag the Main Camera that holds the Cinemachine Brain here")]
    public CinemachineBrain cameraBrain;

    [Header("UI Elements")]
    public RectTransform menuUIContainer;
    public CanvasGroup menuCanvasGroup;

    [Header("Sub-Panels (Optional)")]
    public GameObject settingsPanel;
    public GameObject leaderboardPanel;

    [Header("Buttons")]
    [Tooltip("\"Play\" button shown only on first launch. Disabled after the first transition.")]
    [SerializeField] private GameObject playButton;
    [Tooltip("\"Resume\" button shown while paused (from the second menu open onward).")]
    [SerializeField] private GameObject resumeButton;
    [Tooltip("\"Restart\" button shown while paused next to Resume. Reloads Act_01.")]
    [SerializeField] private GameObject restartButton;

    [Header("Game Over")]
    [Tooltip("\"Death Container\" canvas: name + Save Score. Active only on Player death.")]
    [SerializeField] private GameObject deathContainer;
    [Tooltip("Name field inside the Death Container, read by SaveScore().")]
    [SerializeField] private TMP_InputField nameInputField;
    [Tooltip("Maximum number of characters allowed in the name field.")]
    [SerializeField] private int nameCharacterLimit = 10;
    [Tooltip("HUD bars (PlayerHUD, StatueProgressBar, ...) to fade out when the menu opens (Esc pause) and on Player death; they fade back in on resume.")]
    [FormerlySerializedAs("hudHideOnDeath")]
    [SerializeField] private HudReveal[] hudBars;

    [Header("UI animation settings")]
    [Tooltip("Duration in seconds of the side panels' fade effect")]
    public float panelFadeDuration = 0.25f;

    [Header("Post Processing")]
    public Volume menuBlurVolume;

    [Header("Player Reference")]
    public GameObject player;

    [Header("Intro")]
    [Tooltip("Dialogue played on the first Play, after the camera settles on the player")]
    [SerializeField] private DialogueAsset introDialogue;

    [Header("Esc lock during gameplay")]
    [Tooltip("Optional references: if assigned, Esc-to-menu is blocked while these are active")]
    [SerializeField] private BookManager bookManager;
    [SerializeField] private CinematicFallManager cinematicFallManager;
    [SerializeField] private JammoGuideController jammoGuideController;

    [Header("Level settings")]
    [Tooltip("Tick this if you're in an advanced level (e.g. Act_02) to skip the title screen")]
    public bool startDirectlyInGame = false;

    private bool isGameActive = false;
    private bool isTransitioning = false;
    private bool isFirstPlay = true;
    private bool _isGameOver = false;
    
    private Vector2 originalMenuPosition;
    private float originalCameraBlendTime; 

    // State for the sub-panels' open status and animations
    private bool isSettingsOpen = false;
    private bool isLeaderboardOpen = false;
    private CanvasGroup settingsCG;
    private CanvasGroup leaderboardCG;
    private CanvasGroup deathContainerCG;
    private Coroutine settingsFadeCoroutine;
    private Coroutine leaderboardFadeCoroutine;
    private Coroutine _deathFadeCoroutine;
    private bool[] _hudWasVisible; // HUD bars' state before the pause (for restore on resume)

    private void Start()
    {
        if (menuUIContainer != null)
        {
            originalMenuPosition = menuUIContainer.anchoredPosition;
        }

        if (cameraBrain != null)
        {
            originalCameraBlendTime = cameraBrain.DefaultBlend.Time;
        }

        if (settingsPanel != null)
        {
            settingsCG = settingsPanel.GetComponent<CanvasGroup>();
            if (settingsCG == null) settingsCG = settingsPanel.AddComponent<CanvasGroup>();
        }

        if (leaderboardPanel != null)
        {
            leaderboardCG = leaderboardPanel.GetComponent<CanvasGroup>();
            if (leaderboardCG == null) leaderboardCG = leaderboardPanel.AddComponent<CanvasGroup>();
        }

        if (deathContainer != null)
        {
            deathContainerCG = deathContainer.GetComponent<CanvasGroup>();
            if (deathContainerCG == null) deathContainerCG = deathContainer.AddComponent<CanvasGroup>();
        }

        if (nameInputField != null && nameCharacterLimit > 0)
            nameInputField.characterLimit = nameCharacterLimit;

        CloseAllSubPanels(true);

        if (startDirectlyInGame)
        {
            // If we're in Act_02, tell the Manager we're already playing!
            isGameActive = true;
            isFirstPlay = false; // Skip the intro dialogue

            if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);
            if (menuCamera != null) menuCamera.SetActive(false);
            if (menuBlurVolume != null) menuBlurVolume.weight = 0f;

            // Unlock the player and hide the mouse instantly
            SetPlayerMovement(true);
        }
        else
        {
            // Normal behavior for Act_01 (menu open at startup)
            if (menuBlurVolume != null) menuBlurVolume.weight = 1f;
            SetPlayerMovement(false);
        }

        ApplyPauseButtonsState();
    }

    private void ApplyPauseButtonsState()
    {
        if (_isGameOver)
        {
            // Death screen: only Restart (no Resume/Play) + Death Container.
            if (playButton != null) playButton.SetActive(false);
            if (resumeButton != null) resumeButton.SetActive(false);
            if (restartButton != null) restartButton.SetActive(true);
            if (deathContainer != null) deathContainer.SetActive(true);
            return;
        }

        bool showPlay = isFirstPlay;
        if (playButton != null) playButton.SetActive(showPlay);
        if (resumeButton != null) resumeButton.SetActive(!showPlay);
        if (restartButton != null) restartButton.SetActive(!showPlay);
        if (deathContainer != null) deathContainer.SetActive(false);
    }

    private void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return;
        if (bookManager != null && bookManager.IsOpen) return;
        if (cinematicFallManager != null && cinematicFallManager.IsPlaying) return;
        if (jammoGuideController != null && jammoGuideController.IsWalking) return;

        // When dead the Game Over screen stays: Esc must not "resume".
        if (_isGameOver) return;

        if (!isTransitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isGameActive)
            {
                ReturnToMenu();
            }
            else if (!isFirstPlay)
            {
                PlayGame();
            }
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            StartCoroutine(ForceCursorState());
        }
    }

    private IEnumerator ForceCursorState()
    {
        yield return new WaitForEndOfFrame();

        if (isGameActive)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private IEnumerator LockCursorDeferred()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForEndOfFrame();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    // --- SUB-PANEL LOGIC WITH FADE ANIMATION ---

    /// <summary>Opens/closes the settings panel with a fade (closes the leaderboard if open).</summary>
    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;

        if (isSettingsOpen)
        {
            isSettingsOpen = false;
            if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
            settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, false));
        }
        else
        {
            isSettingsOpen = true;
            if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
            settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, true));
            
            if (isLeaderboardOpen) ToggleLeaderboardPanel(); 
        }
    }

    /// <summary>Opens/closes the leaderboard panel with a fade (closes the settings if open).</summary>
    public void ToggleLeaderboardPanel()
    {
        if (leaderboardPanel == null) return;

        if (isLeaderboardOpen)
        {
            isLeaderboardOpen = false;
            if (leaderboardFadeCoroutine != null) StopCoroutine(leaderboardFadeCoroutine);
            leaderboardFadeCoroutine = StartCoroutine(FadePanel(leaderboardPanel, leaderboardCG, false));
        }
        else
        {
            isLeaderboardOpen = true;
            if (leaderboardFadeCoroutine != null) StopCoroutine(leaderboardFadeCoroutine);
            leaderboardFadeCoroutine = StartCoroutine(FadePanel(leaderboardPanel, leaderboardCG, true));
            
            if (isSettingsOpen) ToggleSettingsPanel();
        }
    }

    private void CloseAllSubPanels(bool instant = false)
    {
        // --- SETTINGS HANDLING ---
        isSettingsOpen = false;
        if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
        
        if (instant) 
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (settingsCG != null) settingsCG.alpha = 0f; 
        }
        else if (settingsPanel != null) 
        {
            settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, false));
        }

        // --- LEADERBOARD HANDLING ---
        isLeaderboardOpen = false;
        if (leaderboardFadeCoroutine != null) StopCoroutine(leaderboardFadeCoroutine);
        
        if (instant) 
        {
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
            if (leaderboardCG != null) leaderboardCG.alpha = 0f; 
        }
        else if (leaderboardPanel != null)
        {
            leaderboardFadeCoroutine = StartCoroutine(FadePanel(leaderboardPanel, leaderboardCG, false));
        }
    }

    private IEnumerator FadePanel(GameObject panel, CanvasGroup cg, bool fadeIn)
    {
        if (cg == null) 
        {
            panel.SetActive(fadeIn);
            yield break;
        }

        if (fadeIn)
        {
            panel.SetActive(true);
            cg.blocksRaycasts = true; 
        }
        else
        {
            cg.blocksRaycasts = false; 
        }

        float elapsedTime = 0f;
        float startAlpha = cg.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;

        while (elapsedTime < panelFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / panelFadeDuration);
            yield return null;
        }

        cg.alpha = targetAlpha;

        if (!fadeIn)
        {
            panel.SetActive(false); 
        }
    }

    // ------------------------------

    /// <summary>Starts/resumes gameplay: locks the cursor and runs the menu→game transition. Wired to the Play/Resume buttons.</summary>
    public void PlayGame()
    {
        if (isGameActive || isTransitioning) return;

        Time.timeScale = 1f;

        // CLEAR FOCUS: tell Unity to "forget" the Play button just clicked
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // INSTANT FEEDBACK: hide and lock the cursor right away
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // If PlayGame comes from the Esc key, that same Esc releases the cursor lock
        // (built-in Editor behavior): re-assert the state for the frames after the Esc.
        StartCoroutine(LockCursorDeferred());

        CloseAllSubPanels(false);

        StartCoroutine(TransitionToGame());
    }

    /// <summary>Alias of <see cref="PlayGame"/> for the Resume button.</summary>
    public void ResumeGame()
    {
        PlayGame();
    }

    /// <summary>
    /// Called on Player death (wired in the Inspector to Health.onDied / PlayerHealth.onPlayerDied).
    /// Opens the menu as a Game Over screen: locks the Player, shows blur + timeScale=0 and the
    /// death button set (Restart + Save Score). TransitionToMenu calls ApplyPauseButtonsState
    /// afterward, which with _isGameOver=true shows the correct set.
    /// </summary>
    public void ShowGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        SetPlayerMovement(false);

        // The HUD bars fade out (the death screen must stay clean):
        // HideHudBars() inside TransitionToMenu() handles it.

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // The Death Container may have been faded out by a SaveScore from a previous
        // session in the same play (unlikely, but idempotent): restore the alpha so
        // TransitionToMenu re-enables it fully visible.
        if (deathContainerCG != null) deathContainerCG.alpha = 1f;

        StartCoroutine(TransitionToMenu());
    }

    /// <summary>
    /// Placeholder: the scoring system isn't implemented yet. Reads the name from the Death
    /// Container and logs it; the field fades out as "saved" feedback.
    /// </summary>
    public void SaveScore()
    {
        string playerName = (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            ? nameInputField.text.Trim()
            : "Anonymous";

        Debug.Log($"[Placeholder] Score for '{playerName}' — saving not implemented yet.");

        // Fade out the Death Container: FadePanel disables it at the end of the fade-out
        // and runs on unscaledDeltaTime (works with the game paused, timeScale=0).
        if (deathContainer != null)
        {
            if (_deathFadeCoroutine != null) StopCoroutine(_deathFadeCoroutine);
            _deathFadeCoroutine = StartCoroutine(FadePanel(deathContainer, deathContainerCG, false));
        }
    }

    /// <summary>Restarts the game by reloading Act_01 (resets timeScale and cursor first). Wired to the Restart button.</summary>
    public void RestartGame()
    {
        if (isTransitioning) return;

        // CRITICAL: without restoring timeScale to 1, the reloaded scene would start
        // frozen and the new Start() wouldn't unlock.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("Act_01");
    }

    /// <summary>Opens the pause menu (game→menu transition). Wired to Esc and to in-game menu buttons.</summary>
    public void ReturnToMenu()
    {
        if (isTransitioning) return;

        // INSTANT FEEDBACK: show the cursor right away when the menu opens
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(TransitionToMenu());
    }

    /// <summary>Quits the application (stops Play mode in the Editor).</summary>
    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private IEnumerator TransitionToGame()
    {
        isTransitioning = true;
        isGameActive = true;

        bool wasFirstPlay = isFirstPlay;

        float camDuration = wasFirstPlay ? originalCameraBlendTime : 0.5f;
        float uiDuration = wasFirstPlay ? 1.5f : 0.3f;

        if (cameraBrain != null)
        {
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(cameraBrain.DefaultBlend.Style, camDuration);
        }

        if (menuCamera != null) menuCamera.SetActive(false);

        if (!wasFirstPlay)
        {
            SetPlayerMovement(true);
            // Resume from pause: the HUD bars fade back in (only those that were visible
            // before the pause). On first play I do NOT reveal them: the scene director
            // handles it after the prologue (e.g. Act02Director).
            RevealHudBars();
        }

        float elapsedTime = 0f;
        Vector2 startPosition = menuUIContainer.anchoredPosition;
        Vector2 targetPosition = originalMenuPosition + new Vector2(-500f, 0f);

        while (elapsedTime < uiDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / uiDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(1f, 0f, smoothT);

            yield return null;
        }

        if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);

        // Safety fallback at the end of the transition
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        isFirstPlay = false;
        isTransitioning = false;

        if (wasFirstPlay)
        {
            if (playButton != null) playButton.SetActive(false);
            ApplyPauseButtonsState();
        }

        if (wasFirstPlay)
        {
            if (cameraBrain != null)
            {
                yield return new WaitWhile(() => cameraBrain.IsBlending);
            }

            if (introDialogue != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlayDialogue(introDialogue);
            }
            else
            {
                Debug.LogWarning("[MainMenuManager] introDialogue or DialogueManager.Instance is null on first play. Unlocking player as fallback.");
                SetPlayerMovement(true);
            }
        }
    }

    private IEnumerator TransitionToMenu()
    {
        isTransitioning = true;
        isGameActive = false;

        SetPlayerMovement(false);

        // The HUD bars fade away while the menu appears (Esc pause or death):
        // so they don't linger over the menu. The state is remembered for resume.
        HideHudBars();

        CloseAllSubPanels(true);

        if (cameraBrain != null) 
        {
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(cameraBrain.DefaultBlend.Style, 0.5f);
        }

        if (menuCamera != null) menuCamera.SetActive(true);
        if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(true);

        float duration = 0.3f; 
        float elapsedTime = 0f;
        
        Vector2 startPosition = menuUIContainer.anchoredPosition;
        Vector2 targetPosition = originalMenuPosition; 

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = elapsedTime / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(0f, 1f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(0f, 1f, smoothT);

            yield return null;
        }

        ApplyPauseButtonsState();

        if (EventSystem.current != null)
        {
            // In Game Over the focus goes to Restart (Resume is hidden); otherwise to Resume.
            GameObject focusTarget = _isGameOver ? restartButton : (!isFirstPlay ? resumeButton : null);
            if (focusTarget != null) EventSystem.current.SetSelectedGameObject(focusTarget);
        }

        isTransitioning = false;

        Time.timeScale = 0f;
    }

    // Snapshot the current visibility and fade out all HUD bars.
    private void HideHudBars()
    {
        if (hudBars == null) return;
        if (_hudWasVisible == null || _hudWasVisible.Length != hudBars.Length)
            _hudWasVisible = new bool[hudBars.Length];

        for (int i = 0; i < hudBars.Length; i++)
        {
            if (hudBars[i] == null) { _hudWasVisible[i] = false; continue; }
            _hudWasVisible[i] = hudBars[i].IsVisible;
            hudBars[i].Hide();
        }
    }

    // Fade in only the bars that were visible before the pause.
    private void RevealHudBars()
    {
        if (hudBars == null || _hudWasVisible == null) return;
        for (int i = 0; i < hudBars.Length; i++)
            if (hudBars[i] != null && i < _hudWasVisible.Length && _hudWasVisible[i]) hudBars[i].Reveal();
    }

    private void SetPlayerMovement(bool canMove)
    {
        if (player != null)
        {
            Behaviour thirdPersonScript = player.GetComponent("ThirdPersonController") as Behaviour;
            Behaviour playerInputScript = player.GetComponent("PlayerInput") as Behaviour;
            Behaviour starterInputsScript = player.GetComponent("StarterAssetsInputs") as Behaviour;
            Behaviour playerCombatScript = player.GetComponent("PlayerCombat") as Behaviour; 

            if (thirdPersonScript != null) thirdPersonScript.enabled = canMove;
            if (playerInputScript != null) playerInputScript.enabled = canMove;
            if (starterInputsScript != null) starterInputsScript.enabled = canMove;
            if (playerCombatScript != null) playerCombatScript.enabled = canMove; 
        }

        if (canMove == false) 
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else 
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}