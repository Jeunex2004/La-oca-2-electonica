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

### 2. Mundo 3D, Tableros y Sistema de Cámara (Etapa 1)

- **4 Tableros 3D (`BoardManager.cs`, `BoardPath.cs`, `BoardTile.cs`, `TileType.cs`):**
  - **Tablero Principal (`MainBoard`):** Camino en espiral de 64 posiciones (`0 = START` a `63 = Meta`) con casillas tipadas (*Oca*, *Puente*, *Muerte*, *Dinero*, *Experiencia*, *Tienda*, *Hechizos*, *Cartas Salvajes*, *Boss*, *Zombi*).
  - **Tablero del Infierno 1 (`Hell1`):** Tablero secundario para jugadores que mueren en el tablero principal.
  - **Tablero del Infierno 2 (`Hell2`):** Tablero en S al que van los jugadores que mueren estando en el Infierno 1 (al salir vuelven como *Zombis* inmortales).
  - **Tablero del Payaso (`ClownWheel`):** Rueda especial de sectores *NO* y *EXIT*.
- **Cámara Isométrica 3D (`BoardCameraController.cs`):**
  - Rotación cardinal de 90° con las teclas `A` y `D`.
  - Cambio de vista entre los 4 tableros con `Flecha Izquierda` y `Flecha Derecha`.
  - Zoom con la rueda del ratón, paneo con clic derecho/central y enfoque general con `Espacio`.

---

## Estructura de Scripts del Proyecto

```text
Assets/Scripts/
├── Board/
│   ├── BoardManager.cs          # Gestor central de los 4 tableros y cambio de vista
│   ├── BoardPath.cs             # Secuencia de casillas y cálculo de avance con rebote en la 63
│   ├── BoardTile.cs             # Casilla individual, conexiones (Oca/Puente) y Gizmos en Editor
│   └── TileType.cs              # Enumeraciones de tipos de casilla (TileType) y tableros (BoardType)
├── Camera/
│   └── BoardCameraController.cs # Cámara isométrica 3D con rotación cardinal, zoom, paneo y órbita en menú
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
