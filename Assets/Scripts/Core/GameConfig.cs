using System.Collections.Generic;
using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>Configuración de un jugador elegida en el lobby.</summary>
    public class PlayerSetup
    {
        public string Name;
        public bool IsBot;
        public int ColorIndex;
        public int CharacterIndex; // índice en GameConfig.CharacterIds
    }

    /// <summary>Cómo termina la partida.</summary>
    public enum EndMode
    {
        Classic, // hasta que quede un solo jugador en pie
        Time,    // al agotarse el tiempo gana el de mayor patrimonio
        Rounds   // al completar N rondas gana el de mayor patrimonio
    }

    /// <summary>Config estática que sobrevive al cambio de escena Menu → Main.</summary>
    public static class GameConfig
    {
        public static readonly string[] CharacterIds = { "rey", "mago", "enano", "arquera" };
        public static readonly string[] CharacterNames = { "King", "Mage", "Dwarf", "Archer" };

        public static readonly List<PlayerSetup> Players = new List<PlayerSetup>();
        public static bool Configured => Players.Count >= 2;

        // Reglas de la partida (configurables en el menú)
        public static EndMode Mode = EndMode.Classic;
        public static int TimeLimitMin = 20; // minutos, si Mode == Time
        public static int RoundLimit = 20;   // rondas, si Mode == Rounds
        public static int StartGold = 1500;  // oro inicial de cada jugador

        public static void Reset() => Players.Clear();
    }

    /// <summary>Paleta de colores/fichas disponible en el lobby.</summary>
    public static class PlayerPalette
    {
        public static readonly Color[] Colors =
        {
            new Color(0.90f, 0.25f, 0.25f), // Rojo
            new Color(0.25f, 0.45f, 0.95f), // Azul
            new Color(0.25f, 0.80f, 0.35f), // Verde
            new Color(0.95f, 0.80f, 0.25f), // Amarillo
            new Color(0.65f, 0.35f, 0.85f), // Morado
            new Color(0.25f, 0.80f, 0.85f)  // Cian
        };

        public static readonly string[] Names =
        {
            "Red", "Blue", "Green", "Yellow", "Purple", "Cyan"
        };
    }
}
