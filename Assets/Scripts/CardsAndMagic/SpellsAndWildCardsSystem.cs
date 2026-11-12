using System;
using System.Collections.Generic;
using UnityEngine;
using Oca2.Board;
using Oca2.Data;
using Oca2.Players;
using Oca2.Progression;

namespace Oca2.CardsAndMagic
{
    [Serializable]
    public class SpellDefinition
    {
        public SpellType spellType;
        public string displayName;
        public int manaCost;
        public bool isCombatSpell;
        public bool oncePerCombat;
        public bool requiresTargetPlayer;
        public string description;
    }

    public enum WildCardEffectType
    {
        PlayerDies,
        AllPlayersDie,
        GoToClownBoard,
        UnoDuelForCoin,
        TouchGrassGainExp,
        TellAJokeOrStart,
        ClearNegativeEffect,
        FreeCrucifijo,
        FreeRedBull,
        FreeGuanteDuelo,
        FreeLupa,
        FreeCartaUNOReverse,
        JinxAnotherPlayer,
        NothingHappens,
        CurseSayCuac,
        CurseNoLetterE,
        Advance1d6,
        Advance1d8,
        Advance1d20,
        Retreat1d6,
        Retreat1d8,
        Retreat1d20,
        SwapPositionWithPlayer,
        CurseOneWordOnly,
        RedistributeWealthAndMana,
        ExtraTurnObo,
        WordBetOneCoin,
        Roll1d20AdvanceOrRetreat,
        SkipNextTurn
    }

    [Serializable]
    public class WildCardDefinition
    {
        public WildCardEffectType effectType;
        public string title;
        public string description;
        public bool isNegative;
    }

    /// <summary>
    /// Gestiona los 13 Hechizos de "Hechizos.jpg" (Reglas pág. 30-31) y el Mazo de Cartas Salvajes
    /// de "Wild Cards.jpg" (Reglas pág. 13).
    /// </summary>
    public static class SpellsAndWildCardsSystem
    {
        private static List<SpellDefinition> spellCatalog;
        private static List<WildCardDefinition> wildCardDeck;

        public static IReadOnlyList<SpellDefinition> SpellCatalog
        {
            get
            {
                if (spellCatalog == null || spellCatalog.Count == 0)
                {
                    InitializeSpells();
                }
                return spellCatalog;
            }
        }

        public static IReadOnlyList<WildCardDefinition> WildCardDeck
        {
            get
            {
                if (wildCardDeck == null || wildCardDeck.Count == 0)
                {
                    InitializeWildCards();
                }
                return wildCardDeck;
            }
        }

        public static void InitializeSpells()
        {
            spellCatalog = new List<SpellDefinition>
            {
                new SpellDefinition
                {
                    spellType = SpellType.Teletransporte,
                    displayName = "Teletransporte",
                    manaCost = 5,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = true,
                    description = "Intercambia posiciones con cualquier jugador en juego."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Payasizacion,
                    displayName = "Payasización",
                    manaCost = 4,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "El próximo jugador con el que te cruces este turno es enviado al Tablero del Payaso."
                },
                new SpellDefinition
                {
                    spellType = SpellType.PasoLigero,
                    displayName = "Paso Ligero",
                    manaCost = 3,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "Puedes elegir el número exacto de pasos en tu siguiente jugada (entre el mínimo y el máximo)."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Vampirismo,
                    displayName = "Vampirismo",
                    manaCost = 2,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "Roba 1 moneda a cada jugador con el que te cruces en tu siguiente tirada."
                },
                new SpellDefinition
                {
                    spellType = SpellType.FlechaMagica,
                    displayName = "Flecha Mágica",
                    manaCost = 1,
                    isCombatSpell = true,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "Durante un combate, tira 1d4 y añade el resultado a tu ataque (se puede usar varias veces)."
                },
                new SpellDefinition
                {
                    spellType = SpellType.BolaDeFuego,
                    displayName = "Bola de Fuego",
                    manaCost = 1,
                    isCombatSpell = true,
                    oncePerCombat = true,
                    requiresTargetPlayer = false,
                    description = "Durante un combate, tira 1d10 y añade el resultado a tu ataque (1 vez por combate)."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Victoria,
                    displayName = "Victoria",
                    manaCost = 35,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "¡Ganas automáticamente la partida!"
                },
                new SpellDefinition
                {
                    spellType = SpellType.EldritchBlast,
                    displayName = "Eldritch Blast",
                    manaCost = 0,
                    isCombatSpell = true,
                    oncePerCombat = true,
                    requiresTargetPlayer = false,
                    description = "Durante un combate, tira 1d6 y añade el resultado a tu ataque (Coste 0, 1 vez por combate)."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Congelar,
                    displayName = "Congelar",
                    manaCost = 3,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = true,
                    description = "Sáltale el próximo turno a otro jugador."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Velocidad,
                    displayName = "Velocidad",
                    manaCost = 2,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "Duplica el resultado de tu tirada de movimiento (lanzar antes de tirar)."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Ralentizar,
                    displayName = "Ralentizar",
                    manaCost = 1,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = true,
                    description = "Divide entre 2 el resultado de la próxima tirada de otro jugador."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Muerte,
                    displayName = "Muerte",
                    manaCost = 4,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = true,
                    description = "Mata a cualquier jugador instantáneamente."
                },
                new SpellDefinition
                {
                    spellType = SpellType.Limpieza,
                    displayName = "Limpieza",
                    manaCost = 2,
                    isCombatSpell = false,
                    oncePerCombat = false,
                    requiresTargetPlayer = false,
                    description = "Quita a ti o a otro jugador un efecto negativo activo."
                }
            };
        }

        public static SpellDefinition GetSpellDefinition(SpellType type)
        {
            if (type == SpellType.None) return null;
            foreach (var s in SpellCatalog)
            {
                if (s.spellType == type) return s;
            }
            return null;
        }

        public static SpellType DrawRandomSpell()
        {
            var list = SpellCatalog;
            int idx = UnityEngine.Random.Range(0, list.Count);
            return list[idx].spellType;
        }

        /// <summary>
        /// Lanza el hechizo actual del jugador durante su turno.
        /// Nota: En La Oca 2, el jugador conserva su hechizo en su ranura única (hasta cambiarlo en otra casilla de Hechizos)
        /// y paga su coste en Maná cada vez que lo lanza.
        /// </summary>
        public static bool TryCastTurnSpell(
            PlayerOcaToken casterToken,
            PlayerOcaToken targetToken,
            BoardManager boardMgr,
            int totalPlayers,
            Func<PlayerOcaToken, BoardManager, string> killPlayerFunc,
            out string message,
            out bool instantVictory)
        {
            message = "";
            instantVictory = false;
            if (casterToken == null || casterToken.PlayerData == null) return false;

            PlayerSetupData pData = casterToken.PlayerData;
            SpellDefinition def = GetSpellDefinition(pData.currentSpell);
            if (def == null)
            {
                message = "No tienes ningún hechizo equipado.";
                return false;
            }

            if (def.isCombatSpell)
            {
                message = $"El hechizo '{def.displayName}' es de combate y se lanza durante un duelo.";
                return false;
            }

            if (pData.mana < def.manaCost)
            {
                message = $"Maná insuficiente para lanzar {def.displayName} (Requiere {def.manaCost} Maná, tienes {pData.mana}).";
                return false;
            }

            switch (def.spellType)
            {
                case SpellType.Teletransporte:
                    if (targetToken == null || targetToken == casterToken)
                    {
                        message = "Selecciona a otro jugador para intercambiar posiciones con Teletransporte.";
                        return false;
                    }
                    pData.mana -= def.manaCost;
                    BoardTile casterTile = boardMgr.GetTile(casterToken.CurrentBoard, casterToken.CurrentTileIndex);
                    BoardTile targetTile = boardMgr.GetTile(targetToken.CurrentBoard, targetToken.CurrentTileIndex);
                    if (casterTile != null && targetTile != null)
                    {
                        casterToken.PlaceOnTileImmediate(targetTile, pData.playerIndex, totalPlayers);
                        targetToken.PlaceOnTileImmediate(casterTile, targetToken.PlayerData.playerIndex, totalPlayers);
                        boardMgr.SwitchToBoard(casterToken.CurrentBoard);
                    }
                    message = $"¡{pData.playerName} lanzó Teletransporte (5 Maná) e intercambió posición con {targetToken.PlayerData.playerName}!";
                    return true;

                case SpellType.Payasizacion:
                    pData.mana -= def.manaCost;
                    pData.payasizacionActive = true;
                    message = $"¡{pData.playerName} lanzó Payasización (4 Maná)! El próximo jugador con el que se cruce irá al Tablero del Payaso.";
                    return true;

                case SpellType.PasoLigero:
                    pData.mana -= def.manaCost;
                    SkillTreeAndAbilities.GetMovementRollParameters(pData, out _, out _, out _, out int maxStep);
                    pData.forcedNextMoveRoll = maxStep;
                    message = $"¡{pData.playerName} lanzó Paso Ligero (3 Maná)! Su próxima tirada está fijada en {maxStep} pasos (ajustable).";
                    return true;

                case SpellType.Vampirismo:
                    pData.mana -= def.manaCost;
                    pData.vampirismActive = true;
                    message = $"¡{pData.playerName} lanzó Vampirismo (2 Maná)! Robará 1 moneda a cada jugador con el que se cruce.";
                    return true;

                case SpellType.Victoria:
                    pData.mana -= def.manaCost;
                    instantVictory = true;
                    message = $"¡¡{pData.playerName} reunió 35 de Maná, lanzó el hechizo VICTORIA y GANA AUTOMÁTICAMENTE LA PARTIDA!!";
                    return true;

                case SpellType.Congelar:
                    if (targetToken == null || targetToken == casterToken)
                    {
                        message = "Selecciona a otro jugador para Congelar su próximo turno.";
                        return false;
                    }
                    pData.mana -= def.manaCost;
                    targetToken.PlayerData.skipNextTurn = true;
                    message = $"¡{pData.playerName} lanzó Congelar (3 Maná)! {targetToken.PlayerData.playerName} perderá su próximo turno.";
                    return true;

                case SpellType.Velocidad:
                    pData.mana -= def.manaCost;
                    pData.doubleNextMoveRoll = true;
                    message = $"¡{pData.playerName} lanzó Velocidad (2 Maná)! Duplicará el resultado de su próxima tirada de movimiento.";
                    return true;

                case SpellType.Ralentizar:
                    if (targetToken == null || targetToken == casterToken)
                    {
                        message = "Selecciona a otro jugador para Ralentizar su próxima tirada.";
                        return false;
                    }
                    pData.mana -= def.manaCost;
                    targetToken.PlayerData.halveNextMoveRoll = true;
                    message = $"¡{pData.playerName} lanzó Ralentizar (1 Maná)! La próxima tirada de {targetToken.PlayerData.playerName} se dividirá entre 2.";
                    return true;

                case SpellType.Muerte:
                    if (targetToken == null || targetToken == casterToken)
                    {
                        message = "Selecciona a otro jugador para lanzar el hechizo Muerte.";
                        return false;
                    }
                    pData.mana -= def.manaCost;
                    if (targetToken.PlayerData.isZombie)
                    {
                        message = $"¡{pData.playerName} lanzó Muerte sobre {targetToken.PlayerData.playerName}, pero como es Zombi no puede morir!";
                    }
                    else
                    {
                        string hellName = killPlayerFunc != null ? killPlayerFunc(targetToken, boardMgr) : "Infierno";
                        message = $"¡{pData.playerName} lanzó el hechizo Muerte (4 Maná) sobre {targetToken.PlayerData.playerName} y lo envió a {hellName}!";
                    }
                    return true;

                case SpellType.Limpieza:
                    pData.mana -= def.manaCost;
                    PlayerSetupData cleanTarget = (targetToken != null && targetToken.PlayerData != null) ? targetToken.PlayerData : pData;
                    cleanTarget.activeNegativeEffect = "";
                    cleanTarget.halveNextMoveRoll = false;
                    cleanTarget.skipNextTurn = false;
                    message = $"¡{pData.playerName} lanzó Limpieza (2 Maná) y eliminó todos los efectos negativos de {cleanTarget.playerName}!";
                    return true;
            }

            return false;
        }

        public static void InitializeWildCards()
        {
            wildCardDeck = new List<WildCardDefinition>
            {
                new WildCardDefinition { effectType = WildCardEffectType.PlayerDies, title = "Mueres", description = "¡Mueres instantáneamente y vas al Infierno!", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.AllPlayersDie, title = "Apocalipsis Oca", description = "¡Todos los jugadores mueren y son enviados al Infierno!", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.GoToClownBoard, title = "¡Al Payaso!", description = "Ve directamente al Tablero del Payaso.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.UnoDuelForCoin, title = "Duelo de UNO", description = "Escoge a otro jugador y echad una partida rápida: quien gane se lleva 1 moneda.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.UnoDuelForCoin, title = "Duelo de UNO", description = "Escoge a otro jugador y echad una partida rápida: quien gane se lleva 1 moneda.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.TouchGrassGainExp, title = "Toca Hierba", description = "Sal de casa y toca hierba (+1 de Experiencia por reflexionar).", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.TellAJokeOrStart, title = "Cuenta un Chiste", description = "Si los demás jugadores lo consideran gracioso, recibes +1 moneda; si no, vuelves a START.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.ClearNegativeEffect, title = "Purificación", description = "Si tienes algún efecto negativo activo, deshazte de él.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.ClearNegativeEffect, title = "Purificación", description = "Si tienes algún efecto negativo activo, deshazte de él.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.ClearNegativeEffect, title = "Purificación", description = "Si tienes algún efecto negativo activo, deshazte de él.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeCrucifijo, title = "¡Crucifijo Gratis!", description = "Obtén un Crucifijo gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeCrucifijo, title = "¡Crucifijo Gratis!", description = "Obtén un Crucifijo gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeRedBull, title = "¡RedBull Gratis!", description = "Obtén una RedBull gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeRedBull, title = "¡RedBull Gratis!", description = "Obtén una RedBull gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeGuanteDuelo, title = "¡Guante de Duelo Gratis!", description = "Obtén un Guante de Duelo gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeGuanteDuelo, title = "¡Guante de Duelo Gratis!", description = "Obtén un Guante de Duelo gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeLupa, title = "¡Lupa Gratis!", description = "Obtén una Lupa gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.FreeCartaUNOReverse, title = "¡Carta UNO Reverse Gratis!", description = "Obtén una Carta Reversa del UNO gratis en tu inventario.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.JinxAnotherPlayer, title = "Mal de Ojo", description = "Elige a un jugador: cada vez que algo malo le pase, recibes 1 moneda.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.NothingHappens, title = "No Pasa Nada", description = "No pasa absolutamente nada. Respira tranquilo.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.CurseSayCuac, title = "Maldición 'Cuac'", description = "Efecto negativo: Debes terminar tus frases con 'Cuac' o pierdes 1 moneda.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.CurseNoLetterE, title = "Maldición Sin 'E'", description = "Efecto negativo: No puedes pronunciar la letra 'E' o pierdes 1 moneda.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.Advance1d6, title = "Impulso Corto", description = "¡Avanza 1d6 casillas!", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.Advance1d8, title = "Impulso Medio", description = "¡Avanza 1d8 casillas!", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.Advance1d20, title = "¡ Hiper Impulso !", description = "¡Avanza 1d20 casillas!", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.Retreat1d6, title = "Tropezón (1d6)", description = "Retrocede 1d6 casillas.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.Retreat1d6, title = "Tropezón (1d6)", description = "Retrocede 1d6 casillas.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.Retreat1d8, title = "Retroceso (1d8)", description = "Retrocede 1d8 casillas.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.Retreat1d8, title = "Retroceso (1d8)", description = "Retrocede 1d8 casillas.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.Retreat1d20, title = "Gran Caída (1d20)", description = "¡Retrocede 1d20 casillas!", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.SwapPositionWithPlayer, title = "Cambio de Lugar", description = "Intercambia tu posición con otro jugador a elección.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.CurseOneWordOnly, title = "Maldición Monosílabo", description = "Efecto negativo: Solo puedes usar una palabra hasta tu próximo turno.", isNegative = true },
                new WildCardDefinition { effectType = WildCardEffectType.RedistributeWealthAndMana, title = "Reparto Equitativo", description = "Se junta todo el dinero y maná de todos los jugadores y se reparte en partes iguales.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.ExtraTurnObo, title = "¡Pillas el Obo!", description = "¡Vuelves a tirar inmediatamente!", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.WordBetOneCoin, title = "Palabra Prohibida", description = "Elige una palabra: si alguien la dice antes de tu turno te da 1 moneda.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.Roll1d20AdvanceOrRetreat, title = "Ruleta del 1d20", description = "Tira 1d20: si sale menor que 10 avanzas ese número; si sale 10 o más retrocedes ese número.", isNegative = false },
                new WildCardDefinition { effectType = WildCardEffectType.SkipNextTurn, title = "Turno Perdido", description = "Se te salta tu próximo turno.", isNegative = true }
            };
        }

        public static WildCardDefinition DrawRandomWildCard()
        {
            var deck = WildCardDeck;
            int idx = UnityEngine.Random.Range(0, deck.Count);
            return deck[idx];
        }
    }
}
