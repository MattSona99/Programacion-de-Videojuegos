using UnityEngine;
using System.Collections;

/// <summary>
/// Moves Jammo as the Act 1 guide: he walks the correct path (start→checkpoint, then
/// checkpoint→end), lighting his purple trail tile by tile while the Player is locked, faces the
/// door at the end, then clears the trail and returns control to the Player.
/// </summary>
public class JammoGuideController : MonoBehaviour
{
    [Header("Base settings")]
    public PathPuzzleManager manager;
    public float walkSpeed = 3f;
    public Animator jammoAnimator;

    [Header("Player lock")]
    [Tooltip("Drag the scene object holding the CinematicFallManager here")]
    public CinematicFallManager cinematicManager;

    [Header("Collisions during the walk")]
    [Tooltip("Optional: Jammo's non-trigger collider to disable during the walk, so the player doesn't push him to the checkpoint.")]
    [SerializeField] private Collider jammoBodyCollider;

    [Header("Final pose")]
    [Tooltip("Transform of the door (or any point): when Jammo reaches the last tile he turns to face this target.")]
    [SerializeField] private Transform doorLookTarget;

    [Tooltip("Rotation speed (Slerp) of Jammo toward the final target.")]
    [SerializeField] private float turnSpeed = 5f;

    private Coroutine _currentWalk;

    /// <summary>
    /// True while Jammo is walking the path (the player is already locked by the
    /// cinematicManager): used by PathPuzzleManager to not grant the memory hint during the
    /// guided walk.
    /// </summary>
    public bool IsWalking => _currentWalk != null;

    /// <summary>Walks Jammo from the start tile to the checkpoint.</summary>
    public void WalkToCheckpoint()
    {
        if (_currentWalk != null) StopCoroutine(_currentWalk);
        _currentWalk = StartCoroutine(FollowPathRoutine(0, manager.checkpointIndex));
    }

    /// <summary>Resumes Jammo's walk from the checkpoint to the end of the path.</summary>
    public void ResumeWalkToEnd()
    {
        if (_currentWalk != null) StopCoroutine(_currentWalk);
        _currentWalk = StartCoroutine(FollowPathRoutine(manager.checkpointIndex, manager.correctPath.Count - 1));
    }

    /// <summary>Walks Jammo through the path range [startIndex, endIndex], lighting the trail, then restores Player control.</summary>
    private IEnumerator FollowPathRoutine(int startIndex, int endIndex)
    {
        // 1. LOCK THE PLAYER AT THE START OF THE WALK
        //    (keepLookActive stays true by default → mouse / right-stick keep working)
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(false);
        }

        if (jammoBodyCollider != null) jammoBodyCollider.enabled = false;

        if (jammoAnimator != null)
        {
            jammoAnimator.SetFloat("Speed", 1f); 
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            PathTile targetTileScript = manager.correctPath[i];
            Transform targetTile = targetTileScript.transform;

            Vector3 targetPosition = new Vector3(targetTile.position.x, transform.position.y, targetTile.position.z);

            while (Vector3.Distance(transform.position, targetPosition) > 0.05f)
            {
                Vector3 direction = (targetPosition - transform.position).normalized;
                if (direction != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
                }

                transform.position = Vector3.MoveTowards(transform.position, targetPosition, walkSpeed * Time.deltaTime);

                yield return null;
            }

            // Explicit trail: Jammo's triggers may be disabled during the walk
            // (jammoBodyCollider.enabled = false above), so we don't rely on
            // OnTriggerEnter to light the tile.
            targetTileScript.SetColor(targetTileScript.robotColor);
        }

        if (jammoAnimator != null)
        {
            jammoAnimator.SetFloat("Speed", 0f);
        }

        // If this is the last walk (Jammo reached the end of the path) and there's a
        // target, turn him to face the door before unlocking the player.
        if (endIndex == manager.correctPath.Count - 1 && doorLookTarget != null)
        {
            yield return TurnToFace(doorLookTarget);
        }

        if (jammoBodyCollider != null) jammoBodyCollider.enabled = true;

        // Clear the purple trail BEFORE returning control to the player: so the tiles he
        // re-crosses start from default and light up in playerColor step by step.
        // It blinks 3 times before disappearing.
        if (manager != null) yield return manager.StartCoroutine(manager.BlinkAndClearRobotTrail());

        // 2. UNLOCK THE PLAYER WHEN JAMMO STOPS (at the checkpoint or at the end)
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(true);
        }

        _currentWalk = null;
    }

    /// <summary>Rotates Jammo to face <paramref name="target"/> on the XZ plane (3s safety cap).</summary>
    private IEnumerator TurnToFace(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f; // rotate only on the XZ plane, no pitch.
        if (toTarget.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        // Safety: 3s cap in case the target is practically on top of Jammo.
        float elapsed = 0f;
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f && elapsed < 3f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRot;
    }
}