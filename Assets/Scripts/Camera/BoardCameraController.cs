using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Oca2.Cameras
{
    /// <summary>
    /// Controlador de cámara 3D isométrica para juegos de mesa.
    /// Permite rotar a los 4 puntos cardinales con las teclas A y D (90° por pulsación),
    /// cambiar de tablero con las flechas Izquierda y Derecha,
    /// hacer paneo con el arrastre del ratón, zoom con la rueda y seguimiento de fichas.
    /// </summary>
    public class BoardCameraController : MonoBehaviour
    {
        [Header("Punto de Enfoque y Tableros")]
        [Tooltip("Transform del centro del tablero actual (o centro del mundo)")]
        [SerializeField] private Transform boardCenter;

        [Tooltip("Asigna aquí los Transforms del centro de los 4 tableros (Principal, Infierno 1, Infierno 2, Payaso)")]
        [SerializeField] private Transform[] allBoardCenters;

        [Tooltip("Objetivo actual a seguir (ej: la Oca activa en su turno)")]
        [SerializeField] private Transform followTarget;

        [Header("Rotación Cardinal (Teclas A / D)")]
        [Tooltip("Ángulo cardinal actual: 0=Norte(0°), 1=Este(90°), 2=Sur(180°), 3=Oeste(270°)")]
        [SerializeField] private int currentCardinalIndex = 0;

        [Tooltip("Tiempo de suavizado para girar entre ángulos cardinales")]
        [SerializeField] private float rotationSmoothTime = 0.25f;

        [Tooltip("Inclinación vertical de la vista isométrica (pitch) en grados")]
        [Range(20f, 85f)]
        [SerializeField] private float pitchAngle = 50f;

        [Header("Zoom")]
        [SerializeField] private float defaultDistance = 18f;
        [SerializeField] private float minDistance = 6f;
        [SerializeField] private float maxDistance = 32f;
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float zoomSmoothTime = 0.15f;

        [Header("Paneo y Movimiento")]
        [SerializeField] private float panSpeed = 12f;
        [SerializeField] private float followSmoothTime = 0.2f;
        [SerializeField] private Vector2 panLimits = new Vector2(15f, 15f);

        // Variables internas de estado y suavizado
        private Vector3 currentPivot;
        private Vector3 targetPivot;
        private Vector3 pivotVelocity;

        private float currentYaw;
        private float targetYaw;
        private float yawVelocity;

        private float currentDistance;
        private float targetDistance;
        private float distanceVelocity;

        private bool isFollowingTarget = false;
        private int currentBoardIndex = 0;

        public int CurrentCardinalIndex => currentCardinalIndex;
        public float CurrentYaw => currentYaw;
        public bool IsFollowingTarget => isFollowingTarget;

        private void Awake()
        {
            Vector3 initialPos = boardCenter != null ? boardCenter.position : Vector3.zero;
            currentPivot = initialPos;
            targetPivot = initialPos;

            targetYaw = currentCardinalIndex * 90f;
            currentYaw = targetYaw;

            currentDistance = defaultDistance;
            targetDistance = defaultDistance;

            UpdateCameraTransformImmediate();
        }

        private void Update()
        {
            if (Oca2.Core.GameSessionManager.Instance != null && !Oca2.Core.GameSessionManager.Instance.IsMatchActive)
            {
                // Mientras se está en el Menú Principal / Selección de Jugador, girar suavemente alrededor del tablero
                targetYaw += 6f * Time.deltaTime;
                UpdateCameraTransform();
                return;
            }

            HandleInputs();
            UpdateCameraTransform();
        }

        /// <summary>
        /// Procesa las entradas de teclado y ratón usando el nuevo Input System (con fallback de ser necesario).
        /// </summary>
        private void HandleInputs()
        {
            float panX = 0f;
            float panZ = 0f;
            float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Rotación cardinal de 90° con A y D
                if (keyboard.dKey.wasPressedThisFrame)
                {
                    RotateCardinal(1); // Girar 90° horario
                }
                else if (keyboard.aKey.wasPressedThisFrame)
                {
                    RotateCardinal(-1); // Girar 90° antihorario
                }

                // Cambiar de tablero con Flecha Izquierda y Derecha
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    CycleBoard(1);
                }
                else if (keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    CycleBoard(-1);
                }

                // Restablecer vista general con barra espaciadora
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    ResetToOverview();
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // Zoom con la rueda del ratón
                scroll = mouse.scroll.ReadValue().y;

                // Paneo opcional con clic central o clic secundario
                if (mouse.middleButton.isPressed || mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    panX -= delta.x * 0.05f;
                    panZ -= delta.y * 0.05f;
                }
            }
#else
            // Fallback para Input Manager clásico
            if (Input.GetKeyDown(KeyCode.D)) RotateCardinal(1);
            if (Input.GetKeyDown(KeyCode.A)) RotateCardinal(-1);

            // Cambiar de tablero con Flecha Izquierda y Derecha (Fallback)
            if (Input.GetKeyDown(KeyCode.RightArrow)) CycleBoard(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) CycleBoard(-1);

            if (Input.GetKeyDown(KeyCode.Space)) ResetToOverview();

            scroll = Input.GetAxis("Mouse ScrollWheel") * 100f;
            if (Input.GetMouseButton(2) || Input.GetMouseButton(1))
            {
                panX -= Input.GetAxis("Mouse X") * 0.5f;
                panZ -= Input.GetAxis("Mouse Y") * 0.5f;
            }
#endif

            // Aplicar Zoom
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float zoomDelta = Mathf.Sign(scroll) * zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance - zoomDelta, minDistance, maxDistance);
            }

            // Aplicar Paneo manual (por ratón)
            if (Mathf.Abs(panX) > 0.01f || Mathf.Abs(panZ) > 0.01f)
            {
                // Si el jugador mueve la cámara manualmente, desactivar temporalmente el seguimiento
                isFollowingTarget = false;

                // Mover en relación a la rotación horizontal actual de la cámara
                Quaternion horizontalRotation = Quaternion.Euler(0, currentYaw, 0);
                Vector3 moveDir = horizontalRotation * new Vector3(panX, 0, panZ).normalized;

                float speedMult = (Oca2.Core.GameSessionManager.Instance != null)
                    ? Oca2.Core.GameSessionManager.Instance.Options.cameraSpeedMultiplier
                    : 1f;
                targetPivot += moveDir * (panSpeed * speedMult * Time.deltaTime);

                // Limitar el paneo dentro de los límites del tablero
                Vector3 center = boardCenter != null ? boardCenter.position : Vector3.zero;
                targetPivot.x = Mathf.Clamp(targetPivot.x, center.x - panLimits.x, center.x + panLimits.x);
                targetPivot.z = Mathf.Clamp(targetPivot.z, center.z - panLimits.y, center.z + panLimits.y);
            }

            // Si estamos siguiendo un objetivo activo
            if (isFollowingTarget && followTarget != null)
            {
                targetPivot = followTarget.position;
            }
        }

        /// <summary>
        /// Rota la cámara exactamente 90 grados al siguiente punto cardinal.
        /// </summary>
        public void RotateCardinal(int direction)
        {
            currentCardinalIndex = (currentCardinalIndex + direction) % 4;
            if (currentCardinalIndex < 0) currentCardinalIndex += 4;

            targetYaw = currentCardinalIndex * 90f;
        }

        /// <summary>
        /// Cambia al tablero siguiente o anterior usando el arreglo de tableros.
        /// </summary>
        private void CycleBoard(int direction)
        {
            if (allBoardCenters == null || allBoardCenters.Length == 0) return;

            currentBoardIndex = (currentBoardIndex + direction) % allBoardCenters.Length;
            
            // Ajuste para evitar índices negativos si se presiona la flecha izquierda
            if (currentBoardIndex < 0) currentBoardIndex += allBoardCenters.Length;

            SwitchBoard(allBoardCenters[currentBoardIndex]);
        }

        /// <summary>
        /// Enfoca la cámara en una ficha u objeto en particular.
        /// </summary>
        public void FocusOnTarget(Transform target, float desiredDistance = -1f)
        {
            followTarget = target;
            isFollowingTarget = true;
            if (desiredDistance > 0f)
            {
                targetDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
            }
        }

        /// <summary>
        /// Vuelve a enfocar el centro general del tablero activo.
        /// </summary>
        public void ResetToOverview()
        {
            isFollowingTarget = false;
            followTarget = null;
            targetPivot = boardCenter != null ? boardCenter.position : Vector3.zero;
            targetDistance = defaultDistance;
        }

        /// <summary>
        /// Transiciona suavemente la cámara a otro tablero.
        /// </summary>
        public void SwitchBoard(Transform newBoardCenter)
        {
            boardCenter = newBoardCenter;
            ResetToOverview();
        }

        private void UpdateCameraTransform()
        {
            // Interpolar suavemente el ángulo cardinal horizontal (Yaw)
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, rotationSmoothTime);

            // Interpolar distancia (Zoom)
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, zoomSmoothTime);

            // Interpolar posición del pivote (Seguimiento / Paneo)
            currentPivot = Vector3.SmoothDamp(currentPivot, targetPivot, ref pivotVelocity, followSmoothTime);

            // Calcular posición y orientación final de la cámara
            Quaternion rotation = Quaternion.Euler(pitchAngle, currentYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);

            transform.position = currentPivot + offset;
            transform.rotation = rotation;
        }

        private void UpdateCameraTransformImmediate()
        {
            currentYaw = targetYaw;
            currentDistance = targetDistance;
            currentPivot = targetPivot;

            Quaternion rotation = Quaternion.Euler(pitchAngle, currentYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);

            transform.position = currentPivot + offset;
            transform.rotation = rotation;
        }
    }
}