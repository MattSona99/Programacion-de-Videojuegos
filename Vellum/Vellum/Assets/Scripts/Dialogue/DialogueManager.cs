using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

        // Pulisci i campi prima del fade-in così il canvas non riappare con il testo del dialogo precedente
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

    private IEnumerator TypewriterRoutine(string text)
    {
        float charDelay = 1f / Mathf.Max(charsPerSecond, 1f);
        int i = 0;

        while (i < text.Length)
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (bodyText != null) bodyText.text = text;
                yield break;
            }

            i++;
            if (bodyText != null) bodyText.text = text.Substring(0, i);
            yield return new WaitForSeconds(charDelay);
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

    private void SetPlayerLocked(bool locked)
    {
        if (player != null)
        {
            if (locked)
            {
                player.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);

                Animator anim = player.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                }
            }

            // Allinea con MainMenuManager.SetPlayerMovement: enable/disable del componente PlayerInput
            // (non Activate/Deactivate, perché potrebbe essere chiamato su un componente disabilitato dal menu)
            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = !locked;
            }

            Behaviour thirdPersonScript = player.GetComponent("ThirdPersonController") as Behaviour;
            Behaviour starterInputsScript = player.GetComponent("StarterAssetsInputs") as Behaviour;
            Behaviour playerCombatScript = player.GetComponent("PlayerCombat") as Behaviour;

            if (thirdPersonScript != null) thirdPersonScript.enabled = !locked;
            if (starterInputsScript != null) starterInputsScript.enabled = !locked;
            if (playerCombatScript != null) playerCombatScript.enabled = !locked;
        }

        // Cursor stays locked & hidden during dialogues (different from BookManager)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
