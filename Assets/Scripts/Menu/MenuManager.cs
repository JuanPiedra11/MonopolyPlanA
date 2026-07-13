using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonopolyPlanA
{
    /// <summary>
    /// Pantalla 1: splash art a pantalla completa con Jugar/Salir.
    /// Pantalla 2: modo de juego — Jugar Solo (1 humano + 1–3 bots) o Multijugador.
    /// Solo: nombre + personaje + nº de bots y arranca. Multijugador: lobby completo
    /// (cuántos jugadores, humano/bot, nombre, color y personaje). Luego carga Main.
    /// El splash usa Resources/UI/splash.png si existe; si no, un background al azar.
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        enum Step { Title, Mode, Solo, Lobby }

        Step _step = Step.Title;
        int _total = 2;
        int _soloBots = 1; // rivales bot en modo solo (1–3)

        // tamaño estándar de los botones principales (mismas proporciones en todo el menú)
        const float BtnW = 220f, BtnH = 62f;
        readonly PlayerSetup[] _slots = new PlayerSetup[4];
        Vector2 _lobbyScroll;
        string _error = "";
        Texture2D _splash;
        Texture2D _soloBg; // escenario aleatorio de la pantalla Jugar Solo
        bool _dedicatedSplash; // el arte ya trae el logo dibujado
        readonly Texture2D[] _avatars = new Texture2D[4];

        void Awake()
        {
#if !UNITY_EDITOR
            // build jugable: ventana fija 16:9 para que la UI no se deforme
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
#endif
            EnsureSlots();
            AudioManager.PlayMusic("music_menu"); // tema del menú principal

            _splash = Resources.Load<Texture2D>("UI/splash");
            _dedicatedSplash = _splash != null;
            if (_splash == null)
            {
                var bgs = Resources.LoadAll<Texture2D>("Backgrounds");
                if (bgs != null && bgs.Length > 0)
                    _splash = bgs[Random.Range(0, bgs.Length)];
            }

            for (int c = 0; c < 4; c++)
                _avatars[c] = Resources.Load<Texture2D>($"Avatars/{GameConfig.CharacterIds[c]}_avatar");
        }

        /// <summary>Crea los slots si faltan (Awake o tras un hot-reload del editor).</summary>
        void EnsureSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) continue;
                _slots[i] = new PlayerSetup
                {
                    Name = i == 0 ? "Player 1" : "Bot " + (i + 1),
                    IsBot = i != 0,
                    ColorIndex = i,
                    CharacterIndex = i
                };
            }
        }

        void OnGUI()
        {
            EnsureSlots(); // por si el recompilado en caliente vació los slots
            UIFonts.ApplyBody();
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUI.skin.textField.fontSize = 16;

            switch (_step)
            {
                case Step.Title: DrawTitle(); break;
                case Step.Mode:  DrawMode();  break;
                case Step.Solo:  DrawSolo();  break;
                default:         DrawLobby(); break;
            }
        }

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

        static void DrawCover(Texture2D tex)
        {
            float W = Screen.width, H = Screen.height;
            float ta = (float)tex.width / tex.height;
            float sa = W / H;
            Rect r = ta > sa
                ? new Rect((W - H * ta) / 2f, 0f, H * ta, H)
                : new Rect(0f, (H - W / ta) / 2f, W, W / ta);
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
        }

        // ---------- Pantalla 1: Splash ----------

        void DrawTitle()
        {
            float W = Screen.width, H = Screen.height;
            Color prev = GUI.color;

            if (_splash != null)
            {
                DrawCover(_splash);
                // velo solo abajo, para los botones
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(0, H * 0.74f, W, H * 0.26f), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            if (!_dedicatedSplash)
            {
                // sin splash dedicado: dibujar el logo por código
                var logo = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(H * 0.14f), fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
                };
                OutlinedLabel(new Rect(0, H * 0.08f, W, H * 0.2f), "MONOPOLY", logo,
                    new Color(1f, 0.85f, 0.3f), new Color(0.15f, 0.08f, 0.02f), 5f);

                var sub = new GUIStyle(GUI.skin.label)
                {
                    fontSize = (int)(H * 0.035f), alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
                };
                OutlinedLabel(new Rect(0, H * 0.26f, W, H * 0.06f), "PlanA technology", sub,
                    Color.white, new Color(0.1f, 0.1f, 0.1f), 2f);
            }

            // Botones (tamaño estándar unificado, estilo ornamentado)
            var ornate = new GUIStyle(UIFonts.OrnateButton) { fontSize = 22 };
            if (GUI.Button(new Rect(W / 2f - BtnW / 2f, H * 0.74f, BtnW, BtnH), "Play", ornate))
            {
                AudioManager.Play("sfx_button_menu");
                _error = "";
                _step = Step.Mode;
            }
            if (GUI.Button(new Rect(W / 2f - BtnW / 2f, H * 0.74f + BtnH + 10f, BtnW, BtnH), "Quit", ornate))
            {
                AudioManager.Play("sfx_button_menu");
                Application.Quit();
            }
        }

        // ---------- Pantalla 2: Modo de juego ----------

        void DrawMode()
        {
            float W = Screen.width, H = Screen.height;
            Color prev = GUI.color;

            if (_splash != null)
            {
                DrawCover(_splash);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var header = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(H * 0.06f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };
            OutlinedLabel(new Rect(0, H * 0.18f, W, H * 0.1f), "Game Mode", header,
                new Color(1f, 0.85f, 0.3f), new Color(0.15f, 0.08f, 0.02f), 3f);

            var ornateBig = new GUIStyle(UIFonts.OrnateButton) { fontSize = 22 };
            var ornateMid = new GUIStyle(UIFonts.OrnateButton) { fontSize = 16 };
            if (GUI.Button(new Rect(W / 2f - BtnW / 2f, H * 0.40f, BtnW, BtnH), "Single Player", ornateBig))
            {
                AudioManager.Play("sfx_button_menu");
                _error = "";
                _step = Step.Solo;
            }
            if (GUI.Button(new Rect(W / 2f - BtnW / 2f, H * 0.40f + BtnH + 16f, BtnW, BtnH), "Multiplayer", ornateBig))
            {
                AudioManager.Play("sfx_button_menu");
                _error = "";
                _step = Step.Lobby;
            }
            if (GUI.Button(new Rect(W / 2f - BtnW / 2f, H * 0.40f + 2 * (BtnH + 16f), BtnW, BtnH), "◄ Back", ornateMid))
            {
                AudioManager.Play("sfx_button_menu");
                _step = Step.Title;
            }
        }

        // ---------- Pantalla 2b: Jugar Solo ----------

        void DrawSolo()
        {
            float W = Screen.width, H = Screen.height;
            Color prev = GUI.color;

            // fondo: escenario aleatorio con velo para legibilidad
            if (_soloBg == null)
            {
                var bgs = Resources.LoadAll<Texture2D>("Backgrounds");
                if (bgs != null && bgs.Length > 0)
                    _soloBg = bgs[Random.Range(0, bgs.Length)];
            }
            if (_soloBg != null) DrawCover(_soloBg);
            GUI.color = new Color(0f, 0f, 0f, 0.60f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = prev;

            var me = _slots[0];
            me.IsBot = false;

            // título
            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = (int)(H * 0.072f), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };
            OutlinedLabel(new Rect(0, H * 0.045f, W, H * 0.10f), "SINGLE PLAYER", title,
                new Color(1f, 0.85f, 0.3f), new Color(0.15f, 0.08f, 0.02f), 3f);

            // ------- columna izquierda: formulario -------
            float fx = W * 0.09f;
            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21, fontStyle = FontStyle.Bold, font = UIFonts.Title
            };
            label.normal.textColor = new Color(1f, 0.85f, 0.3f);

            // nombre
            GUI.Label(new Rect(fx, H * 0.21f, 260, 28), "Your name", label);
            GUI.skin.textField.fontSize = 18;
            me.Name = GUI.TextField(new Rect(fx, H * 0.21f + 34f, 270, 36), me.Name, 14);

            // personaje (retratos con aire)
            GUI.Label(new Rect(fx, H * 0.35f, 260, 28), "Your character", label);
            float cell = 78f, gapC = 20f;
            float py = H * 0.35f + 36f;
            for (int c = 0; c < 4; c++)
            {
                var r = new Rect(fx + c * (cell + gapC), py, cell, cell);
                bool selected = me.CharacterIndex == c;

                Color pc = GUI.color;
                GUI.color = selected ? new Color(1f, 0.84f, 0.25f) : new Color(0.55f, 0.55f, 0.6f);
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
                GUI.color = new Color(0.13f, 0.13f, 0.17f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (_avatars[c] != null) GUI.DrawTexture(r, _avatars[c], ScaleMode.ScaleAndCrop);
                GUI.color = pc;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    me.CharacterIndex = c;

                var tag = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14, alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
                };
                tag.normal.textColor = selected ? new Color(1f, 0.84f, 0.25f) : new Color(0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(r.x - 10, r.yMax + 6f, cell + 20, 22), GameConfig.CharacterNames[c], tag);
            }

            // bots rivales (botones amplios, número centrado)
            float by = py + cell + 46f;
            GUI.Label(new Rect(fx, by, 260, 28), "Rival bots", label);
            var bot = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };
            for (int n = 1; n <= 3; n++)
            {
                var r = new Rect(fx + (n - 1) * 96f, by + 36f, 78f, 50f);
                if (GUI.Toggle(r, _soloBots == n, n.ToString(), bot) && _soloBots != n)
                    _soloBots = n;
            }

            if (_error != "")
            {
                var err = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
                err.normal.textColor = new Color(1f, 0.45f, 0.35f);
                GUI.Label(new Rect(fx, by + 98f, W * 0.45f, 26), _error, err);
            }

            // ------- columna derecha: configuración de la partida -------
            float cx2 = W * 0.58f;
            float optW = W * 0.105f, optH = 46f, optGap = W * 0.012f;

            var caption = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            caption.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            var opt = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };

            // fin de partida
            GUI.Label(new Rect(cx2, H * 0.21f, 300, 28), "Match end", label);
            string[] modos = { "Classic", "Time", "Rounds" };
            for (int m = 0; m < 3; m++)
            {
                var r = new Rect(cx2 + m * (optW + optGap), H * 0.21f + 34f, optW, optH);
                if (GUI.Toggle(r, (int)GameConfig.Mode == m, modos[m], opt) && (int)GameConfig.Mode != m)
                    GameConfig.Mode = (EndMode)m;
            }

            // sub-opción según el modo
            float sy = H * 0.21f + 34f + optH + 16f;
            if (GameConfig.Mode == EndMode.Classic)
            {
                GUI.Label(new Rect(cx2, sy + 6f, 320, 26), "Until only one player remains.", caption);
            }
            else if (GameConfig.Mode == EndMode.Time)
            {
                GUI.Label(new Rect(cx2, sy, 300, 26), "Duration (minutes)", caption);
                int[] mins = { 10, 20, 30 };
                for (int m = 0; m < 3; m++)
                {
                    var r = new Rect(cx2 + m * (optW * 0.8f + optGap), sy + 30f, optW * 0.8f, 42f);
                    if (GUI.Toggle(r, GameConfig.TimeLimitMin == mins[m], mins[m].ToString(), opt)
                        && GameConfig.TimeLimitMin != mins[m])
                        GameConfig.TimeLimitMin = mins[m];
                }
            }
            else
            {
                GUI.Label(new Rect(cx2, sy, 300, 26), "Number of rounds", caption);
                int[] rondas = { 10, 20, 30 };
                for (int m = 0; m < 3; m++)
                {
                    var r = new Rect(cx2 + m * (optW * 0.8f + optGap), sy + 30f, optW * 0.8f, 42f);
                    if (GUI.Toggle(r, GameConfig.RoundLimit == rondas[m], rondas[m].ToString(), opt)
                        && GameConfig.RoundLimit != rondas[m])
                        GameConfig.RoundLimit = rondas[m];
                }
            }

            // oro inicial
            float gy = sy + 30f + 42f + 26f;
            GUI.Label(new Rect(cx2, gy, 300, 28), "Starting gold", label);
            var goldTxt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter, font = UIFonts.Title
            };
            goldTxt.normal.textColor = new Color(1f, 0.85f, 0.3f);
            var minus = new Rect(cx2, gy + 34f, 46f, 46f);
            var plus = new Rect(cx2 + 46f + 130f, gy + 34f, 46f, 46f);
            if (GUI.Button(minus, "–", opt))
                GameConfig.StartGold = Mathf.Max(500, GameConfig.StartGold - 250);
            if (GUI.Button(plus, "+", opt))
                GameConfig.StartGold = Mathf.Min(5000, GameConfig.StartGold + 250);
            GUI.Label(new Rect(minus.xMax, gy + 34f, 130f, 46f), $"${GameConfig.StartGold}", goldTxt);

            // ------- botones -------
            if (GUI.Button(new Rect(W / 2f - BtnW - 10f, H * 0.855f, BtnW, BtnH), "◄ Back", UIFonts.OrnateButton))
            {
                AudioManager.Play("sfx_button_menu");
                _step = Step.Mode;
            }
            if (GUI.Button(new Rect(W / 2f + 10f, H * 0.855f, BtnW, BtnH), "Play!", UIFonts.OrnateButton))
            {
                AudioManager.Play("sfx_button_menu");
                StartSolo();
            }
        }

        void StartSolo()
        {
            var me = _slots[0];
            if (string.IsNullOrWhiteSpace(me.Name))
            {
                _error = "Enter your name.";
                return;
            }

            GameConfig.Reset();
            GameConfig.Players.Add(new PlayerSetup
            {
                Name = me.Name.Trim(),
                IsBot = false,
                ColorIndex = me.ColorIndex,
                CharacterIndex = me.CharacterIndex
            });

            // personajes de los bots: distintos entre sí y distinto al del jugador
            var pool = new System.Collections.Generic.List<int>();
            for (int c = 0; c < 4; c++)
                if (c != me.CharacterIndex) pool.Add(c);
            for (int i = pool.Count - 1; i > 0; i--) // barajar
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            for (int i = 0; i < _soloBots; i++)
            {
                GameConfig.Players.Add(new PlayerSetup
                {
                    Name = "Bot " + (i + 1),
                    IsBot = true,
                    ColorIndex = (me.ColorIndex + 1 + i) % PlayerPalette.Colors.Length,
                    CharacterIndex = pool[i]
                });
            }
            SceneManager.LoadScene("Main");
        }

        // ---------- Pantalla 3: Lobby (multijugador) ----------

        void DrawLobby()
        {
            float W = Screen.width, H = Screen.height;
            Color prevBg = GUI.backgroundColor;

            // fondo suavemente oscurecido sobre el tablero decorativo
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = prev;

            const float w = 680f;
            float h = Mathf.Min(206f + _total * 78f, H - 12f); // nunca más alto que la pantalla
            GUILayout.BeginArea(new Rect((W - w) / 2f, Mathf.Max(6f, (H - h) / 2f), w, h), GUI.skin.box);

            var header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, font = UIFonts.Title
            };
            GUILayout.Label("Lobby — Choose your character", header);
            GUILayout.Space(4);

            // Total de jugadores
            GUILayout.BeginHorizontal();
            GUILayout.Label("Players:", GUILayout.Width(90));
            for (int n = 2; n <= 4; n++)
                if (GUILayout.Toggle(_total == n, "  " + n + "  ", GUI.skin.button, GUILayout.Height(26)) && _total != n)
                    _total = n;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // los slots van en un scroll: los botones inferiores siempre quedan visibles
            _lobbyScroll = GUILayout.BeginScrollView(_lobbyScroll);
            for (int i = 0; i < _total; i++)
                DrawSlot(i);
            GUILayout.EndScrollView();

            if (_error != "")
            {
                var err = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                err.normal.textColor = new Color(1f, 0.45f, 0.35f);
                GUILayout.Label(_error, err);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("◄ Back", UIFonts.OrnateButton, GUILayout.Height(BtnH), GUILayout.Width(BtnW)))
                _step = Step.Mode;
            GUILayout.Space(16);
            if (GUILayout.Button("Play!", UIFonts.OrnateButton, GUILayout.Height(BtnH), GUILayout.Width(BtnW)))
                TryStart();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.EndArea();
            GUI.backgroundColor = prevBg;
        }

        void DrawSlot(int i)
        {
            var s = _slots[i];
            Color prevBg = GUI.backgroundColor;

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(64));

            // Tipo
            GUILayout.BeginVertical(GUILayout.Width(92));
            if (GUILayout.Toggle(!s.IsBot, " Human", GUI.skin.button, GUILayout.Height(26)) && s.IsBot)
            {
                s.IsBot = false;
                s.Name = "Player " + (i + 1);
            }
            if (GUILayout.Toggle(s.IsBot, " Bot", GUI.skin.button, GUILayout.Height(26)) && !s.IsBot)
            {
                s.IsBot = true;
                s.Name = "Bot " + (i + 1);
            }
            GUILayout.EndVertical();

            // Nombre + color
            GUILayout.BeginVertical(GUILayout.Width(170));
            if (s.IsBot) GUILayout.Label(s.Name, GUILayout.Width(160));
            else s.Name = GUILayout.TextField(s.Name, 14, GUILayout.Width(160));

            GUI.backgroundColor = PlayerPalette.Colors[s.ColorIndex];
            if (GUILayout.Button(PlayerPalette.Names[s.ColorIndex], GUILayout.Width(110), GUILayout.Height(26)))
                s.ColorIndex = NextFreeColor(s.ColorIndex, i);
            GUI.backgroundColor = prevBg;
            GUILayout.EndVertical();

            // Personaje: 4 retratos (se pueden repetir; el nombre flotante los diferencia in-game)
            DrawCharacterPicker(s);

            GUILayout.EndHorizontal();
        }

        /// <summary>Fila de 4 retratos para elegir personaje (marco dorado en el elegido).</summary>
        void DrawCharacterPicker(PlayerSetup s)
        {
            for (int c = 0; c < 4; c++)
            {
                bool selected = s.CharacterIndex == c;

                GUILayout.BeginVertical(GUILayout.Width(56));

                Rect r = GUILayoutUtility.GetRect(50, 50, GUILayout.Width(50));

                // marco: dorado si es el elegido
                Color pc = GUI.color;
                GUI.color = selected ? new Color(1f, 0.84f, 0.25f) : new Color(0.55f, 0.55f, 0.6f);
                GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
                GUI.color = new Color(0.13f, 0.13f, 0.17f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                if (_avatars[c] != null) GUI.DrawTexture(r, _avatars[c], ScaleMode.ScaleAndCrop);
                GUI.color = pc;

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    s.CharacterIndex = c;

                var tag = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
                if (selected) tag.normal.textColor = new Color(1f, 0.84f, 0.25f);
                GUILayout.Label(GameConfig.CharacterNames[c], tag, GUILayout.Width(50));

                GUILayout.EndVertical();
            }
        }

        int NextFreeColor(int current, int slot)
        {
            for (int step = 1; step <= PlayerPalette.Colors.Length; step++)
            {
                int c = (current + step) % PlayerPalette.Colors.Length;
                bool used = false;
                for (int j = 0; j < _total; j++)
                    if (j != slot && _slots[j].ColorIndex == c) used = true;
                if (!used) return c;
            }
            return current;
        }

        void TryStart()
        {
            int humans = 0;
            for (int i = 0; i < _total; i++)
            {
                if (!_slots[i].IsBot) humans++;
                if (string.IsNullOrWhiteSpace(_slots[i].Name))
                {
                    _error = "Every player needs a name.";
                    return;
                }
            }
            if (humans < 1)
            {
                _error = "At least 1 human player is required.";
                return;
            }

            GameConfig.Reset();
            for (int i = 0; i < _total; i++)
            {
                GameConfig.Players.Add(new PlayerSetup
                {
                    Name = _slots[i].Name.Trim(),
                    IsBot = _slots[i].IsBot,
                    ColorIndex = _slots[i].ColorIndex,
                    CharacterIndex = _slots[i].CharacterIndex
                });
            }
            SceneManager.LoadScene("Main");
        }
    }
}
