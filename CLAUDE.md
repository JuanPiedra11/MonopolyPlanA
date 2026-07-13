# MonopolyPlanA — Memoria del proyecto

Monopoly de fantasía medieval en **Unity 6.5 (6000.5.3f1)**, IMGUI (OnGUI) para toda la UI, escenas generadas por código. Idioma del juego: **INGLÉS** (todo texto visible al jugador). Repo GitHub "MonopolyPlanA" (privado — solo commit inicial, PENDIENTE subir todo el avance).

## Arquitectura

- **Escenas**: `Menu.unity` (solo MenuBootstrap) y `Main.unity` (Main Camera, Directional Light, Bootstrap). Todo se construye en runtime vía `Resources.Load`.
- `Assets/Scripts/Core/GameManager.cs` (~2900 líneas): corazón del juego. Estado (`GameState`: Setup, WaitingRoll, Moving, Decision, Card, StartRoll, GameOver), turnos, bots, UI ingame completa, modales, victoria.
- `Assets/Scripts/Menu/MenuManager.cs`: Title → Game Mode (Single/Multiplayer) → Solo config / Lobby multijugador.
- `Assets/Scripts/Player/CharacterToken.cs`: fichas billboard 2D animadas por frames.
- `Assets/Scripts/Core/CameraOrbitController.cs`: cámaras Free / Top / Follow (V alterna, F sigue turno, default Follow Turn).
- `Assets/Scripts/Core/UIFonts.cs`: fuentes (Cinzel títulos/botones, Alegreya cuerpo, MedievalSharp acentos) + estilos de botón.
- `Assets/Scripts/Core/GameConfig.cs`: config estática entre escenas. `EndMode {Classic, Time, Rounds}`, StartGold, personajes {King, Mage, Dwarf, Archer} = ids {rey, mago, enano, arquera}.
- `Assets/Scripts/Editor/CardImportSettings.cs`: AssetPostprocessor (npotScale None; Resources/UI legible/isReadable; Backgrounds/UI max 2048).
- `Assets/Scripts/Editor/PlanASetup.cs`: menú "PlanA" (crear escena Menu, limpiar scripts faltantes, reimportar cartas).

## Sistemas de juego implementados

- **Tirada inicial (Turn Order)**: pantalla con arte `UI/turn_order_bg`; dado y botón anclados a fracciones del arte (dieX 0.582, buttonX 0.586, textX 0.582, Y 0.565 — constantes en DrawStartRollModal). Tabla se llena con resultados, gana el mayor.
- **Turnos**: delay 2 s entre turnos (`TurnChangeDelay`); dobles repiten; 3 dobles seguidos → cárcel.
- **GO**: $200 al PASAR (movimiento casilla a casilla, cartas incluidas). No paga al retroceder.
- **Cárcel** (`RestPenaltyTurns = 3`, `BailCost = 50`): cada turno 3 opciones — Pay bail / Roll doubles (pares = sale y mueve, sin turno extra) / Wait. Bots: pagan fianza si oro ≥ 250, si no intentan pares.
- **Casas**: condición = grupo de color completo (monopolio), máx 4. Botón "Build house" en el turno abre menú de propiedades elegibles (`BuildableTiles`/`BuildHouseAt`). Bots construyen pre-tirada con colchón de $300. `HouseCost = max(50, Price/2)`.
- **Trade (1 por turno, `_tradeUsed`)**: fases Pick (avatares de rivales CON propiedades) → Select (galería de cartas del rival, precio debajo, marco dorado) → Offer (cartas bloqueadas + oro por input text y ±50) → Waiting 2 s → bot decide (valor recibido ≥ 1.15× lo entregado; sus monopolios ×2) o humano Accept/Reject → Result sin velo. Juego EN PAUSA (`Time.timeScale = 0`) hasta la decisión. Al aceptar: carta 3D de dos caras (anverso propiedad + reverso `Cards/back_card`) vuela 5 s del dueño al comprador con smootherstep, volteretas 3 ejes (1080/1800/720°), arco alto y pop de escala.
- **Bancarrota**: con oro negativo → `SettleDebts`: liquidación forzosa (vende casas a mitad, caras primero; hipoteca propiedades al banco al 50%, baratas primero). Si no alcanza → `BankruptRoutine`: fuera de rotación ya, animación de derrota 3 s, ficha desaparece, propiedades al banco, HUD en gris. Último en pie gana.
- **Fin por límite**: Rounds/Time → `EndByLimit` ordena por patrimonio (`NetWorth` = oro + precios + casas).
- **Corona del líder**: VFX/crown_0..7 animada (0.18 s/frame) sobre la placa del nombre del líder por patrimonio (empate = oculta), con halo dorado radial aditivo pulsante generado en runtime, bob suave.
- **Skybox ingame**: `Backgrounds/HDRI_INGAME` (equirect 360°) → material Skybox/Panoramic en `SetupSkybox()` (GameManager.Start).
- **Lluvia de monedas (GANAR oro)**: `PlayCoinRain(p)` al GANAR oro (pasar GO, cobrar renta/cartas vía `Pay` con receptor, carta Money positiva, vendedor de un trade). DOS modos elegibles con `CoinRainSettings` en el Bootstrap de Main (menú PlanA → Añadir CoinRainSettings): AnimatedSprite (billboard `VFX/coin_rain_0..15`, 0.075 s/frame; hoja en Art/Sprites/Coin_Rain_Sheet.png) o Particles (Particle System runtime con `VFX/Coin` limpia, burst/tamaño/radio/gravedad/vida ajustables en Inspector; default Particles). En modo Particles caen girando en X/Y/Z, rebotan en un plano de colisión a nivel del tablero y quedan tiradas hasta desvanecerse. Defaults: `Rain Duration = 3 s`, `Post Delay = 1 s` (el turno espera al VFX antes de continuar; ambos editables en vivo).
- **Abducción de monedas (PERDER oro)**: espejo exacto de la lluvia — al pagar renta, impuestos, cartas de pago, compras de propiedad, casas, fianza o el oro entregado en un trade. Las monedas NACEN en el suelo del tablero (capa plana alrededor de la ficha, mismo radio que la lluvia) y suben acelerando girando en los 3 ejes hasta desvanecerse por encima de la cabeza. Misma duración y Post Delay que la lluvia. GOTCHA Unity: los 3 ejes de `velocityOverLifetime` deben usar el MISMO modo (X/Z curvas planas en 0, Y curva de aceleración); y `AddComponent<ParticleSystem>()` nace reproduciéndose y no deja tocar `duration` en caliente → detener con `StopEmittingAndClear` al crear, configurar, y recién entonces `Play()`.
- **Popup de dinero (MoneyPopup)**: junto a AMBOS VFX, texto flotante sobre la placa del nombre — **+$X en verde** al ganar, **-$X en rojo** al perder. Cinzel Bold, centrado horizontal, billboard (siempre de cara a cámara), `characterSize 0.065` (probados 0.045 y 0.085; 0.065 es el punto medio, sin contorno oscuro). Pop de entrada con rebote, asciende ~2.6 s y se desvanece. Si el jugador lleva la corona de líder, la corona se desplaza más arriba (0.72) mientras el texto está visible y vuelve a apoyarse sobre la placa al desaparecer.

## Audio (SFX + música)

- `AudioManager` (Core/AudioManager.cs): singleton runtime con DontDestroyOnLoad, `PlayMusic(nombre)` (loop, no reinicia si ya suena), `Play(nombre, vol)` (one-shot) y `PlayRandom(base)` (elige entre `base_0..N`, p. ej. dados). Volúmenes en el Inspector del GameObject "AudioManager" runtime (musicVolume 0.4, sfxVolume 0.9). Clips en `Resources/Audio/`.
- Clips REALES del usuario (fuente en `Assets/Art/Sounds/*.mp3`, copiados/renombrados a Resources/Audio): music_menu (Mainmenu, 115 s), music_medieval (ingame music, 185 s), sfx_go (cashregister, al pasar GO), sfx_coins_gain (earning_coins), sfx_coins_lose (Losing_coins), sfx_trade_offer (sonido_de_negociacion), sfx_trade_reject (negociacion_rechazada), sfx_build_house (comprar_casa), y `sfx_dice_0..8` (9 variantes troceadas de dados.mp3 por detección de silencios con ffmpeg+python; se elige una al azar por tirada). Siguen sintetizados: sfx_button, sfx_jail, sfx_token_step, sfx_victory, sfx_crowd.
- Hooks: music_menu en MenuManager.Awake, music_medieval en GameManager.Start; PlayRandom("sfx_dice") en DiceRollRoutine/JailRollRoutine/StartRollCurrent; sfx_go + lluvia al pasar GO; paso de ficha por casilla (0.55); monedas en PlayCoinRain/PlayCoinDrain; cárcel SendToJail; casa BuildHouseAt/BuildHouse; oferta al enviar trade; rechazo en EnterTradeResult; victoria+multitud una vez en DrawVictoryScreen (`_victorySfxPlayed`); sfx_button en botones principales.

## UI ingame

- **HUD**: por defecto el jugador del turno, esquina superior izquierda (placa `UI/hud_player` 340px, avatar, oro/props/casas, Status/Situation). Zonas azules recoloreadas al color del jugador vía `CharacterToken.TintedPlate` (HSV, cachea por (tex,color)). En bancarrota → gris. Panel de propiedades debajo (miniaturas de cartas, clic = zoom).
- **Selector de jugador (avatares-botón bajo el HUD)**: fila de botones usando `_selectedPlayer`. Primero un **botón de dado** = modo "seguir turno" (el HUD y el panel de cartas cambian solos con el jugador de turno; marco dorado cuando está activo). Luego **un avatar por jugador/bot activo** con su cinta de color; clic = FIJA el HUD + panel de cartas de ese jugador sin que cambie al pasar el turno (marco dorado en el fijado; marco blanco tenue marca siempre a quien tiene el turno). Si el fijado quiebra, vuelve solo al modo turno. El panel de propiedades sigue esta selección.
- **Acciones**: fila CENTRADA abajo (Trade · Roll dice · Build house); Trade solo si hay rival con propiedades. Ocultas con cualquier modal abierto (`modalOpen` — CRÍTICO para que el popup no active botones de atrás).
- **Iconos sobre botones**: `DrawButtonIcon` — encogen al 90% hover / 82% pulsado acompañando al botón.
- **Botones**: ornate (`UIFonts.OrnateButton`, 15/15/14/14 border) para principales 220×62; simple _low (34px, border 7) para compactos. Texto Cinzel con color por estado (pergamino/tinta/brasa). Labels SIEMPRE estáticos (sin hover — neutralizado en ApplyBody y OutlinedLabel).
- **Cámara panel**: superior derecha, colapsable (icono cámara). Indicador de límite (reloj icon_time) arriba al centro.
- **Log**: caja inferior; botón [TEST] End game (solo editor).
- **Victoria**: pergamino results_panel (limpiado en runtime con `StripBackground`) con ranking (1st/2nd/3rd/4th, oro/props/casas con iconos, corona al ganador), resumen (turnos, tiempo, casas), botones Play again/Main menu debajo; arte rank_[char]_[1-4] del ganador con Winner_panel (nombre centrado en banda azul) y perdedores en fila con nombre debajo.

## Ficha 3D del rey (toggle 2D/3D)

- `Resources/Models/`: RMo_Rey.fbx (rig T-pose), Rey_Idle/Walkcycle/Victory/Defeat.fbx (clips), bake_diffuse.png. Importer (CardImportSettings.OnPreprocessAnimation) pone Loop Time a Idle/Walk.
- `TokenStyleSettings` en el Bootstrap de Main (menú PlanA → Añadir TokenStyleSettings): toggle `rey3D` + `reyBodyHeight` (1.95 = altura del cuerpo 2D). `Use3D(charId)`.
- CharacterToken modo 3D (`_is3D`): instancia el modelo (escala por bounds a reyBodyHeight; pies apoyados usando los huesos Toes/ToesEnd en T-pose, con `reyGroundOffset` de ajuste vivo), anima con `ReyAnimator.controller` (asset creado por menú PlanA → Crear AnimatorController del rey: Idle⇄Walk por bool "walking", Victory/Defeat por triggers AnyState y vuelven solos a Idle; animator.speed 1.2 en walk). In-place: applyRootMotion=false + el joint `GameSkeletonRoot` se fija en XZ SOLO durante walk (en idle/victory/defeat el root queda original para que no patinen los pies). Sin controller cae a Playables. El root NO es billboard: LateUpdate orienta placa/label/avatar, y el LABEL del nombre se empuja 0.05 hacia la cámara para verse por ambos lados de la placa. Movimiento por casillas: lerp 0.4 s por paso + `RepositionTile(smooth)` con GlideTo 0.28 s (sin teletransportes). Vista Top: oculta modelo y muestra avatar. Toggle también ingame: botón "King token: 2D/3D" bajo los avatares del HUD (reconstruye la ficha conservando casilla y cámara).
- Corona y MoneyPopup se orientan a cámara por su cuenta (funcionan con root sin billboard).
- `ModelPrefix` dict en CharacterToken: charId → prefijo de archivos ("rey" → "Rey"). Para añadir mago/arquera/enano 3D: subir FBXs con ese patrón + entrada en dict + campos en TokenStyleSettings.

## Fichas (CharacterToken)

- `CanvasWorldHeight`: rey 2.39, mago 2.21, arquera 2.39, enano 1.87. `BodyFraction`: 0.815/0.893/0.816/0.801 (fracción del lienzo que ocupa el cuerpo, medida en idle). Pies en -0.5 local.
- Placa de nombre `UI/hud_nameplate` recolorada al color del jugador (TintedPlate), apoyada sobre la coronilla (`HeadTopY + 0.08`). `PlateTopY` público (lo usa la corona).
- Animaciones `Resources/Anims/{char}_{idle|walk|walk_front|walk_back|victory|defeat}_{0..7}.png`, canvas alto 553, cuerpo normalizado a la fracción del idle de cada personaje, pies a 0.998. Walk elige variante según dirección vs cámara (lateral con espejo — `SideArtFacing`: mago y enano -1, rey y arquera +1).
- Hojas fuente del mago en Art/Sprites: `Mago_WalkCycle_Front.png` (frontal) y `Mago_WalkCycle_Side.png` (lateral, mira a la izquierda) — renombradas el 13/07/2026.
- **Vista Top**: la ficha muestra el AVATAR (`Avatars/{char}_avatar`) en vez de la animación; la placa sube sobre el avatar; `FitQuadToAnim` tiene guard `_avatarShown` para que caminar no estire la foto.

## Pipeline de arte (sandbox Python/PIL/numpy/scipy)

- Quitar fondo blanco/checker: BFS/label desde bordes con `lum>175 & chroma<24`; islas: conservar componentes ≥3% del mayor; defringe de halos claros en hover.
- Normalización de altura: medir bbox alpha por frame, escalar a `bodyfrac_idle * 553`, pies a 0.998, centrado horizontal. OJO victory/defeat: brazos arriba o poses caídas engañan el bbox (usar frames de pie).
- Verificación SIEMPRE con contact sheet sobre magenta antes de dar por bueno.
- 9-slice IMGUI mapea px 1:1: texturas de botón deben ir cerca del tamaño de pantalla (54px/34px).

## GOTCHAS críticos del entorno

- **Mount sandbox**: archivos SOBRESCRITOS sirven contenido rancio para siempre al leer desde sandbox (¡NUNCA leer-modificar-escribir un archivo grande desde bash si fue editado por Windows/Edit tool — así se truncó GameManager.cs una vez; se reconstruyó replay-ando el historial del transcript!). Escribir con rm + rename a nombre nuevo funciona; archivos NUEVOS sincronizan bien. Para leer un archivo sobrescrito por el usuario: copiarlo a nombre nuevo desde el Explorador de Windows (computer-use) o verificar md5.
- **Unity via computer-use**: `open_application "Unity"` a veces abre Unity Hub (cerrarlo con X ~963,272 o clic al proyecto para enfocar el editor). Flujo: foco clic (640,300) → ctrl+r → esperar 25-30 s → contadores consola en zoom [1000,588,1085,606]. Si estaba en Play, el hot-reload resetea campos no serializados (hay guards: EnsureSlots, _players.Count, _contenders).
- Import order: texturas importadas en el mismo refresh en que compila el postprocessor usan settings viejos → borrar .meta y segundo refresh.
- `FindFirstObjectByType` deprecado (CS0618) → usar `FindAnyObjectByType`.
- `Object.FindObjectsByType<T>(FindObjectsSortMode)` deprecado en 6.5 → usar overload con `FindObjectsInactive`.

## Assets clave (Resources/)

- `Cards/` cartas de propiedad `card_[grupo]_[nombre]`, `back_card` (dorso, 1122×1402), `Events/` cartas Chance/Community en inglés (20 textos en EventCards.cs).
- `UI/` hud_player (1200×317), hud_nameplate (1024×310), turn_order_bg (1672×941), Winner_panel, results_panel, rank_{char}_{1-4} (16 poses de ranking), Buttons/ (ornate + simple_low con estados), Icons/ (22 iconos: dice, coins, castle, house, crown, trade, time, camera, jail...).
- `Anims/` 8 frames × 6 anims × 4 personajes. `Avatars/{char}_avatar` + retratos. `Backgrounds/` 4 fondos de menú + HDRI_INGAME. `VFX/` crown_0..7. `Dice/` die_1..6. `Fonts/` Cinzel, Alegreya, MedievalSharp.

## Build y publicación

- **Build Windows**: menú PlanA → Build Windows (16:9) → `Builds/MonopolyPlanA/MonopolyPlanA.exe` (StandaloneWindows64, escenas Menu+Main, ~518 MB). Aspecto FIJO 16:9: PlayerSettings 1600×900 ventana no redimensionable sin alt-enter + `Screen.SetResolution(1600,900,Windowed)` en MenuManager.Awake y GameManager.Start bajo `#if !UNITY_EDITOR`.
- Builds/ está en .gitignore (no se sube al repo).
- Repo: https://github.com/JuanPiedra11/MonopolyPlanA (rama main). OJO: git desde el sandbox (mount) es LENTÍSIMO con los assets — usar GitHub Desktop en Windows para commit/push cuando sea posible.
- Sonido botón menú: `sfx_button_menu` (button_menu.mp3) en los 7 botones del menú; ingame usa `sfx_button`.
- Label del nombre en 3D: anclado a la placa con offset hacia cámara vía rotación billboard (NUNCA leer y reescribir su posición acumulativamente — causó fuga hacia arriba).
- In-place 3D definitivo: durante walk se congela la traslación COMPLETA (XYZ) de `GameSkeletonRoot` y `GameSkeletonRoot_M`; idle/victory/defeat originales.

## Pendientes

- El usuario suele subir arte nuevo por chat/carpetas y pedir integración (renombrar, limpiar fondo, normalizar).

## Historial de sesiones

- **13/07/2026 (Cowork/Opus)**: se subió TODO el avance a GitHub via **GitHub Desktop** en Windows (antes el repo solo tenía el commit inicial; ~184 archivos: scripts, arte de Sprites/Sounds/Models, ProjectSettings, README). El git desde el sandbox falla (`.git/index.lock` → "Operation not permitted") y no tiene credenciales; SIEMPRE usar GitHub Desktop para commit/push.
- **Cambiar la visibilidad del repo (privado ↔ público) lo hace el USUARIO**, no Claude: cambiar permisos de compartición/acceso de un repo está fuera de lo que Claude puede ejecutar por política de seguridad. Ruta: github.com/JuanPiedra11/MonopolyPlanA → Settings → General → Danger Zone → "Change repository visibility". Claude puede guiar (teach mode) pero no ejecutar el toggle.
