using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// ÚNICO componente que hay que colocar en la escena.
    /// Crea el tablero, el GameManager, la cámara y la luz automáticamente.
    /// Uso: escena vacía → GameObject vacío → añadir GameBootstrap → Play.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // Tablero
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<BoardGenerator>();
            board.Build();

            // GameManager
            var gmGo = new GameObject("GameManager");
            var gm = gmGo.AddComponent<GameManager>();
            gm.Board = board;

            SetupCamera();
            SetupLight();
        }

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.transform.position = new Vector3(0f, 26f, -17f);
            cam.transform.LookAt(new Vector3(0f, 0f, -1.5f));
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        void SetupLight()
        {
#if UNITY_2023_1_OR_NEWER
            if (FindFirstObjectByType<Light>() != null) return;
#else
            if (FindObjectOfType<Light>() != null) return;
#endif

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }
    }
}
