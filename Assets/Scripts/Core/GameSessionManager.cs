using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Oca2.Board;
using Oca2.CardsAndMagic;
using Oca2.Combat;
using Oca2.Data;
using Oca2.Items;
using Oca2.Players;
using Oca2.Progression;

namespace Oca2.Core
{
    /// <summary>
    /// Gestor central de la sesión de juego de La Oca 2: Electric Boogaloo.
    /// Administra:
    /// - Catálogo de las 6 Ocas y sus Habilidades Pasivas (TP2 pág. 5 y 23)
    /// - Árbol de Habilidades de 22 niveles por Experiencia ("Skill Tree.png")
    /// - Tienda Principal ("Tienda.png"), Tienda del Infierno ("Tienda infierno.png") y los 12 Objetos
    /// - Mazo de Cartas Salvajes ("Wild Cards.jpg") y los 13 Hechizos ("Hechizos.jpg") con Fases Lunares
    /// - Los 3 Bosses, combates PvE/PvP y navegación entre los 4 tableros.
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
        private int moonPhase = 0; // 0 a 3 (Luna llena al completar 4 rondas -> +Maná según pág. 18 y Skill Tree)

        // Estado de tienda abierta durante el movimiento (al pasar o caer exactamente en casilla Tienda)
        private bool isCurrentShopHell = false;
        private bool currentShopLandedExactly = false;

        public IReadOnlyList<OcaCharacterDefinition> OcaCatalog => ocaCatalog;
        public GameOptionsData Options => options;
        public List<PlayerSetupData> ConfiguredPlayers => configuredPlayers;
        public IReadOnlyList<PlayerOcaToken> ActiveTokens => activeTokens;
        public bool IsMatchActive => isMatchActive;
        public int CurrentTurnPlayerIndex => currentTurnPlayerIndex;
        public int CurrentRound => currentRound;
        public int MoonPhase => moonPhase;
        public bool IsCurrentShopHell => isCurrentShopHell;
        public bool CurrentShopLandedExactly => currentShopLandedExactly;

        public Sprite CoinIcon => GetOrCreateSprite(ref coinIcon, coinTexture);
        public Sprite ExpIcon => GetOrCreateSprite(ref expIcon, expTexture);
        public Sprite ManaIcon => GetOrCreateSprite(ref manaIcon, manaTexture);

        public event Action OnMatchStarted;
        public event Action OnMatchEndedToMenu;
        public event Action<int, int, string> OnTurnStateUpdated; // (playerIdx, lastRoll, statusMessage)
        public event Action<PlayerOcaToken, bool, bool> OnShopTriggered; // (buyerToken, isHellShop, landedExactly)
        public event Action<PlayerOcaToken, WildCardDefinition, string> OnWildCardDrawn; // (token, card, resultText)

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
            ShopAndItemsSystem.InitializeCatalog();
            SpellsAndWildCardsSystem.InitializeSpells();
            SpellsAndWildCardsSystem.InitializeWildCards();

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
        /// Garantiza que las 6 Ocas oficiales del documento TP2 estén registradas con sus nombres, descripciones y habilidades pasivas.
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
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.LaOcaDefault),
                    defaultColor = new Color(0.95f, 0.95f, 0.95f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaOcarina,
                    displayName = "La Ocarina",
                    assetFileName = "La ocarina.png",
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.LaOcarina),
                    defaultColor = new Color(0.20f, 0.78f, 0.30f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaOcaDeBoca,
                    displayName = "La Oca de Boca",
                    assetFileName = "La oca de boca.png",
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.LaOcaDeBoca),
                    defaultColor = new Color(0.15f, 0.45f, 0.95f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaLoca,
                    displayName = "La Loca",
                    assetFileName = "La loca.png",
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.LaLoca),
                    defaultColor = new Color(0.92f, 0.22f, 0.25f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.LaBarroca,
                    displayName = "La Barroca",
                    assetFileName = "La barroca.png",
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.LaBarroca),
                    defaultColor = new Color(0.25f, 0.65f, 0.88f)
                });
                ocaCatalog.Add(new OcaCharacterDefinition
                {
                    ocaType = OcaType.ElXoca,
                    displayName = "El Xoca",
                    assetFileName = "El xoca.png",
                    description = SkillTreeAndAbilities.GetOcaPassiveDescription(OcaType.ElXoca),
                    defaultColor = new Color(0.35f, 0.35f, 0.40f)
                });
            }
            else
            {
                foreach (var def in ocaCatalog)
                {
                    def.description = SkillTreeAndAbilities.GetOcaPassiveDescription(def.ocaType);
                }
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

            foreach (var itemDef in ShopAndItemsSystem.Catalog)
            {
                if (itemDef.sprite == null && !string.IsNullOrEmpty(itemDef.assetFileName))
                {
                    itemDef.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(folder + itemDef.assetFileName);
                    if (itemDef.sprite == null)
                    {
                        Texture2D tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + itemDef.assetFileName);
                        if (tex != null) itemDef.sprite = CreateSpriteFromTexture(tex);
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

        public PlayerOcaToken GetCurrentTurnToken()
        {
            if (!isMatchActive || currentTurnPlayerIndex < 0 || currentTurnPlayerIndex >= activeTokens.Count)
                return null;
            return activeTokens[currentTurnPlayerIndex];
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

            // Instanciar los 3 Bosses en las casillas 13 (La Foca), 35 (La Roca) y 53 (La Trollface)
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
                pData.ResetMatchState(options.startingCoins, options.startingExpLevel);
                SkillTreeAndAbilities.ApplyOcaStartingPassive(pData);

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
                $"¡Partida iniciada! Cada Oca activó su habilidad pasiva inicial."
            );
        }

        // =====================================================================
        // ACCIONES DEL JUGADOR EN TURNO (ÁRBOL DE EXP, TIENDA, OBJETOS Y HECHIZOS)
        // =====================================================================

        public bool TryUpgradeCurrentPlayerSkill(SkillBranchType branch, out string message)
        {
            message = "";
            PlayerOcaToken token = GetCurrentTurnToken();
            if (token == null) return false;

            bool ok = SkillTreeAndAbilities.TryUpgradeSkillBranch(token.PlayerData, branch, out message);
            if (ok)
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, 0, message);
            }
            return ok;
        }

        public bool TryBuyShopItemForCurrentPlayer(ItemType itemType, out string message)
        {
            message = "";
            PlayerOcaToken token = GetCurrentTurnToken();
            if (token == null) return false;

            bool ok = ShopAndItemsSystem.TryBuyItem(token.PlayerData, itemType, currentShopLandedExactly, out message);
            if (ok)
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, 0, message);
            }
            return ok;
        }

        public bool TryUseTurnItemForCurrentPlayer(ItemType itemType, int targetPlayerIdx, out string message)
        {
            message = "";
            PlayerOcaToken userToken = GetCurrentTurnToken();
            if (userToken == null) return false;

            PlayerOcaToken targetToken = (targetPlayerIdx >= 0 && targetPlayerIdx < activeTokens.Count)
                ? activeTokens[targetPlayerIdx]
                : null;

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();

            bool ok = ShopAndItemsSystem.TryUseItemInTurn(
                userToken,
                itemType,
                targetToken,
                boardMgr,
                activeTokens.Count,
                KillPlayerAndSendToHell,
                out message
            );

            if (ok)
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, 0, message);
            }
            return ok;
        }

        public bool TryCastTurnSpellForCurrentPlayer(int targetPlayerIdx, out string message)
        {
            message = "";
            PlayerOcaToken casterToken = GetCurrentTurnToken();
            if (casterToken == null) return false;

            PlayerOcaToken targetToken = (targetPlayerIdx >= 0 && targetPlayerIdx < activeTokens.Count)
                ? activeTokens[targetPlayerIdx]
                : null;

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();

            bool ok = SpellsAndWildCardsSystem.TryCastTurnSpell(
                casterToken,
                targetToken,
                boardMgr,
                activeTokens.Count,
                KillPlayerAndSendToHell,
                out message,
                out bool instantVictory
            );

            if (ok)
            {
                OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, 0, message);
                if (instantVictory)
                {
                    isMatchActive = false;
                }
            }
            return ok;
        }

        // =====================================================================
        // TIRADA DE DADO DE MOVIMIENTO Y RECORRIDO POR LOS 4 TABLEROS
        // =====================================================================

        public void RollDiceForCurrentPlayer()
        {
            if (!isMatchActive || activeTokens.Count == 0) return;
            if (CombatManager.Instance != null && CombatManager.Instance.IsInCombat) return;

            PlayerOcaToken currentToken = activeTokens[currentTurnPlayerIndex];
            if (currentToken == null || currentToken.IsMoving) return;

            PlayerSetupData pData = currentToken.PlayerData;

            // Verificar si tiene el turno congelado / saltado (Hechizo Congelar o Carta Salvaje)
            if (pData.skipNextTurn)
            {
                pData.skipNextTurn = false;
                AdvanceTurn($"¡{pData.playerName} tenía el turno congelado y pierde este turno!", 0);
                return;
            }

            BoardManager boardMgr = BoardManager.Instance != null
                ? BoardManager.Instance
                : FindAnyObjectByType<BoardManager>();
            if (boardMgr == null) return;

            BoardPath path = boardMgr.GetPath(currentToken.CurrentBoard);
            if (path == null) return;

            // Tirar dados de movimiento según Árbol de Habilidades + RedBull + Botas + Lupa/Paso Ligero + Velocidad/Ralentizar
            int roll = SkillTreeAndAbilities.RollPlayerMovement(pData, out string rollBreakdown);

            List<BoardTile> stepSequence = path.TraceStepSequence(
                currentToken.CurrentTileIndex,
                roll,
                options.exactRollForGoal
            );

            int targetIndex = stepSequence.Count > 0
                ? stepSequence[stepSequence.Count - 1].TileNumber
                : currentToken.CurrentTileIndex;

            // Regla TP2 (pág. 8-9): Intercepción por Boss vivo en el Tablero Principal
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

            // Verificar si el Guante de Duelo detiene al jugador en la primera casilla donde se cruce con otro jugador
            PlayerOcaToken duelInterceptedOpponent = null;
            if (pData.duelGloveActive && currentToken.CurrentBoard != BoardType.Hell2)
            {
                pData.duelGloveActive = false;
                for (int s = 0; s < stepSequence.Count; s++)
                {
                    int stepTileIdx = stepSequence[s].TileNumber;
                    if (stepTileIdx == 0) continue;
                    foreach (var other in activeTokens)
                    {
                        if (other == null || other == currentToken) continue;
                        if (other.CurrentBoard == currentToken.CurrentBoard && other.CurrentTileIndex == stepTileIdx)
                        {
                            duelInterceptedOpponent = other;
                            interceptedBoss = null;
                            targetIndex = stepTileIdx;
                            stepSequence = stepSequence.GetRange(0, s + 1);
                            break;
                        }
                    }
                    if (duelInterceptedOpponent != null) break;
                }
            }

            // Verificar cruces en el camino: Vampirismo, Payasización y paso por Tienda
            string crossingNotes = ProcessPathCrossings(currentToken, stepSequence, boardMgr, out bool passedOverShop, out bool shopIsHell, out bool shopLandedExact);

            boardMgr.SwitchToBoard(currentToken.CurrentBoard);
            if (boardMgr.CameraController != null)
            {
                boardMgr.CameraController.FocusOnTarget(currentToken.transform, 14f);
            }

            string playerName = pData.playerName;
            string moveMsg = interceptedBoss != null
                ? $"{playerName} tiró {roll} {rollBreakdown}, ¡pero el Boss {interceptedBoss.displayName} bloquea el paso en la casilla {targetIndex}!"
                : $"{playerName} tiró {roll} {rollBreakdown} y avanza a la casilla {targetIndex} ({currentToken.CurrentBoard})...";

            if (!string.IsNullOrEmpty(crossingNotes))
            {
                moveMsg += " " + crossingNotes;
            }

            OnTurnStateUpdated?.Invoke(currentTurnPlayerIndex, roll, moveMsg);

            currentToken.MoveAlongSequence(stepSequence, currentTurnPlayerIndex, activeTokens.Count, () =>
            {
                // Si pasó por una Tienda o cayó exactamente en ella, notificar a la UI para permitir comprar
                if (passedOverShop)
                {
                    isCurrentShopHell = shopIsHell;
                    currentShopLandedExactly = shopLandedExact;
                    OnShopTriggered?.Invoke(currentToken, shopIsHell, shopLandedExact);
                }

                if (interceptedBoss != null && CombatManager.Instance != null)
                {
                    CombatManager.Instance.StartBossCombat(currentToken, interceptedBoss, (encounter) =>
                    {
                        HandleCombatConcluded(encounter, boardMgr, roll);
                    });
                }
                else if (duelInterceptedOpponent != null && CombatManager.Instance != null)
                {
                    CombatManager.Instance.StartPvPCombat(currentToken, duelInterceptedOpponent, (encounter) =>
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

        private string ProcessPathCrossings(
            PlayerOcaToken mover,
            List<BoardTile> stepSequence,
            BoardManager boardMgr,
            out bool triggeredShop,
            out bool isHellShop,
            out bool landedExactlyOnShop)
        {
            triggeredShop = false;
            isHellShop = (mover.CurrentBoard == BoardType.Hell1);
            landedExactlyOnShop = false;

            if (stepSequence == null || stepSequence.Count == 0) return "";

            PlayerSetupData pData = mover.PlayerData;
            List<string> notes = new List<string>();
            HashSet<PlayerOcaToken> crossedPlayers = new HashSet<PlayerOcaToken>();

            for (int i = 0; i < stepSequence.Count; i++)
            {
                BoardTile stepTile = stepSequence[i];
                if (stepTile == null) continue;

                bool isFinalStep = (i == stepSequence.Count - 1);

                if (stepTile.Type == TileType.Tienda)
                {
                    triggeredShop = true;
                    isHellShop = (mover.CurrentBoard == BoardType.Hell1);
                    if (isFinalStep) landedExactlyOnShop = true;
                }

                foreach (var other in activeTokens)
                {
                    if (other == null || other == mover) continue;
                    if (other.CurrentBoard == mover.CurrentBoard && other.CurrentTileIndex == stepTile.TileNumber)
                    {
                        crossedPlayers.Add(other);
                    }
                }
            }

            if (pData.vampirismActive)
            {
                pData.vampirismActive = false;
                foreach (var victim in crossedPlayers)
                {
                    if (victim.PlayerData.coins > 0)
                    {
                        victim.PlayerData.coins -= 1;
                        pData.coins += 1;
                        notes.Add($"¡Vampirismo robó 1 moneda a {victim.PlayerData.playerName}!");
                    }
                }
            }

            if (pData.payasizacionActive)
            {
                pData.payasizacionActive = false;
                foreach (var victim in crossedPlayers)
                {
                    SendPlayerToClownWheel(victim, boardMgr);
                    notes.Add($"¡Payasización envió a {victim.PlayerData.playerName} al Tablero del Payaso!");
                    break;
                }
            }

            return string.Join(" ", notes);
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
                    int coinsWon = SkillTreeAndAbilities.GetCoinsPerMoneyTile(pData);
                    pData.coins += coinsWon;
                    effectMsg = $"¡{pData.playerName} cayó en Casilla de Dinero ({landedTile.TileNumber}) y ganó +{coinsWon} Moneda(s)! (Total: {pData.coins})";
                    break;

                case TileType.Experiencia:
                    int lvlsGained = SkillTreeAndAbilities.GainExperienceAndGrantPoints(pData, 1);
                    effectMsg = $"¡{pData.playerName} cayó en Casilla de Experiencia ({landedTile.TileNumber}) y ganó +1 EXP! (Nivel {pData.expLevel})" +
                                (lvlsGained > 0 ? $" ¡+{lvlsGained} Punto de Habilidad disponible!" : "");
                    break;

                case TileType.Tienda:
                    isCurrentShopHell = (token.CurrentBoard == BoardType.Hell1);
                    currentShopLandedExactly = true;
                    string boxName = isCurrentShopHell ? "Caja Roja (Espada/Pistola)" : "Caja Verde (Lupa/Carta UNO Reverse)";
                    effectMsg = $"¡{pData.playerName} cayó exactamente en la Tienda ({landedTile.TileNumber})! Se desbloquea la {boxName}.";
                    break;

                case TileType.Hechizos:
                    SpellType drawnSpell = SpellsAndWildCardsSystem.DrawRandomSpell();
                    pData.currentSpell = drawnSpell;
                    SpellDefinition sDef = SpellsAndWildCardsSystem.GetSpellDefinition(drawnSpell);
                    effectMsg = $"¡{pData.playerName} cayó en Casilla de Hechizos ({landedTile.TileNumber}) y obtuvo el hechizo '{sDef?.displayName ?? drawnSpell.ToString()}' (Coste: {sDef?.manaCost ?? 0} Maná)!";
                    break;

                case TileType.CartasSalvajes:
                    string locaBonus = "";
                    if (pData.selectedOca == OcaType.LaLoca)
                    {
                        pData.coins += 1;
                        locaBonus = " [Pasiva La Loca: +1 Moneda]";
                    }
                    WildCardDefinition card = SpellsAndWildCardsSystem.DrawRandomWildCard();
                    string cardResult = ExecuteWildCardEffect(token, card, boardMgr, ref extraTurn, ref playerDiedOnTile);
                    effectMsg = $"¡Carta Salvaje '{card.title}'!{locaBonus} {cardResult}";
                    OnWildCardDrawn?.Invoke(token, card, effectMsg);
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
                    else if (pData.inventory.Contains(ItemType.CartaUNOReverse))
                    {
                        pData.inventory.Remove(ItemType.CartaUNOReverse);
                        effectMsg = $"¡{pData.playerName} cayó en la Calavera, pero activó su Carta Reversa del UNO y evitó la muerte!";
                    }
                    else
                    {
                        playerDiedOnTile = true;
                        RewardJinxBeneficiaryIfAny(pData);
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
            // Si el jugador cae en una casilla con flecha roja ascendente (SpecialDestinationTile), sube al siguiente anillo o al EXIT
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

        /// <summary>
        /// Ejecuta el efecto de una Carta Salvaje robada del mazo ("Wild Cards.jpg").
        /// </summary>
        private string ExecuteWildCardEffect(
            PlayerOcaToken token,
            WildCardDefinition card,
            BoardManager boardMgr,
            ref bool extraTurn,
            ref bool playerDied)
        {
            if (card == null || token == null) return "";
            PlayerSetupData pData = token.PlayerData;

            if (card.isNegative)
            {
                RewardJinxBeneficiaryIfAny(pData);
                if (pData.inventory.Contains(ItemType.Crucifijo))
                {
                    pData.inventory.Remove(ItemType.Crucifijo);
                    return $"({card.description}) ¡Pero {pData.playerName} usó automáticamente su Crucifijo y anuló el efecto negativo!";
                }
            }

            switch (card.effectType)
            {
                case WildCardEffectType.PlayerDies:
                    if (pData.isZombie) return "Mueres, pero como eres Zombi sobrevives sin ir al Infierno.";
                    playerDied = true;
                    string h = KillPlayerAndSendToHell(token, boardMgr);
                    return $"¡{pData.playerName} muere por la Carta Salvaje y es enviado a {h}!";

                case WildCardEffectType.AllPlayersDie:
                    foreach (var t in activeTokens)
                    {
                        if (t != null && !t.PlayerData.isZombie)
                        {
                            KillPlayerAndSendToHell(t, boardMgr);
                        }
                    }
                    playerDied = !pData.isZombie;
                    return "¡Apocalipsis! Todos los jugadores (no zombis) mueren y son enviados al Infierno.";

                case WildCardEffectType.GoToClownBoard:
                    SendPlayerToClownWheel(token, boardMgr);
                    return $"¡{pData.playerName} fue enviado directamente a la Rueda del Payaso!";

                case WildCardEffectType.UnoDuelForCoin:
                    pData.coins += 1;
                    return "¡Ganaste el reto rápido de UNO y obtuviste +1 Moneda!";

                case WildCardEffectType.TouchGrassGainExp:
                    SkillTreeAndAbilities.GainExperienceAndGrantPoints(pData, 1);
                    return $"¡Saliste a tocar hierba y ganaste +1 de Experiencia (Nivel {pData.expLevel})!";

                case WildCardEffectType.TellAJokeOrStart:
                    if (UnityEngine.Random.value >= 0.35f)
                    {
                        pData.coins += 1;
                        return "¡Tu chiste hizo reír a la mesa y ganaste +1 Moneda!";
                    }
                    else
                    {
                        ReturnPlayerToMainBoardStart(token, boardMgr);
                        return "Nadie se rió del chiste... Vuelves a la casilla START.";
                    }

                case WildCardEffectType.ClearNegativeEffect:
                    pData.activeNegativeEffect = "";
                    pData.halveNextMoveRoll = false;
                    pData.skipNextTurn = false;
                    return "Te deshiciste de todos tus efectos negativos activos.";

                case WildCardEffectType.FreeCrucifijo:
                    pData.inventory.Add(ItemType.Crucifijo);
                    return "¡Recibiste un Crucifijo gratis en tu inventario!";

                case WildCardEffectType.FreeRedBull:
                    pData.inventory.Add(ItemType.RedBull);
                    return "¡Recibiste una RedBull gratis en tu inventario!";

                case WildCardEffectType.FreeGuanteDuelo:
                    pData.inventory.Add(ItemType.GuanteDeDuelo);
                    return "¡Recibiste un Guante de Duelo gratis en tu inventario!";

                case WildCardEffectType.FreeLupa:
                    pData.inventory.Add(ItemType.Lupa);
                    return "¡Recibiste una Lupa gratis en tu inventario!";

                case WildCardEffectType.FreeCartaUNOReverse:
                    pData.inventory.Add(ItemType.CartaUNOReverse);
                    return "¡Recibiste una Carta Reversa del UNO gratis en tu inventario!";

                case WildCardEffectType.JinxAnotherPlayer:
                    if (activeTokens.Count > 1)
                    {
                        int targetIdx = (pData.playerIndex + 1) % activeTokens.Count;
                        activeTokens[targetIdx].PlayerData.jinxBeneficiaryPlayerIndex = pData.playerIndex;
                        return $"¡Mal de Ojo lanzado sobre {activeTokens[targetIdx].PlayerData.playerName}! Recibirás +1 moneda cuando le pase algo malo.";
                    }
                    return "No hay otros jugadores para echarles Mal de Ojo.";

                case WildCardEffectType.NothingHappens:
                    return "No pasa nada :)";

                case WildCardEffectType.CurseSayCuac:
                    pData.activeNegativeEffect = "Terminar frases con 'Cuac'";
                    return "Maldición activa: Debes terminar tus frases con 'Cuac' o pierdes 1 moneda.";

                case WildCardEffectType.CurseNoLetterE:
                    pData.activeNegativeEffect = "Prohibido decir la letra 'E'";
                    return "Maldición activa: No puedes decir la letra 'E' o pierdes 1 moneda.";

                case WildCardEffectType.CurseOneWordOnly:
                    pData.activeNegativeEffect = "Hablar con 1 sola palabra";
                    return "Maldición activa: Solo puedes hablar usando una palabra hasta tu próximo turno.";

                case WildCardEffectType.Advance1d6:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, UnityEngine.Random.Range(1, 7), "1d6");

                case WildCardEffectType.Advance1d8:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, UnityEngine.Random.Range(1, 9), "1d8");

                case WildCardEffectType.Advance1d20:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, UnityEngine.Random.Range(1, 21), "1d20");

                case WildCardEffectType.Retreat1d6:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, -UnityEngine.Random.Range(1, 7), "-1d6");

                case WildCardEffectType.Retreat1d8:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, -UnityEngine.Random.Range(1, 9), "-1d8");

                case WildCardEffectType.Retreat1d20:
                    return MovePlayerRelativeOnCurrentBoard(token, boardMgr, -UnityEngine.Random.Range(1, 21), "-1d20");

                case WildCardEffectType.SwapPositionWithPlayer:
                    if (activeTokens.Count > 1)
                    {
                        PlayerOcaToken other = activeTokens[(pData.playerIndex + 1) % activeTokens.Count];
                        BoardTile myTile = boardMgr.GetTile(token.CurrentBoard, token.CurrentTileIndex);
                        BoardTile otherTile = boardMgr.GetTile(other.CurrentBoard, other.CurrentTileIndex);
                        if (myTile != null && otherTile != null)
                        {
                            token.PlaceOnTileImmediate(otherTile, pData.playerIndex, activeTokens.Count);
                            other.PlaceOnTileImmediate(myTile, other.PlayerData.playerIndex, activeTokens.Count);
                        }
                        return $"¡Intercambiaste tu posición en el tablero con {other.PlayerData.playerName}!";
                    }
                    return "No hay otro jugador para intercambiar posición.";

                case WildCardEffectType.RedistributeWealthAndMana:
                    int totalCoins = 0;
                    int totalMana = 0;
                    foreach (var t in activeTokens)
                    {
                        totalCoins += t.PlayerData.coins;
                        totalMana += t.PlayerData.mana;
                    }
                    int count = Mathf.Max(1, activeTokens.Count);
                    int eachCoins = totalCoins / count;
                    int eachMana = totalMana / count;
                    foreach (var t in activeTokens)
                    {
                        t.PlayerData.coins = eachCoins;
                        t.PlayerData.mana = eachMana;
                    }
                    return $"¡Reparto Equitativo! Todos los jugadores ahora tienen {eachCoins} Moneda(s) y {eachMana} de Maná.";

                case WildCardEffectType.ExtraTurnObo:
                    extraTurn = true;
                    return "¡Pillas el Obo! Tienes una tirada extra inmediata.";

                case WildCardEffectType.WordBetOneCoin:
                    pData.coins += 1;
                    return "¡Alguien dijo la palabra prohibida y ganaste +1 Moneda!";

                case WildCardEffectType.Roll1d20AdvanceOrRetreat:
                    int r20 = UnityEngine.Random.Range(1, 21);
                    int delta = r20 < 10 ? r20 : -r20;
                    return $"Salió {r20} en el 1d20: " + MovePlayerRelativeOnCurrentBoard(token, boardMgr, delta, "1d20");

                case WildCardEffectType.SkipNextTurn:
                    pData.skipNextTurn = true;
                    return "Se te saltará tu próximo turno.";
            }

            return card.description;
        }

        private string MovePlayerRelativeOnCurrentBoard(PlayerOcaToken token, BoardManager boardMgr, int deltaSteps, string label)
        {
            if (token == null || boardMgr == null) return "";
            BoardPath path = boardMgr.GetPath(token.CurrentBoard);
            if (path == null) return "";

            int maxTile = Mathf.Max(0, path.TileCount - 1);
            int newIndex = Mathf.Clamp(token.CurrentTileIndex + deltaSteps, 0, maxTile);
            BoardTile dest = path.GetTile(newIndex);
            if (dest != null)
            {
                token.PlaceOnTileImmediate(dest, token.PlayerData.playerIndex, activeTokens.Count);
            }

            return deltaSteps >= 0
                ? $"Avanzaste {deltaSteps} casillas ({label}) hasta la casilla {newIndex}."
                : $"Retrocediste {Mathf.Abs(deltaSteps)} casillas ({label}) hasta la casilla {newIndex}.";
        }

        private void SendPlayerToClownWheel(PlayerOcaToken victim, BoardManager boardMgr)
        {
            if (victim == null || boardMgr == null) return;
            BoardTile clownStart = boardMgr.GetTile(BoardType.ClownWheel, 1); // Sector NO
            if (clownStart == null) clownStart = boardMgr.GetTile(BoardType.ClownWheel, 0);
            if (clownStart != null)
            {
                victim.PlaceOnTileImmediate(clownStart, victim.PlayerData.playerIndex, activeTokens.Count);
            }
        }

        private void RewardJinxBeneficiaryIfAny(PlayerSetupData victimData)
        {
            if (victimData == null) return;
            int benIdx = victimData.jinxBeneficiaryPlayerIndex;
            if (benIdx >= 0 && benIdx < activeTokens.Count && benIdx != victimData.playerIndex)
            {
                activeTokens[benIdx].PlayerData.coins += 1;
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
        /// Aplica las recompensas (+3 EXP o +4 EXP para El Xoca al ganador) y castigos (muerte / envío al Infierno al perdedor)
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
                    int expGain = player.PlayerData.selectedOca == OcaType.ElXoca ? 4 : 3;
                    SkillTreeAndAbilities.GainExperienceAndGrantPoints(player.PlayerData, expGain);
                    summaryMsg = $"¡{player.PlayerData.playerName} venció a {boss.displayName} y obtuvo +{expGain} EXP (Nivel {player.PlayerData.expLevel})!";
                }
                else
                {
                    RewardJinxBeneficiaryIfAny(player.PlayerData);
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

                int expGain = winner.PlayerData.selectedOca == OcaType.ElXoca ? 4 : 3;
                SkillTreeAndAbilities.GainExperienceAndGrantPoints(winner.PlayerData, expGain);
                RewardJinxBeneficiaryIfAny(loser.PlayerData);

                if (loser.PlayerData.isZombie)
                {
                    summaryMsg = $"¡{winner.PlayerData.playerName} venció a {loser.PlayerData.playerName} (+{expGain} EXP)! Como {loser.PlayerData.playerName} es Zombi, no muere.";
                }
                else if (loser.PlayerData.inventory.Contains(ItemType.CartaUNOReverse))
                {
                    loser.PlayerData.inventory.Remove(ItemType.CartaUNOReverse);
                    summaryMsg = $"¡{winner.PlayerData.playerName} venció a {loser.PlayerData.playerName} (+{expGain} EXP), pero {loser.PlayerData.playerName} usó su Carta UNO Reverse para evitar ir al Infierno!";
                }
                else
                {
                    string hellDest = KillPlayerAndSendToHell(loser, boardMgr);
                    summaryMsg = $"¡{winner.PlayerData.playerName} venció a {loser.PlayerData.playerName} (+{expGain} EXP)! {loser.PlayerData.playerName} muere y va a {hellDest}.";
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
            SkillTreeAndAbilities.GainExperienceAndGrantPoints(pData, amount);
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
                        int manaGain = SkillTreeAndAbilities.GetManaPerFullMoon(t.PlayerData);
                        t.PlayerData.mana += manaGain;
                    }
                    previousMessage += " ¡Luna Llena! Todos los jugadores reciben Maná lunar.";
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
