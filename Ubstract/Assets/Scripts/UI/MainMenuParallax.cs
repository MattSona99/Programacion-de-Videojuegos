using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Manages an infinite parallax scrolling system for the Main Menu UI.
/// Includes a dynamic time-of-day system that swaps texture sets based on the user's system clock.
/// </summary>
public class MainMenuParallax : MonoBehaviour
{
    /// <summary>
    /// Container for a complete environment theme consisting of four parallax layers.
    /// </summary>
    [System.Serializable]
    public struct TimeSlot
    {
        public Texture bg_1; 
        public Texture bg_2; 
        public Texture bg_3; 
        public Texture bg_4; 
    }

    [Header("UI Layer References")]
    [Tooltip("RawImage components for each layer. Layer 1 is the most distant background.")]
    public RawImage raw_1;
    public RawImage raw_2;
    public RawImage raw_3;
    public RawImage raw_4;

    [Header("Parallax Velocity")]
    [Tooltip("Horizontal scroll speed for the background layer (Layer 2).")]
    public float speed_2 = 0.01f;
    
    [Tooltip("Horizontal scroll speed for the midground layer (Layer 3).")]
    public float speed_3 = 0.02f;
    
    [Tooltip("Horizontal scroll speed for the foreground layer (Layer 4).")]
    public float speed_4 = 0.08f;

    [Header("Thematic Texture Sets")]
    [Tooltip("Textures assigned for the Morning phase (06:00 - 11:59).")]
    public TimeSlot setMorning;
    
    [Tooltip("Textures assigned for the Afternoon phase (12:00 - 16:59).")]
    public TimeSlot setAfternoon;
    
    [Tooltip("Textures assigned for the Evening phase (17:00 - 20:59).")]
    public TimeSlot setEvening;
    
    [Tooltip("Textures assigned for the Night phase (21:00 - 05:59).")]
    public TimeSlot setNight;

    /// <summary>
    /// Synchronizes the background visuals with the current system time upon initialization.
    /// </summary>
    void Start()
    {
        SetDayTime();
    }

    /// <summary>
    /// Updates UV offsets per frame to create the illusion of depth and movement.
    /// </summary>
    void Update()
    {
        // Sky/Layer 1 remains static to provide a fixed point of reference.
        ScrollLevel(raw_2, speed_2);
        ScrollLevel(raw_3, speed_3);
        ScrollLevel(raw_4, speed_4);
    }

    /// <summary>
    /// Procedurally shifts the UV Rect of a RawImage. 
    /// This creates a seamless scrolling effect assuming the texture is set to 'Repeat' wrap mode.
    /// </summary>
    /// <param name="img">Target UI RawImage component.</param>
    /// <param name="speed">Translation velocity along the X-axis.</param>
    void ScrollLevel(RawImage img, float speed)
    {
        if (img.texture == null) return;

        Rect uv = img.uvRect;
        
        // Apply frame-rate independent movement to the UV coordinates
        uv.x += speed * Time.deltaTime;
        
        img.uvRect = uv;
    }

    /// <summary>
    /// Queries the real-world local hour and selects the appropriate texture set.
    /// This establishes the menu's atmosphere based on the player's environment.
    /// </summary>
    void SetDayTime()
    {
        int now = DateTime.Now.Hour;

        if (now >= 6 && now < 12) 
        {
            ApplySet(setMorning);
        }
        else if (now >= 12 && now < 17)
        {
            ApplySet(setAfternoon);
        }
        else if (now >= 17 && now < 21)
        {
            ApplySet(setEvening);
        }
        else
        {
            ApplySet(setNight);
        }
    }

    /// <summary>
    /// Updates the texture references for all parallax layers based on the provided TimeSlot data.
    /// </summary>
    /// <param name="set">The targeted collection of thematic textures.</param>
    void ApplySet(TimeSlot set)
    {
        raw_1.texture = set.bg_1;
        raw_2.texture = set.bg_2;
        raw_3.texture = set.bg_3;
        raw_4.texture = set.bg_4;
    }
}