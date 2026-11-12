using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using Oca2.CardsAndMagic;
using Oca2.Combat;
using Oca2.Core;
using Oca2.Data;
using Oca2.Items;
using Oca2.Players;
using Oca2.Progression;

namespace Oca2.UI
{
    /// <summary>
    /// Controlador integral de Interfaz de Usuario (UI/UX) para La Oca 2: Electric Boogaloo.
    /// Construye y gestiona:
    /// 1. Menú Principal (fiel al diseño de la pág. 24 del TP2: Nuevo juego, Opciones, Créditos, Salir)
    /// 2. Selección de Jugadores (1 a 8 jugadores) y elección de las 6 Ocas (pág. 3 y 5 del TP2)
    /// 3. Menú de Opciones de Juego (Audio, Video/Cámara y Reglas de Partida del TP2)
    /// 4. Pantalla de Créditos y HUD de Partida activa.
    /// </summary>
    public class MainMenuUIController : MonoBehaviour
    {
        public static MainMenuUIController Instance { get; private set; }

        private Canvas mainCanvas;
        private Font defaultFont;

        // Paneles principales
        private GameObject mainMenuPanel;
        private GameObject playerSelectionPanel;
        private GameObject optionsPanel;
        private GameObject creditsPanel;
        private GameObject inGameHudPanel;

        // Elementos de Selección de Jugadores (1 a 8)
        private Text playerCountValueText;
        private Text sessionEstimateText;
        private Text startingResourcesNoticeText;
        private readonly List<PlayerSlotUI> playerSlotUIs = new List<PlayerSlotUI>();

        // Elementos de Opciones
        private Slider masterVolSlider;
        private Text masterVolValueText;
        private Slider musicVolSlider;
        private Text musicVolValueText;
        private Slider sfxVolSlider;
        private Text sfxVolValueText;
        private Toggle fullscreenToggle;
        private Text qualityValueText;
        private Slider camSpeedSlider;
        private Text camSpeedValueText;
        private Text startCoinsValueText;
        private Text startLevelValueText;
        private Toggle exactGoalToggle;
        private Toggle bossesToggle;

        // Elementos del HUD en partida
        private Transform hudPlayersBar;
        private Text hudStatusText;
        private Text hudTurnHeaderText;
        private RectTransform hudExpBarFillRt;
        private Text hudExpBarText;
        private Text hudGrowthStatsText;
        private Button rollDiceButton;
        private Text skillTreeBtnLabel;
        private Text bagSpellBtnLabel;

        // Ventana Modal del Árbol de Habilidades ("Skill Tree.png")
        private GameObject skillTreeModalPanel;
        private Text skillTreeHeaderText;
        private Text skillTreePassiveText;
        private RectTransform skillTreeExpBarFillRt;
        private Text skillTreeExpBarText;
        private readonly Image[] skillTreeLevelDots = new Image[22];
        private readonly Text[] skillTreeLevelNumbers = new Text[22];
        private readonly Image[] skillNodeCircles = new Image[21];
        private readonly Text[] skillNodeTexts = new Text[21];
        private readonly Button[] skillNodeButtons = new Button[21];
        private Text skillBranchMovText;
        private Text skillBranchCombLeftText;
        private Text skillBranchWealthText;
        private Text skillBranchMagicText;
        private Text skillTreeFeedbackText;

        // Ventana Modal de Tienda Principal y Tienda del Infierno ("Tienda.png", "Tienda infierno.png")
        private GameObject shopModalPanel;
        private Text shopHeaderTitleText;
        private Text shopSubNoticeText;
        private Text shopPlayerCoinsText;
        private Transform shopGridContainer;
        private Text shopFeedbackText;

        // Ventana Modal de Inventario (Mochila) y Hechizos ("Hechizos.jpg")
        private GameObject bagAndMagicModalPanel;
        private Text bagHeaderText;
        private Text bagSpellInfoText;
        private Text bagTargetPlayerText;
        private Text bagStepControlText;
        private Transform bagItemsContainer;
        private Text bagFeedbackText;
        private int selectedTargetPlayerIdx = 0;

        // Elementos de la Ventana de Combate (PvE Bosses y PvP)
        private GameObject combatModalPanel;
        private Text combatHeaderTitleText;
        private Image attackerPortraitImg;
        private Text attackerNameText;
        private Text attackerFormulaText;
        private Text attackerRollResultText;
        private Image defenderPortraitImg;
        private Text defenderNameText;
        private Text defenderFormulaText;
        private Text defenderRollResultText;
        private Text combatNarrationText;
        private Button combatRollBtn;
        private Button combatContinueBtn;

        // Colores del tema visual inspirados en el TP2 (pág. 5 y 24)
        private static readonly Color CreamButtonColor = new Color(0.96f, 0.93f, 0.79f, 1f);
        private static readonly Color CreamButtonHighlight = new Color(1f, 0.98f, 0.88f, 1f);
        private static readonly Color DarkBorderColor = new Color(0.12f, 0.11f, 0.09f, 1f);
        private static readonly Color TitleRedColor = new Color(0.88f, 0.12f, 0.12f, 1f);
        private static readonly Color PanelBgColor = new Color(0.11f, 0.14f, 0.11f, 0.92f);
        private static readonly Color CardBgColor = new Color(0.18f, 0.22f, 0.18f, 0.96f);
        private static readonly Color AccentGreenColor = new Color(0.22f, 0.68f, 0.32f, 1f);

        private class PlayerSlotUI
        {
            public int index;
            public GameObject rootCard;
            public Image colorHeaderImage;
            public InputField nameInput;
            public Image ocaPortraitImage;
            public Text ocaNameText;
            public Text ocaDescText;
            public Text resourcesBadgeText;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsureMenuExists()
        {
            if (FindAnyObjectByType<MainMenuUIController>() == null)
            {
                GameObject go = new GameObject("GameSession_UI_Manager");
                go.AddComponent<GameSessionManager>();
                go.AddComponent<MainMenuUIController>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (GetComponent<GameSessionManager>() == null && GameSessionManager.Instance == null)
            {
                gameObject.AddComponent<GameSessionManager>();
            }

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            EnsureEventSystem();
            BuildCompleteUI();
        }

        private void Start()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.OnMatchStarted += HandleMatchStarted;
                GameSessionManager.Instance.OnMatchEndedToMenu += HandleMatchEndedToMenu;
                GameSessionManager.Instance.OnTurnStateUpdated += HandleTurnStateUpdated;
                GameSessionManager.Instance.OnShopTriggered += HandleShopTriggered;
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatStarted += HandleCombatStarted;
                CombatManager.Instance.OnCombatRolled += HandleCombatRolled;
                CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
            }

            ShowMainMenu();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.OnMatchStarted -= HandleMatchStarted;
                GameSessionManager.Instance.OnMatchEndedToMenu -= HandleMatchEndedToMenu;
                GameSessionManager.Instance.OnTurnStateUpdated -= HandleTurnStateUpdated;
                GameSessionManager.Instance.OnShopTriggered -= HandleShopTriggered;
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatStarted -= HandleCombatStarted;
                CombatManager.Instance.OnCombatRolled -= HandleCombatRolled;
                CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureEventSystem();
        }

        private void Update()
        {
            // Permitir abrir/volver al menú con Escape durante la partida
            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escPressed = true;
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }
#endif
            if (escPressed)
            {
                if (optionsPanel != null && optionsPanel.activeSelf)
                {
                    if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsMatchActive)
                        ShowInGameHUD();
                    else
                        ShowMainMenu();
                }
                else if (playerSelectionPanel != null && playerSelectionPanel.activeSelf)
                {
                    ShowMainMenu();
                }
                else if (creditsPanel != null && creditsPanel.activeSelf)
                {
                    ShowMainMenu();
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esObj.AddComponent<InputSystemUIInputModule>();
#else
            esObj.AddComponent<StandaloneInputModule>();
#endif
        }

        // =====================================================================
        // CONSTRUCCIÓN DE LA INTERFAZ
        // =====================================================================

        private void BuildCompleteUI()
        {
            // Limpiar Canvas previo si existiera como hijo
            Transform oldCanvas = transform.Find("Oca2_MainCanvas");
            if (oldCanvas != null) Destroy(oldCanvas.gameObject);

            GameObject canvasObj = new GameObject("Oca2_MainCanvas");
            canvasObj.transform.SetParent(transform, false);
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            BuildMainMenuPanel(canvasObj.transform);
            BuildPlayerSelectionPanel(canvasObj.transform);
            BuildOptionsPanel(canvasObj.transform);
            BuildCreditsPanel(canvasObj.transform);
            BuildInGameHudPanel(canvasObj.transform);
        }

        // ---------------------------------------------------------------------
        // 1. PANEL DE MENÚ PRINCIPAL (Pág. 24 del TP2)
        // ---------------------------------------------------------------------
        private void BuildMainMenuPanel(Transform parent)
        {
            mainMenuPanel = CreatePanel(parent, "MainMenu_Panel", new Color(0.06f, 0.09f, 0.06f, 0.68f));

            // Contenedor izquierdo: Escenario de presentación con las 6 Ocas del juego
            GameObject leftShowcase = CreateBox(mainMenuPanel.transform, "Left_Showcase", new Color(0.12f, 0.22f, 0.12f, 0.88f),
                new Vector2(0.05f, 0.12f), new Vector2(0.55f, 0.88f));
            AddOutline(leftShowcase, DarkBorderColor, 4f);

            CreateText(leftShowcase.transform, "ShowcaseHeader", "¡CONOCE A LAS OCAS COMPETIDORAS!", 28, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f));

            // Grilla de las 6 Ocas en la portada
            GameObject showcaseGridObj = CreateBox(leftShowcase.transform, "Ocas_Grid", Color.clear,
                new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.84f));
            GridLayoutGroup grid = showcaseGridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(260, 220);
            grid.spacing = new Vector2(22, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.EnsureDefaultOcaCatalog();
                foreach (var ocaDef in GameSessionManager.Instance.OcaCatalog)
                {
                    GameObject card = CreateBox(showcaseGridObj.transform, $"Preview_{ocaDef.ocaType}",
                        new Color(0.95f, 0.93f, 0.85f, 0.95f), Vector2.zero, Vector2.one);
                    AddOutline(card, DarkBorderColor, 3f);

                    GameObject imgHolder = CreateBox(card.transform, "Sprite", Color.white,
                        new Vector2(0.15f, 0.24f), new Vector2(0.85f, 0.93f));
                    Image img = imgHolder.GetComponent<Image>();
                    img.sprite = ocaDef.sprite;
                    img.preserveAspect = true;

                    CreateText(card.transform, "Label", ocaDef.displayName, 20, FontStyle.Bold,
                        DarkBorderColor, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.22f));
                }
            }

            CreateText(leftShowcase.transform, "ShowcaseFooter",
                "1 a 8 Jugadores  •  63 Casillas  •  3 Bosses  •  4 Tableros (Principal, Infierno 1 y 2, Payaso)",
                18, FontStyle.Italic, new Color(0.85f, 0.92f, 0.85f), TextAnchor.MiddleCenter,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.12f));

            // Contenedor derecho: Título "LA OCA 2" + los 4 botones del mockup (Pág. 24 del TP2)
            GameObject rightColumn = CreateBox(mainMenuPanel.transform, "Right_MenuColumn", Color.clear,
                new Vector2(0.58f, 0.10f), new Vector2(0.95f, 0.92f));

            // Título principal "LA OCA 2" en rojo intenso como en el documento
            Text titleText = CreateText(rightColumn.transform, "Title_LaOca2", "LA OCA 2", 86, FontStyle.Bold,
                TitleRedColor, TextAnchor.MiddleCenter, new Vector2(0.0f, 0.78f), new Vector2(1.0f, 0.98f));
            AddShadow(titleText.gameObject, Color.black, new Vector2(4f, -4f));

            Text subtitleText = CreateText(rightColumn.transform, "Subtitle_Electric", "Electric Boogaloo", 32, FontStyle.BoldAndItalic,
                new Color(1f, 0.35f, 0.25f), TextAnchor.MiddleCenter, new Vector2(0.0f, 0.69f), new Vector2(1.0f, 0.80f));
            AddShadow(subtitleText.gameObject, Color.black, new Vector2(2f, -2f));

            // Botones fieles a la pág. 24: "Nuevo juego", "Opciones", "Creditos", "Salir"
            CreateMockupMenuButton(rightColumn.transform, "Btn_NuevoJuego", "Nuevo juego",
                new Vector2(0.10f, 0.52f), new Vector2(0.90f, 0.64f), ShowPlayerSelection);

            CreateMockupMenuButton(rightColumn.transform, "Btn_Opciones", "Opciones",
                new Vector2(0.10f, 0.36f), new Vector2(0.90f, 0.48f), ShowOptions);

            CreateMockupMenuButton(rightColumn.transform, "Btn_Creditos", "Creditos",
                new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.32f), ShowCredits);

            CreateMockupMenuButton(rightColumn.transform, "Btn_Salir", "Salir",
                new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.16f), QuitGame);
        }

        // ---------------------------------------------------------------------
        // 2. PANEL DE SELECCIÓN DE JUGADORES Y OCAS (Pág. 3, 5 y 27 del TP2)
        // ---------------------------------------------------------------------
        private void BuildPlayerSelectionPanel(Transform parent)
        {
            playerSelectionPanel = CreatePanel(parent, "PlayerSelection_Panel", PanelBgColor);

            // Cabecera superior
            GameObject headerBox = CreateBox(playerSelectionPanel.transform, "Header_Bar", new Color(0.08f, 0.11f, 0.08f, 0.95f),
                new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.97f));
            AddOutline(headerBox, DarkBorderColor, 3f);

            CreateText(headerBox.transform, "Header_Title", "SELECCIÓN DE JUGADORES Y OCAS", 36, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleLeft, new Vector2(0.03f, 0.15f), new Vector2(0.48f, 0.85f));

            // Selector de cantidad de jugadores (1 a 8)
            CreateText(headerBox.transform, "Lbl_Cant", "Cantidad de Jugadores:", 24, FontStyle.Bold,
                Color.white, TextAnchor.MiddleRight, new Vector2(0.46f, 0.20f), new Vector2(0.68f, 0.80f));

            CreateStyledButton(headerBox.transform, "Btn_MinusPlayer", "-", new Vector2(0.69f, 0.18f), new Vector2(0.74f, 0.82f),
                CreamButtonColor, DarkBorderColor, 30, () => ChangePlayerCount(-1));

            GameObject countBadge = CreateBox(headerBox.transform, "Count_Badge", new Color(0.15f, 0.15f, 0.15f, 1f),
                new Vector2(0.75f, 0.18f), new Vector2(0.89f, 0.82f));
            AddOutline(countBadge, CreamButtonColor, 2f);
            playerCountValueText = CreateText(countBadge.transform, "Txt_PlayerCount", "2 Jugadores", 24, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

            CreateStyledButton(headerBox.transform, "Btn_PlusPlayer", "+", new Vector2(0.90f, 0.18f), new Vector2(0.95f, 0.82f),
                CreamButtonColor, DarkBorderColor, 30, () => ChangePlayerCount(1));

            // Sub-barra informativa (Duración estimada según pág. 3 + Recursos iniciales según pág. 5)
            GameObject infoStrip = CreateBox(playerSelectionPanel.transform, "Info_Strip", new Color(0.16f, 0.24f, 0.16f, 0.92f),
                new Vector2(0.03f, 0.79f), new Vector2(0.97f, 0.85f));
            sessionEstimateText = CreateText(infoStrip.transform, "Txt_SessionEstimate",
                "Duración estimada: ~20 - 35 min", 20, FontStyle.Normal,
                new Color(0.85f, 0.95f, 0.85f), TextAnchor.MiddleLeft, new Vector2(0.02f, 0f), new Vector2(0.48f, 1f));

            startingResourcesNoticeText = CreateText(infoStrip.transform, "Txt_InitialResources",
                "Preparación (TP2): Cada jugador inicia con 3 Monedas y Tabla de EXP a Nivel 1", 20, FontStyle.Bold,
                new Color(1f, 0.90f, 0.45f), TextAnchor.MiddleRight, new Vector2(0.48f, 0f), new Vector2(0.98f, 1f));

            // Grilla de 8 tarjetas de jugador (4 columnas x 2 filas)
            GameObject gridContainer = CreateBox(playerSelectionPanel.transform, "Players_Grid", Color.clear,
                new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.77f));
            GridLayoutGroup gridLayout = gridContainer.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(425, 315);
            gridLayout.spacing = new Vector2(20, 18);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 4;
            gridLayout.childAlignment = TextAnchor.UpperCenter;

            playerSlotUIs.Clear();
            for (int i = 0; i < 8; i++)
            {
                PlayerSlotUI slot = CreatePlayerSelectionCard(gridContainer.transform, i);
                playerSlotUIs.Add(slot);
            }

            // Barra inferior de acciones
            CreateMockupMenuButton(playerSelectionPanel.transform, "Btn_BackMenu", "Volver al Menú",
                new Vector2(0.05f, 0.025f), new Vector2(0.32f, 0.115f), ShowMainMenu);

            CreateStyledButton(playerSelectionPanel.transform, "Btn_StartMatch", "¡COMENZAR PARTIDA!",
                new Vector2(0.58f, 0.025f), new Vector2(0.95f, 0.115f),
                AccentGreenColor, Color.white, 32, OnStartMatchClicked);
        }

        private PlayerSlotUI CreatePlayerSelectionCard(Transform parent, int playerIdx)
        {
            PlayerSlotUI slot = new PlayerSlotUI { index = playerIdx };

            GameObject card = CreateBox(parent, $"PlayerSlot_{playerIdx + 1}", CardBgColor, Vector2.zero, Vector2.one);
            AddOutline(card, DarkBorderColor, 3f);
            slot.rootCard = card;

            // Franja superior con el color de la ficha del jugador
            GameObject topBar = CreateBox(card.transform, "ColorBar", GameSessionManager.DefaultPlayerColors[playerIdx],
                new Vector2(0f, 0.84f), new Vector2(1f, 1f));
            slot.colorHeaderImage = topBar.GetComponent<Image>();

            CreateText(topBar.transform, "Lbl_PlayerNum", $"JUGADOR {playerIdx + 1}", 20, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.05f, 0f), new Vector2(0.62f, 1f));

            // Botón para cambiar el color de la base de la ficha
            int capturedIdx = playerIdx;
            CreateStyledButton(topBar.transform, "Btn_CycleColor", "Color Ficha",
                new Vector2(0.64f, 0.14f), new Vector2(0.96f, 0.86f),
                new Color(0.15f, 0.15f, 0.15f, 0.85f), Color.white, 14,
                () => CyclePlayerColor(capturedIdx));

            // Campo de texto editable para el Nombre del Jugador
            slot.nameInput = CreateInputField(card.transform, $"Input_Name_{playerIdx + 1}", $"Jugador {playerIdx + 1}",
                new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.81f), (newName) =>
                {
                    if (GameSessionManager.Instance != null && capturedIdx < GameSessionManager.Instance.ConfiguredPlayers.Count)
                    {
                        GameSessionManager.Instance.ConfiguredPlayers[capturedIdx].playerName =
                            string.IsNullOrWhiteSpace(newName) ? $"Jugador {capturedIdx + 1}" : newName.Trim();
                    }
                });

            // Selector de Oca (< Retrato >)
            CreateStyledButton(card.transform, "Btn_PrevOca", "<",
                new Vector2(0.05f, 0.34f), new Vector2(0.19f, 0.64f),
                CreamButtonColor, DarkBorderColor, 26, () => CyclePlayerOca(capturedIdx, -1));

            GameObject portraitBg = CreateBox(card.transform, "Portrait_Bg", new Color(0.95f, 0.94f, 0.88f, 1f),
                new Vector2(0.22f, 0.30f), new Vector2(0.78f, 0.66f));
            AddOutline(portraitBg, DarkBorderColor, 2f);

            GameObject portraitImgObj = CreateBox(portraitBg.transform, "Oca_Image", Color.white,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            slot.ocaPortraitImage = portraitImgObj.GetComponent<Image>();
            slot.ocaPortraitImage.preserveAspect = true;

            CreateStyledButton(card.transform, "Btn_NextOca", ">",
                new Vector2(0.81f, 0.34f), new Vector2(0.95f, 0.64f),
                CreamButtonColor, DarkBorderColor, 26, () => CyclePlayerOca(capturedIdx, 1));

            // Nombre y descripción de la Oca seleccionada
            slot.ocaNameText = CreateText(card.transform, "Txt_OcaName", "La Oca Default", 20, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.28f));

            slot.ocaDescText = CreateText(card.transform, "Txt_OcaDesc", "", 13, FontStyle.Italic,
                new Color(0.80f, 0.86f, 0.80f), TextAnchor.MiddleCenter, new Vector2(0.04f, 0.09f), new Vector2(0.96f, 0.19f));

            // Badge inferior de recursos iniciales
            slot.resourcesBadgeText = CreateText(card.transform, "Txt_ResourcesBadge", "Inicio: 3 Monedas | Nivel 1 EXP", 14, FontStyle.Bold,
                new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.09f));

            return slot;
        }

        // ---------------------------------------------------------------------
        // 3. PANEL DE OPCIONES DE JUEGO
        // ---------------------------------------------------------------------
        private void BuildOptionsPanel(Transform parent)
        {
            optionsPanel = CreatePanel(parent, "Options_Panel", PanelBgColor);

            GameObject container = CreateBox(optionsPanel.transform, "Options_Window", CardBgColor,
                new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.92f));
            AddOutline(container, DarkBorderColor, 4f);

            CreateText(container.transform, "Title_Options", "OPCIONES DE JUEGO", 42, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // Columna Izquierda: Audio y Video/Cámara
            GameObject leftCol = CreateBox(container.transform, "LeftCol_AudioVideo", new Color(0.13f, 0.16f, 0.13f, 0.9f),
                new Vector2(0.04f, 0.18f), new Vector2(0.48f, 0.85f));
            AddOutline(leftCol, DarkBorderColor, 2f);

            CreateText(leftCol.transform, "Sec_Audio", "AUDIO Y PANTALLA", 24, FontStyle.Bold,
                new Color(1f, 0.88f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // Volumen General
            CreateSliderRow(leftCol.transform, "Volumen General", 0.72f, 0f, 1f, out masterVolSlider, out masterVolValueText, (val) =>
            {
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.Options.masterVolume = val;
                    AudioListener.volume = val;
                }
                masterVolValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // Volumen Música (sin copyright - pág. 24)
            CreateSliderRow(leftCol.transform, "Volumen Música", 0.56f, 0f, 1f, out musicVolSlider, out musicVolValueText, (val) =>
            {
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.Options.musicVolume = val;
                }
                musicVolValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // Volumen Efectos SFX
            CreateSliderRow(leftCol.transform, "Volumen Efectos (SFX)", 0.40f, 0f, 1f, out sfxVolSlider, out sfxVolValueText, (val) =>
            {
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.Options.sfxVolume = val;
                }
                sfxVolValueText.text = $"{Mathf.RoundToInt(val * 100)}%";
            });

            // Velocidad de Cámara
            CreateSliderRow(leftCol.transform, "Velocidad de Cámara", 0.24f, 0.5f, 2.5f, out camSpeedSlider, out camSpeedValueText, (val) =>
            {
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.Options.cameraSpeedMultiplier = val;
                }
                camSpeedValueText.text = $"{val:F1}x";
            });

            // Pantalla Completa y Calidad
            fullscreenToggle = CreateToggleRow(leftCol.transform, "Pantalla Completa", new Vector2(0.06f, 0.11f), new Vector2(0.94f, 0.21f), (isOn) =>
            {
                if (GameSessionManager.Instance != null)
                {
                    GameSessionManager.Instance.Options.fullscreen = isOn;
                }
            });

            CreateText(leftCol.transform, "Lbl_Quality", "Calidad Gráfica:", 20, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.01f), new Vector2(0.48f, 0.10f));

            CreateStyledButton(leftCol.transform, "Btn_PrevQuality", "<", new Vector2(0.50f, 0.02f), new Vector2(0.60f, 0.09f),
                CreamButtonColor, DarkBorderColor, 20, () => ChangeQualityLevel(-1));

            qualityValueText = CreateText(leftCol.transform, "Txt_Quality", "Alta", 19, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.61f, 0.01f), new Vector2(0.83f, 0.10f));

            CreateStyledButton(leftCol.transform, "Btn_NextQuality", ">", new Vector2(0.84f, 0.02f), new Vector2(0.94f, 0.09f),
                CreamButtonColor, DarkBorderColor, 20, () => ChangeQualityLevel(1));

            // Columna Derecha: Reglas de Partida según el Documento TP2
            GameObject rightCol = CreateBox(container.transform, "RightCol_Rules", new Color(0.13f, 0.16f, 0.13f, 0.9f),
                new Vector2(0.52f, 0.18f), new Vector2(0.96f, 0.85f));
            AddOutline(rightCol, DarkBorderColor, 2f);

            CreateText(rightCol.transform, "Sec_Rules", "REGLAS DE PARTIDA (TP2)", 24, FontStyle.Bold,
                new Color(1f, 0.88f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));

            // Monedas Iniciales (Por defecto 3 según pág. 5)
            CreateText(rightCol.transform, "Lbl_StartCoins", "Monedas Iniciales (TP2: 3):", 20, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.72f), new Vector2(0.62f, 0.84f));

            CreateStyledButton(rightCol.transform, "Btn_CoinsMinus", "-", new Vector2(0.64f, 0.73f), new Vector2(0.73f, 0.83f),
                CreamButtonColor, DarkBorderColor, 24, () => ChangeStartingCoins(-1));

            startCoinsValueText = CreateText(rightCol.transform, "Txt_StartCoins", "3", 22, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.72f), new Vector2(0.84f, 0.84f));

            CreateStyledButton(rightCol.transform, "Btn_CoinsPlus", "+", new Vector2(0.85f, 0.73f), new Vector2(0.94f, 0.83f),
                CreamButtonColor, DarkBorderColor, 24, () => ChangeStartingCoins(1));

            // Nivel de EXP Inicial (Por defecto 1, máx 22 según pág. 18)
            CreateText(rightCol.transform, "Lbl_StartLevel", "Nivel EXP Inicial (Máx 22):", 20, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.56f), new Vector2(0.62f, 0.68f));

            CreateStyledButton(rightCol.transform, "Btn_LvlMinus", "-", new Vector2(0.64f, 0.57f), new Vector2(0.73f, 0.67f),
                CreamButtonColor, DarkBorderColor, 24, () => ChangeStartingLevel(-1));

            startLevelValueText = CreateText(rightCol.transform, "Txt_StartLevel", "1", 22, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.56f), new Vector2(0.84f, 0.68f));

            CreateStyledButton(rightCol.transform, "Btn_LvlPlus", "+", new Vector2(0.85f, 0.57f), new Vector2(0.94f, 0.67f),
                CreamButtonColor, DarkBorderColor, 24, () => ChangeStartingLevel(1));

            // Caída exacta en casilla 63
            exactGoalToggle = CreateToggleRow(rightCol.transform, "Caída Exacta en Casilla 63 (Rebote)",
                new Vector2(0.06f, 0.40f), new Vector2(0.94f, 0.51f), (isOn) =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.Options.exactRollForGoal = isOn;
                    }
                });

            // Bosses activos (La Foca, La Roca, La Trollface)
            bossesToggle = CreateToggleRow(rightCol.transform, "Activar los 3 Bosses del Tablero",
                new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.36f), (isOn) =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.Options.enableBosses = isOn;
                    }
                });

            CreateText(rightCol.transform, "Txt_ControlsHelp",
                "Controles de Cámara en Partida:\n• [A / D] Rotar vista 90° cardinal\n• [Flechas Izq / Der] Cambiar de Tablero\n• [Rueda Ratón] Zoom   • [Espacio] Vista General",
                16, FontStyle.Normal, new Color(0.80f, 0.90f, 0.80f), TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.23f));

            // Botones inferiores de Opciones
            CreateMockupMenuButton(container.transform, "Btn_ResetDefaults", "Valores por Defecto",
                new Vector2(0.06f, 0.03f), new Vector2(0.42f, 0.14f), () =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.Options.ResetToDefaults();
                        RefreshOptionsUI();
                    }
                });

            CreateStyledButton(container.transform, "Btn_SaveAndBack", "Guardar y Volver",
                new Vector2(0.56f, 0.03f), new Vector2(0.94f, 0.14f),
                AccentGreenColor, Color.white, 28, () =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.ApplyOptions();
                        if (GameSessionManager.Instance.IsMatchActive)
                        {
                            ShowInGameHUD();
                            return;
                        }
                    }
                    ShowMainMenu();
                });
        }

        // ---------------------------------------------------------------------
        // 4. PANEL DE CRÉDITOS (Datos del TP2)
        // ---------------------------------------------------------------------
        private void BuildCreditsPanel(Transform parent)
        {
            creditsPanel = CreatePanel(parent, "Credits_Panel", PanelBgColor);

            GameObject box = CreateBox(creditsPanel.transform, "Credits_Window", CardBgColor,
                new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.86f));
            AddOutline(box, DarkBorderColor, 4f);

            CreateText(box.transform, "Credits_Title", "CRÉDITOS - LA OCA 2", 42, FontStyle.Bold,
                TitleRedColor, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f));

            string creditsBody =
                "UNIVERSIDAD DE LA CUENCA DEL PLATA\n" +
                "Facultad de Ingeniería, Tecnología y Arquitectura\n" +
                "Carrera: Ingeniería en Sistemas de Información\n" +
                "Asignatura: Diseño y Desarrollo de Videojuegos (Año 2026)\n\n" +
                "EQUIPO DE DESARROLLO (TP2 - AE2):\n" +
                "• Alfonzo Enzo — Programador\n" +
                "• Evin Linder Jeuel Elias Natanael — Encargado de las Ideas y Mecánicas\n\n" +
                "Personajes: La Oca Default, La Ocarina, La Oca de Boca, La Loca, La Barroca y El Xoca.";

            CreateText(box.transform, "Credits_Body", creditsBody, 23, FontStyle.Normal,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.80f));

            CreateMockupMenuButton(box.transform, "Btn_CloseCredits", "Volver al Menú",
                new Vector2(0.28f, 0.05f), new Vector2(0.72f, 0.18f), ShowMainMenu);
        }

        // ---------------------------------------------------------------------
        // 5. HUD EN PARTIDA (Resumen de jugadores + Tirar Dado + Menú)
        // ---------------------------------------------------------------------
        private void BuildInGameHudPanel(Transform parent)
        {
            inGameHudPanel = CreatePanel(parent, "InGameHUD_Panel", Color.clear);

            // Barra superior con las tarjetas de los jugadores activos
            GameObject topBar = CreateBox(inGameHudPanel.transform, "Top_PlayersBar", new Color(0.07f, 0.10f, 0.07f, 0.88f),
                new Vector2(0.01f, 0.87f), new Vector2(0.99f, 0.99f));
            AddOutline(topBar, DarkBorderColor, 2f);

            HorizontalLayoutGroup hLayout = topBar.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10f;
            hLayout.padding = new RectOffset(12, 12, 6, 6);
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = true;
            hLayout.childForceExpandHeight = true;
            hudPlayersBar = topBar.transform;

            // Barra inferior de estado, Barra de Experiencia/Crecimiento y botones de acción de turno
            GameObject bottomBar = CreateBox(inGameHudPanel.transform, "Bottom_ActionBar", new Color(0.08f, 0.11f, 0.08f, 0.94f),
                new Vector2(0.01f, 0.012f), new Vector2(0.99f, 0.165f));
            AddOutline(bottomBar, DarkBorderColor, 2f);

            // Franja superior de la barra inferior: Barra Visual de Experiencia (1..22) y Resumen de Crecimiento del Jugador en Turno
            GameObject expStrip = CreateBox(bottomBar.transform, "ActivePlayer_ExpAndGrowthStrip", new Color(0.05f, 0.08f, 0.05f, 0.95f),
                new Vector2(0.01f, 0.62f), new Vector2(0.99f, 0.96f));
            AddOutline(expStrip, new Color(0.25f, 0.55f, 0.30f, 0.8f), 1f);

            // Barra de progreso de EXP rellenable
            GameObject expBarBg = CreateBox(expStrip.transform, "Hud_ExpBar_Bg", new Color(0.12f, 0.14f, 0.18f, 1f),
                new Vector2(0.01f, 0.14f), new Vector2(0.38f, 0.86f));
            AddOutline(expBarBg, new Color(0.30f, 0.72f, 0.95f), 1.5f);

            GameObject expBarFill = CreateBox(expBarBg.transform, "Hud_ExpBar_Fill", new Color(0.20f, 0.68f, 0.92f, 0.95f),
                new Vector2(0f, 0f), new Vector2(0f, 1f));
            hudExpBarFillRt = expBarFill.GetComponent<RectTransform>();

            hudExpBarText = CreateText(expBarBg.transform, "Hud_ExpBar_Text", "NIVEL 1 / 22  •  EXP: 0 / 1", 14, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AddShadow(hudExpBarText.gameObject, Color.black, new Vector2(1f, -1f));

            hudGrowthStatsText = CreateText(expStrip.transform, "Hud_GrowthStats_Text",
                "Crecimiento: Mov 1d6 | Att 1d6 | Oro/casilla: +1 | Maná/luna: +1 | Nodos: 0/21 | Escala: 1.00x",
                14, FontStyle.Bold, new Color(0.82f, 0.96f, 0.82f), TextAnchor.MiddleLeft,
                new Vector2(0.395f, 0.05f), new Vector2(0.99f, 0.95f));

            // Franja inferior: Turno, Estado y Botones
            hudTurnHeaderText = CreateText(bottomBar.transform, "Txt_TurnHeader", "Turno: Jugador 1", 18, FontStyle.Bold,
                new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleLeft, new Vector2(0.015f, 0.32f), new Vector2(0.45f, 0.60f));

            hudStatusText = CreateText(bottomBar.transform, "Txt_Status", "Agarra el Vaso 3D con el ratón, agítalo y suéltalo para lanzar el dado.", 15, FontStyle.Normal,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.015f, 0.04f), new Vector2(0.45f, 0.32f));

            rollDiceButton = CreateStyledButton(bottomBar.transform, "Btn_RollDice", "Agitar Vaso",
                new Vector2(0.46f, 0.08f), new Vector2(0.58f, 0.56f),
                AccentGreenColor, Color.white, 19, () =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.RollDiceForCurrentPlayer();
                    }
                });

            Button skillBtn = CreateStyledButton(bottomBar.transform, "Btn_SkillTree", "Árbol EXP",
                new Vector2(0.59f, 0.08f), new Vector2(0.70f, 0.56f),
                new Color(0.20f, 0.52f, 0.85f, 1f), Color.white, 17, OpenSkillTreeModal);
            skillTreeBtnLabel = skillBtn.GetComponentInChildren<Text>();

            Button bagBtn = CreateStyledButton(bottomBar.transform, "Btn_BagAndSpell", "Mochila / Magia",
                new Vector2(0.71f, 0.08f), new Vector2(0.83f, 0.56f),
                new Color(0.58f, 0.28f, 0.82f, 1f), Color.white, 17, OpenBagAndMagicModal);
            bagSpellBtnLabel = bagBtn.GetComponentInChildren<Text>();

            CreateMockupMenuButton(bottomBar.transform, "Btn_InGameOptions", "Opciones",
                new Vector2(0.84f, 0.08f), new Vector2(0.91f, 0.56f), ShowOptions);

            CreateMockupMenuButton(bottomBar.transform, "Btn_ReturnMenu", "Menú",
                new Vector2(0.92f, 0.08f), new Vector2(0.99f, 0.56f), () =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.ReturnToMainMenu();
                    }
                    else
                    {
                        ShowMainMenu();
                    }
                });

            BuildSkillTreeModalPanel(inGameHudPanel.transform);
            BuildShopModalPanel(inGameHudPanel.transform);
            BuildBagAndMagicModalPanel(inGameHudPanel.transform);
            BuildCombatModalPanel(inGameHudPanel.transform);
        }

        // ---------------------------------------------------------------------
        // 5A. MODAL DEL ÁRBOL DE HABILIDADES ("Skill Tree.png", Niveles 1 a 22 + 21 Nodos Circulares)
        // ---------------------------------------------------------------------
        private void BuildSkillTreeModalPanel(Transform parent)
        {
            skillTreeModalPanel = CreatePanel(parent, "SkillTree_ModalOverlay", new Color(0.02f, 0.04f, 0.02f, 0.88f));

            GameObject window = CreateBox(skillTreeModalPanel.transform, "SkillTree_Window", CardBgColor,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f));
            AddOutline(window, new Color(0.25f, 0.68f, 0.95f), 4f);

            skillTreeHeaderText = CreateText(window.transform, "Header", "ÁRBOL DE EXPERIENCIA Y CRECIMIENTO (NIVEL 1/22)", 25, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.91f), new Vector2(0.97f, 0.985f));

            // 1) PISTA DE NIVELES 1 A 22 (idéntica a la barra superior de "Skill Tree.png")
            GameObject trackBox = CreateBox(window.transform, "LevelTrack_1_To_22", new Color(0.09f, 0.12f, 0.09f, 0.98f),
                new Vector2(0.03f, 0.80f), new Vector2(0.97f, 0.905f));
            AddOutline(trackBox, DarkBorderColor, 2f);

            for (int lvl = 1; lvl <= 22; lvl++)
            {
                int idx = lvl - 1;
                float xMin = 0.01f + idx * (0.98f / 22f);
                float xMax = xMin + (0.98f / 22f) - 0.003f;

                GameObject cell = CreateBox(trackBox.transform, $"LvlCell_{lvl}", new Color(0.15f, 0.18f, 0.15f, 1f),
                    new Vector2(xMin, 0.08f), new Vector2(xMax, 0.92f));
                AddOutline(cell, DarkBorderColor, 1f);

                // Punto superior sobre cada número como en Skill Tree.png
                GameObject dotObj = CreateBox(cell.transform, "Dot", new Color(0.3f, 0.3f, 0.3f, 1f),
                    new Vector2(0.25f, 0.58f), new Vector2(0.75f, 0.90f));
                skillTreeLevelDots[idx] = dotObj.GetComponent<Image>();

                skillTreeLevelNumbers[idx] = CreateText(cell.transform, "Num", lvl.ToString(), 14, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 0.04f), new Vector2(1f, 0.56f));
            }

            // 2) BARRA DE PROGRESO DE EXPERIENCIA HACIA EL SIGUIENTE NIVEL + PASIVA DE OCA
            GameObject expBarBg = CreateBox(window.transform, "Modal_ExpBar_Bg", new Color(0.10f, 0.13f, 0.18f, 1f),
                new Vector2(0.03f, 0.73f), new Vector2(0.48f, 0.79f));
            AddOutline(expBarBg, new Color(0.28f, 0.72f, 0.98f), 2f);

            GameObject expBarFill = CreateBox(expBarBg.transform, "Modal_ExpBar_Fill", new Color(0.18f, 0.68f, 0.95f, 0.96f),
                new Vector2(0f, 0f), new Vector2(0f, 1f));
            skillTreeExpBarFillRt = expBarFill.GetComponent<RectTransform>();

            skillTreeExpBarText = CreateText(expBarBg.transform, "Modal_ExpBar_Text", "EXP: 0 / 1 para subir a Nivel 2", 15, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AddShadow(skillTreeExpBarText.gameObject, Color.black, new Vector2(1f, -1f));

            skillTreePassiveText = CreateText(window.transform, "PassiveInfo", "Pasiva de Oca: —", 15, FontStyle.Bold,
                new Color(0.78f, 0.96f, 0.78f), TextAnchor.MiddleRight, new Vector2(0.50f, 0.73f), new Vector2(0.97f, 0.79f));

            // 3) LAS 4 COLUMNAS DEL ÁRBOL DE HABILIDADES CON SUS 21 NODOS CIRCULARES (abajo hacia arriba como en "Skill Tree.png")
            // Columna 1: MOVIMIENTO (5 círculos: 3 base abajo->arriba + bifurcación en la cima: Izq +1d6 Mov / Der +3 Mov)
            GameObject colMov = CreateSkillColumnBox(window.transform, "Col_Movement", "RAMA 1: MOVIMIENTO",
                new Vector2(0.03f, 0.14f), new Vector2(0.255f, 0.715f), out skillBranchMovText);
            CreateConnectingLine(colMov.transform, new Vector2(0.49f, 0.16f), new Vector2(0.51f, 0.62f));
            CreateConnectingLine(colMov.transform, new Vector2(0.26f, 0.60f), new Vector2(0.74f, 0.62f));
            CreateConnectingLine(colMov.transform, new Vector2(0.25f, 0.60f), new Vector2(0.27f, 0.76f));
            CreateConnectingLine(colMov.transform, new Vector2(0.73f, 0.60f), new Vector2(0.75f, 0.76f));

            CreateSkillNodeButton(colMov.transform, 0, "+1 Mov", new Vector2(0.22f, 0.06f), new Vector2(0.78f, 0.20f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.Movement));
            CreateSkillNodeButton(colMov.transform, 1, "+2 Mov", new Vector2(0.22f, 0.26f), new Vector2(0.78f, 0.40f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.Movement));
            CreateSkillNodeButton(colMov.transform, 2, "+1d6 Mov", new Vector2(0.22f, 0.46f), new Vector2(0.78f, 0.60f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.Movement));
            CreateSkillNodeButton(colMov.transform, 3, "+1d6 Mov", new Vector2(0.04f, 0.68f), new Vector2(0.48f, 0.84f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.MovementTopLeft));
            CreateSkillNodeButton(colMov.transform, 4, "+3 Mov", new Vector2(0.52f, 0.68f), new Vector2(0.96f, 0.84f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.MovementTopRight));

            // Columna 2: COMBATE (6 círculos: Base +1 Att abajo -> Bifurcación Izq [+1d6, +1d6] y Der [+2, +1d8, +1d10])
            GameObject colComb = CreateSkillColumnBox(window.transform, "Col_Combat", "RAMA 2: COMBATE",
                new Vector2(0.268f, 0.14f), new Vector2(0.515f, 0.715f), out skillBranchCombLeftText);
            CreateConnectingLine(colComb.transform, new Vector2(0.26f, 0.20f), new Vector2(0.74f, 0.22f));
            CreateConnectingLine(colComb.transform, new Vector2(0.25f, 0.20f), new Vector2(0.27f, 0.62f));
            CreateConnectingLine(colComb.transform, new Vector2(0.73f, 0.20f), new Vector2(0.75f, 0.76f));

            CreateSkillNodeButton(colComb.transform, 5, "+1 Att\n(Base)", new Vector2(0.24f, 0.05f), new Vector2(0.76f, 0.20f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatLeft));
            CreateSkillNodeButton(colComb.transform, 6, "+1d6 Att", new Vector2(0.04f, 0.28f), new Vector2(0.48f, 0.43f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatLeft));
            CreateSkillNodeButton(colComb.transform, 7, "+1d6 Att", new Vector2(0.04f, 0.50f), new Vector2(0.48f, 0.65f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatLeft));
            CreateSkillNodeButton(colComb.transform, 8, "+2 Att", new Vector2(0.52f, 0.26f), new Vector2(0.96f, 0.40f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatRight));
            CreateSkillNodeButton(colComb.transform, 9, "+1d8 Att", new Vector2(0.52f, 0.47f), new Vector2(0.96f, 0.61f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatRight));
            CreateSkillNodeButton(colComb.transform, 10, "+1d10 Att", new Vector2(0.52f, 0.68f), new Vector2(0.96f, 0.84f),
                () => UpgradeSkillBranchFromUI(SkillBranchType.CombatRight));

            // Columna 3: RIQUEZA (5 círculos verticales abajo->arriba: Paso 1, +1 Mon/casilla, Paso 3, Paso 4, +1 Mon/casilla)
            GameObject colWealth = CreateSkillColumnBox(window.transform, "Col_Wealth", "RAMA 3: RIQUEZA",
                new Vector2(0.528f, 0.14f), new Vector2(0.742f, 0.715f), out skillBranchWealthText);
            CreateConnectingLine(colWealth.transform, new Vector2(0.49f, 0.14f), new Vector2(0.51f, 0.78f));

            string[] wealthLabels = { "Paso 1\n(○)", "+1 Moneda\n/ casilla", "Paso 3\n(○)", "Paso 4\n(○)", "+1 Moneda\n/ casilla" };
            for (int n = 0; n < 5; n++)
            {
                float yMin = 0.05f + n * 0.16f;
                CreateSkillNodeButton(colWealth.transform, 11 + n, wealthLabels[n],
                    new Vector2(0.16f, yMin), new Vector2(0.84f, yMin + 0.14f),
                    () => UpgradeSkillBranchFromUI(SkillBranchType.Wealth));
            }

            // Columna 4: MAGIA (5 círculos verticales abajo->arriba: Paso 1, +1 Maná/luna, Paso 3, Paso 4, +1 Maná/luna)
            GameObject colMagic = CreateSkillColumnBox(window.transform, "Col_Magic", "RAMA 4: MAGIA LUNAR",
                new Vector2(0.755f, 0.14f), new Vector2(0.97f, 0.715f), out skillBranchMagicText);
            CreateConnectingLine(colMagic.transform, new Vector2(0.49f, 0.14f), new Vector2(0.51f, 0.78f));

            string[] magicLabels = { "Paso 1\n(○)", "+1 Maná\n/ luna", "Paso 3\n(○)", "Paso 4\n(○)", "+1 Maná\n/ luna" };
            for (int n = 0; n < 5; n++)
            {
                float yMin = 0.05f + n * 0.16f;
                CreateSkillNodeButton(colMagic.transform, 16 + n, magicLabels[n],
                    new Vector2(0.16f, yMin), new Vector2(0.84f, yMin + 0.14f),
                    () => UpgradeSkillBranchFromUI(SkillBranchType.Magic));
            }

            skillTreeFeedbackText = CreateText(window.transform, "Feedback",
                "Haz clic en cualquier nodo circular disponible (de abajo hacia arriba) para colocar tu ficha de habilidad.",
                16, FontStyle.Bold, new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0.03f), new Vector2(0.75f, 0.12f));

            CreateMockupMenuButton(window.transform, "Btn_CloseSkillTree", "Cerrar Árbol",
                new Vector2(0.77f, 0.03f), new Vector2(0.97f, 0.12f), () => skillTreeModalPanel.SetActive(false));

            skillTreeModalPanel.SetActive(false);
        }

        private GameObject CreateSkillColumnBox(Transform parent, string name, string title, Vector2 anchorMin, Vector2 anchorMax, out Text statusOut)
        {
            GameObject col = CreateBox(parent, name, new Color(0.10f, 0.14f, 0.10f, 0.96f), anchorMin, anchorMax);
            AddOutline(col, DarkBorderColor, 2f);

            CreateText(col.transform, "Title", title, 16, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.99f));

            statusOut = CreateText(col.transform, "Status", "0 / 5", 13, FontStyle.Bold,
                new Color(0.75f, 0.95f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.04f, 0.84f), new Vector2(0.96f, 0.91f));

            return col;
        }

        private void CreateConnectingLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            CreateBox(parent, "TreeLine", new Color(0.45f, 0.50f, 0.45f, 0.85f), anchorMin, anchorMax);
        }

        private void CreateSkillNodeButton(Transform parent, int nodeGlobalIdx, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            Button btn = CreateStyledButton(parent, $"SkillNode_{nodeGlobalIdx}", label,
                anchorMin, anchorMax, new Color(0.22f, 0.25f, 0.22f, 1f), Color.white, 13, onClick);
            skillNodeCircles[nodeGlobalIdx] = btn.GetComponent<Image>();
            skillNodeTexts[nodeGlobalIdx] = btn.GetComponentInChildren<Text>();
            skillNodeButtons[nodeGlobalIdx] = btn;
        }

        // ---------------------------------------------------------------------
        // 5B. MODAL DE TIENDA PRINCIPAL Y TIENDA DEL INFIERNO ("Tienda.png", "Tienda infierno.png")
        // ---------------------------------------------------------------------
        private void BuildShopModalPanel(Transform parent)
        {
            shopModalPanel = CreatePanel(parent, "Shop_ModalOverlay", new Color(0.03f, 0.03f, 0.02f, 0.85f));

            GameObject window = CreateBox(shopModalPanel.transform, "Shop_Window", CardBgColor,
                new Vector2(0.10f, 0.12f), new Vector2(0.90f, 0.88f));
            AddOutline(window, new Color(0.95f, 0.78f, 0.22f), 4f);

            shopHeaderTitleText = CreateText(window.transform, "Header", "TIENDA PRINCIPAL", 34, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.88f), new Vector2(0.68f, 0.98f));

            shopPlayerCoinsText = CreateText(window.transform, "Coins", "Tus Monedas: 3", 24, FontStyle.Bold,
                new Color(1f, 0.88f, 0.25f), TextAnchor.MiddleRight, new Vector2(0.68f, 0.88f), new Vector2(0.96f, 0.98f));

            shopSubNoticeText = CreateText(window.transform, "SubNotice",
                "Puedes comprar objetos normales al pasar por la Tienda. La Caja Especial requiere caer exactamente en la casilla.",
                17, FontStyle.Italic, new Color(0.82f, 0.92f, 0.82f), TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.88f));

            GameObject gridObj = CreateBox(window.transform, "Items_Grid", Color.clear,
                new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.79f));
            GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(335, 210);
            grid.spacing = new Vector2(18, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            shopGridContainer = gridObj.transform;

            shopFeedbackText = CreateText(window.transform, "Feedback", "Haz clic en un objeto para comprarlo.", 19, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.04f), new Vector2(0.72f, 0.15f));

            CreateStyledButton(window.transform, "Btn_CloseShop", "Continuar Turno",
                new Vector2(0.74f, 0.04f), new Vector2(0.96f, 0.15f),
                AccentGreenColor, Color.white, 22, () => shopModalPanel.SetActive(false));

            shopModalPanel.SetActive(false);
        }

        // ---------------------------------------------------------------------
        // 5C. MODAL DE MOCHILA (INVENTARIO) Y HECHIZOS ("Hechizos.jpg")
        // ---------------------------------------------------------------------
        private void BuildBagAndMagicModalPanel(Transform parent)
        {
            bagAndMagicModalPanel = CreatePanel(parent, "BagAndMagic_ModalOverlay", new Color(0.03f, 0.02f, 0.05f, 0.85f));

            GameObject window = CreateBox(bagAndMagicModalPanel.transform, "Bag_Window", CardBgColor,
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            AddOutline(window, new Color(0.68f, 0.35f, 0.95f), 4f);

            bagHeaderText = CreateText(window.transform, "Header", "MOCHILA DE OBJETOS Y MAGIA LUNAR", 30, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.98f));

            // Panel superior de Hechizo equipado + Selector de jugador objetivo + Selector de pasos (Lupa/Paso Ligero)
            GameObject spellBox = CreateBox(window.transform, "Spell_Section", new Color(0.14f, 0.11f, 0.22f, 0.95f),
                new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.86f));
            AddOutline(spellBox, new Color(0.55f, 0.35f, 0.85f), 2f);

            bagSpellInfoText = CreateText(spellBox.transform, "SpellInfo", "Hechizo Equipado: Ninguno (Maná: 0)", 19, FontStyle.Bold,
                new Color(0.85f, 0.78f, 1f), TextAnchor.MiddleLeft, new Vector2(0.02f, 0.52f), new Vector2(0.72f, 0.96f));

            CreateStyledButton(spellBox.transform, "Btn_CastSpell", "Lanzar Hechizo",
                new Vector2(0.74f, 0.54f), new Vector2(0.98f, 0.92f),
                new Color(0.55f, 0.25f, 0.85f, 1f), Color.white, 19, CastEquippedTurnSpellFromUI);

            // Selector de Jugador Objetivo (para Pistola, Teletransporte, Congelar, Ralentizar, Muerte, Limpieza)
            CreateText(spellBox.transform, "Lbl_Target", "Jugador Objetivo:", 17, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.06f), new Vector2(0.18f, 0.46f));

            CreateStyledButton(spellBox.transform, "Btn_PrevTarget", "<",
                new Vector2(0.19f, 0.10f), new Vector2(0.24f, 0.44f),
                CreamButtonColor, DarkBorderColor, 18, () => CycleTargetPlayer(-1));

            bagTargetPlayerText = CreateText(spellBox.transform, "Txt_TargetPlayer", "Jugador 2", 18, FontStyle.Bold,
                new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.25f, 0.06f), new Vector2(0.45f, 0.46f));

            CreateStyledButton(spellBox.transform, "Btn_NextTarget", ">",
                new Vector2(0.46f, 0.10f), new Vector2(0.51f, 0.44f),
                CreamButtonColor, DarkBorderColor, 18, () => CycleTargetPlayer(1));

            // Ajuste de pasos exactos cuando Lupa o Paso Ligero están activos
            bagStepControlText = CreateText(spellBox.transform, "Txt_StepForced", "Tirada Fija (Lupa/Paso Ligero): Automática", 16, FontStyle.Bold,
                new Color(0.75f, 0.95f, 0.75f), TextAnchor.MiddleRight, new Vector2(0.53f, 0.06f), new Vector2(0.84f, 0.46f));

            CreateStyledButton(spellBox.transform, "Btn_StepMinus", "-",
                new Vector2(0.85f, 0.10f), new Vector2(0.91f, 0.44f),
                CreamButtonColor, DarkBorderColor, 18, () => AdjustForcedMoveSteps(-1));

            CreateStyledButton(spellBox.transform, "Btn_StepPlus", "+",
                new Vector2(0.92f, 0.10f), new Vector2(0.98f, 0.44f),
                CreamButtonColor, DarkBorderColor, 18, () => AdjustForcedMoveSteps(1));

            // Grilla de Objetos en Inventario
            GameObject itemsBox = CreateBox(window.transform, "BagItems_Grid", Color.clear,
                new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.59f));
            GridLayoutGroup grid = itemsBox.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(315, 130);
            grid.spacing = new Vector2(14, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperLeft;
            bagItemsContainer = itemsBox.transform;

            bagFeedbackText = CreateText(window.transform, "Feedback",
                "Haz clic en un objeto de tu mochila para usarlo antes de tirar el dado.",
                18, FontStyle.Bold, CreamButtonColor, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.04f), new Vector2(0.72f, 0.15f));

            CreateMockupMenuButton(window.transform, "Btn_CloseBag", "Cerrar Mochila",
                new Vector2(0.74f, 0.04f), new Vector2(0.96f, 0.15f), () => bagAndMagicModalPanel.SetActive(false));

            bagAndMagicModalPanel.SetActive(false);
        }

        // ---------------------------------------------------------------------
        // 6. VENTANA MODAL DE COMBATE (Bosses: La Foca, La Roca, La Trollface y PvP)
        // ---------------------------------------------------------------------
        private void BuildCombatModalPanel(Transform parent)
        {
            combatModalPanel = CreatePanel(parent, "Combat_ModalOverlay", new Color(0.03f, 0.02f, 0.02f, 0.82f));

            GameObject arenaWindow = CreateBox(combatModalPanel.transform, "Combat_ArenaWindow", new Color(0.16f, 0.12f, 0.12f, 0.98f),
                new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.90f));
            AddOutline(arenaWindow, new Color(0.85f, 0.18f, 0.18f), 4f);

            // Cabecera del combate
            GameObject headerBar = CreateBox(arenaWindow.transform, "Header", new Color(0.55f, 0.08f, 0.08f, 1f),
                new Vector2(0f, 0.88f), new Vector2(1f, 1f));
            combatHeaderTitleText = CreateText(headerBar.transform, "Txt_CombatTitle", "¡ENCUENTRO DE COMBATE!", 34, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AddShadow(combatHeaderTitleText.gameObject, Color.black, new Vector2(3f, -3f));

            // Tarjeta Izquierda: Jugador Atacante
            GameObject leftCard = CreateBox(arenaWindow.transform, "Attacker_Card", new Color(0.12f, 0.20f, 0.14f, 0.96f),
                new Vector2(0.04f, 0.40f), new Vector2(0.43f, 0.85f));
            AddOutline(leftCard, AccentGreenColor, 3f);

            GameObject attPortraitBg = CreateBox(leftCard.transform, "Portrait_Bg", new Color(0.95f, 0.94f, 0.88f, 1f),
                new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.94f));
            AddOutline(attPortraitBg, DarkBorderColor, 2f);
            GameObject attImgObj = CreateBox(attPortraitBg.transform, "Img", Color.white,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            attackerPortraitImg = attImgObj.GetComponent<Image>();
            attackerPortraitImg.preserveAspect = true;

            attackerNameText = CreateText(leftCard.transform, "Txt_AttName", "Jugador 1", 22, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.42f));

            attackerFormulaText = CreateText(leftCard.transform, "Txt_AttFormula", "Fuerza de Combate: 1d6", 18, FontStyle.Bold,
                new Color(0.75f, 0.95f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.30f));

            attackerRollResultText = CreateText(leftCard.transform, "Txt_AttRoll", "Tirada: —", 22, FontStyle.Bold,
                new Color(1f, 0.90f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.17f));

            // Emblema central VS
            Text vsText = CreateText(arenaWindow.transform, "Txt_VS", "VS", 54, FontStyle.BoldAndItalic,
                TitleRedColor, TextAnchor.MiddleCenter, new Vector2(0.43f, 0.54f), new Vector2(0.57f, 0.74f));
            AddShadow(vsText.gameObject, Color.black, new Vector2(3f, -3f));

            // Tarjeta Derecha: Defensor (Boss u Oca Rival)
            GameObject rightCard = CreateBox(arenaWindow.transform, "Defender_Card", new Color(0.24f, 0.12f, 0.12f, 0.96f),
                new Vector2(0.57f, 0.40f), new Vector2(0.96f, 0.85f));
            AddOutline(rightCard, TitleRedColor, 3f);

            GameObject defPortraitBg = CreateBox(rightCard.transform, "Portrait_Bg", new Color(0.95f, 0.94f, 0.88f, 1f),
                new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.94f));
            AddOutline(defPortraitBg, DarkBorderColor, 2f);
            GameObject defImgObj = CreateBox(defPortraitBg.transform, "Img", Color.white,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            defenderPortraitImg = defImgObj.GetComponent<Image>();
            defenderPortraitImg.preserveAspect = true;

            defenderNameText = CreateText(rightCard.transform, "Txt_DefName", "La Foca", 22, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.42f));

            defenderFormulaText = CreateText(rightCard.transform, "Txt_DefFormula", "Fuerza de Combate: 1d8", 18, FontStyle.Bold,
                new Color(1f, 0.75f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.30f));

            defenderRollResultText = CreateText(rightCard.transform, "Txt_DefRoll", "Tirada: —", 22, FontStyle.Bold,
                new Color(1f, 0.90f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.17f));

            // Barra de Objetos y Hechizos de Combate (Armadura +5, Espada +8, Pentagrama -1d6, UNO Reverse, Hechizo Combate)
            GameObject combatToolsBar = CreateBox(arenaWindow.transform, "CombatTools_Bar", new Color(0.11f, 0.11f, 0.14f, 0.95f),
                new Vector2(0.04f, 0.29f), new Vector2(0.96f, 0.38f));
            AddOutline(combatToolsBar, DarkBorderColor, 2f);

            CreateStyledButton(combatToolsBar.transform, "Btn_UseArmor", "Armadura (+5)",
                new Vector2(0.01f, 0.12f), new Vector2(0.19f, 0.88f),
                CreamButtonColor, DarkBorderColor, 15, () => TryUseCombatItemFromUI(ItemType.Armadura));

            CreateStyledButton(combatToolsBar.transform, "Btn_UseSword", "Espada (+8)",
                new Vector2(0.20f, 0.12f), new Vector2(0.38f, 0.88f),
                CreamButtonColor, DarkBorderColor, 15, () => TryUseCombatItemFromUI(ItemType.Espada));

            CreateStyledButton(combatToolsBar.transform, "Btn_UsePentagram", "Pentagrama (-1d6)",
                new Vector2(0.39f, 0.12f), new Vector2(0.59f, 0.88f),
                CreamButtonColor, DarkBorderColor, 15, () => TryUseCombatItemFromUI(ItemType.Pentagrama));

            CreateStyledButton(combatToolsBar.transform, "Btn_UseUnoReverse", "UNO Reverse",
                new Vector2(0.60f, 0.12f), new Vector2(0.78f, 0.88f),
                new Color(0.95f, 0.78f, 0.20f), DarkBorderColor, 15, () => TryUseCombatItemFromUI(ItemType.CartaUNOReverse));

            CreateStyledButton(combatToolsBar.transform, "Btn_CastCombatSpell", "Hechizo Combate",
                new Vector2(0.79f, 0.12f), new Vector2(0.99f, 0.88f),
                new Color(0.55f, 0.25f, 0.85f), Color.white, 15, TryCastCombatSpellFromUI);

            // Narración / Resultado del Combate
            GameObject narrationBox = CreateBox(arenaWindow.transform, "Narration_Box", new Color(0.09f, 0.09f, 0.09f, 0.95f),
                new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.27f));
            AddOutline(narrationBox, DarkBorderColor, 2f);

            combatNarrationText = CreateText(narrationBox.transform, "Txt_Narration",
                "¡Lanza los dados de combate! El ganador recibe +3 de EXP y el perdedor muere.", 19, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));

            // Botones de acción de combate
            combatRollBtn = CreateStyledButton(arenaWindow.transform, "Btn_CombatRoll", "¡LANZAR DADOS DE COMBATE!",
                new Vector2(0.16f, 0.025f), new Vector2(0.56f, 0.12f),
                TitleRedColor, Color.white, 22, () =>
                {
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.RollActiveCombat();
                    }
                });

            combatContinueBtn = CreateStyledButton(arenaWindow.transform, "Btn_CombatContinue", "Continuar Partida",
                new Vector2(0.60f, 0.025f), new Vector2(0.88f, 0.12f),
                AccentGreenColor, Color.white, 22, () =>
                {
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.ConcludeActiveCombat();
                    }
                });

            combatModalPanel.SetActive(false);
        }

        // =====================================================================
        // NAVEGACIÓN Y ACTUALIZACIÓN DE ESTADO DE PANELES
        // =====================================================================

        public void ShowMainMenu()
        {
            SetActivePanel(mainMenuPanel);
        }

        public void ShowPlayerSelection()
        {
            SetActivePanel(playerSelectionPanel);
            RefreshPlayerSelectionUI();
        }

        public void ShowOptions()
        {
            SetActivePanel(optionsPanel);
            RefreshOptionsUI();
        }

        public void ShowCredits()
        {
            SetActivePanel(creditsPanel);
        }

        public void ShowInGameHUD()
        {
            SetActivePanel(inGameHudPanel);
            RefreshInGameHudPlayers();
        }

        private void SetActivePanel(GameObject targetPanel)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(targetPanel == mainMenuPanel);
            if (playerSelectionPanel != null) playerSelectionPanel.SetActive(targetPanel == playerSelectionPanel);
            if (optionsPanel != null) optionsPanel.SetActive(targetPanel == optionsPanel);
            if (creditsPanel != null) creditsPanel.SetActive(targetPanel == creditsPanel);
            if (inGameHudPanel != null) inGameHudPanel.SetActive(targetPanel == inGameHudPanel);
        }

        private void ChangePlayerCount(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            int newCount = Mathf.Clamp(GameSessionManager.Instance.ConfiguredPlayers.Count + delta, 1, 8);
            GameSessionManager.Instance.SetPlayerCount(newCount);
            RefreshPlayerSelectionUI();
        }

        private void CyclePlayerOca(int playerIdx, int direction)
        {
            if (GameSessionManager.Instance == null) return;
            var players = GameSessionManager.Instance.ConfiguredPlayers;
            if (playerIdx < 0 || playerIdx >= players.Count) return;

            int totalOcas = 6;
            int nextOca = ((int)players[playerIdx].selectedOca + direction) % totalOcas;
            if (nextOca < 0) nextOca += totalOcas;

            players[playerIdx].selectedOca = (OcaType)nextOca;
            RefreshPlayerSelectionUI();
        }

        private void CyclePlayerColor(int playerIdx)
        {
            if (GameSessionManager.Instance == null) return;
            var players = GameSessionManager.Instance.ConfiguredPlayers;
            if (playerIdx < 0 || playerIdx >= players.Count) return;

            Color[] palette = GameSessionManager.DefaultPlayerColors;
            int currentColorIdx = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                float d = Vector4.Distance(palette[i], players[playerIdx].tokenColor);
                if (d < bestDist)
                {
                    bestDist = d;
                    currentColorIdx = i;
                }
            }

            players[playerIdx].tokenColor = palette[(currentColorIdx + 1) % palette.Length];
            RefreshPlayerSelectionUI();
        }

        public void RefreshPlayerSelectionUI()
        {
            if (GameSessionManager.Instance == null) return;
            var mgr = GameSessionManager.Instance;
            int count = mgr.ConfiguredPlayers.Count;

            if (playerCountValueText != null)
            {
                playerCountValueText.text = count == 1 ? "1 Jugador (Solo)" : $"{count} Jugadores";
            }

            if (sessionEstimateText != null)
            {
                // Según pág. 3 del TP2: estimación según cantidad de jugadores (con 6 jugadores entre 30m y 1h20m)
                int minMinutes = Mathf.Max(10, count * 6);
                int maxMinutes = Mathf.Max(20, count * 13);
                sessionEstimateText.text = $"Duración estimada (TP2): ~{minMinutes} a {maxMinutes} minutos ({count} {(count == 1 ? "jugador" : "jugadores")})";
            }

            if (startingResourcesNoticeText != null)
            {
                startingResourcesNoticeText.text =
                    $"Preparación: Cada jugador recibe {mgr.Options.startingCoins} Monedas y Tabla de EXP a Nivel {mgr.Options.startingExpLevel}";
            }

            for (int i = 0; i < playerSlotUIs.Count; i++)
            {
                PlayerSlotUI slot = playerSlotUIs[i];
                bool isActive = i < count;
                slot.rootCard.SetActive(isActive);

                if (isActive)
                {
                    PlayerSetupData pData = mgr.ConfiguredPlayers[i];
                    OcaCharacterDefinition ocaDef = mgr.GetOcaDefinition(pData.selectedOca);

                    slot.colorHeaderImage.color = pData.tokenColor;
                    if (slot.nameInput.text != pData.playerName)
                    {
                        slot.nameInput.SetTextWithoutNotify(pData.playerName);
                    }

                    if (ocaDef != null)
                    {
                        slot.ocaPortraitImage.sprite = ocaDef.sprite;
                        slot.ocaNameText.text = ocaDef.displayName;
                        slot.ocaDescText.text = ocaDef.description;
                    }

                    slot.resourcesBadgeText.text = $"Inicio: {mgr.Options.startingCoins} Monedas | Nivel {mgr.Options.startingExpLevel} EXP";
                }
            }
        }

        private void ChangeQualityLevel(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            int maxQ = Mathf.Max(0, QualitySettings.names.Length - 1);
            int nextQ = Mathf.Clamp(GameSessionManager.Instance.Options.qualityLevel + delta, 0, maxQ);
            GameSessionManager.Instance.Options.qualityLevel = nextQ;
            RefreshOptionsUI();
        }

        private void ChangeStartingCoins(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            GameSessionManager.Instance.Options.startingCoins =
                Mathf.Clamp(GameSessionManager.Instance.Options.startingCoins + delta, 0, 10);
            RefreshOptionsUI();
        }

        private void ChangeStartingLevel(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            GameSessionManager.Instance.Options.startingExpLevel =
                Mathf.Clamp(GameSessionManager.Instance.Options.startingExpLevel + delta, 1, 22);
            RefreshOptionsUI();
        }

        public void RefreshOptionsUI()
        {
            if (GameSessionManager.Instance == null) return;
            GameOptionsData opt = GameSessionManager.Instance.Options;

            if (masterVolSlider != null) masterVolSlider.SetValueWithoutNotify(opt.masterVolume);
            if (masterVolValueText != null) masterVolValueText.text = $"{Mathf.RoundToInt(opt.masterVolume * 100)}%";

            if (musicVolSlider != null) musicVolSlider.SetValueWithoutNotify(opt.musicVolume);
            if (musicVolValueText != null) musicVolValueText.text = $"{Mathf.RoundToInt(opt.musicVolume * 100)}%";

            if (sfxVolSlider != null) sfxVolSlider.SetValueWithoutNotify(opt.sfxVolume);
            if (sfxVolValueText != null) sfxVolValueText.text = $"{Mathf.RoundToInt(opt.sfxVolume * 100)}%";

            if (camSpeedSlider != null) camSpeedSlider.SetValueWithoutNotify(opt.cameraSpeedMultiplier);
            if (camSpeedValueText != null) camSpeedValueText.text = $"{opt.cameraSpeedMultiplier:F1}x";

            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(opt.fullscreen);

            if (qualityValueText != null)
            {
                string[] qNames = QualitySettings.names;
                int qIdx = Mathf.Clamp(opt.qualityLevel, 0, qNames.Length - 1);
                qualityValueText.text = qNames.Length > 0 ? qNames[qIdx] : "Estándar";
            }

            if (startCoinsValueText != null) startCoinsValueText.text = opt.startingCoins.ToString();
            if (startLevelValueText != null) startLevelValueText.text = opt.startingExpLevel.ToString();
            if (exactGoalToggle != null) exactGoalToggle.SetIsOnWithoutNotify(opt.exactRollForGoal);
            if (bossesToggle != null) bossesToggle.SetIsOnWithoutNotify(opt.enableBosses);
        }

        private void OnStartMatchClicked()
        {
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.StartMatch();
            }
        }

        private void HandleMatchStarted()
        {
            ShowInGameHUD();
        }

        private void HandleMatchEndedToMenu()
        {
            if (combatModalPanel != null) combatModalPanel.SetActive(false);
            if (skillTreeModalPanel != null) skillTreeModalPanel.SetActive(false);
            if (shopModalPanel != null) shopModalPanel.SetActive(false);
            if (bagAndMagicModalPanel != null) bagAndMagicModalPanel.SetActive(false);
            ShowMainMenu();
        }

        // =====================================================================
        // CONTROLADORES DE MODALES: ÁRBOL DE HABILIDADES, TIENDA Y MOCHILA/MAGIA
        // =====================================================================

        public void OpenSkillTreeModal()
        {
            if (skillTreeModalPanel == null) return;
            RefreshSkillTreeModal();
            skillTreeModalPanel.SetActive(true);
        }

        private void RefreshSkillTreeModal()
        {
            if (GameSessionManager.Instance == null) return;
            PlayerOcaToken token = GameSessionManager.Instance.GetCurrentTurnToken();
            if (token == null) return;

            PlayerSetupData pData = token.PlayerData;
            int totalUnlocked = SkillTreeAndAbilities.GetTotalUnlockedSkillNodes(pData);

            if (skillTreeHeaderText != null)
            {
                skillTreeHeaderText.text =
                    $"ÁRBOL DE EXPERIENCIA Y CRECIMIENTO — {pData.playerName.ToUpper()}  |  NIVEL {pData.expLevel}/22  |  FIChas DISPONIBLES: {pData.unspentSkillPoints}  |  NODOS: {totalUnlocked}/21";
            }

            // 1) Actualizar la Pista de Niveles 1 a 22 ("Skill Tree.png")
            for (int lvl = 1; lvl <= 22; lvl++)
            {
                int idx = lvl - 1;
                bool isReached = lvl <= pData.expLevel;
                bool isCurrent = lvl == pData.expLevel;

                if (skillTreeLevelDots[idx] != null)
                {
                    skillTreeLevelDots[idx].color = isCurrent
                        ? new Color(1f, 0.90f, 0.20f, 1f)
                        : (isReached ? new Color(0.25f, 0.85f, 0.42f, 1f) : new Color(0.24f, 0.26f, 0.24f, 1f));
                }

                if (skillTreeLevelNumbers[idx] != null)
                {
                    skillTreeLevelNumbers[idx].color = isCurrent
                        ? new Color(1f, 0.92f, 0.25f, 1f)
                        : (isReached ? new Color(0.75f, 1f, 0.80f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f));
                }
            }

            // 2) Actualizar Barra Visual de Progreso de EXP hacia el siguiente nivel
            float normExp = SkillTreeAndAbilities.GetExpProgressNormalized(pData);
            if (skillTreeExpBarFillRt != null)
            {
                skillTreeExpBarFillRt.anchorMax = new Vector2(Mathf.Clamp01(normExp), 1f);
            }

            if (skillTreeExpBarText != null)
            {
                if (pData.expLevel >= 22)
                {
                    skillTreeExpBarText.text = "¡NIVEL 22/22 ALCANZADO (EXPERIENCIA MÁXIMA)!";
                }
                else
                {
                    int req = SkillTreeAndAbilities.GetExpRequiredForNextLevel(pData.expLevel);
                    skillTreeExpBarText.text = $"Nivel {pData.expLevel} -> {pData.expLevel + 1}  •  EXP: {pData.currentExp} / {req} ({Mathf.RoundToInt(normExp * 100)}%)";
                }
            }

            if (skillTreePassiveText != null)
            {
                skillTreePassiveText.text = SkillTreeAndAbilities.GetOcaPassiveDescription(pData.selectedOca);
            }

            // 3) Actualizar estado de cabeceras de las 4 ramas
            SkillTreeAndAbilities.GetMovementRollParameters(pData, out int movD6, out int movFlat, out _, out _);
            string movFormula = movFlat > 0 ? $"{movD6}d6+{movFlat}" : $"{movD6}d6";
            int movCount = Mathf.Min(3, pData.movementBranchNodes) + (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
            if (movCount == 3 && pData.movementBranchNodes > 3) movCount = pData.movementBranchNodes;

            if (skillBranchMovText != null)
                skillBranchMovText.text = $"{movCount}/5 nodos  •  Actual: {movFormula}";
            if (skillBranchCombLeftText != null)
                skillBranchCombLeftText.text = $"{pData.combatLeftBranchNodes + pData.combatRightBranchNodes}/6 nodos  •  Actual: {SkillTreeAndAbilities.GetCombatFormulaSummary(pData)}";
            if (skillBranchWealthText != null)
                skillBranchWealthText.text = $"{pData.wealthBranchNodes}/5 nodos  •  +{SkillTreeAndAbilities.GetCoinsPerMoneyTile(pData)} Moneda/casilla";
            if (skillBranchMagicText != null)
                skillBranchMagicText.text = $"{pData.magicBranchNodes}/5 nodos  •  +{SkillTreeAndAbilities.GetManaPerFullMoon(pData)} Maná/luna";

            // 4) Actualizar el estado visual e interactividad de los 21 Nodos Circulares (de abajo hacia arriba)
            bool hasPoints = pData.unspentSkillPoints > 0;
            bool movTopLeft = pData.movementTopLeftUnlocked || (!pData.movementTopLeftUnlocked && !pData.movementTopRightUnlocked && pData.movementBranchNodes >= 4);
            bool movTopRight = pData.movementTopRightUnlocked || (!pData.movementTopLeftUnlocked && !pData.movementTopRightUnlocked && pData.movementBranchNodes >= 5);

            // Rama 1: Movimiento (Nodos 0..4)
            UpdateSingleSkillNodeVisual(0, "+1 Mov", pData.movementBranchNodes >= 1, pData.movementBranchNodes == 0, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(1, "+2 Mov", pData.movementBranchNodes >= 2, pData.movementBranchNodes == 1, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(2, "+1d6 Mov", pData.movementBranchNodes >= 3, pData.movementBranchNodes == 2, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(3, "+1d6 Mov", movTopLeft, pData.movementBranchNodes >= 3 && !movTopLeft, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(4, "+3 Mov", movTopRight, pData.movementBranchNodes >= 3 && !movTopRight, hasPoints, pData.tokenColor);

            // Rama 2: Combate (Nodo 5 base + Nodos 6..7 sub-rama Izq + Nodos 8..10 sub-rama Der)
            UpdateSingleSkillNodeVisual(5, "+1 Att\n(Base)", pData.combatLeftBranchNodes >= 1, pData.combatLeftBranchNodes == 0, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(6, "+1d6 Att", pData.combatLeftBranchNodes >= 2, pData.combatLeftBranchNodes == 1, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(7, "+1d6 Att", pData.combatLeftBranchNodes >= 3, pData.combatLeftBranchNodes == 2, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(8, "+2 Att", pData.combatRightBranchNodes >= 1, pData.combatLeftBranchNodes >= 1 && pData.combatRightBranchNodes == 0, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(9, "+1d8 Att", pData.combatRightBranchNodes >= 2, pData.combatLeftBranchNodes >= 1 && pData.combatRightBranchNodes == 1, hasPoints, pData.tokenColor);
            UpdateSingleSkillNodeVisual(10, "+1d10 Att", pData.combatRightBranchNodes >= 3, pData.combatLeftBranchNodes >= 1 && pData.combatRightBranchNodes == 2, hasPoints, pData.tokenColor);

            // Rama 3: Riqueza (Nodos 11..15)
            string[] wealthBaseLabels = { "Paso 1", "+1 Mon/cas.", "Paso 3", "Paso 4", "+1 Mon/cas." };
            for (int n = 0; n < 5; n++)
            {
                UpdateSingleSkillNodeVisual(11 + n, wealthBaseLabels[n],
                    pData.wealthBranchNodes >= (n + 1),
                    pData.wealthBranchNodes == n,
                    hasPoints,
                    pData.tokenColor);
            }

            // Rama 4: Magia (Nodos 16..20)
            string[] magicBaseLabels = { "Paso 1", "+1 Maná/luna", "Paso 3", "Paso 4", "+1 Maná/luna" };
            for (int n = 0; n < 5; n++)
            {
                UpdateSingleSkillNodeVisual(16 + n, magicBaseLabels[n],
                    pData.magicBranchNodes >= (n + 1),
                    pData.magicBranchNodes == n,
                    hasPoints,
                    pData.tokenColor);
            }
        }

        private void UpdateSingleSkillNodeVisual(int nodeIdx, string baseLabel, bool isUnlocked, bool isNextAvailable, bool playerHasSkillPoints, Color playerColor)
        {
            if (nodeIdx < 0 || nodeIdx >= skillNodeCircles.Length) return;

            Image img = skillNodeCircles[nodeIdx];
            Text txt = skillNodeTexts[nodeIdx];
            Button btn = skillNodeButtons[nodeIdx];

            if (isUnlocked)
            {
                if (img != null) img.color = Color.Lerp(playerColor, new Color(0.20f, 0.78f, 0.35f, 1f), 0.55f);
                if (txt != null)
                {
                    txt.text = $"[✓] {baseLabel}";
                    txt.color = Color.white;
                }
                if (btn != null) btn.interactable = false;
            }
            else if (isNextAvailable)
            {
                if (img != null)
                {
                    img.color = playerHasSkillPoints
                        ? new Color(0.22f, 0.68f, 0.32f, 1f)
                        : new Color(0.28f, 0.36f, 0.28f, 1f);
                }
                if (txt != null)
                {
                    txt.text = playerHasSkillPoints ? $"[+1 Pto] {baseLabel}" : $"[Sig.] {baseLabel}";
                    txt.color = playerHasSkillPoints ? new Color(1f, 0.96f, 0.55f, 1f) : new Color(0.85f, 0.90f, 0.85f, 1f);
                }
                if (btn != null) btn.interactable = playerHasSkillPoints;
            }
            else
            {
                if (img != null) img.color = new Color(0.15f, 0.17f, 0.15f, 0.90f);
                if (txt != null)
                {
                    txt.text = baseLabel;
                    txt.color = new Color(0.55f, 0.58f, 0.55f, 1f);
                }
                if (btn != null) btn.interactable = false;
            }
        }

        private void UpgradeSkillBranchFromUI(SkillBranchType branch)
        {
            if (GameSessionManager.Instance == null) return;
            GameSessionManager.Instance.TryUpgradeCurrentPlayerSkill(branch, out string msg);
            if (skillTreeFeedbackText != null) skillTreeFeedbackText.text = msg;
            RefreshSkillTreeModal();
            RefreshInGameHudPlayers();
        }

        private void HandleShopTriggered(PlayerOcaToken buyerToken, bool isHellShop, bool landedExactly)
        {
            if (shopModalPanel == null || buyerToken == null) return;
            RefreshShopModal(buyerToken.PlayerData, isHellShop, landedExactly);
            shopModalPanel.SetActive(true);
        }

        private void RefreshShopModal(PlayerSetupData buyer, bool isHellShop, bool landedExactly)
        {
            if (buyer == null || shopGridContainer == null) return;

            if (shopHeaderTitleText != null)
            {
                shopHeaderTitleText.text = isHellShop ? "TIENDA DEL INFIERNO" : "TIENDA PRINCIPAL";
            }

            if (shopPlayerCoinsText != null)
            {
                shopPlayerCoinsText.text = $"Monedas de {buyer.playerName}: {buyer.coins}";
            }

            if (shopSubNoticeText != null)
            {
                string specialBox = isHellShop ? "Caja Roja (Espada y Pistola)" : "Caja Verde (Lupa y Carta UNO Reverse)";
                shopSubNoticeText.text = landedExactly
                    ? $"¡Caíste EXACTAMENTE en la Tienda! Tienes acceso completo, incluyendo la {specialBox}."
                    : $"Pasaste por encima de la Tienda: puedes comprar objetos normales ({specialBox} bloqueada).";
            }

            for (int i = shopGridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(shopGridContainer.GetChild(i).gameObject);
            }

            var items = ShopAndItemsSystem.GetShopItems(isHellShop);
            foreach (var itemDef in items)
            {
                bool lockedByExact = itemDef.requiresExactLanding && !landedExactly;
                Color cardColor = itemDef.requiresExactLanding
                    ? (isHellShop ? new Color(0.32f, 0.12f, 0.12f, 0.96f) : new Color(0.12f, 0.28f, 0.14f, 0.96f))
                    : new Color(0.16f, 0.19f, 0.16f, 0.96f);

                GameObject card = CreateBox(shopGridContainer, $"ShopItem_{itemDef.itemType}", cardColor, Vector2.zero, Vector2.one);
                AddOutline(card, lockedByExact ? Color.gray : new Color(0.95f, 0.78f, 0.22f), 2f);

                if (itemDef.sprite != null)
                {
                    GameObject iconObj = CreateBox(card.transform, "Icon", Color.white,
                        new Vector2(0.04f, 0.28f), new Vector2(0.28f, 0.92f));
                    Image iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = itemDef.sprite;
                    iconImg.preserveAspect = true;
                }

                CreateText(card.transform, "Name", $"{itemDef.displayName} ({itemDef.priceCoins} Mon.)", 18, FontStyle.Bold,
                    CreamButtonColor, TextAnchor.MiddleLeft, new Vector2(0.31f, 0.66f), new Vector2(0.96f, 0.95f));

                CreateText(card.transform, "Desc", itemDef.shortEffect, 14, FontStyle.Normal,
                    Color.white, TextAnchor.UpperLeft, new Vector2(0.31f, 0.28f), new Vector2(0.96f, 0.66f));

                ItemType capturedType = itemDef.itemType;
                string btnLabel = lockedByExact ? "Requiere Caída Exacta" : $"Comprar ({itemDef.priceCoins} Mon.)";
                Button buyBtn = CreateStyledButton(card.transform, "Btn_Buy", btnLabel,
                    new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.24f),
                    lockedByExact ? new Color(0.35f, 0.35f, 0.35f) : AccentGreenColor, Color.white, 15, () =>
                    {
                        if (GameSessionManager.Instance != null)
                        {
                            GameSessionManager.Instance.TryBuyShopItemForCurrentPlayer(capturedType, out string msg);
                            if (shopFeedbackText != null) shopFeedbackText.text = msg;
                            RefreshShopModal(buyer, isHellShop, landedExactly);
                            RefreshInGameHudPlayers();
                        }
                    });
                buyBtn.interactable = !lockedByExact && buyer.coins >= itemDef.priceCoins;
            }
        }

        public void OpenBagAndMagicModal()
        {
            if (bagAndMagicModalPanel == null || GameSessionManager.Instance == null) return;
            int total = GameSessionManager.Instance.ActiveTokens.Count;
            if (total > 1 && selectedTargetPlayerIdx == GameSessionManager.Instance.CurrentTurnPlayerIndex)
            {
                selectedTargetPlayerIdx = (GameSessionManager.Instance.CurrentTurnPlayerIndex + 1) % total;
            }
            RefreshBagAndMagicModal();
            bagAndMagicModalPanel.SetActive(true);
        }

        private void CycleTargetPlayer(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            int count = GameSessionManager.Instance.ActiveTokens.Count;
            if (count <= 0) return;
            selectedTargetPlayerIdx = (selectedTargetPlayerIdx + delta) % count;
            if (selectedTargetPlayerIdx < 0) selectedTargetPlayerIdx += count;
            RefreshBagAndMagicModal();
        }

        private void AdjustForcedMoveSteps(int delta)
        {
            if (GameSessionManager.Instance == null) return;
            PlayerOcaToken token = GameSessionManager.Instance.GetCurrentTurnToken();
            if (token == null) return;

            PlayerSetupData pData = token.PlayerData;
            if (pData.forcedNextMoveRoll <= 0)
            {
                if (bagFeedbackText != null)
                    bagFeedbackText.text = "Usa primero una Lupa o el hechizo Paso Ligero para elegir tus pasos exactos.";
                return;
            }

            SkillTreeAndAbilities.GetMovementRollParameters(pData, out _, out _, out int minStep, out int maxStep);
            pData.forcedNextMoveRoll = Mathf.Clamp(pData.forcedNextMoveRoll + delta, minStep, maxStep);
            RefreshBagAndMagicModal();
        }

        private void CastEquippedTurnSpellFromUI()
        {
            if (GameSessionManager.Instance == null) return;
            GameSessionManager.Instance.TryCastTurnSpellForCurrentPlayer(selectedTargetPlayerIdx, out string msg);
            if (bagFeedbackText != null) bagFeedbackText.text = msg;
            RefreshBagAndMagicModal();
            RefreshInGameHudPlayers();
        }

        private void RefreshBagAndMagicModal()
        {
            if (GameSessionManager.Instance == null || bagItemsContainer == null) return;
            PlayerOcaToken token = GameSessionManager.Instance.GetCurrentTurnToken();
            if (token == null) return;

            PlayerSetupData pData = token.PlayerData;

            if (bagHeaderText != null)
            {
                bagHeaderText.text = $"MOCHILA Y MAGIA — {pData.playerName.ToUpper()} (Monedas: {pData.coins} | Maná: {pData.mana})";
            }

            SpellDefinition spellDef = SpellsAndWildCardsSystem.GetSpellDefinition(pData.currentSpell);
            if (bagSpellInfoText != null)
            {
                bagSpellInfoText.text = spellDef != null
                    ? $"Hechizo: {spellDef.displayName} (Coste: {spellDef.manaCost} Maná) — {spellDef.description}"
                    : $"Hechizo Equipado: Ninguno (Cae en una Casilla de Hechizos para aprender uno) | Maná actual: {pData.mana}";
            }

            var tokens = GameSessionManager.Instance.ActiveTokens;
            if (bagTargetPlayerText != null && tokens.Count > 0)
            {
                selectedTargetPlayerIdx = Mathf.Clamp(selectedTargetPlayerIdx, 0, tokens.Count - 1);
                bagTargetPlayerText.text = tokens[selectedTargetPlayerIdx].PlayerData.playerName;
            }

            if (bagStepControlText != null)
            {
                if (pData.forcedNextMoveRoll > 0)
                {
                    SkillTreeAndAbilities.GetMovementRollParameters(pData, out _, out _, out int minS, out int maxS);
                    bagStepControlText.text = $"Pasos Elegidos: {pData.forcedNextMoveRoll} (Rango {minS}-{maxS})";
                }
                else
                {
                    bagStepControlText.text = "Pasos Fijos (Lupa/Paso Ligero): Inactivo";
                }
            }

            for (int i = bagItemsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(bagItemsContainer.GetChild(i).gameObject);
            }

            if (pData.inventory.Count == 0)
            {
                CreateText(bagItemsContainer, "EmptyMsg", "Tu mochila está vacía. Pasa por una Tienda para comprar objetos.",
                    18, FontStyle.Italic, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
                return;
            }

            foreach (ItemType itemType in pData.inventory)
            {
                ShopItemDefinition def = ShopAndItemsSystem.GetDefinition(itemType);
                if (def == null) continue;

                GameObject card = CreateBox(bagItemsContainer, $"InvItem_{itemType}", new Color(0.16f, 0.20f, 0.16f, 0.96f), Vector2.zero, Vector2.one);
                AddOutline(card, CreamButtonColor, 2f);

                CreateText(card.transform, "Title", def.displayName, 17, FontStyle.Bold,
                    CreamButtonColor, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.96f));

                CreateText(card.transform, "Desc", def.shortEffect, 13, FontStyle.Normal,
                    Color.white, TextAnchor.UpperLeft, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.68f));

                ItemType captured = itemType;
                CreateStyledButton(card.transform, "Btn_Use", def.usableInTurn ? "Usar Ahora" : "Uso en Combate",
                    new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.28f),
                    def.usableInTurn ? AccentGreenColor : new Color(0.4f, 0.4f, 0.4f), Color.white, 14, () =>
                    {
                        if (GameSessionManager.Instance != null)
                        {
                            GameSessionManager.Instance.TryUseTurnItemForCurrentPlayer(captured, selectedTargetPlayerIdx, out string msg);
                            if (bagFeedbackText != null) bagFeedbackText.text = msg;
                            RefreshBagAndMagicModal();
                            RefreshInGameHudPlayers();
                        }
                    });
            }
        }

        private void TryUseCombatItemFromUI(ItemType itemType)
        {
            if (CombatManager.Instance == null) return;
            CombatManager.Instance.TryUseCombatItem(itemType, out string msg);
            if (combatNarrationText != null) combatNarrationText.text = msg;
            RefreshInGameHudPlayers();
        }

        private void TryCastCombatSpellFromUI()
        {
            if (CombatManager.Instance == null) return;
            CombatManager.Instance.TryCastCombatSpell(out string msg);
            if (combatNarrationText != null) combatNarrationText.text = msg;
            RefreshInGameHudPlayers();
        }

        private void HandleCombatStarted(CombatEncounter encounter)
        {
            if (combatModalPanel == null || encounter == null) return;

            ShowInGameHUD();
            combatModalPanel.SetActive(true);

            if (rollDiceButton != null) rollDiceButton.interactable = false;

            // Configurar datos del Atacante
            PlayerSetupData attData = encounter.attackerToken.PlayerData;
            OcaCharacterDefinition attOca = GameSessionManager.Instance != null
                ? GameSessionManager.Instance.GetOcaDefinition(attData.selectedOca)
                : null;

            if (attackerPortraitImg != null && attOca != null) attackerPortraitImg.sprite = attOca.sprite;
            if (attackerNameText != null) attackerNameText.text = $"{attData.playerName} (Nivel {attData.expLevel})";
            if (attackerFormulaText != null) attackerFormulaText.text = $"Fuerza: {encounter.attackerFormulaText}";
            if (attackerRollResultText != null) attackerRollResultText.text = "Tirada: ?";

            // Configurar datos del Defensor (Boss o Jugador Rival)
            if (encounter.isBossCombat && encounter.targetBoss != null)
            {
                BossDefinition boss = encounter.targetBoss;
                if (combatHeaderTitleText != null)
                    combatHeaderTitleText.text = $"¡COMBATE CONTRA BOSS: {boss.displayName.ToUpper()}!";
                if (defenderPortraitImg != null)
                    defenderPortraitImg.sprite = boss.sprite;
                if (defenderNameText != null)
                    defenderNameText.text = $"{boss.displayName} (Casilla {boss.tileIndex})";
            }
            else if (encounter.defenderPlayerToken != null)
            {
                PlayerSetupData defData = encounter.defenderPlayerToken.PlayerData;
                OcaCharacterDefinition defOca = GameSessionManager.Instance != null
                    ? GameSessionManager.Instance.GetOcaDefinition(defData.selectedOca)
                    : null;

                if (combatHeaderTitleText != null)
                    combatHeaderTitleText.text = "¡DUELO ENTRE JUGADORES!";
                if (defenderPortraitImg != null && defOca != null)
                    defenderPortraitImg.sprite = defOca.sprite;
                if (defenderNameText != null)
                    defenderNameText.text = $"{defData.playerName} (Nivel {defData.expLevel})";
            }

            if (defenderFormulaText != null) defenderFormulaText.text = $"Fuerza: {encounter.defenderFormulaText}";
            if (defenderRollResultText != null) defenderRollResultText.text = "Tirada: ?";
            if (combatNarrationText != null) combatNarrationText.text = encounter.resultSummary;

            if (combatRollBtn != null) combatRollBtn.interactable = true;
            if (combatContinueBtn != null) combatContinueBtn.interactable = false;
        }

        private void HandleCombatRolled(CombatEncounter encounter)
        {
            if (encounter == null) return;

            if (attackerFormulaText != null)
                attackerFormulaText.text = $"Fuerza: {encounter.attackerFormulaText}";
            if (defenderFormulaText != null)
                defenderFormulaText.text = $"Fuerza: {encounter.defenderFormulaText}";

            if (attackerRollResultText != null && encounter.attackerRolls.Count > 0)
            {
                attackerRollResultText.text = "Tirada: " + CombatManager.FormatRolls(
                    encounter.attackerRolls,
                    encounter.attackerFormula.flatBonus,
                    encounter.attackerTotal
                );
            }

            if (defenderRollResultText != null && encounter.defenderRolls.Count > 0)
            {
                defenderRollResultText.text = "Tirada: " + CombatManager.FormatRolls(
                    encounter.defenderRolls,
                    encounter.defenderFormula.flatBonus,
                    encounter.defenderTotal
                );
            }

            if (combatNarrationText != null)
            {
                combatNarrationText.text = encounter.resultSummary;
            }

            if (combatRollBtn != null) combatRollBtn.interactable = !encounter.hasRolled || encounter.isTie;
            if (combatContinueBtn != null) combatContinueBtn.interactable = encounter.hasRolled && !encounter.isTie;
        }

        private void HandleCombatEnded(CombatEncounter encounter)
        {
            if (combatModalPanel != null) combatModalPanel.SetActive(false);
            if (rollDiceButton != null) rollDiceButton.interactable = true;
            RefreshInGameHudPlayers();
        }

        private void HandleTurnStateUpdated(int activePlayerIdx, int lastRoll, string message)
        {
            RefreshInGameHudPlayers();

            if (GameSessionManager.Instance != null &&
                activePlayerIdx >= 0 &&
                activePlayerIdx < GameSessionManager.Instance.ActiveTokens.Count)
            {
                var mgr = GameSessionManager.Instance;
                var token = mgr.ActiveTokens[activePlayerIdx];
                var pData = token.PlayerData;
                var ocaDef = mgr.GetOcaDefinition(pData.selectedOca);
                string ocaTitle = ocaDef != null ? ocaDef.displayName : pData.selectedOca.ToString();
                string[] moonNames = { "Luna Nueva (1/4)", "Cuarto Creciente (2/4)", "Gibosa (3/4)", "¡LUNA LLENA! (4/4)" };
                string moonStr = moonNames[Mathf.Clamp(mgr.MoonPhase, 0, 3)];

                if (hudTurnHeaderText != null)
                {
                    hudTurnHeaderText.text = $"Ronda {mgr.CurrentRound} [{moonStr}]  •  Turno: {pData.playerName} ({ocaTitle})";
                }

                // Actualizar Barra de Experiencia y Crecimiento en el HUD Inferior
                UpdateHudActivePlayerExpAndGrowthBar(pData);

                if (skillTreeBtnLabel != null)
                {
                    skillTreeBtnLabel.text = pData.unspentSkillPoints > 0
                        ? $"Árbol EXP (+{pData.unspentSkillPoints}!)"
                        : $"Árbol EXP (Nvl {pData.expLevel})";
                }

                if (bagSpellBtnLabel != null)
                {
                    bagSpellBtnLabel.text = $"Mochila ({pData.inventory.Count}) / Magia";
                }
            }

            if (hudStatusText != null && !string.IsNullOrEmpty(message))
            {
                hudStatusText.text = message;
            }
        }

        private void UpdateHudActivePlayerExpAndGrowthBar(PlayerSetupData pData)
        {
            if (pData == null) return;

            float normExp = SkillTreeAndAbilities.GetExpProgressNormalized(pData);
            if (hudExpBarFillRt != null)
            {
                hudExpBarFillRt.anchorMax = new Vector2(Mathf.Clamp01(normExp), 1f);
            }

            if (hudExpBarText != null)
            {
                string ptsTag = pData.unspentSkillPoints > 0 ? $"  [¡+{pData.unspentSkillPoints} Pto(s)!]" : "";
                hudExpBarText.text = $"{SkillTreeAndAbilities.GetExpBarDisplayText(pData)}{ptsTag}";
            }

            if (hudGrowthStatsText != null)
            {
                int unlocked = SkillTreeAndAbilities.GetTotalUnlockedSkillNodes(pData);
                float growthScale = SkillTreeAndAbilities.GetGrowthScaleMultiplier(pData);
                hudGrowthStatsText.text =
                    $"Crecimiento: {SkillTreeAndAbilities.GetPlayerGrowthSummary(pData)}  |  Nodos: {unlocked}/21  |  Tamaño Oca: {growthScale:F2}x";
            }
        }

        private void RefreshInGameHudPlayers()
        {
            if (hudPlayersBar == null || GameSessionManager.Instance == null) return;

            for (int i = hudPlayersBar.childCount - 1; i >= 0; i--)
            {
                Destroy(hudPlayersBar.GetChild(i).gameObject);
            }

            var tokens = GameSessionManager.Instance.ActiveTokens;
            int activeTurn = GameSessionManager.Instance.CurrentTurnPlayerIndex;

            if (activeTurn >= 0 && activeTurn < tokens.Count && tokens[activeTurn] != null)
            {
                UpdateHudActivePlayerExpAndGrowthBar(tokens[activeTurn].PlayerData);
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                PlayerOcaToken token = tokens[i];
                if (token == null) continue;

                PlayerSetupData pData = token.PlayerData;
                OcaCharacterDefinition ocaDef = GameSessionManager.Instance.GetOcaDefinition(pData.selectedOca);
                bool isCurrent = (i == activeTurn);

                Color cardCol = isCurrent ? new Color(0.24f, 0.36f, 0.22f, 0.98f) : new Color(0.14f, 0.17f, 0.14f, 0.92f);
                GameObject badge = CreateBox(hudPlayersBar, $"HudPlayer_{i + 1}", cardCol, Vector2.zero, Vector2.one);
                AddOutline(badge, isCurrent ? new Color(1f, 0.88f, 0.25f) : pData.tokenColor, isCurrent ? 3f : 2f);

                // Banda lateral del color del jugador
                CreateBox(badge.transform, "ColorStripe", pData.tokenColor, new Vector2(0f, 0f), new Vector2(0.05f, 1f));

                // Avatar de la Oca
                GameObject avatarObj = CreateBox(badge.transform, "Avatar", Color.white,
                    new Vector2(0.07f, 0.10f), new Vector2(0.26f, 0.90f));
                Image avatarImg = avatarObj.GetComponent<Image>();
                if (ocaDef != null) avatarImg.sprite = ocaDef.sprite;
                avatarImg.preserveAspect = true;

                // Nombre + Oca + Estado Zombi/Puntos de Habilidad
                string ocaLabel = ocaDef != null ? ocaDef.displayName : pData.selectedOca.ToString();
                string extraTag = pData.isZombie ? " [ZOMBI]" : "";
                if (pData.unspentSkillPoints > 0) extraTag += $" <color=#FFE045>[+{pData.unspentSkillPoints} Pto]</color>";

                CreateText(badge.transform, "Txt_Name", $"{pData.playerName}{extraTag}\n<size=11>{ocaLabel}</size>",
                    13, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(0.28f, 0.60f), new Vector2(0.98f, 0.96f));

                // Estadísticas: Monedas, Maná, Casilla
                string spellShort = pData.currentSpell != SpellType.None ? $" | {pData.currentSpell}" : "";
                string stats = $"Mon: {pData.coins} | Maná: {pData.mana} | Casilla: {token.CurrentTileIndex}{spellShort}";
                CreateText(badge.transform, "Txt_Stats", stats,
                    11, FontStyle.Normal, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleLeft,
                    new Vector2(0.28f, 0.32f), new Vector2(0.98f, 0.60f));

                // Barra Visual de Experiencia individual en cada tarjeta del HUD superior
                GameObject expBg = CreateBox(badge.transform, "Card_ExpBar_Bg", new Color(0.08f, 0.10f, 0.14f, 1f),
                    new Vector2(0.28f, 0.07f), new Vector2(0.96f, 0.30f));
                AddOutline(expBg, new Color(0.25f, 0.65f, 0.90f, 0.8f), 1f);

                float cardExpNorm = SkillTreeAndAbilities.GetExpProgressNormalized(pData);
                CreateBox(expBg.transform, "Card_ExpBar_Fill", new Color(0.18f, 0.65f, 0.92f, 0.95f),
                    new Vector2(0f, 0f), new Vector2(Mathf.Clamp01(cardExpNorm), 1f));

                CreateText(expBg.transform, "Card_ExpBar_Txt", SkillTreeAndAbilities.GetExpBarDisplayText(pData),
                    10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            }
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================
        // UTILIDADES DE CONSTRUCCIÓN DE ELEMENTOS UGUI
        // =====================================================================

        private GameObject CreatePanel(Transform parent, string name, Color bgColor)
        {
            return CreateBox(parent, name, bgColor, Vector2.zero, Vector2.one);
        }

        private GameObject CreateBox(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = obj.AddComponent<Image>();
            img.color = color;
            return obj;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style,
            Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Text txt = obj.AddComponent<Text>();
            txt.font = defaultFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        /// <summary>
        /// Crea un botón con la estética crema y borde oscuro del mockup de la página 24 del TP2.
        /// </summary>
        private Button CreateMockupMenuButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            return CreateStyledButton(parent, name, label, anchorMin, anchorMax,
                CreamButtonColor, DarkBorderColor, 28, onClick);
        }

        private Button CreateStyledButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor, int fontSize,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreateBox(parent, name, bgColor, anchorMin, anchorMax);
            AddOutline(btnObj, DarkBorderColor, 3f);

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;

            CreateText(btnObj.transform, "Label", label, fontSize, FontStyle.Bold,
                textColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }
            return btn;
        }

        private InputField CreateInputField(Transform parent, string name, string initialText,
            Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction<string> onValueChanged)
        {
            GameObject root = CreateBox(parent, name, new Color(0.10f, 0.12f, 0.10f, 1f), anchorMin, anchorMax);
            AddOutline(root, CreamButtonColor, 1.5f);

            Text textComp = CreateText(root.transform, "Text", initialText, 18, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));
            textComp.supportRichText = false;

            Text placeholder = CreateText(root.transform, "Placeholder", "Nombre del jugador...", 16, FontStyle.Italic,
                new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f));

            InputField input = root.AddComponent<InputField>();
            input.textComponent = textComp;
            input.placeholder = placeholder;
            input.text = initialText;
            input.characterLimit = 18;

            if (onValueChanged != null)
            {
                input.onValueChanged.AddListener(onValueChanged);
            }
            return input;
        }

        private void CreateSliderRow(Transform parent, string label, float yMin, float minVal, float maxVal,
            out Slider sliderOut, out Text valueTextOut, UnityEngine.Events.UnityAction<float> onChanged)
        {
            float yMax = yMin + 0.13f;
            CreateText(parent, $"Lbl_{label}", label, 19, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.06f, yMin + 0.05f), new Vector2(0.72f, yMax));

            valueTextOut = CreateText(parent, $"Val_{label}", "", 18, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleRight, new Vector2(0.72f, yMin + 0.05f), new Vector2(0.94f, yMax));

            GameObject sliderObj = CreateBox(parent, $"Slider_{label}", new Color(0.08f, 0.08f, 0.08f, 1f),
                new Vector2(0.06f, yMin), new Vector2(0.94f, yMin + 0.05f));

            GameObject fillArea = CreateBox(sliderObj.transform, "Fill Area", Color.clear,
                new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.85f));
            GameObject fill = CreateBox(fillArea.transform, "Fill", AccentGreenColor, Vector2.zero, Vector2.one);

            GameObject handleArea = CreateBox(sliderObj.transform, "Handle Slide Area", Color.clear,
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f));
            GameObject handle = CreateBox(handleArea.transform, "Handle", CreamButtonColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f));
            RectTransform handleRt = handle.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(22f, 0f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();

            if (onChanged != null)
            {
                slider.onValueChanged.AddListener(onChanged);
            }
            sliderOut = slider;
        }

        private Toggle CreateToggleRow(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
            UnityEngine.Events.UnityAction<bool> onChanged)
        {
            GameObject row = CreateBox(parent, $"Toggle_{label}", new Color(0.18f, 0.22f, 0.18f, 0.8f), anchorMin, anchorMax);
            AddOutline(row, DarkBorderColor, 1.5f);

            GameObject checkBg = CreateBox(row.transform, "Background", new Color(0.10f, 0.10f, 0.10f, 1f),
                new Vector2(0.04f, 0.18f), new Vector2(0.13f, 0.82f));
            AddOutline(checkBg, CreamButtonColor, 1.5f);

            GameObject checkmark = CreateBox(checkBg.transform, "Checkmark", AccentGreenColor,
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f));

            CreateText(row.transform, "Label", label, 18, FontStyle.Bold,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.16f, 0.05f), new Vector2(0.96f, 0.95f));

            Toggle toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = checkBg.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = true;

            if (onChanged != null)
            {
                toggle.onValueChanged.AddListener(onChanged);
            }
            return toggle;
        }

        private static void AddOutline(GameObject target, Color color, float distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        private static void AddShadow(GameObject target, Color color, Vector2 distance)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }
    }
}
