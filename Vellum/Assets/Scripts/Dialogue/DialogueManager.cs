using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton that plays <see cref="DialogueAsset"/>s: fades in a canvas, types each line with a
/// typewriter effect (Space skips/advances), and locks the Player for the duration. External
/// directors can also lock/unlock the Player around a dialogue via LockPlayer/UnlockPlayer.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI References")]
    public CanvasGroup dialogueCanvasGroup;
    public TMP_Text speakerLabel;
    public TMP_Text bodyText;
    public GameObject advanceHint;

    [Header("Player Reference")]
    public GameObject player;

    [Header("Settings")]
    public float fadeSpeed = 8f;
    public float charsPerSecond = 30f;

    public bool IsPlaying { get; private set; }

    private Coroutine _runRoutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (dialogueCanvasGroup != null) dialogueCanvasGroup.alpha = 0f;
        if (advanceHint != null) advanceHint.SetActive(false);
    }

    // Exposed for external directors (e.g. Act02Director) that must lock the player
    // BEFORE PlayDialogue — typically during an initial camera blend, when the dialogue
    // hasn't started yet but the player must not be able to move.
    /// <summary>Locks the Player (movement + character scripts) without starting a dialogue.</summary>
    public void LockPlayer()   { SetPlayerLocked(true); }
    /// <summary>Unlocks the Player previously locked via <see cref="LockPlayer"/>.</summary>
    public void UnlockPlayer() { SetPlayerLocked(false); }

    /// <summary>Plays a dialogue asset to completion, locking the Player, then invokes <paramref name="onComplete"/>.</summary>
    public void PlayDialogue(DialogueAsset dialogue, Action onComplete = null)
    {
        if (IsPlaying)
        {
            Debug.LogWarning("[DialogueManager] PlayDialogue called while another dialogue is playing. Ignored.");
            return;
        }

        if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
        {
            Debug.LogWarning("[DialogueManager] PlayDialogue called with empty/null DialogueAsset.");
            onComplete?.Invoke();
            return;
        }

        _runRoutine = StartCoroutine(RunDialogue(dialogue, onComplete));
    }

    private IEnumerator RunDialogue(DialogueAsset dialogue, Action onComplete)
    {
        IsPlaying = true;
        SetPlayerLocked(true);

        // Clear the fields before fade-in so the canvas doesn't reappear with the previous dialogue's text
        if (speakerLabel != null) speakerLabel.text = "";
        if (bodyText != null) bodyText.text = "";
        if (advanceHint != null) advanceHint.SetActive(false);

        yield return Fade(1f);

        foreach (var line in dialogue.lines)
        {
            if (speakerLabel != null) speakerLabel.text = line.speaker;
            if (bodyText != null) bodyText.text = "";
            if (advanceHint != null) advanceHint.SetActive(false);

            yield return TypewriterRoutine(line.text);

            if (advanceHint != null) advanceHint.SetActive(true);

            yield return WaitForSpace();
        }

        if (advanceHint != null) advanceHint.SetActive(false);
        yield return Fade(0f);

        SetPlayerLocked(false);
        IsPlaying = false;
        _runRoutine = null;

        onComplete?.Invoke();
    }

    /// <summary>Reveals <paramref name="text"/> character by character; pressing Space reveals it all at once.</summary>
    private IEnumerator TypewriterRoutine(string text)
    {
        float charDelay = 1f / Mathf.Max(charsPerSecond, 1f);
        int i = 0;
        float timer = 0f;

        while (i < text.Length)
        {
            // Check the input EVERY SINGLE FRAME, so we never miss a press!
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (bodyText != null) bodyText.text = text;
                yield break;
            }

            timer += Time.deltaTime;

            // If enough time has passed, advance the letters.
            // Use a while in case charsPerSecond is very high (lets us print multiple letters in one frame)
            while (timer >= charDelay && i < text.Length)
            {
                i++;
                timer -= charDelay;
            }

            if (bodyText != null) bodyText.text = text.Substring(0, i);

            // Wait exactly one frame before resuming the loop and re-checking the key
            yield return null;
        }
    }

    private IEnumerator WaitForSpace()
    {
        // Skip one frame so the same Space press that finished the typewriter doesn't auto-advance
        yield return null;

        while (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            yield return null;
        }

        // Consume the frame of the press so the next TypewriterRoutine doesn't see it as a skip
        yield return null;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (dialogueCanvasGroup == null) yield break;
        while (!Mathf.Approximately(dialogueCanvasGroup.alpha, targetAlpha))
        {
            dialogueCanvasGroup.alpha = Mathf.MoveTowards(
                dialogueCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            yield return null;
        }
        dialogueCanvasGroup.alpha = targetAlpha;
    }

    /// <summary>Locks/unlocks the Player: zeroes movement input/animation and toggles PlayerInput and character scripts.</summary>
    private void SetPlayerLocked(bool locked)
    {
        if (player != null)
        {
            if (locked)
            {
                // Broadcast to reach StarterAssetsInputs even if it's on a child of the player
                player.BroadcastMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.BroadcastMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);

                Animator anim = player.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);

                    // Force the animator to Idle in the same frame, otherwise the blend tree
                    // keeps showing the run while the speed interpolates to 0.
                    anim.Play("Idle Walk Run Blend", 0, 0f);
                    anim.Update(0f);
                }
            }

            // Mirrors MainMenuManager.SetPlayerMovement: enable/disable the PlayerInput component
            // (not Activate/Deactivate, since it may be called on a component disabled by the menu)
            var playerInput = player.GetComponentInChildren<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = !locked;
            }

            // GetComponentsInChildren so we catch the scripts even if they live on a child
            MonoBehaviour[] allScripts = player.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in allScripts)
            {
                string scriptName = script.GetType().Name;
                if (scriptName == "ThirdPersonController" ||
                    scriptName == "StarterAssetsInputs" ||
                    scriptName == "PlayerCombat")
                {
                    script.enabled = !locked;
                }
            }
        }

        // Cursor stays locked & hidden during dialogues (different from BookManager)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
