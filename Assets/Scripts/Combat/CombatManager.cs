using System;
using System.Collections.Generic;
using UnityEngine;
using Oca2.Board;
using Oca2.CardsAndMagic;
using Oca2.Core;
using Oca2.Data;
using Oca2.Items;
using Oca2.Players;
using Oca2.Progression;

namespace Oca2.Combat
{
    /// <summary>
    /// Gestor del Sistema de Combate y de los 3 Bosses de La Oca 2: Electric Boogaloo (TP2 pág. 6, 8, 17, 18).
    /// Administra:
    /// - Los 3 Bosses del Tablero Principal: La Foca (casilla 13, 1d8), La Roca (casilla 35, 2d6) y La Trollface (casilla 53, 3d6).
    /// - Intercepción automática cuando un jugador pasa por encima de una casilla con un Boss vivo.
    /// - Combates PvP cuando un jugador cae en la casilla de otro jugador (excepto START e Infierno 2) o usa Guante de Duelo.
    /// - Integración completa con el Árbol de Habilidades ("Skill Tree.png"), Objetos de Combate (Armadura, Espada, Pentagrama, Carta UNO Reverse)
    ///   y Hechizos de Combate (Flecha Mágica, Bola de Fuego, Eldritch Blast).
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Catálogo de los 3 Bosses (TP2 Pág. 17-18)")]
        [SerializeField] private List<BossDefinition> bosses = new List<BossDefinition>();

        private readonly List<BossBoardToken> spawnedBossTokens = new List<BossBoardToken>();
        private GameObject bossesContainer;

        private CombatEncounter activeEncounter;
        private Action<CombatEncounter> onEncounterConcludedCallback;

        public IReadOnlyList<BossDefinition> Bosses => bosses;
        public CombatEncounter ActiveEncounter => activeEncounter;
        public bool IsInCombat => activeEncounter != null;

        public event Action<CombatEncounter> OnCombatStarted;
        public event Action<CombatEncounter> OnCombatRolled;
        public event Action<CombatEncounter> OnCombatEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureDefaultBosses();
        }

        /// <summary>
        /// Registra los 3 Bosses oficiales del documento TP2 y del Tablero Principal ("Tablero principal.png"):
        /// - La Foca (Casilla 13): 1d8
        /// - La Roca (Casilla 35): 2d6
        /// - La Trollface (Casilla 53): 3d6
        /// </summary>
        public void EnsureDefaultBosses()
        {
            if (bosses == null) bosses = new List<BossDefinition>();

            if (bosses.Count < 3)
            {
                bosses.Clear();
                bosses.Add(new BossDefinition
                {
                    bossType = BossType.LaFoca,
                    displayName = "La Foca",
                    tileIndex = 13,
                    combatStrength = new DiceFormula(1, 8, 0), // 1d8 (pág. 17)
                    assetFileName = "La foca.png",
                    description = "Primer Boss (Casilla 13). Tiene una fuerza de combate de 1d8 (un dado de 8 caras).",
                    isDefeated = false
                });
                bosses.Add(new BossDefinition
                {
                    bossType = BossType.LaRoca,
                    displayName = "La Roca",
                    tileIndex = 35,
                    combatStrength = new DiceFormula(2, 6, 0), // 2d6 (pág. 18)
                    assetFileName = "La roca.png",
                    description = "Segundo Boss (Casilla 35). Tiene una fuerza de combate de 2d6 (dos dados de 6 caras).",
                    isDefeated = false
                });
                bosses.Add(new BossDefinition
                {
                    bossType = BossType.LaTrollface,
                    displayName = "La Trollface",
                    tileIndex = 53,
                    combatStrength = new DiceFormula(3, 6, 0), // 3d6 (pág. 18)
                    assetFileName = "Trollface.png",
                    description = "Boss Final (Casilla 53). Tiene una fuerza de combate de 3d6 (tres dados de 6 caras).",
                    isDefeated = false
                });
            }
            else
            {
                // Sincronizar índices de casilla con las 3 casillas rojas del Tablero Principal (13, 35, 53)
                foreach (var b in bosses)
                {
                    if (b.bossType == BossType.LaFoca) b.tileIndex = 13;
                    else if (b.bossType == BossType.LaRoca) b.tileIndex = 35;
                    else if (b.bossType == BossType.LaTrollface) b.tileIndex = 53;
                }
            }

#if UNITY_EDITOR
            LoadBossAssetsInEditorIfNeeded();
#endif

            for (int i = 0; i < bosses.Count; i++)
            {
                var b = bosses[i];
                if (b.sprite == null && b.texture != null)
                {
                    b.sprite = GameSessionManager.CreateSpriteFromTexture(b.texture);
                }
            }
        }

#if UNITY_EDITOR
        public void LoadBossAssetsInEditorIfNeeded()
        {
            const string folder = "Assets/OCA 2 assets/";
            foreach (var b in bosses)
            {
                if (b.texture == null && !string.IsNullOrEmpty(b.assetFileName))
                {
                    b.texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(folder + b.assetFileName);
                }
                if (b.sprite == null && !string.IsNullOrEmpty(b.assetFileName))
                {
                    b.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(folder + b.assetFileName);
                    if (b.sprite == null && b.texture != null)
                    {
                        b.sprite = GameSessionManager.CreateSpriteFromTexture(b.texture);
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Instancia las figuras 3D de los 3 Bosses en las casillas 13, 35 y 53 del Tablero Principal.
        /// </summary>
        public void SpawnBossesOnBoard(BoardManager boardMgr, bool enableBosses)
        {
            ClearBossTokens();
            EnsureDefaultBosses();

            foreach (var b in bosses)
            {
                b.isDefeated = !enableBosses;
            }

            if (!enableBosses || boardMgr == null) return;

            if (bossesContainer == null)
            {
                bossesContainer = new GameObject("Bosses_Tokens_Container");
            }

            foreach (var bossDef in bosses)
            {
                BoardTile bossTile = boardMgr.GetTile(BoardType.MainBoard, bossDef.tileIndex);
                if (bossTile == null) continue;

                GameObject obj = new GameObject($"Boss_{bossDef.displayName}");
                obj.transform.SetParent(bossesContainer.transform, false);

                BossBoardToken token = obj.AddComponent<BossBoardToken>();
                token.Initialize(bossDef, bossTile);
                spawnedBossTokens.Add(token);
            }
        }

        public void ClearBossTokens()
        {
            foreach (var token in spawnedBossTokens)
            {
                if (token != null)
                {
                    Destroy(token.gameObject);
                }
            }
            spawnedBossTokens.Clear();
        }

        /// <summary>
        /// Verifica si en el recorrido normal desde <paramref name="fromTileIndex"/> hasta <paramref name="intendedTargetIndex"/>
        /// el jugador pasa por encima o cae en una casilla con un Boss activo (TP2 pág. 8).
        /// </summary>
        public BossDefinition GetBossInterceptOnPath(BoardType board, int fromTileIndex, int intendedTargetIndex)
        {
            if (board != BoardType.MainBoard) return null;
            if (intendedTargetIndex <= fromTileIndex) return null;

            EnsureDefaultBosses();

            BossDefinition closestBoss = null;
            int closestIndex = int.MaxValue;

            foreach (var boss in bosses)
            {
                if (boss.isDefeated) continue;

                // Si el jugador pasa por encima o cae exactamente en la casilla del Boss
                if (boss.tileIndex > fromTileIndex && boss.tileIndex <= intendedTargetIndex)
                {
                    if (boss.tileIndex < closestIndex)
                    {
                        closestIndex = boss.tileIndex;
                        closestBoss = boss;
                    }
                }
            }

            return closestBoss;
        }

        /// <summary>
        /// Devuelve el Boss vivo ubicado exactamente en la casilla <paramref name="tileIndex"/> del tablero indicado (o null si no hay ninguno).
        /// </summary>
        public BossDefinition GetAliveBossOnTile(BoardType board, int tileIndex)
        {
            if (board != BoardType.MainBoard) return null;
            EnsureDefaultBosses();

            foreach (var boss in bosses)
            {
                if (!boss.isDefeated && boss.tileIndex == tileIndex)
                {
                    return boss;
                }
            }
            return null;
        }

        /// <summary>
        /// Verifica si hay otro jugador en la misma casilla para iniciar un combate PvP.
        /// Según TP2 pág. 11: en el Tablero del Infierno 2 no puede haber combates.
        /// </summary>
        public PlayerOcaToken GetPvPOpponentOnTile(PlayerOcaToken attacker, IReadOnlyList<PlayerOcaToken> allPlayers)
        {
            if (attacker == null || allPlayers == null) return null;
            if (attacker.CurrentBoard == BoardType.Hell2) return null; // Sin combates en Infierno 2
            if (attacker.CurrentTileIndex == 0) return null;           // Casilla START es zona segura

            foreach (var other in allPlayers)
            {
                if (other == null || other == attacker) continue;
                if (other.CurrentBoard == attacker.CurrentBoard && other.CurrentTileIndex == attacker.CurrentTileIndex)
                {
                    return other;
                }
            }
            return null;
        }

        /// <summary>
        /// Devuelve la descripción completa de la fórmula de dados de combate de un jugador según su Árbol de Habilidades y bonos activos.
        /// </summary>
        public string DescribePlayerCombatFormula(PlayerSetupData pData, int extraBonus = 0)
        {
            if (pData == null) return "1d6";

            int d6Count = 1;
            int d8Count = 0;
            int d10Count = 0;
            int flatBonus = pData.combatFlatBonus + extraBonus;

            if (pData.combatLeftBranchNodes >= 1) flatBonus += 1;
            if (pData.combatLeftBranchNodes >= 2) d6Count += 1;
            if (pData.combatLeftBranchNodes >= 3) d6Count += 1;

            if (pData.combatRightBranchNodes >= 1) flatBonus += 2;
            if (pData.combatRightBranchNodes >= 2) d8Count += 1;
            if (pData.combatRightBranchNodes >= 3) d10Count += 1;

            List<string> parts = new List<string> { $"{d6Count}d6" };
            if (d8Count > 0) parts.Add($"{d8Count}d8");
            if (d10Count > 0) parts.Add($"{d10Count}d10");
            if (flatBonus > 0) parts.Add($"+{flatBonus}");
            else if (flatBonus < 0) parts.Add($"{flatBonus}");

            return string.Join(" ", parts);
        }

        public DiceFormula GetPlayerCombatFormula(PlayerSetupData player, int extraBonus = 0)
        {
            if (player == null) return new DiceFormula(1, 6, extraBonus);
            int d6Count = 1 + (player.combatLeftBranchNodes >= 2 ? 1 : 0) + (player.combatLeftBranchNodes >= 3 ? 1 : 0);
            int flatBonus = player.combatFlatBonus + extraBonus
                + (player.combatLeftBranchNodes >= 1 ? 1 : 0)
                + (player.combatRightBranchNodes >= 1 ? 2 : 0);
            return new DiceFormula(d6Count, 6, flatBonus);
        }

        /// <summary>
        /// Inicia un encuentro de combate contra uno de los 3 Bosses.
        /// </summary>
        public void StartBossCombat(PlayerOcaToken attacker, BossDefinition boss, Action<CombatEncounter> onConcluded)
        {
            activeEncounter = new CombatEncounter
            {
                isBossCombat = true,
                attackerToken = attacker,
                targetBoss = boss,
                attackerFormula = GetPlayerCombatFormula(attacker.PlayerData),
                defenderFormula = boss.combatStrength,
                attackerFormulaText = DescribePlayerCombatFormula(attacker.PlayerData),
                defenderFormulaText = boss.combatStrength.ToString(),
                hasRolled = false,
                resultSummary = $"¡{attacker.PlayerData.playerName} se topó con el Boss {boss.displayName} en la casilla {boss.tileIndex}! Puedes usar objetos/hechizos o lanzar los dados."
            };
            onEncounterConcludedCallback = onConcluded;
            OnCombatStarted?.Invoke(activeEncounter);
        }

        /// <summary>
        /// Inicia un encuentro de combate PvP contra otro jugador.
        /// </summary>
        public void StartPvPCombat(PlayerOcaToken attacker, PlayerOcaToken defender, Action<CombatEncounter> onConcluded)
        {
            activeEncounter = new CombatEncounter
            {
                isBossCombat = false,
                attackerToken = attacker,
                defenderPlayerToken = defender,
                attackerFormula = GetPlayerCombatFormula(attacker.PlayerData),
                defenderFormula = GetPlayerCombatFormula(defender.PlayerData),
                attackerFormulaText = DescribePlayerCombatFormula(attacker.PlayerData),
                defenderFormulaText = DescribePlayerCombatFormula(defender.PlayerData),
                hasRolled = false,
                resultSummary = $"¡Duelo de Ocas en la casilla {attacker.CurrentTileIndex}! {attacker.PlayerData.playerName} desafía a {defender.PlayerData.playerName}."
            };
            onEncounterConcludedCallback = onConcluded;
            OnCombatStarted?.Invoke(activeEncounter);
        }

        private void RefreshActiveEncounterFormulas()
        {
            if (activeEncounter == null) return;
            PlayerSetupData att = activeEncounter.attackerToken.PlayerData;
            activeEncounter.attackerFormula = GetPlayerCombatFormula(att, activeEncounter.attackerExtraCombatBonus);
            activeEncounter.attackerFormulaText = DescribePlayerCombatFormula(att, activeEncounter.attackerExtraCombatBonus);

            if (activeEncounter.isBossCombat && activeEncounter.targetBoss != null)
            {
                DiceFormula bForm = activeEncounter.targetBoss.combatStrength;
                bForm.flatBonus += activeEncounter.defenderPenaltyBonus;
                activeEncounter.defenderFormula = bForm;
                activeEncounter.defenderFormulaText = bForm.ToString();
            }
            else if (activeEncounter.defenderPlayerToken != null)
            {
                PlayerSetupData def = activeEncounter.defenderPlayerToken.PlayerData;
                activeEncounter.defenderFormula = GetPlayerCombatFormula(def, activeEncounter.defenderPenaltyBonus);
                activeEncounter.defenderFormulaText = DescribePlayerCombatFormula(def, activeEncounter.defenderPenaltyBonus);
            }
        }

        /// <summary>
        /// Permite al jugador atacante usar un Objeto de Combate durante el duelo:
        /// - Armadura (+5 a la tirada de combate)
        /// - Espada (+8 a la tirada de combate)
        /// - Pentagrama (-1d6 a la tirada del oponente)
        /// - Carta UNO Reverse (revierte una derrota tras lanzar los dados)
        /// </summary>
        public bool TryUseCombatItem(ItemType itemType, out string message)
        {
            message = "";
            if (activeEncounter == null || activeEncounter.attackerToken == null) return false;

            PlayerSetupData attData = activeEncounter.attackerToken.PlayerData;
            if (!attData.inventory.Contains(itemType))
            {
                message = $"No tienes {itemType} en tu inventario.";
                return false;
            }

            switch (itemType)
            {
                case ItemType.Armadura:
                    if (activeEncounter.hasRolled && !activeEncounter.isTie)
                    {
                        message = "La Armadura debe usarse antes de definir el combate.";
                        return false;
                    }
                    attData.inventory.Remove(itemType);
                    activeEncounter.attackerExtraCombatBonus += 5;
                    RefreshActiveEncounterFormulas();
                    message = $"¡{attData.playerName} equipó Armadura (+5 de Fuerza en este combate)!";
                    activeEncounter.resultSummary = message;
                    OnCombatRolled?.Invoke(activeEncounter);
                    return true;

                case ItemType.Espada:
                    if (activeEncounter.hasRolled && !activeEncounter.isTie)
                    {
                        message = "La Espada debe usarse antes de definir el combate.";
                        return false;
                    }
                    attData.inventory.Remove(itemType);
                    activeEncounter.attackerExtraCombatBonus += 8;
                    RefreshActiveEncounterFormulas();
                    message = $"¡{attData.playerName} desenvainó la Espada (+8 de Fuerza en este combate)!";
                    activeEncounter.resultSummary = message;
                    OnCombatRolled?.Invoke(activeEncounter);
                    return true;

                case ItemType.Pentagrama:
                    if (activeEncounter.hasRolled && !activeEncounter.isTie)
                    {
                        message = "El Pentagrama debe usarse antes de definir el combate.";
                        return false;
                    }
                    attData.inventory.Remove(itemType);
                    int pen = UnityEngine.Random.Range(1, 7);
                    activeEncounter.defenderPenaltyBonus -= pen;
                    RefreshActiveEncounterFormulas();
                    message = $"¡{attData.playerName} invocó el Pentagrama! El oponente sufre -{pen} (-1d6) en su tirada.";
                    activeEncounter.resultSummary = message;
                    OnCombatRolled?.Invoke(activeEncounter);
                    return true;

                case ItemType.CartaUNOReverse:
                    if (!activeEncounter.hasRolled || activeEncounter.attackerWon)
                    {
                        message = "Usa la Carta Reversa del UNO cuando hayas perdido la tirada de combate para revertir la derrota.";
                        return false;
                    }
                    attData.inventory.Remove(itemType);
                    activeEncounter.attackerWon = true;
                    string defName = activeEncounter.isBossCombat
                        ? activeEncounter.targetBoss.displayName
                        : activeEncounter.defenderPlayerToken.PlayerData.playerName;
                    message = $"¡¡CARTA REVERSA DEL UNO!! ¡{attData.playerName} invierte el resultado y derrota a {defName}!";
                    activeEncounter.resultSummary = message;
                    OnCombatRolled?.Invoke(activeEncounter);
                    return true;

                default:
                    message = "Ese objeto no es de combate.";
                    return false;
            }
        }

        /// <summary>
        /// Permite al jugador atacante lanzar su Hechizo de Combate equipado (Flecha Mágica, Bola de Fuego o Eldritch Blast).
        /// </summary>
        public bool TryCastCombatSpell(out string message)
        {
            message = "";
            if (activeEncounter == null || activeEncounter.attackerToken == null) return false;
            if (activeEncounter.hasRolled && !activeEncounter.isTie)
            {
                message = "El combate ya fue decidido.";
                return false;
            }

            PlayerSetupData attData = activeEncounter.attackerToken.PlayerData;
            SpellDefinition spell = SpellsAndWildCardsSystem.GetSpellDefinition(attData.currentSpell);
            if (spell == null || !spell.isCombatSpell)
            {
                message = "No tienes un hechizo de combate equipado (Flecha Mágica, Bola de Fuego o Eldritch Blast).";
                return false;
            }

            if (attData.mana < spell.manaCost)
            {
                message = $"Maná insuficiente para {spell.displayName} (Requiere {spell.manaCost}, tienes {attData.mana}).";
                return false;
            }

            if (spell.spellType == SpellType.BolaDeFuego && activeEncounter.usedFireball)
            {
                message = "Bola de Fuego solo puede lanzarse 1 vez por combate.";
                return false;
            }

            if (spell.spellType == SpellType.EldritchBlast && activeEncounter.usedEldritchBlast)
            {
                message = "Eldritch Blast solo puede lanzarse 1 vez por combate.";
                return false;
            }

            attData.mana -= spell.manaCost;
            int spellRoll = 0;

            switch (spell.spellType)
            {
                case SpellType.FlechaMagica:
                    spellRoll = UnityEngine.Random.Range(1, 5); // 1d4
                    activeEncounter.attackerExtraCombatBonus += spellRoll;
                    message = $"¡{attData.playerName} lanzó Flecha Mágica (1 Maná) y sumó +{spellRoll} (1d4) a su ataque!";
                    break;

                case SpellType.BolaDeFuego:
                    spellRoll = UnityEngine.Random.Range(1, 11); // 1d10
                    activeEncounter.usedFireball = true;
                    activeEncounter.attackerExtraCombatBonus += spellRoll;
                    message = $"¡{attData.playerName} lanzó Bola de Fuego (1 Maná) y sumó +{spellRoll} (1d10) a su ataque!";
                    break;

                case SpellType.EldritchBlast:
                    spellRoll = UnityEngine.Random.Range(1, 7); // 1d6
                    activeEncounter.usedEldritchBlast = true;
                    activeEncounter.attackerExtraCombatBonus += spellRoll;
                    message = $"¡{attData.playerName} lanzó Eldritch Blast (0 Maná) y sumó +{spellRoll} (1d6) a su ataque!";
                    break;
            }

            RefreshActiveEncounterFormulas();
            activeEncounter.resultSummary = message;
            OnCombatRolled?.Invoke(activeEncounter);
            return true;
        }

        /// <summary>
        /// Ejecuta las tiradas de dados de ambos combatientes (incluyendo todos los dados del Árbol de Habilidades) y calcula el ganador.
        /// </summary>
        public void RollActiveCombat()
        {
            if (activeEncounter == null) return;

            RefreshActiveEncounterFormulas();

            PlayerSetupData attData = activeEncounter.attackerToken.PlayerData;
            activeEncounter.attackerTotal = SkillTreeAndAbilities.RollPlayerCombatStrength(
                attData,
                activeEncounter.attackerExtraCombatBonus,
                out activeEncounter.attackerRolls,
                out activeEncounter.attackerFormulaText
            );

            if (activeEncounter.isBossCombat && activeEncounter.targetBoss != null)
            {
                activeEncounter.defenderTotal = activeEncounter.defenderFormula.Roll(out activeEncounter.defenderRolls);
            }
            else if (activeEncounter.defenderPlayerToken != null)
            {
                activeEncounter.defenderTotal = SkillTreeAndAbilities.RollPlayerCombatStrength(
                    activeEncounter.defenderPlayerToken.PlayerData,
                    activeEncounter.defenderPenaltyBonus,
                    out activeEncounter.defenderRolls,
                    out activeEncounter.defenderFormulaText
                );
            }

            string attackerName = attData.playerName;
            string defenderName = activeEncounter.isBossCombat
                ? activeEncounter.targetBoss.displayName
                : activeEncounter.defenderPlayerToken.PlayerData.playerName;

            int attFlat = CalculateTotalFlatBonus(attData, activeEncounter.attackerExtraCombatBonus);
            int defFlat = activeEncounter.isBossCombat
                ? activeEncounter.defenderFormula.flatBonus
                : CalculateTotalFlatBonus(activeEncounter.defenderPlayerToken.PlayerData, activeEncounter.defenderPenaltyBonus);

            string attRollsStr = FormatRolls(activeEncounter.attackerRolls, attFlat, activeEncounter.attackerTotal);
            string defRollsStr = FormatRolls(activeEncounter.defenderRolls, defFlat, activeEncounter.defenderTotal);

            // Verificar si el defensor en PvP tiene Carta UNO Reverse y perdió
            if (activeEncounter.attackerTotal == activeEncounter.defenderTotal)
            {
                activeEncounter.isTie = true;
                activeEncounter.hasRolled = false; // Permitir volver a tirar para desempatar
                activeEncounter.resultSummary =
                    $"¡EMPATE ({activeEncounter.attackerTotal} a {activeEncounter.defenderTotal})!\n" +
                    $"{attackerName}: {attRollsStr}  vs  {defenderName}: {defRollsStr}.\n" +
                    "¡Vuelve a lanzar los dados para desempatar!";
            }
            else
            {
                activeEncounter.isTie = false;
                activeEncounter.hasRolled = true;
                activeEncounter.attackerWon = activeEncounter.attackerTotal > activeEncounter.defenderTotal;

                int expRewardAtt = attData.selectedOca == OcaType.ElXoca ? 4 : 3;

                if (activeEncounter.attackerWon)
                {
                    activeEncounter.resultSummary =
                        $"¡VICTORIA DE {attackerName.ToUpper()}! ({activeEncounter.attackerTotal} vs {activeEncounter.defenderTotal})\n" +
                        $"{attackerName} recibe +{expRewardAtt} de Experiencia y {defenderName} es derrotado.";
                }
                else
                {
                    int expRewardDef = (!activeEncounter.isBossCombat && activeEncounter.defenderPlayerToken.PlayerData.selectedOca == OcaType.ElXoca) ? 4 : 3;
                    activeEncounter.resultSummary =
                        $"¡DERROTA! {defenderName} gana el combate ({activeEncounter.defenderTotal} vs {activeEncounter.attackerTotal}).\n" +
                        (activeEncounter.isBossCombat
                            ? $"¡{attackerName} muere ante {defenderName} y es enviado al Infierno!"
                            : $"¡{defenderName} recibe +{expRewardDef} de Experiencia y {attackerName} muere!");
                }
            }

            OnCombatRolled?.Invoke(activeEncounter);
        }

        private static int CalculateTotalFlatBonus(PlayerSetupData pData, int extra)
        {
            if (pData == null) return extra;
            int flat = pData.combatFlatBonus + extra;
            if (pData.combatLeftBranchNodes >= 1) flat += 1;
            if (pData.combatRightBranchNodes >= 1) flat += 2;
            return flat;
        }

        /// <summary>
        /// Cierra el combate resuelto, marca al Boss como derrotado si corresponde y notifica al GameSessionManager.
        /// </summary>
        public void ConcludeActiveCombat()
        {
            if (activeEncounter == null || !activeEncounter.hasRolled) return;

            CombatEncounter finished = activeEncounter;
            var callback = onEncounterConcludedCallback;

            if (finished.isBossCombat && finished.attackerWon && finished.targetBoss != null)
            {
                MarkBossDefeated(finished.targetBoss.bossType);
            }

            activeEncounter = null;
            onEncounterConcludedCallback = null;

            OnCombatEnded?.Invoke(finished);
            callback?.Invoke(finished);
        }

        private void MarkBossDefeated(BossType type)
        {
            foreach (var b in bosses)
            {
                if (b.bossType == type)
                {
                    b.isDefeated = true;
                }
            }

            foreach (var token in spawnedBossTokens)
            {
                if (token != null && token.BossData != null && token.BossData.bossType == type)
                {
                    token.SetDefeated(true);
                }
            }
        }

        public static string FormatRolls(List<int> rolls, int bonus, int total)
        {
            if (rolls == null || rolls.Count == 0) return total.ToString();
            string dicePart = "[" + string.Join(" + ", rolls) + "]";
            if (bonus > 0) return $"{dicePart} +{bonus} = {total}";
            if (bonus < 0) return $"{dicePart} {bonus} = {total}";
            return $"{dicePart} = {total}";
        }
    }
}
