using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Oca2.Board;
using Oca2.Cameras;

namespace Oca2.Editor
{
    /// <summary>
    /// Herramienta de Editor para construir y configurar automáticamente el mundo 3D
    /// y el sistema de cámara de La Oca 2 en la escena activa.
    /// </summary>
    public static class BoardSetupUtility
    {
        private const string ASSETS_FOLDER = "Assets/OCA 2 assets/";
        private const string MATERIALS_FOLDER = "Assets/Materials/";

        [MenuItem("Tools/La Oca 2/Construir Tableros y Configurar Cámara")]
        public static void SetupCompleteWorldAndCamera()
        {
            // 1. Asegurar carpeta de materiales
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            // 2. Crear o reutilizar Root del Mundo
            GameObject worldRoot = GameObject.Find("World_Root");
            if (worldRoot == null)
            {
                worldRoot = new GameObject("World_Root");
                Undo.RegisterCreatedObjectUndo(worldRoot, "Create World Root");
            }

            // 3. Crear Materiales para cada Tablero
            Material mainBoardMat = GetOrCreateBoardMaterial("Board_Principal_Mat", ASSETS_FOLDER + "Tablero principal.png");
            Material hell1Mat = GetOrCreateBoardMaterial("Board_Hell1_Mat", ASSETS_FOLDER + "Tablero infierno 1.png");
            Material hell2Mat = GetOrCreateBoardMaterial("Board_Hell2_Mat", ASSETS_FOLDER + "Tablero infierno 2.png");
            Material clownMat = GetOrCreateBoardMaterial("Board_Clown_Mat", ASSETS_FOLDER + "Tablero payaso.png");

            // 4. Instanciar Tablero Principal
            GameObject mainBoardObj = SetupBoardObject("Board_Principal", new Vector3(0, 0, 0), new Vector3(18, 1, 18), mainBoardMat, worldRoot.transform);
            BoardPath mainBoardPath = SetupMainBoardPath(mainBoardObj);

            // 5. Instanciar Tableros Secundarios (Dimensiones) en zonas adyacentes
            GameObject hell1Obj = SetupBoardObject("Board_Infierno_1", new Vector3(32, -4, 0), new Vector3(16, 1, 16), hell1Mat, worldRoot.transform);
            BoardPath hell1Path = SetupSubBoardPath(hell1Obj, BoardType.Hell1, 24);

            GameObject hell2Obj = SetupBoardObject("Board_Infierno_2", new Vector3(32, -10, 26), new Vector3(16, 1, 16), hell2Mat, worldRoot.transform);
            BoardPath hell2Path = SetupSubBoardPath(hell2Obj, BoardType.Hell2, 20);

            GameObject clownObj = SetupBoardObject("Board_Payaso", new Vector3(-32, 2, 0), new Vector3(16, 1, 16), clownMat, worldRoot.transform);
            BoardPath clownPath = SetupSubBoardPath(clownObj, BoardType.ClownWheel, 8);

            // 6. Configurar o Crear BoardManager
            BoardManager boardManager = Object.FindFirstObjectByType<BoardManager>();
            if (boardManager == null)
            {
                GameObject managerObj = new GameObject("BoardManager");
                managerObj.transform.SetParent(worldRoot.transform);
                boardManager = managerObj.AddComponent<BoardManager>();
            }

            // Asignar referencias en BoardManager usando SerializedObject
            SerializedObject soManager = new SerializedObject(boardManager);
            soManager.FindProperty("mainBoardPath").objectReferenceValue = mainBoardPath;
            soManager.FindProperty("hell1BoardPath").objectReferenceValue = hell1Path;
            soManager.FindProperty("hell2BoardPath").objectReferenceValue = hell2Path;
            soManager.FindProperty("clownWheelBoardPath").objectReferenceValue = clownPath;

            soManager.FindProperty("mainBoardCenter").objectReferenceValue = mainBoardObj.transform;
            soManager.FindProperty("hell1BoardCenter").objectReferenceValue = hell1Obj.transform;
            soManager.FindProperty("hell2BoardCenter").objectReferenceValue = hell2Obj.transform;
            soManager.FindProperty("clownWheelBoardCenter").objectReferenceValue = clownObj.transform;

            // 7. Configurar Cámara Principal y BoardCameraController
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCamera = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            BoardCameraController camController = mainCamera.GetComponent<BoardCameraController>();
            if (camController == null)
            {
                camController = mainCamera.gameObject.AddComponent<BoardCameraController>();
            }

            SerializedObject soCam = new SerializedObject(camController);
            soCam.FindProperty("boardCenter").objectReferenceValue = mainBoardObj.transform;
            SerializedProperty centersProp = soCam.FindProperty("allBoardCenters");
            if (centersProp != null && centersProp.isArray)
            {
                centersProp.arraySize = 4;
                centersProp.GetArrayElementAtIndex(0).objectReferenceValue = mainBoardObj.transform;
                centersProp.GetArrayElementAtIndex(1).objectReferenceValue = hell1Obj.transform;
                centersProp.GetArrayElementAtIndex(2).objectReferenceValue = hell2Obj.transform;
                centersProp.GetArrayElementAtIndex(3).objectReferenceValue = clownObj.transform;
            }
            soCam.ApplyModifiedProperties();

            soManager.FindProperty("cameraController").objectReferenceValue = camController;
            soManager.ApplyModifiedProperties();

            // 8. Iluminación sugerida
            SetupLighting(worldRoot.transform);

            // 9. Configurar Menú Principal, Opciones y Selección de Jugadores
            MenuSetupUtility.SetupMenuAndPlayerSelection();

            AssetDatabase.SaveAssets();
            Debug.Log("[BoardSetupUtility] ¡Mundo 3D, Sistema de Cámara y Menú Principal configurados exitosamente!");
        }

        private static Material GetOrCreateBoardMaterial(string matName, string texturePath)
        {
            string path = MATERIALS_FOLDER + matName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                mat = new Material(shader) { name = matName };

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", Color.white);
                    }
                }

                // Aspectos para que el tablero no brille excesivamente
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static GameObject SetupBoardObject(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
        {
            Transform existing = parent.Find(name);
            GameObject boardObj;
            if (existing != null)
            {
                boardObj = existing.gameObject;
            }
            else
            {
                // Crear un Quad o Cubo aplanado para dar presencia 3D al tablero sobre la mesa
                boardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardObj.name = name;
                boardObj.transform.SetParent(parent);
            }

            boardObj.transform.position = position;
            boardObj.transform.localScale = new Vector3(size.x, 0.2f, size.z);

            MeshRenderer renderer = boardObj.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }

            return boardObj;
        }

        private static BoardPath SetupMainBoardPath(GameObject boardObj)
        {
            Transform pathContainer = boardObj.transform.Find("Tiles_Path");
            if (pathContainer != null)
            {
                Object.DestroyImmediate(pathContainer.gameObject);
            }

            GameObject container = new GameObject("Tiles_Path");
            container.transform.SetParent(boardObj.transform);
            container.transform.localPosition = Vector3.zero;

            BoardPath boardPath = container.AddComponent<BoardPath>();
            List<BoardTile> tilesList = new List<BoardTile>();

            // Coordenadas calculadas en espiral (64 casillas: 0=START a 63=Meta)
            Vector3[] spiralPoints = GenerateMainSpiralPoints(64, 7.6f);

            // Mapeo de tipos de casillas según el GDD (pág. 6 a 10)
            Dictionary<int, TileType> specialTileTypes = GetMainBoardTileTypes();

            for (int i = 0; i < spiralPoints.Length; i++)
            {
                GameObject tileObj = new GameObject($"Tile_{i:D2}");
                tileObj.transform.SetParent(container.transform);
                tileObj.transform.position = boardObj.transform.position + spiralPoints[i];

                BoardTile tile = tileObj.AddComponent<BoardTile>();
                TileType type = specialTileTypes.ContainsKey(i) ? specialTileTypes[i] : TileType.Normal;
                tile.Initialize(i, type, BoardType.MainBoard, GetTileDescriptiveName(i, type));

                // Agregar un pequeño collider de apoyo para selección con el mouse
                BoxCollider boxCol = tileObj.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.9f, 0.2f, 0.9f);
                boxCol.center = new Vector3(0, 0.1f, 0);

                tilesList.Add(tile);
            }

            boardPath.SetTiles(tilesList);

            // Conectar enlaces especiales (Oca a Oca, Puente a Puente)
            ConnectSpecialTiles(tilesList);

            return boardPath;
        }

        private static BoardPath SetupSubBoardPath(GameObject boardObj, BoardType boardType, int count)
        {
            Transform pathContainer = boardObj.transform.Find("Tiles_Path");
            if (pathContainer != null)
            {
                Object.DestroyImmediate(pathContainer.gameObject);
            }

            GameObject container = new GameObject("Tiles_Path");
            container.transform.SetParent(boardObj.transform);
            container.transform.localPosition = Vector3.zero;

            BoardPath boardPath = container.AddComponent<BoardPath>();
            List<BoardTile> tilesList = new List<BoardTile>();

            float radius = 5.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;
                Vector3 localPos = new Vector3(Mathf.Cos(angle) * radius, 0.2f, Mathf.Sin(angle) * radius);

                GameObject tileObj = new GameObject($"Tile_{i:D2}");
                tileObj.transform.SetParent(container.transform);
                tileObj.transform.position = boardObj.transform.position + localPos;

                BoardTile tile = tileObj.AddComponent<BoardTile>();
                tile.Initialize(i, TileType.Normal, boardType, $"{boardType} Casilla {i}");

                tilesList.Add(tile);
            }

            boardPath.SetTiles(tilesList);
            return boardPath;
        }

        private static Vector3[] GenerateMainSpiralPoints(int totalTiles, float maxExtent)
        {
            Vector3[] points = new Vector3[totalTiles];
            float xMin = -maxExtent, xMax = maxExtent;
            float zMin = -maxExtent, zMax = maxExtent;
            float step = 1.35f;

            float curX = xMin;
            float curZ = zMin;
            int direction = 0; // 0=Este(+X), 1=Norte(+Z), 2=Oeste(-X), 3=Sur(-Z)

            for (int i = 0; i < totalTiles; i++)
            {
                points[i] = new Vector3(curX, 0.15f, curZ);

                // Calcular siguiente paso en espiral
                switch (direction)
                {
                    case 0: // Hacia la derecha (+X)
                        curX += step;
                        if (curX >= xMax)
                        {
                            curX = xMax;
                            direction = 1;
                            zMin += step;
                        }
                        break;
                    case 1: // Hacia arriba (+Z)
                        curZ += step;
                        if (curZ >= zMax)
                        {
                            curZ = zMax;
                            direction = 2;
                            xMax -= step;
                        }
                        break;
                    case 2: // Hacia la izquierda (-X)
                        curX -= step;
                        if (curX <= xMin)
                        {
                            curX = xMin;
                            direction = 3;
                            zMax -= step;
                        }
                        break;
                    case 3: // Hacia abajo (-Z)
                        curZ -= step;
                        if (curZ <= zMin)
                        {
                            curZ = zMin;
                            direction = 0;
                            xMin += step;
                        }
                        break;
                }
            }

            // Casilla 63 final en el centro exacto
            points[totalTiles - 1] = new Vector3(0, 0.15f, 0);
            return points;
        }

        private static Dictionary<int, TileType> GetMainBoardTileTypes()
        {
            return new Dictionary<int, TileType>
            {
                { 0, TileType.Start },
                { 1, TileType.Oca },
                { 2, TileType.Muerte },
                { 3, TileType.Dinero },
                { 4, TileType.Puente },
                { 5, TileType.Experiencia },
                { 6, TileType.CartasSalvajes },
                { 7, TileType.Experiencia },
                { 8, TileType.Muerte },
                { 9, TileType.Oca },
                { 12, TileType.Puente },
                { 13, TileType.Boss },       // La Foca
                { 14, TileType.Oca },
                { 18, TileType.Oca },
                { 23, TileType.Oca },
                { 27, TileType.Oca },
                { 31, TileType.Boss },       // La Roca
                { 32, TileType.Oca },
                { 36, TileType.Oca },
                { 41, TileType.Oca },
                { 45, TileType.Oca },
                { 46, TileType.Tienda },
                { 47, TileType.Hechizos },
                { 50, TileType.Oca },
                { 52, TileType.Boss },       // La Trollface
                { 54, TileType.Oca },
                { 59, TileType.Oca },       // Oca final que lleva a la 63
                { 63, TileType.Meta }        // Meta final
            };
        }

        private static string GetTileDescriptiveName(int number, TileType type)
        {
            return type switch
            {
                TileType.Start => "Inicio (START)",
                TileType.Meta => "Casilla 63 - ¡Victoria!",
                TileType.Oca => $"Casilla {number} - Oca",
                TileType.Puente => $"Casilla {number} - Puente",
                TileType.Boss when number == 13 => "Casilla 13 - Boss: La Foca",
                TileType.Boss when number == 31 => "Casilla 31 - Boss: La Roca",
                TileType.Boss => $"Casilla {number} - Boss: La Trollface",
                TileType.Muerte => $"Casilla {number} - Muerte (Infierno)",
                TileType.Tienda => $"Casilla {number} - Tienda",
                TileType.Hechizos => $"Casilla {number} - Hechizos",
                TileType.CartasSalvajes => $"Casilla {number} - Carta Salvaje",
                _ => $"Casilla {number}"
            };
        }

        private static void ConnectSpecialTiles(List<BoardTile> tiles)
        {
            // Conectar Ocas consecutivas
            List<BoardTile> ocas = tiles.FindAll(t => t.Type == TileType.Oca);
            for (int i = 0; i < ocas.Count; i++)
            {
                if (i < ocas.Count - 1)
                {
                    ocas[i].SpecialDestinationTile = ocas[i + 1];
                }
                else
                {
                    // La última oca (59) lleva a la casilla final 63
                    ocas[i].SpecialDestinationTile = tiles[tiles.Count - 1];
                }
            }

            // Conectar Puentes (del 4 al 12 y viceversa)
            BoardTile puente1 = tiles.Find(t => t.TileNumber == 4);
            BoardTile puente2 = tiles.Find(t => t.TileNumber == 12);
            if (puente1 != null && puente2 != null)
            {
                puente1.SpecialDestinationTile = puente2;
                puente2.SpecialDestinationTile = puente1;
            }
        }

        private static void SetupLighting(Transform parent)
        {
            Light light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                lightObj.transform.SetParent(parent);
                light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.color = new Color(1f, 0.95f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
        }
    }
}
