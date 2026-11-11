namespace Oca2.Board
{
    /// <summary>
    /// Tipos de casillas disponibles según el Documento de Diseño de La Oca 2: Electric Boogaloo.
    /// </summary>
    public enum TileType
    {
        Start = 0,          // Casilla de inicio
        Normal = 1,         // Casilla estándar sin evento especial
        Oca = 2,            // De oca a oca: avanza a la siguiente oca y repite tirada (la 59 lleva a la 63)
        Puente = 3,         // De puente a puente: salta al otro puente y repite tirada
        Muerte = 4,         // Calavera: el jugador muere (va a Infierno 1, o a Infierno 2 si ya estaba en el infierno)
        Dinero = 5,         // Recibe 1 de Oro (o más con mejoras)
        Experiencia = 6,    // Recibe 1 de EXP
        Tienda = 7,         // Permite comprar en la tienda
        Hechizos = 8,       // Obtiene 1 hechizo
        CartasSalvajes = 9, // Roba una carta salvaje
        Boss = 10,          // Casilla de jefe (La Foca, La Roca, La Trollface)
        BobEsponja = 11,    // Bob Esponja triste: sin efecto
        Zombi = 12,         // Se convierte en zombi (inmortal)
        Meta = 13           // Casilla 63 final: requiere caer exacto para ganar
    }

    /// <summary>
    /// Los distintos mundos / tableros del juego.
    /// </summary>
    public enum BoardType
    {
        MainBoard = 0,      // Tablero del mundo principal (63 casillas)
        Hell1 = 1,          // Tablero del infierno 1 (4 círculos concéntricos)
        Hell2 = 2,          // Tablero del infierno 2 (camino en S de Bob Esponjas)
        ClownWheel = 3      // Tablero del payaso (rueda con sectores NO y EXIT)
    }
}
