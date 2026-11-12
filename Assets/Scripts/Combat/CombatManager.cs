using System;
using System.Collections.Generic;
using UnityEngine;
using Oca2.Board;
using Oca2.Core;
using Oca2.Data;
using Oca2.Players;

namespace Oca2.Combat
{
    /// <summary>
    /// Gestor del Sistema de Combate y de los 3 Bosses de La Oca 2: Electric Boogaloo (TP2 pág. 6, 8, 17, 18).
    /// Administra:
    /// - Los 3 Bosses del Tablero Principal: La Foca (casilla 13, 1d8), La Roca (casilla 31, 2d6) y La Trollface (casilla 52, 3d6).
    /// - Intercepción automática cuando un jugador pasa por encima de una casilla con un Boss vivo.
    /// - Combates PvP cuando un jugador cae en la casilla de otro jugador (excepto START e Infierno 2).
    /// - Resolución por dados de fuerza, otorgando +3 de Experiencia al ganador y enviando al perdedor al Infierno.
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
        /// Registra los 3 Bosses oficiales del documento TP2 con sus casillas y dados de fuerza exactos:
        /// - La Foca (Casilla 13): 1d8
        /// - La Roca (Casilla 31): 2d6
        /// - La Trollface (Casilla 52): 3d6
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
                    tileIndex = 31,
                    combatStrength = new DiceFormula(2, 6, 0), // 2d6 (pág. 18)
                    assetFileName = "La roca.png",
                    description = "Segundo Boss (Casilla 31). Tiene una fuerza de combate de 2d6 (dos dados de 6 caras).",
                    isDefeated = false
                });
                bosses.Add(new BossDefinition
                {
                    bossType = BossType.LaTrollface,
                    displayName = "La Trollface",
                    tileIndex = 52,
                    combatStrength = new DiceFormula(3, 6, 0), // 3d6 (pág. 18)
                    assetFileName = "Trollface.png",
                    description = "Boss Final (Casilla 52). Tiene una fuerza de combate de 3d6 (tres dados de 6 caras).",
                    isDefeated = false
                });
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
        /// Instancia las figuras 3D de los 3 Bosses en las casillas 13, 31 y 52 del Tablero Principal.
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
        /// Calcula la fórmula de dados de combate de un jugador.
        /// Por defecto las Ocas tienen 1d6 de fuerza (TP2 pág. 17) más bonificaciones por nivel de EXP u objetos.
        /// </summary>
        public DiceFormula GetPlayerCombatFormula(PlayerSetupData player)
        {
            if (player == null) return new DiceFormula(1, 6, 0);

            // Bonificación progresiva por nivel de experiencia (+1 cada 4 niveles superados) más bonos directos
            int levelAttackBonus = (player.expLevel - 1) / 4;
            int totalFlatBonus = player.combatFlatBonus + levelAttackBonus;

            return new DiceFormula(1, 6, totalFlatBonus);
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
                hasRolled = false,
                resultSummary = $"¡{attacker.PlayerData.playerName} se topó con el Boss {boss.displayName} en la casilla {boss.tileIndex}! Lanza los dados para combatir."
            };
            onEncounterConcludedCallback = onConcluded;
            OnCombatStarted?.Invoke(activeEncounter);
        }

        /// <summary>
        /// Inicia un encuentro de combate PvP contra otro jugador en la misma casilla.
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
                hasRolled = false,
                resultSummary = $"¡Duelo de Ocas en la casilla {attacker.CurrentTileIndex}! {attacker.PlayerData.playerName} desafía a {defender.PlayerData.playerName}."
            };
            onEncounterConcludedCallback = onConcluded;
            OnCombatStarted?.Invoke(activeEncounter);
        }

        /// <summary>
        /// Ejecuta las tiradas de dados de ambos combatientes y calcula el ganador.
        /// </summary>
        public void RollActiveCombat()
        {
            if (activeEncounter == null) return;

            activeEncounter.attackerTotal = activeEncounter.attackerFormula.Roll(out activeEncounter.attackerRolls);
            activeEncounter.defenderTotal = activeEncounter.defenderFormula.Roll(out activeEncounter.defenderRolls);

            string attackerName = activeEncounter.attackerToken.PlayerData.playerName;
            string defenderName = activeEncounter.isBossCombat
                ? activeEncounter.targetBoss.displayName
                : activeEncounter.defenderPlayerToken.PlayerData.playerName;

            string attRollsStr = FormatRolls(activeEncounter.attackerRolls, activeEncounter.attackerFormula.flatBonus, activeEncounter.attackerTotal);
            string defRollsStr = FormatRolls(activeEncounter.defenderRolls, activeEncounter.defenderFormula.flatBonus, activeEncounter.defenderTotal);

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

                if (activeEncounter.attackerWon)
                {
                    activeEncounter.resultSummary =
                        $"¡VICTORIA DE {attackerName.ToUpper()}! ({activeEncounter.attackerTotal} vs {activeEncounter.defenderTotal})\n" +
                        $"{attackerName} recibe +3 de Experiencia y {defenderName} es derrotado.";
                }
                else
                {
                    activeEncounter.resultSummary =
                        $"¡DERROTA! {defenderName} gana el combate ({activeEncounter.defenderTotal} vs {activeEncounter.attackerTotal}).\n" +
                        (activeEncounter.isBossCombat
                            ? $"¡{attackerName} muere ante {defenderName} y es enviado al Infierno!"
                            : $"¡{defenderName} recibe +3 de Experiencia y {attackerName} muere!");
                }
            }

            OnCombatRolled?.Invoke(activeEncounter);
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
