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

        public void Build()
        {
            Tiles = BoardFactory.CreateBoard();
            _baseMat = new Material(Shader.Find("Standard"));

            for (int i = 0; i < TileCount; i++)
                CreateTileObject(i);

            CreateCenter();
        }

        /// <summary>Posición en mundo del centro de la casilla.</summary>
        public Vector3 GetTileWorldPos(int index)
        {
            int col, row;
            if (index <= 10) { col = 10 - index; row = 0; }
            else if (index <= 20) { col = 0; row = index - 10; }
            else if (index <= 30) { col = index - 20; row = 10; }
            else { col = 10; row = 10 - (index - 30); }

            return new Vector3((col - 5) * TileSize, 0f, (row - 5) * TileSize);
        }

        void CreateTileObject(int index)
        {
            TileData data = Tiles[index];

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Tile_{index:00}_{data.Name}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = GetTileWorldPos(index);
            go.transform.localScale = new Vector3(TileSize * 0.94f, 0.2f, TileSize * 0.94f);

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
