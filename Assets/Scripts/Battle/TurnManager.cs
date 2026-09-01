using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 턴 시퀀싱을 관리한다.
/// StartTurn/EndTurn의 에너지·카운터·디버프·드로우·코어 처리.
/// </summary>
public class TurnManager : MonoBehaviour
{
    private const string RansomwareKeyword = "ransomware";
    private const int RansomwareBaseDamage = 5;
    private const int RansomwareDamageIncrease = 5;

    // 양쪽 덱이 모두 소진되면 매 턴 시작 시 손패에 추가되는 교착 해소용 특수 카드.
    private const string StruggleCardId = "STATUS_STRUGGLE";
    // 발버둥이 턴 시작 시 손패에 남아있을 때 입는 최대 체력 비율(%) 피해.
    private const int StrugglePercentDamage = 10;

    public static TurnManager Instance { get; private set; }

    public bool IsPlayerTurn { get; private set; } = true;
    public int TurnNumber { get; private set; }
    public bool IsTurnTransitionInProgress { get; private set; }
    public int LastStartTurnDrawCount { get; private set; }

    [Header("Turn Flow")]
    [SerializeField] private float enemyTurnLeadDelay = 0.45f;
    [SerializeField] private float nextPlayerTurnLeadDelay = 0.45f;
    [SerializeField] private TurnBannerVfx turnBannerVfx;
    [SerializeField] private BattleInitializer battleInitializer;

    /// <summary>턴 시작 이벤트 (해당 엔티티 전달)</summary>
    public event Action<EntityState> OnTurnStart;

    /// <summary>턴 종료 이벤트 (해당 엔티티 전달)</summary>
    public event Action<EntityState> OnTurnEnd;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        ResolveTurnBannerVfx();
        if (battleInitializer == null)
            battleInitializer = GetComponent<BattleInitializer>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===================================================
    //  첫 턴
    // ===================================================

    /// <summary>
    /// 첫 턴을 시작한다. 멀리건 완료 후 BattleInitializer에서 호출.
    /// </summary>
    public void BeginFirstTurn()
    {
        TurnNumber = 1;
        IsPlayerTurn = true;
        IsTurnTransitionInProgress = false;
        StartTurn(BattleEngine.Instance.Player);
    }

    // ===================================================
    //  턴 시작 (계획서 StartTurn 흐름)
    // ===================================================

    /// <summary>
    /// 턴 시작 처리.
    /// 에너지 → 카운터 초기화 → 상대 부식 실드 감소(누적형) → 드로우 → 코어(turnStart) → 오버클럭 감소.
    /// </summary>
    public void StartTurn(EntityState entity)
    {
        var engine = BattleEngine.Instance;
        if (engine == null || engine.IsBattleEnded) return;
        LastStartTurnDrawCount = 0;

        // -- 에너지 = baseEnergy - overflowNext (이월 처리) --
        if (entity.energyCarryOver)
            entity.energy = Mathf.Min(entity.energy + entity.baseEnergy, 10);
        else
            entity.energy = entity.baseEnergy;

        entity.energy = Mathf.Max(0, entity.energy - entity.overflowNext);
        entity.overflowNext = 0;

        entity.energy = Mathf.Max(0, entity.energy - entity.energyDrainNext);
        entity.energyDrainNext = 0;

        // -- 턴 내 카운터 초기화 --
        entity.ResetTurnCounters();
        if (entity.virusOnCardPlayedNextTurn > 0)
        {
            entity.Turn.virusOnCardPlayedThisTurn = entity.virusOnCardPlayedNextTurn;
            entity.virusOnCardPlayedNextTurn = 0;
        }

        // -- 부식 데미지 → 상대방 실드 감소 후 스택 -1 --
        // 내 턴이 시작될 때 상대의 부식이 발동. 틱 후 스택이 1씩 감소.
        var opp = entity.opponent;
        if (opp != null && opp.corrosion > 0)
        {
            opp.shield = Mathf.Max(0, opp.shield - opp.corrosion);
            opp.corrosion--;
        }

        // -- 드로우 (baseDraw + extraDraw - 현재 손패) --
        int drawCount = entity.baseDraw + entity.extraDraw - entity.hand.Count;
        drawCount = Mathf.Max(0, drawCount);
        if (drawCount > 0)
            LastStartTurnDrawCount = engine.DrawCards(entity, drawCount);

        // -- 발버둥: 직전 턴부터 손패에 남아있던 카드는 턴 시작 시 최대 체력 10% 피해 --
        //    (이번 턴 새로 추가되는 카드는 제외하기 위해 드로우 전에 검사한다) --
        ApplyStruggleStartTurnDamage(entity, engine);
        if (engine.IsBattleEnded)
            return;

        // -- 발버둥: 이번 턴 드로우를 시도했으나 0장 뽑힌 경우(덱 소진) 손패에 추가 --
        ApplyStruggleDraw(entity, engine, wantedDraw: drawCount, drawnThisTurn: LastStartTurnDrawCount);

        ApplyRansomwareStartTurnDamage(entity, engine);
        if (engine.IsBattleEnded)
            return;

        // -- 지연 액션 실행 (nextTurnStart) --
        ProcessDeferredActions("nextTurnStart", entity);

        // -- 오버클럭 스택 감소 (-1) --
        // '모든 제한 해제' 효과(overclockUnlimited)가 없을 때만 감소하며, 1 미만으로는 내려가지 않는다.
        if (entity.overclockStacks > 1 && !entity.overclockUnlimited)
            entity.overclockStacks--;

        // -- 코어 효과 (turnStart) --
        engine.ApplyCoreEffects("turnStart", entity);

        OnTurnStart?.Invoke(entity);
        engine.NotifyStateChanged();

        string entityName = entity == engine.Player ? "플레이어" : "적";
        BattleLogger.Log(BattleLogType.Info,
            $"=== 턴 {TurnNumber} 시작 ({entityName}) - 에너지 {entity.energy}, 손패 {entity.hand.Count}장 ===",
            GetLogActor(entity, engine));

        Debug.Log($"[TurnManager] 턴 {TurnNumber} 시작 " +
                  $"(에너지: {entity.energy}, 손패: {entity.hand.Count}장)");
    }

    private static BattleLogActor GetLogActor(EntityState entity, BattleEngine engine)
    {
        if (engine == null || entity == null)
            return BattleLogActor.Neutral;

        if (entity == engine.Player)
            return BattleLogActor.Player;

        if (entity == engine.Enemy)
            return BattleLogActor.Enemy;

        return BattleLogActor.Neutral;
    }

    /// <summary>
    /// 플레이어와 적의 드로우 더미가 모두 비었으면, 턴을 시작하는 엔티티의 손패에
    /// 발버둥 카드를 1장 추가한다. 양쪽이 더 이상 드로우할 카드가 없는 교착 상태를 해소한다.
    /// </summary>
    private static void ApplyStruggleDraw(EntityState entity, BattleEngine engine, int wantedDraw, int drawnThisTurn)
    {
        if (entity == null || engine == null || engine.IsBattleEnded)
            return;

        // 이번 턴 드로우를 시도했으나(필요량 > 0) 실제로 0장을 뽑은 경우에만 발동.
        // 손패가 차서 애초에 드로우가 필요 없던 경우(wantedDraw == 0)는 제외한다.
        if (wantedDraw <= 0 || drawnThisTurn > 0)
            return;

        // 손패가 가득 차 있으면 추가하지 않는다 (최대 10장).
        if (entity.hand.Count >= 10)
            return;

        CardData struggle = CardDatabase.Instance.GetById(StruggleCardId);
        if (struggle == null)
        {
            Debug.LogWarning($"[TurnManager] 발버둥 카드('{StruggleCardId}')를 찾을 수 없어 추가하지 못했습니다.");
            return;
        }

        entity.hand.Add(struggle.Clone());

        string entityName = entity == engine.Player ? "플레이어" : "적";
        BattleLogger.Log(BattleLogType.Info,
            $"덱 소진: {entityName} 손패에 발버둥 1장 추가",
            GetLogActor(entity, engine));
    }

    /// <summary>
    /// 턴 시작 시 손패에 발버둥 카드가 남아있으면, 카드 1장당 최대 체력의 10% 피해를 입힌다.
    /// 실드를 먼저 소모한 뒤 남은 피해를 HP에서 차감한다 (랜섬웨어와 동일한 처리).
    /// </summary>
    private static void ApplyStruggleStartTurnDamage(EntityState entity, BattleEngine engine)
    {
        if (entity == null || engine == null || entity.hand == null || entity.hand.Count == 0)
            return;

        BattleLogActor actor = GetLogActor(entity, engine);
        string entityName = entity == engine.Player ? "플레이어" : "적";

        for (int i = 0; i < entity.hand.Count; i++)
        {
            CardData card = entity.hand[i];
            if (card == null || card.id != StruggleCardId)
                continue;

            int damage = Mathf.Max(1, Mathf.RoundToInt(entity.maxHp * (StrugglePercentDamage / 100f)));
            int hpBefore = entity.hp;
            int shieldBefore = entity.shield;
            int shieldAbsorb = Mathf.Min(entity.shield, damage);
            entity.shield -= shieldAbsorb;
            int hpLoss = damage - shieldAbsorb;
            if (hpLoss > 0)
                entity.TakeDamageRaw(hpLoss);

            string shieldInfo = shieldAbsorb > 0 ? $", 방어도 흡수 {shieldAbsorb} ({shieldBefore}→{entity.shield})" : "";
            BattleLogger.Log(BattleLogType.Damage,
                $"{BattleLogger.CardRef(card)}: {entityName} 최대 체력 {StrugglePercentDamage}% 피해 {damage} (HP {hpBefore}→{entity.hp}{shieldInfo})",
                actor);

            engine.CheckBattleEnd();
            if (engine.IsBattleEnded)
                return;
        }
    }

    private static void ApplyRansomwareStartTurnDamage(EntityState entity, BattleEngine engine)
    {
        if (entity == null || engine == null || entity.hand == null || entity.hand.Count == 0)
            return;

        BattleLogActor actor = GetLogActor(entity, engine);
        string entityName = entity == engine.Player ? "플레이어" : "적";

        for (int i = 0; i < entity.hand.Count; i++)
        {
            CardData card = entity.hand[i];
            if (card == null || !card.HasKeyword(RansomwareKeyword))
                continue;

            int damage = card.ransomwareDamage > 0 ? card.ransomwareDamage : RansomwareBaseDamage;
            int hpBefore = entity.hp;
            int shieldBefore = entity.shield;
            int shieldAbsorb = Mathf.Min(entity.shield, damage);
            entity.shield -= shieldAbsorb;
            int hpLoss = damage - shieldAbsorb;
            if (hpLoss > 0)
                entity.TakeDamageRaw(hpLoss);
            card.ransomwareDamage = damage + RansomwareDamageIncrease;

            string shieldInfo = shieldAbsorb > 0 ? $", 방어도 흡수 {shieldAbsorb} ({shieldBefore}→{entity.shield})" : "";
            BattleLogger.Log(BattleLogType.Damage,
                $"{BattleLogger.CardRef(card)}: {entityName} 피해 {damage} (HP {hpBefore}→{entity.hp}{shieldInfo}, 다음 피해 {card.ransomwareDamage})",
                actor);

            engine.CheckBattleEnd();
            if (engine.IsBattleEnded)
                return;
        }
    }

    // ===================================================
    //  턴 종료 (계획서 EndTurn 흐름)
    // ===================================================

    /// <summary>
    /// 턴 종료 처리.
    /// HP 페널티 → 코어(turnEnd) → 약화 감소.
    /// </summary>
    public void EndTurn(EntityState entity)
    {
        var engine = BattleEngine.Instance;
        if (engine == null || engine.IsBattleEnded) return;

        // -- 턴 종료 HP 페널티 (gambleDebt 등) --
        if (entity.turnEndHpPenalty > 0)
        {
            entity.hp = Mathf.Max(0, entity.hp - entity.turnEndHpPenalty);
            Debug.Log($"[TurnManager] 턴 종료 HP 페널티: {entity.turnEndHpPenalty}");
            entity.turnEndHpPenalty = 0;
        }

        if (entity.Turn.luckyDayDebtActive && entity.Turn.luckyDaySuccessCount > 0)
        {
            int hpLoss = entity.Turn.luckyDaySuccessCount * Mathf.Max(1, entity.Turn.luckyDayHpLossPerSuccess);
            entity.TakeDamageRaw(hpLoss);
            Debug.Log($"[TurnManager] 운수 좋은 날 대가: 성공 {entity.Turn.luckyDaySuccessCount}회 x {entity.Turn.luckyDayHpLossPerSuccess} → HP {hpLoss} 손실");
        }

        // -- 임시 교체 손패 정리 --
        for (int i = entity.hand.Count - 1; i >= 0; i--)
        {
            if (entity.hand[i].isTemporary)
            {
                Debug.Log($"[TurnManager] 임시 카드 소멸: {entity.hand[i].cardName}");
                entity.voidPile.Add(entity.hand[i]);
                entity.hand.RemoveAt(i);
            }
        }

        // -- 지연 액션 실행 (turnEnd) --
        ProcessDeferredActions("turnEnd", entity);

        // -- 코어 효과 (turnEnd) --
        engine.ApplyCoreEffects("turnEnd", entity);

        // -- 약화 자연 감소: 대상이 자기 턴을 보낸 뒤 감소 --
        ApplyEndTurnWeaknessDecay(entity);

        RestoreRollbackHand(entity);

        OnTurnEnd?.Invoke(entity);
        engine.NotifyStateChanged();
    }

    // ===================================================
    //  플레이어 턴 종료 (전체 사이클)
    // ===================================================

    /// <summary>
    /// 플레이어 턴 종료 → 적 턴 (스텁) → 다음 플레이어 턴 시작.
    /// 8단계 EnemyAI 도입 시 적 턴에 카드 사용 로직이 추가된다.
    /// </summary>
    public void EndPlayerTurn()
    {
        var engine = BattleEngine.Instance;
        if (!IsPlayerTurn) return;
        if (IsTurnTransitionInProgress) return;
        if (engine == null || engine.IsBattleEnded) return;

        Debug.Log($"[TurnManager] 턴 {TurnNumber} 종료");
        StartCoroutine(RunEndPlayerTurnCycle());
    }

    private IEnumerator RunEndPlayerTurnCycle()
    {
        var engine = BattleEngine.Instance;
        if (engine == null || engine.IsBattleEnded)
            yield break;

        IsTurnTransitionInProgress = true;

        EndTurn(engine.Player);
        if (engine.IsBattleEnded)
        {
            IsTurnTransitionInProgress = false;
            yield break;
        }

        IsPlayerTurn = false;

        if (engine.IsTrainingMode)
        {
            BattleLogger.Log(BattleLogType.Info, "-- 허수아비 턴 스킵 --", BattleLogActor.Enemy);
        }
        else
        {
            StartTurn(engine.Enemy);
            if (engine.IsBattleEnded)
            {
                IsTurnTransitionInProgress = false;
                yield break;
            }

            yield return StartCoroutine(WaitForEnemyTurnIntro());

            BattleLogger.Log(BattleLogType.Info, "-- 적 행동 시작 --", BattleLogActor.Enemy);

            int enemyCardsPlayed = 0;
            if (BattleSceneData.IsTutorial && TutorialManager.Instance != null
                && TutorialManager.Instance.BattleController != null
                && TutorialManager.Instance.BattleController.UseScriptedEnemyTurns)
            {
                enemyCardsPlayed = TutorialManager.Instance.BattleController
                    .PlayScriptedEnemyTurn(TurnNumber, engine.Enemy, engine.Player);
            }
            else if (EnemyAI.Instance != null)
                yield return StartCoroutine(PlayEnemyAITurn(engine, count => enemyCardsPlayed = count));
            else
                BattleLogger.Log(BattleLogType.Warning, "적 AI가 없어 행동을 실행하지 못했습니다.", BattleLogActor.Enemy);

            if (enemyCardsPlayed == 0 && !engine.IsBattleEnded)
                BattleLogger.Log(BattleLogType.Info, "-- 적: 사용 가능한 카드 없음 --", BattleLogActor.Enemy);

            BattleLogger.Log(BattleLogType.Info,
                $"-- 적 행동 종료 (에너지 {engine.Enemy.energy}, 손패 {engine.Enemy.hand.Count}장) --",
                BattleLogActor.Enemy);

            EndTurn(engine.Enemy);

            if (BattleSceneData.IsTutorial && TutorialManager.Instance != null)
                TutorialManager.Instance.NotifyCondition(TutorialCondition.EnemyTurnEnd);

            if (engine.IsBattleEnded)
            {
                IsTurnTransitionInProgress = false;
                yield break;
            }
        }

        IsPlayerTurn = true;
        TurnNumber++;

        if (nextPlayerTurnLeadDelay > 0f)
            yield return new WaitForSeconds(nextPlayerTurnLeadDelay);

        IsTurnTransitionInProgress = false;
        StartTurn(engine.Player);
    }

    // ===================================================
    //  지연 액션 처리
    // ===================================================

    /// <summary>
    /// 지정된 타이밍의 지연 액션을 모두 실행하고 큐에서 제거한다.
    /// 실행 중 새 지연 액션이 추가될 수 있으므로 복사본을 순회한다.
    /// </summary>
    private void ProcessDeferredActions(string timing, EntityState entity)
    {
        var engine = BattleEngine.Instance;
        if (engine == null || engine.IsBattleEnded) return;
        if (entity.deferredActions.Count == 0) return;

        // 해당 타이밍의 액션만 추출
        var toExecute = new List<DeferredAction>();
        for (int i = entity.deferredActions.Count - 1; i >= 0; i--)
        {
            if (entity.deferredActions[i].timing == timing)
            {
                toExecute.Add(entity.deferredActions[i]);
                entity.deferredActions.RemoveAt(i);
            }
        }

        if (toExecute.Count == 0) return;

        // 등록 순서대로 실행 (역순 추출했으므로 뒤집기)
        toExecute.Reverse();

        foreach (var da in toExecute)
        {
            if (engine.IsBattleEnded) break;

            var ctx = new EffectContext
            {
                Caster = da.caster,
                Target = da.target,
                Effect = da.effect,
                Engine = engine,
                Card = da.card,
            };

            Debug.Log($"[TurnManager] 지연 액션 실행 ({timing}): {da.effect.type}");
            engine.Interpreter.Execute(ctx);
        }
    }

    private static void ApplyEndTurnWeaknessDecay(EntityState entity)
    {
        if (entity == null || entity.weakness <= 0) return;

        int decay = 1;
        if (entity.activeCores != null)
        {
            foreach (var core in entity.activeCores)
            {
                if (core.coreEffect?.coreType == "extraDebuffReduction")
                    decay += Mathf.Max(1, (int)core.coreEffect.value);
            }
        }

        entity.weakness = Mathf.Max(0, entity.weakness - decay);
    }

    private static void RestoreRollbackHand(EntityState entity)
    {
        if (entity == null || entity.rollbackStoredHand == null || entity.rollbackStoredHand.Count == 0)
            return;

        entity.hand.AddRange(entity.rollbackStoredHand);
        entity.rollbackStoredHand.Clear();
    }

    private float GetEnemyTurnLeadDelay()
    {
        ResolveTurnBannerVfx();

        if (turnBannerVfx == null)
            return enemyTurnLeadDelay;

        return Mathf.Max(enemyTurnLeadDelay, turnBannerVfx.GetPlaybackDuration(false));
    }

    private IEnumerator WaitForEnemyTurnIntro()
    {
        ResolveTurnBannerVfx();

        float enemyLeadDelay = GetEnemyTurnLeadDelay();
        if (enemyLeadDelay > 0f)
            yield return new WaitForSeconds(enemyLeadDelay);

        while (turnBannerVfx != null && turnBannerVfx.IsPlaying)
            yield return null;
    }

    private void ResolveTurnBannerVfx()
    {
        if (turnBannerVfx != null)
            return;

        if (BattleManagerRoot.Instance != null)
            turnBannerVfx = BattleManagerRoot.Instance.TurnBannerVfx;

        if (turnBannerVfx == null)
            turnBannerVfx = GetComponentInChildren<TurnBannerVfx>();

        if (turnBannerVfx == null)
            turnBannerVfx = FindFirstObjectByType<TurnBannerVfx>();
    }

    private IEnumerator PlayEnemyAITurn(BattleEngine engine, Action<int> onCompleted)
    {
        int playedCount = 0;

        if (engine == null || EnemyAI.Instance == null)
        {
            onCompleted?.Invoke(playedCount);
            yield break;
        }

        int safetyLimit = 30;
        while (safetyLimit-- > 0)
        {
            if (engine.Enemy.forceEndTurn) break;
            if (engine.IsBattleEnded) break;

            int bestIndex = EnemyAI.Instance.SelectBestPlayableCard(engine.Enemy, engine.Player);
            if (bestIndex < 0) break;

            CardData card = engine.Enemy.hand[bestIndex];
            if (battleInitializer != null)
            {
                yield return StartCoroutine(battleInitializer.PlayEnemyCardUseVfxAndExecute(
                    bestIndex, card, engine.Enemy, engine.Player));
            }
            else
            {
                engine.PlayCard(bestIndex, engine.Enemy, engine.Player);
            }

            playedCount++;
        }

        onCompleted?.Invoke(playedCount);
    }
}
