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

    [Header("Audio Engine")]
    public UnityEngine.Audio.AudioMixer mainMixer;

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

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        LoadPreferences();
        LoadLevel(0);

        StartCoroutine(FirstLevelIntroRoutine());
    }

    /// <summary>
    /// CORE MECHANIC: Safely disables or enables player movement and physics during cinematic transitions.
    /// </summary>
    private void TogglePlayerControls(bool isActive)
    {
        if (playerTransform == null) return;

        GameObject player = playerTransform.gameObject;
        MonoBehaviour pMovements = (MonoBehaviour)player.GetComponent("PlayerMovements");
        MonoBehaviour pCombat = (MonoBehaviour)player.GetComponent("PlayerCombat");
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (pMovements != null) pMovements.enabled = isActive;
        if (pCombat != null) pCombat.enabled = isActive;

        if (rb != null)
        {
            if (!isActive)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }

    private IEnumerator FirstLevelIntroRoutine()
    {
        // 1. BLOCCA IL GIOCATORE
        TogglePlayerControls(false); 

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
        
        TogglePlayerControls(true); 
    }

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


    public void PauseGame()
    {
        if (gameOverPanel != null && gameOverPanel.activeSelf) return;

        pausePanel.SetActive(true);
        if (pauseMenuContent != null) pauseMenuContent.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Time.timeScale = 0f; 
        isPaused = true;
    }

    public void ResumeGame()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f; 
        isPaused = false;
    }

    public void OpenSettings()
    {
        if (pauseMenuContent != null) pauseMenuContent.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseMenuContent != null) pauseMenuContent.SetActive(true);
    }

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

    private IEnumerator TransitionToNextLevelRoutine()
    {
        // 1. BLOCCA IL GIOCATORE mentre lo schermo diventa nero
        TogglePlayerControls(false); 

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

        // 2. SBLOCCA IL GIOCATORE 
        TogglePlayerControls(true); 
    }

    private IEnumerator ShowVictoryPanelRoutine()
    {
        TogglePlayerControls(false);

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

    public void GameOver()
    {
        if (gameOverPanel != null)
        {
            if (GameAudioManager.instance != null)
            {
                GameAudioManager.instance.StopAllMusic();
            }
            TogglePlayerControls(false);

            if (DataManager.instance != null) DataManager.instance.FinalizeAndSaveMatch();
            
            gameOverPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        if (GameAudioManager.instance != null)
        {
            GameAudioManager.instance.StopAllMusic();
        }
        
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
        {
            string oldName = DataManager.instance.currentMatchData.playerName;
            DataManager.instance.StartNewMatch(oldName); 
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
    }

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
    /// Retrieves saved game settings from PlayerPrefs and applies them to the AudioMixer.
    /// This ensures volumes are correct instantly upon loading the arena.
    /// </summary>
    public void LoadPreferences()
    {
        float music = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float sfx = PlayerPrefs.GetFloat("SFXVolume", 1f); // CORRETTO: rinominato in 'sfx'
        bool isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        AudioListener.volume = isMuted ? 0f : 1f;

        if (mainMixer != null)
        {
            // Applica la conversione logaritmica ai decibel per l'AudioMixer
            mainMixer.SetFloat("MusicVol", Mathf.Log10(Mathf.Max(0.0001f, music)) * 20f);
            mainMixer.SetFloat("SFXVol", Mathf.Log10(Mathf.Max(0.0001f, sfx)) * 20f);
            Debug.Log("GameManager: Preferences loaded and applied to AudioMixer.");
        }
        else
        {
            Debug.LogWarning("GameManager: MainMixer non assegnato nell'Inspector!");
        }
    }
}