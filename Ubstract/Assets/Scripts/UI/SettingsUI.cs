using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages the game settings interface, handling audio levels via AudioMixer, 
/// display preferences, and resolution switching with persistent data storage.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Audio Engine")]
    public AudioMixer audioMixer;

    [Header("Audio Controls")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle muteToggle;

    [Header("Display Controls")]
    public Toggle fullscreenToggle;
    public Toggle crtToggle;

    [Header("Resolution Selection")]
    public Button res1080Button;
    public Button res720Button;
    public Button res480Button;

    /// <summary>
    /// Initializes the UI elements with saved preferences upon script start.
    /// </summary>
    void Start()
    {
        SetupAudioUI();
        SetupVideoUI();
    }

    /// <summary>
    /// Retrieves saved audio settings and initializes sliders and toggles.
    /// Uses 'WithoutNotify' to prevent triggering feedback sounds during the setup phase.
    /// </summary>
    void SetupAudioUI()
    {
        float savedMusic = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float savedSFX = PlayerPrefs.GetFloat("SFXVolume", 1f);
        bool isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

        if (musicSlider != null) { musicSlider.SetValueWithoutNotify(savedMusic); musicSlider.onValueChanged.AddListener(SetMusicVolume); }
        if (sfxSlider != null) { sfxSlider.SetValueWithoutNotify(savedSFX); sfxSlider.onValueChanged.AddListener(SetSFXVolume); }
        if (muteToggle != null) { muteToggle.SetIsOnWithoutNotify(isMuted); muteToggle.onValueChanged.AddListener(SetMute); }

        SetMusicVolume(savedMusic);
        SetSFXVolume(savedSFX);
        SetMute(isMuted);
    }

    /// <summary>
    /// Synchronizes video toggles and resolution buttons with current hardware settings or saved preferences.
    /// </summary>
    void SetupVideoUI()
    {
        // 1. Fullscreen Persistence
        bool isFull = PlayerPrefs.GetInt("Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(isFull);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        SetFullscreen(isFull);

        // 2. CRT Overlay Filter
        bool isCrtOn = PlayerPrefs.GetInt("CRTFilter", 0) == 1;
        if (crtToggle != null)
        {
            crtToggle.SetIsOnWithoutNotify(isCrtOn);
            crtToggle.onValueChanged.AddListener(SetCRT);
        }
        SetCRT(isCrtOn);

        // 3. Resolution Configuration via Button Listeners
        if (res1080Button != null) res1080Button.onClick.AddListener(() => SetResolution(0));
        if (res720Button != null) res720Button.onClick.AddListener(() => SetResolution(1));
        if (res480Button != null) res480Button.onClick.AddListener(() => SetResolution(2));

        int savedRes = PlayerPrefs.GetInt("ResolutionIndex", 0);
        SetResolution(savedRes);
    }

    /// <summary>
    /// Updates the Music channel volume. 
    /// Converts linear slider values (0.0001 to 1) to logarithmic Decibel scale (-80dB to 0dB).
    /// </summary>
    public void SetMusicVolume(float value)
    {
        audioMixer.SetFloat("MusicVol", Mathf.Log10(value) * 20f);
        PlayerPrefs.SetFloat("MusicVolume", value);
    }

    /// <summary>
    /// Updates the SFX channel volume using logarithmic conversion.
    /// </summary>
    public void SetSFXVolume(float value)
    {
        audioMixer.SetFloat("SFXVol", Mathf.Log10(value) * 20f);
        PlayerPrefs.SetFloat("SFXVolume", value);
    }

    /// <summary>
    /// Toggles the global AudioListener volume between absolute silence and full output.
    /// </summary>
    public void SetMute(bool isMuted)
    {
        AudioListener.volume = isMuted ? 0f : 1f; 
        PlayerPrefs.SetInt("IsMuted", isMuted ? 1 : 0);
    }

    /// <summary>
    /// Manually commits all PlayerPrefs to disk.
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Toggles the application between windowed and fullscreen modes.
    /// </summary>
    public void SetFullscreen(bool isFull)
    {
        Screen.fullScreen = isFull;
        PlayerPrefs.SetInt("Fullscreen", isFull ? 1 : 0);
    }

    /// <summary>
    /// Updates the CRT filter state globally by finding all active/inactive CRTControllers in the scene.
    /// </summary>
    public void SetCRT(bool isOn)
    {
        PlayerPrefs.SetInt("CRTFilter", isOn ? 1 : 0);
        PlayerPrefs.Save();

        CRTController[] filters = Object.FindObjectsByType<CRTController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach(var filter in filters)
        {
            filter.UpdateFilter(isOn);
        }
    }

    /// <summary>
    /// Switches the display resolution based on the selected index and updates UI feedback.
    /// </summary>
    /// <param name="index">Resolution Index: 0 = 1080p, 1 = 720p, 2 = 480p.</param>
    public void SetResolution(int index)
    {
        if (index == 0) Screen.SetResolution(1920, 1080, Screen.fullScreen);
        else if (index == 1) Screen.SetResolution(1280, 720, Screen.fullScreen);
        else if (index == 2) Screen.SetResolution(854, 480, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", index);
        
        UpdateButtonVisuals(index);
    }

    /// <summary>
    /// Adjusts the interactable state of resolution buttons to highlight the current selection.
    /// </summary>
    private void UpdateButtonVisuals(int activeIndex)
    {
        if (res1080Button != null) res1080Button.interactable = (activeIndex != 0);
        if (res720Button != null) res720Button.interactable = (activeIndex != 1);
        if (res480Button != null) res480Button.interactable = (activeIndex != 2);
    }
}