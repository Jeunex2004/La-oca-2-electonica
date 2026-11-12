using System.Collections.Generic;
using UnityEngine;
using Oca2.Data;

namespace Oca2.Progression
{
    /// <summary>
    /// Gestiona las Habilidades Pasivas de las 6 Ocas (TP2 pág. 5 y 23) y el Árbol de Habilidades
    /// por Experiencia de 22 niveles ("Skill Tree.png", Reglas pág. 27-28).
    /// </summary>
    public static class SkillTreeAndAbilities
    {
        /// <summary>
        /// Aplica los bonos iniciales de la habilidad pasiva propia de cada una de las 6 Ocas al comenzar la partida.
        /// </summary>
        public static void ApplyOcaStartingPassive(PlayerSetupData pData)
        {
            if (pData == null) return;

            switch (pData.selectedOca)
            {
                case OcaType.LaOcaDefault:
                    // Equilibrio Clásico: empieza con +1 de Experiencia (sube a Nivel 2 inmediatamente)
                    GainExperienceAndGrantPoints(pData, 1);
                    break;

                case OcaType.LaOcarina:
                    // Melodía Lunar: empieza con +1 de Maná y 1 Hechizo aleatorio
                    pData.mana += 1;
                    pData.currentSpell = (SpellType)Random.Range(1, 14);
                    break;

                case OcaType.LaOcaDeBoca:
                    // La Mitad Más Uno: +1 fijo permanente en todas las tiradas de combate
                    pData.combatFlatBonus += 1;
                    break;

                case OcaType.LaLoca:
                    // Caos Festivo: +1 moneda cada vez que cae en Carta Salvaje (se aplica al robar Carta Salvaje)
                    break;

                case OcaType.LaBarroca:
                    // Opulencia Aristocrática: empieza con +2 monedas adicionales
                    pData.coins += 2;
                    break;

                case OcaType.ElXoca:
                    // Tryhardeará Hasta el Final: +1 EXP extra al ganar combates (+4 EXP en lugar de +3 EXP)
                    break;
            }
        }

        /// <summary>
        /// Devuelve la descripción corta de la habilidad pasiva de una Oca.
        /// </summary>
        public static string GetOcaPassiveDescription(OcaType oca)
        {
            return oca switch
            {
                OcaType.LaOcaDefault => "Pasiva (Equilibrio Clásico): Comienza la partida con +1 de Experiencia (Nivel 2 inicial).",
                OcaType.LaOcarina    => "Pasiva (Melodía Lunar): Comienza la partida con +1 de Maná y un Hechizo inicial.",
                OcaType.LaOcaDeBoca  => "Pasiva (La Mitad Más Uno): Obtiene +1 fijo permanente a su fuerza en todos los combates.",
                OcaType.LaLoca       => "Pasiva (Caos Festivo): Gana +1 Moneda adicional cada vez que cae en una Carta Salvaje.",
                OcaType.LaBarroca    => "Pasiva (Opulencia Aristocrática): Comienza la partida con +2 Monedas extra.",
                OcaType.ElXoca       => "Pasiva (Tryhard): Obtiene +4 de Experiencia al ganar un combate (en lugar de +3).",
                _ => ""
            };
        }

        /// <summary>
        /// Suma experiencia al jugador respetando la regla oficial (subir de nivel N requiere N de EXP, máx nivel 22)
        /// y otorga 1 punto de habilidad por cada nivel subido.
        /// </summary>
        public static int GainExperienceAndGrantPoints(PlayerSetupData pData, int amount)
        {
            if (pData == null || amount <= 0) return 0;

            int levelsGained = 0;
            pData.currentExp += amount;

            while (pData.expLevel < 22 && pData.currentExp >= pData.expLevel)
            {
                pData.currentExp -= pData.expLevel;
                pData.expLevel++;
                pData.unspentSkillPoints++;
                levelsGained++;
            }

            if (pData.expLevel >= 22)
            {
                pData.currentExp = 0;
            }

            return levelsGained;
        }

        /// <summary>
        /// Devuelve cuánta EXP se necesita para pasar del nivel actual al siguiente (Reglas pág. 27: Nivel N requiere N puntos de EXP; Nivel 22 es el máximo).
        /// </summary>
        public static int GetExpRequiredForNextLevel(int currentLevel)
        {
            if (currentLevel >= 22) return 0;
            return Mathf.Max(1, currentLevel);
        }

        /// <summary>
        /// Devuelve el progreso normalizado [0..1] de la barra de experiencia del jugador hacia su siguiente nivel.
        /// </summary>
        public static float GetExpProgressNormalized(PlayerSetupData pData)
        {
            if (pData == null) return 0f;
            if (pData.expLevel >= 22) return 1f;
            int req = GetExpRequiredForNextLevel(pData.expLevel);
            return Mathf.Clamp01((float)pData.currentExp / req);
        }

        /// <summary>
        /// Devuelve el texto formateado de la barra de experiencia (ej. "Nvl 3/22 • EXP: 1/3").
        /// </summary>
        public static string GetExpBarDisplayText(PlayerSetupData pData)
        {
            if (pData == null) return "Nvl 1/22 • EXP: 0/1";
            if (pData.expLevel >= 22) return "Nvl 22/22 (MÁXIMO)";
            int req = GetExpRequiredForNextLevel(pData.expLevel);
            return $"Nvl {pData.expLevel}/22 • EXP: {pData.currentExp}/{req}";
        }

        /// <summary>
        /// Devuelve el total de nodos desbloqueados en el Árbol de Habilidades (de un máximo de 21 círculos en "Skill Tree.png").
        /// </summary>
        public static int GetTotalUnlockedSkillNodes(PlayerSetupData pData)
        {
            if (pData == null) return 0;
            int movBase = Mathf.Min(3, pData.movementBranchNodes);
            int movTop = (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
            if (movTop == 0 && pData.movementBranchNodes > 3) movTop = pData.movementBranchNodes - 3;
            return movBase + movTop + pData.combatLeftBranchNodes + pData.combatRightBranchNodes + pData.wealthBranchNodes + pData.magicBranchNodes;
        }

        /// <summary>
        /// Calcula el multiplicador de escala visual 3D (crecimiento físico de la Oca en el tablero según su nivel de EXP y nodos desbloqueados).
        /// </summary>
        public static float GetGrowthScaleMultiplier(PlayerSetupData pData)
        {
            if (pData == null) return 1f;
            int lvl = Mathf.Clamp(pData.expLevel, 1, 22);
            // Crece progresivamente desde 1.00x en Nivel 1 hasta 1.52x en Nivel 22
            return 1.0f + (lvl - 1) * 0.025f;
        }

        /// <summary>
        /// Devuelve un resumen compacto de las estadísticas actuales de crecimiento del jugador (Movimiento, Ataque, Monedas/casilla, Maná/luna).
        /// </summary>
        public static string GetPlayerGrowthSummary(PlayerSetupData pData)
        {
            if (pData == null) return "Mov: 1d6 | Att: 1d6 | Oro/casilla: 1 | Maná/luna: 1";
            GetMovementRollParameters(pData, out int movD6, out int movFlat, out _, out _);
            string movStr = movFlat > 0 ? $"{movD6}d6+{movFlat}" : $"{movD6}d6";
            string attStr = GetCombatFormulaSummary(pData);
            int coinsPerTile = GetCoinsPerMoneyTile(pData);
            int manaPerMoon = GetManaPerFullMoon(pData);
            return $"Mov: {movStr}  |  Att: {attStr}  |  Oro/casilla: +{coinsPerTile}  |  Maná/luna: +{manaPerMoon}";
        }

        /// <summary>
        /// Devuelve la fórmula actual de combate del jugador como texto (ej. "2d6 + 1d8 + 3").
        /// </summary>
        public static string GetCombatFormulaSummary(PlayerSetupData pData)
        {
            int d6Count = 1;
            int d8Count = 0;
            int d10Count = 0;
            int flatBonus = 0;

            if (pData != null)
            {
                flatBonus += pData.combatFlatBonus;
                if (pData.combatLeftBranchNodes >= 1) flatBonus += 1;
                if (pData.combatLeftBranchNodes >= 2) d6Count += 1;
                if (pData.combatLeftBranchNodes >= 3) d6Count += 1;

                if (pData.combatRightBranchNodes >= 1) flatBonus += 2;
                if (pData.combatRightBranchNodes >= 2) d8Count += 1;
                if (pData.combatRightBranchNodes >= 3) d10Count += 1;
            }

            List<string> parts = new List<string> { $"{d6Count}d6" };
            if (d8Count > 0) parts.Add($"{d8Count}d8");
            if (d10Count > 0) parts.Add($"{d10Count}d10");
            if (flatBonus > 0) parts.Add($"+{flatBonus}");
            return string.Join("", parts);
        }

        /// <summary>
        /// Intenta gastar 1 punto de habilidad en una de las ramas o bifurcaciones del Árbol de Experiencia ("Skill Tree.png"),
        /// respetando la regla de rellenar los nodos desde abajo hacia arriba ("siempre y cuando no haya una casilla vacía debajo suya").
        /// </summary>
        public static bool TryUpgradeSkillBranch(PlayerSetupData pData, SkillBranchType branch, out string message)
        {
            message = "";
            if (pData == null) return false;

            if (pData.unspentSkillPoints <= 0)
            {
                message = "No tienes puntos de habilidad disponibles. Sube de nivel de EXP para obtener más.";
                return false;
            }

            switch (branch)
            {
                case SkillBranchType.Movement:
                    if (pData.movementBranchNodes < 3)
                    {
                        pData.movementBranchNodes++;
                        pData.unspentSkillPoints--;
                        message = $"¡Rama Movimiento mejorada ({GetMovementNodeRewardText(pData.movementBranchNodes)})!";
                        return true;
                    }
                    else if (!pData.movementTopLeftUnlocked)
                    {
                        pData.movementTopLeftUnlocked = true;
                        pData.movementBranchNodes = 3 + (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
                        pData.unspentSkillPoints--;
                        message = "¡Cima Izquierda de Movimiento desbloqueada (+1d6 Mov)!";
                        return true;
                    }
                    else if (!pData.movementTopRightUnlocked)
                    {
                        pData.movementTopRightUnlocked = true;
                        pData.movementBranchNodes = 3 + (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
                        pData.unspentSkillPoints--;
                        message = "¡Cima Derecha de Movimiento desbloqueada (+3 Mov)!";
                        return true;
                    }
                    else
                    {
                        message = "La rama de Movimiento ya está al máximo (5/5).";
                        return false;
                    }

                case SkillBranchType.MovementTopLeft:
                    if (pData.movementBranchNodes < 3)
                    {
                        message = "Primero debes desbloquear los 3 nodos inferiores de Movimiento (+1 Mov, +2 Mov y +1d6 Mov).";
                        return false;
                    }
                    if (pData.movementTopLeftUnlocked)
                    {
                        message = "La cima izquierda de Movimiento (+1d6 Mov) ya está desbloqueada.";
                        return false;
                    }
                    pData.movementTopLeftUnlocked = true;
                    pData.movementBranchNodes = 3 + (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
                    pData.unspentSkillPoints--;
                    message = "¡Cima Izquierda de Movimiento desbloqueada (+1d6 Mov)!";
                    return true;

                case SkillBranchType.MovementTopRight:
                    if (pData.movementBranchNodes < 3)
                    {
                        message = "Primero debes desbloquear los 3 nodos inferiores de Movimiento (+1 Mov, +2 Mov y +1d6 Mov).";
                        return false;
                    }
                    if (pData.movementTopRightUnlocked)
                    {
                        message = "La cima derecha de Movimiento (+3 Mov) ya está desbloqueada.";
                        return false;
                    }
                    pData.movementTopRightUnlocked = true;
                    pData.movementBranchNodes = 3 + (pData.movementTopLeftUnlocked ? 1 : 0) + (pData.movementTopRightUnlocked ? 1 : 0);
                    pData.unspentSkillPoints--;
                    message = "¡Cima Derecha de Movimiento desbloqueada (+3 Mov)!";
                    return true;

                case SkillBranchType.CombatLeft:
                    if (pData.combatLeftBranchNodes >= 3)
                    {
                        message = "La sub-rama de Combate (Dados d6) ya está al máximo (3/3).";
                        return false;
                    }
                    pData.combatLeftBranchNodes++;
                    pData.unspentSkillPoints--;
                    message = $"¡Rama Combate (Izq) mejorada a {pData.combatLeftBranchNodes}/3 ({GetCombatLeftNodeRewardText(pData.combatLeftBranchNodes)})!";
                    return true;

                case SkillBranchType.CombatRight:
                    if (pData.combatLeftBranchNodes < 1)
                    {
                        message = "Primero debes desbloquear la base de Combate (+1 Att) en la rama de Combate.";
                        return false;
                    }
                    if (pData.combatRightBranchNodes >= 3)
                    {
                        message = "La sub-rama de Combate Pesado (d8/d10) ya está al máximo (3/3).";
                        return false;
                    }
                    pData.combatRightBranchNodes++;
                    pData.unspentSkillPoints--;
                    message = $"¡Rama Combate (Der) mejorada a {pData.combatRightBranchNodes}/3 ({GetCombatRightNodeRewardText(pData.combatRightBranchNodes)})!";
                    return true;

                case SkillBranchType.Wealth:
                    if (pData.wealthBranchNodes >= 5)
                    {
                        message = "La rama de Riqueza ya está al máximo (5/5).";
                        return false;
                    }
                    pData.wealthBranchNodes++;
                    pData.unspentSkillPoints--;
                    message = $"¡Rama Riqueza mejorada a {pData.wealthBranchNodes}/5 ({GetWealthNodeRewardText(pData.wealthBranchNodes)})!";
                    return true;

                case SkillBranchType.Magic:
                    if (pData.magicBranchNodes >= 5)
                    {
                        message = "La rama de Magia ya está al máximo (5/5).";
                        return false;
                    }
                    pData.magicBranchNodes++;
                    pData.unspentSkillPoints--;
                    message = $"¡Rama Magia mejorada a {pData.magicBranchNodes}/5 ({GetMagicNodeRewardText(pData.magicBranchNodes)})!";
                    return true;
            }

            return false;
        }

        public static string GetMovementNodeRewardText(int node)
        {
            return node switch
            {
                1 => "+1 Mov",
                2 => "+2 Mov",
                3 => "+1d6 Mov",
                4 => "+1d6 Mov",
                5 => "+3 Mov",
                _ => ""
            };
        }

        public static string GetCombatLeftNodeRewardText(int node)
        {
            return node switch
            {
                1 => "+1 Att (Base)",
                2 => "+1d6 Att",
                3 => "+1d6 Att",
                _ => ""
            };
        }

        public static string GetCombatRightNodeRewardText(int node)
        {
            return node switch
            {
                1 => "+2 Att",
                2 => "+1d8 Att",
                3 => "+1d10 Att",
                _ => ""
            };
        }

        public static string GetWealthNodeRewardText(int node)
        {
            return node switch
            {
                2 => "+1 Moneda/casilla",
                5 => "+1 Moneda/casilla",
                _ => "Nodo de paso"
            };
        }

        public static string GetMagicNodeRewardText(int node)
        {
            return node switch
            {
                2 => "+1 Maná/luna",
                5 => "+1 Maná/luna",
                _ => "Nodo de paso"
            };
        }

        /// <summary>
        /// Calcula cuántos dados d6 de movimiento y cuánto bono fijo tiene el jugador (incluyendo mejoras del Árbol y objetos temporales).
        /// </summary>
        public static void GetMovementRollParameters(PlayerSetupData pData, out int totalD6Count, out int flatBonus, out int minPossible, out int maxPossible)
        {
            totalD6Count = 1; // 1d6 base
            flatBonus = 0;

            if (pData != null)
            {
                if (pData.movementBranchNodes >= 1) flatBonus += 1;
                if (pData.movementBranchNodes >= 2) flatBonus += 2;
                if (pData.movementBranchNodes >= 3) totalD6Count += 1;

                // Bifurcación superior de Movimiento en "Skill Tree.png": Cima Izq (+1d6 Mov) y Cima Der (+3 Mov)
                bool hasTopLeft = pData.movementTopLeftUnlocked || (!pData.movementTopLeftUnlocked && !pData.movementTopRightUnlocked && pData.movementBranchNodes >= 4);
                bool hasTopRight = pData.movementTopRightUnlocked || (!pData.movementTopLeftUnlocked && !pData.movementTopRightUnlocked && pData.movementBranchNodes >= 5);

                if (hasTopLeft) totalD6Count += 1;
                if (hasTopRight) flatBonus += 3;

                totalD6Count += pData.tempMoveExtraD6;
                flatBonus += pData.tempMoveFlatBonus;
            }

            minPossible = totalD6Count + flatBonus;
            maxPossible = (totalD6Count * 6) + flatBonus;
        }

        /// <summary>
        /// Ejecuta la tirada de movimiento del jugador aplicando el Árbol de Habilidades, RedBull, Botas, Lupa/Paso Ligero, Velocidad y Ralentizar.
        /// </summary>
        public static int RollPlayerMovement(PlayerSetupData pData, out string breakdown)
        {
            return ResolveMovementFromPhysicalRolls(pData, null, out breakdown);
        }

        /// <summary>
        /// Calcula el movimiento final del jugador a partir de las caras obtenidas por los dados físicos 3D del Vaso/Cubilete
        /// (o tirada aleatoria si physicalRolls es nulo), aplicando todos los bonos del Árbol, objetos y hechizos.
        /// </summary>
        public static int ResolveMovementFromPhysicalRolls(PlayerSetupData pData, List<int> physicalRolls, out string breakdown)
        {
            GetMovementRollParameters(pData, out int d6Count, out int flatBonus, out int minVal, out int maxVal);

            int total;
            List<string> parts = new List<string>();

            if (pData != null && pData.forcedNextMoveRoll > 0)
            {
                total = Mathf.Clamp(pData.forcedNextMoveRoll, minVal, maxVal);
                parts.Add($"Elegido ({total})");
            }
            else
            {
                int sumDice = 0;
                for (int i = 0; i < d6Count; i++)
                {
                    int r = (physicalRolls != null && i < physicalRolls.Count)
                        ? Mathf.Clamp(physicalRolls[i], 1, 6)
                        : Random.Range(1, 7);
                    sumDice += r;
                    parts.Add(r.ToString());
                }
                total = sumDice + flatBonus;
            }

            string formulaStr = flatBonus > 0 ? $"{d6Count}d6+{flatBonus}" : $"{d6Count}d6";
            breakdown = pData != null && pData.forcedNextMoveRoll > 0
                ? $"[{formulaStr}: {string.Join("+", parts)}]"
                : $"[{formulaStr}: ({string.Join("+", parts)})" + (flatBonus > 0 ? $"+{flatBonus}" : "") + "]";

            if (pData != null)
            {
                if (pData.doubleNextMoveRoll)
                {
                    total *= 2;
                    breakdown += " x2 (Velocidad)";
                }
                if (pData.halveNextMoveRoll)
                {
                    total = Mathf.Max(1, total / 2);
                    pData.halveNextMoveRoll = false;
                    breakdown += " /2 (Ralentizado)";
                }
                pData.ClearTurnMoveModifiers();
            }

            return Mathf.Max(1, total);
        }

        /// <summary>
        /// Calcula y tira todos los dados de combate de un jugador según su Árbol de Habilidades ("Skill Tree.png"):
        /// Base 1d6 + nodos de Combate (+1 Att, +1d6 Att, +1d6 Att, +2 Att, +1d8 Att, +1d10 Att) + pasiva de Oca.
        /// </summary>
        public static int RollPlayerCombatStrength(PlayerSetupData pData, int extraCombatFlatBonus, out List<int> individualRolls, out string formulaDescription)
        {
            individualRolls = new List<int>();
            int d6Count = 1;
            int d8Count = 0;
            int d10Count = 0;
            int flatBonus = extraCombatFlatBonus;

            if (pData != null)
            {
                flatBonus += pData.combatFlatBonus;

                if (pData.combatLeftBranchNodes >= 1) flatBonus += 1;
                if (pData.combatLeftBranchNodes >= 2) d6Count += 1;
                if (pData.combatLeftBranchNodes >= 3) d6Count += 1;

                if (pData.combatRightBranchNodes >= 1) flatBonus += 2;
                if (pData.combatRightBranchNodes >= 2) d8Count += 1;
                if (pData.combatRightBranchNodes >= 3) d10Count += 1;
            }

            int total = 0;
            for (int i = 0; i < d6Count; i++)
            {
                int r = Random.Range(1, 7);
                individualRolls.Add(r);
                total += r;
            }
            for (int i = 0; i < d8Count; i++)
            {
                int r = Random.Range(1, 9);
                individualRolls.Add(r);
                total += r;
            }
            for (int i = 0; i < d10Count; i++)
            {
                int r = Random.Range(1, 11);
                individualRolls.Add(r);
                total += r;
            }

            total += flatBonus;

            List<string> fParts = new List<string> { $"{d6Count}d6" };
            if (d8Count > 0) fParts.Add($"{d8Count}d8");
            if (d10Count > 0) fParts.Add($"{d10Count}d10");
            if (flatBonus > 0) fParts.Add($"+{flatBonus}");
            else if (flatBonus < 0) fParts.Add($"{flatBonus}");

            formulaDescription = string.Join(" ", fParts);
            return Mathf.Max(1, total);
        }

        /// <summary>
        /// Devuelve cuántas monedas obtiene un jugador al caer en una casilla de Dinero (1 base + rama de Riqueza).
        /// </summary>
        public static int GetCoinsPerMoneyTile(PlayerSetupData pData)
        {
            int coins = 1;
            if (pData != null)
            {
                if (pData.wealthBranchNodes >= 2) coins += 1;
                if (pData.wealthBranchNodes >= 5) coins += 1;
            }
            return coins;
        }

        /// <summary>
        /// Devuelve cuánto Maná obtiene un jugador cada Luna Llena (1 base + rama de Magia).
        /// </summary>
        public static int GetManaPerFullMoon(PlayerSetupData pData)
        {
            int mana = 1;
            if (pData != null)
            {
                if (pData.magicBranchNodes >= 2) mana += 1;
                if (pData.magicBranchNodes >= 5) mana += 1;
            }
            return mana;
        }
    }
}
