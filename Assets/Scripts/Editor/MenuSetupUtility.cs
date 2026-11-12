using UnityEngine;
using UnityEditor;
using Oca2.Core;
using Oca2.UI;

namespace Oca2.Editor
{
    /// <summary>
    /// Herramienta de Editor para configurar los Sprites de las 6 Ocas y crear el gestor
    /// de Menú Principal, Opciones y Selección de Jugadores en la escena activa.
    /// </summary>
    public static class MenuSetupUtility
    {
        private const string ASSETS_FOLDER = "Assets/OCA 2 assets/";

        private static readonly string[] OcaSpriteFiles = new string[]
        {
            "La oca default.png",
            "La ocarina.png",
            "La oca de boca.png",
            "La loca.png",
            "La barroca.png",
            "El xoca.png",
            "Moneda.png",
            "EXP.png",
            "Mana.png"
        };

        [MenuItem("Tools/La Oca 2/Configurar Menú Principal y Selección de Jugadores")]
        public static void SetupMenuAndPlayerSelection()
        {
            // 1. Asegurar que las imágenes de las Ocas tengan transparencia y tipo Sprite
            EnsureOcaSpritesConfigured();

            // 2. Crear o actualizar el objeto GameSession_UI_Manager en la escena
            GameSessionManager sessionMgr = Object.FindAnyObjectByType<GameSessionManager>();
            GameObject managerObj;
            if (sessionMgr == null)
            {
                managerObj = new GameObject("GameSession_UI_Manager");
                Undo.RegisterCreatedObjectUndo(managerObj, "Create GameSession UI Manager");
                sessionMgr = managerObj.AddComponent<GameSessionManager>();
            }
            else
            {
                managerObj = sessionMgr.gameObject;
            }

            if (managerObj.GetComponent<MainMenuUIController>() == null)
            {
                managerObj.AddComponent<MainMenuUIController>();
            }

            sessionMgr.EnsureDefaultOcaCatalog();
            sessionMgr.LoadAssetsInEditorIfNeeded();
            sessionMgr.SetPlayerCount(2);

            EditorUtility.SetDirty(sessionMgr);
            EditorUtility.SetDirty(managerObj);
            AssetDatabase.SaveAssets();

            Debug.Log("[MenuSetupUtility] ¡Menú Principal, Opciones y Selección de Jugadores configurados exitosamente!");
        }

        public static void EnsureOcaSpritesConfigured()
        {
            bool anyModified = false;

            foreach (string fileName in OcaSpriteFiles)
            {
                string path = ASSETS_FOLDER + fileName;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool needsReimport = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        needsReimport = true;
                    }
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        needsReimport = true;
                    }
                    if (needsReimport)
                    {
                        importer.SaveAndReimport();
                        anyModified = true;
                    }
                }
            }

            if (anyModified)
            {
                AssetDatabase.Refresh();
            }
        }
    }
}
