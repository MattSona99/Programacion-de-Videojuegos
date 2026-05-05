using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class BookManager : MonoBehaviour
{
    [Header("Riferimenti UI")]
    [Tooltip("Il RectTransform dell'immagine del libro (BookPanel)")]
    public RectTransform bookPanel;
    
    [Header("Riferimenti Player")]
    [Tooltip("Trascina qui il tuo Player per bloccarne i movimenti")]
    public GameObject player;

    [Header("Impostazioni Animazione")]
    public float animationDuration = 0.4f; // Quanto dura l'entrata/uscita in secondi
    public Vector2 offScreenPosition = new Vector2(0f, -1500f); // Posizione fuori dallo schermo (in basso)
    public Vector2 onScreenPosition = new Vector2(0f, 0f);      // Posizione al centro dello schermo

    [Header("Pickup")]
    [Tooltip("L'oggetto libro nel mondo che verrà distrutto al primo utilizzo")]
    public GameObject worldBookObject;

    [Header("Dialogo")]
    [Tooltip("Dialogo che parte alla prima chiusura del libro dopo la raccolta")]
    public DialogueAsset postBookDialogue;

    [Header("Eventi")]
    [Tooltip("Invocato la prima volta che il giocatore raccoglie il libro (usato per gating della tomba)")]
    public UnityEvent onBookPickedUp;

    private bool _isOpen = false;
    private bool _hasBeenPickedUp = false;
    private bool _hasShownPostBookDialogue = false;
    private int _lastToggleFrame = -1;
    private Coroutine _currentAnim;

    void Start()
    {
        if (bookPanel != null)
        {
            bookPanel.anchoredPosition = offScreenPosition;
        }
    }

    void Update()
    {
        // Dopo aver raccolto il libro, il giocatore può aprirlo/chiuderlo in qualsiasi momento con F.
        // Il controllo sul frame evita il doppio-toggle nel frame della raccolta, quando anche
        // l'InteractableObject del libro nel mondo invoca ToggleBookMenu().
        if (_hasBeenPickedUp
            && Time.frameCount != _lastToggleFrame
            && Keyboard.current != null
            && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBookMenu();
        }
    }

    /// <summary>
    /// Questa è la funzione che chiameremo dall'InteractableObject quando premi F
    /// </summary>
    public void ToggleBookMenu()
    {
        // Al primo utilizzo "raccogli" il libro: rimuovilo dal mondo.
        // Da qui in poi il giocatore lo apre con F senza dover essere vicino all'oggetto.
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

        // Ferma l'animazione precedente se stai cliccando molto velocemente
        if (_currentAnim != null) StopCoroutine(_currentAnim);
        
        if (_isOpen)
        {
            // Apri: fai scivolare verso il centro e blocca il player
            _currentAnim = StartCoroutine(SlideRoutine(onScreenPosition));
            SetPlayerMovement(false);
        }
        else
        {
            // Chiudi: fai scivolare verso il basso, sblocca il player, e (al primo close) lancia il dialogo
            _currentAnim = StartCoroutine(CloseAndMaybeShowDialogueRoutine());
            SetPlayerMovement(true);
        }
    }

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

    // Coroutine per creare un movimento fluido e rallentato verso la fine (Ease-Out)
    private IEnumerator SlideRoutine(Vector2 targetPos)
    {
        Vector2 startPos = bookPanel.anchoredPosition;
        float time = 0f;

        while (time < animationDuration)
        {
            time += Time.deltaTime;
            
            // Formula matematica per rendere il movimento morbido e non robotico
            float t = time / animationDuration;
            t = t * t * (3f - 2f * t); // SmoothStep formula

            bookPanel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        bookPanel.anchoredPosition = targetPos;
    }

    private void SetPlayerMovement(bool canMove)
    {
        if (player != null)
        {
            // 1. AZZERA GLI INPUT E LE ANIMAZIONI (La magia per non farlo camminare da solo)
            if (!canMove)
            {
                // Invia un segnale forzato per azzerare il joystick/tastiera
                player.SendMessage("MoveInput", Vector2.zero, SendMessageOptions.DontRequireReceiver);
                player.SendMessage("SprintInput", false, SendMessageOptions.DontRequireReceiver);
                
                // Blocca l'animazione di corsa se c'è un Animator
                Animator anim = player.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetFloat("Speed", 0f);
                    anim.SetFloat("MotionSpeed", 0f);
                }
            }

            // 2. DISATTIVA IL PLAYER INPUT UFFICIALE DI UNITY
            var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                if (canMove) playerInput.ActivateInput();   // Riaccende la tastiera
                else playerInput.DeactivateInput();         // Spegne la tastiera
            }

            // 3. ACCENDI/SPEGNI GLI SCRIPT DEL PERSONAGGIO
            Behaviour thirdPersonScript = player.GetComponent("ThirdPersonController") as Behaviour;
            Behaviour starterInputsScript = player.GetComponent("StarterAssetsInputs") as Behaviour;
            Behaviour playerCombatScript = player.GetComponent("PlayerCombat") as Behaviour;

            if (thirdPersonScript != null) thirdPersonScript.enabled = canMove;
            if (starterInputsScript != null) starterInputsScript.enabled = canMove;
            if (playerCombatScript != null) playerCombatScript.enabled = canMove;
        }

        // 4. MOSTRA O NASCONDI IL MOUSE
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