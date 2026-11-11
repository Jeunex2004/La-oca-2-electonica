using System.Collections.Generic;
using UnityEngine;
using Oca2.Cameras;

namespace Oca2.Board
{
    /// <summary>
    /// Administrador central del mundo y los tableros de La Oca 2.
    /// Controla la instanciación de los tableros (Principal, Infierno 1, Infierno 2, Payaso)
    /// y gestiona el acceso a las casillas y sus posiciones espaciales.
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
