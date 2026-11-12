using System;
using System.Collections.Generic;
using UnityEngine;
using Oca2.Players;

namespace Oca2.Combat
{
    /// <summary>
    /// Los 3 Bosses del juego definidos en el documento TP2 (pág. 6, 17, 18 y 27).
    /// </summary>
    public enum BossType
    {
        None = 0,
        LaFoca = 1,      // Casilla 13 - Fuerza: 1d8
        LaRoca = 2,      // Casilla 35 - Fuerza: 2d6
        LaTrollface = 3  // Casilla 53 - Fuerza: 3d6
    }

    /// <summary>
    /// Representa una fórmula de dados de fuerza de combate (ej. 1d6, 1d8, 2d6, 3d6 + bonos).
    /// </summary>
    [Serializable]
    public struct DiceFormula
    {
        public int diceCount;
        public int diceSides;
        public int flatBonus;

        public DiceFormula(int count, int sides, int bonus = 0)
        {
            diceCount = Mathf.Max(1, count);
            diceSides = Mathf.Max(2, sides);
            flatBonus = bonus;
        }

        public int Roll(out List<int> individualRolls)
        {
            individualRolls = new List<int>(diceCount);
            int sum = 0;
            for (int i = 0; i < diceCount; i++)
            {
                int val = UnityEngine.Random.Range(1, diceSides + 1);
                individualRolls.Add(val);
                sum += val;
            }
            return Mathf.Max(1, sum + flatBonus);
        }

        public override string ToString()
        {
            if (flatBonus > 0)
                return $"{diceCount}d{diceSides} +{flatBonus}";
            if (flatBonus < 0)
                return $"{diceCount}d{diceSides} {flatBonus}";
            return $"{diceCount}d{diceSides}";
        }
    }

    /// <summary>
    /// Configuración y estado de un Boss en el tablero principal.
    /// </summary>
    [Serializable]
    public class BossDefinition
    {
        public BossType bossType;
        public string displayName;
        public int tileIndex;
        public DiceFormula combatStrength;
        public string assetFileName;
        [TextArea(2, 3)]
        public string description;
        public Sprite sprite;
        public Texture2D texture;
        public bool isDefeated;
    }

    /// <summary>
    /// Estado completo de un encuentro de combate activo (PvE contra un Boss o PvP entre dos jugadores).
    /// </summary>
    public class CombatEncounter
    {
        public bool isBossCombat;
        public PlayerOcaToken attackerToken;
        public PlayerOcaToken defenderPlayerToken; // Solo si es PvP
        public BossDefinition targetBoss;          // Solo si es PvE contra Boss

        public DiceFormula attackerFormula;
        public DiceFormula defenderFormula;
        public string attackerFormulaText = "1d6";
        public string defenderFormulaText = "1d6";

        // Bonificaciones activas en este combate (Objetos: Armadura +5, Espada +8, Pentagrama -1d6 y Hechizos de combate)
        public int attackerExtraCombatBonus = 0;
        public int defenderPenaltyBonus = 0;
        public bool usedFireball = false;
        public bool usedEldritchBlast = false;

        public List<int> attackerRolls = new List<int>();
        public List<int> defenderRolls = new List<int>();
        public int attackerTotal = 0;
        public int defenderTotal = 0;

        public bool hasRolled = false;
        public bool isTie = false;
        public bool attackerWon = false;
        public string resultSummary = "";
    }
}
