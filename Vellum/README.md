# 📜 Vellum

**Vellum** is a **narrative-driven 3D action-adventure** built in **Unity 6 (URP)**. You play through three self-contained *Acts*, each with a completely different core mechanic — a memory path puzzle, a wave-based arena, and a mirror boss duel — accompanied by **Jammo**, a small robot companion who guides you, builds with you, and fights at your side.

Where most projects reuse the same loop in every level, Vellum was built as a showcase of **distinct gameplay systems and algorithms**: a custom **fuzzy-logic AI engine**, **procedural path generation**, **crowd steering**, a **boss finite-state machine**, and shader-driven cinematics.

---

## 📋 Game Description

Vellum opens on a prologue (an old book and a narrator speaking in the past tense) and unfolds across three Acts. The protagonist is escorted by **Jammo**, a companion robot that is mechanically central to two of the three levels (he assembles a statue in Act 2 and ferries relic pieces in Act 3).

### Game Type
- **Genre**: Narrative Action-Adventure (puzzle + melee combat)
- **Perspective**: 3D Third-Person
- **Mode**: Single-Player, story-driven (linear Acts)
- **Engine**: Unity 6000.3.x — Universal Render Pipeline (URP)
- **Camera**: Cinemachine 3
- **Input**: Unity's new Input System (`StarterAssets` + `PlayerInput`)

---

## 🎯 Core Mechanics

### Movement
- Third-person locomotion via **StarterAssets `ThirdPersonController`** and the **new Input System**.
- Camera framing and cinematic blends handled by **Cinemachine 3** (priority-driven virtual cameras).

### Melee Combat
- **Combo → Finisher**: light hits play on a **masked upper-body Animator layer**; after `comboHitsBeforeFinisher` hits the next input triggers a **full-body finisher** (the masked layer blends back to weight 0).
- **Defense**: a guard/block state (`PlayerCombat.IsDefending`) and a directional `FrontalShieldBlock` in the combat layer.
- Data-driven damage pipeline: attacks send a `DamageInfo` to any `IDamageable`; reactions (`IDamageReaction`) and filters (`IDamageFilter`) let targets respond to or veto hits. `KnockbackReceiver` applies impact.

### Health & Pickups
- `Health` is the shared component for Player, enemies, Jammo and the boss (normalized 0..1, `IsDead`, death events).
- Health drops spawn from defeated enemies (`HealthPickup` for the Player, a separate Jammo-tuned pickup), with configurable drop chance/share.

### Companion: Jammo
- `JammoCompanion` / `JammoCarrier` — follows, picks up objects, carries them to a target, and plays a role in the level objective itself.

### Interaction & Dialogue
- **`InteractableObject`** uses Unity **`UnityEvent`s** (no hard-coded coupling) on trigger colliders; **`InteractionUIManager`** is a screen-space singleton showing interaction "speech bubbles".
- **Dialogue** is data-driven: `DialogueAsset` (a list of `DialogueLine`) played by `DialogueManager`, which can lock/unlock the player during conversations.

---

## 🗺️ The Three Acts

### Act 1 — The Path Puzzle  *(`Assets/Scripts/Level_1/`)*
A **memory puzzle** played on a grid of tiles. A **correct path is generated procedurally** every run (fixed start tile, fixed end tile, and a *mandated penultimate tile* so the arrival direction is always consistent). When the puzzle starts, the **global floor is switched off** — only the correct tiles hold you up, so stepping on a wrong tile makes you fall and respawn after a short delay.

- **Objective**: cross the room by memorizing and walking the generated path.
- **Mechanics**: a limited-charge **memory hint** lights up the next N correct tiles; Jammo acts as an in-world guide (`JammoGuideController`); on completion a door **builds itself and opens** (`DoorBuildController` / `DoorPortal`).
- **Algorithms used here**: *Procedural Path Generation* (randomized self-avoiding backtracking + Warnsdorff heuristic) and *Fuzzy aesthetic scoring* of candidate paths — see the Algorithms section.

### Act 2 — The Arena & the Statue  *(`Assets/Scripts/Level_2/`)*
A **wave-based combat arena**. Enemies spawn from the surrounding walls (`WaveManager`) while **Jammo collects the pieces dropped during the fight and assembles a statue** on a pedestal (`StatueAssemblyDirector`, `StatueRig`, `PieceSpawner`, `JammoCarrier`). The arena does **not** end after a fixed number of waves — it ends when the **statue is complete** (waves loop until then).

- **Objective**: survive and protect Jammo long enough to finish the statue.
- **Mechanics**: enemies use **fuzzy-logic decision-making** and surround their target in an **angular "fan"** formation; killed enemies may drop healing for the Player or for Jammo. A cinematic director (`Act02Director`) frames a prologue on the statue, runs the waves, then plays an epilogue and loads **Act 3**.
- **Algorithms used here**: *Mamdani Fuzzy Inference* (enemy aggression), *Crowd Steering & Encirclement*, *Object Pooling*, *spawn rejection sampling*.

### Act 3 — "The Mirror of Water"  *(`Assets/Scripts/Level_3/`)*
The finale: a **duel against a doppelganger** that mirrors the Player's own appearance (`EnemySkinMirror`, `MirrorCameraSync`). The fight runs in **two identical phases — Sun, then Moon** (`MirrorDuelDirector`), each in two beats:

1. **Collection** — the enemy is immune; every few of the Player's hits **unlocks** one relic piece on the enemy altar, and **Jammo ferries it** to the Player's altar.
2. **Damage window** — once all pieces are delivered, the enemy becomes vulnerable (down to 50% in the Sun phase, down to 0 = Win in the Moon phase).

- **Moon phase twist**: the boss is empowered and may **break off to intercept Jammo** mid-carry — if hit, the piece returns to the enemy altar and Jammo takes damage.
- **Mechanics**: boss AI driven by a **finite-state machine** with **reactive defense**; the Sun→Moon transition rotates the celestial bodies and **swaps skyboxes/world layers** rather than crossfading (`MirrorFlipDirector`).
- **Algorithms used here**: *Boss Finite-State Machine* + reactive guard, *Fuzzy decision-making* (whether to chase Jammo), *mirror/skybox layer swap*.

---

## 🧠 Algorithms & AI Techniques

This is where most of the project's engineering effort went. Every technique below maps to a real file under `Assets/Scripts/`.

### 1. Mamdani Fuzzy Inference Engine — `AI/Fuzzy/`
A from-scratch, reusable **fuzzy-logic controller** (`FuzzyController`):

1. **Fuzzify** crisp inputs through linguistic sets (triangular / shoulder membership functions in `MembershipFunction`).
2. **Rule strength** = `min` of the antecedents' membership degrees (logical AND).
3. **Aggregate** each output's clipped consequent sets with `max`.
4. **Defuzzify** with a **sampled centroid** over the output domain.

It is built once via a fluent `Builder` (`.Rule().If(...).And(...).Then(...)`), then `Evaluate()` runs **allocation-free** (preallocated strength buffer, inputs/outputs as aligned `float[]`). The engine is **shared and immutable**, so every agent keeps only its own small I/O buffers.

**Used for three different problems:**
- **Enemy aggression** (`EnemyFuzzyBrain`, Act 2): inputs *distance / health / crowding* → output *aggression*, which modulates attack cooldown (high aggression ⇒ shorter cooldown, coordinated rush when in a group).
- **Boss intent** (`BossFuzzyBrain`, Act 3 Moon phase): decides whether the boss should keep pressuring the Player or break off to intercept a piece-carrying Jammo.
- **Procedural path quality** (`FuzzyPathEvaluator`, Act 1): scores how "nice" a generated path looks (see below).

### 2. Procedural Path Generation — `Level_1/PathGeneration/SelfAvoidingPathGenerator`
Generates a **4-directional self-avoiding path of exact length** between two cells (every cell used once ⇒ no crossings by construction):

- **Randomized backtracking** with a per-attempt node budget; restarts with a fresh random ordering if a branch goes pathological.
- **Manhattan + parity pruning**: prunes any branch whose remaining moves can't possibly reach the target with the right parity.
- **Warnsdorff heuristic**: among legal moves, prefers the neighbor with the fewest free exits (tie-break after a random shuffle) — fast convergence and varied paths.
- **8-neighbour spacing** and an **anti-zig-zag** rule (no two consecutive turns) keep parallel corridors apart and the path readable.
- **Multi-candidate selection**: several candidates are generated and the one with the **highest fuzzy aesthetic score** (turn density, edge proximity, spread) is kept.

This produces a puzzle path that is **different every run but consistently well-shaped**.

### 3. Crowd Steering & Encirclement — `Enemies/EnemyAI` + `Enemies/EnemyTargetCoordinator`
Arena enemies move without a NavMesh, using lightweight steering:

- **Boids-like separation**: each enemy is pushed away from nearby peers with an inverse-square weight (`Physics.OverlapSphereNonAlloc`, no per-frame allocations).
- **Angular "fan" encirclement**: a shared `EnemyTargetCoordinator` assigns each attacker an **angular slot** around the shared target, so they spread out instead of stacking in a single-file queue.
- **Dynamic targeting**: enemies chase Jammo by default but switch to the Player (claiming a limited Player "slot") when the Player damages them (`IDamageReaction.OnDamaged`).
- Safe stop distance derived from the actual capsule radii (never shoves the target's collider).

### 4. Boss Finite-State Machine — `Level_3/BossDuelAI`
The final boss uses a **windowed FSM** for varied pacing: **Aggro** (chase + multi-hit combo), **Reposition** (run to a random nearby point), **Guard** (hold guard and bait), plus **Kite** and **SeekHealth** behaviors in the empowered Moon phase. On top of the state machine sits a **reactive defense layer**: when it detects the Player's swing starting (`PlayerMeleeAttack.IsSwinging`) at close range and facing it, it raises its guard *probabilistically* to block that specific hit. Locomotion uses a walk/run blend tree on real m/s `Speed`.

### 5. Object Pooling & Spawn Distribution — `Utils/SimplePool` + `Level_2/WaveManager`
- **`SimplePool`**: a `Queue`-backed pool that recycles instances via `SetActive` instead of `Destroy` (per the project's performance conventions) — used for enemies and their corpses.
- **Spawn rejection sampling**: within a wave, spawn points are re-rolled up to *N* tries to respect a **minimum separation**, preventing enemies from overlapping at birth. Waves loop until the win condition is met.

### 6. Cinematics, Cameras & Visuals
- **Cinemachine 3 priority blends** sequenced through **coroutines** (`Act02Director`, `CinematicFallManager`) following the project's *lock input → camera → timed sequence → restore* pattern.
- **Mirror world / sky flip** (`MirrorFlipDirector` + `MirrorCameraSync`): a Sun↔Moon transition that rotates celestial bodies, crossfades lights, and **swaps skyboxes and `UpWorld`/`DownWorld` layers** at the midpoint (two skyboxes can't crossfade without a blend shader, so the swap is masked by the rotation).
- **Animator hot-swap** (`PlayerSkinSwitcher`): swaps the player's 3D model at runtime and calls **`Animator.Rebind()`** so animations re-align to the new rig.
- **Global shader communication** via `Shader.SetGlobalVector` / `SetGlobalFloat` (URP Shader Graph) instead of per-material references — used for effects like the grey disintegration/expansion VFX.

---

## 🎮 How to Play

### Controls (new Input System)
- **Move**: `WASD` / left stick
- **Look / Camera**: mouse / right stick (Cinemachine)
- **Jump**: `Space`
- **Attack (combo → finisher)**: left mouse button
- **Defend / Block**: dedicated defend input (held)
- **Interact**: interaction key on `InteractableObject` triggers (shows a speech-bubble prompt)
- **Pause / Menu**: `Esc`

> Bindings live in `Assets/InputSystem_Actions.inputactions` and can be remapped (gamepad-ready).

### Game Flow
1. **Main Menu** → start a new game (`MainMenuManager`).
2. **Prologue** → the book + narrator set up the story.
3. **Act 1** → solve the path puzzle to open the door.
4. **Act 2** → survive the arena while Jammo completes the statue.
5. **Act 3** → win the two-phase mirror duel.

---

## 📁 Project Structure

> All hand-written gameplay code lives in `Assets/Scripts/`. Everything under `Assets/Downloaded/` is third-party. `Library/`, `Temp/`, `Logs/`, `obj/`, `docs/`, etc. are not tracked (see `.gitignore`).

```
Assets/Scripts/
│
├── Player/                      # Player character
│   ├── PlayerCombat.cs          # Combo + masked-layer finisher, defend
│   ├── PlayerMeleeAttack.cs     # Swing windows / hit detection
│   ├── MeleeFinisherState.cs    # Finisher StateMachineBehaviour
│   ├── PlayerHealth.cs          # Player HP + Game Over hook
│   ├── PlayerSkinSwitcher.cs    # Runtime model hot-swap + Animator.Rebind()
│   └── AnimationEventForwarder.cs
│
├── Combat/                      # Shared, data-driven combat
│   ├── Health.cs                # HP component for all actors
│   ├── IDamageable.cs / DamageInfo.cs
│   ├── IDamageReaction.cs / IDamageFilter.cs
│   ├── KnockbackReceiver.cs
│   └── FrontalShieldBlock.cs
│
├── AI/Fuzzy/                    # Custom Mamdani fuzzy engine
│   ├── FuzzyController.cs        # Inference engine + fluent Builder
│   ├── FuzzyVariable.cs / FuzzyRule.cs / MembershipFunction.cs
│
├── Enemies/                     # Arena enemy AI (Act 2)
│   ├── EnemyAI.cs                # Steering, fan encirclement, fuzzy aggression
│   ├── EnemyFuzzyBrain.cs        # Distance/health/crowding → aggression
│   └── EnemyTargetCoordinator.cs # Angular slots, Player/Jammo targeting
│
├── Level_1/                     # Act 1 — Path Puzzle
│   ├── PathPuzzleManager.cs      # Puzzle flow, hints, fail/respawn
│   ├── PathGeneration/
│   │   ├── SelfAvoidingPathGenerator.cs  # Backtracking + Warnsdorff
│   │   ├── FuzzyPathEvaluator.cs          # Aesthetic scoring
│   │   └── GridPath.cs
│   ├── PathTile.cs / DoorBuildController.cs / DoorPortal.cs
│   └── JammoGuideController.cs
│
├── Level_2/                     # Act 2 — Arena & Statue
│   ├── WaveManager.cs            # Waves, pooling, spawn distribution, drops
│   ├── StatueAssemblyDirector.cs # Jammo assembles the statue
│   ├── StatueRig.cs / PieceSpawner.cs / PickupRotator.cs
│   ├── JammoCarrier.cs / JammoHealth.cs / JammoPartSet.cs
│   ├── Act02Director.cs          # Prologue/epilogue cinematics → Act_03
│   └── VortexEnterTransition.cs
│
├── Level_3/                     # Act 3 — The Mirror of Water
│   ├── MirrorDuelDirector.cs     # Two-phase (Sun/Moon) duel orchestration
│   ├── BossDuelAI.cs             # Boss FSM + reactive defense
│   ├── BossFuzzyBrain.cs         # Moon-phase chase decision (fuzzy)
│   ├── BossShield.cs / DuelHealthSpawner.cs
│   ├── MirrorFlipDirector.cs     # Sky/world flip (skybox + layer swap)
│   └── EnemySkinMirror.cs
│
├── JammoCompanion/              # Companion robot (follow/activate)
├── Items/                       # InteractableObject + InteractionUIManager
│   └── Objects/                 # BookManager, CinematicFallManager, pickups
├── Dialogue/                    # DialogueAsset / DialogueLine / DialogueManager
├── UI/                          # PlayerHUD, StatueProgressBar, HudReveal,
│                                #   CRTController, SettingsUI, button styling
├── Menu/                        # MainMenuManager
└── Utils/                       # SimplePool, AnimatorParameterCache,
                                 #   TileGridGenerator, ForestSower (+ Editor tools)
```

---

## ⚙️ Technical Requirements

- **Engine**: Unity **6000.3.x** (version pinned in `ProjectSettings/ProjectVersion.txt`)
- **Render Pipeline**: Universal Render Pipeline (URP) — all shaders/materials are URP-compatible
- **Target Platform**: Windows / PC
- **Recommended Resolution**: 1920×1080

### Unity Packages Used
- Universal Render Pipeline (URP)
- **Cinemachine 3** (`CinemachineCamera`, not the legacy `CinemachineVirtualCamera`)
- **Input System** (new) + StarterAssets (Third-Person)
- Shader Graph (URP Lit)
- TextMesh Pro

---

## 📦 How to Run the Project

### Open in Unity
1. Clone the repository.
2. Open **Unity Hub** → *Add project from disk* → select the `Vellum/Vellum/` folder.
3. Let Unity import (first compile can take a while).

### Play
1. Open the menu/boot scene from `Assets/Scenes/`.
2. Press **Play** in the editor, or build a standalone.

### Build
1. *File → Build Settings*
2. Add the scenes (Menu → `Act_01` → `Act_02` → `Act_03`) in order.
3. Choose **Windows PC** and build.

---

## 🔧 Code Architecture & Conventions

- **UnityEvent interaction**: behaviors are wired in the Inspector via `InteractableObject` rather than hard-coded references — fast to iterate without recompiling.
- **Singleton UI managers**: e.g. `InteractionUIManager.Instance`, screen-space UI following `Camera.main.WorldToScreenPoint`.
- **Interface-based combat**: `IDamageable` / `IDamageReaction` / `IDamageFilter` decouple who deals damage from who reacts.
- **Cinematic directors** follow a fixed recipe: **lock input → drive Cinemachine priorities → timed coroutine → restore state**.
- **Performance-first**: cached references (no `Find*` in hot paths), `OverlapSphereNonAlloc`, allocation-free fuzzy evaluation, **global shader properties** over per-material loops, and **`SetActive(false)` + pooling** instead of `Destroy()`.
- **Naming**: private `_camelCase`, public/`SerializeField` `camelCase`, `PascalCase` methods/classes, `UPPER_SNAKE_CASE` constants.

> See `CLAUDE.md` for the full set of project conventions and AI-assistant rules.

---

## 📈 Expansion Potential

- **New enemies**: add an `EnemyAI`-compatible prefab; reuse `EnemyFuzzyBrain` or extend the rule base.
- **New waves**: configure additional `Wave` entries (spawn wall, prefab, count, spread).
- **New Acts**: add a scene + a director coroutine following the existing pattern.
- **Tune the AI**: fuzzy behavior changes are just edits to the linguistic rule base — no control-flow rewrites.
- **New interactions**: drop an `InteractableObject` and wire `UnityEvent`s in the Inspector.

---

## 📝 Development Notes

- Code comments are written in Italian (project language); this README is the English overview.
- The fuzzy engine is intentionally generic and shared — prefer adding **rules**, not new branching logic.
- When swapping player skins, always keep the `Animator.Rebind()` call or animations will desync.
- The path generator requires start/end/penultimate tiles to satisfy Manhattan-distance **and parity** constraints, or it will (by design) throw a descriptive error.

---

## 📄 License

See the `LICENSE` file in the repository root for license details.

---

**Walk the path, raise the statue, and face yourself in the Mirror of Water. 📜🤖⚔️**
