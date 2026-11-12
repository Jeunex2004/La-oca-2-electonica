# La Oca 2: Electric Boogaloo

**Universidad de la Cuenca del Plata (UCP)** — Facultad de Ingeniería, Tecnología y Arquitectura  
**Carrera:** Ingeniería en Sistemas de Información  
**Asignatura:** Diseño y Desarrollo de Videojuegos (Año 2026) — **Trabajo Práctico 2 (AE2)**  

### Equipo de Desarrollo
- **Alfonzo Enzo** — *Programador*
- **Evin Linder Jeuel Elias Natanael** — *Encargado de las ideas y mecánicas*

---

## Descripción General

**La Oca 2: Electric Boogaloo** es una reinterpretación competitiva y estratégica en 3D del clásico juego de la Oca, pensada para **1 a 8 jugadores** en PC. El objetivo principal es recorrer las **63 casillas** del tablero principal y caer exactamente en la casilla final (63), administrando monedas, experiencia (hasta nivel 22), maná, hechizos, tiendas, cartas salvajes, los 3 jefes (*La Foca*, *La Roca* y *La Trollface*) y los tableros alternativos (*Infierno 1*, *Infierno 2* y *Tablero del Payaso*).

---

## Características Implementadas

### 1. Menú Principal, Opciones de Juego y Selección de Jugadores (Etapa 2)

Implementado siguiendo el diseño de **Interfaz y Experiencia de Usuario (UI/UX)** y las reglas de preparación del documento **TP2 (AE2)**:

#### Menú Principal (`MainMenuUIController.cs`)
- **Diseño fiel al mockup del GDD (pág. 24):** Título principal **LA OCA 2** (*Electric Boogaloo*), galería de presentación con las 6 Ocas competidoras y los 4 botones principales:
  - **Nuevo juego:** Abre la pantalla de configuración de partida y selección de jugadores/ocas.
  - **Opciones:** Abre el panel de configuración de audio, pantalla, cámara y reglas.
  - **Créditos:** Muestra la información académica y los roles del equipo de desarrollo.
  - **Salir:** Cierra la aplicación (o detiene el modo *Play* en el Editor de Unity).
- **Cámara de fondo en menú:** Mientras el usuario navega por los menús, la cámara realiza una órbita suave alrededor del tablero 3D.

#### Selección de Jugadores y Ocas (`PlayerSelectionPanel`)
- **Soporte de 1 a 8 jugadores (pág. 3 y 27):** Selector dinámico `- / +` para configurar entre 1 jugador (modo solitario) y 8 jugadores, con cálculo automático de la duración estimada de la sesión.
- **Elección de las 6 Ocas oficiales (pág. 5):**
  1. **La Oca Default** (`La oca default.png`)
  2. **La Ocarina** (`La ocarina.png`)
  3. **La Oca de Boca** (`La oca de boca.png`)
  4. **La Loca** (`La loca.png`)
  5. **La Barroca** (`La barroca.png`)
  6. **El Xoca** (`El xoca.png`)
- **Personalización por jugador:** Cada tarjeta de jugador permite editar el **nombre del participante**, elegir su **Oca** con vista previa y descripción, y cambiar el **color de la base** de su ficha 3D.
- **Preparación inicial reglamentaria (pág. 5):** Al iniciar la partida, cada jugador recibe automáticamente **3 monedas** y su **tabla de experiencia en Nivel 1**.

#### Opciones de Juego (`GameOptionsData` / `OptionsPanel`)
- **Audio:** Control de *Volumen General*, *Volumen de Música* (pensado para pistas sin copyright según pág. 24) y *Volumen de Efectos (SFX)*.
- **Video y Cámara:** Toggle de *Pantalla Completa*, selector de *Calidad Gráfica* y ajuste de *Velocidad de Cámara* (`0.5x` a `2.5x`).
- **Reglas de Partida (TP2):** Configuración de *Monedas iniciales* (por defecto `3`), *Nivel de EXP inicial* (por defecto `1`, máximo `22`), *Caída exacta en Casilla 63 con rebote* y activación de los *3 Bosses del tablero*.
- **Persistencia:** Todas las preferencias se guardan y cargan automáticamente mediante `PlayerPrefs`.

#### Fichas 3D de Jugadores y HUD en Partida (`PlayerOcaToken.cs` y `GameSessionManager.cs`)
- **Instanciación en casilla START:** Al pulsar **¡Comenzar Partida!**, se generan las fichas 3D de todos los jugadores seleccionados sobre la casilla `START` (`Tile_00_Start`) del Tablero Principal, organizadas circularmente para evitar superposiciones.
- **Billboard 2D en entorno 3D:** Cada ficha combina una base cilíndrica 3D con el color del jugador y el sprite 2D de su Oca orientado hacia la cámara mediante `OcaBillboard`.
- **HUD de Partida y Turnos:** Barra superior con el estado en vivo de cada jugador (*Avatar de Oca*, *Nombre*, *Monedas*, *Nivel de EXP*, *Casilla actual*) y barra inferior con botón de **Tirar Dado (1d6)**, resolución de casillas especiales (*Oca a Oca*, *Puente a Puente*, *Dinero*, *Experiencia*, *Zombi*, *Muerte -> Infierno*, *Meta 63*), acceso a **Opciones** y retorno al **Menú Principal** (`ESC`).

---

### 2. Sistema de Combate y los 3 Bosses (Etapa 3)

Implementado conforme a las reglas de las páginas 6, 8, 17, 18 y 27 del documento **TP2 (AE2)**:

#### Los 3 Bosses del Tablero Principal (`CombatManager.cs`, `BossBoardToken.cs`, `CombatData.cs`)
- **Instanciación 3D en el Tablero:** Al comenzar la partida, los 3 Bosses aparecen físicamente sobre sus casillas rojas del Tablero Principal con un pedestal carmesí y su sprite orientado hacia la cámara con `OcaBillboard`:
  1. **Casilla 13 — La Foca** (`La foca.png`): Fuerza de combate **`1d8`** (1 dado de 8 caras).
  2. **Casilla 35 — La Roca** (`La roca.png`): Fuerza de combate **`2d6`** (2 dados de 6 caras).
  3. **Casilla 53 — La Trollface** (`Trollface.png`): Fuerza de combate **`3d6`** (3 dados de 6 caras).
- **Intercepción al pasar por encima (`GetBossInterceptOnPath`):** Si un jugador avanza con su tirada y pasa por encima (o cae) en la casilla de un Boss vivo, su movimiento se detiene en la casilla del Boss y se abre el duelo obligatorio. Si el jugador salta por encima usando las casillas *De Oca a Oca* (ej. de la 9 a la 14, de la 32 a la 36, o de la 50 a la 54), logra saltear al Boss como indica la pág. 4 y 27 del GDD.

#### Combate entre Jugadores (PvP) y Resolución por Dados
- **Duelos PvP (`GetPvPOpponentOnTile`):** Si un jugador termina su movimiento en una casilla ocupada por otro jugador (excepto en `START` y en el `Infierno 2`, donde no hay combates según la pág. 11), se inicia un combate directo entre ambos.
- **Ventana Modal de Combate (`MainMenuUIController.cs`):** Muestra cara a cara las cartas del Atacante y del Defensor (Boss u Oca rival), sus fórmulas de fuerza (`1d6` base para las Ocas + bonos por nivel), el desglose de cada dado lanzado, repetición automática en caso de empate y aplicación del resultado:
  - **Ganador:** Recibe **+3 de Experiencia** (subiendo de nivel automáticamente según la progresión de nivel 1 a 22). Si era contra un Boss, el Boss queda derrotado en el tablero.
  - **Perdedor:** Muere y es enviado al **Tablero del Infierno 1** (o al **Infierno 2** si ya estaba en el Infierno 1), salvo que sea **Zombi** (inmortal).

---

### 3. Mapeo Exacto de Casillas y Coordenadas UV en los 4 Tableros (Etapa 4)

Implementado en [`BoardLayoutBuilder.cs`](Assets/Scripts/Board/BoardLayoutBuilder.cs), [`BoardPath.cs`](Assets/Scripts/Board/BoardPath.cs) y [`BoardManager.cs`](Assets/Scripts/Board/BoardManager.cs) para que todas las casillas coincidan exactamente con las texturas oficiales (`Tablero principal.png`, `Tablero infierno 1.png`, `Tablero infierno 2.png` y `Tablero payaso.png`):

- **Tablero Principal (`MainBoard` — 64 casillas: `0 = START` a `63 = Meta`):**
  - Coordenadas UV mapeadas sobre la cara superior del tablero (incluyendo las casillas dobles en diagonal de las esquinas `[8,9]`, `[18,19]`, `[28,29]`, `[36,37]`, `[44,45]`, `[50,51]`, `[56,57]` y `[60,61]`).
  - **Ocas conectadas (`SpecialDestinationTile`):** `1 -> 5 -> 9 -> 14 -> 18 -> 23 -> 27 -> 32 -> 36 -> 41 -> 45 -> 50 -> 54 -> 59 -> 63 (Meta)`.
  - **Puentes conectados:** `4 <-> 12`.
  - **Calaveras (Muerte):** Casillas `2`, `58` y `60`.
  - **Tiendas:** Casillas `11`, `34` y `46`.
  - **Bosses:** Casillas `13` (*La Foca*), `35` (*La Roca*) y `53` (*La Trollface*).
- **Tablero del Infierno 1 (`Hell1` — 42 casillas):**
  - `0 = START` (con entrada a la casilla `6`), **4 filas cíclicas horizontales de 10 casillas cada una** (`1..10` de *Dinero*, `11..20` de *Hechizos* y *Cartas Salvajes*, `21..30` de *Experiencia*, `31..40` de *Dinero* y *Tiendas*) y `41 = EXIT`.
  - Enlaces de **flechas rojas ascendentes** entre niveles (`4->14`, `7->17`, `10->20`; `14->24`, `16->26`, `18->28`; `22->32`, `25->35`, `27->37`; `35->41`, `36->41`). Al llegar a `EXIT` (`41`), el jugador regresa a `START` (`0`) del Tablero Principal.
- **Tablero del Infierno 2 (`Hell2` — 17 casillas en curva S):**
  - `0 = START`, `1..14 = BobEsponja` (*"No pasa nada :)"*), `15 = Zombi` y `16 = EXIT`. Al llegar o pasar por `15`/`16` (sin requerir tirada exacta), el jugador se transforma en **Zombi inmortal** y regresa a `START` (`0`) del Tablero Principal.
- **Tablero del Payaso (`ClownWheel` — 6 sectores circulares):**
  - `0 = EXIT` (sector verde inferior) y `1..5 = NO` dispuestos en bucle circular cerrado (`0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 0`). Al caer exactamente en `0` (`EXIT`), el jugador escapa al `START` del Tablero Principal.

---

### 4. Mundo 3D y Sistema de Cámara (Etapa 1)

- **Cámara Isométrica 3D (`BoardCameraController.cs`):**
  - Rotación cardinal de 90° con las teclas `A` y `D`.
  - Cambio de vista entre los 4 tableros con `Flecha Izquierda` y `Flecha Derecha` (y cambio automático al tablero del jugador activo en su turno).
  - Zoom con la rueda del ratón, paneo con clic derecho/central y enfoque general con `Espacio`.

---

## Estructura de Scripts del Proyecto

```text
Assets/Scripts/
├── Board/
│   ├── BoardLayoutBuilder.cs    # Coordenadas UV exactas, tipos y enlaces de los 4 tableros (64, 42, 17 y 6 casillas)
│   ├── BoardManager.cs          # Gestor central de los 4 tableros, sincronización automática y cambio de vista
│   ├── BoardPath.cs             # Trazado paso a paso (rebote en 63, anillos en Hell1, curva S en Hell2 y rueda en Payaso)
│   ├── BoardTile.cs             # Casilla individual, conexiones (Oca/Puente/Flechas Rojas) y Gizmos en Editor
│   └── TileType.cs              # Enumeraciones de tipos de casilla (TileType) y tableros (BoardType)
├── Camera/
│   └── BoardCameraController.cs # Cámara isométrica 3D con rotación cardinal, zoom, paneo y órbita en menú
├── Combat/
│   ├── BossBoardToken.cs        # Figura 3D de cada Boss (La Foca, La Roca, La Trollface) sobre el tablero
│   ├── CombatData.cs            # Estructuras DiceFormula, BossDefinition y CombatEncounter
│   └── CombatManager.cs         # Lógica de intercepción de Bosses, combates PvP y tiradas de fuerza
├── Core/
│   └── GameSessionManager.cs    # Singleton de sesión: catálogo de 6 Ocas, opciones, turnos y spawn de jugadores
├── Data/
│   └── OcaGameData.cs           # Modelos de datos: OcaType, OcaCharacterDefinition, PlayerSetupData, GameOptionsData
├── Editor/
│   ├── BoardSetupUtility.cs     # Herramienta Editor: construcción automática de tableros y cámara
│   └── MenuSetupUtility.cs      # Herramienta Editor: configuración de sprites de Ocas y escena de menú
├── OCAS/
│   ├── OcaBillboard.cs          # Mantiene los sprites de las Ocas mirando hacia la cámara principal
│   └── PlayerOcaToken.cs        # Ficha 3D de jugador, base coloreada, sprite de Oca y animación de salto
└── UI/
    └── MainMenuUIController.cs  # Interfaz UGUI: Menú Principal, Selección de Jugadores (1-8), Opciones, Créditos y HUD
```

---

## Cómo Ejecutar y Configurar en Unity

1. Abrir el proyecto en **Unity 6 (6000.x)**.
2. Abrir cualquiera de las escenas en `Assets/Scenes/` (`SceneMenu.unity` o `SampleScene.unity`) y presionar **Play**: el sistema detecta e inicializa automáticamente el Menú Principal, las 6 Ocas, las Opciones y la Selección de Jugadores.
3. *(Opcional en el Editor)* Desde la barra superior de Unity puedes usar las herramientas automatizadas:
   - `Tools > La Oca 2 > Construir Tableros y Configurar Cámara`
   - `Tools > La Oca 2 > Configurar Menú Principal y Selección de Jugadores`
