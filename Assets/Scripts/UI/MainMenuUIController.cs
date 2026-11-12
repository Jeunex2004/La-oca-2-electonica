using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif
using Oca2.Combat;
using Oca2.Core;
using Oca2.Data;
using Oca2.Players;

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
        private Button rollDiceButton;

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

            // Barra inferior de estado y botones de acción de turno
            GameObject bottomBar = CreateBox(inGameHudPanel.transform, "Bottom_ActionBar", new Color(0.08f, 0.11f, 0.08f, 0.90f),
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.12f));
            AddOutline(bottomBar, DarkBorderColor, 2f);

            hudTurnHeaderText = CreateText(bottomBar.transform, "Txt_TurnHeader", "Turno: Jugador 1", 22, FontStyle.Bold,
                new Color(1f, 0.88f, 0.35f), TextAnchor.MiddleLeft, new Vector2(0.02f, 0.52f), new Vector2(0.55f, 0.96f));

            hudStatusText = CreateText(bottomBar.transform, "Txt_Status", "Listo para tirar el dado (1d6).", 19, FontStyle.Normal,
                Color.white, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.05f), new Vector2(0.58f, 0.52f));

            rollDiceButton = CreateStyledButton(bottomBar.transform, "Btn_RollDice", "Tirar Dado (1d6)",
                new Vector2(0.60f, 0.12f), new Vector2(0.76f, 0.88f),
                AccentGreenColor, Color.white, 22, () =>
                {
                    if (GameSessionManager.Instance != null)
                    {
                        GameSessionManager.Instance.RollDiceForCurrentPlayer();
                    }
                });

            CreateMockupMenuButton(bottomBar.transform, "Btn_InGameOptions", "Opciones",
                new Vector2(0.77f, 0.12f), new Vector2(0.87f, 0.88f), ShowOptions);

            CreateMockupMenuButton(bottomBar.transform, "Btn_ReturnMenu", "Menú Principal",
                new Vector2(0.88f, 0.12f), new Vector2(0.99f, 0.88f), () =>
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

            BuildCombatModalPanel(inGameHudPanel.transform);
        }

        // ---------------------------------------------------------------------
        // 6. VENTANA MODAL DE COMBATE (Bosses: La Foca, La Roca, La Trollface y PvP)
        // ---------------------------------------------------------------------
        private void BuildCombatModalPanel(Transform parent)
        {
            combatModalPanel = CreatePanel(parent, "Combat_ModalOverlay", new Color(0.03f, 0.02f, 0.02f, 0.82f));

            GameObject arenaWindow = CreateBox(combatModalPanel.transform, "Combat_ArenaWindow", new Color(0.16f, 0.12f, 0.12f, 0.98f),
                new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f));
            AddOutline(arenaWindow, new Color(0.85f, 0.18f, 0.18f), 4f);

            // Cabecera del combate
            GameObject headerBar = CreateBox(arenaWindow.transform, "Header", new Color(0.55f, 0.08f, 0.08f, 1f),
                new Vector2(0f, 0.86f), new Vector2(1f, 1f));
            combatHeaderTitleText = CreateText(headerBar.transform, "Txt_CombatTitle", "¡ENCUENTRO DE COMBATE!", 36, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AddShadow(combatHeaderTitleText.gameObject, Color.black, new Vector2(3f, -3f));

            // Tarjeta Izquierda: Jugador Atacante
            GameObject leftCard = CreateBox(arenaWindow.transform, "Attacker_Card", new Color(0.12f, 0.20f, 0.14f, 0.96f),
                new Vector2(0.04f, 0.32f), new Vector2(0.43f, 0.83f));
            AddOutline(leftCard, AccentGreenColor, 3f);

            GameObject attPortraitBg = CreateBox(leftCard.transform, "Portrait_Bg", new Color(0.95f, 0.94f, 0.88f, 1f),
                new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.94f));
            AddOutline(attPortraitBg, DarkBorderColor, 2f);
            GameObject attImgObj = CreateBox(attPortraitBg.transform, "Img", Color.white,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            attackerPortraitImg = attImgObj.GetComponent<Image>();
            attackerPortraitImg.preserveAspect = true;

            attackerNameText = CreateText(leftCard.transform, "Txt_AttName", "Jugador 1", 24, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.42f));

            attackerFormulaText = CreateText(leftCard.transform, "Txt_AttFormula", "Fuerza de Combate: 1d6", 20, FontStyle.Bold,
                new Color(0.75f, 0.95f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.30f));

            attackerRollResultText = CreateText(leftCard.transform, "Txt_AttRoll", "Tirada: —", 24, FontStyle.Bold,
                new Color(1f, 0.90f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.17f));

            // Emblema central VS
            Text vsText = CreateText(arenaWindow.transform, "Txt_VS", "VS", 58, FontStyle.BoldAndItalic,
                TitleRedColor, TextAnchor.MiddleCenter, new Vector2(0.43f, 0.48f), new Vector2(0.57f, 0.70f));
            AddShadow(vsText.gameObject, Color.black, new Vector2(3f, -3f));

            // Tarjeta Derecha: Defensor (Boss u Oca Rival)
            GameObject rightCard = CreateBox(arenaWindow.transform, "Defender_Card", new Color(0.24f, 0.12f, 0.12f, 0.96f),
                new Vector2(0.57f, 0.32f), new Vector2(0.96f, 0.83f));
            AddOutline(rightCard, TitleRedColor, 3f);

            GameObject defPortraitBg = CreateBox(rightCard.transform, "Portrait_Bg", new Color(0.95f, 0.94f, 0.88f, 1f),
                new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.94f));
            AddOutline(defPortraitBg, DarkBorderColor, 2f);
            GameObject defImgObj = CreateBox(defPortraitBg.transform, "Img", Color.white,
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            defenderPortraitImg = defImgObj.GetComponent<Image>();
            defenderPortraitImg.preserveAspect = true;

            defenderNameText = CreateText(rightCard.transform, "Txt_DefName", "La Foca", 24, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.42f));

            defenderFormulaText = CreateText(rightCard.transform, "Txt_DefFormula", "Fuerza de Combate: 1d8", 20, FontStyle.Bold,
                new Color(1f, 0.75f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.30f));

            defenderRollResultText = CreateText(rightCard.transform, "Txt_DefRoll", "Tirada: —", 24, FontStyle.Bold,
                new Color(1f, 0.90f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.17f));

            // Narración / Resultado del Combate
            GameObject narrationBox = CreateBox(arenaWindow.transform, "Narration_Box", new Color(0.09f, 0.09f, 0.09f, 0.95f),
                new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.30f));
            AddOutline(narrationBox, DarkBorderColor, 2f);

            combatNarrationText = CreateText(narrationBox.transform, "Txt_Narration",
                "¡Lanza los dados de combate! El ganador recibe +3 de EXP y el perdedor muere.", 20, FontStyle.Bold,
                CreamButtonColor, TextAnchor.MiddleCenter, new Vector2(0.03f, 0.05f), new Vector2(0.97f, 0.95f));

            // Botones de acción de combate
            combatRollBtn = CreateStyledButton(arenaWindow.transform, "Btn_CombatRoll", "¡LANZAR DADOS DE COMBATE!",
                new Vector2(0.16f, 0.03f), new Vector2(0.56f, 0.13f),
                TitleRedColor, Color.white, 24, () =>
                {
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.RollActiveCombat();
                    }
                });

            combatContinueBtn = CreateStyledButton(arenaWindow.transform, "Btn_CombatContinue", "Continuar Partida",
                new Vector2(0.60f, 0.03f), new Vector2(0.88f, 0.13f),
                AccentGreenColor, Color.white, 24, () =>
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
            ShowMainMenu();
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
            if (attackerFormulaText != null) attackerFormulaText.text = $"Fuerza: {encounter.attackerFormula}";
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

            if (defenderFormulaText != null) defenderFormulaText.text = $"Fuerza: {encounter.defenderFormula}";
            if (defenderRollResultText != null) defenderRollResultText.text = "Tirada: ?";
            if (combatNarrationText != null) combatNarrationText.text = encounter.resultSummary;

            if (combatRollBtn != null) combatRollBtn.interactable = true;
            if (combatContinueBtn != null) combatContinueBtn.interactable = false;
        }

        private void HandleCombatRolled(CombatEncounter encounter)
        {
            if (encounter == null) return;

            if (attackerRollResultText != null)
            {
                attackerRollResultText.text = "Tirada: " + CombatManager.FormatRolls(
                    encounter.attackerRolls,
                    encounter.attackerFormula.flatBonus,
                    encounter.attackerTotal
                );
            }

            if (defenderRollResultText != null)
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

            // Si hubo empate, permitir volver a tirar; si ya hay ganador, habilitar Continuar
            if (combatRollBtn != null) combatRollBtn.interactable = encounter.isTie;
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
                var token = GameSessionManager.Instance.ActiveTokens[activePlayerIdx];
                var ocaDef = GameSessionManager.Instance.GetOcaDefinition(token.PlayerData.selectedOca);
                string ocaTitle = ocaDef != null ? ocaDef.displayName : token.PlayerData.selectedOca.ToString();
                if (hudTurnHeaderText != null)
                {
                    hudTurnHeaderText.text = $"Ronda {GameSessionManager.Instance.CurrentRound}  •  Turno de: {token.PlayerData.playerName} ({ocaTitle})";
                }
            }

            if (hudStatusText != null && !string.IsNullOrEmpty(message))
            {
                hudStatusText.text = message;
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
                    new Vector2(0.07f, 0.10f), new Vector2(0.28f, 0.90f));
                Image avatarImg = avatarObj.GetComponent<Image>();
                if (ocaDef != null) avatarImg.sprite = ocaDef.sprite;
                avatarImg.preserveAspect = true;

                // Nombre + Oca
                string ocaLabel = ocaDef != null ? ocaDef.displayName : pData.selectedOca.ToString();
                CreateText(badge.transform, "Txt_Name", $"{pData.playerName}\n<size=12>{ocaLabel}</size>",
                    15, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft,
                    new Vector2(0.30f, 0.45f), new Vector2(0.98f, 0.95f));

                // Estadísticas: Monedas, EXP, Casilla
                string stats = $"Monedas: {pData.coins} | EXP Nvl: {pData.expLevel} | Casilla: {token.CurrentTileIndex}";
                CreateText(badge.transform, "Txt_Stats", stats,
                    13, FontStyle.Normal, new Color(1f, 0.92f, 0.55f), TextAnchor.MiddleLeft,
                    new Vector2(0.30f, 0.05f), new Vector2(0.98f, 0.45f));
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
