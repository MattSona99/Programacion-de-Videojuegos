import pygame
import sys
import random
from settings import *
from sprites import Player, Enemy, EnemyBullet
from levels import LEVEL_3_STRING

# Pygame and font initialization
pygame.init()
screen = pygame.display.set_mode((WIDTH, HEIGHT))
pygame.display.set_caption("Space Invaders")
clock = pygame.time.Clock()
ui_font = pygame.font.SysFont("courier", 24, bold=True)

# Sprite group instantiation for rendering and collision management
all_sprites = pygame.sprite.Group()
enemies = pygame.sprite.Group()
bullets = pygame.sprite.Group()
enemy_bullets = pygame.sprite.Group()

player = Player()
all_sprites.add(player)

# Global game state variables initialization
score = 0
lives = MAX_LIVES
current_level_index = 0

# Generate static background starfield coordinates
stars = [(random.randint(0, WIDTH), random.randint(0, HEIGHT)) for _ in range(100)]

def load_level(level_idx):
    """
    Clears existing entities and populates the enemy grid based on the specified level index.
    Calculates dynamic bounding boxes and positioning for responsive scaling.
    """
    # Purge existing entities from previous levels or states
    for enemy in enemies: enemy.kill()
    for bullet in bullets: bullet.kill()
    for e_bullet in enemy_bullets: e_bullet.kill()
        
    matrix = []
    
    if level_idx == 0:
        # Level 1: Standard 5x10 matrix generation
        matrix = [[random.choice([1, 2, 3, 4]) for _ in range(10)] for _ in range(5)]
        
    elif level_idx == 1:
        # Level 2: Triangular formation generation
        for i in range(5):
            row_len = 2 + (i * 2) 
            padding = (10 - row_len) // 2
            row = [0]*padding + [random.choice([1, 2, 3, 4]) for _ in range(row_len)] + [0]*padding
            matrix.append(row)
            
    else:
        # Level 3: ASCII-art based mapping from string template
        for row_str in LEVEL_3_STRING:
            row = []
            for char in row_str:
                if char.lower() == 'x':
                    row.append(random.choice([1, 2, 3, 4]))
                else:
                    row.append(0)
            matrix.append(row)

    # Dynamic entity dimension and coordinate calculations
    cols = len(matrix[0])
    padding_x = 5
    padding_y = 15
    margin = 200
    available_width = WIDTH - margin
    
    enemy_w = (available_width - (cols + 1) * padding_x) // cols 
    enemy_h = 30
    
    total_block_width = cols * enemy_w + (cols - 1) * padding_x
    start_x = (WIDTH - total_block_width) // 2
    start_y = 60
    
    # Instantiate enemy sprites based on matrix coordinates
    for row_idx, row in enumerate(matrix):
        for col_idx, val in enumerate(row):
            if val == 0: continue
            
            x = start_x + col_idx * (enemy_w + padding_x)
            y = start_y + row_idx * (enemy_h + padding_y)
            
            is_black = (val == 4)
            enemy = Enemy(x, y, hp=val, is_black=is_black, width=enemy_w, height=enemy_h) 
            all_sprites.add(enemy)
            enemies.add(enemy)
            
    return len(enemies)
    
initial_enemy_count = load_level(current_level_index)

# Main application loop
running = True
game_over = False

while running:
    # Frame rate regulation
    clock.tick(FPS)
    
    # Event polling and processing
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False
        elif event.type == pygame.KEYDOWN:
            # Trigger player projectile instantiation, capped at 5 active bullets
            if event.key == pygame.K_SPACE and not game_over:
                if len(bullets) < 5:
                    player.shoot(all_sprites, bullets)
            
    if not game_over:
        current_enemy_count = len(enemies)
        
        # --- Level Completion Logic ---
        if current_enemy_count == 0:
            # Increment level index (modulo 3 for cyclic progression) and restore a life
            current_level_index = (current_level_index + 1) % 3 
            lives = min(MAX_LIVES, lives + 1)
            
            initial_enemy_count = load_level(current_level_index)
            current_enemy_count = initial_enemy_count

        # --- Enemy Translation Logic ---
        # Speed scales inversely with the remaining enemy count
        speed_multiplier = 1 + ((initial_enemy_count - current_enemy_count) * 0.005)
        current_enemy_speed = ENEMY_BASE_SPEED * speed_multiplier

        # Detect lateral boundary collision to trigger vertical shift
        move_down = False
        for enemy in enemies:
            if (enemy.rect.right >= WIDTH and enemy.direction == 1) or \
               (enemy.rect.left <= 0 and enemy.direction == -1):
                move_down = True
                break
        
        # Apply translations
        for enemy in enemies:
            if move_down:
                enemy.direction *= -1
                enemy.rect.y += 15
            enemy.update(current_enemy_speed)

        # --- Enemy Projectile Generation ---
        for enemy in enemies:
            if enemy.is_black and random.random() < (ENEMY_SHOOT_CHANCE / 2):
                e_bullet = EnemyBullet(enemy.rect.centerx, enemy.rect.bottom)
                all_sprites.add(e_bullet)
                enemy_bullets.add(e_bullet)

        # Process sprite physics/state updates
        player.update()
        bullets.update()
        enemy_bullets.update()
    
        # --- Player Projectile vs Enemy Collision Resolution ---
        hits = pygame.sprite.groupcollide(bullets, enemies, False, False)
        
        for bullet, hit_enemies in hits.items():
            bullet_destroyed = False
            for enemy in hit_enemies:
                if enemy.is_black:
                    # Implement piercing mechanic: target is invulnerable if standard enemies remain
                    has_normal_enemies = any(not e.is_black for e in enemies)
                    if has_normal_enemies:
                        continue  # Projectile bypasses collision resolution
                    else:
                        points = enemy.hit()
                        if points: score += points
                        bullet_destroyed = True
                else:
                    points = enemy.hit()
                    if points: score += points
                    bullet_destroyed = True
                    
                if bullet_destroyed:
                    bullet.kill()
                    break  # Terminate inner iteration upon projectile consumption
            
        # --- Enemy Projectile vs Player Collision Resolution ---
        if pygame.sprite.spritecollide(player, enemy_bullets, True):
            lives -= 1
            if lives <= 0:
                game_over = True
                
        # --- Invasion Condition Validation ---
        for enemy in enemies:
            if enemy.rect.bottom >= player.rect.top:
                lives = 0
                game_over = True
                break
    
    # --- Rendering Pipeline ---
    screen.fill(BG_COLOR)
    
    # Render static background elements
    for star in stars:
        pygame.draw.circle(screen, (200, 200, 200), star, 1)
    
    if not game_over:
        # Active gameplay state rendering
        all_sprites.draw(screen)
        
        score_text = ui_font.render(f"SCORE: {score}", True, WHITE)
        level_text = ui_font.render(f"LEVEL: {current_level_index + 1}", True, WHITE)
        lives_text = ui_font.render(f"LIVES: {lives}", True, GREEN)
        
        screen.blit(score_text, (20, 20))
        screen.blit(level_text, (WIDTH // 2 - 50, 20))
        screen.blit(lives_text, (WIDTH - 120, 20))
        
    else:
        # Terminal state rendering (Game Over sequence)
        go_text = ui_font.render("GAME OVER", True, RED)
        final_score_text = ui_font.render(f"FINAL SCORE: {score}", True, WHITE)
        
        # Calculate geometric centers for text alignment
        go_rect = go_text.get_rect(center=(WIDTH // 2, HEIGHT // 2 - 20))
        score_rect = final_score_text.get_rect(center=(WIDTH // 2, HEIGHT // 2 + 20))
        
        screen.blit(go_text, go_rect)
        screen.blit(final_score_text, score_rect)

    # Swap front and back buffers
    pygame.display.flip()
    
# Process termination and resource cleanup
pygame.quit()
sys.exit()