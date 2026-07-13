using UnityEngine;

namespace MonopolyPlanA
{
    public enum CoinRainMode
    {
        AnimatedSprite, // secuencia VFX/coin_rain_0..15 en un billboard
        Particles       // Particle System con la moneda VFX/Coin
    }

    /// <summary>
    /// Selector del VFX de lluvia de monedas (cuando un jugador GANA oro).
    /// Añádelo al Bootstrap de la escena Main (menú PlanA → Añadir CoinRainSettings)
    /// y elige el modo en el Inspector. Sin este componente se usa el sprite animado.
    /// </summary>
    public class CoinRainSettings : MonoBehaviour
    {
        [Header("Qué VFX usar al ganar oro")]
        public CoinRainMode mode = CoinRainMode.Particles;

        [Header("Tiempos")]
        [Tooltip("Duración de la lluvia de monedas (segundos)")]
        [Range(2f, 10f)] public float rainDuration = 3f;
        [Tooltip("Espera adicional tras el VFX antes de continuar el turno")]
        [Range(0f, 5f)] public float postDelay = 1f;

        [Header("Ajustes del Particle System")]
        [Tooltip("Monedas totales de la lluvia")]
        [Range(4, 80)] public int burstCoins = 26;
        [Tooltip("Tamaño de cada moneda en unidades de mundo")]
        [Range(0.05f, 1f)] public float coinSize = 0.26f;
        [Tooltip("Radio horizontal de la lluvia")]
        [Range(0.1f, 2f)] public float radius = 0.55f;
        [Tooltip("Gravedad (más alto = caen más rápido)")]
        [Range(0f, 6f)] public float gravity = 2.4f;
        [Tooltip("Vida de cada moneda en segundos")]
        [Range(0.4f, 3f)] public float life = 1.4f;
    }
}
