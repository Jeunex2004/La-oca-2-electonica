using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Oca2.Board;
using Oca2.Combat;
using Oca2.Data;
using Oca2.Players;

namespace Oca2.Core
{
    /// <summary>
    /// Gestor central de la sesión de juego de La Oca 2: Electric Boogaloo.
    /// Administra el catálogo de las 6 Ocas del documento TP2, las opciones de juego,
    /// la configuración de 1 a 8 jugadores, la transición entre SceneMenu y SampleScene,
    /// la instanciación de fichas y de los 3 Bosses, el flujo de turnos y la resolución de combates.
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        private const string BOARD_SCENE_NAME = "SampleScene";
        private const string MENU_SCENE_NAME = "SceneMenu";

        [Header("Catálogo de Ocas (TP2 - Pág. 5)")]
        [SerializeField] private List<OcaCharacterDefinition> ocaCatalog = new List<OcaCharacterDefinition>();

        [Header("Íconos de Recursos")]
        [SerializeField] private Sprite coinIcon;
        [SerializeField] private Texture2D coinTexture;
        [SerializeField] private Sprite expIcon;
        [SerializeField] private Texture2D expTexture;
        [SerializeField] private Sprite manaIcon;
        [SerializeField] private Texture2D manaTexture;

        [Header("Configuración Actual")]
        [SerializeField] private GameOptionsData options = new GameOptionsData();
        [SerializeField] private List<PlayerSetupData> configuredPlayers = new List<PlayerSetupData>();

        private readonly List<PlayerOcaToken> activeTokens = new List<PlayerOcaToken>();
        private GameObject tokensContainer;
        private GameObject scenePlaceholderToken;

        private bool isMatchActive = false;
        private bool pendingMatchStartAfterSceneLoad = false;
        private int currentTurnPlayerIndex = 0;
        private int currentRound = 1;
        private int moonPhase = 0; // 0 a 3 (Luna llena al completar 4 rondas -> +1 Maná según pág. 18)

        public IReadOnlyList<OcaCharacterDefinition> OcaCatalog => ocaCatalog;
        public GameOptionsData Options => options;
        public List<PlayerSetupData> ConfiguredPlayers => configuredPlayers;
        public IReadOnlyList<PlayerOcaToken> ActiveTokens => activeTokens;
        public bool IsMatchActive => isMatchActive;
        public int CurrentTurnPlayerIndex => currentTurnPlayerIndex;
        public int CurrentRound => currentRound;
        public int MoonPhase => moonPhase;

        public Sprite CoinIcon => GetOrCreateSprite(ref coinIcon, coinTexture);
        public Sprite ExpIcon => GetOrCreateSprite(ref expIcon, expTexture);
        public Sprite ManaIcon => GetOrCreateSprite(ref manaIcon, manaTexture);

        public event Action OnMatchStarted;
        public event Action OnMatchEndedToMenu;
        public event Action<int, int, string> OnTurnStateUpdated; // (playerIdx, lastRoll, statusMessage)

        public static readonly Color[] DefaultPlayerColors = new Color[]
        {
            new Color(0.95f, 0.25f, 0.25f), // Jugador 1: Rojo
            new Color(0.20f, 0.55f, 0.95f), // Jugador 2: Azul
            new Color(0.20f, 0.80f, 0.35f), // Jugador 3: Verde
            new Color(0.95f, 0.80f, 0.15f), // Jugador 4: Amarillo
            new Color(0.70f, 0.30f, 0.95f), // Jugador 5: Púrpura
            new Color(0.95f, 0.50f, 0.15f), // Jugador 6: Naranja
            new Color(0.15f, 0.85f, 0.85f), // Jugador 7: Cian
            new Color(0.95f, 0.35f, 0.70f)  // Jugador 8: Rosa
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GetComponent<CombatManager>() == null)
            {
                gameObject.AddComponent<CombatManager>();
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            EnsureDefaultOcaCatalog();
            options.LoadFromPrefs();
            ApplyOptions();

            if (configuredPlayers.Count == 0)
            {
                SetPlayerCount(2);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (pendingMatchStartAfterSceneLoad && FindAnyObjectByType<BoardManager>() != null)
            {
                pendingMatchStartAfterSceneLoad = false;
                SpawnPlayersAndBeginMatch();
            }
        }

        /// <summary>
        /// Garantiza que las 6 Ocas oficiales del documento TP2 estén registradas con sus nombres y descripciones.
        /// </summary>
        public void EnsureDefaultOcaCatalog()
        {
            if (ocaCatalog == null) ocaCatalog = new List<OcaCharacterDefinition>();

            if (ocaCatalog.Count < 6)
            {
                ocaCatalog.Clear();
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaOcaDefault,
                    displayName = "La Oca Default",
                    assetFileName = "La oca default.png",
                    description = "La oca clásica e inmutable. Lista para recorrer las 63 casillas sin miedo a nada.",
                    defaultColor = new Color(0.95f, 0.95f, 0.95f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaOcarina,
                    displayName = "La Ocarina",
                    assetFileName = "La ocarina.png",
                    description = "Vestida de héroe legendario. Dicen que sus graznidos pueden alterar el destino del tablero.",
                    defaultColor = new Color(0.20f, 0.78f, 0.30f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaOcaDeBoca,
                    displayName = "La Oca de Boca",
                    assetFileName = "La oca de boca.png",
                    description = "Con la azul y oro puesta. Juega cada tirada como una final en La Bombonera.",
                    defaultColor = new Color(0.15f, 0.45f, 0.95f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaLoca,
                    displayName = "La Loca",
                    assetFileName = "La loca.png",
                    description = "Impredecible y temeraria. Disfruta el caos de las Cartas Salvajes y el tablero del Payaso.",
                    defaultColor = new Color(0.92f, 0.22f, 0.25f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaBarroca,
                    displayName = "La Barroca",
                    assetFileName = "La barroca.png",
                    description = "Elegancia clásica del siglo XVII. Administra sus monedas y hechizos con distinguida frialdad.",
                    defaultColor = new Color(0.25f, 0.65f, 0.88f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.ElXoca,
                    displayName = "El Xoca",
                    assetFileName = "El xoca.png",
                    description = "¡No es un juego, es la vida real! Experto en tryhardear el árbol de experiencia hasta nivel 22.",
                    defaultColor = new Color(0.35f, 0.35f, 0.40f)
                });
            }

#if UNITY_EDITOR
            LoadAssetsInEditorIfNeeded();
#endif

            for (int i = 0; i < ocaCatalog.Count; i++)
            {
                var def = ocaCatalog[i];
                if (def.sprite == null && def.texture != null)
                {
                    def.sprite = CreateSpriteFromTexture(def.texture);
                }
            }
        }

#if UNITY_EDITOR
        public void LoadAssetsInEditorIfNeeded()
        {
            const string folder = "Assets/OCA 2 assets/";
            foreach (var def in ocaCatalog)
            {
                if (def.texture == null && !string.IsNullOrEmpty(def.assetFileName))
                {
                    def.texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + def.assetFileName);
                }
                if (def.sprite == null && !string.IsNullOrEmpty(def.assetFileName))
                {
                    def.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(folder + def.assetFileName);
                    if (def.sprite == null && def.texture != null)
                    {
                        def.sprite = CreateSpriteFromTexture(def.texture);
                    }
                }
            }

            if (coinTexture == null) coinTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Moneda.png");
            if (expTexture == null) expTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "EXP.png");
            if (manaTexture == null) manaTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Mana.png");
        }
#endif

        private static Sprite GetOrCreateSprite(ref Sprite cachedSprite, Texture2D tex)
        {
            if (cachedSprite == null && tex != null)
            {
                cachedSprite = CreateSpriteFromTexture(tex);
            }
            return cachedSprite;
        }

        public static Sprite CreateSpriteFromTexture(Texture2D tex)
        {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public OcaCharacterDefinition GetOcaDefinition(OcaType type)
        {
            EnsureDefaultOcaCatalog();
            foreach (var def in ocaCatalog)
            {
                if (def.ocaType == type)
                {
                    if (def.sprite == null && def.texture != null)
                    {
                        def.sprite = CreateSpriteFromTexture(def.texture);
                    }
                    return def;
                }
            }
            return ocaCatalog.Count > 0 ? ocaCatalog[0] : null;
        }

        /// <summary>
        /// Ajusta la cantidad de jugadores entre 1 y 8 (según TP2 pág. 3: "Pensado para 1 a 8 jugadores").
        /// </summary>
        public void SetPlayerCount(int count)
        {
            count = Mathf.Clamp(count, 1, 8);

            while (configuredPlayers.Count < count)
            {
                int idx = configuredPlayers.Count;
                OcaType defaultOca = (OcaType)(idx % 6);
                Color col = DefaultPlayerColors[idx % DefaultPlayerColors.Length];
                configuredPlayers.Add(new PlayerSetupData(
                    idx,
                    $"Jugador {idx + 1}",
                    defaultOca,
                    col,
                    options.startingCoins,
                    options.startingExpLevel
                ));
            }

            while (configuredPlayers.Count > count)
            {
                configuredPlayers.RemoveAt(configuredPlayers.Count - 1);
            }
        }

        /// <summary>
        /// Aplica las opciones actuales al motor (Audio, Pantalla, Calidad) y las guarda en PlayerPrefs.
        /// </summary>
        public void ApplyOptions()
        {
            AudioListener.volume = Mathf.Clamp01(options.masterVolume);

            if (Screen.fullScreen != options.fullscreen)
            {
                Screen.fullScreen = options.fullscreen;
            }

            int maxQuality = QualitySettings.names.Length - 1;
            if (maxQuality >= 0)
            {
                int clampedQuality = Mathf.Clamp(options.qualityLevel, 0, maxQuality);
                if (QualitySettings.GetQualityLevel() != clampedQuality)
                {
                    QualitySettings.SetQualityLevel(clampedQuality);
                }
            }

            options.SaveToPrefs();
        }

        /// <summary>
        /// Inicia la partida con los jugadores configurados en la pantalla de Selección de Jugador.
        /// Si se está en SceneMenu (sin el tablero cargado), carga SampleScene automáticamente
        /// y luego instancia las fichas en la casilla START.
        /// </summary>
        public void StartMatch()
        {
            EnsureDefaultOcaCatalog();

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();

            if (boardMgr == null && Application.CanStreamedLevelBeLoaded(BOARD_SCENE_NAME))
            {
                pendingMatchStartAfterSceneLoad = true;
                SceneManager.LoadScene(BOARD_SCENE_NAME);
                return;
            }

            SpawnPlayersAndBeginMatch();
        }

        private void SpawnPlayersAndBeginMatch()
        {
            // Ocultar la ficha de prueba estática si existe en la escena
            if (scenePlaceholderToken == null)
            {
                scenePlaceholderToken = GameObject.Find("Ficha Oca");
            }
            if (scenePlaceholderToken != null)
            {
                scenePlaceholderToken.SetActive(false);
            }

            ClearActiveTokens();

            if (tokensContainer == null)
            {
                tokensContainer = new GameObject("Players_Tokens_Container");
            }

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();

            // Instanciar los 3 Bosses en las casillas 13 (La Foca), 31 (La Roca) y 52 (La Trollface)
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.SpawnBossesOnBoard(boardMgr, options.enableBosses);
            }

            BoardTile startTile = boardMgr != null ? boardMgr.GetTile(BoardType.MainBoard, 0) : null;

            int totalPlayers = configuredPlayers.Count;
            for (int i = 0; i < totalPlayers; i++)
            {
                PlayerSetupData pData = configuredPlayers[i];
                pData.playerIndex = i;
                pData.coins = options.startingCoins;
                pData.expLevel = options.startingExpLevel;
                pData.currentExp = 0;
                pData.mana = 0;
                pData.isZombie = false;
                pData.combatFlatBonus = 0;

                OcaCharacterDefinition ocaDef = GetOcaDefinition(pData.selectedOca);
                Sprite ocaSprite = ocaDef != null ? ocaDef.sprite : null;

                GameObject tokenObj = new GameObject($"PlayerToken_{i + 1}");
                tokenObj.transform.SetParent(tokensContainer.transform, false);

                PlayerOcaToken token = tokenObj.AddComponent<PlayerOcaToken>();
                token.Initialize(pData, ocaSprite);

                if (startTile != null)
                {
                    token.PlaceOnTileImmediate(startTile, i, totalPlayers);
                }
                else
                {
                    token.transform.position = new Vector3(-7.6f + i * 0.45f, 0.4f, -7.6f);
                }

                activeTokens.Add(token);
            }

            currentTurnPlayerIndex = 0;
            currentRound = 1;
            moonPhase = 0;
            isMatchActive = true;

            if (boardMgr != null)
            {
                boardMgr.SwitchToBoard(BoardType.MainBoard);
                if (activeTokens.Count > 0 && boardMgr.CameraController != null)
                {
                    boardMgr.CameraController.FocusOnTarget(activeTokens[0].transform, 15f);
                }
            }

            OnMatchStarted?.Invoke();
            OnTurnStateUpdated?.Invoke(
                currentTurnPlayerIndex,
                0,
                $"¡Partida iniciada! Cada jugador recibió {options.startingCoins} monedas y EXP Nivel {options.startingExpLevel}."
            );
        }

        /// <summary>
        /// Lanza el dado de movimiento (1d6) para el jugador en turno y verifica intercepciones de Bosses y efectos de casilla
        /// en los 4 tableros (Principal, Infierno 1, Infierno 2 y Rueda del Payaso).
        /// </summary>
        public void RollDiceForCurrentPlayer()
        {
            if (!isMatchActive || activeTokens.Count == 0) return;
            if (CombatManager.Instance != null && CombatManager.Instance.IsInCombat) return;

            PlayerOcaToken currentToken = activeTokens[currentTurnPlayerIndex];
            if (currentToken == null || currentToken.IsMoving) return;

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();
            if (boardMgr == null) return;

            BoardPath path = boardMgr.GetPath(currentToken.CurrentBoard);
            if (path == null) return;

            int roll = UnityEngine.Random.Range(1, 7); // 1d6 por defecto (pág. 6)

            // Trazar secuencia preliminar de casillas según las reglas del tablero actual
            List<BoardTile> stepSequence = path.TraceStepSequence(
                currentToken.CurrentTileIndex,
                roll,
                options.exactRollForGoal
            );

            int targetIndex = stepSequence.Count > 0
                ? stepSequence[stepSequence.Count - 1].TileNumber
                : currentToken.CurrentTileIndex;

            // Regla TP2 (pág. 8-9): Si en el Tablero Principal pasa por encima de un Boss vivo, debe detenerse y enfrentarse a él
            BossDefinition interceptedBoss = null;
            if (options.enableBosses && CombatManager.Instance != null && currentToken.CurrentBoard == BoardType.MainBoard)
            {
                int rawReach = Mathf.Min(currentToken.CurrentTileIndex + roll, path.TileCount - 1);
                interceptedBoss = CombatManager.Instance.GetBossInterceptOnPath(
                    currentToken.CurrentBoard,
                    currentToken.CurrentTileIndex,
                    rawReach
                );

                if (interceptedBoss != null)
                {
                    targetIndex = interceptedBoss.tileIndex;
                    stepSequence = path.TraceStepSequence(
                        currentToken.CurrentTileIndex,
                        roll,
                        options.exactRollForGoal,
                        targetIndex
                    );
                }
            }

            boardMgr.SwitchToBoard(currentToken.CurrentBoard);
            if (boardMgr.CameraController != null)
            {
                boardMgr.CameraController.FocusOnTarget(currentToken.transform, 14f);
            }

            string playerName = currentToken.PlayerData.playerName;
            string moveMsg = interceptedBoss != null
                ? $"{playerName} tiró un {roll}, ¡pero el Boss {interceptedBoss.displayName} bloquea el paso en la casilla {targetIndex}!"
                : $"{playerName} tiró un {roll} y avanza a la casilla {targetIndex} ({currentToken.CurrentBoard})...";

            OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, roll, moveMsg);

            currentToken.MoveAlongSequence(stepSequence, currentTurnPlayerIndex, activeTokens.Count, () =>
            {
                if (interceptedBoss != null && CombatManager.Instance != null)
                {
                    CombatManager.Instance.StartBossCombat(currentToken, interceptedBoss, (encounter) =>
                    {
                        HandleCombatConcluded(encounter, boardMgr, roll);
                    });
                }
                else
                {
                    ResolveTileEffect(currentToken, boardMgr, roll);
                }
            });
        }

        private void ResolveTileEffect(PlayerOcaToken token, BoardManager boardMgr, int lastRoll)
        {
            BoardTile landedTile = boardMgr.GetTile(token.CurrentBoard, token.CurrentTileIndex);
            if (landedTile == null)
            {
                AdvanceTurn();
                return;
            }

            PlayerSetupData pData = token.PlayerData;
            bool extraTurn = false;
            bool playerDiedOnTile = false;
            string effectMsg = $"{pData.playerName} cayó en {landedTile.TileName}.";

            switch (landedTile.Type)
            {
                case TileType.Dinero:
                    pData.coins += 1;
                    effectMsg = $"¡{pData.playerName} cayó en Casilla de Dinero ({landedTile.TileNumber}) y ganó +1 Moneda! (Total: {pData.coins})";
                    break;

                case TileType.Experiencia:
                    GainPlayerExperience(pData, 1);
                    effectMsg = $"¡{pData.playerName} cayó en Casilla de Experiencia ({landedTile.TileNumber}) y ganó +1 EXP! (Nivel {pData.expLevel})";
                    break;

                case TileType.Oca:
                    if (landedTile.SpecialDestinationTile != null)
                    {
                        BoardTile nextOca = landedTile.SpecialDestinationTile;
                        token.PlaceOnTileImmediate(nextOca, currentTurnPlayerIndex, activeTokens.Count);
                        if (nextOca.Type == TileType.Meta)
                        {
                            effectMsg = $"¡De oca a oca (59 -> 63)! ¡{pData.playerName} llegó a la Casilla 63 y GANA LA PARTIDA!";
                            OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, lastRoll, effectMsg);
                            return;
                        }
                        extraTurn = true;
                        effectMsg = $"¡De oca a oca! {pData.playerName} salta a la casilla {nextOca.TileNumber} y vuelve a tirar.";
                    }
                    break;

                case TileType.Puente:
                    if (landedTile.SpecialDestinationTile != null)
                    {
                        BoardTile otherBridge = landedTile.SpecialDestinationTile;
                        token.PlaceOnTileImmediate(otherBridge, currentTurnPlayerIndex, activeTokens.Count);
                        extraTurn = true;
                        effectMsg = $"¡De puente a puente! {pData.playerName} cruza a la casilla {otherBridge.TileNumber} y vuelve a tirar.";
                    }
                    break;

                case TileType.BobEsponja:
                    effectMsg = $"{pData.playerName} cayó en Bob Esponja ({landedTile.TileNumber}): No pasa nada :)";
                    break;

                case TileType.Zombi:
                    pData.isZombie = true;
                    ReturnPlayerToMainBoardStart(token, boardMgr);
                    effectMsg = $"¡{pData.playerName} llegó a la casilla Zombi, se convirtió en ZOMBI inmortal y regresa al START del Tablero Principal!";
                    break;

                case TileType.Muerte:
                    if (pData.isZombie)
                    {
                        effectMsg = $"{pData.playerName} cayó en Muerte, ¡pero como es Zombi no le pasa nada!";
                    }
                    else
                    {
                        playerDiedOnTile = true;
                        string hellName = KillPlayerAndSendToHell(token, boardMgr);
                        effectMsg = $"¡{pData.playerName} cayó en la Calavera y fue enviado a {hellName}!";
                    }
                    break;

                case TileType.Meta:
                    if (token.CurrentBoard == BoardType.MainBoard)
                    {
                        effectMsg = $"¡¡VICTORIA!! ¡{pData.playerName} llegó a la Casilla 63 y ganó La Oca 2: Electric Boogaloo!";
                        OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, lastRoll, effectMsg);
                        return;
                    }
                    else if (token.CurrentBoard == BoardType.Hell1)
                    {
                        ReturnPlayerToMainBoardStart(token, boardMgr);
                        effectMsg = $"¡{pData.playerName} alcanzó el EXIT de Infierno 1 y regresa al START del Tablero Principal!";
                    }
                    else if (token.CurrentBoard == BoardType.Hell2)
                    {
                        pData.isZombie = true;
                        ReturnPlayerToMainBoardStart(token, boardMgr);
                        effectMsg = $"¡{pData.playerName} alcanzó el EXIT de Infierno 2, se convirtió en ZOMBI y regresa al START del Tablero Principal!";
                    }
                    else if (token.CurrentBoard == BoardType.ClownWheel)
                    {
                        ReturnPlayerToMainBoardStart(token, boardMgr);
                        effectMsg = $"¡{pData.playerName} cayó exactamente en EXIT y escapa de la Rueda del Payaso hacia el Tablero Principal!";
                    }
                    break;

                case TileType.Normal:
                    if (token.CurrentBoard == BoardType.ClownWheel)
                    {
                        effectMsg = $"¡NO! {pData.playerName} cayó en el sector '{landedTile.TileName}' y sigue en la Dimensión del Payaso.";
                    }
                    break;
            }

            // Regla especial de Infierno 1 ("Tablero infierno 1.png"):
            // Si el jugador cae en una casilla con flecha roja ascendente (SpecialDestinationTile), sube al siguiente nivel o al EXIT
            if (token.CurrentBoard == BoardType.Hell1 && landedTile.SpecialDestinationTile != null)
            {
                BoardTile dest = landedTile.SpecialDestinationTile;
                if (dest.Type == TileType.Meta)
                {
                    ReturnPlayerToMainBoardStart(token, boardMgr);
                    effectMsg += $" ¡Subió por la flecha roja hacia el EXIT y regresa al START del Tablero Principal!";
                }
                else
                {
                    token.PlaceOnTileImmediate(dest, currentTurnPlayerIndex, activeTokens.Count);
                    effectMsg += $" ¡Flecha roja! Sube al siguiente anillo ({dest.TileName}).";
                }
            }

            // Verificar si después del efecto de casilla hay otro jugador en la misma posición para iniciar un Combate PvP
            if (!playerDiedOnTile && CombatManager.Instance != null)
            {
                PlayerOcaToken opponent = CombatManager.Instance.GetPvPOpponentOnTile(token, activeTokens);
                if (opponent != null)
                {
                    bool pendingExtraTurn = extraTurn;
                    CombatManager.Instance.StartPvPCombat(token, opponent, (encounter) =>
                    {
                        HandleCombatConcluded(encounter, boardMgr, lastRoll, pendingExtraTurn && encounter.attackerWon);
                    });
                    return;
                }
            }

            if (!extraTurn)
            {
                AdvanceTurn(effectMsg, lastRoll);
            }
            else
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, lastRoll, effectMsg);
            }
        }

        private void ReturnPlayerToMainBoardStart(PlayerOcaToken token, BoardManager boardMgr)
        {
            if (token == null || boardMgr == null) return;
            BoardTile mainStart = boardMgr.GetTile(BoardType.MainBoard, 0);
            if (mainStart != null)
            {
                token.PlaceOnTileImmediate(mainStart, token.PlayerData.playerIndex, activeTokens.Count);
            }
        }

        /// <summary>
        /// Aplica las recompensas (+3 EXP al ganador) y castigos (muerte / envío al Infierno al perdedor)
        /// al concluir un combate contra un Boss o contra otro jugador (TP2 pág. 17).
        /// </summary>
        private void HandleCombatConcluded(CombatEncounter encounter, BoardManager boardMgr, int lastRoll, bool grantExtraTurn = false)
        {
            string summaryMsg;

            if (encounter.isBossCombat)
            {
                PlayerOcaToken player = encounter.attackerToken;
                BossDefinition boss = encounter.targetBoss;

                if (encounter.attackerWon)
                {
                    GainPlayerExperience(player.PlayerData, 3); // +3 EXP al victorioso (pág. 17)
                    summaryMsg = $"¡{player.PlayerData.playerName} venció a {boss.displayName} y obtuvo +3 EXP (Nivel {player.PlayerData.expLevel})!";
                }
                else
                {
                    if (player.PlayerData.isZombie)
                    {
                        summaryMsg = $"{player.PlayerData.playerName} perdió contra {boss.displayName}, ¡pero al ser Zombi no puede morir!";
                    }
                    else
                    {
                        string hellDest = KillPlayerAndSendToHell(player, boardMgr);
                        summaryMsg = $"¡{player.PlayerData.playerName} fue derrotado por {boss.displayName} y enviado a {hellDest}!";
                    }
                    grantExtraTurn = false;
                }
            }
            else
            {
                PlayerOcaToken winner = encounter.attackerWon ? encounter.attackerToken : encounter.defenderPlayerToken;
                PlayerOcaToken loser = encounter.attackerWon ? encounter.defenderPlayerToken : encounter.attackerToken;

                GainPlayerExperience(winner.PlayerData, 3); // +3 EXP al victorioso (pág. 17)

                if (loser.PlayerData.isZombie)
                {
                    summaryMsg = $"¡{winner.PlayerData.playerName} venció a {loser.PlayerData.playerName} (+3 EXP)! Como {loser.PlayerData.playerName} es Zombi, no muere.";
                }
                else
                {
                    string hellDest = KillPlayerAndSendToHell(loser, boardMgr);
                    summaryMsg = $"¡{winner.PlayerData.playerName} venció a {loser.PlayerData.playerName} (+3 EXP)! {loser.PlayerData.playerName} muere y va a {hellDest}.";
                }
            }

            if (grantExtraTurn)
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, lastRoll, summaryMsg + " ¡Conserva su tirada extra!");
            }
            else
            {
                AdvanceTurn(summaryMsg, lastRoll);
            }
        }

        private string KillPlayerAndSendToHell(PlayerOcaToken victim, BoardManager boardMgr)
        {
            if (victim == null || boardMgr == null) return "Infierno";

            BoardType targetHell = (victim.CurrentBoard == BoardType.MainBoard || victim.CurrentBoard == BoardType.ClownWheel)
                ? BoardType.Hell1
                : BoardType.Hell2;

            BoardTile hellStart = boardMgr.GetTile(targetHell, 0);
            if (hellStart != null)
            {
                victim.PlaceOnTileImmediate(hellStart, victim.PlayerData.playerIndex, activeTokens.Count);
            }

            return targetHell == BoardType.Hell1 ? "Infierno 1" : "Infierno 2";
        }

        public static void GainPlayerExperience(PlayerSetupData pData, int amount)
        {
            if (pData == null) return;
            pData.currentExp += amount;
            // Según pág. 18 del TP2: Para subir del nivel N hace falta N de experiencia, hasta nivel 22 máximo
            while (pData.expLevel < 22 && pData.currentExp >= pData.expLevel)
            {
                pData.currentExp -= pData.expLevel;
                pData.expLevel++;
            }
        }

        private void AdvanceTurn(string previousMessage = "", int lastRoll = 0)
        {
            if (activeTokens.Count == 0) return;

            currentTurnPlayerIndex++;
            if (currentTurnPlayerIndex >= activeTokens.Count)
            {
                currentTurnPlayerIndex = 0;
                currentRound++;
                moonPhase = (moonPhase + 1) % 4;

                if (moonPhase == 3)
                {
                    foreach (var t in activeTokens)
                    {
                        t.PlayerData.mana += 1;
                    }
                    previousMessage += " ¡Luna Llena! Todos los jugadores reciben +1 de Maná.";
                }
            }

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();

            PlayerOcaToken nextToken = activeTokens[currentTurnPlayerIndex];
            if (boardMgr != null && nextToken != null)
            {
                boardMgr.SwitchToBoard(nextToken.CurrentBoard);
                if (boardMgr.CameraController != null)
                {
                    boardMgr.CameraController.FocusOnTarget(nextToken.transform, 15f);
                }
            }

            string turnNotice = string.IsNullOrEmpty(previousMessage)
                ? $"Turno de {nextToken.PlayerData.playerName}"
                : $"{previousMessage} | Turno de {nextToken.PlayerData.playerName}";

            OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, lastRoll, turnNotice);
        }

        public void ReturnToMainMenu()
        {
            isMatchActive = false;
            ClearActiveTokens();

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.ClearBossTokens();
            }

            if (scenePlaceholderToken != null)
            {
                scenePlaceholderToken.SetActive(true);
            }

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();
            if (boardMgr != null)
            {
                boardMgr.SwitchToBoard(BoardType.MainBoard);
            }

            OnMatchEndedToMenu?.Invoke();
        }

        private void ClearActiveTokens()
        {
            foreach (var token in activeTokens)
            {
                if (token != null)
                {
                    Destroy(token.gameObject);
                }
            }
            activeTokens.Clear();
        }
    }
}
