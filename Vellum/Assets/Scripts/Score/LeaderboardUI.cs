using TMPro;
using UnityEngine;

/// <summary>
/// Builds the leaderboard list: clears the content holder, loads the persisted entries
/// (<see cref="LeaderboardStore"/>), and instantiates one <see cref="LeaderboardEntryView"/> row per
/// entry (already sorted desc by score). Call <see cref="Refresh"/> whenever the panel is opened or a
/// score is saved (driven by <c>MainMenuManager</c>).
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Tooltip("The 'Player Entry' prefab (must carry a LeaderboardEntryView).")]
    [SerializeField] private LeaderboardEntryView entryPrefab;
    [Tooltip("Parent under which rows are instantiated (the Scroll View 'Content').")]
    [SerializeField] private Transform contentParent;
    [Tooltip("Optional label shown only when the leaderboard is empty.")]
    [SerializeField] private TMP_Text emptyLabel;

    /// <summary>Rebuilds the row list from the persisted leaderboard.</summary>
    public void Refresh()
    {
        if (contentParent == null || entryPrefab == null)
        {
            Debug.LogWarning("[LeaderboardUI] entryPrefab or contentParent not assigned: cannot build the list.", this);
            return;
        }

        // Support both setups: entryPrefab can be a Project prefab asset OR an in-scene template.
        // A scene template must be kept (used as the clone source) and NOT destroyed by the clear
        // loop below — otherwise we'd Instantiate a destroyed object and get zero rows.
        bool sceneTemplate = entryPrefab.gameObject.scene.IsValid();
        Transform templateTf = sceneTemplate ? entryPrefab.transform : null;
        if (sceneTemplate) entryPrefab.gameObject.SetActive(false); // hidden source

        // Clear previous rows (backwards: we're removing children); never remove the scene template.
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);
            if (child == templateTf) continue;
            Destroy(child.gameObject);
        }

        LeaderboardData data = LeaderboardStore.Load();
        if (emptyLabel != null) emptyLabel.gameObject.SetActive(data.entries.Count == 0);

        for (int i = 0; i < data.entries.Count; i++)
        {
            LeaderboardEntryView row = Instantiate(entryPrefab, contentParent);
            row.gameObject.SetActive(true); // in case the source template was hidden
            row.Populate(i + 1, data.entries[i]);
        }

        Debug.Log($"[LeaderboardUI] Built {data.entries.Count} rows from the saved leaderboard.", this);
    }
}
