using UnityEngine;

namespace MonopolyPlanA
{
    public enum CameraMode
    {
        Free,   // Orbital libre alrededor del tablero
        Top,    // Vista cenital (top-down)
        Follow  // Tercera persona siguiendo la ficha de un jugador
    }

    /// <summary>
    /// Cámara del juego con tres modos:
    /// - Libre:  clic derecho orbita, rueda zoom, WASD/clic medio desplaza.
    /// - Cenital: vista superior fija; rueda zoom, WASD/clic medio desplaza.
    /// - Seguir: tercera persona sobre una ficha; clic derecho orbita, rueda zoom.
    /// Atajos: V alterna Libre/Cenital · R resetea la vista del modo actual.
    /// (F = seguir al jugador del turno, gestionado por GameManager.)
    /// </summary>
    public class CameraOrbitController : MonoBehaviour
    {
        public static CameraOrbitController Instance { get; private set; }

        public CameraMode Mode { get; private set; } = CameraMode.Free;
        public Transform FollowTarget;

        public float OrbitSpeed = 4f;
        public float ZoomSpeed = 10f;
        public float PanSpeed = 14f;
        public float FollowSmooth = 8f;

        const float PanLimit = 14f;

        Vector3 _target = Vector3.zero;
        float _distance = 31f, _yaw = 0f, _pitch = 57f;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Apply(_target);
        }

        public void SetMode(CameraMode mode, Transform followTarget = null)
        {
            Mode = mode;
            if (followTarget != null) FollowTarget = followTarget;

            switch (mode)
            {
                case CameraMode.Free:
                    _distance = 31f; _pitch = 57f; _yaw = 0f; _target = Vector3.zero;
                    break;
                case CameraMode.Top:
                    _distance = 36f; _pitch = 89f; _yaw = 0f; _target = Vector3.zero;
                    break;
                case CameraMode.Follow:
                    _distance = 8f; _pitch = 40f;
                    break;
            }
        }

        public void ToggleTopFree()
        {
            SetMode(Mode == CameraMode.Top ? CameraMode.Free : CameraMode.Top);
        }

        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.V)) ToggleTopFree();
            if (Input.GetKeyDown(KeyCode.R)) SetMode(Mode); // reset del modo actual

            // Si el objetivo seguido desaparece (bancarrota), volver a vista libre
            if (Mode == CameraMode.Follow && (FollowTarget == null || !FollowTarget.gameObject.activeInHierarchy))
                SetMode(CameraMode.Free);

            // Zoom (todos los modos)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                float min = Mode == CameraMode.Follow ? 3f : 6f;
                float max = Mode == CameraMode.Follow ? 18f : 45f;
                _distance = Mathf.Clamp(_distance - scroll * ZoomSpeed, min, max);
            }

            // Orbitar con clic derecho (Libre y Seguir)
            if (Mode != CameraMode.Top && Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * OrbitSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * OrbitSpeed;
                _pitch = Mathf.Clamp(_pitch, Mode == CameraMode.Follow ? 10f : 15f, 89f);
            }

            // Desplazamiento (Libre y Cenital)
            if (Mode != CameraMode.Follow)
            {
                Vector3 pan = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
                if (Input.GetMouseButton(2))
                {
                    pan.x -= Input.GetAxis("Mouse X") * 1.5f;
                    pan.z -= Input.GetAxis("Mouse Y") * 1.5f;
                }
                if (pan.sqrMagnitude > 0.0001f)
                {
                    Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
                    _target += yawRot * pan * PanSpeed * Time.deltaTime;
                    _target.x = Mathf.Clamp(_target.x, -PanLimit, PanLimit);
                    _target.z = Mathf.Clamp(_target.z, -PanLimit, PanLimit);
                }
            }

            // Punto de enfoque
            Vector3 focus = _target;
            if (Mode == CameraMode.Follow && FollowTarget != null)
            {
                // Suavizado para que la cámara "persiga" a la ficha
                _target = Vector3.Lerp(_target, FollowTarget.position, FollowSmooth * Time.deltaTime);
                focus = _target;
            }

            Apply(focus);
        }

        void Apply(Vector3 focus)
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = focus + rot * new Vector3(0f, 0f, -_distance);
            transform.rotation = rot;
        }
    }
}
