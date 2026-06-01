using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Cameras")]
    public GameObject menuCamera;
    [Tooltip("Trascina qui la Main Camera che contiene il Cinemachine Brain")]
    public CinemachineBrain cameraBrain; 

    [Header("UI Elements")]
    public RectTransform menuUIContainer;
    public CanvasGroup menuCanvasGroup;

    [Header("Sub-Panels (Opzionali)")]
    public GameObject settingsPanel;
    public GameObject leaderboardPanel;

    [Header("Pulsanti")]
    [Tooltip("Pulsante \"Play\" mostrato solo al primo avvio. Disattivato dopo la prima transizione.")]
    [SerializeField] private GameObject playButton;
    [Tooltip("Pulsante \"Resume\" mostrato in pausa (dalla seconda apertura del menu in poi).")]
    [SerializeField] private GameObject resumeButton;
    [Tooltip("Pulsante \"Restart\" mostrato in pausa accanto a Resume. Ricarica Act_01.")]
    [SerializeField] private GameObject restartButton;

    [Header("Game Over")]
    [Tooltip("Canvas \"Death Container\": nome + Save Score. Attivo solo alla morte del Player.")]
    [SerializeField] private GameObject deathContainer;
    [Tooltip("Campo nome dentro il Death Container, letto da SaveScore().")]
    [SerializeField] private TMP_InputField nameInputField;
    [Tooltip("Numero massimo di caratteri inseribili nel campo nome.")]
    [SerializeField] private int nameCharacterLimit = 10;
    [Tooltip("Barre HUD (PlayerHUD, StatueProgressBar, ...) da nascondere in dissolvenza quando si apre il menu (pausa Esc) e alla morte del Player; rientrano al resume.")]
    [FormerlySerializedAs("hudHideOnDeath")]
    [SerializeField] private HudReveal[] hudBars;
    
    [Header("Impostazioni Animazioni UI")]
    [Tooltip("Durata in secondi dell'effetto sfumatura (Fade) dei pannelli laterali")]
    public float panelFadeDuration = 0.25f;

    [Header("Post Processing")]
    public Volume menuBlurVolume;

    [Header("Player Reference")]
    public GameObject player;

    [Header("Intro")]
    [Tooltip("Dialogo che parte al primo Play, dopo che la camera si è fermata sul player")]
    [SerializeField] private DialogueAsset introDialogue;

    [Header("Blocco Esc durante gameplay")]
    [Tooltip("Riferimenti opzionali: se assegnati, l'Esc verso il menu è bloccato mentre questi sono attivi")]
    [SerializeField] private BookManager bookManager;
    [SerializeField] private CinematicFallManager cinematicFallManager;
    [SerializeField] private JammoGuideController jammoGuideController;

    [Header("Impostazioni Livello")]
    [Tooltip("Spunta questa casella se sei in un livello avanzato (es. Act_02) per saltare la schermata del titolo")]
    public bool startDirectlyInGame = false;

    private bool isGameActive = false;
    private bool isTransitioning = false;
    private bool isFirstPlay = true;
    private bool _isGameOver = false;
    
    private Vector2 originalMenuPosition;
    private float originalCameraBlendTime; 

    // Variabili per gestire lo stato di apertura e le animazioni
    private bool isSettingsOpen = false;
    private bool isLeaderboardOpen = false;
    private CanvasGroup settingsCG;
    private CanvasGroup leaderboardCG;
    private CanvasGroup deathContainerCG;
    private Coroutine settingsFadeCoroutine;
    private Coroutine leaderboardFadeCoroutine;
    private Coroutine _deathFadeCoroutine;
    private bool[] _hudWasVisible; // stato delle barre HUD prima della pausa (per il restore al resume)

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

        // --- LA NUOVA LOGICA ---
        if (startDirectlyInGame)
        {
            // Se siamo in Act_02, diciamo al Manager che stiamo già giocando!
            isGameActive = true;
            isFirstPlay = false; // Saltiamo il dialogo iniziale

            if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);
            if (menuCamera != null) menuCamera.SetActive(false);
            if (menuBlurVolume != null) menuBlurVolume.weight = 0f;

            // Sblocca il player e nascondi il mouse istantaneamente
            SetPlayerMovement(true);
        }
        else
        {
            // Comportamento normale per Act_01 (Menu aperto all'avvio)
            if (menuBlurVolume != null) menuBlurVolume.weight = 1f;
            SetPlayerMovement(false);
        }

        ApplyPauseButtonsState();
    }

    private void ApplyPauseButtonsState()
    {
        if (_isGameOver)
        {
            // Schermata di morte: solo Restart (niente Resume/Play) + Death Container.
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

        // Da morto la schermata di Game Over resta: l'Esc non deve far "resume".
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

    // --- LOGICA SOTTOMENU CON ANIMAZIONE FADE ---

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
        // --- GESTIONE SETTINGS ---
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

        // --- GESTIONE LEADERBOARD ---
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

    public void PlayGame()
    {
        if (isGameActive || isTransitioning) return;

        Time.timeScale = 1f;

        // RIMOZIONE FOCUS: Diciamo a Unity di "dimenticare" il tasto Play appena cliccato
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // FEEDBACK ISTANTANEO: Nascondiamo e blocchiamo il cursore subito
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Se PlayGame arriva dal tasto Esc, lo stesso Esc fa rilasciare il lock
        // del cursore (comportamento integrato dell'Editor): ri-affermiamo lo
        // stato per i frame successivi a quello dell'Esc.
        StartCoroutine(LockCursorDeferred());

        CloseAllSubPanels(false);

        StartCoroutine(TransitionToGame());
    }

    public void ResumeGame()
    {
        PlayGame();
    }

    // Chiamato dalla morte del Player (wirato da Inspector su Health.onDied /
    // PlayerHealth.onPlayerDied). Apre il menu come schermata di Game Over:
    // blocca il Player, mostra blur + timeScale=0 e il set di pulsanti morte
    // (Restart + Salva Punteggio). TransitionToMenu chiama ApplyPauseButtonsState
    // in coda, che con _isGameOver=true mostra il set corretto.
    public void ShowGameOver()
    {
        if (_isGameOver) return;
        _isGameOver = true;

        SetPlayerMovement(false);

        // Le barre HUD scompaiono in dissolvenza (la schermata di morte deve
        // restare pulita): ci pensa HideHudBars() dentro TransitionToMenu().

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Il Death Container potrebbe essere stato dissolto da un SaveScore di
        // una sessione precedente nello stesso play (improbabile, ma idempotente):
        // ripristina l'alpha così TransitionToMenu lo riattiva pienamente visibile.
        if (deathContainerCG != null) deathContainerCG.alpha = 1f;

        StartCoroutine(TransitionToMenu());
    }

    // Placeholder: il sistema di punteggi non è ancora implementato. Legge il
    // nome dal Death Container e lo logga; il campo diventa non-interattivo
    // come feedback di "salvato".
    public void SaveScore()
    {
        string playerName = (nameInputField != null && !string.IsNullOrWhiteSpace(nameInputField.text))
            ? nameInputField.text.Trim()
            : "Anonimo";

        Debug.Log($"[Placeholder] Punteggio di '{playerName}' — salvataggio non ancora implementato.");

        // Dissolvenza del Death Container: FadePanel lo disattiva a fine fade-out
        // e gira su unscaledDeltaTime (funziona col gioco in pausa, timeScale=0).
        if (deathContainer != null)
        {
            if (_deathFadeCoroutine != null) StopCoroutine(_deathFadeCoroutine);
            _deathFadeCoroutine = StartCoroutine(FadePanel(deathContainer, deathContainerCG, false));
        }
    }

    public void RestartGame()
    {
        if (isTransitioning) return;

        // CRITICO: senza riportare timeScale a 1, la scena ricaricata
        // nascerebbe ferma e il nuovo Start() non si sbloccherebbe.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene("Act_01");
    }

    public void ReturnToMenu()
    {
        if (isTransitioning) return;

        // FEEDBACK ISTANTANEO: Mostriamo il cursore subito quando si apre il menu
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(TransitionToMenu());
    }

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
            // Resume da pausa: le barre HUD rientrano in dissolvenza (solo quelle
            // che erano visibili prima della pausa). Sul first play NON le rivelo:
            // ci pensa il regista di scena dopo il prologo (es. Act02Director).
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

        // Fallback di sicurezza a fine transizione
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

        // Le barre HUD sfumano via mentre appare il menu (pausa Esc o morte):
        // così non restano sopra il menu. Lo stato viene ricordato per il resume.
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
            // In Game Over il focus va su Restart (Resume è nascosto); altrimenti su Resume.
            GameObject focusTarget = _isGameOver ? restartButton : (!isFirstPlay ? resumeButton : null);
            if (focusTarget != null) EventSystem.current.SetSelectedGameObject(focusTarget);
        }

        isTransitioning = false;

        Time.timeScale = 0f;
    }

    // Snapshot della visibilità corrente e fade-out di tutte le barre HUD.
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

    // Fade-in solo delle barre che erano visibili prima della pausa.
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