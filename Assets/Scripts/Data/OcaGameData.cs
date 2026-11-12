using System;
using UnityEngine;

namespace Oca2.Data
{
    /// <summary>
    /// Las 6 Ocas jugables definidas en el documento de diseño (TP2 - pág. 5).
    /// </summary>
    public enum OcaType
    {
        LaOcaDefault = 0,
        LaOcarina = 1,
        LaOcaDeBoca = 2,
        LaLoca = 3,
        LaBarroca = 4,
        ElXoca = 5
    }

    /// <summary>
    /// Información estática y visual de cada personaje (Oca) disponible para elegir.
    /// </summary>
    [Serializable]
    public class OcaCharacterDefinition
    {
        public OcaType ocaType;
        public string displayName;
        public string assetFileName;
        [TextArea(2, 3)]
        public string description;
        public Color defaultColor;
        public Sprite sprite;
        public Texture2D texture;
    }

    /// <summary>
    /// Configuración de un jugador individual elegida en la pantalla de Selección de Jugador.
    /// Según el TP2 (pág. 5 y 27): cada jugador comienza con 3 monedas y tabla de experiencia a nivel 1.
    /// </summary>
    [Serializable]
    public class PlayerSetupData
    {
        public int playerIndex; // 0 a 7 (Jugador 1 a 8)
        public string playerName;
        public OcaType selectedOca;
        public Color tokenColor;

        // Recursos iniciales según las reglas del TP2
        public int coins = 3;
        public int expLevel = 1;
        public int currentExp = 0;
        public int mana = 0;
        public bool isZombie = false;

        public PlayerSetupData(int index, string name, OcaType oca, Color color, int initialCoins = 3, int initialLevel = 1)
        {
            playerIndex = index;
            playerName = name;
            selectedOca = oca;
            tokenColor = color;
            coins = initialCoins;
            expLevel = initialLevel;
            currentExp = 0;
            mana = 0;
            isZombie = false;
        }
    }

    /// <summary>
    /// Opciones globales del juego y de reglas de la partida configurables desde el Menú de Opciones.
    /// </summary>
    [Serializable]
    public class GameOptionsData
    {
        [Header("Audio")]
        [Range(0f, 1f)] public float masterVolume = 0.85f;
        [Range(0f, 1f)] public float musicVolume = 0.70f;
        [Range(0f, 1f)] public float sfxVolume = 0.85f;

        [Header("Video y Cámara")]
        public bool fullscreen = true;
        public int qualityLevel = 2;
        [Range(0.5f, 2.5f)] public float cameraSpeedMultiplier = 1.0f;

        [Header("Reglas de Partida (TP2)")]
        [Range(0, 10)] public int startingCoins = 3;        // Por defecto 3 monedas (pág. 5)
        [Range(1, 22)] public int startingExpLevel = 1;     // Por defecto Nivel 1, máx 22 (pág. 5 y 18)
        public bool exactRollForGoal = true;                // Caer exactamente en la casilla 63 (pág. 4)
        public bool enableBosses = true;                    // Activar los 3 bosses: La Foca, La Roca, La Trollface

        private const string PREF_MASTER_VOL = "Oca2_MasterVol";
        private const string PREF_MUSIC_VOL = "Oca2_MusicVol";
        private const string PREF_SFX_VOL = "Oca2_SfxVol";
        private const string PREF_FULLSCREEN = "Oca2_Fullscreen";
        private const string PREF_QUALITY = "Oca2_Quality";
        private const string PREF_CAM_SPEED = "Oca2_CamSpeed";
        private const string PREF_START_COINS = "Oca2_StartCoins";
        private const string PREF_START_LEVEL = "Oca2_StartLevel";
        private const string PREF_EXACT_GOAL = "Oca2_ExactGoal";
        private const string PREF_BOSSES = "Oca2_Bosses";

        public void SaveToPrefs()
        {
            PlayerPrefs.SetFloat(PREF_MASTER_VOL, masterVolume);
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, musicVolume);
            PlayerPrefs.SetFloat(PREF_SFX_VOL, sfxVolume);
            PlayerPrefs.SetInt(PREF_FULLSCREEN, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(PREF_QUALITY, qualityLevel);
            PlayerPrefs.SetFloat(PREF_CAM_SPEED, cameraSpeedMultiplier);
            PlayerPrefs.SetInt(PREF_START_COINS, startingCoins);
            PlayerPrefs.SetInt(PREF_START_LEVEL, startingExpLevel);
            PlayerPrefs.SetInt(PREF_EXACT_GOAL, exactRollForGoal ? 1 : 0);
            PlayerPrefs.SetInt(PREF_BOSSES, enableBosses ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void LoadFromPrefs()
        {
            masterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
            musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.70f);
            sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.85f);
            fullscreen = PlayerPrefs.GetInt(PREF_FULLSCREEN, Screen.fullScreen ? 1 : 0) == 1;
            int defaultQuality = QualitySettings.GetQualityLevel();
            qualityLevel = PlayerPrefs.GetInt(PREF_QUALITY, defaultQuality);
            cameraSpeedMultiplier = PlayerPrefs.GetFloat(PREF_CAM_SPEED, 1.0f);
            startingCoins = PlayerPrefs.GetInt(PREF_START_COINS, 3);
            startingExpLevel = PlayerPrefs.GetInt(PREF_START_LEVEL, 1);
            exactRollForGoal = PlayerPrefs.GetInt(PREF_EXACT_GOAL, 1) == 1;
            enableBosses = PlayerPrefs.GetInt(PREF_BOSSES, 1) == 1;
        }

        public void ResetToDefaults()
        {
            masterVolume = 0.85f;
            musicVolume = 0.70f;
            sfxVolume = 0.85f;
            fullscreen = true;
            qualityLevel = QualitySettings.GetQualityLevel();
            cameraSpeedMultiplier = 1.0f;
            startingCoins = 3;
            startingExpLevel = 1;
            exactRollForGoal = true;
            enableBosses = true;
        }
    }
}
