# Intro/Post-Book Dialogues + Tomb Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un sistema di dialogo narrativo (ScriptableObject-based) con typewriter, un dialogo intro al primo Play, un dialogo post-libro alla prima chiusura, e gating silenzioso della tomba finché il libro non viene raccolto.

**Architecture:** Singleton `DialogueManager` che consuma `DialogueAsset` (ScriptableObject contenente una lista di `DialogueLine`). UI in scena con `CanvasGroup` + `TMP_Text` (speaker/body) + hint "[Space]". Trigger: `MainMenuManager.TransitionToGame` invoca l'intro al primo play (dopo blend camera + fade UI); `BookManager.ToggleBookMenu` invoca il post-libro alla prima chiusura. Gating tomba via `UnityEvent onBookPickedUp` wired in Inspector verso `InteractableObject.enabled = true`.

**Tech Stack:** Unity 6000.3.x, URP, Cinemachine 3, new Input System (`Keyboard.current`), TextMesh Pro, ScriptableObject, Coroutines.

**Spec:** [`docs/superpowers/specs/2026-05-05-tomb-gating-intro-tutorial-design.md`](../specs/2026-05-05-tomb-gating-intro-tutorial-design.md)

**Note sul testing:** il progetto Unity non ha test framework configurato; la validazione è in **Play mode** manuale. Ogni task di codice include un check di compilazione; il Task 6 finale è la validazione end-to-end in Play mode.

---

## File Structure

| File | Type | Responsibility |
|---|---|---|
| `Assets/Scripts/Dialogue/DialogueLine.cs` | NEW | Struct serializable: speaker + text |
| `Assets/Scripts/Dialogue/DialogueAsset.cs` | NEW | ScriptableObject con lista di righe |
| `Assets/Scripts/Dialogue/DialogueManager.cs` | NEW | Singleton: lifecycle, typewriter, lock player |
| `Assets/Scripts/Items/Objects/BookManager.cs` | MODIFY | +postBookDialogue, +onBookPickedUp, +CloseAndMaybeShowDialogueRoutine |
| `Assets/Scripts/Menu/MainMenuManager.cs` | MODIFY | +introDialogue, +Update guard, +TransitionToGame branch |
| `Assets/Dialogues/IntroDialogue.asset` | NEW (editor) | 3 righe Narrator |
| `Assets/Dialogues/PostBookDialogue.asset` | NEW (editor) | 2 righe Narrator |
| `Assets/Scenes/<scene>.unity` | MODIFY (editor) | DialogueCanvas + DialogueManager GO + Inspector wiring |

---

## Task 1: Create `DialogueLine` and `DialogueAsset`

**Files:**
- Create: `Assets/Scripts/Dialogue/DialogueLine.cs`
- Create: `Assets/Scripts/Dialogue/DialogueAsset.cs`

- [ ] **Step 1: Create the folder**

```powershell
New-Item -ItemType Directory -Path "Assets/Scripts/Dialogue" -Force
```

- [ ] **Step 2: Create `DialogueLine.cs`**

```csharp
using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}
```

- [ ] **Step 3: Create `DialogueAsset.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Vellum/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    public string dialogueId;
    public List<DialogueLine> lines;
}
```

- [ ] **Step 4: Wait for Unity compile, check Console**

Switch to Unity Editor, attendere la compilazione automatica. Console deve essere pulita (nessun errore CS####). Se vedi un warning su `[CreateAssetMenu]` ignoralo.

- [ ] **Step 5: Commit**

```powershell
git add "Assets/Scripts/Dialogue/DialogueLine.cs" "Assets/Scripts/Dialogue/DialogueAsset.cs"
git commit -m "feat(dialogue): add DialogueLine struct and DialogueAsset ScriptableObject"
```

---

## Task 2: Create `DialogueManager`

**Files:**
- Create: `Assets/Scripts/Dialogue/DialogueManager.cs`

- [ ] **Step 1: Create `DialogueManager.cs`**

```csharp
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

            var playerInput = player.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                if (locked) playerInput.DeactivateInput();
                else playerInput.ActivateInput();
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
```

- [ ] **Step 2: Wait for Unity compile**

Console deve essere pulita. Se appare `The type or namespace name 'TMP_Text' could not be found`, verifica che il package **TextMesh Pro** sia importato (`Window > TextMeshPro > Import TMP Essential Resources`).

- [ ] **Step 3: Commit**

```powershell
git add "Assets/Scripts/Dialogue/DialogueManager.cs"
git commit -m "feat(dialogue): add DialogueManager singleton with typewriter and player lock"
```

---

## Task 3: Modify `BookManager` — pickup event + post-book dialogue

**Files:**
- Modify: `Assets/Scripts/Items/Objects/BookManager.cs`

- [ ] **Step 1: Add `using` for `UnityEvent`**

Trova:

```csharp
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
```

Sostituisci con:

```csharp
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.Events;
```

- [ ] **Step 2: Aggiungi i nuovi campi Inspector dopo il blocco "Pickup"**

Trova:

```csharp
    [Header("Pickup")]
    [Tooltip("L'oggetto libro nel mondo che verrà distrutto al primo utilizzo")]
    public GameObject worldBookObject;

    private bool _isOpen = false;
    private bool _hasBeenPickedUp = false;
    private int _lastToggleFrame = -1;
    private Coroutine _currentAnim;
```

Sostituisci con:

```csharp
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
```

- [ ] **Step 3: Invocare `onBookPickedUp` al primo pickup**

Trova:

```csharp
        if (!_hasBeenPickedUp)
        {
            _hasBeenPickedUp = true;
            if (worldBookObject != null)
            {
                Destroy(worldBookObject);
            }
        }
```

Sostituisci con:

```csharp
        if (!_hasBeenPickedUp)
        {
            _hasBeenPickedUp = true;
            if (worldBookObject != null)
            {
                Destroy(worldBookObject);
            }
            onBookPickedUp.Invoke();
        }
```

- [ ] **Step 4: Sostituire la chiusura con il wrapper coroutine che concatena il dialogo**

Trova:

```csharp
        if (_isOpen)
        {
            // Apri: fai scivolare verso il centro e blocca il player
            _currentAnim = StartCoroutine(SlideRoutine(onScreenPosition));
            SetPlayerMovement(false);
        }
        else
        {
            // Chiudi: fai scivolare verso il basso e sblocca il player
            _currentAnim = StartCoroutine(SlideRoutine(offScreenPosition));
            SetPlayerMovement(true);
        }
    }
```

Sostituisci con:

```csharp
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
```

- [ ] **Step 5: Wait for Unity compile**

Console pulita.

- [ ] **Step 6: Commit**

```powershell
git add "Assets/Scripts/Items/Objects/BookManager.cs"
git commit -m "feat(book): fire onBookPickedUp event and trigger post-book dialogue on first close"
```

---

## Task 4: Modify `MainMenuManager` — intro dialogue trigger

**Files:**
- Modify: `Assets/Scripts/Menu/MainMenuManager.cs`

- [ ] **Step 1: Aggiungi il campo `introDialogue`**

Trova:

```csharp
    [Header("Player Reference")]
    public GameObject player;
```

Sostituisci con:

```csharp
    [Header("Player Reference")]
    public GameObject player;

    [Header("Intro")]
    [Tooltip("Dialogo che parte al primo Play, dopo che la camera si è fermata sul player")]
    [SerializeField] private DialogueAsset introDialogue;
```

- [ ] **Step 2: Aggiungi la guardia su Escape durante un dialogo**

Trova:

```csharp
    private void Update()
    {
        if (!isTransitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isGameActive)
            {
                ReturnToMenu();
            }
            else if (!isFirstPlay)
            {
                PlayGame();
            }
        }
    }
```

Sostituisci con:

```csharp
    private void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return;

        if (!isTransitioning && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isGameActive)
            {
                ReturnToMenu();
            }
            else if (!isFirstPlay)
            {
                PlayGame();
            }
        }
    }
```

- [ ] **Step 3: Riscrivi `TransitionToGame` per gestire il primo play con dialogo**

Trova:

```csharp
    private IEnumerator TransitionToGame()
    {
        isTransitioning = true; 
        isGameActive = true;

        float camDuration = isFirstPlay ? originalCameraBlendTime : 0.5f;
        float uiDuration = isFirstPlay ? 1.5f : 0.3f;

        if (cameraBrain != null) 
        {
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(cameraBrain.DefaultBlend.Style, camDuration);
        }

        if (menuCamera != null) menuCamera.SetActive(false);

        SetPlayerMovement(true);

        float elapsedTime = 0f;
        Vector2 startPosition = menuUIContainer.anchoredPosition;
        Vector2 targetPosition = originalMenuPosition + new Vector2(-500f, 0f);

        while (elapsedTime < uiDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / uiDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(1f, 0f, smoothT);

            yield return null;
        }

        if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);
        
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        isFirstPlay = false; 
        isTransitioning = false; 
    }
```

Sostituisci con:

```csharp
    private IEnumerator TransitionToGame()
    {
        isTransitioning = true; 
        isGameActive = true;

        bool wasFirstPlay = isFirstPlay;

        float camDuration = wasFirstPlay ? originalCameraBlendTime : 0.5f;
        float uiDuration = wasFirstPlay ? 1.5f : 0.3f;

        if (cameraBrain != null) 
        {
            cameraBrain.DefaultBlend = new CinemachineBlendDefinition(cameraBrain.DefaultBlend.Style, camDuration);
        }

        if (menuCamera != null) menuCamera.SetActive(false);

        // Sul primo play teniamo il player bloccato fino alla fine del dialogo intro.
        if (!wasFirstPlay)
        {
            SetPlayerMovement(true);
        }

        float elapsedTime = 0f;
        Vector2 startPosition = menuUIContainer.anchoredPosition;
        Vector2 targetPosition = originalMenuPosition + new Vector2(-500f, 0f);

        while (elapsedTime < uiDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / uiDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (menuUIContainer != null) menuUIContainer.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, smoothT);
            if (menuCanvasGroup != null) menuCanvasGroup.alpha = Mathf.Lerp(1f, 0f, smoothT);
            if (menuBlurVolume != null) menuBlurVolume.weight = Mathf.Lerp(1f, 0f, smoothT);

            yield return null;
        }

        if (menuUIContainer != null) menuUIContainer.gameObject.SetActive(false);
        
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        isFirstPlay = false; 
        isTransitioning = false; 

        if (wasFirstPlay)
        {
            if (cameraBrain != null)
            {
                yield return new WaitWhile(() => cameraBrain.IsBlending);
            }

            if (introDialogue != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.PlayDialogue(introDialogue);
            }
            else
            {
                Debug.LogWarning("[MainMenuManager] introDialogue or DialogueManager.Instance is null on first play. Unlocking player as fallback.");
                SetPlayerMovement(true);
            }
        }
    }
```

- [ ] **Step 4: Wait for Unity compile**

Console pulita. Se appare `'CinemachineBrain' does not contain a definition for 'IsBlending'`, controlla che il package **Cinemachine 3** sia installato (Window > Package Manager > Cinemachine ≥ 3.x).

- [ ] **Step 5: Commit**

```powershell
git add "Assets/Scripts/Menu/MainMenuManager.cs"
git commit -m "feat(menu): trigger intro dialogue on first play after camera and UI settle"
```

---

## Task 5: Editor setup — assets, UI, GameObjects, Inspector wiring

> Tutti questi step sono **manuali** in Unity Editor. Dopo aver finito, salva la scena (`Ctrl+S`). La scena `.unity` cambierà → vedrai un diff in `git status`. Committeremo la scena alla fine.

### Sub-task 5.1: Creare gli asset di dialogo

- [ ] **Step 1: Creare la cartella `Assets/Dialogues/`**

In Project window, click destro su `Assets` → `Create > Folder`, rinomina `Dialogues`.

- [ ] **Step 2: Creare `IntroDialogue.asset`**

Click destro su `Assets/Dialogues` → `Create > Vellum > Dialogue Asset`. Rinomina `IntroDialogue`.

Selezionalo e nell'Inspector compila:
- **Dialogue Id:** `intro`
- **Lines** (Size = 3):

| # | Speaker | Text |
|---|---|---|
| 0 | `Narrator` | `And so the wanderer stepped into the woods, where every name was remembered.` |
| 1 | `Narrator` | `A book had been waiting there, hidden among the trees.` |
| 2 | `Narrator` | `When the wanderer found it, the pages opened at the press of [B].` |

- [ ] **Step 3: Creare `PostBookDialogue.asset`**

Stesso flusso: click destro → `Create > Vellum > Dialogue Asset`. Rinomina `PostBookDialogue`.
- **Dialogue Id:** `post_book`
- **Lines** (Size = 2):

| # | Speaker | Text |
|---|---|---|
| 0 | `Narrator` | `The pages whispered their secret, and the wanderer listened.` |
| 1 | `Narrator` | `Then they sought the tomb, and found it among the silent stones.` |

### Sub-task 5.2: Costruire `DialogueCanvas` in scena

- [ ] **Step 4: Creare il Canvas**

Apri la scena di gioco. Hierarchy → click destro → `UI > Canvas`. Rinomina `DialogueCanvas`.
- **Render Mode:** `Screen Space - Overlay`
- **Pixel Perfect:** off
- **Sort Order:** lascia 0 (o 1 se hai altri canvas che devono stare sotto)

- [ ] **Step 5: Aggiungere il `Panel`**

Sotto `DialogueCanvas`, click destro → `UI > Panel`. Rinomina `Panel`.
- **Anchor preset:** bottom-stretch (premi `Shift+Alt` quando clicchi il preset per far combaciare anche pivot+position).
- **Height:** ~300 (~30% di 1080).
- **Image:** colore semi-trasparente, es. `RGBA = 0, 0, 0, 200/255`.
- Aggiungi component `CanvasGroup` (Add Component → CanvasGroup). Setta **Alpha = 0** (sarà invisibile fino a che il dialogo non parte).

- [ ] **Step 6: Aggiungere `SpeakerLabel`**

Sotto `Panel`, click destro → `UI > Text - TextMeshPro` (se appare il prompt "Import TMP Essentials", cliccalo).
- Rinomina `SpeakerLabel`.
- Posizionalo in alto a sinistra del pannello.
- **Font Size:** ~28
- **Text:** lascia vuoto (verrà settato a runtime)

- [ ] **Step 7: Aggiungere `BodyText`**

Stesso flusso: click destro su `Panel` → `UI > Text - TextMeshPro`. Rinomina `BodyText`.
- Posizionalo al centro del pannello, con `Stretch` orizzontale.
- **Font Size:** ~26
- **Text:** lascia vuoto.
- **Alignment:** Center / Middle.

- [ ] **Step 8: Aggiungere `AdvanceHint`**

Click destro su `Panel` → `UI > Text - TextMeshPro`. Rinomina `AdvanceHint`.
- Posizionalo bottom-right.
- **Font Size:** ~18
- **Text:** `[Space] ▼`
- **Disabilita il GameObject** (deseleziona la checkbox in alto nell'Inspector). Verrà attivato a runtime.

### Sub-task 5.3: Creare il GameObject `DialogueManager`

- [ ] **Step 9: GameObject vuoto + componente**

Hierarchy → click destro → `Create Empty`. Rinomina `DialogueManager`.

Inspector → `Add Component` → cerca `DialogueManager` (lo script che hai creato al Task 2).

- [ ] **Step 10: Wire-up Inspector**

Sul componente `DialogueManager`:
- **Dialogue Canvas Group** → drag del `Panel` (ha il CanvasGroup).
- **Speaker Label** → drag di `SpeakerLabel`.
- **Body Text** → drag di `BodyText`.
- **Advance Hint** → drag del GameObject `AdvanceHint` (anche se è disabilitato, il riferimento è al GameObject).
- **Player** → drag del Player della scena (lo stesso usato da `BookManager`/`MainMenuManager`).
- **Fade Speed:** 8
- **Chars Per Second:** 30

### Sub-task 5.4: Wire-up `MainMenuManager` e `BookManager`

- [ ] **Step 11: `MainMenuManager.Intro Dialogue`**

Selezione il GameObject che contiene `MainMenuManager` in scena. Inspector → trova il nuovo campo `Intro Dialogue` (sotto la sezione Intro). Drag dell'asset `Assets/Dialogues/IntroDialogue.asset`.

- [ ] **Step 12: `BookManager.Post Book Dialogue`**

Seleziona il GameObject che contiene `BookManager`. Inspector → campo `Post Book Dialogue`. Drag dell'asset `Assets/Dialogues/PostBookDialogue.asset`.

### Sub-task 5.5: Gating della Tomba via UnityEvent

- [ ] **Step 13: Disabilitare l'`InteractableObject` della Tomba**

In Hierarchy, seleziona il GameObject della Tomba (quello che ha sopra `InteractableObject` con il prompt F + `onInteract` → `CinematicFallManager.StartFallSequence`).

Inspector → trova il componente `Interactable Object` → **deseleziona la checkbox in alto a sinistra** del componente. Il **GameObject deve restare attivo** (in alto in Inspector la checkbox del nome resta selezionata); è solo il **componente** ad essere disabilitato.

- [ ] **Step 14: Wire `BookManager.On Book Picked Up` → abilita InteractableObject della Tomba**

Seleziona il GameObject del `BookManager`. Inspector → trova la sezione **On Book Picked Up** (UnityEvent).

- Click sul `+` in basso a destra → si crea uno slot vuoto.
- **Object slot:** drag della Tomba (lo stesso GameObject del passo 13).
- **Function dropdown:** click → `Interactable Object` → `bool enabled` (è la versione setter di `Behaviour.enabled`).
- **Boolean argument:** spunta il checkbox (= `true`).

> ⚠ Importante: scegli `bool enabled` **sotto** la sezione `Interactable Object`, NON sotto `GameObject`. Quella sotto `GameObject` è `bool active` e attiverebbe/disattiverebbe il GameObject intero, perdendo tutto il GO.

- [ ] **Step 15: Salva la scena**

`File > Save` (o `Ctrl+S`). La scena `.unity` viene aggiornata.

- [ ] **Step 16: Commit della scena + asset**

```powershell
git add "Assets/Dialogues" "Assets/Scenes"
git commit -m "feat(scene): add DialogueManager + DialogueCanvas, wire intro/post-book dialogues, gate tomb"
```

> Se la scena non è in `Assets/Scenes/`, usa il path corretto. Verifica con `git status` prima del commit che siano in stage solo: i due `.asset` di dialogo, i loro `.meta`, e la scena `.unity` (+ il suo `.meta` se nuovo).

---

## Task 6: Validazione end-to-end in Play mode

> Nessuna modifica codice. Solo verifica funzionale.

- [ ] **Step 1: Primo Play (intro dialogue)**

Premi Play in Unity. Atteso:
1. La camera del menu fa il blend verso il player.
2. La UI del menu sfuma a 0.
3. Quando la camera si ferma, appare il `DialogueCanvas` con la prima riga: *"And so the wanderer stepped into the woods..."* — typewriter visibile.
4. Premendo **Space** durante il typewriter → la riga si completa subito.
5. Premendo **Space** di nuovo → passa alla riga 2.
6. Riga 3: *"...the pages opened at the press of [B]."*
7. Premendo Space all'ultima riga → fade out + il player diventa controllabile.
8. Cursore: invisibile e locked.

- [ ] **Step 2: Tomba pre-libro**

Avvicinati alla Tomba **senza** prendere il libro. Atteso:
- **Nessuna nuvoletta F** sopra il player.
- Premere F non fa niente.

- [ ] **Step 3: Pickup libro**

Vai dal libro nel mondo. Atteso:
- Nuvoletta F appare.
- Premi F → il libro viene "raccolto" (l'oggetto nel mondo sparisce), il libro UI scivola in alto.
- L'`InteractableObject` della Tomba ora è abilitato (verifica in Inspector durante la Play mode: la checkbox del componente è ora attiva).

- [ ] **Step 4: Post-book dialogue**

Premi **B** per chiudere il libro. Atteso:
1. Il libro UI esce verso il basso.
2. Dopo lo slide (~0.4s), appare il `DialogueCanvas` con: *"The pages whispered their secret..."*
3. Avanza con Space → riga 2: *"Then they sought the tomb..."*
4. Space → fade out, player libero di muoversi.

- [ ] **Step 5: Tomba post-libro**

Avvicinati alla Tomba. Atteso:
- Nuvoletta F appare.
- F → parte la cinematica di caduta (`CinematicFallManager.StartFallSequence`, comportamento esistente invariato).

- [ ] **Step 6: Re-open libro non ri-triggera dialogo**

Apri (B) e chiudi (B) il libro una seconda volta. Atteso:
- Il post-book dialogue **non** si ripete (controllato da `_hasShownPostBookDialogue`).

- [ ] **Step 7: Ritorno al menu + Play**

Premi Esc → menu. Click Play di nuovo. Atteso:
- Niente intro dialogue (`isFirstPlay` ora è false).
- Player sbloccato subito come prima.

- [ ] **Step 8: Escape durante intro è ignorato**

Stop Play. Riavvia Play (resetta `isFirstPlay`). Quando l'intro dialogue è in corso, premi Esc. Atteso:
- Esc viene ignorato, il dialogo continua.

- [ ] **Step 9: Se tutto OK → niente da committare**

Nessun cambio file. Termina la sessione.

Se qualcosa non va, leggi la Console: i warning del `DialogueManager` (riferimenti null, asset vuoto) ti diranno cosa è scollegato in Inspector.

---

## Self-Review

**1. Spec coverage:**
- Sezione 1 (obiettivo) → Tasks 1-5 coprono tutto.
- Sezione 2 (decisioni) → riflesso nel codice (Task 2 = typewriter+Space+lock, Task 3 = post-libro alla chiusura, Task 4 = intro al primo play, Task 5 = gating tomba).
- Sezione 3.1-3.4 (file/UI) → Task 1, 2, 5.1, 5.2.
- Sezione 4 (modifiche script) → Task 3, 4.
- Sezione 5 (data flow) → validato dal Task 6.
- Sezione 6 (testi) → Task 5.1.
- Sezione 7 (edge case) → coperti nel codice (warning paths, `_hasShownPostBookDialogue`, Update guard) e validati nei Step 6-8 del Task 6.
- Sezione 8 (editor steps) → Task 5 espande tutto passo-passo.
- Sezione 9 (file toccati) → consistente.

**2. Placeholder scan:** nessun TBD/TODO/"add error handling later". Tutti i blocchi di codice sono completi e copy-paste-ready.

**3. Type consistency:** `DialogueAsset`, `DialogueLine`, `DialogueManager.Instance`, `DialogueManager.IsPlaying`, `DialogueManager.PlayDialogue(DialogueAsset, Action)`, `BookManager.onBookPickedUp`, `BookManager.postBookDialogue`, `MainMenuManager.introDialogue` — tutti i nomi coincidono tra Tasks 1-5.

**4. Una cosa potenzialmente fragile:** in Task 5 Step 14 dipende dal nome esatto del setter `bool enabled` esposto da Unity nell'Inspector. In Cinemachine 3 + Unity 6 il dropdown mostra **`InteractableObject > bool enabled`** (sotto la sezione del componente). Se per qualche motivo non c'è, fallback: scegli `Behaviour > bool enabled` — fa la stessa cosa (`InteractableObject` deriva da `MonoBehaviour` → `Behaviour`).
