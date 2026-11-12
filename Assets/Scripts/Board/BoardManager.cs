using UnityEngine;
using Oca2.Cameras;

namespace Oca2.Board
{
    /// <summary>
    /// Administrador central del mundo y los tableros de La Oca 2.
    /// Controla los 4 tableros (Principal, Infierno 1, Infierno 2, Payaso),
    /// sincroniza la posición y tipo exacto de sus casillas y gestiona la cámara.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        public static BoardManager Instance { get; private set; }

        [Header("Referencias de Tableros")]
        [SerializeField] private BoardPath mainBoardPath;
        [SerializeField] private BoardPath hell1BoardPath;
        [SerializeField] private BoardPath hell2BoardPath;
        [SerializeField] private BoardPath clownWheelBoardPath;

        [Header("Transform de los Centros de Tablero")]
        [SerializeField] private Transform mainBoardCenter;
        [SerializeField] private Transform hell1BoardCenter;
        [SerializeField] private Transform hell2BoardCenter;
        [SerializeField] private Transform clownWheelBoardCenter;

        [Header("Cámara")]
        [SerializeField] private BoardCameraController cameraController;

        public BoardPath MainBoard => mainBoardPath;
        public BoardPath Hell1Board => hell1BoardPath;
        public BoardPath Hell2Board => hell2BoardPath;
        public BoardPath ClownWheelBoard => clownWheelBoardPath;
        public BoardCameraController CameraController => cameraController;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SynchronizeAllBoardLayouts();
        }

        /// <summary>
        /// Sincroniza y reconstruye las casillas de los 4 tableros según las texturas oficiales
        /// usando BoardLayoutBuilder para garantizar precisión absoluta en coordenadas y reglas.
        /// </summary>
        [ContextMenu("Sincronizar Casillas de los 4 Tableros")]
        public void SynchronizeAllBoardLayouts()
        {
            if (mainBoardCenter == null)
            {
                GameObject obj = GameObject.Find("Board_Principal");
                if (obj != null) mainBoardCenter = obj.transform;
            }
            if (hell1BoardCenter == null)
            {
                GameObject obj = GameObject.Find("Board_Infierno_1");
                if (obj != null) hell1BoardCenter = obj.transform;
            }
            if (hell2BoardCenter == null)
            {
                GameObject obj = GameObject.Find("Board_Infierno_2");
                if (obj != null) hell2BoardCenter = obj.transform;
            }
            if (clownWheelBoardCenter == null)
            {
                GameObject obj = GameObject.Find("Board_Payaso");
                if (obj != null) clownWheelBoardCenter = obj.transform;
            }

            if (mainBoardCenter != null)
            {
                mainBoardPath = BoardLayoutBuilder.RebuildBoardTiles(mainBoardCenter.gameObject, BoardType.MainBoard);
            }
            if (hell1BoardCenter != null)
            {
                hell1BoardPath = BoardLayoutBuilder.RebuildBoardTiles(hell1BoardCenter.gameObject, BoardType.Hell1);
            }
            if (hell2BoardCenter != null)
            {
                hell2BoardPath = BoardLayoutBuilder.RebuildBoardTiles(hell2BoardCenter.gameObject, BoardType.Hell2);
            }
            if (clownWheelBoardCenter != null)
            {
                clownWheelBoardPath = BoardLayoutBuilder.RebuildBoardTiles(clownWheelBoardCenter.gameObject, BoardType.ClownWheel);
            }
        }

        /// <summary>
        /// Obtiene el camino correspondiente a un tipo de tablero.
        /// </summary>
        public BoardPath GetPath(BoardType boardType)
        {
            return boardType switch
            {
                BoardType.MainBoard => mainBoardPath,
                BoardType.Hell1 => hell1BoardPath,
                BoardType.Hell2 => hell2BoardPath,
                BoardType.ClownWheel => clownWheelBoardPath,
                _ => mainBoardPath
            };
        }

        /// <summary>
        /// Obtiene la casilla según el tablero y el índice numérico.
        /// </summary>
        public BoardTile GetTile(BoardType board, int tileIndex)
        {
            BoardPath path = GetPath(board);
            return path != null ? path.GetTile(tileIndex) : null;
        }

        /// <summary>
        /// Cambia la vista de la cámara al tablero especificado.
        /// </summary>
        public void SwitchToBoard(BoardType boardType)
        {
            Transform targetCenter = boardType switch
            {
                BoardType.MainBoard => mainBoardCenter,
                BoardType.Hell1 => hell1BoardCenter,
                BoardType.Hell2 => hell2BoardCenter,
                BoardType.ClownWheel => clownWheelBoardCenter,
                _ => mainBoardCenter
            };

            if (cameraController != null && targetCenter != null)
            {
                cameraController.SwitchBoard(targetCenter);
            }
        }
    }
}
