using System.Collections.Generic;
using UnityEngine;

namespace Oca2.Board
{
    /// <summary>
    /// Gestiona la secuencia ordenada de casillas de un tablero.
    /// </summary>
    public class BoardPath : MonoBehaviour
    {
        [Header("Configuración")]
        [SerializeField] private BoardType boardType = BoardType.MainBoard;
        [SerializeField] private List<BoardTile> tiles = new List<BoardTile>();

        public BoardType Board => boardType;
        public int TileCount => tiles.Count;
        public IReadOnlyList<BoardTile> Tiles => tiles;

        /// <summary>
        /// Obtiene la casilla por su número de índice.
        /// </summary>
        public BoardTile GetTile(int index)
        {
            if (index >= 0 && index < tiles.Count)
            {
                return tiles[index];
            }
            return null;
        }

        /// <summary>
        /// Calcula la casilla destino según la tirada de dado.
        /// Regla de La Oca: Si la tirada supera la casilla final (63), el exceso rebota hacia atrás.
        /// </summary>
        public int CalculateTargetTileIndex(int currentTileIndex, int diceRoll)
        {
            if (tiles.Count == 0) return 0;
            int maxIndex = tiles.Count - 1; // 63 para el tablero principal
            int target = currentTileIndex + diceRoll;

            if (target > maxIndex)
            {
                int excess = target - maxIndex;
                target = maxIndex - excess;
                if (target < 0) target = 0;
            }

            return target;
        }

        /// <summary>
        /// Registra y enlaza la lista de casillas del camino.
        /// </summary>
        public void SetTiles(List<BoardTile> newTiles)
        {
            tiles = new List<BoardTile>(newTiles);

            // Enlazar referencias de casillas contiguas
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null) continue;
                tiles[i].PreviousTile = (i > 0) ? tiles[i - 1] : null;
                tiles[i].NextTile = (i < tiles.Count - 1) ? tiles[i + 1] : null;
            }
        }

        /// <summary>
        /// Busca todas las casillas hijas y las ordena por su número.
        /// </summary>
        [ContextMenu("Actualizar Casillas Desde Hijos")]
        public void CollectAndSortChildTiles()
        {
            tiles.Clear();
            BoardTile[] foundTiles = GetComponentsInChildren<BoardTile>();
            System.Array.Sort(foundTiles, (a, b) => a.TileNumber.CompareTo(b.TileNumber));
            SetTiles(new List<BoardTile>(foundTiles));
            Debug.Log($"[BoardPath] Se registraron y ordenaron {tiles.Count} casillas en {gameObject.name}.");
        }

        private void OnDrawGizmos()
        {
            if (tiles == null || tiles.Count < 2) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            for (int i = 0; i < tiles.Count - 1; i++)
            {
                if (tiles[i] != null && tiles[i + 1] != null)
                {
                    Vector3 from = tiles[i].GetStandPosition();
                    Vector3 to = tiles[i + 1].GetStandPosition();
                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }
}
