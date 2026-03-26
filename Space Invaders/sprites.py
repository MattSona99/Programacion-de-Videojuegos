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