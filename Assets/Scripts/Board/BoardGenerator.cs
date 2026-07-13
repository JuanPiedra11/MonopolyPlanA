using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// Genera el tablero 3D proceduralmente: 40 casillas en un cuadrado de 11x11.
    /// Los cubos/materiales generados aquí son placeholders que luego se
    /// reemplazan con el arte creado en ComfyUI (texturas) y modelos 3D.
    /// </summary>
    public class BoardGenerator : MonoBehaviour
    {
        public const int TileCount = 40;
        public const float TileSize = 2.2f;

        public List<TileData> Tiles { get; private set; }

        static readonly Color[] GroupColors =
        {
            new Color(0.55f, 0.35f, 0.20f), // 0 Bocetos (marrón)
            new Color(0.55f, 0.80f, 0.95f), // 1 Storyboard (celeste)
            new Color(0.90f, 0.45f, 0.65f), // 2 Modelado (rosa)
            new Color(0.95f, 0.60f, 0.20f), // 3 Texturizado (naranja)
            new Color(0.85f, 0.20f, 0.20f), // 4 Rigging (rojo)
            new Color(0.95f, 0.85f, 0.25f), // 5 Animación (amarillo)
            new Color(0.25f, 0.70f, 0.35f), // 6 Iluminación (verde)
            new Color(0.20f, 0.35f, 0.75f)  // 7 Render Final (azul)
        };

        readonly List<GameObject> _tileObjects = new List<GameObject>();
        Material _baseMat;

        /// <summary>Color del grupo para la UI (chips de propiedades).</summary>
        public static Color GetGroupColor(int group) =>
            group >= 0 && group < GroupColors.Length ? GroupColors[group] : Color.gray;

        // --- Mazos 3D de cartas sobre el tablero ---
        public Vector3 ChanceDeckWorldPos { get; private set; }
        public Vector3 CommunityDeckWorldPos { get; private set; }

        void CreateDecks(float boardSize)
        {
            var chanceBack = Resources.Load<Texture2D>("Cards/Events/event_chance_back");
            var communityBack = Resources.Load<Texture2D>("Cards/Events/event_community_back");
            if (communityBack == null) communityBack = chanceBack;

            // posiciones medidas de los rombos dibujados en el arte del tablero (u,v desde arriba-izquierda)
            ChanceDeckWorldPos = CreateDeckStack(chanceBack, new Vector2(0.69f, 0.71f), boardSize, "ChanceDeck");
            CommunityDeckWorldPos = CreateDeckStack(communityBack, new Vector2(0.28f, 0.27f), boardSize, "CommunityDeck");
        }

        Vector3 CreateDeckStack(Texture2D back, Vector2 uvTopLeft, float boardSize, string name)
        {
            float x = (uvTopLeft.x - 0.5f) * boardSize;
            float z = (0.5f - uvTopLeft.y) * boardSize;

            var parent = new GameObject(name);
            parent.transform.SetParent(transform, false);
            parent.transform.localPosition = new Vector3(x, 0f, z);
            parent.transform.localRotation = Quaternion.Euler(0f, 45f, 0f); // rombos del arte

            if (back != null)
            {
                var mat = new Material(Shader.Find("Unlit/Transparent"));
                mat.mainTexture = back;

                const float cw = 2.25f; // del tamaño del recuadro dibujado
                float ch = cw * ((float)back.height / back.width);

                for (int i = 0; i < 8; i++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = $"Card_{i}";
                    Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(parent.transform, false);
                    q.transform.localPosition = new Vector3(
                        Random.Range(-0.02f, 0.02f), 0.05f + i * 0.012f, Random.Range(-0.02f, 0.02f));
                    q.transform.localRotation = Quaternion.Euler(90f, 0f, Random.Range(-3.5f, 3.5f));
                    q.transform.localScale = new Vector3(cw, ch, 1f);
                    q.GetComponent<Renderer>().material = mat;
                }
            }

            return parent.transform.position + Vector3.up * 0.18f;
        }

        readonly Dictionary<int, GameObject> _houseGroups = new Dictionary<int, GameObject>();

        /// <summary>Dibuja (o redibuja) las casitas sobre una casilla.</summary>
        public void SetHouses(int index, int count)
        {
            if (_houseGroups.TryGetValue(index, out var old) && old != null)
                Destroy(old);
            if (count <= 0) return;

            var parent = new GameObject($"Houses_{index}");
            parent.transform.SetParent(transform, false);
            parent.transform.localPosition = GetTileWorldPos(index);
            _houseGroups[index] = parent;

            for (int i = 0; i < count; i++)
            {
                var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
                h.name = "House";
                h.transform.SetParent(parent.transform, false);
                h.transform.localScale = new Vector3(0.28f, 0.3f, 0.28f);
                h.transform.localPosition = new Vector3(-0.68f + i * 0.45f, 0.18f, 0.68f);

                var mat = new Material(_baseMat);
                mat.color = new Color(0.15f, 0.65f, 0.25f);
                h.GetComponent<Renderer>().material = mat;
            }
        }

        public void Build()
        {
            Tiles = BoardFactory.CreateBoard();
            _baseMat = new Material(Shader.Find("Standard"));

            // Tablero ilustrado (Resources/Board/board_uniform): un plano con el arte.
            var boardTex = Resources.Load<Texture2D>("Board/board_uniform");
            if (boardTex != null)
            {
                float size = TileSize * 11f;

                var art = GameObject.CreatePrimitive(PrimitiveType.Quad);
                art.name = "BoardArt";
                Destroy(art.GetComponent<Collider>());
                art.transform.SetParent(transform, false);
                art.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                art.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                art.transform.localScale = new Vector3(size, size, 1f);

                var mat = new Material(Shader.Find("Unlit/Texture"));
                mat.mainTexture = boardTex;
                art.GetComponent<Renderer>().material = mat;

                // base sólida bajo el arte
                var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                basePlate.name = "BoardBase";
                basePlate.transform.SetParent(transform, false);
                basePlate.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                basePlate.transform.localScale = new Vector3(size + 0.3f, 0.12f, size + 0.3f);
                var baseMat = new Material(_baseMat);
                baseMat.color = new Color(0.16f, 0.12f, 0.09f);
                basePlate.GetComponent<Renderer>().material = baseMat;

                CreateDecks(size);
                return;
            }

            // Fallback procedural (cubos de colores) si no hay textura
            for (int i = 0; i < TileCount; i++)
                CreateTileObject(i);

            CreateCenter();
        }

        /// <summary>
        /// Fronteras de las 11 celdas (fracciones 0..1) según las proporciones
        /// clásicas de Monopoly que usa el arte: esquinas 13.75%, calles 8.05%.
        /// </summary>
        static readonly float[] Bounds =
        {
            0f, 0.1375f, 0.218f, 0.2985f, 0.379f, 0.4595f,
            0.54f, 0.6205f, 0.701f, 0.7815f, 0.862f, 1f
        };

        public const float BoardSize = TileSize * 11f;

        static float CellCenter(int i) => (Bounds[i] + Bounds[i + 1]) * 0.5f;
        static float CellWidth(int i) => (Bounds[i + 1] - Bounds[i]) * BoardSize;

        static void TileToGrid(int index, out int col, out int row)
        {
            if (index <= 10) { col = 10 - index; row = 0; }
            else if (index <= 20) { col = 0; row = index - 10; }
            else if (index <= 30) { col = index - 20; row = 10; }
            else { col = 10; row = 10 - (index - 30); }
        }

        /// <summary>Posición en mundo del centro de la casilla (alineada al arte).</summary>
        public Vector3 GetTileWorldPos(int index)
        {
            TileToGrid(index, out int col, out int row);
            return new Vector3(
                (CellCenter(col) - 0.5f) * BoardSize,
                0f,
                (CellCenter(row) - 0.5f) * BoardSize);
        }

        /// <summary>Tamaño en mundo (ancho X, fondo Z) de la casilla.</summary>
        public Vector3 GetTileWorldSize(int index)
        {
            TileToGrid(index, out int col, out int row);
            return new Vector3(CellWidth(col), 0f, CellWidth(row));
        }

        void CreateTileObject(int index)
        {
            TileData data = Tiles[index];

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Tile_{index:00}_{data.Name}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GetTileWorldPos(index);
            Vector3 ts = GetTileWorldSize(index);
            go.transform.localScale = new Vector3(ts.x * 0.94f, 0.2f, ts.z * 0.94f);

            var mat = new Material(_baseMat);
            mat.color = TileColor(data);
            go.GetComponent<Renderer>().material = mat;

            CreateLabel(go.transform, data.Name, index);
            _tileObjects.Add(go);
        }

        Color TileColor(TileData data)
        {
            switch (data.Type)
            {
                case TileType.Start:       return new Color(0.30f, 0.85f, 0.40f);
                case TileType.Jail:        return new Color(0.45f, 0.45f, 0.50f);
                case TileType.GoToJail:    return new Color(0.30f, 0.30f, 0.35f);
                case TileType.FreeParking: return new Color(0.80f, 0.80f, 0.75f);
                case TileType.Tax:         return new Color(0.60f, 0.55f, 0.75f);
                case TileType.Chance:      return new Color(0.95f, 0.55f, 0.10f);
                case TileType.Community:   return new Color(0.35f, 0.65f, 0.95f);
                case TileType.Studio:      return new Color(0.25f, 0.25f, 0.25f);
                case TileType.Utility:     return new Color(0.75f, 0.75f, 0.35f);
                default:
                    return data.ColorGroup >= 0 && data.ColorGroup < GroupColors.Length
                        ? GroupColors[data.ColorGroup]
                        : Color.white;
            }
        }

        void CreateLabel(Transform parent, string text, int index)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.05f;

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = Wrap(text);
            tm.fontSize = 48;
            tm.characterSize = 0.5f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.black;
        }

        static string Wrap(string text)
        {
            var words = text.Split(' ');
            if (words.Length <= 1) return text;
            int mid = words.Length / 2;
            return string.Join(" ", words, 0, mid) + "\n" + string.Join(" ", words, mid, words.Length - mid);
        }

        void CreateCenter()
        {
            var center = GameObject.CreatePrimitive(PrimitiveType.Cube);
            center.name = "BoardCenter";
            center.transform.SetParent(transform, false);
            center.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            center.transform.localScale = new Vector3(TileSize * 9f, 0.1f, TileSize * 9f);

            var mat = new Material(_baseMat);
            mat.color = new Color(0.85f, 0.93f, 0.85f);
            center.GetComponent<Renderer>().material = mat;

            var logoGo = new GameObject("CenterLabel");
            logoGo.transform.SetParent(transform, false);
            logoGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            logoGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            logoGo.transform.localScale = Vector3.one * 0.25f;

            var tm = logoGo.AddComponent<TextMesh>();
            tm.text = "MONOPOLY\nPlanA";
            tm.fontSize = 60;
            tm.characterSize = 0.5f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.15f, 0.35f, 0.25f);
        }
    }
}
