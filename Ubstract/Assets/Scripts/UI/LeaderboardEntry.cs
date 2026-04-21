using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Controls a single entry in the leaderboard UI. 
/// Features an accordion-style expansion system to reveal detailed match statistics
/// while maintaining fluid layout rebuilds.
/// </summary>
public class LeaderboardEntry : MonoBehaviour
{
    [Header("Primary Header (Always Visible)")]
    public Button expandButton;
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI scoreText;

    [Header("Dropdown Details")]
    public GameObject detailsPanel;
    public TextMeshProUGUI statsText;

    [Header("Animation Settings")]
    [Tooltip("The target height for the details panel when fully expanded.")]
    public float expandedHeight = 400f; 
    [Tooltip("Total duration in seconds for the expansion/contraction animation.")]
    public float animationSpeed = 0.25f; 

    private bool isExpanded = false;
    private LayoutElement detailsLayout;
    private Coroutine animCoroutine;
    private RectTransform parentRect;
    private RectTransform myRect;

    /// <summary>
    /// Initializes the entry with player data and formats the detailed statistics string.
    /// Dynamically calculates the required expansion height based on text content.
    /// </summary>
    /// <param name="rank">The numerical rank of the player (1-based).</param>
    /// <param name="record">The data container for the player's match performance.</param>
    public void Setup(int rank, PlayerRecord record)
    {
        myRect = GetComponent<RectTransform>();

        if (rankText != null) rankText.text = "#" + rank.ToString();
        if (nameText != null) nameText.text = record.playerName;
        if (scoreText != null) scoreText.text = record.finalScore.ToString("N0");

        if (statsText != null)
        {
            statsText.text = 
                $"<b>Enemies Defeated:</b> <color=#FFD700>{record.enemiesDefeated}</color>\n" +
                $"<b>Perfect Parries:</b> <color=#FFD700>{record.perfectParries}</color>\n" +
                $"<b>Potions Found:</b> <color=#00FF00>{record.potionsCollected}</color>\n" +
                $"<b>Normal Defenses:</b> <color=#FFD700>{record.normalDefenses}</color>\n" +
                $"<b>Damage Dealt:</b> <color=#FFD700>{record.damageDealt}</color>\n" +
                $"<b>Health Lost:</b> <color=#FF4444>{record.healthLost}</color>\n" +
                $"<b>Damage Blocked:</b> <color=#FFD700>{record.damageBlocked}</color>\n" +
                $"<b>Time Taken:</b> <color=#FFD700>{record.timeTaken:F1}s</color>";

                // Force mesh generation to accurately calculate the preferred height for the accordion
                statsText.ForceMeshUpdate();
                expandedHeight = statsText.preferredHeight + 40f;
        }

        // Ensure a LayoutElement is present to drive the vertical expansion
        detailsLayout = detailsPanel.GetComponent<LayoutElement>();
        if (detailsLayout == null) detailsLayout = detailsPanel.AddComponent<LayoutElement>();

        parentRect = transform.parent.GetComponent<RectTransform>();

        // Default state: Closed and hidden
        detailsLayout.preferredHeight = 0f;
        detailsLayout.minHeight = 0f;
        detailsPanel.SetActive(false);
        isExpanded = false;

        expandButton.onClick.RemoveAllListeners();
        expandButton.onClick.AddListener(ToggleAccordion);

        if (GlobalButtonStyling.Instance != null)
        {
            GlobalButtonStyling.Instance.ApplyAnimatedHoverLogic(expandButton);
        }
    }

    /// <summary>
    /// Swaps the expansion state and triggers the visual transition coroutine.
    /// </summary>
    private void ToggleAccordion()
    {
        if (MenuAudioManager.instance != null)
        {
            MenuAudioManager.instance.PlayAccordionSound();
        }
        isExpanded = !isExpanded;

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(SmoothAccordion(isExpanded ? expandedHeight : 0f));
    }

    /// <summary>
    /// Coroutine that interpolates the height of the details panel.
    /// Manages LayoutRebuilder calls to ensure the vertical group parent adjusts smoothly.
    /// </summary>
    /// <param name="targetHeight">The height (0 or expandedHeight) to reach.</param>
    private IEnumerator SmoothAccordion(float targetHeight)
    {
        // Activate panel before animation to allow layout calculation
        if (isExpanded) detailsPanel.SetActive(true);

        float startHeight = detailsLayout.minHeight;
        float time = 0;

        if (animationSpeed <= 0.01f) time = 1f;

        while (time < 1.0f)
        {
            time += Time.unscaledDeltaTime / animationSpeed;
            
            // Apply easing for a polished user experience
            float t = Mathf.SmoothStep(0f, 1f, time);
            float currentH = Mathf.Lerp(startHeight, targetHeight, t);

            detailsLayout.minHeight = currentH;
            detailsLayout.preferredHeight = currentH;

            // Force immediate UI layout recalculation for the entry and its parent container
            LayoutRebuilder.MarkLayoutForRebuild(myRect);
            if (parentRect != null) LayoutRebuilder.MarkLayoutForRebuild(parentRect);

            yield return null;
        }

        detailsLayout.minHeight = targetHeight;
        detailsLayout.preferredHeight = targetHeight;
        
        LayoutRebuilder.MarkLayoutForRebuild(myRect);
        if (parentRect != null) LayoutRebuilder.MarkLayoutForRebuild(parentRect);
        
        // Deactivate object when contraction is complete to optimize UI rendering
        if (!isExpanded) detailsPanel.SetActive(false);
        
        animCoroutine = null;
    }
}