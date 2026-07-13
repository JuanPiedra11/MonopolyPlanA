using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    public enum GameState { Setup, StartRoll, WaitingRoll, Moving, Decision, Card, GameOver }

    /// <summary>
    /// Núcleo del prototipo: turnos hot-seat 2-4 jugadores, dados, movimiento,
    /// compra de propiedades, rentas, impuestos, cartas y bancarrota.
    /// La UI es IMGUI (OnGUI) para no depender de nada en la escena;
    /// en la siguiente iteración se reemplaza por UGUI con arte de ComfyUI.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public BoardGenerator Board;

        readonly List<PlayerState> _players = new List<PlayerState>();
        GameState _state = GameState.Setup;
        int _currentIndex;
        int _die1, _die2;
        int _doublesInARow;
        string _log = "Welcome to Monopoly PlanA. Choose the number of players.";
        TileData _pendingTile;   // casilla sobre la que se decide
        bool _pendingIsHouse;    // true = decisión de construir casa, false = compra
        const float DecisionTime = 60f;
        float _decisionLeft;     // segundos restantes para decidir (humanos)

        void EnterDecision(TileData tile, bool isHouse)
        {
            _pendingTile = tile;
            _pendingIsHouse = isHouse;
            _decisionLeft = DecisionTime;
            _state = GameState.Decision;
        }

        // --- Mazos de Suerte y Caja de Comunidad ---
        Queue<EventCard> _chanceDeck;
        Queue<EventCard> _communityDeck;
        EventCard _drawnCard;
        bool _drawnIsChance;
        bool _pendingDoubles;

        void DrawEventCard(TileType type, bool doubles)
        {
            _drawnIsChance = type == TileType.Chance;
            var deck = _drawnIsChance ? _chanceDeck : _communityDeck;
            var card = deck.Dequeue();
            deck.Enqueue(card); // se recicla al fondo del mazo

            _pendingDoubles = doubles;
            _state = GameState.Card; // bloquea la UI; el modal aparece al llegar la carta
            _log = $"{Current.Name} draws a {(_drawnIsChance ? "Chance" : "Community Chest")} card.";
            StartCoroutine(CardDrawCinematic(card));
        }

        /// <summary>
        /// La carta vuela en 3D desde su mazo en el tablero hasta el frente de la
        /// cámara, volteándose para revelar la cara (dos quads: reverso y anverso).
        /// Al llegar, se abre el modal con la carta.
        /// </summary>
        IEnumerator CardDrawCinematic(EventCard card)
        {
            var cam = Camera.main;
            Vector3 start = _drawnIsChance ? Board.ChanceDeckWorldPos : Board.CommunityDeckWorldPos;

            var backTex = GetTex(_drawnIsChance
                ? "Cards/Events/event_chance_back"
                : "Cards/Events/event_community_back");
            if (backTex == null) backTex = GetTex("Cards/Events/event_chance_back");
            var faceTex = GetTex("Cards/Events/event_" + card.Id);
            if (faceTex == null) faceTex = backTex;

            if (cam != null && backTex != null)
            {
                var flying = new GameObject("FlyingCard");
                flying.transform.position = start;

                const float cw = 2.25f; // mismo tamaño que las cartas del mazo
                float ch = cw * ((float)backTex.height / backTex.width);

                // anverso (mira hacia -Z del padre) y reverso (hacia +Z)
                foreach (var (tex, yRot) in new[] { (faceTex, 0f), (backTex, 180f) })
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(flying.transform, false);
                    q.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
                    q.transform.localScale = new Vector3(cw, ch, 1f);
                    var m = new Material(Shader.Find("Unlit/Transparent"));
                    m.mainTexture = tex;
                    q.GetComponent<Renderer>().material = m;
                }

                Quaternion flat = Quaternion.Euler(-90f, 45f, 0f); // tumbada sobre el mazo (reverso arriba)
                const float dur = 1.15f;

                for (float t = 0f; t < dur; t += Time.deltaTime)
                {
                    float e = Mathf.SmoothStep(0f, 1f, t / dur);

                    Vector3 target = cam.transform.position + cam.transform.forward * 2.3f;
                    Vector3 mid = start + Vector3.up * 4.5f;
                    Vector3 a = Vector3.Lerp(start, mid, e);
                    Vector3 b = Vector3.Lerp(mid, target, e);
                    flying.transform.position = Vector3.Lerp(a, b, e); // arco bezier

                    Quaternion facing = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                    flying.transform.rotation =
                        Quaternion.Slerp(flat, facing, e) * Quaternion.Euler(0f, 360f * e, 0f); // voltereta

                    flying.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, e);
                    yield return null;
                }

                Destroy(flying);
            }

            _drawnCard = card; // ahora sí: modal con la carta
        }

        /// <summary>Aplica el efecto de la carta robada y continúa el turno.</summary>
        void ResolveDrawnCard()
        {
            var card = _drawnCard;
            _drawnCard = null;
            if (card == null) return;

            switch (card.Effect)
            {
                case CardEffectType.Money:
                    if (card.Amount >= 0)
                    {
                        Current.Money += card.Amount;
                        PlayCoinRain(Current, card.Amount);
                        PlayReaction(Current, true);
                    }
                    else
                    {
                        Pay(Current, null, -card.Amount);
                    }
                    _log = $"[{DeckName()}] {card.Text}";
                    EndOfMove(_pendingDoubles);
                    break;

                case CardEffectType.MoneyFromPlayers:
                {
                    foreach (var p in _players)
                    {
                        if (p == Current || p.Bankrupt) continue;
                        Pay(p, Current, card.Amount);
                        if (_state == GameState.GameOver) return;
                    }
                    PlayReaction(Current, true);
                    _log = $"[{DeckName()}] {card.Text}";
                    EndOfMove(_pendingDoubles);
                    break;
                }

                case CardEffectType.PayPerHouse:
                {
                    int houses = 0;
                    foreach (int idx in Current.OwnedTiles) houses += Board.Tiles[idx].Houses;
                    int total = houses * card.Amount;
                    if (total > 0) Pay(Current, null, total);
                    _log = $"[{DeckName()}] {card.Text} (total ${total})";
                    if (_state != GameState.GameOver) EndOfMove(_pendingDoubles);
                    break;
                }

                case CardEffectType.GoToJail:
                    _log = $"[{DeckName()}] {card.Text}";
                    SendToJail(Current);
                    PlayReaction(Current, false);
                    NextPlayer();
                    break;

                case CardEffectType.MoveTo:
                {
                    int steps = (card.Amount - Current.Position + BoardGenerator.TileCount) % BoardGenerator.TileCount;
                    if (steps == 0) steps = BoardGenerator.TileCount;
                    _log = $"[{DeckName()}] {card.Text}";
                    StartCoroutine(MoveRoutine(steps, _pendingDoubles));
                    break;
                }

                case CardEffectType.MoveBack:
                    _log = $"[{DeckName()}] {card.Text}";
                    StartCoroutine(MoveBackRoutine(card.Amount, _pendingDoubles));
                    break;

                case CardEffectType.MoveToNearestStudio:
                {
                    int steps = StepsToNearestStudio();
                    _log = $"[{DeckName()}] {card.Text}";
                    StartCoroutine(MoveRoutine(steps, _pendingDoubles));
                    break;
                }
            }
        }

        string DeckName() => _drawnIsChance ? "CHANCE" : "COMMUNITY";

        int StepsToNearestStudio()
        {
            int best = BoardGenerator.TileCount;
            foreach (int studio in new[] { 5, 15, 25, 35 })
            {
                int steps = (studio - Current.Position + BoardGenerator.TileCount) % BoardGenerator.TileCount;
                if (steps > 0 && steps < best) best = steps;
            }
            return best;
        }

        /// <summary>Retrocede casilla a casilla (sin cobrar SALIDA) y resuelve donde cae.</summary>
        IEnumerator MoveBackRoutine(int steps, bool doubles)
        {
            _state = GameState.Moving;

            int startTile = Current.Position;
            var character = Current.Token.GetComponent<CharacterToken>();
            if (character != null) character.SetWalking(true);

            for (int s = 0; s < steps; s++)
            {
                Current.Position = (Current.Position + BoardGenerator.TileCount - 1) % BoardGenerator.TileCount;

                Vector3 from = Current.Token.transform.position;
                Vector3 to = TileCenterPos(Current.Position);
                if (character != null) character.SetMoveDirection(to - from);

                // traslación suave hasta la casilla (sin teletransporte, ritmo de caminata)
                const float stepDur = 0.4f;
                float t0 = Time.time;
                while (Time.time - t0 < stepDur)
                {
                    float k = Mathf.Clamp01((Time.time - t0) / stepDur);
                    Current.Token.transform.position = Vector3.Lerp(from, to, k);
                    yield return null;
                }
                Current.Token.transform.position = to;
            }

            if (character != null) character.SetWalking(false);

            RepositionTile(startTile, true);
            RepositionTile(Current.Position, true);

            ResolveTile(doubles);
        }
        Vector2 _propScroll;
        int _selectedPlayer = -1; // -1 = seguir al jugador del turno

        readonly Dictionary<string, Texture2D> _cardCache = new Dictionary<string, Texture2D>();
        TileData _cardOverlay; // carta ampliada al hacer clic en una propiedad

        /// <summary>
        /// Reacción del personaje EN EL TABLERO: la ficha del jugador reproduce
        /// su animación de victoria (compra/casa/ganar) o derrota (pagos/bancarrota).
        /// </summary>
        void PlayReaction(PlayerState p, bool victory)
        {
            if (p == null || p.Token == null) return;
            var character = p.Token.GetComponent<CharacterToken>();
            if (character == null) return; // bots con cápsula no tienen animación

            if (victory) character.PlayVictory();
            else character.PlayDefeat();
        }

        /// <summary>Carga y cachea cualquier textura de Resources por su ruta.</summary>
        Texture2D GetTex(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) return null;
            if (!_cardCache.TryGetValue(resPath, out var tex))
            {
                tex = Resources.Load<Texture2D>(resPath);
                _cardCache[resPath] = tex;
            }
            return tex;
        }

        Texture2D GetCardTexture(string resName) =>
            string.IsNullOrEmpty(resName) ? null : GetTex("Cards/" + resName);

        Texture2D GetCard(TileData t) => t == null ? null : GetCardTexture(t.Card);

        const int Salary = 200;
        const int StartMoney = 1500;
        const int RestPenaltyTurns = 3; // turnos máximos en la cárcel
        const int BailCost = 50;        // fianza para salir de inmediato

        static readonly Color[] PlayerColors =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.95f),
            new Color(0.25f, 0.80f, 0.35f),
            new Color(0.95f, 0.80f, 0.25f)
        };

        PlayerState Current => _players[_currentIndex];

        bool _followCurrent = true; // en modo Seguir: true = sigue al jugador del turno
        bool _camPanelOpen = true;  // panel de cámara desplegado o contraído
        int _pinnedPlayer = -1;     // índice de jugador fijado (-1 = ninguno)

        CameraOrbitController Cam => CameraOrbitController.Instance;

        float _botTimer;
        const float BotDelay = 1.3f;

        void Update()
        {
            if (_state == GameState.Setup) return;

            UpdateLeaderCrown(); // corona flotante sobre el que va ganando
            UpdateTradePhases(); // espera y resolución de la negociación

            if (Cam != null && Input.GetKeyDown(KeyCode.F))
                FollowCurrentPlayer();

            // Tirada inicial: los bots tiran solos
            if (_state == GameState.StartRoll && !_startDieAnimating
                && _players.Count > 0 && _contenders != null
                && _contenderPtr < _contenders.Count && CurrentRoller.IsBot)
            {
                _botTimer += Time.deltaTime;
                if (_botTimer >= 1.1f)
                {
                    _botTimer = 0f;
                    StartRollCurrent();
                }
            }

            // Temporizador de decisión (solo humanos): al agotarse, pasa automáticamente
            if (_state == GameState.Decision && _pendingTile != null && !Current.IsBot)
            {
                _decisionLeft -= Time.deltaTime;
                if (_decisionLeft <= 0f)
                {
                    SkipPending();
                    _log = "⏱ Time's up: " + _log;
                }
            }

            UpdateBot();
        }

        /// <summary>Turnos automáticos: el bot lanza y decide compras con un pequeño retardo.</summary>
        void UpdateBot()
        {
            // durante la tirada inicial, el timer lo gestiona el bloque de StartRoll
            if (_state == GameState.StartRoll) return;

            if (_players.Count == 0 || !Current.IsBot) { _botTimer = 0f; return; }

            if (_state == GameState.WaitingRoll)
            {
                _botTimer += Time.deltaTime;
                if (_botTimer >= BotDelay)
                {
                    _botTimer = 0f;

                    // En la cárcel: paga la fianza si va holgado, si no prueba suerte con pares
                    if (Current.RestTurns > 0)
                    {
                        if (Current.Money >= 250) PayBail(); // tirará en el próximo tick, ya libre
                        else JailRoll();
                        return;
                    }

                    // Antes de tirar: construye una casa si el colchón se lo permite
                    var buildable = BuildableTiles();
                    if (buildable.Count > 0 && Current.Money - HouseCost(Board.Tiles[buildable[0]]) >= 300)
                    {
                        BuildHouseAt(buildable[0]);
                        _botTimer = -0.8f; // pausa para que se lea el log
                        return;
                    }

                    RollDice();
                }
            }
            else if (_state == GameState.Card && _drawnCard != null)
            {
                // el bot "lee" la carta un momento y continúa
                _botTimer += Time.deltaTime;
                if (_botTimer >= 2.2f)
                {
                    _botTimer = 0f;
                    ResolveDrawnCard();
                }
            }
            else if (_state == GameState.Decision && _pendingTile != null)
            {
                _botTimer += Time.deltaTime;
                if (_botTimer >= BotDelay)
                {
                    _botTimer = 0f;
                    if (_pendingIsHouse)
                    {
                        // Construye si le queda un colchón de $300
                        if (Current.Money - HouseCost(_pendingTile) >= 300) BuildHouse();
                        else SkipPending();
                    }
                    else
                    {
                        // Heurística simple: compra si le queda un colchón de $250
                        if (Current.Money - _pendingTile.Price >= 250) BuyPending();
                        else SkipPending();
                    }
                }
            }
            else
            {
                _botTimer = 0f;
            }
        }

        void FollowCurrentPlayer()
        {
            _followCurrent = true;
            _pinnedPlayer = -1;
            Cam.SetMode(CameraMode.Follow, Current.Token.transform);
        }

        void FollowPinnedPlayer(int index)
        {
            _followCurrent = false;
            _pinnedPlayer = index;
            Cam.SetMode(CameraMode.Follow, _players[index].Token.transform);
        }

        void UpdateFollowTarget()
        {
            if (Cam == null || Cam.Mode != CameraMode.Follow) return;
            if (_followCurrent)
                Cam.FollowTarget = Current.Token.transform;
        }

        // ---------- Setup ----------

        void Start()
        {
#if !UNITY_EDITOR
            // build jugable: ventana fija 16:9 para que la UI no se deforme
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
#endif
            AudioManager.PlayMusic("music_medieval"); // ambientación medieval en bucle
            SetupSkybox(); // panorama HDRI de fondo en vez del gris azulado

            // Si venimos del lobby (escena Menu), arrancar directo con esa config
            if (GameConfig.Configured)
                StartGameFromConfig();
            else
                StartGame(2); // respaldo: jugar la escena Main directamente en el editor
        }

        /// <summary>
        /// Cielo del modo ingame: panorama equirectangular 360°
        /// (Resources/Backgrounds/HDRI_INGAME) como skybox panorámico.
        /// </summary>
        void SetupSkybox()
        {
            var tex = Resources.Load<Texture2D>("Backgrounds/HDRI_INGAME");
            if (tex == null) return;
            var sh = Shader.Find("Skybox/Panoramic");
            if (sh == null) return;

            var mat = new Material(sh);
            mat.SetTexture("_MainTex", tex);
            mat.SetFloat("_Mapping", 1f);   // Latitude-Longitude
            mat.SetFloat("_ImageType", 0f); // 360 grados
            mat.SetFloat("_Exposure", 1.05f);
            RenderSettings.skybox = mat;

            var cam = Camera.main;
            if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            DynamicGI.UpdateEnvironment();
        }

        void StartGameFromConfig()
        {
            var list = new List<PlayerState>();
            foreach (var s in GameConfig.Players)
                list.Add(new PlayerState(s.Name, PlayerPalette.Colors[s.ColorIndex])
                {
                    IsBot = s.IsBot,
                    CharacterId = GameConfig.CharacterIds[s.CharacterIndex % GameConfig.CharacterIds.Length]
                });
            BeginMatch(list);
        }

        void StartGame(int playerCount)
        {
            var list = new List<PlayerState>();
            for (int i = 0; i < playerCount; i++)
                list.Add(new PlayerState($"Player {i + 1}", PlayerColors[i])
                {
                    CharacterId = GameConfig.CharacterIds[i % GameConfig.CharacterIds.Length]
                });
            BeginMatch(list);
        }

        void BeginMatch(List<PlayerState> players)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.Money = GameConfig.StartGold;
                p.Avatar = Resources.Load<Texture2D>($"Avatars/{p.CharacterId}_avatar");
                p.Token = CreateToken(p, i, players.Count);
                _players.Add(p);
            }

            _currentIndex = 0;
            _chanceDeck = EventDecks.CreateChance();
            _communityDeck = EventDecks.CreateCommunity();
            RepositionTile(0); // distribuir las fichas en SALIDA
            FollowCurrentPlayer(); // cámara en "seguir turno" por defecto
            BeginStartRoll();  // la tirada inicial decide quién empieza
        }

        GameObject CreateToken(PlayerState p, int index, int total)
        {
            // Todos los jugadores (humanos y bots) tienen personaje 2D animado,
            // con su nombre flotando encima para diferenciarlos
            var ct = CharacterToken.Create(p.CharacterId, p.Name, p.Color);
            GameObject token = ct.gameObject;
            token.name = $"Token_{p.Name}";
            token.transform.position = TileCenterPos(0);
            return token;
        }

        Vector3 TileCenterPos(int tileIndex) =>
            Board.GetTileWorldPos(tileIndex) + Vector3.up * 0.6f;

        /// <summary>
        /// Distribuye las fichas que comparten una casilla: 1 centrada,
        /// 2 lado a lado, 3-4 en cuadrícula, proporcional al tamaño de la casilla.
        /// </summary>
        void RepositionTile(int tileIndex, bool smooth = false)
        {
            var here = new List<PlayerState>();
            foreach (var p in _players)
                if (!p.Bankrupt && p.Position == tileIndex) here.Add(p);
            if (here.Count == 0) return;

            Vector3 size = Board.GetTileWorldSize(tileIndex);
            float dx = size.x * 0.22f;
            float dz = size.z * 0.22f;

            Vector2[] layout;
            switch (here.Count)
            {
                case 1: layout = new[] { Vector2.zero }; break;
                case 2: layout = new[] { new Vector2(-1, 0), new Vector2(1, 0) }; break;
                case 3: layout = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(0, 1) }; break;
                default: layout = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) }; break;
            }

            for (int i = 0; i < here.Count; i++)
            {
                var l = layout[Mathf.Min(i, layout.Length - 1)];
                Vector3 target = TileCenterPos(tileIndex) + new Vector3(l.x * dx, 0f, l.y * dz);
                var tr = here[i].Token.transform;

                // al llegar caminando, el acomodo en la casilla también es por traslación
                if (smooth && here[i].Token.activeInHierarchy
                    && (tr.position - target).sqrMagnitude > 0.0004f)
                    StartCoroutine(GlideTo(tr, target, 0.28f));
                else
                    tr.position = target;
            }
        }

        IEnumerator GlideTo(Transform tr, Vector3 to, float dur)
        {
            Vector3 from = tr.position;
            float t0 = Time.time;
            while (tr != null && Time.time - t0 < dur)
            {
                float k = Mathf.SmoothStep(0f, 1f, (Time.time - t0) / dur);
                tr.position = Vector3.Lerp(from, to, k);
                yield return null;
            }
            if (tr != null) tr.position = to;
        }

        // ---------- Turno ----------

        // --- Tirada inicial: un dado por jugador, el más alto empieza ---
        int[] _startRolls;
        List<int> _contenders;
        int _contenderPtr;
        bool _startDieAnimating;
        float _startDieT0;
        int _startDieResult;
        bool _startResolved; // orden decidido: se muestra la tabla final antes de empezar

        IEnumerator FinishStartRoll()
        {
            yield return new WaitForSeconds(2.5f);
            _startResolved = false;
            _doublesInARow = 0;
            _state = GameState.WaitingRoll;
            _gameStartTime = Time.time; // arranca el reloj de la partida
            _turnCount = 1;
            UpdateFollowTarget();
        }

        void BeginStartRoll()
        {
            _startRolls = new int[_players.Count];
            _contenders = new List<int>();
            for (int i = 0; i < _players.Count; i++)
            {
                _startRolls[i] = -1;
                _contenders.Add(i);
            }
            _contenderPtr = 0;
            _state = GameState.StartRoll;
            _log = "Opening roll: the highest number starts the game.";
        }

        PlayerState CurrentRoller => _players[_contenders[_contenderPtr]];

        void StartRollCurrent()
        {
            _startDieResult = Random.Range(1, 7);
            AudioManager.PlayRandom("sfx_dice");
            StartCoroutine(StartRollAnim());
        }

        IEnumerator StartRollAnim()
        {
            _startDieAnimating = true;
            _startDieT0 = Time.time;

            yield return new WaitForSeconds(1.35f);

            _startDieAnimating = false;
            int who = _contenders[_contenderPtr];
            _startRolls[who] = _startDieResult;
            _log = $"{_players[who].Name} rolls a {_startDieResult}.";

            _contenderPtr++;
            if (_contenderPtr >= _contenders.Count)
                ResolveStartRoll();
        }

        void ResolveStartRoll()
        {
            int best = -1;
            foreach (int i in _contenders)
                if (_startRolls[i] > best) best = _startRolls[i];

            var tied = new List<int>();
            foreach (int i in _contenders)
                if (_startRolls[i] == best) tied.Add(i);

            if (tied.Count == 1)
            {
                _currentIndex = tied[0];
                _startResolved = true; // mostrar el orden final un momento
                _log = $"{Current.Name} rolls the highest number and starts the game!";
                StartCoroutine(FinishStartRoll());
            }
            else
            {
                // empate: los empatados vuelven a tirar
                _contenders = tied;
                _contenderPtr = 0;
                string names = "";
                foreach (int i in tied)
                {
                    _startRolls[i] = -1;
                    names += (names == "" ? "" : ", ") + _players[i].Name;
                }
                _log = $"Tie at {best}! Tie-break: {names}.";
            }
        }

        /// <summary>
        /// Pantalla de la tirada inicial: arte a pantalla completa con la mesa
        /// de los 4 personajes; el botón/dado va al centro del tablero-brújula
        /// y la tabla TURN ORDER de la izquierda se llena según los resultados.
        /// </summary>
        void DrawStartRollModal()
        {
            Color prevColor = GUI.color;
            float W = Screen.width, H = Screen.height;

            // fondo dedicado (cover); todos los elementos se anclan al RECTÁNGULO
            // de la imagen para quedar alineados con el arte en cualquier aspecto
            var bg = GetTex("UI/turn_order_bg");
            Rect art = new Rect(0, 0, W, H);
            if (bg != null)
            {
                float ta = (float)bg.width / bg.height, sa = W / H;
                art = ta > sa
                    ? new Rect((W - H * ta) / 2f, 0f, H * ta, H)
                    : new Rect(0f, (H - W / ta) / 2f, W, W / ta);
                GUI.DrawTexture(art, bg, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.color = new Color(0f, 0f, 0f, 0.8f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }

            Rect AF(float fx, float fy, float fw, float fh) => new Rect(
                art.x + art.width * fx, art.y + art.height * fy,
                art.width * fw, art.height * fh);

            // ---- tabla TURN ORDER (ranuras del arte, fracciones de pantalla) ----
            // entradas: mientras se tira, ordenadas por resultado; al resolver,
            // el orden real de turnos (el que empieza y luego el sentido de mesa)
            var entries = new List<(string name, int roll, bool winner)>();
            if (_startResolved)
            {
                for (int k = 0; k < _players.Count; k++)
                {
                    int idx = (_currentIndex + k) % _players.Count;
                    entries.Add((_players[idx].Name, _startRolls[idx], k == 0));
                }
            }
            else
            {
                var rolled = new List<int>();
                for (int i = 0; i < _players.Count; i++)
                    if (_startRolls[i] >= 0) rolled.Add(i);
                rolled.Sort((a, b) => _startRolls[b].CompareTo(_startRolls[a]));
                foreach (int i in rolled)
                    entries.Add((_players[i].Name, _startRolls[i], false));
            }

            float[] slotY = { 0.290f, 0.410f, 0.531f, 0.651f };
            var slotName = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(art.height * 0.034f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, font = UIFonts.Title,
                wordWrap = false, clipping = TextClipping.Overflow
            };
            var slotRoll = new GUIStyle(slotName) { alignment = TextAnchor.MiddleRight };
            for (int s = 0; s < entries.Count && s < 4; s++)
            {
                var e = entries[s];
                Color c = e.winner ? new Color(1f, 0.85f, 0.3f) : new Color(0.92f, 0.88f, 0.78f);
                slotName.normal.textColor = c;
                slotRoll.normal.textColor = c;
                GUI.Label(AF(0.085f, slotY[s] - 0.026f, 0.13f, 0.052f), e.name, slotName);
                if (e.roll >= 0)
                    GUI.Label(AF(0.185f, slotY[s] - 0.026f, 0.055f, 0.052f), e.roll.ToString(), slotRoll);
            }

            // ---- centro del tablado: posiciones calibradas sobre el arte ----
            const float dieFx = 0.582f, dieFy = 0.565f, dieFs = 0.185f;
            const float btnFx = 0.586f, btnFy = 0.565f, btnW2 = 240f, btnH2 = 64f;
            const float txtFx = 0.582f, txtFy = 0.565f;

            float ccx = art.x + art.width * txtFx, ccy = art.y + art.height * txtFy;

            if (_startDieAnimating)
            {
                var faces = GetDiceFaces();
                if (faces[0] != null)
                {
                    float size = art.height * dieFs;
                    float t = Time.time - _startDieT0;
                    const float settle = 1.0f;
                    int target = _startDieResult - 1;
                    const int totalFaces = 16;
                    int off = ((target - totalFaces) % 6 + 6) % 6;

                    float p = Mathf.Clamp01(t / settle);
                    float back = 0.9f;
                    float q = p - 1f;
                    float ease = 1f + q * q * ((back + 1f) * q + back);
                    float pos = ease * totalFaces;

                    // el dado usa su propio ancla, centrado en la estrella de la mesa
                    float diecx = art.x + art.width * dieFx;
                    float diecy = art.y + art.height * dieFy;
                    var window = new Rect(diecx - size / 2f, diecy - size / 2f, size, size);
                    GUI.BeginGroup(window);
                    int k0 = Mathf.FloorToInt(pos);
                    for (int k = k0 - 1; k <= k0 + 1; k++)
                    {
                        int face = ((k + off) % 6 + 6) % 6;
                        GUI.DrawTexture(new Rect(0f, (pos - k) * size, size, size), faces[face], ScaleMode.ScaleToFit);
                    }
                    GUI.EndGroup();
                }
            }
            else if (_startResolved)
            {
                var done = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(art.height * 0.045f), fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                    wordWrap = false, clipping = TextClipping.Overflow
                };
                OutlinedLabel(new Rect(ccx - art.width * 0.22f, ccy - art.height * 0.05f, art.width * 0.44f, art.height * 0.10f),
                    $"{Current.Name} goes first!", done,
                    new Color(1f, 0.85f, 0.3f), new Color(0.12f, 0.06f, 0.01f), 3f);
            }
            else if (_contenderPtr < _contenders.Count)
            {
                if (!CurrentRoller.IsBot)
                {
                    var ornateRoll = new GUIStyle(UIFonts.OrnateButton) { fontSize = 20 };
                    float bcx = art.x + art.width * btnFx;
                    float bcy = art.y + art.height * btnFy;
                    var rollBtn = new Rect(bcx - btnW2 / 2f, bcy - btnH2 / 2f, btnW2, btnH2);
                    if (GUI.Button(rollBtn, "    Roll die", ornateRoll))
                        StartRollCurrent();
                    DrawButtonIcon(rollBtn, GetTex("UI/Icons/icon_dice"), 36f);
                }
                else
                {
                    var hint = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = (int)(art.height * 0.034f), fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                        wordWrap = false, clipping = TextClipping.Overflow
                    };
                    OutlinedLabel(new Rect(ccx - art.width * 0.2f, ccy - art.height * 0.035f, art.width * 0.4f, art.height * 0.07f),
                        $"{CurrentRoller.Name} is about to roll...", hint,
                        new Color(0.95f, 0.92f, 0.82f), new Color(0.12f, 0.06f, 0.01f), 2f);
                }
            }
            GUI.color = prevColor;
        }

        // --- Animación de dados ---
        bool _diceAnimating;
        float _diceAnimStart;
        Texture2D[] _diceFaces;
        const float DieSettleFirst = 0.85f;  // el primer dado se asienta antes
        const float DieSettleSecond = 1.35f; // el segundo crea el suspenso

        Texture2D[] GetDiceFaces()
        {
            if (_diceFaces == null)
            {
                _diceFaces = new Texture2D[6];
                for (int i = 0; i < 6; i++)
                    _diceFaces[i] = Resources.Load<Texture2D>($"Dice/die_{i + 1}");
            }
            return _diceFaces;
        }

        void RollDice()
        {
            if (Current.RestTurns > 0)
            {
                // en la cárcel, tirar los dados es el intento de sacar pares
                JailRoll();
                return;
            }

            _die1 = Random.Range(1, 7);
            _die2 = Random.Range(1, 7);
            StartCoroutine(DiceRollRoutine());
        }

        // ---------- Cárcel: fianza, intentar pares o esperar ----------

        void PayBail()
        {
            Current.Money -= BailCost;
            PlayCoinDrain(Current, BailCost);
            Current.RestTurns = 0;
            _log = $"{Current.Name} pays the ${BailCost} bail and is free. Roll the dice!";
            SettleDebts(Current);
        }

        void WaitInJail()
        {
            Current.RestTurns--;
            _log = Current.RestTurns <= 0
                ? $"{Current.Name} serves the sentence and will be free next turn."
                : $"{Current.Name} waits in jail ({Current.RestTurns} turn(s) left).";
            NextPlayer();
        }

        void JailRoll()
        {
            _die1 = Random.Range(1, 7);
            _die2 = Random.Range(1, 7);
            StartCoroutine(JailRollRoutine());
        }

        IEnumerator JailRollRoutine()
        {
            _state = GameState.Moving;
            _log = $"{Current.Name} rolls, hoping for doubles to escape jail...";
            AudioManager.PlayRandom("sfx_dice");
            _diceAnimating = true;
            _diceAnimStart = Time.time;
            yield return new WaitForSeconds(DieSettleSecond + 0.8f);
            _diceAnimating = false;

            if (_die1 == _die2)
            {
                Current.RestTurns = 0;
                _doublesInARow = 0;
                _log = $"{Current.Name} rolls doubles ({_die1}+{_die2}) and escapes jail!";
                StartCoroutine(MoveRoutine(_die1 + _die2, false)); // sin turno extra por los pares
            }
            else
            {
                Current.RestTurns--;
                _log = Current.RestTurns <= 0
                    ? $"{Current.Name} fails ({_die1}+{_die2}), but the sentence is over: free next turn."
                    : $"{Current.Name} fails ({_die1}+{_die2}) and stays in jail ({Current.RestTurns} left).";
                PlayReaction(Current, false);
                NextPlayer();
            }
        }

        // ---------- Casas: condición = grupo de color completo (monopolio) ----------

        /// <summary>Propiedades donde el jugador de turno puede construir ahora.</summary>
        List<int> BuildableTiles()
        {
            var list = new List<int>();
            if (_players.Count == 0) return list;
            foreach (int idx in Current.OwnedTiles)
            {
                var t = Board.Tiles[idx];
                if (t.Type == TileType.Property && OwnsFullGroup(Current, t.ColorGroup)
                    && t.Houses < 4 && Current.Money >= HouseCost(t))
                    list.Add(idx);
            }
            return list;
        }

        /// <summary>Construye una casa fuera del flujo de casilla (botón del turno).</summary>
        void BuildHouseAt(int idx)
        {
            var t = Board.Tiles[idx];
            int cost = HouseCost(t);
            Current.Money -= cost;
            PlayCoinDrain(Current, cost);
            AudioManager.Play("sfx_build_house");
            t.Houses++;
            _housesBuilt++;
            Board.SetHouses(idx, t.Houses);
            PlayReaction(Current, true);
            _log = $"{Current.Name} builds a house on {t.Name} (${cost}). Now has {t.Houses}.";
            SettleDebts(Current);
        }

        /// <summary>Dados girando en el centro de la pantalla, luego el resultado y el movimiento.</summary>
        IEnumerator DiceRollRoutine()
        {
            _state = GameState.Moving; // bloquear acciones durante la animación
            _log = $"{Current.Name} rolls the dice...";
            AudioManager.PlayRandom("sfx_dice");
            _diceAnimating = true;
            _diceAnimStart = Time.time;

            // rodando (se asientan uno tras otro) + pausa mostrando el resultado
            yield return new WaitForSeconds(DieSettleSecond + 0.8f);

            _diceAnimating = false;

            bool doubles = _die1 == _die2;
            _doublesInARow = doubles ? _doublesInARow + 1 : 0;

            if (_doublesInARow >= 3)
            {
                _log = $"{Current.Name} rolled 3 doubles in a row... straight to jail!";
                SendToJail(Current);
                PlayReaction(Current, false);
                _doublesInARow = 0;
                NextPlayer();
                yield break;
            }

            _log = $"{Current.Name} rolls: {_die1} + {_die2} = {_die1 + _die2}" + (doubles ? " (doubles!)" : "");
            StartCoroutine(MoveRoutine(_die1 + _die2, doubles));
        }

        /// <summary>
        /// Dados estilo casino: fondo oscurecido, caras cambiando cada vez más
        /// lento (tragamonedas), temblor sutil, se asientan uno tras otro con un
        /// "pop", y el total aparece encima (solo el número).
        /// </summary>
        void DrawDiceAnimation()
        {
            var faces = GetDiceFaces();
            if (faces[0] == null) return;

            float t = Time.time - _diceAnimStart;
            float size = Mathf.Min(Screen.width, Screen.height) * 0.17f;
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f - 10f;

            // atmósfera: oscurecer la mesa
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            for (int d = 0; d < 2; d++)
            {
                float settleAt = d == 0 ? DieSettleFirst : DieSettleSecond;
                int target = (d == 0 ? _die1 : _die2) - 1;
                int totalFaces = d == 0 ? 18 : 26; // cuántas caras "bajan" en el rodillo
                int offset = ((target - totalFaces) % 6 + 6) % 6; // para aterrizar en el resultado

                // avance del rodillo con desaceleración y leve rebote final (easeOutBack)
                float p = Mathf.Clamp01(t / settleAt);
                float back = 0.9f;
                float q = p - 1f;
                float ease = 1f + q * q * ((back + 1f) * q + back);
                float pos = ease * totalFaces;

                float centerX = cx + (d == 0 ? -(size / 2f + 14f) : (size / 2f + 14f));
                var window = new Rect(centerX - size / 2f, cy - size / 2f, size, size);

                // ventana recortada: las caras se deslizan hacia abajo en cascada
                GUI.BeginGroup(window);
                int k0 = Mathf.FloorToInt(pos);
                for (int k = k0 - 1; k <= k0 + 1; k++)
                {
                    int face = ((k + offset) % 6 + 6) % 6;
                    float y = (pos - k) * size; // 0 = centrada; al crecer pos, baja
                    GUI.DrawTexture(new Rect(0f, y, size, size), faces[face], ScaleMode.ScaleToFit);
                }
                GUI.EndGroup();
            }

            // total encima de los dados (solo el número), con pop al aparecer
            if (t >= DieSettleSecond)
            {
                float k = Mathf.Clamp01((t - DieSettleSecond) / 0.2f);
                var totalStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(66f + 34f * (1f - k)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    font = UIFonts.Title
                };
                OutlinedLabel(new Rect(cx - 160f, cy - size / 2f - 120f, 320f, 96f),
                    (_die1 + _die2).ToString(), totalStyle, new Color(1f, 0.86f, 0.25f), Color.black, 4f);

                if (_die1 == _die2)
                {
                    var db = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                    };
                    OutlinedLabel(new Rect(cx - 160f, cy + size * 0.62f, 320f, 40f),
                        "DOUBLES!", db, new Color(1f, 0.55f, 0.2f), Color.black, 3f);
                }
            }
        }

        IEnumerator MoveRoutine(int steps, bool doubles)
        {
            _state = GameState.Moving;

            int startTile = Current.Position;
            var character = Current.Token.GetComponent<CharacterToken>();
            if (character != null) character.SetWalking(true);

            for (int s = 0; s < steps; s++)
            {
                int prev = Current.Position;
                Current.Position = (Current.Position + 1) % BoardGenerator.TileCount;

                if (Current.Position == 0 && prev != 0)
                {
                    Current.Money += Salary;
                    AudioManager.Play("sfx_go"); // caja registradora al pasar por GO
                    PlayCoinRain(Current, Salary);
                    _log = $"{Current.Name} passes GO and collects ${Salary}.";
                }

                Vector3 from = Current.Token.transform.position;
                Vector3 to = TileCenterPos(Current.Position);

                // Elegir caminata (lateral/frente/espalda) según la dirección del paso
                if (character != null)
                    character.SetMoveDirection(to - from);

                // traslación suave hasta la casilla (sin teletransporte, ritmo de caminata)
                const float stepDur = 0.4f;
                float t0 = Time.time;
                while (Time.time - t0 < stepDur)
                {
                    float k = Mathf.Clamp01((Time.time - t0) / stepDur);
                    Current.Token.transform.position = Vector3.Lerp(from, to, k);
                    yield return null;
                }
                Current.Token.transform.position = to;
            }

            if (character != null) character.SetWalking(false);

            // Redistribuir fichas en la casilla de origen y la de llegada
            RepositionTile(startTile, true);
            RepositionTile(Current.Position, true);

            ResolveTile(doubles);
        }

        void ResolveTile(bool doubles)
        {
            TileData tile = Board.Tiles[Current.Position];

            switch (tile.Type)
            {
                case TileType.Property:
                case TileType.Studio:
                case TileType.Utility:
                    if (tile.OwnerIndex == -1)
                    {
                        EnterDecision(tile, false);
                        _log = $"{tile.Name} is unowned. Price: ${tile.Price}.";
                        return;
                    }
                    if (tile.OwnerIndex != _currentIndex)
                    {
                        int rent = CalcRent(tile);
                        Pay(Current, _players[tile.OwnerIndex], rent);
                        _log = $"{Current.Name} pays ${rent} rent to {_players[tile.OwnerIndex].Name} for {tile.Name}.";
                    }
                    else
                    {
                        // En propiedad propia: ofrecer construir casa si tiene el grupo completo
                        if (tile.Type == TileType.Property && OwnsFullGroup(Current, tile.ColorGroup)
                            && tile.Houses < 4 && Current.Money >= HouseCost(tile))
                        {
                            EnterDecision(tile, true);
                            _log = $"{Current.Name} can build a house on {tile.Name} for ${HouseCost(tile)}.";
                            return;
                        }
                        _log = $"{Current.Name} visits their property {tile.Name}.";
                    }
                    break;

                case TileType.Tax:
                    Pay(Current, null, tile.Price);
                    _log = $"{Current.Name} pays ${tile.Price} for {tile.Name}.";
                    break;

                case TileType.Chance:
                case TileType.Community:
                    DrawEventCard(tile.Type, doubles);
                    return; // la carta continúa el turno al resolverse

                case TileType.GoToJail:
                    _log = $"{Current.Name} lands on GO TO JAIL! Pay bail, roll doubles or wait.";
                    SendToJail(Current);
                    break;

                case TileType.Start:
                case TileType.Jail:
                case TileType.FreeParking:
                default:
                    _log = $"{Current.Name} rests at {tile.Name}.";
                    break;
            }

            EndOfMove(doubles);
        }

        int CalcRent(TileData tile)
        {
            var owner = _players[tile.OwnerIndex];

            if (tile.Type == TileType.Studio)
            {
                int studios = 0;
                foreach (int idx in owner.OwnedTiles)
                    if (Board.Tiles[idx].Type == TileType.Studio) studios++;
                return tile.BaseRent * studios;
            }

            if (tile.Type == TileType.Utility)
                return (_die1 + _die2) * 8;

            bool fullGroup = OwnsFullGroup(owner, tile.ColorGroup);
            int rent = fullGroup ? tile.BaseRent * 2 : tile.BaseRent;
            return rent * (1 + tile.Houses);
        }

        static int HouseCost(TileData t) => Mathf.Max(50, t.Price / 2);

        bool OwnsFullGroup(PlayerState owner, int group)
        {
            if (group < 0) return false;
            for (int i = 0; i < Board.Tiles.Count; i++)
            {
                var t = Board.Tiles[i];
                if (t.Type == TileType.Property && t.ColorGroup == group && t.OwnerIndex != _players.IndexOf(owner))
                    return false;
            }
            return true;
        }



        void SendToJail(PlayerState p)
        {
            AudioManager.Play("sfx_jail");
            int fromTile = p.Position;
            p.Position = 10;
            p.RestTurns = RestPenaltyTurns;
            p.Token.transform.position = TileCenterPos(10);
            RepositionTile(fromTile);
            RepositionTile(10);
        }

        void Pay(PlayerState from, PlayerState to, int amount)
        {
            from.Money -= amount;
            PlayCoinDrain(from, amount); // abducción de monedas para el que paga
            if (to != null)
            {
                to.Money += amount;
                PlayCoinRain(to, amount); // lluvia de monedas para el que cobra
            }
            PlayReaction(from, false); // animación de derrota al pagar
            SettleDebts(from);
        }

        // ---------- VFX: monedas al ganar/perder oro ----------
        Texture2D[] _coinFrames;
        CoinRainSettings _coinCfg;
        bool _coinCfgSearched;
        float _vfxHoldUntil; // el turno espera a que termine el VFX + delay
        static Mesh _coinQuadMesh;

        CoinRainSettings CoinCfg
        {
            get
            {
                if (!_coinCfgSearched)
                {
                    _coinCfgSearched = true;
                    _coinCfg = FindAnyObjectByType<CoinRainSettings>();
                }
                return _coinCfg;
            }
        }

        /// <summary>Quad compartido para renderizar las monedas como malla girable en 3D.</summary>
        static Mesh CoinQuadMesh
        {
            get
            {
                if (_coinQuadMesh == null)
                {
                    var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    _coinQuadMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                    Destroy(tmp);
                }
                return _coinQuadMesh;
            }
        }

        // popups activos por ficha (la corona del líder se aparta hacia arriba)
        readonly Dictionary<Transform, int> _popupCounts = new Dictionary<Transform, int>();

        /// <summary>Texto flotante sobre la placa: +$X en verde o -$X en rojo.</summary>
        void SpawnMoneyPopup(PlayerState p, int amount, bool gain)
        {
            if (p == null || p.Token == null || !p.Token.activeInHierarchy || amount <= 0) return;
            StartCoroutine(MoneyPopupRoutine(p.Token.transform, amount, gain));
        }

        IEnumerator MoneyPopupRoutine(Transform token, int amount, bool gain)
        {
            var ct = token.GetComponent<CharacterToken>();
            float baseY = (ct != null ? ct.PlateTopY : 2.2f) + 0.38f;

            var go = new GameObject("MoneyPopup");
            go.transform.SetParent(token, false); // hereda el billboard del token

            string txt = (gain ? "+$" : "-$") + amount;
            var font = UIFonts.Title;

            var tm = go.AddComponent<TextMesh>();
            tm.text = txt;
            tm.fontSize = 90;
            tm.fontStyle = FontStyle.Bold;      // negrita bien indicativa
            tm.characterSize = 0.065f;          // punto medio de tamaño
            tm.anchor = TextAnchor.MiddleCenter; // centrado horizontal
            tm.alignment = TextAlignment.Center;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().material = font.material;
            }
            Color col = gain ? new Color(0.30f, 1f, 0.30f) : new Color(1f, 0.28f, 0.24f);
            tm.color = col;

            _popupCounts.TryGetValue(token, out int n);
            _popupCounts[token] = n + 1;

            const float dur = 2.8f;
            float t0 = Time.time;
            while (go != null && token != null && Time.time - t0 < dur)
            {
                float k = (Time.time - t0) / dur;
                // pop de entrada y ascenso suave
                float s = k < 0.12f ? Mathf.SmoothStep(0.4f, 1.15f, k / 0.12f)
                        : k < 0.25f ? Mathf.SmoothStep(1.15f, 1f, (k - 0.12f) / 0.13f) : 1f;
                go.transform.localScale = Vector3.one * s;
                go.transform.localPosition = new Vector3(0f, baseY + k * 0.45f, -0.02f);

                // de cara a la cámara aunque el token sea un modelo 3D
                var camP = Camera.main;
                if (camP != null)
                    go.transform.rotation =
                        Quaternion.LookRotation(camP.transform.forward, camP.transform.up);

                col.a = k < 0.72f ? 1f : 1f - (k - 0.72f) / 0.28f; // se desvanece al final
                tm.color = col;
                yield return null;
            }
            if (go != null) Destroy(go);
            if (token != null && _popupCounts.TryGetValue(token, out int m))
            {
                if (m <= 1) _popupCounts.Remove(token);
                else _popupCounts[token] = m - 1;
            }
        }

        void PlayCoinRain(PlayerState p, int amount = 0)
        {
            if (p == null || p.Token == null || !p.Token.activeInHierarchy) return;

            if (amount > 0) SpawnMoneyPopup(p, amount, true);
            AudioManager.Play("sfx_coins_gain");

            float rainDur = CoinCfg != null ? CoinCfg.rainDuration : 3f;
            float post = CoinCfg != null ? CoinCfg.postDelay : 1f;
            // el turno no continúa hasta que la lluvia termine + delay
            _vfxHoldUntil = Mathf.Max(_vfxHoldUntil, Time.time + rainDur + post);

            // modo elegible desde el Inspector (CoinRainSettings en el Bootstrap)
            if (CoinCfg != null && CoinCfg.mode == CoinRainMode.Particles)
            {
                SpawnCoinParticles(p.Token.transform);
                return;
            }

            if (_coinFrames == null)
            {
                _coinFrames = new Texture2D[16];
                for (int i = 0; i < 16; i++)
                    _coinFrames[i] = Resources.Load<Texture2D>($"VFX/coin_rain_{i}");
            }
            if (_coinFrames[0] == null) return;
            StartCoroutine(CoinRainRoutine(p.Token.transform));
        }

        /// <summary>
        /// Abducción de monedas al PERDER oro: salen del jugador, suben
        /// acelerando y se desvanecen en el aire.
        /// </summary>
        void PlayCoinDrain(PlayerState p, int amount = 0)
        {
            if (p == null || p.Token == null || !p.Token.activeInHierarchy) return;

            if (amount > 0) SpawnMoneyPopup(p, amount, false);
            AudioManager.Play("sfx_coins_lose");

            var coinTex = Resources.Load<Texture2D>("VFX/Coin");
            if (coinTex == null) return;

            var cfg = CoinCfg;
            int count = cfg != null ? cfg.burstCoins : 26;
            float size = cfg != null ? cfg.coinSize : 0.26f;
            float radius = cfg != null ? cfg.radius : 0.55f;
            float rainDur = cfg != null ? cfg.rainDuration : 3f;
            float post = cfg != null ? cfg.postDelay : 1f;
            _vfxHoldUntil = Mathf.Max(_vfxHoldUntil, Time.time + rainDur + post);

            var target = p.Token.transform;
            var ct = target.GetComponent<CharacterToken>();
            float floorY = target.position.y - 0.48f; // superficie del tablero
            float riseH = (ct != null ? ct.WorldHeight : 2.2f) + 1.2f; // hasta dónde suben

            // la lluvia al revés: las monedas nacen EN EL SUELO alrededor de la ficha
            var go = new GameObject("CoinDrainParticles");
            go.transform.position = new Vector3(target.position.x, floorY + 0.03f, target.position.z);

            var ps = go.AddComponent<ParticleSystem>();
            // el sistema nace reproduciéndose: detenerlo antes de configurar duration
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            float coinLife = 1.7f; // lo que tarda cada moneda en subir y esfumarse
            main.duration = rainDur - coinLife; // emisión repartida en todo el efecto
            main.loop = false;
            main.startLifetime = coinLife;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.75f, size * 1.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = count / Mathf.Max(0.5f, rainDur - coinLife);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(2, count / 5)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 0.04f, radius * 1.4f); // capa plana en el suelo

            // abducción: despegan lento del suelo y aceleran hacia arriba
            // (los 3 ejes deben ir en el MISMO modo: todos como curvas)
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            var up = new AnimationCurve(new Keyframe(0f, 0.08f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 1f));
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
            vel.x = new ParticleSystem.MinMaxCurve(1f, flat);
            vel.y = new ParticleSystem.MinMaxCurve(riseH / coinLife * 1.6f, up);
            vel.z = new ParticleSystem.MinMaxCurve(1f, flat);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            // desaparecen ARRIBA, al final de su subida
            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colLife.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            renderer.material = new Material(sh) { mainTexture = coinTex };
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CoinQuadMesh; // dos caras: el shader de partículas no hace culling
            renderer.alignment = ParticleSystemRenderSpace.Local;

            ps.Play();
            Destroy(go, rainDur + 1f);
        }

        /// <summary>
        /// Lluvia de monedas por Particle System (textura VFX/Coin): caen girando
        /// en los 3 ejes, rebotan y QUEDAN en el suelo del tablero hasta el final.
        /// </summary>
        void SpawnCoinParticles(Transform target)
        {
            var coinTex = Resources.Load<Texture2D>("VFX/Coin");
            if (coinTex == null) return;

            var cfg = CoinCfg;
            int count = cfg != null ? cfg.burstCoins : 26;
            float size = cfg != null ? cfg.coinSize : 0.26f;
            float radius = cfg != null ? cfg.radius : 0.55f;
            float grav = cfg != null ? cfg.gravity : 2.4f;
            float rainDur = cfg != null ? cfg.rainDuration : 3f;

            var ct = target.GetComponent<CharacterToken>();
            float topY = target.position.y + (ct != null ? ct.WorldHeight : 2.2f) + 0.6f;
            float floorY = target.position.y - 0.48f; // superficie del tablero (pies del token)

            var go = new GameObject("CoinRainParticles");
            go.transform.position = new Vector3(target.position.x, topY, target.position.z);

            // plano de colisión: el suelo del tablero
            var floor = new GameObject("CoinFloor");
            floor.transform.SetParent(go.transform, true);
            floor.transform.position = new Vector3(target.position.x, floorY, target.position.z);
            floor.transform.rotation = Quaternion.identity; // normal hacia arriba

            var ps = go.AddComponent<ParticleSystem>();
            // el sistema nace reproduciéndose: detenerlo antes de configurar duration
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = rainDur * 0.5f;   // ventana de emisión
            main.loop = false;
            main.startLifetime = rainDur * 0.8f; // viven hasta el final del efecto
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.75f, size * 1.2f);
            main.gravityModifier = grav;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            // orientación inicial aleatoria en los 3 ejes
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = count / (rainDur * 0.5f);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(3, count / 3)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 0.15f, radius * 1.4f);

            // giro aleatorio en los 3 ejes mientras caen
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            // rebote suave y las monedas se quedan en el suelo
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            col.SetPlane(0, floor.transform);
            col.bounce = 0.18f;
            col.dampen = 0.6f;
            col.lifetimeLoss = 0f;

            // visibles todo el efecto; se desvanecen solo al final
            var colLife = ps.colorOverLifetime;
            colLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
            colLife.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Unlit/Transparent");
            renderer.material = new Material(sh) { mainTexture = coinTex };
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CoinQuadMesh; // plano de dos caras (shader de partículas sin culling)
            renderer.alignment = ParticleSystemRenderSpace.Local;

            ps.Play();
            // el emisor SIGUE a la ficha por su XZ (al pasar GO las monedas caen a lo
            // largo de su trayectoria, no se quedan en la casilla); simulación en World
            // así las ya emitidas quedan en el tablero formando la estela.
            StartCoroutine(FollowEmitterXZ(go.transform, target, topY, rainDur + 1.5f));
            Destroy(go, rainDur + 1.5f);
        }

        /// <summary>Mantiene un emisor de partículas encima de una ficha en movimiento
        /// (solo copia XZ; la Y queda fija para no arrastrar la lluvia verticalmente).</summary>
        IEnumerator FollowEmitterXZ(Transform fx, Transform target, float fixedY, float dur)
        {
            float t0 = Time.time;
            while (fx != null && target != null && Time.time - t0 < dur)
            {
                fx.position = new Vector3(target.position.x, fixedY, target.position.z);
                yield return null;
            }
        }

        IEnumerator CoinRainRoutine(Transform target)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CoinRainFx";
            Destroy(go.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Unlit/Transparent"));
            go.GetComponent<Renderer>().material = mat;

            const float size = 2.5f;
            const float frameTime = 0.075f;
            float rainDur = CoinCfg != null ? CoinCfg.rainDuration : 3f;
            float t0 = Time.time;
            while (target != null && Time.time - t0 < rainDur)
            {
                int idx = (int)((Time.time - t0) / frameTime) % 16; // en bucle toda la lluvia
                if (mat.mainTexture != _coinFrames[idx]) mat.mainTexture = _coinFrames[idx];

                go.transform.position = target.position + Vector3.up * 1.05f;
                var cam = Camera.main;
                if (cam != null)
                    go.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                go.transform.localScale = new Vector3(size, size, 1f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>
        /// Con el oro en negativo: liquidación forzosa (vende casas e hipoteca
        /// propiedades). Si ni así alcanza, quiebra con su animación de salida.
        /// </summary>
        void SettleDebts(PlayerState p)
        {
            if (p.Money >= 0 || p.Bankrupt) return;
            ForceLiquidate(p);
            if (p.Money < 0) StartCoroutine(BankruptRoutine(p));
        }

        /// <summary>
        /// Vende casas a mitad de precio (las más caras primero) y luego hipoteca
        /// propiedades con el banco (recibe la mitad del valor, las más baratas
        /// primero) hasta cubrir la deuda.
        /// </summary>
        void ForceLiquidate(PlayerState p)
        {
            // 1) vender casas
            bool sold = true;
            while (p.Money < 0 && sold)
            {
                sold = false;
                int best = -1;
                foreach (int idx in p.OwnedTiles)
                {
                    var t = Board.Tiles[idx];
                    if (t.Houses > 0 && (best < 0 || HouseCost(t) > HouseCost(Board.Tiles[best])))
                        best = idx;
                }
                if (best >= 0)
                {
                    var t = Board.Tiles[best];
                    t.Houses--;
                    Board.SetHouses(best, t.Houses);
                    int refund = HouseCost(t) / 2;
                    p.Money += refund;
                    _log = $"{p.Name} is broke and sells a house on {t.Name} (+${refund}).";
                    sold = true;
                }
            }

            // 2) hipotecar propiedades con el banco
            while (p.Money < 0 && p.OwnedTiles.Count > 0)
            {
                int best = -1;
                foreach (int idx in p.OwnedTiles)
                    if (best < 0 || Board.Tiles[idx].Price < Board.Tiles[best].Price)
                        best = idx;
                var bt = Board.Tiles[best];
                bt.OwnerIndex = -1;
                p.OwnedTiles.Remove(best);
                int refund2 = bt.Price / 2;
                p.Money += refund2;
                _log = $"{p.Name} mortgages {bt.Name} to the bank (+${refund2}).";
            }
        }

        /// <summary>
        /// Eliminación: fuera de la rotación de inmediato, animación de derrota
        /// durante 3 segundos y la ficha desaparece del tablero.
        /// </summary>
        IEnumerator BankruptRoutine(PlayerState p)
        {
            p.Bankrupt = true;
            p.EliminationOrder = ++_elimCounter;
            foreach (int idx in p.OwnedTiles)
                Board.Tiles[idx].OwnerIndex = -1;
            p.OwnedTiles.Clear();
            _log = $"{p.Name} goes bankrupt and is out!";

            var ct = p.Token != null ? p.Token.GetComponent<CharacterToken>() : null;
            if (ct != null) ct.PlayDefeat();
            yield return new WaitForSeconds(3f);

            if (p.Token != null) p.Token.SetActive(false);
            RepositionTile(p.Position);

            // Si la cámara seguía a este jugador, volver a seguir el turno
            if (_pinnedPlayer >= 0 && _players[_pinnedPlayer] == p)
                _followCurrent = true;

            int alive = 0;
            PlayerState winner = null;
            foreach (var pl in _players)
                if (!pl.Bankrupt) { alive++; winner = pl; }

            if (alive <= 1 && winner != null)
            {
                _state = GameState.GameOver;
                _winner = winner;
                _gameDuration = Time.time - _gameStartTime;
                _log = $"🏆 {winner.Name} wins the game with ${winner.Money}!";
                PlayReaction(winner, true);
            }
        }

        PlayerState _winner;
        int _elimCounter;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Solo testeo: termina la partida ya. Gana el jugador del turno;
        /// el resto queda en el ranking según su dinero (el más pobre, último).</summary>
        void DebugEndGame()
        {
            StopAllCoroutines();
            _diceAnimating = false;
            _pendingTile = null;
            _drawnCard = null;

            var winner = Current;
            var losers = new List<PlayerState>();
            foreach (var p in _players)
                if (p != winner && !p.Bankrupt) losers.Add(p);
            losers.Sort((a, b) => a.Money.CompareTo(b.Money)); // el más pobre cae primero

            foreach (var p in losers)
            {
                p.Bankrupt = true;
                p.EliminationOrder = ++_elimCounter;
                p.Token.SetActive(false);
            }

            _winner = winner;
            _state = GameState.GameOver;
            _gameDuration = Time.time - _gameStartTime;
            _log = $"🏆 {winner.Name} wins the game with ${winner.Money}!";
            PlayReaction(winner, true);
        }
#endif

        void BuyPending()
        {
            if (_pendingTile == null) return;
            Current.Money -= _pendingTile.Price;
            PlayCoinDrain(Current, _pendingTile.Price);
            _pendingTile.OwnerIndex = _currentIndex;
            Current.OwnedTiles.Add(Current.Position);
            PlayReaction(Current, true);
            _log = $"{Current.Name} buys {_pendingTile.Name} for ${_pendingTile.Price}.";
            _pendingTile = null;
            _pendingIsHouse = false;

            SettleDebts(Current);
            EndOfMove(_die1 == _die2);
        }

        void SkipPending()
        {
            _log = _pendingIsHouse
                ? $"{Current.Name} decides not to build on {_pendingTile.Name}."
                : $"{Current.Name} decides not to buy {_pendingTile.Name}.";
            _pendingTile = null;
            _pendingIsHouse = false;
            EndOfMove(_die1 == _die2);
        }

        void BuildHouse()
        {
            if (_pendingTile == null) return;
            int cost = HouseCost(_pendingTile);
            Current.Money -= cost;
            PlayCoinDrain(Current, cost);
            AudioManager.Play("sfx_build_house");
            _pendingTile.Houses++;
            _housesBuilt++;
            Board.SetHouses(Current.Position, _pendingTile.Houses);
            PlayReaction(Current, true);
            _log = $"{Current.Name} builds a house on {_pendingTile.Name} (${cost}). Now has {_pendingTile.Houses}.";
            _pendingTile = null;
            _pendingIsHouse = false;

            SettleDebts(Current);
            EndOfMove(_die1 == _die2);
        }

        // ---------- Estadísticas de la partida ----------
        int _turnCount;      // turnos jugados en total
        int _housesBuilt;    // casas compradas entre todos
        int _round = 1;      // ronda actual (una vuelta completa de todos)
        float _gameStartTime;
        float _gameDuration; // congelada al terminar la partida

        /// <summary>Patrimonio: oro + valor de propiedades y casas.</summary>
        int NetWorth(PlayerState p)
        {
            int worth = p.Money;
            foreach (int idx in p.OwnedTiles)
            {
                var t = Board.Tiles[idx];
                worth += t.Price + t.Houses * HouseCost(t);
            }
            return worth;
        }

        /// <summary>Fin de partida por límite (tiempo o rondas): gana el mayor patrimonio.</summary>
        void EndByLimit(string motivo)
        {
            var alive = new List<PlayerState>();
            foreach (var p in _players) if (!p.Bankrupt) alive.Add(p);
            if (alive.Count == 0) return;
            alive.Sort((a, b) => NetWorth(b).CompareTo(NetWorth(a)));

            // ranking para la pantalla de resultados: mejor patrimonio = mejor puesto
            for (int i = alive.Count - 1; i >= 1; i--)
                alive[i].EliminationOrder = ++_elimCounter;

            _winner = alive[0];
            _state = GameState.GameOver;
            _gameDuration = Time.time - _gameStartTime;
            _log = $"{motivo} {_winner.Name} wins with the highest net worth (${NetWorth(_winner)})!";
            PlayReaction(_winner, true);
        }

        Texture2D _resultsPanelClean; // pergamino con el fondo blanco/checker eliminado

        // ---------- Corona del líder (VFX) ----------
        GameObject _crownFx;
        Material _crownMat;
        Texture2D[] _crownFrames;
        GameObject _crownGlow;
        Material _crownGlowMat;
        const float CrownFrameTime = 0.18f; // giro pausado y suave

        /// <summary>Halo radial dorado generado en runtime para el resplandor de la corona.</summary>
        static Texture2D MakeGlowTex()
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            float c = (S - 1) / 2f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a * a; // caída suave hacia el borde
                    px[y * S + x] = new Color32(255, 224, 115, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Corona animada flotando sobre el jugador que va ganando (mayor
        /// patrimonio). Con empate en cabeza no se muestra.
        /// </summary>
        void UpdateLeaderCrown()
        {
            bool inGame = _state != GameState.Setup && _state != GameState.StartRoll
                          && _state != GameState.GameOver && _players.Count > 0;

            PlayerState leader = null;
            if (inGame)
            {
                int best = int.MinValue; int bestCount = 0;
                foreach (var p in _players)
                {
                    if (p.Bankrupt || p.Token == null) continue;
                    int w = NetWorth(p);
                    if (w > best) { best = w; leader = p; bestCount = 1; }
                    else if (w == best) bestCount++;
                }
                if (bestCount != 1) leader = null; // empate: nadie va ganando
            }

            if (leader == null)
            {
                if (_crownFx != null) _crownFx.SetActive(false);
                return;
            }

            if (_crownFrames == null)
            {
                _crownFrames = new Texture2D[8];
                for (int i = 0; i < 8; i++)
                    _crownFrames[i] = Resources.Load<Texture2D>($"VFX/crown_{i}");
            }
            if (_crownFrames[0] == null) return;

            if (_crownFx == null)
            {
                _crownFx = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _crownFx.name = "LeaderCrown";
                Destroy(_crownFx.GetComponent<Collider>());
                _crownMat = new Material(Shader.Find("Unlit/Transparent"));
                _crownFx.GetComponent<Renderer>().material = _crownMat;

                // resplandor dorado detrás de la corona
                _crownGlow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _crownGlow.name = "CrownGlow";
                Destroy(_crownGlow.GetComponent<Collider>());
                var sh = Shader.Find("Legacy Shaders/Particles/Additive");
                if (sh == null) sh = Shader.Find("Unlit/Transparent");
                _crownGlowMat = new Material(sh) { mainTexture = MakeGlowTex() };
                _crownGlow.GetComponent<Renderer>().material = _crownGlowMat;
                _crownGlow.transform.SetParent(_crownFx.transform, false);
                _crownGlow.transform.localPosition = new Vector3(0f, 0f, 0.02f);
                _crownGlow.transform.localScale = new Vector3(2.0f, 2.3f, 1f);
            }

            _crownFx.SetActive(true);

            // seguir al líder (hereda el billboard del token)
            var ct = leader.Token.GetComponent<CharacterToken>();
            if (_crownFx.transform.parent != leader.Token.transform)
                _crownFx.transform.SetParent(leader.Token.transform, false);

            float aspect = (float)_crownFrames[0].width / _crownFrames[0].height;
            const float crownH = 0.62f; // corona bien visible
            float bob = Mathf.Sin(Time.time * 1.4f) * 0.05f; // flotación lenta y suave
            float plateTop = ct != null ? ct.PlateTopY : 2.2f;
            float extra = 0f;
            if (_popupCounts.TryGetValue(leader.Token.transform, out int pcount) && pcount > 0)
                extra = 0.72f; // deja sitio al texto de ganancia/pérdida

            var camC = Camera.main;
            bool topView = CameraOrbitController.Instance != null
                        && CameraOrbitController.Instance.Mode == CameraMode.Top;
            if (topView && camC != null && ct != null)
            {
                // En cenital un offset en Y es PROFUNDIDAD (la corona caería sobre el
                // avatar). Se apila sobre la placa por el eje "arriba" de la pantalla,
                // encima de la etiqueta del nombre.
                Vector3 up = camC.transform.up;
                _crownFx.transform.position = ct.PlateWorldCenter
                    + up * (ct.PlateHeightWorld * 0.5f + crownH * 0.5f + 0.06f + extra + bob)
                    - camC.transform.forward * 0.05f;
            }
            else
            {
                // apoyada justo sobre la placa del nombre; con popup de dinero, más arriba
                _crownFx.transform.localPosition = new Vector3(0f, plateTop + extra + 0.05f + crownH / 2f + bob, 0f);
            }
            _crownFx.transform.localScale = new Vector3(crownH * aspect, crownH, 1f);

            var tex = _crownFrames[(int)(Time.time / CrownFrameTime) % 8];
            if (tex != null && _crownMat.mainTexture != tex)
                _crownMat.mainTexture = tex;

            // de cara a la cámara aunque el token sea un modelo 3D (sin billboard)
            if (camC != null)
                _crownFx.transform.rotation =
                    Quaternion.LookRotation(camC.transform.forward, camC.transform.up);

            // pulso suave del resplandor
            if (_crownGlowMat != null)
            {
                var gc = new Color(1f, 0.85f, 0.4f, 0.5f + 0.22f * Mathf.Sin(Time.time * 2.1f));
                if (_crownGlowMat.HasProperty("_TintColor")) _crownGlowMat.SetColor("_TintColor", gc);
                else _crownGlowMat.color = gc;
            }
        }

        /// <summary>
        /// Elimina en tiempo de ejecución el fondo blanco/checker horneado de una
        /// textura (requiere Read/Write). Vacía los píxeles brillantes y sin color
        /// conectados al borde de la imagen.
        /// </summary>
        static Texture2D StripBackground(Texture2D src)
        {
            try
            {
                int w = src.width, h = src.height;
                var px = src.GetPixels32();

                bool IsBg(Color32 c)
                {
                    int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    int mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    return (c.r + c.g + c.b) > 525 && (mx - mn) < 24; // brillante y gris/blanco
                }

                var seen = new bool[w * h];
                var q = new Queue<int>();
                void Push(int i) { if (!seen[i] && IsBg(px[i])) { seen[i] = true; q.Enqueue(i); } }

                for (int x = 0; x < w; x++) { Push(x); Push((h - 1) * w + x); }
                for (int y = 0; y < h; y++) { Push(y * w); Push(y * w + w - 1); }

                while (q.Count > 0)
                {
                    int i = q.Dequeue();
                    int x = i % w, y = i / w;
                    if (x > 0) Push(i - 1);
                    if (x < w - 1) Push(i + 1);
                    if (y > 0) Push(i - w);
                    if (y < h - 1) Push(i + w);
                }

                for (int i = 0; i < px.Length; i++)
                    if (seen[i]) px[i] = new Color32(0, 0, 0, 0);

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                return tex;
            }
            catch (System.Exception)
            {
                return src; // si la textura no es legible, usarla tal cual
            }
        }

        void EndOfMove(bool doubles)
        {
            if (_state == GameState.GameOver) return;

            if (doubles && !Current.Bankrupt)
            {
                _state = GameState.WaitingRoll;
                _log += $" {Current.Name} plays again for rolling doubles.";
                return;
            }

            NextPlayer();
        }

        const float TurnChangeDelay = 2f; // respiro entre turno y turno
        bool _nextTurnPending;

        void NextPlayer()
        {
            if (_state == GameState.GameOver) return;
            if (_nextTurnPending) return; // ya hay un cambio de turno en marcha

            _nextTurnPending = true;
            _state = GameState.Moving; // estado inerte mientras corre el delay
            StartCoroutine(NextPlayerAfterDelay());
        }

        System.Collections.IEnumerator NextPlayerAfterDelay()
        {
            yield return new WaitForSeconds(TurnChangeDelay);
            // esperar a que terminen los VFX de monedas (+ su delay posterior)
            while (Time.time < _vfxHoldUntil) yield return null;
            _nextTurnPending = false;
            if (_state == GameState.GameOver) yield break;

            int prevIdx = _currentIndex;
            do { _currentIndex = (_currentIndex + 1) % _players.Count; }
            while (_players[_currentIndex].Bankrupt);

            _tradeUsed = false; // turno nuevo: se puede negociar otra vez
            _turnCount++;
            if (_currentIndex <= prevIdx) _round++; // dio la vuelta: nueva ronda

            // fin por límite, comprobado en el cambio de turno (punto seguro)
            if (GameConfig.Mode == EndMode.Rounds && _round > GameConfig.RoundLimit)
            {
                EndByLimit($"All {GameConfig.RoundLimit} rounds completed!");
                yield break;
            }
            if (GameConfig.Mode == EndMode.Time && _gameStartTime > 0f
                && Time.time - _gameStartTime >= GameConfig.TimeLimitMin * 60f)
            {
                EndByLimit("Time's up!");
                yield break;
            }

            _doublesInARow = 0;
            _state = GameState.WaitingRoll;
            UpdateFollowTarget();
        }

        // ---------- UI (IMGUI provisional) ----------

        void OnGUI()
        {
            UIFonts.ApplyBody(); // Alegreya para toda la UI
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUI.skin.box.fontSize = 14;

            if (_state == GameState.Setup)
            {
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 160, Screen.height / 2f - 100, 320, 220), GUI.skin.box);
                GUILayout.Label("MONOPOLY PlanA — Prototype");
                GUILayout.Space(10);
                GUILayout.Label("How many players?");
                for (int n = 2; n <= 4; n++)
                    if (GUILayout.Button($"{n} players", GUILayout.Height(36)))
                        StartGame(n);
                GUILayout.EndArea();
                return;
            }

            if (_players.Count == 0) return; // aún sin partida montada

            // Indicador del límite de partida (tiempo restante o ronda actual)
            if (_state != GameState.GameOver && _state != GameState.StartRoll && GameConfig.Mode != EndMode.Classic)
            {
                string info = "";
                if (GameConfig.Mode == EndMode.Time && _gameStartTime > 0f)
                {
                    float rest = Mathf.Max(0f, GameConfig.TimeLimitMin * 60f - (Time.time - _gameStartTime));
                    info = $"Time  {(int)(rest / 60f)}:{(int)(rest % 60f):00}";
                }
                else if (GameConfig.Mode == EndMode.Rounds)
                {
                    info = $"Round  {Mathf.Min(_round, GameConfig.RoundLimit)} / {GameConfig.RoundLimit}";
                }
                if (info != "")
                {
                    var lim = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 19, fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
                    };
                    // reloj de arena junto al contador
                    var timeIco = GetTex("UI/Icons/icon_time");
                    float tw = lim.CalcSize(new GUIContent(info)).x;
                    float cx0 = Screen.width / 2f;
                    if (timeIco != null)
                    {
                        float ih2 = 30f, iw2 = ih2 * ((float)timeIco.width / timeIco.height);
                        GUI.DrawTexture(new Rect(cx0 - tw / 2f - iw2 - 8f, 6f, iw2, ih2), timeIco, ScaleMode.ScaleToFit);
                    }
                    OutlinedLabel(new Rect(cx0 - 130f, 8f, 260f, 28f), info, lim,
                        new Color(1f, 0.85f, 0.3f), Color.black, 2f);
                }
            }

            // Con un modal abierto (tirada inicial, compra, carta o victoria), el resto queda desactivado
            bool modalOpen = _state == GameState.StartRoll
                          || (_state == GameState.Decision && _pendingTile != null)
                          || (_state == GameState.Card && _drawnCard != null)
                          || _state == GameState.GameOver
                          || _tradeOpen;
            GUI.enabled = !modalOpen;

            // HUDs de los jugadores activos, apilados en la esquina superior izquierda
            float hudBottom = DrawHud();

            // Panel de propiedades del jugador seleccionado (o del turno), debajo de los HUDs
            DrawPropertiesPanel(hudBottom + 6f);

            // Panel de cámara (arriba a la derecha)
            DrawCameraPanel();
            GUI.enabled = true;

            // Log, en el borde inferior
            GUI.Box(new Rect(200, Screen.height - 44f, Screen.width - 400, 36), _log);

            // Acciones
            if (Current != null && Current.IsBot && _state == GameState.WaitingRoll)
            {
                GUI.Box(new Rect(Screen.width / 2f - 90, Screen.height - 118f, 180, 44), $"{Current.Name} is\nthinking...");
            }
            else if (_state == GameState.WaitingRoll && !modalOpen)
            {
                if (Current.RestTurns > 0)
                {
                    // En la cárcel: pagar fianza, intentar pares o esperar el turno
                    float by = Screen.height - 118f;
                    float cx0 = Screen.width / 2f;
                    var payRect = new Rect(cx0 - 335, by, 215, 58);
                    GUI.enabled = Current.Money >= BailCost;
                    if (GUI.Button(payRect, $"     Pay bail (${BailCost})", UIFonts.OrnateButton))
                    {
                        AudioManager.Play("sfx_button");
                        PayBail();
                    }
                    GUI.enabled = true;
                    DrawButtonIcon(payRect, GetTex("UI/Icons/icon_coins"), 28f);

                    var rollRect2 = new Rect(cx0 - 107, by, 215, 58);
                    if (GUI.Button(rollRect2, "     Roll doubles", UIFonts.OrnateButton))
                    {
                        AudioManager.Play("sfx_button");
                        JailRoll();
                    }
                    DrawButtonIcon(rollRect2, GetTex("UI/Icons/icon_dice"), 30f);

                    var waitRect = new Rect(cx0 + 121, by, 215, 58);
                    if (GUI.Button(waitRect, $"     Wait ({Current.RestTurns})", UIFonts.OrnateButton))
                    {
                        AudioManager.Play("sfx_button");
                        WaitInJail();
                    }
                    DrawButtonIcon(waitRect, GetTex("UI/Icons/icon_jail"), 28f);
                }
                else
                {
                    // fila de acciones CENTRADA: Trade · Roll dice · Build house
                    // (Trade solo aparece si algún rival ya tiene propiedades que ofrecer)
                    bool hasRival = false;
                    for (int i = 0; i < _players.Count; i++)
                        if (i != _currentIndex && !_players[i].Bankrupt
                            && _players[i].OwnedTiles.Count > 0) { hasRival = true; break; }
                    var buildable = BuildableTiles();
                    bool canBuild = buildable.Count > 0;
                    if (!canBuild) _buildMenuOpen = false;

                    hasRival = hasRival && !_tradeUsed; // una sola negociación por turno
                    const float rollW = 220f, sideW = 200f, gap = 14f;
                    float totalW = rollW + (hasRival ? sideW + gap : 0f) + (canBuild ? sideW + gap : 0f);
                    float bx = Screen.width / 2f - totalW / 2f;
                    float by2 = Screen.height - 118f;

                    if (hasRival)
                    {
                        var tRect = new Rect(bx, by2 + 2f, sideW, 58f);
                        if (GUI.Button(tRect, "     Trade", UIFonts.OrnateButton))
                        {
                            AudioManager.Play("sfx_button");
                            ResetTrade();
                            _tradeOpen = true;
                            Time.timeScale = 0f; // el juego queda en pausa durante la oferta
                        }
                        DrawButtonIcon(tRect, GetTex("UI/Icons/icon_trade"), 30f);
                        bx += sideW + gap;
                    }

                    var rollRect = new Rect(bx, by2, rollW, 62f);
                    if (GUI.Button(rollRect, "    Roll dice", UIFonts.OrnateButton))
                    {
                        AudioManager.Play("sfx_button");
                        RollDice();
                    }
                    DrawButtonIcon(rollRect, GetTex("UI/Icons/icon_dice"), 36f);
                    bx += rollW + gap;

                    if (canBuild)
                    {
                        var bRect = new Rect(bx, by2 + 2f, sideW, 58f);
                        if (GUI.Button(bRect, "     Build house", UIFonts.OrnateButton))
                        {
                            AudioManager.Play("sfx_button");
                            _buildMenuOpen = !_buildMenuOpen;
                        }
                        DrawButtonIcon(bRect, GetTex("UI/Icons/icon_house"), 30f);
                    }

                    if (_buildMenuOpen)
                        DrawBuildMenu(buildable);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Botón de testeo: fuerza el final de la partida (gana el jugador del turno)
            if (_state != GameState.GameOver && !modalOpen)
            {
                if (GUI.Button(new Rect(10, Screen.height - 40f, 170, 30), "[TEST] End game"))
                    DebugEndGame();
            }
#endif

            // Animación de dados en el centro
            if (_diceAnimating)
                DrawDiceAnimation();

            // Modales por encima de los paneles
            if (_state == GameState.StartRoll)
                DrawStartRollModal();
            else if (_state == GameState.Decision && _pendingTile != null)
                DrawDecisionModal();
            else if (_state == GameState.Card && _drawnCard != null)
                DrawEventCardModal();
            else if (_state == GameState.GameOver && _winner != null)
                DrawVictoryScreen();
            else if (_tradeOpen)
                DrawTradeModal();

            // Carta ampliada (por encima de todo)
            DrawCardOverlay();
        }

        /// <summary>
        /// Modal centrado de compra/construcción: fondo oscurecido, carta con
        /// proporciones reales, botones y temporizador de 60 s (auto-pasa).
        /// </summary>
        void DrawDecisionModal()
        {
            Color prevColor = GUI.color;

            // Fondo oscurecido
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Carta centrada con su proporción real
            var tex = GetCard(_pendingTile);
            float cardH = Screen.height * 0.56f;
            float cardW = tex != null ? cardH * ((float)tex.width / tex.height) : cardH * 0.69f;
            float cx = Screen.width / 2f;
            var cardRect = new Rect(cx - cardW / 2f, Screen.height * 0.06f, cardW, cardH);

            if (tex != null) GUI.DrawTexture(cardRect, tex, ScaleMode.ScaleToFit);
            else GUI.Box(cardRect, _pendingTile.Name);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };

            float by = cardRect.yMax + 44f;

            if (!Current.IsBot)
            {
                int cost = _pendingIsHouse ? HouseCost(_pendingTile) : _pendingTile.Price;
                string question = _pendingIsHouse
                    ? $"Build a house on {_pendingTile.Name}?"
                    : $"Buy {_pendingTile.Name}?";
                GUI.Label(new Rect(cx - 400, cardRect.yMax + 6, 800, 32), question, title);

                var moneyStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(cx - 400, by + 54, 800, 22), $"Your gold: ${Current.Money}", moneyStyle);

                bool canPay = Current.Money >= cost;
                GUI.enabled = canPay;
                if (GUI.Button(new Rect(cx - 215, by, 205, 50), (_pendingIsHouse ? "Build" : "Buy") + $"  (${cost})"))
                {
                    GUI.enabled = true;
                    if (_pendingIsHouse) BuildHouse();
                    else BuyPending();
                    return;
                }
                GUI.enabled = true;
                if (GUI.Button(new Rect(cx + 10, by, 205, 50), "Pass"))
                {
                    SkipPending();
                    return;
                }

                // Temporizador: barra de progreso + aviso
                float frac = Mathf.Clamp01(_decisionLeft / DecisionTime);
                var barBg = new Rect(cx - 215, by + 80, 430, 10);
                GUI.color = new Color(1f, 1f, 1f, 0.18f);
                GUI.DrawTexture(barBg, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(0.9f, 0.3f, 0.25f), new Color(0.35f, 0.85f, 0.4f), frac);
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * frac, barBg.height), Texture2D.whiteTexture);
                GUI.color = prevColor;

                var hint = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(cx - 400, by + 92, 800, 22),
                    $"Auto-pass in {Mathf.CeilToInt(Mathf.Max(0f, _decisionLeft))} s", hint);
            }
            else
            {
                GUI.Label(new Rect(cx - 400, cardRect.yMax + 6, 800, 32), $"{Current.Name} is thinking...", title);
            }
        }

        // ---------- Negociación entre jugadores ----------
        // Fases: Pick (elegir rival) → Select (marcar sus cartas) → Offer (oro)
        //        → Waiting (2 s) → Decide (receptor humano) / bot → Result
        enum TradePhase { Pick, Select, Offer, Waiting, Decide, Result }
        TradePhase _tradePhase = TradePhase.Pick;
        bool _tradeOpen;
        bool _tradeUsed; // solo se permite una negociación por turno
        int _tradePartner = -1;
        int _tradeGiveMoney;
        float _tradeTimer;
        bool _tradeAccepted;
        readonly HashSet<int> _tradeGetProps = new HashSet<int>();
        Vector2 _tradeScroll;

        void ResetTrade()
        {
            _tradeOpen = false;
            _tradePhase = TradePhase.Pick;
            _tradePartner = -1;
            _tradeGiveMoney = 0;
            _tradeTimer = 0f;
            _tradeGetProps.Clear();
            Time.timeScale = 1f; // el juego se reanuda
        }

        static int TileValue(TileData t) => t.Price + t.Houses * HouseCost(t);

        void TransferTile(int idx, PlayerState from, PlayerState to)
        {
            var t = Board.Tiles[idx];
            t.OwnerIndex = _players.IndexOf(to);
            from.OwnedTiles.Remove(idx);
            to.OwnedTiles.Add(idx);
        }

        string TradeSummary()
        {
            string props = "";
            foreach (int i in _tradeGetProps)
                props += (props.Length > 0 ? ", " : "") + Board.Tiles[i].Name;
            return $"${_tradeGiveMoney} for {props}";
        }

        /// <summary>Avance temporizado de la negociación (espera de 2 s y cierre).</summary>
        void UpdateTradePhases()
        {
            if (!_tradeOpen) return;

            if (_tradePhase == TradePhase.Waiting)
            {
                // con el juego en pausa (timeScale 0) se usa tiempo sin escalar
                _tradeTimer += Time.unscaledDeltaTime;
                if (_tradeTimer < 2f) return;
                _tradeTimer = 0f;
                var partner = _players[_tradePartner];
                if (partner.IsBot)
                {
                    // el bot compara el oro ofrecido con el valor de sus cartas
                    int value = 0;
                    foreach (int i in _tradeGetProps)
                    {
                        var t = Board.Tiles[i];
                        int v = TileValue(t);
                        if (OwnsFullGroup(partner, t.ColorGroup)) v *= 2; // sus monopolios valen doble
                        value += v;
                    }
                    _tradeAccepted = _tradeGiveMoney >= value * 1.15f;
                    EnterTradeResult();
                }
                else
                {
                    _tradePhase = TradePhase.Decide; // el humano decide en pantalla
                }
            }
            else if (_tradePhase == TradePhase.Result)
            {
                _tradeTimer += Time.deltaTime;
                if (_tradeTimer >= 2.4f) ResetTrade();
            }
        }

        /// <summary>Aplica la decisión y lanza las cartas volando si hubo trato.</summary>
        void EnterTradeResult()
        {
            var partner = _players[_tradePartner];
            if (_tradeAccepted)
            {
                Current.Money -= _tradeGiveMoney;
                PlayCoinDrain(Current, _tradeGiveMoney); // el comprador suelta su oro
                partner.Money += _tradeGiveMoney;
                PlayCoinRain(partner, _tradeGiveMoney); // el vendedor cobra su oro
                float delay = 0.15f;
                foreach (int idx in _tradeGetProps)
                {
                    TransferTile(idx, partner, Current);
                    StartCoroutine(FlyCard(Board.Tiles[idx],
                        partner.Token.transform.position,
                        Current.Token.transform.position, delay));
                    delay += 0.3f;
                }
                PlayReaction(Current, true);
                _log = $"{partner.Name} accepts the deal: {TradeSummary()}.";
            }
            else
            {
                AudioManager.Play("sfx_trade_reject");
                PlayReaction(Current, false);
                _log = $"{partner.Name} rejects the offer ({TradeSummary()}).";
            }
            _tradePhase = TradePhase.Result;
            _tradeTimer = 0f;
            Time.timeScale = 1f; // reanudar: la carta vuela y el juego sigue
        }

        /// <summary>La carta viaja en 3D desde el dueño anterior hasta el comprador.</summary>
        IEnumerator FlyCard(TileData t, Vector3 from, Vector3 to, float delay)
        {
            yield return new WaitForSeconds(delay);

            var tex = GetCard(t);
            var backTex = GetTex("Cards/back_card");

            // carta de dos caras: anverso (la propiedad) y reverso (back_card)
            var go = new GameObject("TradeCardFx");
            void MakeFace(Texture2D faceTex, float yRot)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = yRot > 0f ? "Back" : "Front";
                Destroy(q.GetComponent<Collider>());
                var m = new Material(Shader.Find("Unlit/Transparent"));
                if (faceTex != null) m.mainTexture = faceTex;
                q.GetComponent<Renderer>().material = m;
                q.transform.SetParent(go.transform, false);
                q.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            }
            MakeFace(tex, 0f);
            MakeFace(backTex != null ? backTex : tex, 180f);

            const float ch = 1.7f;
            float aspect = tex != null ? (float)tex.width / tex.height : 0.7f;
            go.transform.localScale = new Vector3(ch * aspect, ch, 1f);

            const float dur = 5f; // vuelo largo y ceremonioso
            float t0 = Time.time;
            while (Time.time - t0 < dur)
            {
                float k = Mathf.Clamp01((Time.time - t0) / dur);
                // smootherstep: arranca acelerando y llega frenando con suavidad
                float e = k * k * k * (k * (6f * k - 15f) + 10f);

                Vector3 pos = Vector3.Lerp(from, to, e);
                pos.y += 1.1f + Mathf.Sin(e * Mathf.PI) * 2.2f; // arco alto por el aire
                go.transform.position = pos;

                // volteretas en los 3 ejes (vueltas completas: termina de cara a la cámara)
                var cam = Camera.main;
                Quaternion face = cam != null
                    ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up)
                    : Quaternion.identity;
                go.transform.rotation = face * Quaternion.Euler(1080f * e, 1800f * e, 720f * e);

                // pop de escala: crece en pleno vuelo
                float s = 1f + Mathf.Sin(e * Mathf.PI) * 0.35f;
                go.transform.localScale = new Vector3(ch * aspect * s, ch * s, 1f);

                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Panel de negociación por fases.</summary>
        void DrawTradeModal()
        {
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };
            title.normal.textColor = new Color(1f, 0.85f, 0.35f);

            // el resultado se muestra SIN velo, para ver la carta volar en el tablero
            if (_tradePhase == TradePhase.Result)
            {
                DrawTradeResult(title);
                return;
            }

            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            float W = Screen.width, H = Screen.height;
            float w = Mathf.Min(940f, W * 0.76f), h = Mathf.Min(620f, H * 0.84f);
            var r = new Rect(W / 2f - w / 2f, H / 2f - h / 2f, w, h);
            GUI.Box(r, "");

            switch (_tradePhase)
            {
                case TradePhase.Pick:    DrawTradePick(r, title, prev); break;
                case TradePhase.Select:  DrawTradeSelect(r, title); break;
                case TradePhase.Offer:   DrawTradeOffer(r, title); break;
                case TradePhase.Waiting: DrawTradeWaiting(r, title); break;
                case TradePhase.Decide:  DrawTradeDecide(r, title); break;
            }
        }

        void DrawTradePick(Rect r, GUIStyle title, Color prev)
        {
            GUI.Label(new Rect(r.x, r.y + 16f, r.width, 30f), "Trade with...", title);

            // solo rivales que tengan propiedades que ofrecer
            var others = new List<int>();
            for (int i = 0; i < _players.Count; i++)
                if (i != _currentIndex && !_players[i].Bankrupt
                    && _players[i].OwnedTiles.Count > 0) others.Add(i);

            const float cell = 120f;
            float x0 = r.x + r.width / 2f - (others.Count * cell) / 2f;
            var nm = new GUIStyle(GUI.skin.label)
            { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
            nm.normal.textColor = Color.white;

            for (int k = 0; k < others.Count; k++)
            {
                var p = _players[others[k]];
                var av = new Rect(x0 + k * cell + 15f, r.y + 76f, 90f, 90f);
                if (p.Avatar != null) GUI.DrawTexture(av, p.Avatar, ScaleMode.ScaleAndCrop);
                else GUI.Box(av, p.Name.Substring(0, 1));
                GUI.color = p.Color;
                GUI.DrawTexture(new Rect(av.x, av.yMax + 2f, av.width, 4f), Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(new Rect(av.x - 15f, av.yMax + 10f, cell, 20f), p.Name, nm);
                if (GUI.Button(av, GUIContent.none, GUIStyle.none))
                {
                    _tradePartner = others[k];
                    _tradeGetProps.Clear();
                    _tradePhase = TradePhase.Select;
                }
            }

            if (GUI.Button(new Rect(r.x + r.width / 2f - 110f, r.yMax - 72f, 220f, 54f),
                    "Cancel", UIFonts.OrnateButton))
                ResetTrade();
        }

        void DrawTradeSelect(Rect r, GUIStyle title)
        {
            var partner = _players[_tradePartner];
            GUI.Label(new Rect(r.x, r.y + 14f, r.width, 30f),
                $"{partner.Name}'s properties — pick what you want", title);

            var props = new List<int>(partner.OwnedTiles);
            props.Sort();

            if (props.Count == 0)
            {
                var empty = new GUIStyle(GUI.skin.label)
                { fontSize = 17, alignment = TextAnchor.MiddleCenter };
                empty.normal.textColor = Color.white;
                GUI.Label(new Rect(r.x, r.y + r.height / 2f - 20f, r.width, 40f),
                    $"{partner.Name} owns no properties.", empty);
            }
            else
            {
                // galería de cartas: clic para marcar/desmarcar, con su valor debajo
                const float cardW = 118f, cardH = 172f, gap = 14f, priceH = 24f;
                float pitchY = cardH + priceH + gap;
                int perRow = Mathf.Max(1, (int)((r.width - 60f) / (cardW + gap)));
                int rows = Mathf.CeilToInt(props.Count / (float)perRow);
                var view = new Rect(0, 0, r.width - 60f, rows * pitchY + 8f);
                var price = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
                };
                price.normal.textColor = new Color(1f, 0.85f, 0.35f);
                _tradeScroll = GUI.BeginScrollView(
                    new Rect(r.x + 20f, r.y + 56f, r.width - 40f, r.height - 140f), _tradeScroll, view);
                for (int i = 0; i < props.Count; i++)
                {
                    var t = Board.Tiles[props[i]];
                    float cx = (i % perRow) * (cardW + gap) + 6f;
                    float cy = (i / perRow) * pitchY + 6f;
                    var cr = new Rect(cx, cy, cardW, cardH);
                    bool sel = _tradeGetProps.Contains(props[i]);

                    if (sel) // marco dorado para las elegidas
                    {
                        GUI.color = new Color(1f, 0.82f, 0.30f, 0.95f);
                        GUI.DrawTexture(new Rect(cr.x - 4f, cr.y - 4f, cr.width + 8f, cr.height + 8f),
                            Texture2D.whiteTexture);
                        GUI.color = Color.white;
                    }
                    var tex = GetCard(t);
                    if (tex != null) GUI.DrawTexture(cr, tex, ScaleMode.ScaleToFit);
                    else GUI.Box(cr, t.Name);

                    // valor de la propiedad (precio + casas)
                    GUI.Label(new Rect(cr.x, cr.yMax + 2f, cardW, priceH), $"${TileValue(t)}", price);

                    if (GUI.Button(cr, GUIContent.none, GUIStyle.none))
                    {
                        if (sel) _tradeGetProps.Remove(props[i]);
                        else _tradeGetProps.Add(props[i]);
                    }
                }
                GUI.EndScrollView();
            }

            GUI.enabled = _tradeGetProps.Count > 0;
            if (GUI.Button(new Rect(r.x + r.width / 2f - 235f, r.yMax - 72f, 225f, 54f),
                    "Accept", UIFonts.OrnateButton))
                _tradePhase = TradePhase.Offer;
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + r.width / 2f + 10f, r.yMax - 72f, 225f, 54f),
                    "Cancel", UIFonts.OrnateButton))
                ResetTrade();
        }

        void DrawTradeOffer(Rect r, GUIStyle title)
        {
            var partner = _players[_tradePartner];
            GUI.Label(new Rect(r.x, r.y + 14f, r.width, 30f),
                $"Your offer to {partner.Name}", title);

            // cartas elegidas, ya bloqueadas, con su valor debajo
            var chosen = new List<int>(_tradeGetProps);
            chosen.Sort();
            const float cardW = 96f, cardH = 140f, gap = 12f;
            float x0 = r.x + r.width / 2f - (chosen.Count * (cardW + gap) - gap) / 2f;
            var price = new GUIStyle(GUI.skin.label)
            { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
            price.normal.textColor = new Color(1f, 0.85f, 0.35f);
            for (int i = 0; i < chosen.Count; i++)
            {
                var cr = new Rect(x0 + i * (cardW + gap), r.y + 60f, cardW, cardH);
                var t = Board.Tiles[chosen[i]];
                var tex = GetCard(t);
                if (tex != null) GUI.DrawTexture(cr, tex, ScaleMode.ScaleToFit);
                else GUI.Box(cr, t.Name);
                GUI.Label(new Rect(cr.x, cr.yMax + 2f, cardW, 20f), $"${TileValue(t)}", price);
            }

            // oro: campo de texto + pasos de 50
            var lbl = new GUIStyle(GUI.skin.label)
            { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
            lbl.normal.textColor = new Color(1f, 0.85f, 0.35f);
            float my = r.y + 60f + cardH + 46f;
            GUI.Label(new Rect(r.x, my, r.width, 24f),
                $"Gold offered (you have ${Current.Money})", lbl);

            float mx = r.x + r.width / 2f;
            if (GUI.Button(new Rect(mx - 152f, my + 32f, 46f, 36f), "-"))
                _tradeGiveMoney = Mathf.Max(0, _tradeGiveMoney - 50);
            if (GUI.Button(new Rect(mx + 106f, my + 32f, 46f, 36f), "+"))
                _tradeGiveMoney = Mathf.Min(Current.Money, _tradeGiveMoney + 50);

            var tf = new GUIStyle(GUI.skin.textField)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            string typed = GUI.TextField(new Rect(mx - 100f, my + 32f, 200f, 36f),
                _tradeGiveMoney.ToString(), 6, tf);
            {
                string digits = "";
                foreach (char c in typed) if (char.IsDigit(c)) digits += c;
                int v; int.TryParse(digits, out v);
                _tradeGiveMoney = Mathf.Clamp(v, 0, Current.Money);
            }

            GUI.enabled = _tradeGiveMoney > 0;
            if (GUI.Button(new Rect(r.x + r.width / 2f - 235f, r.yMax - 72f, 225f, 54f),
                    "Send offer", UIFonts.OrnateButton))
            {
                _tradeUsed = true; // la oferta consume la negociación del turno
                AudioManager.Play("sfx_trade_offer");
                _tradePhase = TradePhase.Waiting;
                _tradeTimer = 0f;
                _log = $"{Current.Name} offers {partner.Name}: {TradeSummary()}...";
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + r.width / 2f + 10f, r.yMax - 72f, 225f, 54f),
                    "Back", UIFonts.OrnateButton))
                _tradePhase = TradePhase.Select;
        }

        void DrawTradeWaiting(Rect r, GUIStyle title)
        {
            var partner = _players[_tradePartner];
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
            GUI.Label(new Rect(r.x, r.y + r.height / 2f - 20f, r.width, 40f),
                $"{partner.Name} is considering the offer{dots}", title);
        }

        void DrawTradeDecide(Rect r, GUIStyle title)
        {
            var partner = _players[_tradePartner];
            GUI.Label(new Rect(r.x, r.y + 16f, r.width, 30f),
                $"{partner.Name}: {Current.Name} offers you", title);

            var chosen = new List<int>(_tradeGetProps);
            chosen.Sort();
            const float cardW = 96f, cardH = 140f, gap = 12f;
            float x0 = r.x + r.width / 2f - (chosen.Count * (cardW + gap) - gap) / 2f;
            var price = new GUIStyle(GUI.skin.label)
            { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
            price.normal.textColor = new Color(1f, 0.85f, 0.35f);
            for (int i = 0; i < chosen.Count; i++)
            {
                var cr = new Rect(x0 + i * (cardW + gap), r.y + 64f, cardW, cardH);
                var t = Board.Tiles[chosen[i]];
                var tex = GetCard(t);
                if (tex != null) GUI.DrawTexture(cr, tex, ScaleMode.ScaleToFit);
                else GUI.Box(cr, t.Name);
                GUI.Label(new Rect(cr.x, cr.yMax + 2f, cardW, 20f), $"${TileValue(t)}", price);
            }

            var body = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
            body.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x, r.y + 64f + cardH + 34f, r.width, 28f),
                $"${_tradeGiveMoney} in gold for these properties", body);

            if (GUI.Button(new Rect(r.x + r.width / 2f - 235f, r.yMax - 72f, 225f, 54f),
                    "Accept", UIFonts.OrnateButton))
            {
                _tradeAccepted = true;
                EnterTradeResult();
            }
            if (GUI.Button(new Rect(r.x + r.width / 2f + 10f, r.yMax - 72f, 225f, 54f),
                    "Reject", UIFonts.OrnateButton))
            {
                _tradeAccepted = false;
                EnterTradeResult();
            }
        }

        /// <summary>Decisión en grande sobre el tablero, sin velo (se ve la carta volar).</summary>
        void DrawTradeResult(GUIStyle title)
        {
            var partner = _players[_tradePartner];
            string msg = _tradeAccepted
                ? $"{partner.Name} ACCEPTS the deal!"
                : $"{partner.Name} REJECTS the offer.";
            var st = new GUIStyle(title) { fontSize = 34 };
            OutlinedLabel(new Rect(0, Screen.height * 0.14f, Screen.width, 50f), msg, st,
                _tradeAccepted ? new Color(0.6f, 1f, 0.55f) : new Color(1f, 0.5f, 0.45f),
                new Color(0.1f, 0.05f, 0.01f), 3f);
        }

        bool _buildMenuOpen; // selector de propiedad para construir casa

        /// <summary>Menú flotante con las propiedades donde se puede construir.</summary>
        void DrawBuildMenu(List<int> buildable)
        {
            const float w = 400f, rowH = 36f;
            float h = 46f + buildable.Count * rowH;
            var r = new Rect(Screen.width / 2f - w / 2f, Screen.height - 136f - h, w, h);
            GUI.Box(r, "");

            var title = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                fontSize = 16, font = UIFonts.Title
            };
            title.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, 24f), "Build a house on...", title);

            for (int i = 0; i < buildable.Count; i++)
            {
                var t = Board.Tiles[buildable[i]];
                var row = new Rect(r.x + 12f, r.y + 38f + i * rowH, r.width - 24f, rowH - 6f);
                if (GUI.Button(row, $"{t.Name}  ·  ${HouseCost(t)}  ·  houses {t.Houses}/4"))
                {
                    BuildHouseAt(buildable[i]);
                    _buildMenuOpen = false;
                }
            }
        }

        /// <summary>Texto informativo: mismo color en TODOS los estados (sin hover).</summary>
        static void StaticColor(GUIStyle st, Color c)
        {
            st.normal.textColor = c;
            st.hover.textColor = c;
            st.active.textColor = c;
            st.focused.textColor = c;
            st.hover.background = null;
            st.active.background = null;
        }

        /// <summary>
        /// Icono superpuesto a un botón: acompaña el estado del botón encogiéndose
        /// ligeramente cuando el mouse está encima o pulsando.
        /// </summary>
        void DrawButtonIcon(Rect btn, Texture2D ico, float size)
        {
            if (ico == null) return;
            var e = Event.current;
            bool over = btn.Contains(e.mousePosition);
            bool pressed = over && e.type != EventType.MouseUp && Input.GetMouseButton(0);
            float s = pressed ? size * 0.82f : over ? size * 0.90f : size;
            float cx = btn.x + 16f + size / 2f;
            float cy = btn.y + btn.height / 2f;
            GUI.DrawTexture(new Rect(cx - s / 2f, cy - s / 2f, s, s), ico, ScaleMode.ScaleToFit);
        }

        /// <summary>
        /// HUD principal (esquina superior izquierda): placa del jugador en turno
        /// o del jugador fijado con los avatares. Debajo, fila de avatares-botón
        /// para inspeccionar el HUD y las cartas de cada jugador. Devuelve la Y
        /// donde termina, para colocar el panel de propiedades debajo.
        /// </summary>
        float DrawHud()
        {
            if (_players.Count == 0) return 10f;

            // si el fijado quebró o no existe, volver a seguir el turno
            if (_selectedPlayer >= 0 &&
                (_selectedPlayer >= _players.Count || _players[_selectedPlayer].Bankrupt))
                _selectedPlayer = -1;

            bool followMode = _selectedPlayer < 0;
            int shownIdx = followMode ? _currentIndex : _selectedPlayer;
            var p = _players[shownIdx];

            var hudTex = GetTex("UI/hud_player");
            if (hudTex == null)
            {
                // respaldo mínimo si falta el arte
                GUI.Box(new Rect(10, 10, 460, 36),
                    $"{p.Name} — ${p.Money} · {p.OwnedTiles.Count} prop.");
                return 46f;
            }

            float texAsp = (float)hudTex.width / hudTex.height;
            const float pw2 = 340f;
            float mainH = pw2 / texAsp;
            var plate = new Rect(10f, 10f, pw2, mainH);
            DrawPlayerPlate(plate, p, shownIdx, hudTex);

            // ---- fila de avatares: clic para fijar el HUD de ese jugador ----
            Color prev = GUI.color;
            const float av = 46f, agap = 6f;
            float ay = 10f + mainH + 6f;
            float ax = 10f;

            // botón "seguir turno" (dado): el HUD vuelve a cambiar con el turno
            var turnRect = new Rect(ax, ay, av, av);
            if (followMode)
            {
                GUI.color = new Color(1f, 0.82f, 0.30f, 0.95f);
                GUI.DrawTexture(new Rect(turnRect.x - 3f, turnRect.y - 3f, av + 6f, av + 6f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            GUI.color = new Color(0.12f, 0.09f, 0.05f, 0.92f);
            GUI.DrawTexture(turnRect, Texture2D.whiteTexture);
            GUI.color = prev;
            var diceIco = GetTex("UI/Icons/icon_dice");
            if (diceIco != null)
                GUI.DrawTexture(new Rect(turnRect.x + 7f, turnRect.y + 7f, av - 14f, av - 14f), diceIco, ScaleMode.ScaleToFit);
            if (GUI.Button(turnRect, GUIContent.none, GUIStyle.none))
                _selectedPlayer = -1;
            ax += av + agap;

            for (int i = 0; i < _players.Count; i++)
            {
                var pl = _players[i];
                if (pl.Bankrupt) continue;

                var r = new Rect(ax, ay, av, av);

                // marco: dorado si está fijado; tenue sobre el jugador de turno
                if (_selectedPlayer == i)
                {
                    GUI.color = new Color(1f, 0.82f, 0.30f, 0.95f);
                    GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, av + 6f, av + 6f), Texture2D.whiteTexture);
                    GUI.color = prev;
                }
                else if (i == _currentIndex)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, av + 4f, av + 4f), Texture2D.whiteTexture);
                    GUI.color = prev;
                }

                if (pl.Avatar != null) GUI.DrawTexture(r, pl.Avatar, ScaleMode.ScaleAndCrop);
                else GUI.Box(r, pl.Name.Substring(0, 1));

                // cinta con el color del jugador
                GUI.color = pl.Color;
                GUI.DrawTexture(new Rect(r.x, r.yMax + 1f, av, 4f), Texture2D.whiteTexture);
                GUI.color = prev;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    _selectedPlayer = i;

                ax += av + agap;
            }

            float bottom = ay + av + 10f;

            // toggle 2D/3D del rey, en vivo (si hay un rey en la partida)
            bool anyRey = false;
            foreach (var pl2 in _players)
                if (!pl2.Bankrupt && pl2.CharacterId == "rey") { anyRey = true; break; }
            if (anyRey)
            {
                bool is3D = TokenStyle != null && TokenStyle.rey3D;
                if (GUI.Button(new Rect(10f, bottom, 150f, 28f), $"King token: {(is3D ? "3D" : "2D")}"))
                    ToggleRey3D();
                bottom += 34f;
            }

            return bottom; // Y donde termina el HUD (placa + avatares + toggle)
        }

        TokenStyleSettings _tokenStyle;
        bool _tokenStyleSearched;

        TokenStyleSettings TokenStyle
        {
            get
            {
                if (!_tokenStyleSearched)
                {
                    _tokenStyleSearched = true;
                    _tokenStyle = FindAnyObjectByType<TokenStyleSettings>();
                }
                return _tokenStyle;
            }
        }

        /// <summary>Alterna la ficha del rey entre sprite 2D y modelo 3D en caliente.</summary>
        void ToggleRey3D()
        {
            var style = TokenStyle;
            if (style == null)
            {
                var host = GameObject.Find("Bootstrap") ?? new GameObject("TokenStyleSettings");
                style = host.AddComponent<TokenStyleSettings>();
                style.rey3D = false;
                _tokenStyle = style;
            }
            style.rey3D = !style.rey3D;

            // reconstruir las fichas del rey conservando posición y cámara
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p.CharacterId != "rey" || p.Bankrupt || p.Token == null) continue;
                bool wasFollow = Cam != null && Cam.Mode == CameraMode.Follow
                                 && Cam.FollowTarget == p.Token.transform;
                Destroy(p.Token);
                p.Token = CreateToken(p, i, _players.Count);
                RepositionTile(p.Position);
                if (wasFollow) Cam.SetMode(CameraMode.Follow, p.Token.transform);
            }
            _log = $"King token: {(style.rey3D ? "3D model" : "2D sprite")}.";
        }

        /// <summary>Placa individual del HUD (arte hud_player) con los datos del jugador.</summary>
        void DrawPlayerPlate(Rect plate, PlayerState p, int index, Texture2D hudTex)
        {
            Color prevBg = GUI.color;
            float mainH = plate.height;

            // en bancarrota la placa entera se apaga a gris
            Color baseTint = p.Bankrupt ? new Color(0.42f, 0.42f, 0.42f, 0.92f) : prevBg;
            GUI.color = baseTint;

            // las zonas azules del arte toman el color asignado al jugador
            hudTex = CharacterToken.TintedPlate(hudTex, p.Color);

            int houses = 0;
            foreach (int idx in p.OwnedTiles) houses += Board.Tiles[idx].Houses;

            // brillo dorado detrás de la placa del jugador en turno
            if (p == Current && _state != GameState.GameOver && !p.Bankrupt)
            {
                GUI.color = new Color(1f, 0.82f, 0.30f, 0.45f);
                GUI.DrawTexture(new Rect(plate.x - 4f, plate.y - 4f, plate.width + 8f, plate.height + 8f), Texture2D.whiteTexture);
                GUI.color = baseTint;
            }

            GUI.DrawTexture(plate, hudTex, ScaleMode.ScaleToFit);

            Rect F(float fx, float fy, float fw, float fh) => new Rect(
                plate.x + plate.width * fx, plate.y + plate.height * fy,
                plate.width * fw, plate.height * fh);

            // avatar en el marco izquierdo
            var av = F(0.055f, 0.115f, 0.148f, 0.75f);
            if (p.Avatar != null)
            {
                GUI.DrawTexture(av, p.Avatar, ScaleMode.ScaleAndCrop);
            }
            else
            {
                var initial = new GUIStyle(GUI.skin.label)
                { fontSize = (int)(mainH * 0.34f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
                initial.normal.textColor = Color.white;
                GUI.Label(av, p.Name.Substring(0, 1).ToUpper(), initial);
            }
            // cinta con el color del jugador bajo el avatar
            GUI.color = p.Bankrupt ? new Color(0.5f, 0.5f, 0.5f) : p.Color;
            GUI.DrawTexture(new Rect(av.x, av.yMax + 2f, av.width, 4f), Texture2D.whiteTexture);
            GUI.color = baseTint;

            // nombre en el banner azul superior
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(mainH * 0.17f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                wordWrap = false, clipping = TextClipping.Overflow
            };
            nameStyle.normal.textColor = new Color(0.96f, 0.90f, 0.72f);
            string turnMark = p == Current && _state != GameState.GameOver ? "  ►" : "";
            GUI.Label(F(0.24f, 0.045f, 0.58f, 0.23f),
                p.Name + (p.IsBot ? "  [BOT]" : "") + turnMark, nameStyle);

            // valores: oro, propiedades, casas
            var val = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(mainH * 0.15f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                wordWrap = false, clipping = TextClipping.Overflow
            };
            val.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(F(0.318f, 0.375f, 0.150f, 0.165f), $"${p.Money}", val);
            GUI.Label(F(0.580f, 0.375f, 0.110f, 0.165f), p.OwnedTiles.Count.ToString(), val);
            GUI.Label(F(0.833f, 0.375f, 0.145f, 0.165f), houses.ToString(), val);

            // estado (cárcel) y situación (turno)
            var small = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(mainH * 0.115f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                wordWrap = false, clipping = TextClipping.Overflow
            };
            small.normal.textColor = Color.white;
            string estado = p.RestTurns > 0 ? $"In jail ({p.RestTurns})" : "Free";
            string situ = p.Bankrupt ? "Bankrupt"
                : (p == Current && _state != GameState.GameOver ? "Playing" : "Waiting");
            GUI.Label(F(0.392f, 0.760f, 0.163f, 0.14f), estado, small);
            GUI.Label(F(0.760f, 0.760f, 0.163f, 0.14f), situ, small);

            // clic en la placa: inspeccionar las propiedades de ese jugador
            if (GUI.Button(plate, GUIContent.none, GUIStyle.none))
                _selectedPlayer = index;

            GUI.color = prevBg;
        }

        void DrawPropertiesPanel(float y)
        {
            var p = _selectedPlayer >= 0 && _selectedPlayer < _players.Count
                ? _players[_selectedPlayer]
                : Current;

            GUILayout.BeginArea(new Rect(10, y, 300, 330), GUI.skin.box);

            var bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            string tag = _selectedPlayer >= 0 ? "" : " (turn)";
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{p.Name}{tag} — ${p.Money}", bold);
            GUILayout.FlexibleSpace();
            if (_selectedPlayer >= 0 && GUILayout.Button("◄ Turn", GUILayout.Width(100), GUILayout.Height(30)))
                _selectedPlayer = -1;
            GUILayout.EndHorizontal();

            if (p.OwnedTiles.Count == 0)
            {
                GUILayout.Label("No properties yet.");
            }
            else
            {
                _propScroll = GUILayout.BeginScrollView(_propScroll);

                var sorted = new List<int>(p.OwnedTiles);
                sorted.Sort((a, b) => Board.Tiles[a].ColorGroup.CompareTo(Board.Tiles[b].ColorGroup));

                // Galería: miniaturas de las cartas compradas con su renta debajo
                const int perRow = 4;
                const float cardW = 60f;
                const float cardH = 88f;

                var rentStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                };
                rentStyle.normal.textColor = new Color(1f, 0.83f, 0.28f);

                for (int i = 0; i < sorted.Count; i += perRow)
                {
                    GUILayout.BeginHorizontal();
                    for (int j = i; j < Mathf.Min(i + perRow, sorted.Count); j++)
                    {
                        var t = Board.Tiles[sorted[j]];
                        GUILayout.BeginVertical(GUILayout.Width(cardW + 6f));

                        Rect r = GUILayoutUtility.GetRect(cardW, cardH, GUILayout.Width(cardW));
                        var tex = GetCard(t);
                        if (tex != null)
                        {
                            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                            // clic en la miniatura para verla en grande
                            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                                _cardOverlay = t;
                        }
                        else
                        {
                            GUI.Box(r, t.Name);
                        }

                        string houses = t.Houses > 0 ? $" +{t.Houses}🏠" : "";
                        GUILayout.Label(ShortRent(p, t) + houses, rentStyle, GUILayout.Width(cardW));
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndArea();
        }

        /// <summary>Renta actual en formato corto para la galería (lo que cobra la carta).</summary>
        string ShortRent(PlayerState owner, TileData t)
        {
            switch (t.Type)
            {
                case TileType.Utility:
                    return "dice×8";
                case TileType.Studio:
                {
                    int studios = 0;
                    foreach (int idx in owner.OwnedTiles)
                        if (Board.Tiles[idx].Type == TileType.Studio) studios++;
                    return $"${t.BaseRent * studios}";
                }
                default:
                {
                    bool full = OwnsFullGroup(owner, t.ColorGroup);
                    return $"${(full ? t.BaseRent * 2 : t.BaseRent) * (1 + t.Houses)}";
                }
            }
        }

        /// <summary>
        /// Modal de carta de Suerte/Comunidad: arte de la carta (o reverso genérico),
        /// texto del efecto y botón Continuar (los bots continúan solos).
        /// </summary>
        void DrawEventCardModal()
        {
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prevColor;

            float cx = Screen.width / 2f;
            float top = Screen.height * 0.07f;
            string deckName = _drawnIsChance ? "CHANCE" : "COMMUNITY CHEST";

            // arte específico de la carta o, si no existe, el reverso del mazo
            var tex = GetTex("Cards/Events/event_" + _drawnCard.Id);
            bool specificArt = tex != null; // el arte ya trae el texto dibujado
            if (tex == null)
                tex = GetTex(_drawnIsChance ? "Cards/Events/event_chance_back" : "Cards/Events/event_community_back");

            Rect cardRect;
            if (tex != null)
            {
                float h = Screen.height * (specificArt ? 0.62f : 0.48f);
                float w = h * ((float)tex.width / tex.height);
                cardRect = new Rect(cx - w / 2f, top, w, h);
                GUI.DrawTexture(cardRect, tex, ScaleMode.ScaleToFit);
            }
            else
            {
                // placeholder mientras llega el arte
                float w = Mathf.Min(460f, Screen.width * 0.5f);
                cardRect = new Rect(cx - w / 2f, top + 40f, w, 190f);
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = _drawnIsChance
                    ? new Color(0.95f, 0.55f, 0.10f)   // naranja Suerte
                    : new Color(0.30f, 0.55f, 0.95f);  // azul Comunidad
                GUI.Box(cardRect, "");
                GUI.backgroundColor = prev;
            }

            float actionY;
            if (specificArt)
            {
                // el arte ya trae título y texto: solo la acción debajo
                actionY = cardRect.yMax + 14f;
            }
            else
            {
                var title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                };
                GUI.Label(new Rect(cx - 300f, cardRect.yMax + 8f, 600f, 32f), deckName, title);

                var body = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true
                };
                GUI.Label(new Rect(cx - 300f, cardRect.yMax + 44f, 600f, 64f), _drawnCard.Text, body);
                actionY = cardRect.yMax + 114f;
            }

            if (!Current.IsBot)
            {
                if (GUI.Button(new Rect(cx - 100f, actionY, 200f, 48f), "Continue"))
                    ResolveDrawnCard();
            }
            else
            {
                var hint = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(cx - 200f, actionY + 2f, 400f, 24f), $"{Current.Name} is reading the card...", hint);
            }
        }

        // --- Pantalla de victoria (estilo Smash Bros) ---

        readonly Dictionary<string, Texture2D[]> _uiAnimCache = new Dictionary<string, Texture2D[]>();

        Texture2D[] GetUIAnim(string name)
        {
            if (!_uiAnimCache.TryGetValue(name, out var frames))
            {
                frames = new Texture2D[8];
                for (int i = 0; i < 8; i++)
                    frames[i] = Resources.Load<Texture2D>($"Anims/rey_{name}_{i}");
                _uiAnimCache[name] = frames;
            }
            return frames;
        }

        /// <summary>Texto con contorno (relleno + 4 sombras desplazadas).</summary>
        static void OutlinedLabel(Rect r, string text, GUIStyle style, Color fill, Color outline, float o = 3f)
        {
            Color prev = style.normal.textColor;
            void SetColor(Color c)
            {
                // texto estático: el mismo color en todos los estados del mouse
                style.normal.textColor = c;
                style.hover.textColor = c;
                style.active.textColor = c;
                style.focused.textColor = c;
            }
            SetColor(outline);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, style);
            GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), text, style);
            SetColor(fill);
            GUI.Label(r, text, style);
            SetColor(prev);
        }

        Texture2D _victoryBg;

        bool _victorySfxPlayed;

        void DrawVictoryScreen()
        {
            GUI.enabled = true;

            // fanfarria + multitud celebrando (una sola vez al entrar)
            if (!_victorySfxPlayed)
            {
                _victorySfxPlayed = true;
                AudioManager.Play("sfx_victory");
                AudioManager.Play("sfx_crowd", 0.8f);
            }

            float W = Screen.width, H = Screen.height;
            Color prevColor = GUI.color;

            // Escenario aleatorio de fondo (Resources/Backgrounds)
            if (_victoryBg == null)
            {
                var bgs = Resources.LoadAll<Texture2D>("Backgrounds");
                if (bgs != null && bgs.Length > 0)
                    _victoryBg = bgs[Random.Range(0, bgs.Length)];
            }

            if (_victoryBg != null)
            {
                // aspecto cover: llenar la pantalla sin deformar
                float texAspect = (float)_victoryBg.width / _victoryBg.height;
                float scrAspect = W / H;
                Rect bgRect = texAspect > scrAspect
                    ? new Rect((W - H * texAspect) / 2f, 0f, H * texAspect, H)
                    : new Rect(0f, (H - W / texAspect) / 2f, W, W / texAspect);
                GUI.DrawTexture(bgRect, _victoryBg, ScaleMode.ScaleToFit);

                // velos para legibilidad
                GUI.color = new Color(0f, 0f, 0f, 0.28f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(0, H * 0.64f, W, H * 0.36f), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }
            else
            {
                // respaldo: degradado cálido
                GUI.color = new Color(0.35f, 0.22f, 0.12f, 0.93f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, H * 0.62f, W, H * 0.38f), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }

            // Ganador: arte del 1er puesto (marco + insignia + personaje saliendo del marco)
            var winnerTex = GetTex($"UI/rank_{_winner.CharacterId}_1");
            var panelTex = GetTex("UI/Winner_panel"); // placa para el nombre
            if (winnerTex != null)
            {
                float h = H * 0.55f;
                float w = h * ((float)winnerTex.width / winnerTex.height);
                var poseRect = new Rect(W * 0.70f - w / 2f, H * 0.03f, w, h);
                GUI.DrawTexture(poseRect, winnerTex, ScaleMode.ScaleToFit);

                if (panelTex != null)
                {
                    // placa debajo del marco, un poco más pequeña que el recuadro
                    float pw2 = Mathf.Min(w * 0.92f, W * 0.28f);
                    float ph2 = pw2 * ((float)panelTex.height / panelTex.width);
                    var plate = new Rect(poseRect.center.x - pw2 / 2f, poseRect.yMax - ph2 * 0.10f, pw2, ph2);
                    GUI.DrawTexture(plate, panelTex, ScaleMode.ScaleToFit);

                    // nombre estático, centrado en la franja azul (su centro real
                    // está al 57.6% de la altura por la cresta superior del arte)
                    var plateName = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = (int)(ph2 * 0.30f), fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                        wordWrap = false, clipping = TextClipping.Overflow
                    };
                    // texto puramente informativo: mismo color en todos los estados
                    plateName.hover.textColor = plateName.normal.textColor;
                    plateName.active.textColor = plateName.normal.textColor;
                    plateName.hover.background = null;
                    plateName.active.background = null;
                    var bandRect = new Rect(plate.x, plate.y + ph2 * 0.226f, plate.width, ph2 * 0.70f);
                    OutlinedLabel(bandRect, _winner.Name.ToUpper(), plateName,
                        new Color(1f, 0.85f, 0.3f), new Color(0.1f, 0.05f, 0.01f), 2.5f);
                }
            }
            else
            {
                // respaldo: bloque con su inicial y color
                var box = new Rect(W * 0.55f, H * 0.10f, W * 0.22f, H * 0.5f);
                GUI.color = _winner.Color;
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = prevColor;
                var init = new GUIStyle(GUI.skin.label) { fontSize = 160, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                OutlinedLabel(box, _winner.Name.Substring(0, 1).ToUpper(), init, Color.white, Color.black, 4f);
            }


            // Insignia "1": solo si el arte del ganador no está (el rank_1 ya la trae)
            if (winnerTex == null)
            {
                var badgeTex = GetTex("UI/frame_badge1");
                if (badgeTex != null)
                {
                    float bh = H * 0.30f;
                    float bw = bh * ((float)badgeTex.width / badgeTex.height);
                    GUI.DrawTexture(new Rect(W * 0.155f, H * 0.03f, bw, bh), badgeTex, ScaleMode.ScaleToFit);
                }
                else
                {
                    var one = new GUIStyle(GUI.skin.label) { fontSize = (int)(H * 0.19f), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, font = UIFonts.Title };
                    OutlinedLabel(new Rect(W * 0.16f, H * 0.02f, 300f, H * 0.25f), "1", one, new Color(1f, 0.86f, 0.1f), Color.black, 5f);
                }
            }

            // Panel de resultados (pergamino): ganador en la cabecera, ranking en las filas
            if (_resultsPanelClean == null)
            {
                var raw = GetTex("UI/results_panel");
                if (raw != null) _resultsPanelClean = StripBackground(raw);
            }
            var resultsTex = _resultsPanelClean;
            float btnX = W - 240f, btnY = 20f; // respaldo: arriba a la derecha
            if (resultsTex != null)
            {
                // pergamino grande, con todo el contenido DENTRO del marco
                float texAsp = (float)resultsTex.width / resultsTex.height;
                float rw = W * 0.42f;
                float rh = rw / texAsp;
                if (rh > H * 0.62f) { rh = H * 0.62f; rw = rh * texAsp; }
                var rp = new Rect(W * 0.23f - rw / 2f, H * 0.025f, rw, rh);
                GUI.DrawTexture(rp, resultsTex, ScaleMode.ScaleToFit);

                Rect F(float fx, float fy, float fw, float fh) => new Rect(
                    rp.x + rw * fx, rp.y + rh * fy, rw * fw, rh * fh);

                Color ink = new Color(0.24f, 0.15f, 0.05f);      // tinta sobre pergamino
                Color inkGold = new Color(0.45f, 0.27f, 0.02f);  // oro oscuro, legible
                Color inkSoft = new Color(0.42f, 0.30f, 0.14f);

                // cabecera, bajo el escudo del marco
                var head = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(rh * 0.078f), fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, font = UIFonts.Title,
                    wordWrap = false, clipping = TextClipping.Overflow
                };
                StaticColor(head, inkGold);
                GUI.Label(F(0.20f, 0.185f, 0.60f, 0.10f), "RESULTS", head);

                // ranking en columnas con iconos: nombre | oro | propiedades | casas
                var ranked = new List<PlayerState> { _winner };
                var rest = new List<PlayerState>();
                foreach (var p in _players) if (p != _winner) rest.Add(p);
                rest.Sort((a, b) => b.EliminationOrder.CompareTo(a.EliminationOrder));
                ranked.AddRange(rest);

                var coinI = GetTex("UI/Icons/icon_coins");
                var castI = GetTex("UI/Icons/icon_castle");
                var housI = GetTex("UI/Icons/icon_house");

                var rowL = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                    font = UIFonts.Title, wordWrap = false, clipping = TextClipping.Overflow
                };
                for (int i = 0; i < ranked.Count && i < 4; i++)
                {
                    var p = ranked[i];
                    int ph2c = 0;
                    foreach (int idx in p.OwnedTiles) ph2c += Board.Tiles[idx].Houses;

                    float fy = 0.305f + i * 0.0925f;
                    StaticColor(rowL, i == 0 ? inkGold : ink);
                    rowL.fontSize = (int)(rh * (i == 0 ? 0.053f : 0.044f));

                    float ics = rh * 0.062f; // tamaño de icono
                    float icy = rp.y + rh * (fy + 0.0425f) - ics / 2f;

                    // corona junto al ganador para que resalte
                    if (i == 0)
                    {
                        var crownI = GetTex("UI/Icons/icon_crown");
                        if (crownI != null)
                            GUI.DrawTexture(new Rect(rp.x + rw * 0.118f, icy, ics, ics), crownI, ScaleMode.ScaleToFit);
                    }
                    string ord = i == 0 ? "1st" : i == 1 ? "2nd" : i == 2 ? "3rd" : "4th";
                    GUI.Label(F(0.178f, fy, 0.26f, 0.085f), $"{ord}  {p.Name}", rowL);

                    if (p.Bankrupt)
                    {
                        GUI.Label(F(0.46f, fy, 0.40f, 0.085f), "bankrupt", rowL);
                    }
                    else
                    {
                        // iconos con aire respecto a los valores
                        if (coinI != null) GUI.DrawTexture(new Rect(rp.x + rw * 0.435f, icy, ics, ics), coinI, ScaleMode.ScaleToFit);
                        GUI.Label(F(0.505f, fy, 0.115f, 0.085f), $"${p.Money}", rowL);
                        if (castI != null) GUI.DrawTexture(new Rect(rp.x + rw * 0.625f, icy, ics, ics), castI, ScaleMode.ScaleToFit);
                        GUI.Label(F(0.695f, fy, 0.05f, 0.085f), p.OwnedTiles.Count.ToString(), rowL);
                        if (housI != null) GUI.DrawTexture(new Rect(rp.x + rw * 0.755f, icy, ics, ics), housI, ScaleMode.ScaleToFit);
                        GUI.Label(F(0.825f, fy, 0.05f, 0.085f), ph2c.ToString(), rowL);
                    }
                }

                // resumen con iconografía, al pie
                float segs = _gameDuration > 0f ? _gameDuration : Mathf.Max(0f, Time.time - _gameStartTime);
                string tiempo = $"{(int)(segs / 60f)}:{(int)(segs % 60f):00}";
                var foot = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(rh * 0.045f), fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft, font = UIFonts.Title,
                    wordWrap = false, clipping = TextClipping.Overflow
                };
                StaticColor(foot, inkSoft);

                var diceI = GetTex("UI/Icons/icon_dice");
                var buildI = GetTex("UI/Icons/icon_build");
                float fics = rh * 0.058f;
                if (diceI != null) GUI.DrawTexture(new Rect(rp.x + rw * 0.155f, rp.y + rh * 0.742f, fics, fics), diceI, ScaleMode.ScaleToFit);
                GUI.Label(F(0.225f, 0.732f, 0.62f, 0.078f), $"Turns: {_turnCount}   ·   Time: {tiempo}", foot);
                if (buildI != null) GUI.DrawTexture(new Rect(rp.x + rw * 0.155f, rp.y + rh * 0.818f, fics, fics), buildI, ScaleMode.ScaleToFit);
                GUI.Label(F(0.225f, 0.808f, 0.62f, 0.078f), $"Houses built: {_housesBuilt}", foot);

                // botones grandes apilados bajo el pergamino
                btnX = rp.center.x - 130f;
                btnY = rp.yMax + 8f;
            }
            else
            {
                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = (int)(H * 0.11f), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, font = UIFonts.Title };
                OutlinedLabel(new Rect(W * 0.045f, H * 0.24f, W * 0.55f, H * 0.18f), _winner.Name.ToUpper(), nameStyle, new Color(0.08f, 0.08f, 0.08f), Color.white, 4f);

                var sub = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
                sub.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(W * 0.05f, H * 0.24f + H * 0.13f, W * 0.5f, 30f),
                    $"WINNER!  ·  ${_winner.Money}  ·  {_winner.OwnedTiles.Count} properties", sub);
            }

            // Paneles del resto, ranking 2..4 (estilo Smash)
            var losers = new List<PlayerState>();
            foreach (var p in _players) if (p != _winner) losers.Add(p);
            losers.Sort((a, b) => b.EliminationOrder.CompareTo(a.EliminationOrder)); // último en caer = 2º

            Color[] rankColors =
            {
                new Color(0.78f, 0.78f, 0.82f), // 2 plata
                new Color(0.85f, 0.55f, 0.20f), // 3 bronce
                new Color(0.62f, 0.48f, 0.85f)  // 4 morado
            };

            // fila centrada bajo el ganador, sin escalonado
            float pw = W * 0.17f, ph = H * 0.19f, gap = W * 0.015f;
            float rowW = losers.Count * pw + (losers.Count - 1) * gap;
            float px = W * 0.70f - rowW / 2f, py = H * 0.745f;

            for (int i = 0; i < losers.Count; i++)
            {
                var p = losers[i];
                var panel = new Rect(px + i * (pw + gap), py, pw, ph);
                int rank = i + 2;

                // arte del puesto: marco + insignia + personaje ya integrados
                var rankTex = GetTex($"UI/rank_{p.CharacterId}_{Mathf.Min(rank, 4)}");
                if (rankTex != null)
                {
                    float ah = panel.height;
                    float aw = ah * ((float)rankTex.width / rankTex.height);
                    if (aw > panel.width) { ah *= panel.width / aw; aw = panel.width; }
                    var fit = new Rect(panel.center.x - aw / 2f, panel.yMax - ah, aw, ah);
                    GUI.DrawTexture(fit, rankTex, ScaleMode.ScaleToFit);
                }
                else
                {
                    // respaldo: caja gris con inicial y número de puesto
                    GUI.color = new Color(0.42f, 0.42f, 0.45f, 0.6f);
                    GUI.DrawTexture(panel, Texture2D.whiteTexture);
                    GUI.color = prevColor;

                    var init = new GUIStyle(GUI.skin.label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
                    OutlinedLabel(panel, p.Name.Substring(0, 1).ToUpper(), init, Color.white, Color.black, 2f);

                    var rankStyle = new GUIStyle(GUI.skin.label) { fontSize = (int)(ph * 0.42f), fontStyle = FontStyle.Bold, font = UIFonts.Title };
                    OutlinedLabel(new Rect(panel.x + 8f, panel.y + 2f, 90f, ph * 0.5f), (i + 2).ToString(),
                        rankStyle, rankColors[Mathf.Min(i, rankColors.Length - 1)], Color.black, 3f);
                }

                // nombre (y CPU si es bot) DEBAJO del recuadro, centrado
                var small = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title };
                string tagName = p.IsBot ? $"{p.Name}  ·  CPU" : p.Name;
                OutlinedLabel(new Rect(panel.x, panel.yMax + 2f, panel.width, 24f), tagName, small, Color.white, Color.black, 2f);
            }

            // Botones grandes apilados debajo del panel de resultados
            if (GUI.Button(new Rect(btnX, btnY, 260f, 68f), "Play again", UIFonts.OrnateButton))
            {
                AudioManager.Play("sfx_button");
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }

            if (GUI.Button(new Rect(btnX, btnY + 78f, 260f, 68f), "Main menu", UIFonts.OrnateButton))
            {
                AudioManager.Play("sfx_button");
                GameConfig.Reset();
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }

        /// <summary>Carta ampliada en el centro (clic en una propiedad del panel).</summary>
        void DrawCardOverlay()
        {
            if (_cardOverlay == null) return;

            var tex = GetCard(_cardOverlay);
            if (tex == null) { _cardOverlay = null; return; }

            float h = Screen.height * 0.62f;
            float w = h * tex.width / tex.height;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f - 24f, w, h);

            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
            if (GUI.Button(new Rect(rect.x + w / 2f - 60f, rect.yMax + 10f, 120f, 36f), "Close"))
                _cardOverlay = null;
        }

        void DrawCameraPanel()
        {
            if (Cam == null) return;

            var camIco = GetTex("UI/Icons/icon_camera");

            // contraído: solo un botón con el icono de cámara
            if (!_camPanelOpen)
            {
                var btn = new Rect(Screen.width - 58f, 10f, 48f, 48f);
                if (GUI.Button(btn, GUIContent.none))
                    _camPanelOpen = true;
                if (camIco != null)
                    GUI.DrawTexture(new Rect(btn.x + 8f, btn.y + 8f, 32f, 32f), camIco, ScaleMode.ScaleToFit);
                return;
            }

            float w = 175f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10, 10, w, 300), GUI.skin.box);

            // cabecera: icono + título + botón de contraer
            GUILayout.BeginHorizontal();
            if (camIco != null)
            {
                var ir = GUILayoutUtility.GetRect(26, 26, GUILayout.Width(26));
                GUI.DrawTexture(ir, camIco, ScaleMode.ScaleToFit);
            }
            GUILayout.Label("Camera");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("▲", GUILayout.Width(30), GUILayout.Height(24)))
                _camPanelOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(2);

            bool free = Cam.Mode == CameraMode.Free;
            bool top = Cam.Mode == CameraMode.Top;

            if (GUILayout.Toggle(free, " Free (V)", GUI.skin.button) && !free)
                Cam.SetMode(CameraMode.Free);

            if (GUILayout.Toggle(top, " Top (V)", GUI.skin.button) && !top)
                Cam.SetMode(CameraMode.Top);

            bool followTurn = Cam.Mode == CameraMode.Follow && _followCurrent;
            if (GUILayout.Toggle(followTurn, " Follow turn (F)", GUI.skin.button) && !followTurn)
                FollowCurrentPlayer();

            GUILayout.Space(4);
            GUILayout.Label("Follow player:");
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Bankrupt) continue;
                bool pinned = Cam.Mode == CameraMode.Follow && !_followCurrent && _pinnedPlayer == i;
                if (GUILayout.Toggle(pinned, " " + _players[i].Name, GUI.skin.button) && !pinned)
                    FollowPinnedPlayer(i);
            }

            GUILayout.EndArea();
        }
    }
}
