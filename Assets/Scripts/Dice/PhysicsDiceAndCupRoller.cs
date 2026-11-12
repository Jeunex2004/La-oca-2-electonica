using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Oca2.Dice
{
    /// <summary>
    /// Sistema de Dado(s) 3D con Física real y Vaso / Cubilete interactivo controlado con el ratón.
    /// Permite al jugador:
    /// 1. Agarrar el Vaso 3D con el clic izquierdo del ratón sobre el tablero, moverlo y agitarlo
    ///    (los dados rebotan físicamente dentro del vaso según la fuerza del movimiento del mouse).
    /// 2. Soltar el clic izquierdo para volcar el vaso y lanzar los dados con física sobre el tablero.
    /// 3. Leer automáticamente la cara superior (1 a 6) de cada dado físico cuando se detienen
    ///    y mover la ficha de la Oca esa cantidad de casillas (aplicando mejoras del Árbol, objetos y hechizos).
    /// </summary>
    public class PhysicsDiceAndCupRoller : MonoBehaviour
    {
        public static PhysicsDiceAndCupRoller Instance { get; private set; }

        private const float BOARD_SURFACE_Y = 0.50f;
        private const float CUP_HOVER_HEIGHT = 1.45f;
        private const float ARENA_HALF_EXTENT = 6.2f;
        private const float DIE_SIZE = 0.72f;

        private GameObject arenaRoot;
        private BoxCollider floorCollider;
        private readonly List<Collider> arenaWallColliders = new List<Collider>();

        // Vaso / Cubilete 3D
        private GameObject cupRoot;
        private Rigidbody cupRigidbody;
        private Transform cupVisualTransform;
        private readonly List<Collider> cupWallColliders = new List<Collider>();
        private Collider cupBottomTrapCollider;
        private readonly List<Renderer> cupColorBandRenderers = new List<Renderer>();

        // Dados físicos 3D activos
        private class ActivePhysicsDie
        {
            public GameObject gameObject;
            public Transform transform;
            public Rigidbody rigidbody;
            public BoxCollider boxCollider;
        }

        private readonly List<ActivePhysicsDie> activeDice = new List<ActivePhysicsDie>();
        private PhysicsMaterial dicePhysMat;
        private PhysicsMaterial boardPhysMat;

        // Estado de interacción con el mouse
        private bool isCupReadyToGrab = false;
        private bool isDraggingCup = false;
        private bool isRollingInProgress = false;
        private Vector3 currentBoardCenter = new Vector3(31.2f, BOARD_SURFACE_Y, 0.4f);
        private Vector3 targetCupPosition;
        private Vector3 lastCupPosition;
        private Vector3 smoothedCupVelocity;
        private float accumulatedShakeDistance = 0f;

        private Action<List<int>> onDiceSettledCallback;
        private Action<string> onStatusPromptCallback;
        private Coroutine activeRollCoroutine;

        public bool IsCupReadyToGrab => isCupReadyToGrab;
        public bool IsDraggingCup => isDraggingCup;
        public bool IsRollingInProgress => isRollingInProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsurePhysicsMaterials();
        }

        private void EnsurePhysicsMaterials()
        {
            if (dicePhysMat == null)
            {
                dicePhysMat = new PhysicsMaterial("Oca2_DicePhysicsMat")
                {
                    bounciness = 0.48f,
                    dynamicFriction = 0.38f,
                    staticFriction = 0.38f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }

            if (boardPhysMat == null)
            {
                boardPhysMat = new PhysicsMaterial("Oca2_BoardPhysicsMat")
                {
                    bounciness = 0.35f,
                    dynamicFriction = 0.45f,
                    staticFriction = 0.45f,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Average
                };
            }
        }

        /// <summary>
        /// Prepara el Vaso 3D y la cantidad correspondiente de dados d6 (1d6 base + mejoras del Árbol / RedBull)
        /// sobre el tablero actual para que el jugador pueda agarrar y agitar el vaso con el mouse.
        /// </summary>
        public void PrepareCupAndDiceOnBoard(
            Vector3 boardWorldCenter,
            int diceCount,
            Color playerColor,
            Action<List<int>> onSettled,
            Action<string> onPrompt = null)
        {
            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            EnsurePhysicsMaterials();

            currentBoardCenter = new Vector3(boardWorldCenter.x, BOARD_SURFACE_Y, boardWorldCenter.z);
            onDiceSettledCallback = onSettled;
            onStatusPromptCallback = onPrompt;

            diceCount = Mathf.Clamp(diceCount, 1, 5);

            BuildOrRepositionArena(currentBoardCenter);
            BuildOrResetCup(currentBoardCenter, playerColor);
            SpawnPhysicsDiceInsideCup(diceCount);

            isCupReadyToGrab = true;
            isDraggingCup = false;
            isRollingInProgress = false;
            accumulatedShakeDistance = 0f;
            smoothedCupVelocity = Vector3.zero;

            onStatusPromptCallback?.Invoke(
                "¡Agarra el Vaso 3D en el tablero con el clic izquierdo del ratón, agítalo y suéltalo para lanzar el dado!");
        }

        /// <summary>
        /// Si el jugador pulsa el botón "Tirar Dado" sin arrastrar manualmente el vaso,
        /// agita automáticamente el vaso 3D en pantalla y vuelca los dados con física.
        /// </summary>
        public void TriggerAutoShakeAndThrow()
        {
            if (!isCupReadyToGrab || isRollingInProgress) return;

            isCupReadyToGrab = false;
            isDraggingCup = false;
            isRollingInProgress = true;

            if (activeRollCoroutine != null) StopCoroutine(activeRollCoroutine);
            activeRollCoroutine = StartCoroutine(AutoShakeAndThrowRoutine());
        }

        public void HideCupAndDice()
        {
            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            isCupReadyToGrab = false;
            isDraggingCup = false;
            isRollingInProgress = false;

            ClearActiveDice();
            if (cupRoot != null) cupRoot.SetActive(false);
            if (arenaRoot != null) arenaRoot.SetActive(false);
        }

        private void Update()
        {
            if (!isCupReadyToGrab || isRollingInProgress || cupRoot == null || !cupRoot.activeSelf)
                return;

            HandleMouseCupInteraction();
        }

        private void FixedUpdate()
        {
            if (!isDraggingCup || cupRigidbody == null) return;

            cupRigidbody.MovePosition(targetCupPosition);

            // Inclinación dinámica del vaso según la velocidad del mouse
            Vector3 horizVel = new Vector3(smoothedCupVelocity.x, 0f, smoothedCupVelocity.z);
            float tiltAngle = Mathf.Clamp(horizVel.magnitude * 1.4f, 0f, 26f);
            Quaternion targetRot = Quaternion.identity;
            if (horizVel.sqrMagnitude > 0.05f)
            {
                Vector3 tiltAxis = Vector3.Cross(Vector3.up, horizVel.normalized);
                targetRot = Quaternion.AngleAxis(tiltAngle, tiltAxis);
            }
            cupRigidbody.MoveRotation(Quaternion.Slerp(cupRigidbody.rotation, targetRot, Time.fixedDeltaTime * 16f));

            // Transmitir agitación física a los dados dentro del vaso cuando el jugador mueve el ratón
            float speed = horizVel.magnitude;
            if (speed > 1.2f)
            {
                foreach (var die in activeDice)
                {
                    if (die.rigidbody == null) continue;
                    Vector3 shakeForce = horizVel * 0.45f + UnityEngine.Random.insideUnitSphere * (speed * 0.35f);
                    shakeForce.y = Mathf.Clamp(shakeForce.y, -2f, 3.5f);
                    die.rigidbody.AddForce(shakeForce, ForceMode.VelocityChange);
                    die.rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * (speed * 1.8f), ForceMode.VelocityChange);
                }
            }
        }

        private void HandleMouseCupInteraction()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            bool pressedThisFrame = false;
            bool isHeld = false;
            bool releasedThisFrame = false;
            Vector2 screenPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                isHeld = mouse.leftButton.isPressed;
                releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
                screenPos = mouse.position.ReadValue();
            }
#else
            pressedThisFrame = Input.GetMouseButtonDown(0);
            isHeld = Input.GetMouseButton(0);
            releasedThisFrame = Input.GetMouseButtonUp(0);
            screenPos = Input.mousePosition;
#endif

            if (pressedThisFrame)
            {
                // Ignorar clics sobre botones de la interfaz UGUI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                Ray ray = cam.ScreenPointToRay(screenPos);
                Plane cupPlane = new Plane(Vector3.up, new Vector3(0f, currentBoardCenter.y + CUP_HOVER_HEIGHT, 0f));
                if (cupPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    float distToCup = Vector2.Distance(
                        new Vector2(hitPoint.x, hitPoint.z),
                        new Vector2(cupRoot.transform.position.x, cupRoot.transform.position.z));

                    if (distToCup <= 3.6f)
                    {
                        isDraggingCup = true;
                        accumulatedShakeDistance = 0f;
                        lastCupPosition = cupRoot.transform.position;
                        targetCupPosition = lastCupPosition;

                        if (Oca2.Board.BoardManager.Instance != null && Oca2.Board.BoardManager.Instance.CameraController != null)
                        {
                            Oca2.Board.BoardManager.Instance.CameraController.ResetToOverview();
                        }

                        foreach (var die in activeDice)
                        {
                            if (die.rigidbody != null) die.rigidbody.WakeUp();
                        }

                        onStatusPromptCallback?.Invoke(
                            "¡Sacude el Vaso con el ratón por el tablero y suelta el clic izquierdo para lanzar el dado!");
                    }
                }
            }

            if (isDraggingCup && isHeld)
            {
                Ray ray = cam.ScreenPointToRay(screenPos);
                Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, currentBoardCenter.y + CUP_HOVER_HEIGHT, 0f));
                if (dragPlane.Raycast(ray, out float enter))
                {
                    Vector3 rawTarget = ray.GetPoint(enter);
                    float maxDragRadius = ARENA_HALF_EXTENT - 1.4f;
                    Vector2 offset2D = new Vector2(rawTarget.x - currentBoardCenter.x, rawTarget.z - currentBoardCenter.z);
                    if (offset2D.magnitude > maxDragRadius)
                    {
                        offset2D = offset2D.normalized * maxDragRadius;
                    }

                    targetCupPosition = new Vector3(
                        currentBoardCenter.x + offset2D.x,
                        currentBoardCenter.y + CUP_HOVER_HEIGHT,
                        currentBoardCenter.z + offset2D.y);

                    Vector3 frameDelta = targetCupPosition - lastCupPosition;
                    accumulatedShakeDistance += frameDelta.magnitude;

                    Vector3 instVel = frameDelta / Mathf.Max(0.001f, Time.deltaTime);
                    smoothedCupVelocity = Vector3.Lerp(smoothedCupVelocity, instVel, Time.deltaTime * 15f);
                    lastCupPosition = targetCupPosition;

                    // Mantener los dados dentro de la cavidad del vaso por seguridad ante movimientos bruscos del mouse
                    KeepDiceInsideCupBounds(targetCupPosition);
                }
            }

            if (isDraggingCup && releasedThisFrame)
            {
                isDraggingCup = false;
                isCupReadyToGrab = false;
                isRollingInProgress = true;

                if (activeRollCoroutine != null) StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = StartCoroutine(ReleaseAndThrowDiceRoutine(smoothedCupVelocity));
            }
        }

        private void KeepDiceInsideCupBounds(Vector3 cupCenter)
        {
            float maxInnerRadius = 0.65f;
            for (int i = 0; i < activeDice.Count; i++)
            {
                var die = activeDice[i];
                if (die.rigidbody == null) continue;

                Vector3 pos = die.transform.position;
                Vector2 horizOffset = new Vector2(pos.x - cupCenter.x, pos.z - cupCenter.z);
                if (horizOffset.magnitude > maxInnerRadius)
                {
                    horizOffset = horizOffset.normalized * maxInnerRadius;
                }
                float clampedY = Mathf.Clamp(pos.y, cupCenter.y - 0.65f, cupCenter.y + 0.65f);
                die.transform.position = new Vector3(cupCenter.x + horizOffset.x, clampedY, cupCenter.z + horizOffset.y);
            }
        }

        private IEnumerator AutoShakeAndThrowRoutine()
        {
            if (Oca2.Board.BoardManager.Instance != null && Oca2.Board.BoardManager.Instance.CameraController != null)
            {
                Oca2.Board.BoardManager.Instance.CameraController.ResetToOverview();
            }

            onStatusPromptCallback?.Invoke("¡Agitando el Vaso 3D y lanzando los dados con física!");

            Vector3 basePos = cupRoot.transform.position;
            float shakeDuration = 0.52f;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float phase = elapsed * 28f;
                Vector3 shakeOffset = new Vector3(Mathf.Sin(phase) * 0.85f, Mathf.Abs(Mathf.Cos(phase * 0.5f)) * 0.25f, Mathf.Cos(phase * 0.8f) * 0.65f);
                Vector3 nextPos = basePos + shakeOffset;

                cupRoot.transform.position = nextPos;
                cupRoot.transform.rotation = Quaternion.Euler(Mathf.Cos(phase) * 18f, 0f, -Mathf.Sin(phase) * 22f);
                KeepDiceInsideCupBounds(nextPos);

                foreach (var die in activeDice)
                {
                    if (die.rigidbody != null)
                    {
                        die.rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * 12f, ForceMode.VelocityChange);
                    }
                }

                yield return null;
            }

            Vector3 autoThrowVel = new Vector3(
                UnityEngine.Random.Range(-4.5f, 4.5f),
                0f,
                UnityEngine.Random.Range(-4.5f, 4.5f));

            yield return StartCoroutine(ReleaseAndThrowDiceRoutine(autoThrowVel));
        }

        private IEnumerator ReleaseAndThrowDiceRoutine(Vector3 releaseVelocity)
        {
            onStatusPromptCallback?.Invoke("¡Vaso volcado! Esperando a que el dado físico se detenga sobre el tablero...");

            // 1. Desactivar los colliders del vaso para liberar los dados sobre el tablero
            SetCupCollidersEnabled(false);

            // 2. Impulsar los dados físicamente sobre el tablero con fuerza y rotación
            Vector3 throwDir = new Vector3(releaseVelocity.x, 0f, releaseVelocity.z);
            if (throwDir.magnitude < 2.5f)
            {
                Vector3 toCenter = (currentBoardCenter - cupRoot.transform.position);
                toCenter.y = 0f;
                throwDir = toCenter.sqrMagnitude > 0.2f
                    ? toCenter.normalized * 4.2f
                    : new Vector3(UnityEngine.Random.Range(-3.5f, 3.5f), 0f, UnityEngine.Random.Range(-3.5f, 3.5f));
            }
            else if (throwDir.magnitude > 11f)
            {
                throwDir = throwDir.normalized * 11f;
            }

            for (int i = 0; i < activeDice.Count; i++)
            {
                var die = activeDice[i];
                if (die.rigidbody == null) continue;

                die.rigidbody.isKinematic = false;
                die.rigidbody.WakeUp();

                // Posicionar ligeramente escalonados bajo la boca del vaso para que choquen entre sí con naturalidad
                Vector3 spread = new Vector3((i - (activeDice.Count - 1) * 0.5f) * 0.82f, 0.15f * i, UnityEngine.Random.Range(-0.25f, 0.25f));
                die.transform.position = cupRoot.transform.position + spread;
                die.transform.rotation = UnityEngine.Random.rotation;

                Vector3 impulse = throwDir * UnityEngine.Random.Range(0.85f, 1.15f)
                                  + new Vector3(UnityEngine.Random.Range(-1.8f, 1.8f), -3.2f, UnityEngine.Random.Range(-1.8f, 1.8f));
                die.rigidbody.linearVelocity = impulse;
                die.rigidbody.angularVelocity = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(16f, 32f);
            }

            // 3. Animar el Vaso 3D volcándose y retirándose hacia arriba para dejar ver los dados
            Vector3 cupStartPos = cupRoot.transform.position;
            Quaternion cupStartRot = cupRoot.transform.rotation;
            Vector3 cupLiftPos = cupStartPos + new Vector3(-1.8f, 3.6f, -1.5f);
            Quaternion cupTiltRot = Quaternion.Euler(75f, 25f, -55f);

            float cupAnimTime = 0.38f;
            float t = 0f;
            while (t < cupAnimTime)
            {
                t += Time.deltaTime;
                float norm = Mathf.Clamp01(t / cupAnimTime);
                cupRoot.transform.position = Vector3.Lerp(cupStartPos, cupLiftPos, norm);
                cupRoot.transform.rotation = Quaternion.Slerp(cupStartRot, cupTiltRot, norm);
                yield return null;
            }
            cupRoot.SetActive(false);

            // 4. Esperar a que todos los dados físicos terminen de rebotar y queden quietos sobre el tablero
            float settleTimer = 0f;
            float maxWaitTime = 3.4f;
            float elapsedWait = 0f;

            while (elapsedWait < maxWaitTime)
            {
                elapsedWait += Time.deltaTime;
                bool allStill = true;

                foreach (var die in activeDice)
                {
                    if (die.rigidbody == null) continue;

                    // Si un dado quedó inclinado en una arista después de 1.8s, darle un pequeño empujón para que asiente plano
                    if (elapsedWait > 1.8f && die.rigidbody.linearVelocity.sqrMagnitude < 0.05f)
                    {
                        float bestDot = GetBestAlignmentDot(die.transform);
                        if (bestDot < 0.88f)
                        {
                            die.rigidbody.AddTorque(UnityEngine.Random.onUnitSphere * 4f, ForceMode.VelocityChange);
                        }
                    }

                    if (die.rigidbody.linearVelocity.sqrMagnitude > 0.015f ||
                        die.rigidbody.angularVelocity.sqrMagnitude > 0.03f ||
                        die.transform.position.y > currentBoardCenter.y + DIE_SIZE * 0.95f)
                    {
                        allStill = false;
                    }
                }

                if (allStill)
                {
                    settleTimer += Time.deltaTime;
                    if (settleTimer >= 0.22f) break;
                }
                else
                {
                    settleTimer = 0f;
                }

                yield return null;
            }

            // 5. Leer la cara superior (1..6) de cada dado físico y alinear su rotación final
            List<int> rolledResults = new List<int>();
            for (int i = 0; i < activeDice.Count; i++)
            {
                var die = activeDice[i];
                int faceVal = ReadTopFaceValue(die.transform);
                rolledResults.Add(faceVal);

                if (die.rigidbody != null)
                {
                    die.rigidbody.linearVelocity = Vector3.zero;
                    die.rigidbody.angularVelocity = Vector3.zero;
                    die.rigidbody.isKinematic = true;
                }
            }

            string facesStr = string.Join(" + ", rolledResults);
            onStatusPromptCallback?.Invoke($"¡Dado físico detenido! Cara superior obtenida: {facesStr}");

            // Pausa breve para que el jugador vea los dados físicos posados sobre el tablero antes de mover la Oca
            yield return new WaitForSeconds(0.75f);

            isRollingInProgress = false;
            var callback = onDiceSettledCallback;
            onDiceSettledCallback = null;

            HideCupAndDice();
            callback?.Invoke(rolledResults);
        }

        /// <summary>
        /// Determina qué cara del dado (1 a 6) apunta hacia arriba (Vector3.up) comparando las 6 normales locales:
        /// +Y = 1, -Y = 6, +Z = 2, -Z = 5, +X = 3, -X = 4 (caras opuestas suman 7).
        /// </summary>
        public static int ReadTopFaceValue(Transform dieTransform)
        {
            if (dieTransform == null) return 1;

            Vector3 up = Vector3.up;
            float bestDot = -2f;
            int bestFace = 1;

            CheckFace(dieTransform.up, 1, up, ref bestDot, ref bestFace);
            CheckFace(-dieTransform.up, 6, up, ref bestDot, ref bestFace);
            CheckFace(dieTransform.forward, 2, up, ref bestDot, ref bestFace);
            CheckFace(-dieTransform.forward, 5, up, ref bestDot, ref bestFace);
            CheckFace(dieTransform.right, 3, up, ref bestDot, ref bestFace);
            CheckFace(-dieTransform.right, 4, up, ref bestDot, ref bestFace);

            return bestFace;
        }

        private static float GetBestAlignmentDot(Transform dieTransform)
        {
            Vector3 up = Vector3.up;
            float bestDot = -2f;
            int dummy = 1;
            CheckFace(dieTransform.up, 1, up, ref bestDot, ref dummy);
            CheckFace(-dieTransform.up, 6, up, ref bestDot, ref dummy);
            CheckFace(dieTransform.forward, 2, up, ref bestDot, ref dummy);
            CheckFace(-dieTransform.forward, 5, up, ref bestDot, ref dummy);
            CheckFace(dieTransform.right, 3, up, ref bestDot, ref dummy);
            CheckFace(-dieTransform.right, 4, up, ref bestDot, ref dummy);
            return bestDot;
        }

        private static void CheckFace(Vector3 worldNormal, int faceValue, Vector3 up, ref float bestDot, ref int bestFace)
        {
            float d = Vector3.Dot(worldNormal, up);
            if (d > bestDot)
            {
                bestDot = d;
                bestFace = faceValue;
            }
        }

        // =====================================================================
        // CONSTRUCCIÓN 3D DE LA ARENA DEL TABLERO, EL VASO / CUBILETE Y LOS DADOS
        // =====================================================================

        private void BuildOrRepositionArena(Vector3 boardCenter)
        {
            if (arenaRoot == null)
            {
                arenaRoot = new GameObject("DicePhysics_BoardArena");
                DontDestroyOnLoad(arenaRoot);

                // Piso liso invisible exactamente al nivel de la superficie del tablero
                GameObject floorObj = new GameObject("Arena_Floor");
                floorObj.transform.SetParent(arenaRoot.transform, false);
                floorObj.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                floorCollider = floorObj.AddComponent<BoxCollider>();
                floorCollider.size = new Vector3(ARENA_HALF_EXTENT * 2.2f, 0.30f, ARENA_HALF_EXTENT * 2.2f);
                floorCollider.material = boardPhysMat;

                // 4 Paredes perimetrales invisibles para que los dados reboten dentro del tablero sin caer al vacío
                Vector3[] wallPositions =
                {
                    new Vector3(0f, 2.5f, ARENA_HALF_EXTENT),
                    new Vector3(0f, 2.5f, -ARENA_HALF_EXTENT),
                    new Vector3(ARENA_HALF_EXTENT, 2.5f, 0f),
                    new Vector3(-ARENA_HALF_EXTENT, 2.5f, 0f)
                };
                Vector3[] wallSizes =
                {
                    new Vector3(ARENA_HALF_EXTENT * 2.2f, 5.5f, 0.4f),
                    new Vector3(ARENA_HALF_EXTENT * 2.2f, 5.5f, 0.4f),
                    new Vector3(0.4f, 5.5f, ARENA_HALF_EXTENT * 2.2f),
                    new Vector3(0.4f, 5.5f, ARENA_HALF_EXTENT * 2.2f)
                };

                for (int i = 0; i < 4; i++)
                {
                    GameObject wObj = new GameObject($"Arena_Wall_{i}");
                    wObj.transform.SetParent(arenaRoot.transform, false);
                    wObj.transform.localPosition = wallPositions[i];
                    BoxCollider bc = wObj.AddComponent<BoxCollider>();
                    bc.size = wallSizes[i];
                    bc.material = boardPhysMat;
                    arenaWallColliders.Add(bc);
                }
            }

            arenaRoot.SetActive(true);
            arenaRoot.transform.position = boardCenter;
        }

        private void BuildOrResetCup(Vector3 boardCenter, Color playerColor)
        {
            if (cupRoot == null)
            {
                cupRoot = new GameObject("DiceCup_Vaso3D");
                DontDestroyOnLoad(cupRoot);

                cupRigidbody = cupRoot.AddComponent<Rigidbody>();
                cupRigidbody.isKinematic = true;
                cupRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

                // Contenedor visual del Vaso / Cubilete (boca abajo mientras agita los dados)
                GameObject visRoot = new GameObject("Cup_Visuals");
                visRoot.transform.SetParent(cupRoot.transform, false);
                cupVisualTransform = visRoot.transform;

                Material woodMat = CreateLitMaterial(new Color(0.38f, 0.22f, 0.11f), 0.35f);
                Material innerDarkMat = CreateLitMaterial(new Color(0.14f, 0.09f, 0.06f), 0.15f);
                Material bandMat = CreateLitMaterial(playerColor, 0.55f);

                // Construir paredes curvas del vaso con 16 duelas/segmentos alrededor del círculo
                int segments = 16;
                float radius = 1.02f;
                float height = 2.15f;
                for (int s = 0; s < segments; s++)
                {
                    float angle = (s / (float)segments) * Mathf.PI * 2f;
                    float deg = angle * Mathf.Rad2Deg;
                    Vector3 pos = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);

                    GameObject stave = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stave.name = $"CupStave_{s}";
                    stave.transform.SetParent(visRoot.transform, false);
                    stave.transform.localPosition = pos;
                    stave.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
                    stave.transform.localScale = new Vector3(0.45f, height, 0.14f);
                    Destroy(stave.GetComponent<Collider>());
                    stave.GetComponent<Renderer>().sharedMaterial = woodMat;
                }

                // Tapa superior del vaso invertido (la base del cubilete)
                GameObject topCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                topCap.name = "Cup_TopBase";
                topCap.transform.SetParent(visRoot.transform, false);
                topCap.transform.localPosition = new Vector3(0f, height * 0.48f, 0f);
                topCap.transform.localScale = new Vector3(radius * 2.12f, 0.08f, radius * 2.12f);
                Destroy(topCap.GetComponent<Collider>());
                topCap.GetComponent<Renderer>().sharedMaterial = woodMat;

                // Fondo interior oscuro
                GameObject innerCap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                innerCap.name = "Cup_InnerLining";
                innerCap.transform.SetParent(visRoot.transform, false);
                innerCap.transform.localPosition = new Vector3(0f, height * 0.42f, 0f);
                innerCap.transform.localScale = new Vector3(radius * 1.90f, 0.05f, radius * 1.90f);
                Destroy(innerCap.GetComponent<Collider>());
                innerCap.GetComponent<Renderer>().sharedMaterial = innerDarkMat;

                // Dos aros decorativos con el color del jugador en turno
                float[] bandHeights = { 0.45f, -0.55f };
                foreach (float bh in bandHeights)
                {
                    GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    band.name = "Cup_PlayerColorBand";
                    band.transform.SetParent(visRoot.transform, false);
                    band.transform.localPosition = new Vector3(0f, bh, 0f);
                    band.transform.localScale = new Vector3(radius * 2.22f, 0.09f, radius * 2.22f);
                    Destroy(band.GetComponent<Collider>());
                    Renderer br = band.GetComponent<Renderer>();
                    br.material = bandMat;
                    cupColorBandRenderers.Add(br);
                }

                // Colliders físicos del Vaso (12 paredes circulares + techo + trampa inferior mientras se agita)
                GameObject colRoot = new GameObject("Cup_PhysicsColliders");
                colRoot.transform.SetParent(cupRoot.transform, false);

                int wallCols = 12;
                float colRadius = 1.05f;
                for (int c = 0; c < wallCols; c++)
                {
                    float angle = (c / (float)wallCols) * Mathf.PI * 2f;
                    float deg = angle * Mathf.Rad2Deg;
                    GameObject wColObj = new GameObject($"CupWallCol_{c}");
                    wColObj.transform.SetParent(colRoot.transform, false);
                    wColObj.transform.localPosition = new Vector3(Mathf.Sin(angle) * colRadius, 0f, Mathf.Cos(angle) * colRadius);
                    wColObj.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
                    BoxCollider bc = wColObj.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.68f, 2.5f, 0.35f);
                    bc.material = dicePhysMat;
                    cupWallColliders.Add(bc);
                }

                GameObject roofColObj = new GameObject("CupRoofCol");
                roofColObj.transform.SetParent(colRoot.transform, false);
                roofColObj.transform.localPosition = new Vector3(0f, 1.15f, 0f);
                BoxCollider roofCol = roofColObj.AddComponent<BoxCollider>();
                roofCol.size = new Vector3(2.4f, 0.35f, 2.4f);
                roofCol.material = dicePhysMat;
                cupWallColliders.Add(roofCol);

                GameObject bottomColObj = new GameObject("CupBottomTrapCol");
                bottomColObj.transform.SetParent(colRoot.transform, false);
                bottomColObj.transform.localPosition = new Vector3(0f, -1.05f, 0f);
                cupBottomTrapCollider = bottomColObj.AddComponent<BoxCollider>();
                ((BoxCollider)cupBottomTrapCollider).size = new Vector3(2.4f, 0.30f, 2.4f);
                cupBottomTrapCollider.material = dicePhysMat;
                cupWallColliders.Add(cupBottomTrapCollider);
            }

            // Actualizar el color del aro del vaso al color del jugador activo
            foreach (var r in cupColorBandRenderers)
            {
                if (r != null && r.material != null)
                {
                    r.material.color = playerColor;
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", playerColor);
                }
            }

            cupRoot.SetActive(true);
            SetCupCollidersEnabled(true);

            Vector3 initialCupPos = boardCenter + new Vector3(0f, CUP_HOVER_HEIGHT, -1.2f);
            cupRoot.transform.position = initialCupPos;
            cupRoot.transform.rotation = Quaternion.identity;
            if (cupRigidbody != null)
            {
                cupRigidbody.position = initialCupPos;
                cupRigidbody.rotation = Quaternion.identity;
            }
            targetCupPosition = initialCupPos;
            lastCupPosition = initialCupPos;
        }

        private void SetCupCollidersEnabled(bool enabled)
        {
            foreach (var c in cupWallColliders)
            {
                if (c != null) c.enabled = enabled;
            }
        }

        private void SpawnPhysicsDiceInsideCup(int count)
        {
            ClearActiveDice();

            // Obtener todos los colliders de la escena que NO pertenecen a nuestro sistema de dados
            // para ignorar colisiones con casillas/fichas y garantizar que los dados rueden sobre una superficie 100% lisa
            Collider[] allSceneColliders = FindObjectsByType<Collider>();
            HashSet<Collider> allowedColliders = new HashSet<Collider>(cupWallColliders);
            if (floorCollider != null) allowedColliders.Add(floorCollider);
            foreach (var w in arenaWallColliders)
            {
                if (w != null) allowedColliders.Add(w);
            }

            Vector3 cupPos = cupRoot.transform.position;

            for (int i = 0; i < count; i++)
            {
                GameObject dieObj = BuildSingle3DDie(i + 1);
                DontDestroyOnLoad(dieObj);

                Vector3 localOffset = new Vector3(
                    (i % 2 == 0 ? -0.22f : 0.22f) * (i > 0 ? 1f : 0f),
                    -0.25f + (i * 0.22f),
                    (i >= 2 ? 0.22f : -0.12f));

                dieObj.transform.position = cupPos + localOffset;
                dieObj.transform.rotation = UnityEngine.Random.rotation;

                BoxCollider dieCol = dieObj.GetComponent<BoxCollider>();
                Rigidbody dieRb = dieObj.GetComponent<Rigidbody>();

                // Ignorar colisiones con objetos ajenos a la arena y al vaso
                foreach (var sc in allSceneColliders)
                {
                    if (sc != null && sc != dieCol && !allowedColliders.Contains(sc))
                    {
                        Physics.IgnoreCollision(dieCol, sc, true);
                    }
                }

                // Permitir colisión entre todos los dados activos entre sí
                foreach (var prevDie in activeDice)
                {
                    if (prevDie.boxCollider != null)
                    {
                        Physics.IgnoreCollision(dieCol, prevDie.boxCollider, false);
                    }
                }

                activeDice.Add(new ActivePhysicsDie
                {
                    gameObject = dieObj,
                    transform = dieObj.transform,
                    rigidbody = dieRb,
                    boxCollider = dieCol
                });
            }
        }

        /// <summary>
        /// Construye un Dado 3D de 6 caras con cuerpo blanco marfil, puntos negros (pips 1..6 en relieve)
        /// y componentes Rigidbody + BoxCollider.
        /// </summary>
        private GameObject BuildSingle3DDie(int index)
        {
            GameObject die = GameObject.CreatePrimitive(PrimitiveType.Cube);
            die.name = $"PhysicsDie3D_{index}";
            die.transform.localScale = Vector3.one * DIE_SIZE;

            Renderer rend = die.GetComponent<Renderer>();
            rend.sharedMaterial = CreateLitMaterial(new Color(0.97f, 0.96f, 0.92f), 0.55f);

            BoxCollider col = die.GetComponent<BoxCollider>();
            col.material = dicePhysMat;

            Rigidbody rb = die.AddComponent<Rigidbody>();
            rb.mass = 1.15f;
            rb.linearDamping = 0.25f;
            rb.angularDamping = 0.20f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Material pipMat = CreateLitMaterial(new Color(0.10f, 0.10f, 0.10f), 0.4f);
            Material redPipMat = CreateLitMaterial(new Color(0.85f, 0.14f, 0.14f), 0.5f); // Cara del 1 en rojo clásico

            // Añadir los puntos (pips) en las 6 caras del cubo:
            // +Y = 1, -Y = 6, +Z = 2, -Z = 5, +X = 3, -X = 4
            float d = 0.24f;
            AddFacePips(die.transform, Vector3.up, Vector3.forward, Vector3.right,
                new Vector2[] { Vector2.zero }, redPipMat, 0.18f);

            AddFacePips(die.transform, Vector3.down, Vector3.forward, Vector3.right,
                new Vector2[]
                {
                    new Vector2(-d, -d), new Vector2(-d, 0f), new Vector2(-d, d),
                    new Vector2(d, -d),  new Vector2(d, 0f),  new Vector2(d, d)
                }, pipMat);

            AddFacePips(die.transform, Vector3.forward, Vector3.up, Vector3.right,
                new Vector2[] { new Vector2(-d, -d), new Vector2(d, d) }, pipMat);

            AddFacePips(die.transform, Vector3.back, Vector3.up, Vector3.right,
                new Vector2[]
                {
                    new Vector2(-d, -d), new Vector2(-d, d),
                    new Vector2(0f, 0f),
                    new Vector2(d, -d),  new Vector2(d, d)
                }, pipMat);

            AddFacePips(die.transform, Vector3.right, Vector3.up, Vector3.forward,
                new Vector2[] { new Vector2(-d, -d), new Vector2(0f, 0f), new Vector2(d, d) }, pipMat);

            AddFacePips(die.transform, Vector3.left, Vector3.up, Vector3.forward,
                new Vector2[]
                {
                    new Vector2(-d, -d), new Vector2(-d, d),
                    new Vector2(d, -d),  new Vector2(d, d)
                }, pipMat);

            return die;
        }

        private static void AddFacePips(
            Transform dieTransform,
            Vector3 normal,
            Vector3 tangentU,
            Vector3 tangentV,
            Vector2[] uvCoords,
            Material mat,
            float pipScale = 0.135f)
        {
            for (int i = 0; i < uvCoords.Length; i++)
            {
                GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pip.name = "Pip";
                pip.transform.SetParent(dieTransform, false);
                Destroy(pip.GetComponent<Collider>());

                Vector3 localPos = normal * 0.502f + tangentU * uvCoords[i].x + tangentV * uvCoords[i].y;
                pip.transform.localPosition = localPos;
                pip.transform.localScale = new Vector3(pipScale, pipScale, pipScale);
                pip.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        private void ClearActiveDice()
        {
            foreach (var die in activeDice)
            {
                if (die.gameObject != null)
                {
                    Destroy(die.gameObject);
                }
            }
            activeDice.Clear();
        }

        private static Material CreateLitMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }
    }
}
