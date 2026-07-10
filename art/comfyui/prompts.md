# Pipeline de arte con ComfyUI → Unity

Estilo objetivo: **cartoon / low-poly colorido**, paleta saturada, formas
redondeadas, iluminación suave. Mantener consistencia usando siempre el mismo
checkpoint, el mismo bloque de estilo y seeds fijas por familia de assets.

## Configuración recomendada (workflow txt2img estándar)

| Parámetro | Valor |
|---|---|
| Checkpoint | SDXL base (o el que uses habitualmente, fijo para todo el proyecto) |
| Resolución | 1024x1024 (casillas/cartas), 2048x2048 (tablero central) |
| Steps | 28-35 |
| CFG | 6-7 |
| Sampler | dpmpp_2m + karras |

**Bloque de estilo** (añadir a TODOS los prompts positivos):

```
cartoon board game art, vibrant saturated colors, rounded shapes, soft outlines,
flat shading with subtle gradients, clean vector-like illustration, game asset,
white background, centered composition
```

**Negativo estándar:**

```
photo, realistic, text, watermark, signature, blurry, noisy, dark, gritty,
extra limbs, deformed
```

## Prompts por asset

### Casillas del tablero (una por etapa del pipeline)
```
[ESTILO] + icon illustration of a pencil sketch of a video game character,
drawing tablet, art studio props
```
Variar el sujeto: storyboard panels / 3d wireframe model / paint textures /
character rig skeleton / running animation frames / stage lights / final render screen.

### Cartas de Suerte y Comunidad
```
[ESTILO] + playing card frame design, ornate rounded border, empty center,
orange color scheme   (Suerte)
[ESTILO] + playing card frame design, ornate rounded border, empty center,
blue color scheme     (Comunidad)
```

### Fichas de jugador (concept para modelar en 3D)
```
[ESTILO] + cute chibi character of a 3d animator with headphones, T-pose,
front view, character concept sheet
```
Variantes: modelador con tableta / director con megáfono / becario con café.

### Textura del tablero central
```
[ESTILO] + top-down view of an animation studio floor plan, isometric desks,
monitors, colorful carpet, board game center design, 2048x2048
```

### Billetes / dinero
```
[ESTILO] + toy money bill design, playful banknote, number 100 in corners,
teal color scheme
```

## Flujo de trabajo

1. Generar en ComfyUI → guardar en `art/comfyui/output/` (NO se versiona en git).
2. Curar: elegir la mejor variante, limpiar en Photoshop si hace falta
   (quitar fondo, recortar, exportar PNG con transparencia).
3. Copiar el PNG final a `Assets/Art/Sprites/` (casillas, cartas, UI) o
   `Assets/Art/Textures/` (materiales 3D).
4. En Unity: Texture Type = Sprite (2D and UI) para UI/cartas; Default para texturas.
5. Commit con nombre descriptivo: `art: casillas grupo modelado (3 tiles)`.

## Convención de nombres

```
tile_<grupo>_<nombre>.png      → tile_modelado_lowpoly.png
card_suerte_frame.png / card_comunidad_frame.png
token_<personaje>_concept.png  → token_animador_concept.png
board_center.png
ui_<elemento>.png              → ui_boton_dados.png
```
