using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the visual state of the CRT post-processing overlay.
/// Handles persistent user preferences for screen filters by interfacing with PlayerPrefs.
/// </summary>
public class CRTController : MonoBehaviour
{
    private Image crtImage;

    /// <summary>
    /// Caches the Image component reference used for the visual filter overlay.
    /// </summary>
    void Awake()
    {
        crtImage = GetComponent<Image>();
    }

    /// <summary>
    /// Synchronizes the filter state with the user's saved preferences upon scene initialization.
    /// Defaults to "off" (0) if no preference is found.
    /// </summary>
    void Start()
    {
        // Evaluates the integer preference as a boolean (0 = disabled, 1 = enabled)
        bool isCrtOn = PlayerPrefs.GetInt("CRTFilter", 0) == 1;
        UpdateFilter(isCrtOn);
    }

    /// <summary>
    /// Toggles the visibility of the CRT effect by enabling or disabling the Image renderer.
    /// The GameObject remains active to allow for ongoing logic or external control.
    /// </summary>
    /// <param name="isOn">True to display the filter, false to hide it.</param>
    public void UpdateFilter(bool isOn)
    {
        if (crtImage != null)
        {
            crtImage.enabled = isOn; 
        }
    }
}