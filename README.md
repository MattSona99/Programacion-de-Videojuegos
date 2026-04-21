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
* 🔗 **[Ir a la documentación y código de la Práctica 1](./Space%20Invaders)**

---

### [🎮 Ubstract - Juego de Acción 2D](./Ubstract)
* **Tipo de Juego:** Action / Boss-Fight Arena (2D Side-View)
* **Objetivo:** Desarrollar un juego de acción 2D con mecánicas de combate avanzadas, incluyendo un sistema de "Perfect Parry" (bloqueo al frame exacto), IA de bosses sofisticada y gestión completa de ciclos de juego, interfaz de usuario y persistencia de datos.
* **Problema a resolver:** Implementación de un juego roguelike donde el jugador debe enfrentarse a tres bosses únicos con patrones de ataque específicos. El desafío principal es balancear la dificultad, implementar un sistema de defensa basado en timing preciso, y crear IA enemiga con comportamientos complejos y diferenciados.
* **Mecánicas y Sistemas Implementados:**
  * **Sistema de Combate Avanzado:** *Combos de ataque, bloques simples y Perfect Parry con reflejo de daño.*
  * **IA de Bosses Sofisticada:** *Tres jefes con patrones únicos - Híbrido Range/Melee, State-Driven con modalidad especial, y Maestro Acrobatico.*
  * **Sistema de Puntuación y Leaderboard:** *Tracking de estadísticas detalladas y ranking persistente de jugadores.*
  * **Gestión de Física 2D:** *Movimiento fluido, detección de terreno, restricciones de movimiento durante combate.*
  * **Arquitectura Modular:** *Separación clara entre Controllers, Audio, UI, EnemyAI con paterns como Manager Pattern y State Pattern.*
  * **Persistencia de Datos:** *Salvataje de puntajes, preferencias de jugador y estadísticas de partidas.*
  * **Sistema de Audio Avanzado:** *Mixer centralizado, managers separados para menu y gameplay, feedback sonoro dinámico.*
* 🔗 **[Ir a la documentación y código de Ubstract](./Ubstract)**

---

## 🛠️ Tecnologías Utilizadas

* **Lenguajes:** Python 3.x, C#
* **Librerías/Motores:** Pygame, Unity (2022.3+)
* **Plataformas Target:** Windows/PC
* **Entorno:** Visual Studio Code, Unity Hub

---
*Desarrollado para el curso académico 2025/2026.*
