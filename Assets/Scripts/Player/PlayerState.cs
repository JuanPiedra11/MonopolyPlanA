using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    public class PlayerState
    {
        public string Name;
        public Color Color;
        public bool IsBot;
        public string CharacterId = "rey"; // rey | mago | enano | arquera
        public Texture2D Avatar;     // imagen de perfil para el HUD
        public int EliminationOrder; // 0 = sigue vivo; 1 = primero en caer...
        public int Money = 1500;
        public int Position;
        public int RestTurns;       // turnos que debe descansar (cárcel/crunch)
        public bool Bankrupt;
        public readonly List<int> OwnedTiles = new List<int>();
        public GameObject Token;

        public PlayerState(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
