using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// Manages the prologue book UI. The first interaction "picks up" the world book and removes
/// it; afterwards the Player can open/close the book panel anytime with B/F. Opening locks the
/// Player; the first close can trigger a post-book dialogue. Behaviors are wired via UnityEvents.
/// </summary>
public class BookManager : MonoBehaviour
{
    [Header("UI references")]
    [Tooltip("The RectTransform of the book image (BookPanel)")]
    public RectTransform bookPanel;

    [Header("Player references")]
    [Tooltip("Drag your Player here to lock its movement")]
    public GameObject player;

    [Header("Animation settings")]
    public float animationDuration = 0.4f; // How long the slide in/out lasts, in seconds
    public Vector2 offScreenPosition = new Vector2(0f, -1500f); // Off-screen position (bottom)
    public Vector2 onScreenPosition = new Vector2(0f, 0f);      // Centered on-screen position

    [Header("Pickup")]
    [Tooltip("The world book object that is destroyed on first use")]
    public GameObject worldBookObject;

    [Tooltip("Tick this if the player already picked up the book in a previous level")]
    public bool startAlreadyPickedUp = false;

    [Header("Dialogue")]
    [Tooltip("Dialogue played on the first book close after pickup")]
    public DialogueAsset postBookDialogue;

    [Header("Events")]
    [Tooltip("Invoked the first time the player picks up the book (used for tomb gating)")]
    public UnityEvent onBookPickedUp;

    private bool _isOpen = false;
    public bool IsOpen => _isOpen;
    private bool _hasBeenPickedUp = false;
    private bool _hasShownPostBookDialogue = false;
    private int _lastToggleFrame = -1;
    private Coroutine _currentAnim;

    void Start()
    {
        // If the box is ticked, tell the system the book is already picked up
        if (startAlreadyPickedUp)
        {
            _hasBeenPickedUp = true;
            // Book carried from a previous level: the first-open dialogue already
            // played in that level, so it must not be replayed here.
            _hasShownPostBookDialogue = true;
        }

        if (bookPanel != null)
        {
            bookPanel.anchoredPosition = offScreenPosition;
        }
    }

    void Update()
    {
        // After picking up the book, the player can open/close it anytime with B.
        // The frame check avoids a double-toggle on the pickup frame, when the world book's
        // InteractableObject also invokes ToggleBookMenu().
        if (_hasBeenPickedUp
            && Time.frameCount != _lastToggleFrame
            && Keyboard.current != null
            && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBookMenu();
        }
    }

    /// <summary>
    /// Opens or closes the book panel. Wired to the world book's InteractableObject (press F)
    /// and also bound to B once picked up. On first use it "picks up" the book.
    /// </summary>
    public void ToggleBookMenu()
    {
        // On first use, "pick up" the book: remove it from the world.
        // From now on the player opens it with B without needing to be near the object.
        if (!_hasBeenPickedUp)
        {
            _hasBeenPickedUp = true;
            if (worldBookObject != null)
            {
                Destroy(worldBookObject);
            }
            onBookPickedUp.Invoke();
        }

        _lastToggleFrame = Time.frameCount;
        _isOpen = !_isOpen;

        // Stop the previous animation if clicking very fast
        if (_currentAnim != null) StopCoroutine(_currentAnim);

        if (_isOpen)
        {
            // Open: slide to center and lock the player
            _currentAnim = StartCoroutine(SlideRoutine(onScreenPosition));
            SetPlayerMovement(false);
        }
        else
        {
            // Close: slide down, unlock the player, and (on the first close) play the dialogue
            _currentAnim = StartCoroutine(CloseAndMaybeShowDialogueRoutine());
            SetPlayerMovement(true);
        }
    }

    /// <summary>Slides the book off-screen, then plays the post-book dialogue once (first close after pickup).</summary>
    private IEnumerator CloseAndMaybeShowDialogueRoutine()
    {
        yield return SlideRoutine(offScreenPosition);

        if (_hasBeenPickedUp
            && !_hasShownPostBookDialogue
            && postBookDialogue != null
            && DialogueManager.Instance != null)
        {
            _hasShownPostBookDialogue = true;
            DialogueManager.Instance.PlayDialogue(postBookDialogue);
        }
    }

    /// <summary>Slides the book panel to <paramref name="targetPos"/> with a smooth ease-out.</summary>
    private IEnumerator SlideRoutine(Vector2 targetPos)
    {
        Vector2 startPos = bookPanel.anchoredPosition;
        float time = 0f;

        while (time < animationDuration)
        {
            time += Time.deltaTime;

            // Smooth, non-robotic motion
            float t = time / animationDuration;
            t = t * t * (3f - 2f * t); // SmoothStep formula

            bookPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        bookPanel.anchoredPosition = targetPos;
    }

    /// <summary>Locks/unlocks the Player: zeroes input/animation, toggles PlayerInput and character scripts, and the cursor.</summary>
    private void SetPlayerMovement(bool canMove)
    {
        if (player != null)
        {
            // 1. ZERO THE INPUTS AND ANIMATIONS (keeps the player from walking on its own)
            if (!canMove)
            {
                // Force-send a signal to zero the joystick/keyboard
                player.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);

                // Stop the run animation if there's an Animator
                Animator anim = player.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                }
            }

            // 2. DISABLE UNITY'S OFFICIAL PLAYER INPUT
            var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                if (canMove) playerInput.ActivateInput();   // Re-enable the keyboard
                else playerInput.DeactivateInput();         // Disable the keyboard
            }

            // 3. ENABLE/DISABLE THE CHARACTER SCRIPTS
            Behaviour thirdPersonScript = player.GetComponent("ThirdPersonController") as Behaviour;
            Behaviour starterInputsScript = player.GetComponent("StarterAssetsInputs") as Behaviour;
            Behaviour playerCombatScript = player.GetComponent("PlayerCombat") as Behaviour;

            if (thirdPersonScript != null) thirdPersonScript.enabled = canMove;
            if (starterInputsScript != null) starterInputsScript.enabled = canMove;
            if (playerCombatScript != null) playerCombatScript.enabled = canMove;
        }

        // 4. SHOW OR HIDE THE MOUSE
        if (canMove)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}