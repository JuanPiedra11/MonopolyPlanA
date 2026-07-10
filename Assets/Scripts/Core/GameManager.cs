using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    public enum GameState { Setup, WaitingRoll, Moving, Decision, GameOver }

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
        string _log = "Bienvenido a Monopoly PlanA. Elige el número de jugadores.";
        TileData _pendingTile; // casilla comprable sobre la que se decide

        const int Salary = 200;
        const int StartMoney = 1500;
        const int RestPenaltyTurns = 1;

        static readonly Color[] PlayerColors =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.45f, 0.95f),
            new Color(0.25f, 0.80f, 0.35f),
            new Color(0.95f, 0.80f, 0.25f)
        };

        PlayerState Current => _players[_currentIndex];

        // ---------- Setup ----------

        void StartGame(int playerCount)
        {
            for (int i = 0; i < playerCount; i++)
            {
                var p = new PlayerState($"Jugador {i + 1}", PlayerColors[i]);
                p.Money = StartMoney;
                p.Token = CreateToken(p, i, playerCount);
                _players.Add(p);
            }

            _currentIndex = 0;
            _state = GameState.WaitingRoll;
            _log = $"¡Comienza la partida! Turno de {Current.Name}.";
        }

        GameObject CreateToken(PlayerState p, int index, int total)
        {
            var token = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            token.name = $"Token_{p.Name}";
            token.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = p.Color;
            token.GetComponent<Renderer>().material = mat;

            token.transform.position = TokenWorldPos(0, index);
            return token;
        }

        Vector3 TokenWorldPos(int tileIndex, int playerIndex)
        {
            Vector3 basePos = Board.GetTileWorldPos(tileIndex);
            float off = 0.45f;
            Vector3 offset = new Vector3(
                (playerIndex % 2 == 0 ? -off : off),
                0.6f,
                (playerIndex < 2 ? -off : off));
            return basePos + offset;
        }

        // ---------- Turno ----------

        void RollDice()
        {
            if (Current.RestTurns > 0)
            {
                Current.RestTurns--;
                _log = $"{Current.Name} está en crunch y descansa este turno.";
                NextPlayer();
                return;
            }

            _die1 = Random.Range(1, 7);
            _die2 = Random.Range(1, 7);
            bool doubles = _die1 == _die2;
            _doublesInARow = doubles ? _doublesInARow + 1 : 0;

            if (_doublesInARow >= 3)
            {
                _log = $"{Current.Name} sacó 3 dobles seguidos... ¡directo al crunch!";
                SendToJail(Current);
                _doublesInARow = 0;
                NextPlayer();
                return;
            }

            _log = $"{Current.Name} lanza: {_die1} + {_die2} = {_die1 + _die2}" + (doubles ? " (¡dobles!)" : "");
            StartCoroutine(MoveRoutine(_die1 + _die2, doubles));
        }

        IEnumerator MoveRoutine(int steps, bool doubles)
        {
            _state = GameState.Moving;

            for (int s = 0; s < steps; s++)
            {
                int prev = Current.Position;
                Current.Position = (Current.Position + 1) % BoardGenerator.TileCount;

                if (Current.Position == 0 && prev != 0)
                {
                    Current.Money += Salary;
                    _log = $"{Current.Name} pasa por SALIDA y cobra ${Salary}.";
                }

                Current.Token.transform.position = TokenWorldPos(Current.Position, _currentIndex);
                yield return new WaitForSeconds(0.12f);
            }

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
                        _pendingTile = tile;
                        _state = GameState.Decision;
                        _log = $"{tile.Name} está libre. Precio: ${tile.Price}.";
                        return;
                    }
                    if (tile.OwnerIndex != _currentIndex)
                    {
                        int rent = CalcRent(tile);
                        Pay(Current, _players[tile.OwnerIndex], rent);
                        _log = $"{Current.Name} paga ${rent} de renta a {_players[tile.OwnerIndex].Name} por {tile.Name}.";
                    }
                    else
                    {
                        _log = $"{Current.Name} visita su propiedad {tile.Name}.";
                    }
                    break;

                case TileType.Tax:
                    Pay(Current, null, tile.Price);
                    _log = $"{Current.Name} paga ${tile.Price} de {tile.Name}.";
                    break;

                case TileType.Chance:
                case TileType.Community:
                    ApplyCard(tile.Type);
                    break;

                case TileType.GoToJail:
                    _log = $"{Current.Name} cae en ¡VE AL CRUNCH! y pierde su próximo turno.";
                    SendToJail(Current);
                    break;

                case TileType.Start:
                case TileType.Jail:
                case TileType.FreeParking:
                default:
                    _log = $"{Current.Name} descansa en {tile.Name}.";
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
            return fullGroup ? tile.BaseRent * 2 : tile.BaseRent;
        }

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

        void ApplyCard(TileType type)
        {
            string[] goodMsgs =
            {
                "¡Tu demo reel se hace viral! Cobra ${0}.",
                "Un cliente aprueba el proyecto sin cambios. Cobra ${0}.",
                "Bono por entrega anticipada: ${0}."
            };
            string[] badMsgs =
            {
                "Se corrompió el archivo del proyecto. Paga ${0}.",
                "Renovación de licencias de software: paga ${0}.",
                "El cliente pide 'un pequeño cambio'... Paga ${0}."
            };

            bool good = Random.value > 0.45f;
            int amount = Random.Range(2, 11) * 10;
            string source = type == TileType.Chance ? "SUERTE" : "COMUNIDAD";

            if (good)
            {
                Current.Money += amount;
                _log = $"[{source}] " + string.Format(goodMsgs[Random.Range(0, goodMsgs.Length)], amount);
            }
            else
            {
                Pay(Current, null, amount);
                _log = $"[{source}] " + string.Format(badMsgs[Random.Range(0, badMsgs.Length)], amount);
            }
        }

        void SendToJail(PlayerState p)
        {
            p.Position = 10;
            p.RestTurns = RestPenaltyTurns;
            p.Token.transform.position = TokenWorldPos(10, _players.IndexOf(p));
        }

        void Pay(PlayerState from, PlayerState to, int amount)
        {
            from.Money -= amount;
            if (to != null) to.Money += amount;
            if (from.Money < 0) Bankrupt(from);
        }

        void Bankrupt(PlayerState p)
        {
            p.Bankrupt = true;
            foreach (int idx in p.OwnedTiles)
                Board.Tiles[idx].OwnerIndex = -1;
            p.OwnedTiles.Clear();
            p.Token.SetActive(false);
            _log = $"¡{p.Name} entra en bancarrota y queda fuera!";

            int alive = 0;
            PlayerState winner = null;
            foreach (var pl in _players)
                if (!pl.Bankrupt) { alive++; winner = pl; }

            if (alive <= 1 && winner != null)
            {
                _state = GameState.GameOver;
                _log = $"🏆 ¡{winner.Name} gana la partida con ${winner.Money}!";
            }
        }

        void BuyPending()
        {
            if (_pendingTile == null) return;
            Current.Money -= _pendingTile.Price;
            _pendingTile.OwnerIndex = _currentIndex;
            Current.OwnedTiles.Add(Current.Position);
            _log = $"{Current.Name} compra {_pendingTile.Name} por ${_pendingTile.Price}.";
            _pendingTile = null;

            if (Current.Money < 0) { Bankrupt(Current); }
            EndOfMove(_die1 == _die2);
        }

        void SkipPending()
        {
            _log = $"{Current.Name} decide no comprar {_pendingTile.Name}.";
            _pendingTile = null;
            EndOfMove(_die1 == _die2);
        }

        void EndOfMove(bool doubles)
        {
            if (_state == GameState.GameOver) return;

            if (doubles && !Current.Bankrupt)
            {
                _state = GameState.WaitingRoll;
                _log += $" {Current.Name} repite turno por dobles.";
                return;
            }

            NextPlayer();
        }

        void NextPlayer()
        {
            if (_state == GameState.GameOver) return;

            do { _currentIndex = (_currentIndex + 1) % _players.Count; }
            while (_players[_currentIndex].Bankrupt);

            _doublesInARow = 0;
            _state = GameState.WaitingRoll;
        }

        // ---------- UI (IMGUI provisional) ----------

        void OnGUI()
        {
            GUI.skin.label.fontSize = 16;
            GUI.skin.button.fontSize = 16;
            GUI.skin.box.fontSize = 14;

            if (_state == GameState.Setup)
            {
                GUILayout.BeginArea(new Rect(Screen.width / 2f - 160, Screen.height / 2f - 100, 320, 220), GUI.skin.box);
                GUILayout.Label("MONOPOLY PlanA — Prototipo");
                GUILayout.Space(10);
                GUILayout.Label("¿Cuántos jugadores?");
                for (int n = 2; n <= 4; n++)
                    if (GUILayout.Button($"{n} jugadores", GUILayout.Height(36)))
                        StartGame(n);
                GUILayout.EndArea();
                return;
            }

            // Panel de estado
            GUILayout.BeginArea(new Rect(10, 10, 280, 40 + _players.Count * 26), GUI.skin.box);
            foreach (var p in _players)
            {
                string marker = p == Current && _state != GameState.GameOver ? "► " : "   ";
                string status = p.Bankrupt ? " (fuera)" : $"  ${p.Money}  [{p.OwnedTiles.Count} prop.]";
                GUILayout.Label(marker + p.Name + status);
            }
            GUILayout.EndArea();

            // Log
            GUI.Box(new Rect(10, Screen.height - 90, Screen.width - 20, 36), _log);

            // Acciones
            if (_state == GameState.WaitingRoll)
            {
                if (GUI.Button(new Rect(Screen.width / 2f - 90, Screen.height - 145, 180, 44), "🎲 Lanzar dados"))
                    RollDice();
            }
            else if (_state == GameState.Decision && _pendingTile != null)
            {
                if (GUI.Button(new Rect(Screen.width / 2f - 190, Screen.height - 145, 180, 44), $"Comprar (${_pendingTile.Price})"))
                    BuyPending();
                if (GUI.Button(new Rect(Screen.width / 2f + 10, Screen.height - 145, 180, 44), "Pasar"))
                    SkipPending();
            }
            else if (_state == GameState.GameOver)
            {
                if (GUI.Button(new Rect(Screen.width / 2f - 90, Screen.height - 145, 180, 44), "Jugar de nuevo"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
