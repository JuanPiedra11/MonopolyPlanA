using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// Fuentes del juego (Resources/Fonts, licencia OFL):
    /// - Cinzel: títulos y botones (mayúsculas romanas talladas)
    /// - Alegreya: cuerpo, HUD y cifras (serif legible)
    /// - MedievalSharp: acentos medievales puntuales
    /// </summary>
    public static class UIFonts
    {
        static Font _title, _body, _accent;
        static bool _loaded;

        static Texture2D _btnNormal, _btnHover, _btnPressed;
        static Texture2D _btnSimpleNormal, _btnSimpleHover, _btnSimplePressed;
        static GUIStyle _ornate;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _title = Resources.Load<Font>("Fonts/Cinzel");
            _body = Resources.Load<Font>("Fonts/Alegreya");
            _accent = Resources.Load<Font>("Fonts/MedievalSharp");
            _btnNormal = Resources.Load<Texture2D>("UI/Buttons/button_normal");
            _btnHover = Resources.Load<Texture2D>("UI/Buttons/button_hover");
            _btnPressed = Resources.Load<Texture2D>("UI/Buttons/button_pressed");
            // versión baja del botón simple: no se deforma en filas y toggles compactos
            _btnSimpleNormal = Resources.Load<Texture2D>("UI/Buttons/button_simple_normal_low");
            _btnSimpleHover = Resources.Load<Texture2D>("UI/Buttons/button_simple_hover_low");
            _btnSimplePressed = Resources.Load<Texture2D>("UI/Buttons/button_simple_pressed_low");
        }

        public static Font Title { get { Load(); return _title; } }
        public static Font Body { get { Load(); return _body; } }
        public static Font Accent { get { Load(); return _accent; } }

        static void SetStates(GUIStyle b, Texture2D n, Texture2D h, Texture2D a)
        {
            b.normal.background = n;
            b.hover.background = h != null ? h : n;
            b.active.background = a != null ? a : n;
            b.focused.background = n;
            b.onNormal.background = h != null ? h : n;
            b.onHover.background = h != null ? h : n;
            b.onActive.background = a != null ? a : n;
            b.onFocused.background = h != null ? h : n;

            // Tipografía acorde (Cinzel) y texto centrado en la placa;
            // el arte proyecta sombra abajo, así que el centro óptico va 2px arriba
            if (_title != null) b.font = _title;
            b.fontStyle = FontStyle.Bold;
            b.alignment = TextAnchor.MiddleCenter;
            b.contentOffset = new Vector2(0f, -2f);

            // color del texto según el estado del botón
            Color parchment = new Color(0.96f, 0.90f, 0.72f);  // reposo: pergamino claro
            Color parchDark = new Color(0.32f, 0.20f, 0.06f);  // hover: tinta sobre el oro
            Color emberGold = new Color(1.00f, 0.84f, 0.42f);  // pulsado: brasa dorada
            b.normal.textColor = parchment;
            b.hover.textColor = parchDark;
            b.active.textColor = emberGold;
            b.focused.textColor = parchment;
            b.onNormal.textColor = parchDark;
            b.onHover.textColor = parchDark;
            b.onActive.textColor = emberGold;
            b.onFocused.textColor = parchDark;
        }

        /// <summary>
        /// Botón ornamentado (esquinas con gemas) para las acciones principales
        /// del menú y la pantalla de resultados.
        /// </summary>
        public static GUIStyle OrnateButton
        {
            get
            {
                Load();
                if (_ornate == null)
                {
                    _ornate = new GUIStyle(GUI.skin.button);
                    if (_btnNormal != null)
                    {
                        SetStates(_ornate, _btnNormal, _btnHover, _btnPressed);
                        _ornate.border = new RectOffset(15, 15, 14, 14);
                    }
                }
                return _ornate;
            }
        }

        /// <summary>Aplica la fuente de cuerpo y el botón de madera SIMPLE como estilo por defecto.</summary>
        public static void ApplyBody()
        {
            Load();
            if (_body != null && GUI.skin.font != _body)
                GUI.skin.font = _body;

            // las etiquetas son texto informativo: sin reacción alguna al mouse
            var lbl = GUI.skin.label;
            lbl.hover.textColor = lbl.normal.textColor;
            lbl.active.textColor = lbl.normal.textColor;
            lbl.focused.textColor = lbl.normal.textColor;
            lbl.hover.background = null;
            lbl.active.background = null;

            // La variante simple viste todos los botones compactos (toggles, filas, paneles)
            if (_btnSimpleNormal != null)
            {
                var b = GUI.skin.button;
                SetStates(b, _btnSimpleNormal, _btnSimpleHover, _btnSimplePressed);
                b.border = new RectOffset(7, 7, 7, 7);
            }
        }
    }
}
