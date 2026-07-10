# Monopoly PlanA

Prototipo de juego de mesa estilo Monopoly hecho en **Unity 6.5 (6000.5.3f1)**, tematizado con el pipeline de animación de videojuegos de **PlanA technology**. Arte generado con **ComfyUI** (estilo cartoon / low-poly colorido).

> Nota legal: "Monopoly" es marca registrada de Hasbro. Este proyecto es un prototipo interno/educativo; si se publica, hay que renombrarlo y diferenciar reglas y estética.

## Cómo abrir el proyecto

1. Abre **Unity Hub** → *Add* → *Add project from disk* → selecciona esta carpeta.
2. Ábrelo con Unity **6.5 (6000.5.3f1)**. La primera importación tarda unos minutos (genera `Library/`).

## Cómo jugar el prototipo

1. Crea una escena nueva (File → New Scene) y guárdala en `Assets/Scenes/Main.unity`.
2. Crea un GameObject vacío y añádele el componente **GameBootstrap**.
3. Pulsa **Play**. Elige 2-4 jugadores (hot-seat) y a jugar.

Todo (tablero, cámara, luz, fichas, UI) se genera por código: no hay que configurar nada más en la escena.

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
