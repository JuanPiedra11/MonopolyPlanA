using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    public class PlayerState
    {
        public string Name;
        public Color Color;
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
