using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Oca2.Board;
using Oca2.Cameras;

namespace Oca2.Editor
{
    /// <summary>
    /// Herramienta de Editor para construir, actualizar y configurar automáticamente el mundo 3D,
    /// las casillas exactas de los 4 tableros (usando BoardLayoutBuilder) y el sistema de cámara.
    /// </summary>
    public static class BoardSetupUtility
    {
        private const string ASSETS_FOLDER = "Assets/OCA 2 assets/";
        private const string MATERIALS_FOLDER = "Assets/Materials/";

        [MenuItem("Tools/La Oca 2/Reconstruir Casillas de los 4 Tableros")]
        public static void RebuildAllBoardTilesMenu()
        {
            BoardManager boardManager = Object.FindAnyObjectByType<BoardManager>();
            if (boardManager != null)
            {
                boardManager.SynchronizeAllBoardLayouts();
                EditorUtility.SetDirty(boardManager);
                EditorSceneManager.MarkSceneDirty(boardManager.gameObject.scene);
                Debug.Log("[BoardSetupUtility] ¡Casillas de los 4 tableros reconstruidas y sincronizadas exitosamente!");
            }
            else
            {
                SetupCompleteWorldAndCamera();
            }
        }

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

            // 4. Instanciar o reutilizar los 4 Tableros sobre la mesa (preservando su posición si ya existen en la escena)
            GameObject mainBoardObj = SetupBoardObject("Board_Principal", new Vector3(31.2f, 0.4f, 0.4f), new Vector3(18, 1, 18), mainBoardMat, worldRoot.transform);
            BoardPath mainBoardPath = BoardLayoutBuilder.RebuildBoardTiles(mainBoardObj, BoardType.MainBoard);

            GameObject hell1Obj = SetupBoardObject("Board_Infierno_1", new Vector3(11.2f, 0.4f, 0.4f), new Vector3(16, 1, 16), hell1Mat, worldRoot.transform);
            BoardPath hell1Path = BoardLayoutBuilder.RebuildBoardTiles(hell1Obj, BoardType.Hell1);

            GameObject hell2Obj = SetupBoardObject("Board_Infierno_2", new Vector3(-8.8f, 0.4f, 0.4f), new Vector3(16, 1, 16), hell2Mat, worldRoot.transform);
            BoardPath hell2Path = BoardLayoutBuilder.RebuildBoardTiles(hell2Obj, BoardType.Hell2);

            GameObject clownObj = SetupBoardObject("Board_Payaso", new Vector3(-28.8f, 0.4f, 0.4f), new Vector3(16, 1, 16), clownMat, worldRoot.transform);
            BoardPath clownPath = BoardLayoutBuilder.RebuildBoardTiles(clownObj, BoardType.ClownWheel);

            // 5. Configurar o Crear BoardManager
            BoardManager boardManager = Object.FindAnyObjectByType<BoardManager>();
            if (boardManager == null)
            {
                GameObject managerObj = new GameObject("BoardManager");
                managerObj.transform.SetParent(worldRoot.transform);
                boardManager = managerObj.AddComponent<BoardManager>();
            }

            SerializedObject soManager = new SerializedObject(boardManager);
            soManager.FindProperty("mainBoardPath").objectReferenceValue = mainBoardPath;
            soManager.FindProperty("hell1BoardPath").objectReferenceValue = hell1Path;
            soManager.FindProperty("hell2BoardPath").objectReferenceValue = hell2Path;
            soManager.FindProperty("clownWheelBoardPath").objectReferenceValue = clownPath;

            soManager.FindProperty("mainBoardCenter").objectReferenceValue = mainBoardObj.transform;
            soManager.FindProperty("hell1BoardCenter").objectReferenceValue = hell1Obj.transform;
            soManager.FindProperty("hell2BoardCenter").objectReferenceValue = hell2Obj.transform;
            soManager.FindProperty("clownWheelBoardCenter").objectReferenceValue = clownObj.transform;

            // 6. Configurar Cámara Principal y BoardCameraController
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

            // 7. Iluminación sugerida
            SetupLighting(worldRoot.transform);

            // 8. Configurar Menú Principal, Opciones, Selección de Jugadores y Combate
            MenuSetupUtility.SetupMenuAndPlayerSelection();

            EditorSceneManager.MarkSceneDirty(worldRoot.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BoardSetupUtility] ¡4 Tableros (64, 42, 17 y 6 casillas), Cámara y Menú Principal configurados exitosamente!");
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

                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static GameObject SetupBoardObject(string name, Vector3 defaultPosition, Vector3 size, Material mat, Transform parent)
        {
            Transform existing = parent.Find(name);
            GameObject boardObj;
            if (existing != null)
            {
                boardObj = existing.gameObject;
            }
            else
            {
                boardObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardObj.name = name;
                boardObj.transform.SetParent(parent);
                boardObj.transform.position = defaultPosition;
                boardObj.transform.localScale = new Vector3(size.x, 0.2f, size.z);
            }

            MeshRenderer renderer = boardObj.GetComponent<MeshRenderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }

            return boardObj;
        }

        private static void SetupLighting(Transform parent)
        {
            Light light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                lightObj.transform.SetParent(parent);
                light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.color = new Color(1f, 0.95f, 0.9f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
            }
        }
    }
}
