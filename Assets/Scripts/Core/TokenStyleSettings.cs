using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// Elige si las fichas usan sprite 2D o modelo 3D con rig (por personaje).
    /// Añádelo al Bootstrap de la escena Main (menú PlanA → Añadir TokenStyleSettings)
    /// y cambia el toggle en el Inspector.
    /// </summary>
    public class TokenStyleSettings : MonoBehaviour
    {
        [Header("Rey")]
        [Tooltip("Usar el modelo low-poly con rig (Resources/Models/RMo_Rey) en vez del sprite 2D")]
        public bool rey3D = true;
        [Tooltip("Altura del cuerpo del rey 3D en unidades de mundo (2D usa 1.95)")]
        [Range(1f, 3f)] public float reyBodyHeight = 1.95f;
        [Tooltip("Ajuste fino vertical: + sube, - hunde (editable en vivo)")]
        [Range(-0.5f, 0.5f)] public float reyGroundOffset = 0f;

        public bool Use3D(string charId) => charId == "rey" && rey3D;

        public float BodyHeight(string charId) => reyBodyHeight;
    }
}
