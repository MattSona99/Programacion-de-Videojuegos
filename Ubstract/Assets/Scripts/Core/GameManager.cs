using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Centralized GameManager responsible for orchestrating level progression, 
/// UI states (Pause, Settings, Game Over), and coordinating gameplay pauses 
/// during asynchronous screen transitions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Settings")]
    public float globalVolume = 1f;
    public int timeLimit = 60;
    public int playerLives = 3;

    [Header("Music Settings")]
    public AudioClip[] levelMusicTracks;

    [Header("Progression")]
    public int currentLevel = 1;
    public int currentScore = 0;

    [Header("Victory UI")]
    public GameObject victoryPanel;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    [Header("Pause UI")]
    public GameObject pausePanel;
    
    [Header("Nested Menus")]
    public GameObject pauseMenuContent; 
    public GameObject settingsPanel;    
    
    [Header("Enemy UI")]
    [Tooltip("Drag and drop the EnemyUIPanel containing the CanvasGroup component here.")]
    public CanvasGroup enemyUIGroup; 

    private bool isPaused = false;

    [Header("Level Management")]
    public GameObject[] levelPrefabs;
    public Transform playerTransform;

    private GameObject currentLevelInstance;

    /// <summary>
    /// Initializes the Singleton instance.
    /// </summary>
    void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// Loads saved player preferences, initializes the first level, 
    /// and begins the intro transition sequence.
    /// </summary>
    void Start()
    {
        LoadPreferences();
        LoadLevel(0);

        StartCoroutine(FirstLevelIntroRoutine());
    }

    /// <summary>
    /// Handles the visual and logical sequence for the initial level load.
    /// Disables enemy logic and hides UI to prevent premature attacks during the screen fade.
    /// </summary>
    private IEnumerator FirstLevelIntroRoutine()
    {
        if (enemyUIGroup != null) enemyUIGroup.alpha = 0f;

        EnemyHealth enemy = null;
        if (currentLevelInstance != null)
        {
            enemy = currentLevelInstance.GetComponentInChildren<EnemyHealth>();
            if (enemy != null) enemy.gameObject.SetActive(false);
        }

        if (TransitionManager.instance != null)
        {
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(TransitionManager.instance.AnimateLevelTextRoutine(1));
            yield return StartCoroutine(TransitionManager.instance.FadeInRoutine());
        }

        if (enemy != null) enemy.gameObject.SetActive(true);
        if (enemyUIGroup != null) enemyUIGroup.alpha = 1f;
    }

    /// <summary>
    /// Listens for the Escape key to toggle the pause menu or close nested setting panels.
    /// </summary>
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings(); 
                else ResumeGame(); 
            }
            else PauseGame();
        }
    }

    /// <summary>
    /// Freezes the game's time scale and activates the pause UI overlay.
    /// </summary>
    public void PauseGame()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;

        pausePanel.SetActive(true);
        if (pauseMenuContent != null) pauseMenuContent.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Time.timeScale = 0f; 
        isPaused = true;
    }

    /// <summary>
    /// Restores the game's time scale and deactivates the pause UI overlay.
    /// </summary>
    public void ResumeGame()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f; 
        isPaused = false;
    }

    /// <summary>
    /// Swaps the active UI panel within the pause menu to the settings screen.
    /// </summary>
    public void OpenSettings()
    {
        if (pauseMenuContent != null) pauseMenuContent.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    /// <summary>
    /// Closes the settings screen and returns to the main pause menu layout.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseMenuContent != null) pauseMenuContent.SetActive(true);
    }

    /// <summary>
    /// Destroys the current arena, instantiates the requested level prefab, 
    /// positions the player, and updates camera target references.
    /// </summary>
    /// <param name="levelIndex">The zero-based index of the level to load.</param>
    public void LoadLevel(int levelIndex)
    {
        if (levelIndex >= levelPrefabs.Length)
        {
            Debug.Log("Game Won!");
            if (DataManager.instance != null) DataManager.instance.FinalizeAndSaveMatch();
            GoToMainMenu();
            return;
        }

        if (GameAudioManager.instance != null && levelIndex < levelMusicTracks.Length)
        {
            GameAudioManager.instance.ChangeMusic(levelMusicTracks[levelIndex]);
        }

        if (currentLevelInstance != null) Destroy(currentLevelInstance);

        currentLevelInstance = Instantiate(levelPrefabs[levelIndex]);
        currentLevel = levelIndex + 1;

        Transform spawnPoint = currentLevelInstance.transform.Find("SpawnPointLeft");
        if (spawnPoint != null && playerTransform != null) playerTransform.position = spawnPoint.position;
        
        EnemyHealth newEnemyScript = currentLevelInstance.GetComponentInChildren<EnemyHealth>();
        if (newEnemyScript != null)
        {
            DynamicCamera scriptCamera = Camera.main.GetComponent<DynamicCamera>();
            if (scriptCamera != null) scriptCamera.SetEnemy(newEnemyScript.transform);
        }
    }

    /// <summary>
    /// Triggered by the EnemyHealth system upon boss death. Grants score, hides UI, 
    /// and initiates the transition to the next arena.
    /// </summary>
    public void OnEnemyDefeated()
    {
        currentScore += 1000;

        if (enemyUIGroup != null) enemyUIGroup.alpha = 0f;

        if (currentLevel >= levelPrefabs.Length)
        {
            StartCoroutine(ShowVictoryPanelRoutine());
        }
        else
        {
            if (TransitionManager.instance != null)
            {
                TransitionManager.instance.FadeOut();
                StartCoroutine(TransitionToNextLevelRoutine());
            }
            else
            {
                LoadLevel(currentLevel);
            }
        }
    }

    /// <summary>
    /// Coordinates the inter-level visual transitions. Keeps the newly spawned enemy 
    /// disabled during the fade-in to prevent premature attacks or physics updates.
    /// </summary>
    private IEnumerator TransitionToNextLevelRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        yield return StartCoroutine(TransitionManager.instance.AnimateLevelTextRoutine(currentLevel + 1));
        
        LoadLevel(currentLevel);

        EnemyHealth nextEnemy = null;
        if (currentLevelInstance != null)
        {
            nextEnemy = currentLevelInstance.GetComponentInChildren<EnemyHealth>();
            if (nextEnemy != null) nextEnemy.gameObject.SetActive(false);
        }

        if (TransitionManager.instance != null)
        {
            yield return StartCoroutine(TransitionManager.instance.FadeInRoutine());
        }

        if (nextEnemy != null) nextEnemy.gameObject.SetActive(true);
        if (enemyUIGroup != null) enemyUIGroup.alpha = 1f;
    }

    /// <summary>
    /// Handles the visual and logical sequence for the final level load.
    /// </summary>
    private IEnumerator ShowVictoryPanelRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (TransitionManager.instance != null)
        {
            TransitionManager.instance.FadeOut();
            yield return new WaitForSeconds(1f);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            if (DataManager.instance != null) DataManager.instance.FinalizeAndSaveMatch();
            Time.timeScale = 0f;
        }

    }

    /// <summary>
    /// Commits the current game settings to PlayerPrefs for persistence across sessions.
    /// </summary>
    public void SavePreferences(float volume, int time, int lives)
    {
        globalVolume = volume;
        timeLimit = time;
        playerLives = lives;

        PlayerPrefs.SetFloat("globalVolume", globalVolume);
        PlayerPrefs.SetInt("timeLimit", timeLimit);
        PlayerPrefs.SetInt("playerLives", playerLives);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Halts gameplay execution, saves final match statistics, and displays the Game Over screen.
    /// </summary>
    public void GameOver()
    {
        if (gameOverPanel != null)
        {
            if (DataManager.instance != null) DataManager.instance.FinalizeAndSaveMatch();
            
            gameOverPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// Restores time scale and restarts the active scene, carrying over the current player's data profile.
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;

        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
        {
            string oldName = DataManager.instance.currentMatchData.playerName;
            DataManager.instance.StartNewMatch(oldName); 
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
    }

    /// <summary>
    /// Restores time scale, finalizes match data if actively playing, and returns to the main menu.
    /// </summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;

        if (GameAudioManager.instance != null)
        {
            GameAudioManager.instance.StopAllMusic();
        }

        if (DataManager.instance != null && !gameOverPanel.activeSelf) DataManager.instance.FinalizeAndSaveMatch();
        SceneManager.LoadScene("01_MainMenu"); 
    }

    /// <summary>
    /// Retrieves saved game settings from PlayerPrefs and applies them (e.g., global audio volume).
    /// </summary>
    public void LoadPreferences()
    {
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 1f);
        bool isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;
        AudioListener.volume = isMuted ? 0f : 1f;
    }
}