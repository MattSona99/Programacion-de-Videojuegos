# Programación de Videojuegos 🎮👾

Repositorio global de proyectos y prácticas desarrolladas para la asignatura **Programación de Videojuegos**. 

Este repositorio servirá como portafolio y registro de la evolución en el desarrollo y programación de videojuegos, abarcando desde la gestión del ciclo principal (game loop) y renderizado 2D básico, hasta la implementación de físicas, detección de colisiones y comportamientos de Inteligencia Artificial.

---

## 📋 Índice de Prácticas

A continuación se listan las prácticas desarrolladas durante el curso. Puedes acceder a la carpeta de cada práctica para ver su código fuente, documentación específica e instrucciones de ejecución.

### [✅ Práctica 1: Desarrollo Arcade 2D - Space Invaders](./Practica%201)
* **Objetivo:** Comprender la arquitectura base de un videojuego 2D, implementando el patrón del bucle de juego (Game Loop), la gestión de eventos de entrada y el renderizado eficiente de entidades a través de Sprites.
* **Problema a resolver:** Implementación de un clon funcional del clásico arcade "Space Invaders". El sistema debe gestionar oleadas de enemigos, calcular dinámicamente las posiciones en pantalla según la resolución, y resolver interacciones físicas (colisiones) entre múltiples entidades en tiempo real.
* **Mecánicas y Sistemas Implementados:**
  * Arquitectura Orientada a Objetos: *Gestión mediante agrupaciones de Sprites (`pygame.sprite.Group`).*
  * Generación Dinámica de Niveles: *Carga procedural de formaciones enemigas mediante matrices numéricas y mapeo de cadenas de texto (ASCII-art).*
  * Físicas y Colisiones: *Resolución de intersecciones de Bounding Boxes y mecánicas de invulnerabilidad condicional (enemigos blindados).*
  * IA y Dificultad Adaptativa: *Traslación automática de enjambres, cadencia de disparo pseudoaleatoria y escalado de velocidad inversamente proporcional a la cantidad de entidades restantes.*
* 🔗 **[Ir a la documentación y código de la Práctica 1](./Space%Invaders)**

---

## 🛠️ Tecnologías Utilizadas

* **Lenguaje:** Python 3.x
* **Librerías/Motores:** Pygame
* **Entorno:** Visual Studio Code

---
*Desarrollado para el curso académico 2025/2026.*
