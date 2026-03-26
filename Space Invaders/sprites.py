import pygame
from settings import *

class Player(pygame.sprite.Sprite):
    def __init__(self):
        super().__init__()
        # SRCALPHA flag enables per-pixel alpha transparency for the Surface
        self.image = pygame.Surface((50, 30), pygame.SRCALPHA)
        
        # Render player avatar (turret configuration)
        # Central turret barrel
        pygame.draw.rect(self.image, GREEN, (20, 0, 10, 15))
        # Base chassis
        pygame.draw.rect(self.image, GREEN, (0, 15, 50, 15), border_radius=4)
        
        self.rect = self.image.get_rect()
        self.rect.centerx = WIDTH // 2
        self.rect.bottom = HEIGHT - 20
        self.speedx = 0

    def update(self):
        self.speedx = 0
        keys = pygame.key.get_pressed()
        if keys[pygame.K_LEFT]:
            self.speedx = -PLAYER_SPEED
        if keys[pygame.K_RIGHT]:
            self.speedx = PLAYER_SPEED
        
        self.rect.x += self.speedx
        
        # Screen boundary collision enforcement
        if self.rect.left < 0: self.rect.left = 0
        if self.rect.right > WIDTH: self.rect.right = WIDTH

    def shoot(self, all_sprites, bullets):
        # Instantiate and track player projectile
        bullet = Bullet(self.rect.centerx, self.rect.top)
        all_sprites.add(bullet)
        bullets.add(bullet)

class Bullet(pygame.sprite.Sprite):
    def __init__(self, x, y):
        super().__init__()
        self.image = pygame.Surface((4, 15), pygame.SRCALPHA)
        # Render luminous projectile with rounded bounding box
        pygame.draw.rect(self.image, (150, 255, 150), (0, 0, 4, 15), border_radius=2)
        self.rect = self.image.get_rect()
        self.rect.centerx = x
        self.rect.bottom = y
        self.speedy = -7

    def update(self):
        self.rect.y += self.speedy
        # Deallocate projectile upon exiting the upper screen boundary
        if self.rect.bottom < 0:
            self.kill()

class EnemyBullet(pygame.sprite.Sprite):
    def __init__(self, x, y):
        super().__init__()
        self.image = pygame.Surface((4, 15), pygame.SRCALPHA)
        pygame.draw.rect(self.image, RED, (0, 0, 4, 15), border_radius=2)
        self.rect = self.image.get_rect()
        self.rect.centerx = x
        self.rect.top = y
        self.speedy = ENEMY_BULLET_SPEED

    def update(self):
        self.rect.y += self.speedy
        # Deallocate projectile upon exiting the lower screen boundary
        if self.rect.top > HEIGHT:
            self.kill()

class Enemy(pygame.sprite.Sprite):
    def __init__(self, x, y, hp, is_black=False, width=40, height=30):
        super().__init__()
        self.width = width
        self.height = height
        self.image = pygame.Surface((width, height), pygame.SRCALPHA)
        self.hp = hp
        self.is_black = is_black
        self.update_color()
        
        self.rect = self.image.get_rect()
        self.rect.x = x
        self.rect.y = y
        self.direction = 1

    def update_color(self):
        # Clear previous surface state via transparent fill
        self.image.fill((0, 0, 0, 0))
        
        if self.is_black:
            # Dark grey fill applied to maintain visibility against the default black background
            color = (80, 80, 80)
        elif self.hp == 3:
            color = GREEN
        elif self.hp == 2:
            color = YELLOW
        elif self.hp == 1:
            color = RED

        # Render primary enemy chassis
        pygame.draw.rect(self.image, color, (0, 0, self.width, self.height), border_radius=5)
        
        # Render ocular cutouts using background color mapping
        eye_w = self.width * 0.2
        eye_h = self.height * 0.2
        pygame.draw.rect(self.image, BG_COLOR, (self.width * 0.2, self.height * 0.3, eye_w, eye_h))
        pygame.draw.rect(self.image, BG_COLOR, (self.width * 0.6, self.height * 0.3, eye_w, eye_h))
        
        # Render oral cutout using background color mapping
        pygame.draw.rect(self.image, BG_COLOR, (self.width * 0.35, self.height * 0.65, self.width * 0.3, self.height * 0.15))

    def update(self, current_speed):
        # Apply horizontal translation
        self.rect.x += self.direction * current_speed
        
    def hit(self):
        if not self.is_black:
            self.hp -= 1
            if self.hp > 0:
                # Trigger surface redraw to reflect updated HP state
                self.update_color()
                return 10  
            else:
                self.kill()
                return 50  
        else:
            self.kill()
            return 100