using UnityEngine;

namespace MonopolyPlanA
{
    /// <summary>
    /// Único componente de la escena Menu: crea un tablero decorativo de fondo,
    /// una cámara que orbita lentamente y el MenuManager (título + lobby).
    /// </summary>
    public class MenuBootstrap : MonoBehaviour
    {
        Transform _cam;
        float _angle;

        void Awake()
        {
            // Tablero decorativo de fondo
            var boardGo = new GameObject("BoardDecor");
            boardGo.AddComponent<BoardGenerator>().Build();

            // Cámara
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            _cam = cam.transform;

            // Luz
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }

            gameObject.AddComponent<MenuManager>();
        }

        void Update()
        {
            // Órbita lenta de fondo
            _angle += 7f * Time.deltaTime;
            Quaternion rot = Quaternion.Euler(50f, _angle, 0f);
            _cam.position = rot * new Vector3(0f, 0f, -33f);
            _cam.rotation = rot;
        }
    }
}
