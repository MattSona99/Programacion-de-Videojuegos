using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections; 
using TMPro;

/// <summary>
/// Manages the Main Menu navigation, including UI panel transitions (Fading), 
/// player name validation, and coordinating the asynchronous hand-off to the game scene.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Main Menu")]
    public GameObject mainMenuButtons;

    [Header("Panels")]
    public GameObject logoUbstract;
    public GameObject characterSelectionPanel;
    public GameObject settingsPanel;
    public GameObject controlsPanel;
    public GameObject leaderboardPanel;

    [Header("Transition Settings")]
    public float fadeDuration = 0.3f;
    private bool isFading = false;

    [Tooltip("Delay in seconds before loading the game scene, synchronized with TransitionManager animations.")]
    public float sceneLoadDelay = 1.0f;

    [Header("Player Setup")]
    [Tooltip("Input field for player name.")]
    public TMP_InputField nameInputField;
    
    [Tooltip("Text element used to display validation errors like 'Name already exists'.")]
    public TextMeshProUGUI errorText;

    private Coroutine hideErrorCoroutine;

    /// <summary>
    /// Initializes the Main Menu UI and ensures the logo and main menu panels are visible.
    /// </summary>
    void Start()
    {
        if (logoUbstract != null) logoUbstract.SetActive(true);
        if (mainMenuButtons != null) mainMenuButtons.SetActive(true);
    }

    /// <summary>
    /// Processes the start game request. Validates the input name against the 
    /// DataManager's record list before initiating the scene transition.
    /// </summary>
    public void StartGame()
    {
        string chosenName = nameInputField != null ? nameInputField.text.Trim() : "";

        // Validate that the field is not empty or using default placeholder text
        if (string.IsNullOrWhiteSpace(chosenName) || chosenName.ToLower() == "enter your name...")
        {
            if (MenuAudioManager.instance != null) MenuAudioManager.instance.PlayBattleSoundError();
            ShowErrorMessage("YOU MUST ENTER A NAME!", Color.black);
            return; 
        }

        chosenName = chosenName.ToUpper(); 

        // Check if the DataManager is active to verify unique name constraints
        if (DataManager.instance != null)
        {
            if (DataManager.instance.CheckIfNameExists(chosenName))
            {
                if (MenuAudioManager.instance != null) MenuAudioManager.instance.PlayBattleSoundError();
                ShowErrorMessage("NAME ALREADY TAKEN! CHOOSE ANOTHER.", Color.black);
                return; 
            }
            else
            {
                if (MenuAudioManager.instance != null) MenuAudioManager.instance.PlayBattleSound();
                if (errorText != null) errorText.text = "";
                DataManager.instance.StartNewMatch(chosenName);
            }
        }
        else
        {
            // Fallback to PlayerPrefs if the global DataManager is missing
            if (MenuAudioManager.instance != null) MenuAudioManager.instance.PlayBattleSound();
            Debug.LogWarning("DataManager not found! Falling back to basic PlayerPrefs.");
            PlayerPrefs.SetString("PlayerName", chosenName);
            PlayerPrefs.Save();
        }

        // Initiate transition to the Arena scene
        if (TransitionManager.instance != null)
        {
            StartCoroutine(ToBattleRoutine());
        }
        else
        {
            SceneManager.LoadScene("02_SpaceArena"); 
        }
    }

    /// <summary>
    /// Displays a temporary error message on the UI and manages its lifecycle.
    /// </summary>
    private void ShowErrorMessage(string message, Color color)
    {
        if (errorText == null) return;

        // Reset any existing hide timers to avoid overlapping visibility logic
        if (hideErrorCoroutine != null)
        {
            StopCoroutine(hideErrorCoroutine);
        }

        hideErrorCoroutine = StartCoroutine(HideErrorRoutine(message, color));
    }

    /// <summary>
    /// Coroutine that manages the visibility duration of the error text.
    /// </summary>
    private IEnumerator HideErrorRoutine(string message, Color color)
    {
        errorText.text = message;
        errorText.color = color;

        yield return new WaitForSeconds(2.5f);

        errorText.text = "";
    }

    /// <summary>
    /// Coordinates audio fade-out and visual transition animations before loading the next scene.
    /// </summary>
    private IEnumerator ToBattleRoutine()
    {
        if (MenuAudioManager.instance != null)
        {
            // Diminish menu BGM volume over a fixed duration
            MenuAudioManager.instance.FadeOutMusic(1.5f); 
        }

        // Wait specifically for the TransitionManager's fade-out sequence to finish
        yield return StartCoroutine(TransitionManager.instance.FadeOutRoutine());
        
        SceneManager.LoadScene("02_SpaceArena");
    }

    /// <summary>
    /// Terminates the application and handles editor state if applicable.
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    public void OpenCharacterSelection() 
    {
        if (errorText != null) errorText.text = ""; 
        StartCoroutine(FadeTransition(mainMenuButtons, characterSelectionPanel)); 
        StartCoroutine(FocusNameInputDelay());
    }

    /// <summary>
    /// Delays the input field focus to allow the UI transition to complete, 
    /// ensuring the keyboard cursor is correctly placed.
    /// </summary>
    private IEnumerator FocusNameInputDelay()
    {
        yield return new WaitForSeconds(fadeDuration);
        if (nameInputField != null)
        {
            nameInputField.Select();
            nameInputField.ActivateInputField();
        }
    }

    public void OpenSettings() { StartCoroutine(FadeTransition(mainMenuButtons, settingsPanel)); }
    public void OpenControls() { StartCoroutine(FadeTransition(mainMenuButtons, controlsPanel)); }
    public void OpenLeaderboard() { StartCoroutine(FadeTransition(mainMenuButtons, leaderboardPanel)); }
    public void BackToMainMenuFromCharacterSelect() { StartCoroutine(FadeTransition(characterSelectionPanel, mainMenuButtons)); }
    public void BackToMainMenuFromSettings() { StartCoroutine(FadeTransition(settingsPanel, mainMenuButtons)); }
    public void BackToMainMenuFromControls() { StartCoroutine(FadeTransition(controlsPanel, mainMenuButtons)); }
    public void BackToMainMenuFromLeaderboard() { StartCoroutine(FadeTransition(leaderboardPanel, mainMenuButtons)); }

    /// <summary>
    /// Performs a cross-fade between two UI panels using their CanvasGroup alpha values.
    /// </summary>
    private IEnumerator FadeTransition(GameObject panelOut, GameObject panelIn)
    {
        if (isFading) yield break;
        isFading = true;

        CanvasGroup outGroup = panelOut.GetComponent<CanvasGroup>();
        CanvasGroup inGroup = panelIn.GetComponent<CanvasGroup>();

        if (logoUbstract != null)
        {
            bool isReturningToMain = (panelIn == mainMenuButtons);
            logoUbstract.SetActive(isReturningToMain);
        }

        // Fade Out the exiting panel
        if (outGroup != null)
        {
            float time = 0;
            while (time < fadeDuration)
            {
                outGroup.alpha = Mathf.Lerp(1f, 0f, time / fadeDuration);
                time += Time.deltaTime;
                yield return null;
            }
            outGroup.alpha = 0f;
        }
        
        panelOut.SetActive(false); 
        panelIn.SetActive(true);
        
        // Fade In the entering panel
        if (inGroup != null)
        {
            inGroup.alpha = 0f;
            float time = 0;
            while (time < fadeDuration)
            {
                inGroup.alpha = Mathf.Lerp(0f, 1f, time / fadeDuration);
                time += Time.deltaTime;
                yield return null;
            }
            inGroup.alpha = 1f;
        }

        isFading = false;
    }
}