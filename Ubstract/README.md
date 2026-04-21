# 🎮 Ubstract

Ubstract is a **frantic and adrenaline-pumping 2D action game** based on one-on-one fights with unique and challenging bosses. The game tests your timing skills, reaction time, and defensive strategy in a confined arena where only the best survive.

## 📋 Game Description

**Ubstract** is a roguelike action game where players face off against three distinct bosses, each with their own unique fighting style and specific attack patterns. The core of gameplay is based on a **perfect parry system (frame-perfect blocking)** that not only mitigates incoming damage but also allows you to counterattack and earn valuable rewards.

### Game Type
- **Genre**: Action / Boss-Fight Arena
- **Perspective**: 2D Side-View
- **Mode**: Single-Player Campaign
- **Platform**: Built with Unity

## 🎯 Core Mechanics

### Offensive Combat
- **Attack Combos**: Execute a sequence of 2 hits to deal damage
  - Each hit deals 2-3 damage to the enemy
  - Must be grounded to be effective
  
### Defensive System
- **Simple Block**: Hold down the right mouse button to block incoming attacks
  - Significantly reduces damage taken
  
- **Perfect Parry**: Block damage at the exact moment the attack hits you (0.2 second window)
  - **Benefits**:
    - Reflect damage back to the enemy
    - 30% chance to get a health drop
    - Fundamental mechanic for victory

### Movement and Physics
- Smooth 2D movement with jumping
- Ground detection for jump control
- Movement restrictions during combat actions

### Lives and Health System
- **Starting Health**: 100 HP
- **Healing Items**: Drop randomly after a successful perfect parry
- When life run out → Game Over

## 👹 The Three Bosses

Each boss features a progression of difficulty and unique mechanics:

### Boss 1 - Range/Melee Hybrid
- **Fighting Style**: Range attacks with both ranged and close combat
- **Patterns**: Patrol, Chase, Burst Fire
- **Difficulty**: Beginner - Intermediate

### Boss 2 - Acrobatic Master
- **Fighting Style**: Advanced parabolic jumps and bounce mechanics
- **Patterns**: Aerial Attacks, Wall Bounces, Ceiling Phase Transitions
- **Difficulty**: Intermediate - Advanced

### Boss 3 - State-Driven Fighter
- **Fighting Style**: Proximity-based combos with special mode
- **Patterns**: Spawn Phase, Proximity Combos, "Crazy Mode"
  - **Crazy Mode**: Shield-protected phase that breaks after 4 perfect parries, applies knockback
- **Difficulty**: Advanced - Expert

## 📊 Scoring System and Leaderboard

Ubstract tracks detailed metrics to evaluate your performance:

### Tracked Statistics
- Enemies defeated
- Perfect parries executed
- Successful normal blocks
- Damage dealt
- Damage blocked
- Health lost
- Time taken

### Leaderboard
- Ranking of top players
- Persistent record saving
- Classification based on final performance

## 📁 Folder Structure

```
Ubstract/
├── Assets/
│   ├── Animations/               # Animation controllers and clips
│   │   ├── Arenas/              # Arena animations
│   │   ├── Clips/               # Generic animation clips
│   │   ├── Controllers/         # Animation Controllers
│   │   ├── Enemies/             # Enemy/boss animations
│   │   ├── Items/               # Item animations (health drops, etc)
│   │   ├── Menu/                # Menu UI animations
│   │   └── Player/              # Player animations
│   │
│   ├── Audio/                    # Game audio management
│   │   ├── MainMixer.mixer      # Main audio mixer
│   │   ├── Music/               # Level music tracks
│   │   └── SFX/                 # Sound effects
│   │
│   ├── Prefabs/                  # Reusable prefab objects
│   │
│   ├── Resources/                # Runtime loaded resources
│   │
│   ├── Scenes/                   # Game scenes
│   │
│   ├── Scripts/                  # C# code organized in subfolders
│   │   ├── Core/                # Game management and core logic
│   │   │   ├── GameManager.cs               # Main game manager
│   │   │   ├── DataManager.cs               # Persistent data management
│   │   │   ├── MatchTracker.cs              # Match statistics tracking
│   │   │   └── SceneTransitionManager.cs    # Scene transitions
│   │   │
│   │   ├── Player/               # Player logic
│   │   │   ├── PlayerController.cs          # Movement and input control
│   │   │   ├── PlayerCombat.cs              # Combat system
│   │   │   ├── PlayerHealth.cs              # Health and lives system
│   │   │   ├── PlayerAnimator.cs            # Animation management
│   │   │   └── PlayerAudio.cs               # Player audio feedback
│   │   │
│   │   ├── EnemyAI/              # Boss artificial intelligence
│   │   │   ├── EnemyAI_1.cs                 # Boss 1 - Range/Melee Hybrid
│   │   │   ├── EnemyAI_2.cs                 # Boss 2 - State-Driven
│   │   │   ├── EnemyAI_3.cs                 # Boss 3 - Acrobatic
│   │   │   ├── EnemyHealth.cs               # Enemy health system
│   │   │   └── EnemyAudio.cs                # Boss-specific audio
│   │   │
│   │   ├── Arena/                # Combat arena logic
│   │   │   └── ArenaManager.cs              # Arena management
│   │   │
│   │   ├── Audio/                # Game audio system
│   │   │   ├── MenuAudioManager.cs          # Menu audio
│   │   │   ├── GameplayAudioManager.cs      # Gameplay audio
│   │   │   └── AudioHandler.cs              # Generic audio handler
│   │   │
│   │   ├── UI/                   # User interface logic
│   │   │   ├── MainMenuUI.cs                # Main menu
│   │   │   ├── GameplayUI.cs                # In-game UI
│   │   │   ├── HealthBar.cs                 # Player health bar
│   │   │   ├── EnemyHealthBar.cs            # Enemy health bar
│   │   │   ├── LeaderboardUI.cs             # Leaderboard display
│   │   │   ├── VictoryPanel.cs              # Victory panel
│   │   │   ├── GameOverPanel.cs             # Game over panel
│   │   │   ├── PauseMenu.cs                 # Pause menu
│   │   │   ├── SettingsPanel.cs             # Settings panel
│   │   │   ├── CRTEffectController.cs       # CRT visual effect
│   │   │   └── PlayerNameInput.cs           # Player name input
│   │   │
│   │   └── Models/               # Data models
│   │       ├── ScoreData.cs                 # Score data structure
│   │       └── PlayerData.cs                # Player persistent data
│   │
│   ├── Settings/                 # Game configuration resources
│   │
│   ├── Shaders/                  # Custom shaders
│   │
│   ├── Sprites/                  # Sprite assets (characters, enemies, UI)
│   │
│   └── TextMesh Pro/             # TextMesh Pro resources for UI
│
├── ProjectSettings/              # Unity project configuration
│   ├── AudioManager.asset        # Global audio settings
│   ├── DynamicsManager.asset     # Physics 2D/3D settings
│   ├── Physics2DSettings.asset   # Specific 2D physics configuration
│   ├── QualitySettings.asset     # Graphics quality levels
│   ├── InputManager.asset        # Input configuration
│   ├── TagManager.asset          # Tags and Layers
│   └── ...
│
├── Packages/
│   ├── manifest.json             # Package dependencies
│   └── packages-lock.json        # Package versions lock file
│
├── Logs/                         # Debug and error logs
│
└── README.md                     # This file
```

## 🎮 How to Play

### Basic Controls
- **Movement**: Arrow keys or `AD` for Left and Right
- **Jump**: `SPACE`
- **Attack**: Left mouse button (2-hit combo)
- **Block**: Hold right mouse button
- **Perfect Parry**: Block at the exact moment the attack hits you (requires precision!)
- **Pause**: `ESC` key

### Game Flow
1. **Main Menu**: Enter your name and select a level
2. **Pre-Match**: Position yourself and prepare for the fight
3. **Combat**: Face the boss using attacks, blocks, and parries
4. **Victory/Defeat**: View statistics and your leaderboard position
5. **Leaderboard**: Compare your results with other players

### Recommended Strategies
- **Perfect Parry is Key**: Practice timing to master the parry - it's the best way to deal damage
- **Know Your Bosses**: Each boss has specific patterns - learn them to anticipate attacks
- **Manage Your Health**: Use blocks strategically to preserve health
- **Collect Drops**: After a successful parry, collect the health that falls

## ⚙️ Technical Requirements

- **Engine**: Unity 2022.3+ (URP - Universal Render Pipeline)
- **Target Platform**: Windows/PC
- **Recommended Resolution**: 1920x1080 or higher
- **Minimum Requirements**: GPU with OpenGL 3.2+ support

### Unity Packages Used
- Universal Render Pipeline (URP)
- TextMesh Pro
- Input System (optional)

## 📦 How to Run the Project

### Open in Unity
1. Clone or download the repository
2. Open Unity Hub
3. Add the Ubstract project by clicking "Add project from disk"
4. Select the `Ubstract/` folder
5. Wait for the project to load (compilation may take time)

### Run the Game
1. Open the `Boot` scene from `Assets/Scenes/`
2. Press `Play` in the Unity editor or Build the project to run as standalone

### Build
To create a standalone executable:
1. File → Build Settings
2. Select scenes to include
3. Choose target platform (Windows PC)
4. Click "Build" and select output folder

## 🎨 Graphics and Audio Features

### Advanced Audio System
- Centralized audio mixer with volume control for music and SFX
- Separate audio managers for menu and gameplay
- Sound feedback for every player and enemy action
- Dynamic music for each level

### Visual Effects
- Customizable retro CRT effect
- Smooth animations with state transitions
- Shader system for visual effects

### Refined UI/UX
- Health bars with smooth lerp animation
- Intuitive pause panel
- Settings menu for audio and gameplay
- Leaderboard display with ranking
- Elegant scene transitions

## 🔧 Code Architecture

Ubstract uses a modular and scalable structure:

### Design Patterns
- **Manager Pattern**: GameManager, DataManager, AudioManager centralize logic
- **State Pattern**: EnemyAI uses states for boss behavior
- **Event System**: Loose-coupled communication between systems
- **MVC-like**: Separation between logic (Controllers) and presentation (UI)

### Data System
- **Persistent Data Storage**: Saves player scores and preferences
- **Match Tracking**: Tracks detailed statistics for each match
- **Leaderboard System**: Persistent ranking of players

## 📈 Expansion Potential

The game is built to be easily extensible:
- **New Bosses**: Add new EnemyAI classes
- **New Levels**: Create new scenes and modify difficulty parameters
- **New Attacks**: Expand the player combo system
- **New Mechanics**: Add power-ups, special items, or game modes
- **Multiplayer Mode**: The foundation is ready for multiplayer addition

## 📝 Development Notes

- The game is optimized for 16:9 resolution
- Input system can be easily mapped to gamepad
- When adding new enemies, ensure you create the corresponding AnimationControllers
- The DataManager saves data in `Application.persistentDataPath`

## 📄 License

See the `LICENSE` file in the main repository for license details.

## 🤝 Contributions

If you want to contribute to Ubstract:
1. Fork the repository
2. Create a branch for your feature (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

**Have fun playing Ubstract and test your skills in boss combat! 🎮⚔️**
