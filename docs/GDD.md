# GDD — Monopoly PlanA (prototipo)

## Concepto

Juego de mesa digital estilo Monopoly donde las propiedades son las etapas del pipeline de animación de un videojuego: del boceto al render final. Tono ligero y humor de industria (el "crunch" es la cárcel). Marca: PlanA technology.

## Pilares

1. **Familiar al instante**: reglas de Monopoly que todos conocen.
2. **Temática de animación**: cada grupo de color es una etapa del pipeline (Bocetos → Storyboard → Modelado → Texturizado → Rigging → Animación → Iluminación → Render Final).
3. **Arte IA consistente**: todo el arte 2D se genera con ComfyUI en estilo cartoon colorido, con prompts base compartidos para mantener coherencia.

## Alcance del prototipo (v0.1)

- Hot-seat 2-4 jugadores en un PC.
- Tablero de 40 casillas generado por código con placeholders.
- Comprar, rentar, impuestos, cartas aleatorias, crunch, bancarrota, victoria.
- UI provisional IMGUI.

**Fuera de alcance v0.1**: casas/hoteles, hipotecas, subastas, trades, IA, online.

## Casillas especiales

| Clásico | PlanA |
|---|---|
| Salida | SALIDA (cobras $200 de presupuesto) |
| Cárcel | CRUNCH (pierdes un turno) |
| Ferrocarriles | Estudios (Norte/Sur/Este/Oeste) |
| Servicios | Granja de Render / Captura de Movimiento |
| Suerte / Comunidad | Eventos de producción (cliente, licencias, demo reel...) |

## Dirección de arte

- Estilo: cartoon / low-poly colorido, bordes suaves, paleta saturada pero armónica.
- 2D (ComfyUI): cartas de Suerte/Comunidad, tarjetas de propiedad, iconos de casillas, HUD, logo.
- 3D (Unity/Blender): fichas temáticas (lápiz, tableta, cámara, muñeco de rigging), tablero con relieve.
- Las texturas del tablero pueden generarse en ComfyUI y aplicarse como material.

## Métrica de éxito del prototipo

Una partida completa de 3 jugadores termina sin errores y en menos de 20 minutos, y alguien se ríe con una carta de evento.
