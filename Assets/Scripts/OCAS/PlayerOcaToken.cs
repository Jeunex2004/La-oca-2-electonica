using System.Collections;
using UnityEngine;
using Oca2.Board;
using Oca2.Data;

namespace Oca2.Players
{
    /// <summary>
    /// Representa la ficha física 3D de un jugador sobre el tablero de La Oca 2.
    /// Contiene la base coloreada del jugador, el sprite 2D de la Oca seleccionada
    /// con efecto Billboard hacia la cámara, y su estado actual en el tablero.
    /// </summary>
    public class PlayerOcaToken : MonoBehaviour
    {
        [Header("Datos del Jugador")]
        [SerializeField] private PlayerSetupData playerData;
        [SerializeField] private BoardType currentBoard = BoardType.MainBoard;
        [SerializeField] private int currentTileIndex = 0;

        [Header("Referencias Visuales")]
        [SerializeField] private Renderer outerBaseRenderer;
        [SerializeField] private Renderer innerBaseRenderer;
        [SerializeField] private SpriteRenderer ocaSpriteRenderer;

        private Coroutine moveCoroutine;

        public PlayerSetupData PlayerData => playerData;
        public BoardType CurrentBoard { get => currentBoard; set => currentBoard = value; }
        public int CurrentTileIndex { get => currentTileIndex; set => currentTileIndex = value; }
        public bool IsMoving => moveCoroutine != null;

        /// <summary>
        /// Construye e inicializa la ficha 3D del jugador con su Oca y color elegidos.
        /// </summary>
        public void Initialize(PlayerSetupData setupData, Sprite ocaSprite)
        {
            playerData = setupData;
            currentBoard = BoardType.MainBoard;
            currentTileIndex = 0;
            gameObject.name = $"Ficha_J{setupData.playerIndex + 1}_{setupData.playerName}_{setupData.selectedOca}";

            BuildVisualHierarchyIfNeeded();

            // Aplicar color del jugador a la base exterior
            if (outerBaseRenderer != null)
            {
                Material baseMat = CreateColoredMaterial(setupData.tokenColor);
                outerBaseRenderer.material = baseMat;
            }

            if (innerBaseRenderer != null)
            {
                Color innerColor = Color.Lerp(setupData.tokenColor, Color.white, 0.45f);
                innerBaseRenderer.material = CreateColoredMaterial(innerColor);
            }

            // Aplicar sprite de la Oca elegida
            if (ocaSpriteRenderer != null && ocaSprite != null)
            {
                ocaSpriteRenderer.sprite = ocaSprite;

                // Ajustar escala automática según tamaño del sprite para que todas las Ocas midan ~1.1 unidades de alto
                float spriteHeight = ocaSprite.bounds.size.y;
                if (spriteHeight > 0.01f)
                {
                    float desiredWorldHeight = 1.15f;
                    float scale = desiredWorldHeight / spriteHeight;
                    ocaSpriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        private void BuildVisualHierarchyIfNeeded()
        {
            if (outerBaseRenderer == null)
            {
                GameObject outerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outerBase.name = "Base exterior";
                outerBase.transform.SetParent(transform, false);
                outerBase.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                outerBase.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
                Collider col = outerBase.GetComponent<Collider>();
                if (col != null) Destroy(col);
                outerBaseRenderer = outerBase.GetComponent<Renderer>();
            }

            if (innerBaseRenderer == null)
            {
                GameObject innerBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                innerBase.name = "Base interior";
                innerBase.transform.SetParent(transform, false);
                innerBase.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                innerBase.transform.localScale = new Vector3(0.38f, 0.05f, 0.38f);
                Collider col = innerBase.GetComponent<Collider>();
                if (col != null) Destroy(col);
                innerBaseRenderer = innerBase.GetComponent<Renderer>();
            }

            if (ocaSpriteRenderer == null)
            {
                GameObject spriteObj = new GameObject("Imagen oca");
                spriteObj.transform.SetParent(transform, false);
                spriteObj.transform.localPosition = new Vector3(0f, 0.68f, 0f);
                ocaSpriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                ocaSpriteRenderer.sortingOrder = 5;

                // Añadir el componente OcaBillboard existente para mirar siempre a la cámara
                if (spriteObj.GetComponent<OcaBillboard>() == null)
                {
                    spriteObj.AddComponent<OcaBillboard>();
                }
            }
        }

        private static Material CreateColoredMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.35f);
            }
            return mat;
        }

        /// <summary>
        /// Posiciona la ficha instantáneamente sobre una casilla, aplicando un pequeño desplazamiento
        /// circular para que hasta 8 jugadores entren cómodamente en la misma casilla sin superponerse.
        /// </summary>
        public void PlaceOnTileImmediate(BoardTile tile, int slotIndex = 0, int totalPlayersOnTile = 1)
        {
            if (tile == null) return;
            currentBoard = tile.Board;
            currentTileIndex = tile.TileNumber;
            transform.position = CalculateSlotPosition(tile.GetStandPosition(), slotIndex, totalPlayersOnTile);
        }

        /// <summary>
        /// Anima el movimiento paso a paso por el camino del tablero.
        /// </summary>
        public void MoveAlongPath(BoardPath path, int targetTileIndex, int slotIndex, int totalPlayers, System.Action onComplete = null)
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(MoveRoutine(path, targetTileIndex, slotIndex, totalPlayers, onComplete));
        }

        /// <summary>
        /// Anima el movimiento siguiendo una lista exacta de casillas paso a paso
        /// (soporta rebote en la casilla 63, filas cíclicas de Infierno 1, curva S de Infierno 2 y rueda circular del Payaso).
        /// </summary>
        public void MoveAlongSequence(System.Collections.Generic.List<BoardTile> stepTiles, int slotIndex, int totalPlayers, System.Action onComplete = null)
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
            moveCoroutine = StartCoroutine(MoveSequenceRoutine(stepTiles, slotIndex, totalPlayers, onComplete));
        }

        private IEnumerator MoveSequenceRoutine(System.Collections.Generic.List<BoardTile> stepTiles, int slotIndex, int totalPlayers, System.Action onComplete)
        {
            if (stepTiles == null || stepTiles.Count == 0)
            {
                moveCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            for (int i = 0; i < stepTiles.Count; i++)
            {
                BoardTile nextTile = stepTiles[i];
                if (nextTile == null) continue;

                bool isLastStep = (i == stepTiles.Count - 1);
                Vector3 startPos = transform.position;
                Vector3 endPos = isLastStep
                    ? CalculateSlotPosition(nextTile.GetStandPosition(), slotIndex, totalPlayers)
                    : nextTile.GetStandPosition();

                float duration = 0.22f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float hop = Mathf.Sin(t * Mathf.PI) * 0.45f;
                    transform.position = Vector3.Lerp(startPos, endPos, t) + Vector3.up * hop;
                    yield return null;
                }

                transform.position = endPos;
                currentBoard = nextTile.Board;
                currentTileIndex = nextTile.TileNumber;
            }

            moveCoroutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator MoveRoutine(BoardPath path, int targetTileIndex, int slotIndex, int totalPlayers, System.Action onComplete)
        {
            if (path == null)
            {
                moveCoroutine = null;
                onComplete?.Invoke();
                yield break;
            }

            while (currentTileIndex != targetTileIndex)
            {
                int step = targetTileIndex > currentTileIndex ? 1 : -1;
                int nextIdx = Mathf.Clamp(currentTileIndex + step, 0, path.TileCount - 1);
                BoardTile nextTile = path.GetTile(nextIdx);

                if (nextTile != null)
                {
                    Vector3 startPos = transform.position;
                    Vector3 endPos = (nextIdx == targetTileIndex)
                        ? CalculateSlotPosition(nextTile.GetStandPosition(), slotIndex, totalPlayers)
                        : nextTile.GetStandPosition();

                    float duration = 0.22f;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        float hop = Mathf.Sin(t * Mathf.PI) * 0.45f;
                        transform.position = Vector3.Lerp(startPos, endPos, t) + Vector3.up * hop;
                        yield return null;
                    }
                    transform.position = endPos;
                    currentTileIndex = nextIdx;
                }
                else
                {
                    break;
                }
            }

            moveCoroutine = null;
            onComplete?.Invoke();
        }

        private static Vector3 CalculateSlotPosition(Vector3 center, int slotIndex, int totalPlayers)
        {
            if (totalPlayers <= 1) return center;
            float radius = totalPlayers <= 4 ? 0.26f : 0.34f;
            float angle = (slotIndex / (float)totalPlayers) * Mathf.PI * 2f;
            return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }
    }
}
