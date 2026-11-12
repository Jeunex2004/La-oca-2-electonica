using System.Collections.Generic;
using UnityEngine;

namespace Oca2.Board
{
    /// <summary>
    /// Gestiona la secuencia ordenada de casillas de un tablero y calcula las trayectorias
    /// paso a paso según las reglas específicas de cada dimensión (Principal, Infierno 1, Infierno 2, Payaso).
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
        /// </summary>
        public int CalculateTargetTileIndex(int currentTileIndex, int diceRoll, bool exactRollForGoal = true)
        {
            List<BoardTile> steps = TraceStepSequence(currentTileIndex, diceRoll, exactRollForGoal);
            if (steps.Count > 0 && steps[steps.Count - 1] != null)
            {
                return steps[steps.Count - 1].TileNumber;
            }
            return currentTileIndex;
        }

        /// <summary>
        /// Traza la secuencia exacta de casillas que pisa una ficha al avanzar `diceRoll` pasos,
        /// respetando las reglas de cada tablero:
        /// - MainBoard: Avance lineal (0..63) con rebote hacia atrás en la casilla 63 si exactRollForGoal es true,
        ///   y detención obligatoria si stopAtTileIndex >= 0 (ej: Boss en el camino).
        /// - Hell1: Entrada desde START (0 -> 6) y movimiento cíclico horizontal dentro de cada fila de 10 casillas.
        /// - Hell2: Camino en curva S (0..16) donde no se requiere número exacto para llegar al EXIT (16).
        /// - ClownWheel: Rueda circular cerrada de 6 sectores (0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0).
        /// </summary>
        public List<BoardTile> TraceStepSequence(int currentTileIndex, int diceRoll, bool exactRollForGoal = true, int stopAtTileIndex = -1)
        {
            List<BoardTile> sequence = new List<BoardTile>();
            if (tiles == null || tiles.Count == 0 || diceRoll <= 0) return sequence;

            BoardTile current = GetTile(currentTileIndex);
            if (current == null) current = tiles[0];

            if (boardType == BoardType.MainBoard)
            {
                int maxIndex = tiles.Count - 1; // 63
                int curIdx = current.TileNumber;
                int dir = 1;

                for (int s = 0; s < diceRoll; s++)
                {
                    int nextIdx = curIdx + dir;
                    if (nextIdx > maxIndex)
                    {
                        if (exactRollForGoal)
                        {
                            dir = -1;
                            nextIdx = maxIndex - 1;
                        }
                        else
                        {
                            nextIdx = maxIndex;
                        }
                    }
                    if (nextIdx < 0) nextIdx = 0;

                    curIdx = nextIdx;
                    BoardTile stepTile = GetTile(curIdx);
                    if (stepTile != null)
                    {
                        sequence.Add(stepTile);
                    }

                    if (stopAtTileIndex >= 0 && curIdx == stopAtTileIndex)
                    {
                        break;
                    }
                    if (!exactRollForGoal && curIdx == maxIndex)
                    {
                        break;
                    }
                }
            }
            else if (boardType == BoardType.Hell1 || boardType == BoardType.ClownWheel)
            {
                // Seguir los punteros NextTile (que forman bucles por fila en Hell1 o bucle circular de 6 sectores en ClownWheel)
                for (int s = 0; s < diceRoll; s++)
                {
                    if (current.NextTile != null)
                    {
                        current = current.NextTile;
                        sequence.Add(current);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else if (boardType == BoardType.Hell2)
            {
                // En Infierno 2 (pág. 11): al llegar o pasar por Zombi (15) / EXIT (16) sin necesitar número exacto
                for (int s = 0; s < diceRoll; s++)
                {
                    if (current.NextTile != null)
                    {
                        current = current.NextTile;
                        sequence.Add(current);
                        if (current.Type == TileType.Meta)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return sequence;
        }

        /// <summary>
        /// Configura el tipo de tablero y registra la lista de casillas del camino.
        /// </summary>
        public void ConfigureBoardTypeAndTiles(BoardType type, List<BoardTile> newTiles)
        {
            boardType = type;
            SetTiles(newTiles);
        }

        /// <summary>
        /// Registra y enlaza la lista de casillas del camino.
        /// </summary>
        public void SetTiles(List<BoardTile> newTiles)
        {
            tiles = new List<BoardTile>(newTiles);

            // Enlazar referencias de casillas contiguas por defecto
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
            BoardLayoutBuilder.ConnectBoardEdges(boardType, tiles);
            Debug.Log($"[BoardPath] Se registraron y ordenaron {tiles.Count} casillas en {gameObject.name} ({boardType}).");
        }

        private void OnDrawGizmos()
        {
            if (tiles == null || tiles.Count < 2) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] != null && tiles[i].NextTile != null)
                {
                    Vector3 from = tiles[i].GetStandPosition();
                    Vector3 to = tiles[i].NextTile.GetStandPosition();
                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }
}
