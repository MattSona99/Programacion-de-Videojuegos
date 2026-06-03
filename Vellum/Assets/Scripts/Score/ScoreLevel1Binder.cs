using UnityEngine;

/// <summary>
/// Feeds <see cref="ScoreManager"/> from Level 1 (Path Puzzle) events. Drop this on any object in the
/// Act_01 scene and assign the <see cref="PathPuzzleManager"/>; it subscribes in <c>OnEnable</c> and
/// unsubscribes in <c>OnDisable</c> (no per-frame polling). The manager itself is auto-created, so no
/// other wiring is needed.
/// </summary>
public class ScoreLevel1Binder : MonoBehaviour
{
    [SerializeField] private PathPuzzleManager pathPuzzle;

    private void OnEnable()
    {
        if (pathPuzzle == null) return;
        pathPuzzle.onPuzzleStarted.AddListener(HandleStarted);
        pathPuzzle.onCheckpointReached.AddListener(HandleCheckpoint);
        pathPuzzle.onPuzzleCompleted.AddListener(HandleCompleted);
        pathPuzzle.onFail.AddListener(HandleFail);
        pathPuzzle.onHintUsed.AddListener(HandleHint);
    }

    private void OnDisable()
    {
        if (pathPuzzle == null) return;
        pathPuzzle.onPuzzleStarted.RemoveListener(HandleStarted);
        pathPuzzle.onCheckpointReached.RemoveListener(HandleCheckpoint);
        pathPuzzle.onPuzzleCompleted.RemoveListener(HandleCompleted);
        pathPuzzle.onFail.RemoveListener(HandleFail);
        pathPuzzle.onHintUsed.RemoveListener(HandleHint);
    }

    private void HandleStarted() => ScoreManager.Instance?.BeginLevelTimer(1);

    private void HandleCheckpoint() => ScoreManager.Instance?.MarkLevel1Checkpoint();

    private void HandleCompleted()
    {
        if (ScoreManager.Instance == null) return;
        ScoreManager.Instance.MarkLevelCompleted(1);
        ScoreManager.Instance.EndLevelTimer(1);
    }

    private void HandleFail() => ScoreManager.Instance?.AddFallen();

    private void HandleHint() => ScoreManager.Instance?.AddHelpUsed();
}
