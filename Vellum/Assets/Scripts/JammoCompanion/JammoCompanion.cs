using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives Jammo's one-time activation in Act 1: on interaction it (optionally) plays an
/// activation dialogue, runs the get-up animation, locks the Player meanwhile, then starts the
/// path puzzle and hands control over to JammoGuideController.
/// </summary>
public class JammoCompanion : MonoBehaviour
{
    [Header("Puzzle connection")]
    [Tooltip("Drag the PuzzleManager object from your scene here")]
    public PathPuzzleManager puzzleManager;

    [Header("Activation")]
    [Tooltip("Dialogue played the first time the player enters Jammo's trigger")]
    public DialogueAsset activationDialogue;

    [Tooltip("Name of the Animator bool that starts the get-up animation")]
    public string animatorActivatedBool = "IsActivated";

    [Tooltip("Seconds to wait after the dialogue before Jammo starts walking")]
    public float getUpDelay = 5f;

    [Header("Player lock")]
    [Tooltip("Reference to the CinematicFallManager (the same one JammoGuideController uses). Used to lock the Player while Jammo gets up.")]
    public CinematicFallManager cinematicManager;

    private Animator anim;
    private NavMeshAgent agent;
    private bool _hasBeenTriggered = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // Disable the Agent right away: we no longer use it for free chasing.
        if (agent != null) agent.enabled = false;
    }

    /// <summary>Wired to Jammo's activation trigger. Plays the dialogue (if any), then runs the get-up sequence once.</summary>
    public void HandleActivationInteraction()
    {
        if (_hasBeenTriggered) return;
        _hasBeenTriggered = true;

        if (activationDialogue != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.PlayDialogue(
                activationDialogue,
                () => StartCoroutine(GetUpAndActivateRoutine()));
        }
        else
        {
            StartCoroutine(GetUpAndActivateRoutine());
        }
    }

    /// <summary>Locks the Player, plays the get-up animation, waits, then starts the puzzle and disables this script.</summary>
    private IEnumerator GetUpAndActivateRoutine()
    {
        // keepLookActive=true by default → mouse/right-stick keep moving the camera,
        // only Move/Sprint/Jump are frozen. JammoGuideController unlocks it at the end
        // of the little robot's walk.
        if (cinematicManager != null)
        {
            cinematicManager.SetPlayerMovement(false);
        }

        // 1. Start the wake-up animation
        if (anim != null && !string.IsNullOrEmpty(animatorActivatedBool))
        {
            anim.SetBool(animatorActivatedBool, true);
        }

        // 2. Wait for the get-up to finish
        yield return new WaitForSeconds(getUpDelay);

        // 3. START THE PUZZLE!
        if (puzzleManager != null)
        {
            Debug.Log("Jammo got up! Starting the puzzle sequence.");
            puzzleManager.StartPuzzleSequence();
        }
        else
        {
            Debug.LogError("Warning: PuzzleManager is missing on the JammoCompanion script!");
            // Without puzzleManager nothing would unlock the Player: unlock it here to avoid a stuck state.
            if (cinematicManager != null) cinematicManager.SetPlayerMovement(true);
        }

        // 4. Disable this script (JammoCompanion) because Jammo is now
        // controlled by JammoGuideController!
        this.enabled = false;
    }
}