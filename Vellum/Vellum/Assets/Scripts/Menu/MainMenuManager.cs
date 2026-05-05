using UnityEngine;
using UnityEngine.Rendering; 
using System.Collections;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.EventSystems; 

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

    private bool isGameActive = false;
    private bool isTransitioning = false; 
    private bool isFirstPlay = true; 
    
    private Vector2 originalMenuPosition;
    private float originalCameraBlendTime; 

    // Variabili per gestire lo stato di apertura e le animazioni
    private bool isSettingsOpen = false;
    private bool isLeaderboardOpen = false;
    private CanvasGroup settingsCG;
    private CanvasGroup leaderboardCG;
    private Coroutine settingsFadeCoroutine;
    private Coroutine leaderboardFadeCoroutine;

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

        // Prepariamo i pannelli per l'animazione aggiungendo in automatico il CanvasGroup se manca
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

        SetPlayerMovement(false);
        CloseAllSubPanels(true); // Chiudiamo i sottomenu istantaneamente all'avvio
    }

    private void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return;

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

    // --- LOGICA SOTTOMENU CON ANIMAZIONE FADE ---

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null) return;

        if (isSettingsOpen)
        {
            // Se è aperto, chiudilo con l'animazione
            isSettingsOpen = false;
            if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
            settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, false));
        }
        else
        {
            // Se è chiuso, aprilo con l'animazione
            isSettingsOpen = true;
            if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
            settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, true));
            
            // Se la leaderboard è aperta, chiudiamola
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

    /// <summary>
    /// Chiude tutti i sottomenu. Se 'instant' è true, lo fa senza animazione.
    /// </summary>
    private void CloseAllSubPanels(bool instant = false)
    {
        if (isSettingsOpen)
        {
            isSettingsOpen = false;
            if (settingsFadeCoroutine != null) StopCoroutine(settingsFadeCoroutine);
            
            if (instant) 
            {
                settingsPanel.SetActive(false);
                if (settingsCG != null) settingsCG.alpha = 0f;
            }
            else settingsFadeCoroutine = StartCoroutine(FadePanel(settingsPanel, settingsCG, false));
        }

        if (isLeaderboardOpen)
        {
            isLeaderboardOpen = false;
            if (leaderboardFadeCoroutine != null) StopCoroutine(leaderboardFadeCoroutine);
            
            if (instant) 
            {
                leaderboardPanel.SetActive(false);
                if (leaderboardCG != null) leaderboardCG.alpha = 0f;
            }
            else leaderboardFadeCoroutine = StartCoroutine(FadePanel(leaderboardPanel, leaderboardCG, false));
        }
    }

    // Coroutine magica che gestisce l'effetto dissolvenza
    private IEnumerator FadePanel(GameObject panel, CanvasGroup cg, bool fadeIn)
    {
        if (cg == null) 
        {
            panel.SetActive(fadeIn); // Fallback di sicurezza
            yield break;
        }

        if (fadeIn)
        {
            panel.SetActive(true);
            cg.blocksRaycasts = true; // Permette di cliccare i pulsanti
        }
        else
        {
            cg.blocksRaycasts = false; // Impedisce i click mentre il pannello sta scomparendo
        }

        float elapsedTime = 0f;
        float startAlpha = cg.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;

        while (elapsedTime < panelFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / panelFadeDuration);
            yield return null;
        }

        cg.alpha = targetAlpha;

        if (!fadeIn)
        {
            panel.SetActive(false); // Spegne l'oggetto completamente alla fine dell'uscita
        }
    }

    // ------------------------------

    public void PlayGame()
    {
        if (isGameActive || isTransitioning) return; 
        
        // MODIFICATO: Chiudiamo i pannelli (con animazione) appena clicchi Play!
        CloseAllSubPanels(false); 

        StartCoroutine(TransitionToGame());
    }

    public void ReturnToMenu()
    {
        if (isTransitioning) return;

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

        // Sul primo play teniamo il player bloccato fino alla fine del dialogo intro.
        if (!wasFirstPlay)
        {
            SetPlayerMovement(true);
        }

        float elapsedTime = 0f;
        Vector2 startPosition = menuUIContainer.anchoredPosition;
        Vector2 targetPosition = originalMenuPosition + new Vector2(-500f, 0f);

        while (elapsedTime < uiDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / uiDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(1f, 0f, smoothT);

            yield return null;
        }

        if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);

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
        
        // Quando torniamo al menu principale (es. premendo Esc), assicuriamoci che i sottomenu siano puliti
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
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(0f, 1f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(0f, 1f, smoothT);

            yield return null;
        }

        isTransitioning = false; 
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