# Space Invaders - 2D Arcade Implementation

## Overview
This project is a 2D arcade shooter built using Python and the Pygame library. It implements the core gameplay loop and mechanics of the classic Space Invaders, focusing on object-oriented programming, dynamic entity rendering, state management, and collision detection.

## Project Architecture
The game relies on a component-based structure using Pygame's `Sprite` class to manage entities efficiently. 

### Core Classes
* **`Player`**: Manages the user-controlled turret. Handles horizontal translation via keyboard input, screen boundary enforcement, and projectile instantiation.
* **`Enemy`**: Represents the hostile entities. Features dynamic health points (HP), color-coded states based on remaining HP, and automated lateral/vertical translation logic.
* **`Bullet`**: Manages the player's projectiles, applying vertical upward translation and deallocation upon exiting the screen bounds.
* **`EnemyBullet`**: Manages hostile projectiles, applying vertical downward translation and deallocation upon screen exit.

## Game Mechanics & Logic
* **Dynamic Level Generation**: The `load_level()` function dynamically generates enemy formations using distinct matrices:
    * Level 1: Standard 5x10 rectangular matrix.
    * Level 2: Triangular/pyramidal formation.
    * Level 3: Custom ASCII-art based mapping from a string template.
* **Scaling Difficulty**: Enemy translation speed is dynamically calculated using a multiplier that scales inversely with the remaining enemy count, increasing the challenge as the level progresses.
* **Collision Resolution**: Utilizes `pygame.sprite.groupcollide` and `spritecollide` to resolve physics interactions between projectiles and entities. Includes a specific piercing mechanic where heavily armored (black) enemies are invulnerable until standard enemies are cleared.
* **State Management**: Tracks current score, remaining lives, and level progression, transitioning automatically between active gameplay and terminal states (Game Over / Victory).

## Assets & Resources
All graphical assets in this implementation are generated programmatically via `pygame.Surface` and `pygame.draw` primitives to minimize external dependencies.

*Note: If external assets (audio, textures) are added in future iterations, they must be royalty-free and credited below.*
* **Graphics**: Procedurally generated via Pygame primitives.
* **Fonts**: Built-in system font (`courier`).

## Dependencies & Execution
* **Language**: Python 3.x
* **Libraries**: Pygame
* **Execution**: Run `python main.py` (ensure `settings.py`, `sprites.py`, and `levels.py` are in the same directory).