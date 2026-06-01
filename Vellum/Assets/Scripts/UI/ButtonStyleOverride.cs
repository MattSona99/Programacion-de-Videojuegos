using UnityEngine;

/// <summary>
/// Marker component. Buttons with this attached are skipped by GlobalButtonStyling.
/// Useful for buttons with custom visuals (e.g. character portraits) where the
/// hover-fill rectangle would clash with the design.
/// </summary>
public class ButtonStyleOverride : MonoBehaviour
{
    [Tooltip("If true, only the hover-fill effect is skipped. The hover scale-up still applies.")]
    public bool keepHoverScale = true;

    [Tooltip("Scale multiplier on hover when 'Keep Hover Scale' is enabled.")]
    public float hoverScale = 1.05f;

    [Tooltip("Animation speed for the hover scale (only used when 'Keep Hover Scale' is on).")]
    public float animationSpeed = 25f;
}