using System;
using System.Collections.Generic;
using UnityEngine;
using Oca2.Board;
using Oca2.Data;
using Oca2.Players;
using Oca2.Progression;

namespace Oca2.Items
{
    [Serializable]
    public class ShopItemDefinition
    {
        public ItemType itemType;
        public string displayName;
        public int priceCoins;
        public bool availableInMainShop;
        public bool availableInHellShop;
        public bool requiresExactLanding; // Caja verde (Tienda Principal) o Caja roja (Tienda Infierno)
        public bool usableInCombat;
        public bool usableInTurn;
        public bool requiresTargetPlayer;
        public string assetFileName;
        public string shortEffect;
        public Sprite sprite;
    }

    /// <summary>
    /// Gestiona la Tienda Principal ("Tienda.png"), la Tienda del Infierno ("Tienda infierno.png")
    /// y el inventario/uso de los 12 objetos oficiales (Reglas pág. 20-26).
    /// </summary>
    public static class ShopAndItemsSystem
    {
        private static List<ShopItemDefinition> catalog;

        public static IReadOnlyList<ShopItemDefinition> Catalog
        {
            get
            {
                if (catalog == null || catalog.Count == 0)
                {
                    InitializeCatalog();
                }
                return catalog;
            }
        }

        public static void InitializeCatalog()
        {
            catalog = new List<ShopItemDefinition>
            {
                new ShopItemDefinition
                {
                    itemType = ItemType.RedBull,
                    displayName = "RedBull",
                    priceCoins = 1,
                    availableInMainShop = true,
                    availableInHellShop = true,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "RedBull.png",
                    shortEffect = "Suma +1d6 a tu próxima tirada de movimiento."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Crucifijo,
                    displayName = "Crucifijo",
                    priceCoins = 2,
                    availableInMainShop = true,
                    availableInHellShop = false,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Crucifijo.png",
                    shortEffect = "Quita un efecto negativo o te saca automáticamente del Infierno 1 o 2."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.GuanteDeDuelo,
                    displayName = "Guante de Duelo",
                    priceCoins = 1,
                    availableInMainShop = true,
                    availableInHellShop = true,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Guante de batalla.png",
                    shortEffect = "Entra en combate con el primer jugador con el que te cruces en este turno."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Botas,
                    displayName = "Botas",
                    priceCoins = 3,
                    availableInMainShop = true,
                    availableInHellShop = false,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Botas.png",
                    shortEffect = "Suma +3 fijos a tu tirada de movimiento."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Armadura,
                    displayName = "Armadura",
                    priceCoins = 3,
                    availableInMainShop = true,
                    availableInHellShop = false,
                    requiresExactLanding = false,
                    usableInCombat = true,
                    usableInTurn = false,
                    requiresTargetPlayer = false,
                    assetFileName = "Armadura.png",
                    shortEffect = "Suma +5 al resultado de tu tirada de combate."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Lupa,
                    displayName = "Lupa",
                    priceCoins = 5,
                    availableInMainShop = true,
                    availableInHellShop = false,
                    requiresExactLanding = true, // Caja verde (Tienda Principal)
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Lupa.png",
                    shortEffect = "[Caja Verde] Elige el resultado máximo exacto de tu tirada de movimiento."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.CartaUNOReverse,
                    displayName = "Carta Reversa UNO",
                    priceCoins = 7,
                    availableInMainShop = true,
                    availableInHellShop = false,
                    requiresExactLanding = true, // Caja verde (Tienda Principal)
                    usableInCombat = true,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Carta UNO Reverse.png",
                    shortEffect = "[Caja Verde] Revierte una derrota en combate, muerte o efecto negativo."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Pentagrama,
                    displayName = "Pentagrama",
                    priceCoins = 1,
                    availableInMainShop = false,
                    availableInHellShop = true,
                    requiresExactLanding = false,
                    usableInCombat = true,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Pentagrama.png",
                    shortEffect = "Resta -1d6 a la tirada de combate del oponente."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.BotellaEXP,
                    displayName = "Botella de EXP",
                    priceCoins = 3,
                    availableInMainShop = false,
                    availableInHellShop = true,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Botella de EXP.png",
                    shortEffect = "Otorga inmediatamente 1d4 de Experiencia."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.BotellaMana,
                    displayName = "Botella de Maná",
                    priceCoins = 4,
                    availableInMainShop = false,
                    availableInHellShop = true,
                    requiresExactLanding = false,
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = false,
                    assetFileName = "Botella de Mana.png",
                    shortEffect = "Otorga inmediatamente 1d4 de Maná."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Espada,
                    displayName = "Espada",
                    priceCoins = 4,
                    availableInMainShop = false,
                    availableInHellShop = true,
                    requiresExactLanding = true, // Caja roja (Tienda Infierno)
                    usableInCombat = true,
                    usableInTurn = false,
                    requiresTargetPlayer = false,
                    assetFileName = "Espada.png",
                    shortEffect = "[Caja Roja] Suma +8 al resultado de tu tirada de combate."
                },
                new ShopItemDefinition
                {
                    itemType = ItemType.Pistola,
                    displayName = "Pistola",
                    priceCoins = 5,
                    availableInMainShop = false,
                    availableInHellShop = true,
                    requiresExactLanding = true, // Caja roja (Tienda Infierno)
                    usableInCombat = false,
                    usableInTurn = true,
                    requiresTargetPlayer = true,
                    assetFileName = "Pistola.png",
                    shortEffect = "[Caja Roja] Mata automáticamente a cualquier jugador que elijas."
                }
            };
        }

        public static ShopItemDefinition GetDefinition(ItemType type)
        {
            foreach (var item in Catalog)
            {
                if (item.itemType == type) return item;
            }
            return null;
        }

        /// <summary>
        /// Devuelve los objetos disponibles en la tienda correspondiente (Principal o Infierno).
        /// </summary>
        public static List<ShopItemDefinition> GetShopItems(bool isHellShop)
        {
            List<ShopItemDefinition> result = new List<ShopItemDefinition>();
            foreach (var item in Catalog)
            {
                if (isHellShop && item.availableInHellShop) result.Add(item);
                else if (!isHellShop && item.availableInMainShop) result.Add(item);
            }
            return result;
        }

        /// <summary>
        /// Intenta comprar un objeto en la tienda actual verificando monedas y si cayó exactamente en la casilla (cajas verde/roja).
        /// </summary>
        public static bool TryBuyItem(PlayerSetupData buyer, ItemType itemType, bool landedExactlyOnShopTile, out string message)
        {
            message = "";
            if (buyer == null) return false;

            ShopItemDefinition def = GetDefinition(itemType);
            if (def == null) return false;

            if (def.requiresExactLanding && !landedExactlyOnShopTile)
            {
                message = $"¡{def.displayName} está en la caja especial! Solo puedes comprarlo si caes exactamente en la casilla de Tienda.";
                return false;
            }

            if (buyer.coins < def.priceCoins)
            {
                message = $"No tienes suficientes monedas para comprar {def.displayName} (Cuesta {def.priceCoins}, tienes {buyer.coins}).";
                return false;
            }

            buyer.coins -= def.priceCoins;
            buyer.inventory.Add(itemType);
            message = $"¡{buyer.playerName} compró {def.displayName} por {def.priceCoins} moneda(s)!";
            return true;
        }

        /// <summary>
        /// Usa un objeto del inventario durante el turno del jugador (antes de tirar dado o en cualquier momento).
        /// </summary>
        public static bool TryUseItemInTurn(
            PlayerOcaToken userToken,
            ItemType itemType,
            PlayerOcaToken targetToken,
            BoardManager boardMgr,
            int totalPlayers,
            Func<PlayerOcaToken, BoardManager, string> killPlayerFunc,
            out string message)
        {
            message = "";
            if (userToken == null || userToken.PlayerData == null) return false;
            PlayerSetupData pData = userToken.PlayerData;

            if (!pData.inventory.Contains(itemType))
            {
                message = "No posees ese objeto en tu inventario.";
                return false;
            }

            ShopItemDefinition def = GetDefinition(itemType);

            switch (itemType)
            {
                case ItemType.RedBull:
                    pData.inventory.Remove(itemType);
                    pData.tempMoveExtraD6 += 1;
                    message = $"¡{pData.playerName} bebió una RedBull! Suma +1d6 a su próxima tirada de movimiento.";
                    return true;

                case ItemType.Botas:
                    pData.inventory.Remove(itemType);
                    pData.tempMoveFlatBonus += 3;
                    message = $"¡{pData.playerName} equipó las Botas! Suma +3 a su próxima tirada de movimiento.";
                    return true;

                case ItemType.GuanteDeDuelo:
                    pData.inventory.Remove(itemType);
                    pData.duelGloveActive = true;
                    message = $"¡{pData.playerName} activó el Guante de Duelo! Retará al primer jugador con el que se cruce.";
                    return true;

                case ItemType.Lupa:
                    pData.inventory.Remove(itemType);
                    SkillTreeAndAbilities.GetMovementRollParameters(pData, out _, out _, out _, out int maxRoll);
                    pData.forcedNextMoveRoll = maxRoll;
                    message = $"¡{pData.playerName} usó la Lupa! Su próxima tirada de movimiento está fijada en {maxRoll} (o puedes ajustarla).";
                    return true;

                case ItemType.Crucifijo:
                    if (userToken.CurrentBoard == BoardType.Hell1 || userToken.CurrentBoard == BoardType.Hell2)
                    {
                        pData.inventory.Remove(itemType);
                        BoardTile mainStart = boardMgr != null ? boardMgr.GetTile(BoardType.MainBoard, 0) : null;
                        if (mainStart != null)
                        {
                            userToken.PlaceOnTileImmediate(mainStart, pData.playerIndex, totalPlayers);
                            boardMgr.SwitchToBoard(BoardType.MainBoard);
                        }
                        message = $"¡{pData.playerName} usó el Crucifijo y salió automáticamente del Infierno hacia el Tablero Principal!";
                        return true;
                    }
                    else if (!string.IsNullOrEmpty(pData.activeNegativeEffect) || pData.halveNextMoveRoll || pData.skipNextTurn)
                    {
                        pData.inventory.Remove(itemType);
                        pData.activeNegativeEffect = "";
                        pData.halveNextMoveRoll = false;
                        pData.skipNextTurn = false;
                        message = $"¡{pData.playerName} usó el Crucifijo y purificó todos sus efectos negativos!";
                        return true;
                    }
                    else
                    {
                        message = "El Crucifijo se usa para salir del Infierno 1/2 o quitarte un efecto negativo.";
                        return false;
                    }

                case ItemType.BotellaEXP:
                    pData.inventory.Remove(itemType);
                    int expGain = UnityEngine.Random.Range(1, 5); // 1d4
                    int lvls = SkillTreeAndAbilities.GainExperienceAndGrantPoints(pData, expGain);
                    message = $"¡{pData.playerName} usó una Botella de Experiencia y obtuvo +{expGain} EXP!" +
                              (lvls > 0 ? $" ¡Subió a Nivel {pData.expLevel}!" : "");
                    return true;

                case ItemType.BotellaMana:
                    pData.inventory.Remove(itemType);
                    int manaGain = UnityEngine.Random.Range(1, 5); // 1d4
                    pData.mana += manaGain;
                    message = $"¡{pData.playerName} usó una Botella de Maná y obtuvo +{manaGain} Maná! (Total: {pData.mana})";
                    return true;

                case ItemType.Pistola:
                    if (targetToken == null || targetToken == userToken)
                    {
                        message = "Selecciona a otro jugador para disparar con la Pistola.";
                        return false;
                    }
                    pData.inventory.Remove(itemType);
                    if (targetToken.PlayerData.isZombie)
                    {
                        message = $"¡{pData.playerName} disparó la Pistola contra {targetToken.PlayerData.playerName}, pero al ser Zombi no puede morir!";
                    }
                    else
                    {
                        string hellDest = killPlayerFunc != null ? killPlayerFunc(targetToken, boardMgr) : "Infierno";
                        message = $"¡BANG! ¡{pData.playerName} usó la Pistola contra {targetToken.PlayerData.playerName} y lo envió directamente a {hellDest}!";
                    }
                    return true;

                case ItemType.CartaUNOReverse:
                    if (!string.IsNullOrEmpty(pData.activeNegativeEffect) || pData.halveNextMoveRoll || pData.skipNextTurn)
                    {
                        pData.inventory.Remove(itemType);
                        pData.activeNegativeEffect = "";
                        pData.halveNextMoveRoll = false;
                        pData.skipNextTurn = false;
                        message = $"¡{pData.playerName} usó la Carta Reversa del UNO para anular sus efectos negativos!";
                        return true;
                    }
                    message = "La Carta Reversa del UNO también se activa automáticamente en Combate o ante efectos negativos.";
                    return false;

                default:
                    message = $"{def?.displayName ?? itemType.ToString()} solo puede usarse durante un Combate.";
                    return false;
            }
        }
    }
}
