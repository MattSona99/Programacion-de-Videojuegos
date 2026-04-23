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
│
├── Assets/                     # Recursos del juego
│ ├── Animations/               # Controladores y clips de animación
│ │ ├── Arenas/                 # Animaciones de la arena
│ │ ├── Clips/                  # Clips de animación genéricos
│ │ ├── Controllers/            # Animation Controllers de Unity
│ │ ├── Enemies/                # Animaciones de los jefes
│ │ ├── Items/                  # Animaciones de los ítems (drops de salud)
│ │ ├── Menu/                   # Animaciones de la interfaz del menú
│ │ └── Player/                 # Animaciones del jugador
│ │
│ ├── Audio/                   # Sistema de audio del juego
│ │ ├── MainMixer.mixer         # Mezclador de audio principal
│ │ ├── Music/                  # Pistas de música de los niveles
│ │ └── SFX/                    # Efectos de sonido
│ │
│ ├── Prefabs/                  # Objetos prefabricados reutilizables
│ ├── Resources/                # Recursos cargados en tiempo de ejecución
│ ├── Scenes/                   # Escenas del juego (Menú, Arena, etc.)
│ ├── Settings/                 # Recursos de configuración
│ ├── Shaders/                  # Shaders personalizados
│ ├── Sprites/                  # Assets de sprites (personajes, enemigos, interfaz)
│ └── TextMesh Pro/             # Recursos de TextMesh Pro
│ │
│ └── Scripts/ NÚCLEO DE LA APLICACIÓN
│ │
│ ├── Core/                    # Lógica fundamental del juego
│ │ ├── BootManager.cs          # Inicialización del juego
│ │ ├── GameManager.cs          # Gestor principal del juego
│ │ ├── DataManager.cs          # Gestión de datos persistentes (guardados, estadísticas)
│ │ └── MatchTracker.cs         # Seguimiento de estadísticas de la partida
│ │
│ ├── Player/                  # Lógica del jugador
│ │ ├── PlayerMovements.cs      # Control de movimiento y entrada
│ │ ├── PlayerCombat.cs         # Sistema de combate (ataques, combos)
│ │ ├── PlayerHealth.cs         # Sistema de salud y vidas
│ │ └── HealthPickup.cs         # Gestión de recolección de drops de salud
│ │
│ ├── EnemyAI/                 # Inteligencia artificial de los jefes
│ │ ├── EnemyAI_1.cs            # Jefe 1 - Híbrido a distancia/cuerpo a cuerpo
│ │ ├── EnemyAI_2.cs            # Jefe 2 - Luchador basado en estados
│ │ ├── EnemyAI_3.cs            # Jefe 3 - Maestro acrobático
│ │ ├── EnemyHealth.cs          # Sistema de salud de los enemigos
│ │ ├── Enemy2ComboHitbox.cs    # Hitbox especial del combo del Jefe 2
│ │ └── Enemy2CrazyHitbox.cs    # Hitbox especial "Crazy Mode" del Jefe 2
│ │
│ ├── Arena/                   # Lógica de la arena de combate
| | ├── DynamicCamera.cs        # Gestión de la cámara de vídeo dinámica
| | ├── FloatingText.cs         # Comportamiento de los transitorios
│ │ └── ArenaManager.cs         # Gestión de la escena de la arena
│ │
│ ├── Audio/                   # Sistema de audio avanzado
│ │ ├── GameAudioManager.cs     # Audio durante la partida
│ │ ├── MenuAudioManager.cs     # Audio en el menú
│ │ ├── PlayerAudio.cs          # Audio por el player
│ │ ├── Enemy1Audio.cs          # Audio por el enemigo 1
│ │ ├── Enemy2Audio.cs          # Audio por el enemigo 2
│ │ └── Enemy3Audio.cs          # Audio por el enemigo 3
│ │
│ ├── UI/                      # Lógica de la interfaz de usuario
│ │ ├── MainMenuUI.cs           # Menú principal
│ │ ├── SettingsUI.cs           # Interfaz de ajustes
│ │ ├── LeaderboardUI.cs        # Visualización de la tabla de clasificación
│ │ ├── LeaderboardEntry.cs     # Estilo de tabla de clasificación de tarjetas
│ │ ├── MainMenuParallax.cs     # Estilo para el fondo del menú principal
│ │ ├── GlobalButtonStyling.cs  # Estilo de botón
│ │ ├── CRTController.cs        # Efecto visual CRT retro
│ │ └── TransitionManager.cs    # Efecto de transicción
│ │
│ └── Models/                   # Modelos de datos
│ | └── ScoreData.cs              # Estructura de datos de puntuación
│
├── ProjectSettings/            # Configuración del proyecto de Unity
├── Packages/                   # Dependencias del proyecto
├── Logs/                       # Registros de depuración y errores
│
└── README.md                   # Readme file para Github
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
