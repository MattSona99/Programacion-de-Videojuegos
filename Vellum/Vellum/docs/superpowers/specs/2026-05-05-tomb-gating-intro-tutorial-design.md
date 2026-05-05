# Design — Intro Dialogue, Post-Book Dialogue, Tomb Gating

**Data:** 2026-05-05
**Progetto:** Vellum (Unity 6000.3.x, URP, Cinemachine 3, new Input System)

---

## 1. Obiettivo

Aggiungere al gioco:

1. **Dialogo introduttivo narrativo** che parte solo al primo Play, dopo che la camera si è fermata sul player. Tre righe in inglese, stile cronaca al passato remoto ("the wanderer ... ", come se fosse un manoscritto già scritto).
2. **Dialogo post-libro** che parte alla prima chiusura del libro dopo la raccolta — invita il player a cercare la tomba.
3. **Gating della tomba**: prima della raccolta del libro, l'`InteractableObject` della tomba è inattivo (nessuna nuvoletta F, nessuna interazione). Si abilita silenziosamente al pickup del libro.

Il sistema dei dialoghi deve essere **scalabile**: il progetto avrà altri dialoghi in futuro.

---

## 2. Decisioni di design

| Tema | Scelta |
|---|---|
| Forma visiva | Pannello stile Genshin in basso — speaker label + body text |
| Numero di messaggi | 1 dialogo intro + 1 dialogo post-libro |
| Avanzamento | Tasto **Spazio**. Se la riga sta typewriter-ando, primo Spazio completa la riga; secondo Spazio passa alla successiva (skip pattern Genshin) |
| Player durante un dialogo | Sempre **bloccato** (intro e post-libro). Sblocca a fine dialogo |
| Comportamento tomba pre-libro | Niente nuvoletta F (gating silenzioso via `InteractableObject.enabled = false`) |
| Trigger post-libro | Prima **chiusura** del libro dopo la raccolta (lo costringe ad aprirlo davvero) |
| Effetto typewriter | Sì, ~30 char/s, con skip su Spazio |
| Speaker label | "Narrator" |
| Lingua | Inglese, narrazione al passato remoto |
| Storage dei dialoghi | ScriptableObject `DialogueAsset` (scelto per scalabilità futura) |

---

## 3. Architettura

### 3.1 Nuovi file

```
Assets/Scripts/Dialogue/
├── DialogueLine.cs       (struct serializable)
├── DialogueAsset.cs      (ScriptableObject: List<DialogueLine>)
└── DialogueManager.cs    (Singleton MonoBehaviour)

Assets/Dialogues/
├── IntroDialogue.asset
└── PostBookDialogue.asset
```

### 3.2 Componenti

#### `DialogueLine` (struct)

```csharp
[System.Serializable]
public struct DialogueLine
{
    public string speaker;
    [TextArea(2, 5)] public string text;
}
```

#### `DialogueAsset` (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Vellum/Dialogue Asset")]
public class DialogueAsset : ScriptableObject
{
    public string dialogueId;
    public List<DialogueLine> lines;
}
```

#### `DialogueManager` (Singleton MonoBehaviour)

Stato:
- `public static DialogueManager Instance`
- `public bool IsPlaying { get; private set; }`

Riferimenti Inspector:
- `CanvasGroup dialogueCanvasGroup`
- `TMP_Text speakerLabel` (o `Text` legacy se non c'è TMP)
- `TMP_Text bodyText`
- `GameObject advanceHint` (icona/testo "[Space]")
- `GameObject player`
- `float fadeSpeed = 8f`
- `float charsPerSecond = 30f`

API pubblica:
```csharp
void PlayDialogue(DialogueAsset dialogue, System.Action onComplete = null);
```

Comportamento:
1. Se `IsPlaying == true` → log warning, ignora.
2. Se `dialogue == null || dialogue.lines.Count == 0` → log warning, invoca `onComplete` subito.
3. Altrimenti: `IsPlaying = true`, lock player (`SetPlayerLocked(true)`), fade in canvas, parte la coroutine `RunDialogue`.
4. `RunDialogue` itera sulle righe:
   - Imposta `speakerLabel.text`.
   - Coroutine typewriter: aggiunge un carattere alla volta a `bodyText.text` a velocità `charsPerSecond`.
   - Se durante il typing `Keyboard.current.spaceKey.wasPressedThisFrame` → completa la riga (`bodyText.text = line.text`) e interrompe il typewriter.
   - Quando il typing è completato, mostra `advanceHint`, attende il prossimo `spaceKey.wasPressedThisFrame`.
   - Nasconde `advanceHint`, passa alla riga successiva (o termina).
5. Al termine: fade out canvas, `SetPlayerLocked(false)`, `IsPlaying = false`, invoca `onComplete`.

`SetPlayerLocked(bool)`: ricalca il pattern di `BookManager.SetPlayerMovement` (disabilita `ThirdPersonController`, `StarterAssetsInputs`, `PlayerInput`, `PlayerCombat`; azzera `Animator.Speed/MotionSpeed`; manda `MoveInput`/`SprintInput` a zero), **con una differenza**: il cursore resta `Locked + invisibile` in entrambi gli stati (durante un dialogo non serve il mouse).

> **Nota duplicazione:** è la 4ª copia del pattern di lock. Non viene estratto in helper in questo task (fuori scope: il design risolve due feature concrete). Se in futuro si aggiunge un 5° caso, vale la pena estrarre `PlayerInputLock` static helper.

### 3.3 Asset

`IntroDialogue.asset` — 3 righe Narrator (vedi §6.1).
`PostBookDialogue.asset` — 2 righe Narrator (vedi §6.1).

### 3.4 UI in scena

Nuovo `DialogueCanvas` (Canvas Screen Space – Overlay) con:
- `Panel` (Image semi-trasparente in basso, ~30% altezza schermo) con `CanvasGroup` (alpha 0 di default).
- `SpeakerLabel` (TMP_Text in alto a sinistra del pannello).
- `BodyText` (TMP_Text al centro del pannello).
- `AdvanceHint` (piccolo Image/TMP "[Space]" in basso a destra, attivato solo quando una riga ha finito di typewriter-arsi).

---

## 4. Modifiche agli script esistenti

### 4.1 `MainMenuManager.cs`

**Nuovo campo:**
```csharp
[Header("Intro")]
[SerializeField] private DialogueAsset introDialogue;
```

**`Update()`** — ignora Escape mentre un dialogo è in corso (impedisce di rientrare al menu durante l'intro):
```csharp
if (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) return;
```
da inserire all'inizio del check Escape.

**`TransitionToGame()`** — sul primo play:
1. **Non** chiamare `SetPlayerMovement(true)` (riga 273) sul primo play. Il player resta bloccato dallo `Start()`.
2. Dopo la fine del fade-out UI (post-while), e dopo aver finito le righe `Cursor.visible = false; Cursor.lockState = ...` (righe 299-300):
3. Aspetta che la camera abbia finito il blend: `if (cameraBrain != null) yield return new WaitWhile(() => cameraBrain.IsBlending);`
4. Se `wasFirstPlay && introDialogue != null && DialogueManager.Instance != null`: `DialogueManager.Instance.PlayDialogue(introDialogue);` — il `DialogueManager` sblocca il player a fine dialogo.
5. Se l'intro non c'è (asset mancante o manager non in scena), fallback: `SetPlayerMovement(true)` per non lasciare il player bloccato.
6. `isFirstPlay = false`, `isTransitioning = false` come oggi.

Sui play successivi (`isFirstPlay == false`): comportamento attuale invariato — `SetPlayerMovement(true)` viene chiamato come oggi.

### 4.2 `BookManager.cs`

**Nuovi campi:**
```csharp
[Header("Dialogo")]
[Tooltip("Dialogo che parte alla prima chiusura del libro dopo la raccolta")]
public DialogueAsset postBookDialogue;

[Header("Eventi")]
[Tooltip("Invocato la prima volta che il giocatore raccoglie il libro")]
public UnityEvent onBookPickedUp;
```

**Nuovo flag interno:** `private bool _hasShownPostBookDialogue = false;`

**`ToggleBookMenu()`:**
- Al primo pickup, dopo `Destroy(worldBookObject)`: `onBookPickedUp.Invoke();`
- Quando il libro chiude (`!_isOpen` ramo): invece di `_currentAnim = StartCoroutine(SlideRoutine(offScreenPosition));`, usare un nuovo wrapper `CloseAndMaybeShowDialogueRoutine` che:
  1. Esegue lo `SlideRoutine(offScreenPosition)` inline (`yield return SlideRoutine(...)`).
  2. Se `_hasBeenPickedUp && !_hasShownPostBookDialogue && postBookDialogue != null && DialogueManager.Instance != null`:
     - `_hasShownPostBookDialogue = true`
     - `DialogueManager.Instance.PlayDialogue(postBookDialogue);`

`SetPlayerMovement(true)` resta dov'è oggi (chiamato sincrono in `ToggleBookMenu`). Quando il `DialogueManager` parte, blocca di nuovo il player per la durata del dialogo, poi sblocca.

### 4.3 Tomba in scena

**Nessuna modifica al codice di `InteractableObject.cs`.**

In Inspector:
- Sull'`InteractableObject` della tomba: deselezionare la checkbox in alto del componente (parte disabilitato).
- Sul `BookManager`: nel campo `On Book Picked Up (UnityEvent)`, aggiungere un'entry:
  - **Object:** `InteractableObject` della tomba
  - **Function:** `Behaviour.enabled` (set as bool)
  - **Argument:** `true`

Effetto: prima del pickup, `OnTriggerEnter` non scatta perché il componente è disabilitato → niente nuvoletta. Dopo il pickup, il componente si attiva e tutto funziona normalmente.

---

## 5. Data flow

### 5.1 Sequenza primo Play

```
[Start scena]
  MainMenuManager.Start() → SetPlayerMovement(false)  [player locked, cursor visible]

[Click "Play"]
  PlayGame() → CloseAllSubPanels(false) → TransitionToGame()
  ├─ menuCamera off, Cinemachine inizia il blend verso il player
  ├─ NO SetPlayerMovement(true) sul primo play
  ├─ Loop fade-out UI menu (1.5s)
  ├─ Hide menu UI, EventSystem null, Cursor.visible=false, lockState=Locked
  ├─ yield WaitWhile(cameraBrain.IsBlending)   [aspetta che la camera si fermi]
  └─ DialogueManager.PlayDialogue(introDialogue)
      ├─ SetPlayerLocked(true)   [no-op, già locked; cursor resta hidden]
      ├─ Fade in DialogueCanvas
      ├─ Riga 1: typewriter "And so the wanderer..." → wait Space → next
      ├─ Riga 2: typewriter "A book had been waiting..." → wait Space → next
      ├─ Riga 3: typewriter "When the wanderer found it..." → wait Space → end
      ├─ Fade out DialogueCanvas
      └─ SetPlayerLocked(false)   [player unlocked, libero di muoversi]
```

### 5.2 Sequenza pickup libro + dialogo post-libro

```
[Player vicino al libro nel mondo]
  InteractableObject (libro).onInteract → BookManager.ToggleBookMenu()
  ├─ _hasBeenPickedUp = true, Destroy(worldBookObject)
  ├─ onBookPickedUp.Invoke()
  │   └─ Tomba.InteractableObject.enabled = true   [tomba ora interagibile]
  ├─ _isOpen = true
  ├─ SlideRoutine(onScreenPosition)   [libro entra a schermo]
  └─ SetPlayerMovement(false)   [player bloccato, cursor visible]

[Player preme B per chiudere]
  Update() rileva bKey → ToggleBookMenu()
  ├─ _isOpen = false
  ├─ SetPlayerMovement(true)   [player libero, cursor hidden]
  └─ CloseAndMaybeShowDialogueRoutine
      ├─ yield SlideRoutine(offScreenPosition)   [libro esce]
      └─ DialogueManager.PlayDialogue(postBookDialogue)
          ├─ SetPlayerLocked(true)   [player ri-bloccato per il dialogo]
          ├─ Fade in DialogueCanvas
          ├─ Riga 1: "The pages whispered..."
          ├─ Riga 2: "Then they sought the tomb..."
          ├─ Fade out DialogueCanvas
          └─ SetPlayerLocked(false)
  _hasShownPostBookDialogue = true   [dialogo non si ripete]

[Player avvicina la tomba]
  InteractableObject (tomba) ora attivo → nuvoletta F mostrata, F → onInteract
  → CinematicFallManager.StartFallSequence() (logica esistente)
```

---

## 6. Contenuto narrativo

### 6.1 Testi in inglese

**`IntroDialogue.asset`:**
| # | Speaker | Text |
|---|---|---|
| 1 | Narrator | And so the wanderer stepped into the woods, where every name was remembered. |
| 2 | Narrator | A book had been waiting there, hidden among the trees. |
| 3 | Narrator | When the wanderer found it, the pages opened at the press of [B]. |

**`PostBookDialogue.asset`:**
| # | Speaker | Text |
|---|---|---|
| 1 | Narrator | The pages whispered their secret, and the wanderer listened. |
| 2 | Narrator | Then they sought the tomb, and found it among the silent stones. |

> Tono: cronaca al passato remoto, "as if it were already written" — coerente con il titolo "Vellum" (pergamena/manoscritto).

---

## 7. Edge case e robustezza

| Scenario | Comportamento |
|---|---|
| Escape durante l'intro | Ignorato (`Update` controlla `DialogueManager.IsPlaying`) |
| Player rientra al menu e ri-clicca Play | `isFirstPlay = false` → niente intro |
| Player chiude e riapre il libro più volte | Solo prima chiusura mostra il post-book (`_hasShownPostBookDialogue`) |
| `DialogueManager.Instance == null` | Warning + fallback `SetPlayerMovement(true)` in `MainMenuManager`; in `BookManager` skip silenzioso (nessun dialogo, ma tomba comunque abilitata via `onBookPickedUp`) |
| `introDialogue` o `postBookDialogue` non assegnato | Warning + skip, gioco non si blocca |
| Player apre il libro mentre un dialogo è attivo | Non possibile: il `DialogueManager` lock impedisce input al player |
| Spazio premuto fuori da un dialogo | Nessun effetto (`DialogueManager.Update` legge la chiave solo se `IsPlaying`) |
| Cinematic della caduta (`CinematicFallManager`) | Non toccato. La tomba è già abilitata quando il player la raggiunge |

---

## 8. Modifiche da fare nell'editor Unity

Da fare **dopo** il merge del codice. Tutto in scena, nessuna modifica al codice oltre quelle dei §3-4.

### 8.1 Creare i file ScriptableObject

1. `Project window > Assets/Dialogues/` (creare cartella se non esiste).
2. Click destro → `Create > Vellum > Dialogue Asset`. Rinominare `IntroDialogue`.
3. Nell'Inspector di `IntroDialogue`: settare `Dialogue Id = "intro"` e popolare `Lines` con le 3 righe della tabella §6.1 (Speaker = "Narrator").
4. Ripetere per `PostBookDialogue` con le 2 righe.

### 8.2 Creare il `DialogueCanvas` in scena

1. Hierarchy → tasto destro → `UI > Canvas`. Rinominare `DialogueCanvas`. Render Mode = `Screen Space - Overlay`.
2. Aggiungere figli:
   - `Panel` (UI > Panel). Posizionarlo in basso, ~30% altezza schermo. Image semi-trasparente. Aggiungere `CanvasGroup`, alpha = 0.
   - Sotto `Panel`:
     - `SpeakerLabel` (UI > Text - TextMeshPro). Top-left del pannello. Font size ~24.
     - `BodyText` (UI > Text - TextMeshPro). Centro-inferiore del pannello. Font size ~28.
     - `AdvanceHint` (UI > Text - TextMeshPro o Image). Bottom-right. Testo "[Space]" o icona. Disattivato di default.

### 8.3 Aggiungere il `DialogueManager` in scena

1. Hierarchy → vuoto → rinominare `DialogueManager`.
2. Aggiungere componente `DialogueManager`.
3. Riempire i riferimenti Inspector:
   - `Dialogue Canvas Group` → `DialogueCanvas/Panel/CanvasGroup`
   - `Speaker Label` → `DialogueCanvas/Panel/SpeakerLabel`
   - `Body Text` → `DialogueCanvas/Panel/BodyText`
   - `Advance Hint` → `DialogueCanvas/Panel/AdvanceHint` (GameObject)
   - `Player` → il GameObject Player della scena
   - `Fade Speed` ~ 8
   - `Chars Per Second` ~ 30

### 8.4 Configurare `MainMenuManager`

- Inspector del `MainMenuManager`: settare il nuovo campo `Intro Dialogue` → `IntroDialogue.asset`.

### 8.5 Configurare `BookManager`

- Inspector del `BookManager`: settare `Post Book Dialogue` → `PostBookDialogue.asset`.
- Nello stesso Inspector, sotto `On Book Picked Up`:
  - Click `+` per aggiungere una entry.
  - Drag della **Tomba** GameObject nello slot Object.
  - Function: `InteractableObject > Behaviour.enabled` (boolean version).
  - Toggle `enabled` su `true` (checkbox).

### 8.6 Disabilitare l'`InteractableObject` della tomba

- Selezionare il GameObject della Tomba in Hierarchy.
- Sull'`InteractableObject` component: **deselezionare la checkbox** in alto a sinistra del componente (parte disabilitato).
- Lasciare il GameObject **attivo** (è il *componente* a essere disattivato, non l'oggetto).

---

## 9. File toccati

| File | Tipo | Note |
|---|---|---|
| `Assets/Scripts/Dialogue/DialogueLine.cs` | NEW | Struct serializable |
| `Assets/Scripts/Dialogue/DialogueAsset.cs` | NEW | ScriptableObject |
| `Assets/Scripts/Dialogue/DialogueManager.cs` | NEW | Singleton MonoBehaviour |
| `Assets/Dialogues/IntroDialogue.asset` | NEW | ScriptableObject instance |
| `Assets/Dialogues/PostBookDialogue.asset` | NEW | ScriptableObject instance |
| `Assets/Scripts/Menu/MainMenuManager.cs` | EDIT | +intro field, +Update guard, +TransitionToGame branch |
| `Assets/Scripts/Items/Objects/BookManager.cs` | EDIT | +postBookDialogue field, +onBookPickedUp event, +CloseAndMaybeShowDialogueRoutine |
| `Assets/Scripts/Items/InteractableObject.cs` | NO CHANGE | Gating via Inspector + UnityEvent (regola n.5 CLAUDE.md) |

Scena: nuovo `DialogueCanvas` + `DialogueManager` GameObject; Inspector wiring di `MainMenuManager`, `BookManager`, e disabilitazione `InteractableObject` della tomba.
