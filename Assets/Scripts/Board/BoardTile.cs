using UnityEngine;

namespace Oca2.Board
{
    /// <summary>
    /// Representa una casilla física e interactiva dentro del tablero.
    /// </summary>
    [SelectionBase]
    public class BoardTile : MonoBehaviour
    {
        [Header("Identificación")]
        [Tooltip("Número de la casilla (0 es Start, 63 es Meta)")]
        [SerializeField] private int tileNumber = 0;

        [Tooltip("Tipo de casilla según las reglas del juego")]
        [SerializeField] private TileType tileType = TileType.Normal;

        [Tooltip("A qué tablero pertenece esta casilla")]
        [SerializeField] private BoardType boardType = BoardType.MainBoard;

        [Tooltip("Nombre descriptivo para la UI / Debug")]
        [SerializeField] private string tileName = "";

        [Header("Conexiones")]
        [Tooltip("Siguiente casilla natural en el camino")]
        [SerializeField] private BoardTile nextTile;

        [Tooltip("Casilla anterior en el camino")]
        [SerializeField] private BoardTile previousTile;

        [Tooltip("Casilla destino especial (ej: siguiente Oca, destino de Puente, o casilla de castigo)")]
        [SerializeField] private BoardTile specialDestinationTile;

        [Header("Posición de la Ficha")]
        [Tooltip("Desplazamiento vertical o local donde se apoyará la Oca")]
        [SerializeField] private Vector3 standOffset = new Vector3(0, 0.25f, 0);

        // Propiedades públicas
        public int TileNumber => tileNumber;
        public TileType Type => tileType;
        public BoardType Board => boardType;
        public string TileName => string.IsNullOrEmpty(tileName) ? $"Casilla {tileNumber} ({tileType})" : tileName;
        public BoardTile NextTile { get => nextTile; set => nextTile = value; }
        public BoardTile PreviousTile { get => previousTile; set => previousTile = value; }
        public BoardTile SpecialDestinationTile { get => specialDestinationTile; set => specialDestinationTile = value; }

        /// <summary>
        /// Configura los datos principales de la casilla.
        /// </summary>
        public void Initialize(int number, TileType type, BoardType board, string name = "")
        {
            tileNumber = number;
            tileType = type;
            boardType = board;
            tileName = string.IsNullOrEmpty(name) ? $"Casilla {number}" : name;
            gameObject.name = $"Tile_{number:D2}_{type}";
        }

        /// <summary>
        /// Obtiene la posición en el mundo donde debe pararse la ficha / oca.
        /// </summary>
        public Vector3 GetStandPosition()
        {
            return transform.position + standOffset;
        }

        private void OnDrawGizmos()
        {
            // Color según el tipo de casilla para fácil identificación en el Editor
            Gizmos.color = GetTileGizmoColor(tileType);
            Vector3 center = transform.position;

            // Dibujar marcador de casilla
            Gizmos.DrawWireCube(center, new Vector3(0.8f, 0.1f, 0.8f));
            Gizmos.DrawSphere(GetStandPosition(), 0.12f);

            // Línea hacia la siguiente casilla
            if (nextTile != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center + Vector3.up * 0.1f, nextTile.transform.position + Vector3.up * 0.1f);
            }

            // Línea hacia destino especial (Oca a Oca, Puente a Puente, etc.)
            if (specialDestinationTile != null)
            {
                Gizmos.color = Color.yellow;
                Vector3 start = center + Vector3.up * 0.2f;
                Vector3 end = specialDestinationTile.transform.position + Vector3.up * 0.2f;
                Vector3 mid = (start + end) * 0.5f + Vector3.up * 1.5f;
                Gizmos.DrawLine(start, mid);
                Gizmos.DrawLine(mid, end);
            }
        }

        private static Color GetTileGizmoColor(TileType type)
        {
            return type switch
            {
                TileType.Start => Color.white,
                TileType.Meta => Color.magenta,
                TileType.Oca => Color.cyan,
                TileType.Puente => new Color(0.6f, 0.3f, 0.1f), // Marrón
                TileType.Muerte => Color.black,
                TileType.Dinero => Color.yellow,
                TileType.Experiencia => new Color(0.2f, 0.8f, 0.2f), // Verde
                TileType.Tienda => new Color(1f, 0.5f, 0f), // Naranja
                TileType.Hechizos => new Color(0.6f, 0.2f, 0.9f), // Púrpura
                TileType.CartasSalvajes => new Color(0.1f, 0.6f, 1f), // Celeste
                TileType.Boss => Color.red,
                TileType.BobEsponja => new Color(0.9f, 0.9f, 0.4f), // Amarillo claro
                TileType.Zombi => new Color(0.3f, 0.5f, 0.3f), // Verde oliva
                _ => Color.gray
            };
        }
    }
}
