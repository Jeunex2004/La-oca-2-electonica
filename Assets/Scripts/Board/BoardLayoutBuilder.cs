using System.Collections.Generic;
using UnityEngine;

namespace Oca2.Board
{
    /// <summary>
    /// Constructor compartido (Runtime y Editor) que define las coordenadas exactas UV (0..1),
    /// tipos de casilla y conexiones especiales de los 4 tableros oficiales:
    /// 1. Tablero Principal (64 casillas: 0=START a 63=Meta)
    /// 2. Tablero Infierno 1 (42 casillas: 0=START, 4 filas cíclicas de 10 casillas con flechas rojas, 41=EXIT)
    /// 3. Tablero Infierno 2 (17 casillas en curva S: 0=START, 1..14=BobEsponja, 15=Zombi, 16=EXIT)
    /// 4. Tablero Payaso (6 sectores circulares: 0=EXIT, 1..5=NO)
    /// </summary>
    public static class BoardLayoutBuilder
    {
        public struct TileBlueprint
        {
            public int number;
            public TileType type;
            public float u;
            public float v;
            public string name;

            public TileBlueprint(int number, TileType type, float u, float v, string name)
            {
                this.number = number;
                this.type = type;
                this.u = u;
                this.v = v;
                this.name = name;
            }
        }

        /// <summary>
        /// Reconstruye o sincroniza todas las casillas de un tablero para que coincidan
        /// exactamente con el diseño pintado en su textura PNG.
        /// </summary>
        public static BoardPath RebuildBoardTiles(GameObject boardObj, BoardType boardType)
        {
            if (boardObj == null) return null;

            Transform existingContainer = boardObj.transform.Find("Tiles_Path");
            BoardPath boardPath = existingContainer != null ? existingContainer.GetComponent<BoardPath>() : null;

            GameObject container;
            if (existingContainer != null)
            {
                container = existingContainer.gameObject;
                // Limpiar casillas previas para regenerarlas con las coordenadas y tipos exactos
                for (int i = container.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = container.transform.GetChild(i).gameObject;
                    if (Application.isPlaying)
                    {
                        Object.Destroy(child);
                    }
                    else
                    {
                        Object.DestroyImmediate(child);
                    }
                }
            }
            else
            {
                container = new GameObject("Tiles_Path");
                container.transform.SetParent(boardObj.transform, false);
            }

            // Compensar la escala no uniforme del tablero (ej: 18 x 0.2 x 18) para que los hijos tengan escala mundial (1, 1, 1)
            Vector3 parentScale = boardObj.transform.localScale;
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            container.transform.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.0001f ? 1f / parentScale.x : 1f,
                Mathf.Abs(parentScale.y) > 0.0001f ? 1f / parentScale.y : 1f,
                Mathf.Abs(parentScale.z) > 0.0001f ? 1f / parentScale.z : 1f
            );

            if (boardPath == null)
            {
                boardPath = container.AddComponent<BoardPath>();
            }

            TileBlueprint[] blueprints = GetBlueprintsForBoard(boardType);
            List<BoardTile> tilesList = new List<BoardTile>(blueprints.Length);

            for (int i = 0; i < blueprints.Length; i++)
            {
                TileBlueprint bp = blueprints[i];
                GameObject tileObj = new GameObject($"Tile_{bp.number:D2}_{bp.type}");
                tileObj.transform.SetParent(container.transform, false);
                tileObj.transform.position = UvToBoardWorldPosition(boardObj, bp.u, bp.v, 0.04f);
                tileObj.transform.rotation = boardObj.transform.rotation;
                tileObj.transform.localScale = Vector3.one;

                BoardTile tile = tileObj.AddComponent<BoardTile>();
                tile.Initialize(bp.number, bp.type, boardType, bp.name);

                BoxCollider boxCol = tileObj.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.85f, 0.2f, 0.85f);
                boxCol.center = new Vector3(0f, 0.1f, 0f);

                tilesList.Add(tile);
            }

            boardPath.ConfigureBoardTypeAndTiles(boardType, tilesList);
            ConnectBoardEdges(boardType, tilesList);

            return boardPath;
        }

        /// <summary>
        /// Convierte una coordenada UV normalizada (0..1 de izquierda a derecha y de abajo hacia arriba en la textura PNG)
        /// a la posición 3D exacta sobre la cara superior (+Y) del Mesh del tablero.
        /// </summary>
        public static Vector3 UvToBoardWorldPosition(GameObject boardObj, float u, float v, float heightOffset = 0.04f)
        {
            MeshFilter mf = boardObj != null ? boardObj.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                Vector3[] verts = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;

                Vector3 p00 = new Vector3(-0.5f, 0.5f, -0.5f);
                Vector3 p10 = new Vector3(0.5f, 0.5f, -0.5f);
                Vector3 p01 = new Vector3(-0.5f, 0.5f, 0.5f);
                Vector3 p11 = new Vector3(0.5f, 0.5f, 0.5f);

                float d00 = float.MaxValue, d10 = float.MaxValue, d01 = float.MaxValue, d11 = float.MaxValue;
                bool foundTopFace = false;

                for (int i = 0; i < verts.Length; i++)
                {
                    if (normals != null && i < normals.Length && normals[i].y > 0.5f)
                    {
                        foundTopFace = true;
                        Vector2 uv = (uvs != null && i < uvs.Length) ? uvs[i] : Vector2.zero;

                        float dist00 = (uv - new Vector2(0f, 0f)).sqrMagnitude;
                        if (dist00 < d00) { d00 = dist00; p00 = verts[i]; }

                        float dist10 = (uv - new Vector2(1f, 0f)).sqrMagnitude;
                        if (dist10 < d10) { d10 = dist10; p10 = verts[i]; }

                        float dist01 = (uv - new Vector2(0f, 1f)).sqrMagnitude;
                        if (dist01 < d01) { d01 = dist01; p01 = verts[i]; }

                        float dist11 = (uv - new Vector2(1f, 1f)).sqrMagnitude;
                        if (dist11 < d11) { d11 = dist11; p11 = verts[i]; }
                    }
                }

                if (foundTopFace)
                {
                    Vector3 bottom = Vector3.Lerp(p00, p10, u);
                    Vector3 top = Vector3.Lerp(p01, p11, u);
                    Vector3 localPoint = Vector3.Lerp(bottom, top, v);
                    return boardObj.transform.TransformPoint(localPoint) + Vector3.up * heightOffset;
                }
            }

            // Fallback si no hay MeshFilter
            Vector3 fallbackLocal = new Vector3(u - 0.5f, 0.5f, v - 0.5f);
            return boardObj.transform.TransformPoint(fallbackLocal) + Vector3.up * heightOffset;
        }

        public static TileBlueprint[] GetBlueprintsForBoard(BoardType boardType)
        {
            return boardType switch
            {
                BoardType.MainBoard => GetMainBoardBlueprints(),
                BoardType.Hell1 => GetHell1Blueprints(),
                BoardType.Hell2 => GetHell2Blueprints(),
                BoardType.ClownWheel => GetClownWheelBlueprints(),
                _ => GetMainBoardBlueprints()
            };
        }

        /// <summary>
        /// 64 casillas exactas del Tablero Principal ("Tablero principal.png").
        /// </summary>
        private static TileBlueprint[] GetMainBoardBlueprints()
        {
            return new TileBlueprint[]
            {
                // Fila Inferior Exterior (0 a 9, izquierda a derecha)
                new TileBlueprint(0,  TileType.Start,          0.128f, 0.092f, "Casilla 0 - START"),
                new TileBlueprint(1,  TileType.Oca,            0.285f, 0.092f, "Casilla 1 - Oca"),
                new TileBlueprint(2,  TileType.Muerte,         0.370f, 0.092f, "Casilla 2 - Muerte (Calavera)"),
                new TileBlueprint(3,  TileType.Dinero,         0.455f, 0.092f, "Casilla 3 - Dinero"),
                new TileBlueprint(4,  TileType.Puente,         0.540f, 0.092f, "Casilla 4 - Puente"),
                new TileBlueprint(5,  TileType.Oca,            0.625f, 0.092f, "Casilla 5 - Oca"),
                new TileBlueprint(6,  TileType.CartasSalvajes, 0.710f, 0.092f, "Casilla 6 - Carta Salvaje"),
                new TileBlueprint(7,  TileType.Experiencia,    0.795f, 0.092f, "Casilla 7 - Experiencia"),
                new TileBlueprint(8,  TileType.Hechizos,       0.880f, 0.055f, "Casilla 8 - Hechizos"),
                new TileBlueprint(9,  TileType.Oca,            0.945f, 0.120f, "Casilla 9 - Oca"),

                // Columna Derecha Exterior (10 a 19, abajo hacia arriba)
                new TileBlueprint(10, TileType.Dinero,         0.915f, 0.200f, "Casilla 10 - Dinero"),
                new TileBlueprint(11, TileType.Tienda,         0.915f, 0.284f, "Casilla 11 - Tienda"),
                new TileBlueprint(12, TileType.Puente,         0.915f, 0.368f, "Casilla 12 - Puente"),
                new TileBlueprint(13, TileType.Boss,           0.915f, 0.452f, "Casilla 13 - Boss: La Foca"),
                new TileBlueprint(14, TileType.Oca,            0.915f, 0.536f, "Casilla 14 - Oca"),
                new TileBlueprint(15, TileType.Experiencia,    0.915f, 0.620f, "Casilla 15 - Experiencia"),
                new TileBlueprint(16, TileType.Hechizos,       0.915f, 0.704f, "Casilla 16 - Hechizos"),
                new TileBlueprint(17, TileType.CartasSalvajes, 0.915f, 0.788f, "Casilla 17 - Carta Salvaje"),
                new TileBlueprint(18, TileType.Oca,            0.945f, 0.875f, "Casilla 18 - Oca"),
                new TileBlueprint(19, TileType.Dinero,         0.885f, 0.940f, "Casilla 19 - Dinero"),

                // Fila Superior Exterior (20 a 29, derecha a izquierda)
                new TileBlueprint(20, TileType.CartasSalvajes, 0.802f, 0.905f, "Casilla 20 - Carta Salvaje"),
                new TileBlueprint(21, TileType.Experiencia,    0.716f, 0.905f, "Casilla 21 - Experiencia"),
                new TileBlueprint(22, TileType.CartasSalvajes, 0.630f, 0.905f, "Casilla 22 - Carta Salvaje"),
                new TileBlueprint(23, TileType.Oca,            0.546f, 0.905f, "Casilla 23 - Oca"),
                new TileBlueprint(24, TileType.Dinero,         0.462f, 0.905f, "Casilla 24 - Dinero"),
                new TileBlueprint(25, TileType.Hechizos,       0.376f, 0.905f, "Casilla 25 - Hechizos"),
                new TileBlueprint(26, TileType.Experiencia,    0.292f, 0.905f, "Casilla 26 - Experiencia"),
                new TileBlueprint(27, TileType.Oca,            0.210f, 0.905f, "Casilla 27 - Oca"),
                new TileBlueprint(28, TileType.CartasSalvajes, 0.120f, 0.935f, "Casilla 28 - Carta Salvaje"),
                new TileBlueprint(29, TileType.Dinero,         0.055f, 0.875f, "Casilla 29 - Dinero"),

                // Columna Izquierda Exterior (30 a 37, arriba hacia abajo)
                new TileBlueprint(30, TileType.CartasSalvajes, 0.088f, 0.792f, "Casilla 30 - Carta Salvaje"),
                new TileBlueprint(31, TileType.Experiencia,    0.088f, 0.708f, "Casilla 31 - Experiencia"),
                new TileBlueprint(32, TileType.Oca,            0.088f, 0.624f, "Casilla 32 - Oca"),
                new TileBlueprint(33, TileType.Dinero,         0.088f, 0.540f, "Casilla 33 - Dinero"),
                new TileBlueprint(34, TileType.Tienda,         0.088f, 0.456f, "Casilla 34 - Tienda"),
                new TileBlueprint(35, TileType.Boss,           0.088f, 0.372f, "Casilla 35 - Boss: La Roca"),
                new TileBlueprint(36, TileType.Oca,            0.060f, 0.285f, "Casilla 36 - Oca"),
                new TileBlueprint(37, TileType.Hechizos,       0.125f, 0.215f, "Casilla 37 - Hechizos"),

                // Fila Inferior Interior (38 a 45, izquierda a derecha)
                new TileBlueprint(38, TileType.Experiencia,    0.210f, 0.248f, "Casilla 38 - Experiencia"),
                new TileBlueprint(39, TileType.Dinero,         0.292f, 0.248f, "Casilla 39 - Dinero"),
                new TileBlueprint(40, TileType.CartasSalvajes, 0.376f, 0.248f, "Casilla 40 - Carta Salvaje"),
                new TileBlueprint(41, TileType.Oca,            0.460f, 0.248f, "Casilla 41 - Oca"),
                new TileBlueprint(42, TileType.CartasSalvajes, 0.544f, 0.248f, "Casilla 42 - Carta Salvaje"),
                new TileBlueprint(43, TileType.Experiencia,    0.628f, 0.248f, "Casilla 43 - Experiencia"),
                new TileBlueprint(44, TileType.Dinero,         0.720f, 0.215f, "Casilla 44 - Dinero"),
                new TileBlueprint(45, TileType.Oca,            0.785f, 0.285f, "Casilla 45 - Oca"),

                // Columna Derecha Interior (46 a 51, abajo hacia arriba)
                new TileBlueprint(46, TileType.Tienda,         0.755f, 0.372f, "Casilla 46 - Tienda"),
                new TileBlueprint(47, TileType.Hechizos,       0.755f, 0.456f, "Casilla 47 - Hechizos"),
                new TileBlueprint(48, TileType.CartasSalvajes, 0.755f, 0.540f, "Casilla 48 - Carta Salvaje"),
                new TileBlueprint(49, TileType.Experiencia,    0.755f, 0.624f, "Casilla 49 - Experiencia"),
                new TileBlueprint(50, TileType.Oca,            0.780f, 0.715f, "Casilla 50 - Oca"),
                new TileBlueprint(51, TileType.Dinero,         0.715f, 0.785f, "Casilla 51 - Dinero"),

                // Fila Superior Interior (52 a 57, derecha a izquierda)
                new TileBlueprint(52, TileType.CartasSalvajes, 0.630f, 0.750f, "Casilla 52 - Carta Salvaje"),
                new TileBlueprint(53, TileType.Boss,           0.546f, 0.750f, "Casilla 53 - Boss: La Trollface"),
                new TileBlueprint(54, TileType.Oca,            0.462f, 0.750f, "Casilla 54 - Oca"),
                new TileBlueprint(55, TileType.CartasSalvajes, 0.378f, 0.750f, "Casilla 55 - Carta Salvaje"),
                new TileBlueprint(56, TileType.Experiencia,    0.285f, 0.780f, "Casilla 56 - Experiencia"),
                new TileBlueprint(57, TileType.Hechizos,       0.220f, 0.715f, "Casilla 57 - Hechizos"),

                // Columna Izquierda Interior y Entrada a la Meta (58 a 63)
                new TileBlueprint(58, TileType.Muerte,         0.252f, 0.630f, "Casilla 58 - Muerte (Calavera)"),
                new TileBlueprint(59, TileType.Oca,            0.252f, 0.546f, "Casilla 59 - Oca Final"),
                new TileBlueprint(60, TileType.Muerte,         0.225f, 0.455f, "Casilla 60 - Muerte (Calavera)"),
                new TileBlueprint(61, TileType.CartasSalvajes, 0.285f, 0.385f, "Casilla 61 - Carta Salvaje"),
                new TileBlueprint(62, TileType.CartasSalvajes, 0.372f, 0.420f, "Casilla 62 - Carta Salvaje"),
                new TileBlueprint(63, TileType.Meta,           0.505f, 0.510f, "Casilla 63 - ¡META (Victoria)!")
            };
        }

        /// <summary>
        /// 42 casillas exactas del Tablero Infierno 1 ("Tablero infierno 1.png"):
        /// 0 = START, 1..10 = Nivel 1 (Dinero), 11..20 = Nivel 2 (Hechizos / Cartas Salvajes),
        /// 21..30 = Nivel 3 (Experiencia), 31..40 = Nivel 4 (Dinero / Tienda), 41 = EXIT.
        /// </summary>
        private static TileBlueprint[] GetHell1Blueprints()
        {
            float[] colU = { 0.055f, 0.153f, 0.252f, 0.351f, 0.450f, 0.548f, 0.647f, 0.746f, 0.845f, 0.942f };
            List<TileBlueprint> list = new List<TileBlueprint>(42);

            // 0: START
            list.Add(new TileBlueprint(0, TileType.Start, 0.515f, 0.080f, "Infierno 1 - START"));

            // Fila 1 (Casillas 1..10): 10 casillas de Dinero (v = 0.255)
            for (int c = 0; c < 10; c++)
            {
                int num = 1 + c;
                list.Add(new TileBlueprint(num, TileType.Dinero, colU[c], 0.255f, $"Infierno 1 - Nivel 1 Casilla {num} (Dinero)"));
            }

            // Fila 2 (Casillas 11..20): Hechizos en c = 0, 3, 6, 9; CartasSalvajes en el resto (v = 0.420)
            TileType[] row2Types =
            {
                TileType.Hechizos, TileType.CartasSalvajes, TileType.CartasSalvajes,
                TileType.Hechizos, TileType.CartasSalvajes, TileType.CartasSalvajes,
                TileType.Hechizos, TileType.CartasSalvajes, TileType.CartasSalvajes,
                TileType.Hechizos
            };
            for (int c = 0; c < 10; c++)
            {
                int num = 11 + c;
                TileType t = row2Types[c];
                list.Add(new TileBlueprint(num, t, colU[c], 0.420f, $"Infierno 1 - Nivel 2 Casilla {num} ({t})"));
            }

            // Fila 3 (Casillas 21..30): 10 casillas de Experiencia (v = 0.585)
            for (int c = 0; c < 10; c++)
            {
                int num = 21 + c;
                list.Add(new TileBlueprint(num, TileType.Experiencia, colU[c], 0.585f, $"Infierno 1 - Nivel 3 Casilla {num} (Experiencia)"));
            }

            // Fila 4 (Casillas 31..40): Tienda en c = 3, 6; Dinero en el resto (v = 0.745)
            for (int c = 0; c < 10; c++)
            {
                int num = 31 + c;
                TileType t = (c == 3 || c == 6) ? TileType.Tienda : TileType.Dinero;
                list.Add(new TileBlueprint(num, t, colU[c], 0.745f, $"Infierno 1 - Nivel 4 Casilla {num} ({t})"));
            }

            // 41: EXIT
            list.Add(new TileBlueprint(41, TileType.Meta, 0.495f, 0.925f, "Infierno 1 - EXIT"));

            return list.ToArray();
        }

        /// <summary>
        /// 17 casillas exactas en curva S del Tablero Infierno 2 ("Tablero infierno 2.png"):
        /// 0 = START, 1..14 = BobEsponja, 15 = Zombi, 16 = EXIT.
        /// </summary>
        private static TileBlueprint[] GetHell2Blueprints()
        {
            return new TileBlueprint[]
            {
                new TileBlueprint(0,  TileType.Start,      0.180f, 0.115f, "Infierno 2 - START"),
                new TileBlueprint(1,  TileType.BobEsponja, 0.335f, 0.105f, "Infierno 2 - Casilla 1 (Bob Esponja)"),
                new TileBlueprint(2,  TileType.BobEsponja, 0.445f, 0.108f, "Infierno 2 - Casilla 2 (Bob Esponja)"),
                new TileBlueprint(3,  TileType.BobEsponja, 0.555f, 0.120f, "Infierno 2 - Casilla 3 (Bob Esponja)"),
                new TileBlueprint(4,  TileType.BobEsponja, 0.670f, 0.145f, "Infierno 2 - Casilla 4 (Bob Esponja)"),
                new TileBlueprint(5,  TileType.BobEsponja, 0.795f, 0.190f, "Infierno 2 - Casilla 5 (Bob Esponja)"),
                new TileBlueprint(6,  TileType.BobEsponja, 0.830f, 0.315f, "Infierno 2 - Casilla 6 (Bob Esponja)"),
                new TileBlueprint(7,  TileType.BobEsponja, 0.745f, 0.425f, "Infierno 2 - Casilla 7 (Bob Esponja)"),
                new TileBlueprint(8,  TileType.BobEsponja, 0.605f, 0.450f, "Infierno 2 - Casilla 8 (Bob Esponja)"),
                new TileBlueprint(9,  TileType.BobEsponja, 0.455f, 0.460f, "Infierno 2 - Casilla 9 (Bob Esponja)"),
                new TileBlueprint(10, TileType.BobEsponja, 0.310f, 0.495f, "Infierno 2 - Casilla 10 (Bob Esponja)"),
                new TileBlueprint(11, TileType.BobEsponja, 0.235f, 0.595f, "Infierno 2 - Casilla 11 (Bob Esponja)"),
                new TileBlueprint(12, TileType.BobEsponja, 0.295f, 0.695f, "Infierno 2 - Casilla 12 (Bob Esponja)"),
                new TileBlueprint(13, TileType.BobEsponja, 0.435f, 0.745f, "Infierno 2 - Casilla 13 (Bob Esponja)"),
                new TileBlueprint(14, TileType.BobEsponja, 0.565f, 0.785f, "Infierno 2 - Casilla 14 (Bob Esponja)"),
                new TileBlueprint(15, TileType.Zombi,      0.690f, 0.815f, "Infierno 2 - Casilla 15 (Transformación Zombi)"),
                new TileBlueprint(16, TileType.Meta,       0.850f, 0.875f, "Infierno 2 - EXIT")
            };
        }

        /// <summary>
        /// 6 sectores circulares exactos del Tablero Payaso ("Tablero payaso.png"):
        /// 0 = EXIT (sector verde inferior), 1..5 = Sectores "NO".
        /// </summary>
        private static TileBlueprint[] GetClownWheelBlueprints()
        {
            return new TileBlueprint[]
            {
                new TileBlueprint(0, TileType.Meta,   0.500f, 0.065f, "Rueda del Payaso - EXIT"),
                new TileBlueprint(1, TileType.Normal, 0.115f, 0.280f, "Rueda del Payaso - NO (1)"),
                new TileBlueprint(2, TileType.Normal, 0.145f, 0.720f, "Rueda del Payaso - NO (2)"),
                new TileBlueprint(3, TileType.Normal, 0.500f, 0.925f, "Rueda del Payaso - NO (3)"),
                new TileBlueprint(4, TileType.Normal, 0.855f, 0.720f, "Rueda del Payaso - NO (4)"),
                new TileBlueprint(5, TileType.Normal, 0.885f, 0.280f, "Rueda del Payaso - NO (5)")
            };
        }

        /// <summary>
        /// Conecta los enlaces NextTile, PreviousTile y SpecialDestinationTile específicos de cada tablero.
        /// </summary>
        public static void ConnectBoardEdges(BoardType boardType, List<BoardTile> tiles)
        {
            if (tiles == null || tiles.Count == 0) return;

            switch (boardType)
            {
                case BoardType.MainBoard:
                {
                    // Conectar Ocas consecutivas (1 -> 5 -> 9 -> 14 -> 18 -> 23 -> 27 -> 32 -> 36 -> 41 -> 45 -> 50 -> 54 -> 59 -> 63)
                    List<BoardTile> ocas = tiles.FindAll(t => t.Type == TileType.Oca);
                    for (int i = 0; i < ocas.Count; i++)
                    {
                        ocas[i].SpecialDestinationTile = (i < ocas.Count - 1)
                            ? ocas[i + 1]
                            : tiles[tiles.Count - 1]; // La última Oca (59) salta a la Meta (63)
                    }

                    // Conectar Puentes: cada casilla de Puente (4 y 12) avanza +1 a la casilla inmediatamente siguiente (4 -> 5, 12 -> 13)
                    List<BoardTile> puentes = tiles.FindAll(t => t.Type == TileType.Puente);
                    foreach (BoardTile puente in puentes)
                    {
                        int nextIdx = puente.TileNumber + 1;
                        if (nextIdx >= 0 && nextIdx < tiles.Count)
                        {
                            puente.SpecialDestinationTile = tiles[nextIdx];
                        }
                    }
                    break;
                }

                case BoardType.Hell1:
                {
                    // En Infierno 1:
                    // - Casilla 0 (START) apunta con su flecha negra a la casilla 6 (columna central-derecha del Nivel 1)
                    if (tiles.Count >= 42)
                    {
                        tiles[0].NextTile = tiles[6];
                        tiles[0].PreviousTile = null;

                        // Cada una de las 4 filas de 10 casillas (1..10, 11..20, 21..30, 31..40) forma un bucle horizontal cerrado
                        int[] rowStarts = { 1, 11, 21, 31 };
                        foreach (int startIdx in rowStarts)
                        {
                            for (int c = 0; c < 10; c++)
                            {
                                int curr = startIdx + c;
                                int next = startIdx + ((c + 1) % 10);
                                int prev = startIdx + ((c + 9) % 10);
                                tiles[curr].NextTile = tiles[next];
                                tiles[curr].PreviousTile = tiles[prev];
                            }
                        }

                        // Flechas rojas ascendentes entre niveles ("Tablero infierno 1.png"):
                        // Nivel 1 -> Nivel 2 (columnas c = 3, 6, 9 => casillas 4->14, 7->17, 10->20)
                        tiles[4].SpecialDestinationTile  = tiles[14];
                        tiles[7].SpecialDestinationTile  = tiles[17];
                        tiles[10].SpecialDestinationTile = tiles[20];

                        // Nivel 2 -> Nivel 3 (columnas c = 3, 5, 7 => casillas 14->24, 16->26, 18->28)
                        tiles[14].SpecialDestinationTile = tiles[24];
                        tiles[16].SpecialDestinationTile = tiles[26];
                        tiles[18].SpecialDestinationTile = tiles[28];

                        // Nivel 3 -> Nivel 4 (columnas c = 1, 4, 6 => casillas 22->32, 25->35, 27->37)
                        tiles[22].SpecialDestinationTile = tiles[32];
                        tiles[25].SpecialDestinationTile = tiles[35];
                        tiles[27].SpecialDestinationTile = tiles[37];

                        // Nivel 4 -> EXIT (columnas c = 4, 5 => casillas 35->41, 36->41)
                        tiles[35].SpecialDestinationTile = tiles[41];
                        tiles[36].SpecialDestinationTile = tiles[41];

                        // EXIT (41)
                        tiles[41].NextTile = null;
                    }
                    break;
                }

                case BoardType.Hell2:
                {
                    // Camino lineal en S (0..16), donde 15 (Zombi) y 16 (EXIT) devuelven al jugador como Zombi al Tablero Principal
                    break;
                }

                case BoardType.ClownWheel:
                {
                    // Rueda circular cerrada de 6 sectores (0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0)
                    int count = tiles.Count;
                    for (int i = 0; i < count; i++)
                    {
                        tiles[i].NextTile = tiles[(i + 1) % count];
                        tiles[i].PreviousTile = tiles[(i + count - 1) % count];
                    }
                    break;
                }
            }
        }
    }
}
