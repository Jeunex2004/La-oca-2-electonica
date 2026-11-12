using System;
using System.Collections.Generic;
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
    /// Las ramas y bifurcaciones del Árbol de Habilidades por Experiencia ("Skill Tree.png" / Reglas pág. 27-28).
    /// </summary>
    public enum SkillBranchType
    {
        Movement = 0,           // Rama 1 Base: +1 Mov, +2 Mov, +1d6 Mov (y bifurcación superior)
        CombatLeft = 1,         // Rama 2A: +1 Att (base), +1d6 Att, +1d6 Att (3 nodos)
        CombatRight = 2,        // Rama 2B: +2 Att, +1d8 Att, +1d10 Att (3 nodos tras base)
        Wealth = 3,             // Rama 3: +1 Moneda/casilla en nivel 2 y nivel 5 (5 nodos)
        Magic = 4,              // Rama 4: +1 Maná/luna en nivel 2 y nivel 5 (5 nodos)
        MovementTopLeft = 5,    // Rama 1 Bifurcación Superior Izq: +1d6 Mov (requiere 3 nodos base de Mov)
        MovementTopRight = 6    // Rama 1 Bifurcación Superior Der: +3 Mov (requiere 3 nodos base de Mov)
    }

    /// <summary>
    /// Los 12 objetos de la Tienda Principal y la Tienda del Infierno ("Tienda.png", "Tienda infierno.png", Reglas pág. 20-26).
    /// </summary>
    public enum ItemType
    {
        RedBull = 0,            // 1 moneda (Ambas tiendas): +1d6 a la tirada de movimiento
        Crucifijo = 1,          // 2 monedas (Tienda Principal): Quita efecto negativo o escapa automáticamente de Infierno 1/2
        GuanteDeDuelo = 2,      // 1 moneda (Ambas tiendas): Combate PvP con el primer jugador con el que te cruces
        Botas = 3,              // 3 monedas (Tienda Principal): +3 a la tirada de movimiento
        Armadura = 4,           // 3 monedas (Tienda Principal): +5 a la tirada de combate
        Lupa = 5,               // 5 monedas (Caja Verde - Tienda Principal): Elegir resultado exacto de la tirada de movimiento
        CartaUNOReverse = 6,    // 7 monedas (Caja Verde - Tienda Principal): Revertir derrota en combate o efecto negativo
        Pentagrama = 7,         // 1 moneda (Tienda Infierno): Resta 1d6 a la tirada de otro jugador / rival en combate
        BotellaEXP = 8,         // 3 monedas (Tienda Infierno): Otorga 1d4 de Experiencia inmediatamente
        BotellaMana = 9,        // 4 monedas (Tienda Infierno): Otorga 1d4 de Maná inmediatamente
        Espada = 10,            // 4 monedas (Caja Roja - Tienda Infierno): +8 a la tirada de combate
        Pistola = 11            // 5 monedas (Caja Roja - Tienda Infierno): Mata instantáneamente a cualquier jugador
    }

    /// <summary>
    /// Los 13 Hechizos oficiales de "Hechizos.jpg" (Reglas pág. 30-31).
    /// </summary>
    public enum SpellType
    {
        None = 0,
        Teletransporte = 1,     // Coste 5: Intercambia posiciones con cualquier jugador en juego
        Payasizacion = 2,       // Coste 4: El próximo jugador con el que te cruces este turno va al Tablero del Payaso
        PasoLigero = 3,         // Coste 3: Eliges el número exacto de pasos en tu siguiente jugada (entre mín y máx)
        Vampirismo = 4,         // Coste 2: Roba 1 moneda a cada jugador con el que te cruces en tu siguiente tirada
        FlechaMagica = 5,       // Coste 1 (Combate): Tira 1d4 y súmalo a tu ataque (se puede usar varias veces)
        BolaDeFuego = 6,        // Coste 1 (Combate): Tira 1d10 y súmalo a tu ataque (1 vez por combate)
        Victoria = 7,           // Coste 35: ¡Ganas automáticamente la partida!
        EldritchBlast = 8,      // Coste 0 (Combate): Tira 1d6 y súmalo a tu ataque (1 vez por combate)
        Congelar = 9,           // Coste 3: Sáltale el próximo turno a otro jugador
        Velocidad = 10,         // Coste 2: Duplica el resultado de tu tirada de movimiento (antes de tirar)
        Ralentizar = 11,        // Coste 1: Divide entre 2 el resultado de la próxima tirada de otro jugador
        Muerte = 12,            // Coste 4: Mata a cualquier jugador instantáneamente
        Limpieza = 13           // Coste 2: Quita a ti o a cualquier otro jugador un efecto negativo activo
    }

    /// <summary>
    /// Información estática y visual de cada personaje (Oca) disponible para elegir.
    /// </summary>
    [Serializable]
    public class OcaCharacterDefinition
    {
        public OcaType ocaType;
        public string displayName;
        public string passiveTitle;
        public string assetFileName;
        [TextArea(2, 3)]
        public string description;
        public Color defaultColor;
        public Sprite sprite;
        public Texture2D texture;
    }

    /// <summary>
    /// Configuración y estado completo de un jugador durante la partida.
    /// </summary>
    [Serializable]
    public class PlayerSetupData
    {
        public int playerIndex; // 0 a 7 (Jugador 1 a 8)
        public string playerName;
        public OcaType selectedOca;
        public Color tokenColor;

        // Recursos principales según las reglas del TP2
        public int coins = 3;
        public int expLevel = 1;
        public int currentExp = 0;
        public int mana = 0;
        public bool isZombie = false;
        public int combatFlatBonus = 0;

        // Árbol de Habilidades por Experiencia (Niveles 1 a 22 -> 21 puntos posibles)
        public int unspentSkillPoints = 0;
        public int movementBranchNodes = 0;     // 0 a 5 (0..3 base + 2 en la bifurcación superior)
        public bool movementTopLeftUnlocked = false;  // Cima Izq de Movimiento (+1d6 Mov)
        public bool movementTopRightUnlocked = false; // Cima Der de Movimiento (+3 Mov)
        public int combatLeftBranchNodes = 0;   // 0 a 3 (incluye nodo base +1 Att en nivel 1)
        public int combatRightBranchNodes = 0;  // 0 a 3 (requiere nodo base +1 Att)
        public int wealthBranchNodes = 0;       // 0 a 5 (+1 Mon/casilla en nodos 2 y 5)
        public int magicBranchNodes = 0;        // 0 a 5 (+1 Maná/luna en nodos 2 y 5)

        // Inventario de objetos y Hechizo actual (máximo 1 hechizo a la vez según Reglas pág. 30)
        public List<ItemType> inventory = new List<ItemType>();
        public SpellType currentSpell = SpellType.None;

        // Modificadores activos para el turno / movimiento / combate actual
        public int tempMoveExtraD6 = 0;
        public int tempMoveFlatBonus = 0;
        public int forcedNextMoveRoll = -1;     // Lupa o Paso Ligero
        public bool doubleNextMoveRoll = false; // Hechizo Velocidad
        public bool halveNextMoveRoll = false;  // Hechizo Ralentizar
        public bool duelGloveActive = false;    // Guante de Duelo
        public bool vampirismActive = false;    // Hechizo Vampirismo
        public bool payasizacionActive = false; // Hechizo Payasización
        public bool skipNextTurn = false;       // Congelar o Carta Salvaje
        public string activeNegativeEffect = ""; // Maldición activa (Cuac, Sin E, etc.)
        public int jinxBeneficiaryPlayerIndex = -1; // Carta Salvaje: Gafe

        // Estado específico de sub-tableros
        public bool hasTakenFirstClownRoll = false;
        public bool canClimbHell1RedArrow = false;

        public PlayerSetupData(int index, string name, OcaType oca, Color color, int initialCoins = 3, int initialLevel = 1)
        {
            playerIndex = index;
            playerName = name;
            selectedOca = oca;
            tokenColor = color;
            ResetMatchState(initialCoins, initialLevel);
        }

        public void ResetMatchState(int initialCoins, int initialLevel)
        {
            coins = initialCoins;
            expLevel = initialLevel;
            currentExp = 0;
            mana = 0;
            isZombie = false;
            combatFlatBonus = 0;

            unspentSkillPoints = Mathf.Max(0, initialLevel - 1);
            movementBranchNodes = 0;
            movementTopLeftUnlocked = false;
            movementTopRightUnlocked = false;
            combatLeftBranchNodes = 0;
            combatRightBranchNodes = 0;
            wealthBranchNodes = 0;
            magicBranchNodes = 0;

            if (inventory == null) inventory = new List<ItemType>();
            else inventory.Clear();

            currentSpell = SpellType.None;
            ClearTurnMoveModifiers();
            halveNextMoveRoll = false;
            skipNextTurn = false;
            activeNegativeEffect = "";
            jinxBeneficiaryPlayerIndex = -1;
            hasTakenFirstClownRoll = false;
            canClimbHell1RedArrow = false;
        }

        public void ClearTurnMoveModifiers()
        {
            tempMoveExtraD6 = 0;
            tempMoveFlatBonus = 0;
            forcedNextMoveRoll = -1;
            doubleNextMoveRoll = false;
            duelGloveActive = false;
            vampirismActive = false;
            payasizacionActive = false;
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
