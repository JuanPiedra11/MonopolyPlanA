# Monopoly PlanA

Prototipo de juego de mesa estilo Monopoly hecho en **Unity 6.5 (6000.5.3f1)**, tematizado con el pipeline de animación de videojuegos de **PlanA technology**. Arte generado con **ComfyUI** (estilo cartoon / low-poly colorido).

> Nota legal: "Monopoly" es marca registrada de Hasbro. Este proyecto es un prototipo interno/educativo; si se publica, hay que renombrarlo y diferenciar reglas y estética.

## Cómo abrir el proyecto

1. Abre **Unity Hub** → *Add* → *Add project from disk* → selecciona esta carpeta.
2. Ábrelo con Unity **6.5 (6000.5.3f1)**. La primera importación tarda unos minutos (genera `Library/`).

## Cómo jugar el prototipo

1. La primera vez: menú **PlanA → Crear escena Menu + configurar Build Settings** (crea `Menu.unity` y registra las escenas).
2. Abre `Assets/Scenes/Menu.unity` y pulsa **Play**.
3. Flujo: **Título → Lobby** (elige cuántos jugadores 2-4, cuáles son humanos o bots, nombre y color de cada uno) → **¡Jugar!**

Los bots juegan solos: lanzan los dados y deciden compras automáticamente. La escena `Main.unity` también puede ejecutarse directamente (muestra el selector rápido clásico).

Todo (tablero, cámara, luz, fichas, UI) se genera por código: no hay que configurar nada más en la escena.

**Cámara (3 modos, panel arriba a la derecha):**

- **Libre**: clic derecho = orbitar · rueda = zoom · WASD/clic medio = desplazar
- **Cenital**: vista superior; rueda = zoom · WASD/clic medio = desplazar
- **Seguir**: tercera persona sobre una ficha (la del turno con `F`, o fija un jugador desde el panel) · clic derecho = orbitar · rueda = zoom

Atajos: `V` alterna Libre/Cenital · `F` sigue al jugador del turno · `R` resetea la vista del modo actual.

## Reglas del prototipo

- 2-4 jugadores locales por turnos. Cada uno empieza con $1500.
- Lanza 2 dados; dobles = repite turno; 3 dobles seguidos = crunch (cárcel).
- Compra propiedades libres, cobra renta (x2 con grupo completo), estudios y servicios con renta especial.
- Suerte / Caja de Comunidad: eventos aleatorios de dinero.
- Bancarrota = eliminado. Gana el último en pie.

## Estructura

```
Assets/
  Scripts/
    Board/    → datos y generación procedural del tablero (40 casillas)
    Core/     → GameBootstrap (punto de entrada) y GameManager (turnos, reglas, UI)
    Player/   → estado de cada jugador
  Art/        → arte final importado desde ComfyUI (sprites, texturas, modelos)
  Scenes/     → escenas de Unity
  Prefabs/    → prefabs reutilizables
art/comfyui/  → prompts y guía del pipeline de arte con IA (fuera de Assets)
docs/         → GDD y documentación de diseño
```

## Pipeline de arte (ComfyUI)

Ver [`art/comfyui/prompts.md`](art/comfyui/prompts.md). Flujo: generar en ComfyUI → curar → guardar el PNG final en `Assets/Art/...` con el nombre según convención → Unity lo importa automáticamente.

## Roadmap

- [x] Prototipo jugable hot-seat (tablero procedural + reglas core)
- [ ] Reemplazar IMGUI por UGUI con arte de ComfyUI (cartas, HUD, dados)
- [ ] Texturas del tablero y fichas 3D estilizadas
- [ ] Casas/mejoras, hipotecas y subastas
- [ ] Animaciones de fichas y dados físicos
- [ ] Modo vs IA
