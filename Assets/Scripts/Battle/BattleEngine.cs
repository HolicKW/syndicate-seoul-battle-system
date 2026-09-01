using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 전체를 관장하는 싱글톤 엔진.
/// PlayCard 흐름, 데미지/드로우/해체, 프로토콜 판정, 코어 트리거를 처리한다.
/// </summary>
public class BattleEngine : MonoBehaviour
{
    private const string TemporaryKeyword = "일시적";

    public static BattleEngine Instance { get; private set; }

    // -- 엔티티 --
    public EntityState Player { get; private set; }
    public EntityState Enemy { get; private set; }

    /// <summary>훈련 모드(허수아비). 적이 공격하지 않고, 전투 종료 판정을 건너뛴다.</summary>
    public bool IsTrainingMode { get; set; }

    // -- 이펙트 시스템 --
    public EffectInterpreter Interpreter { get; private set; }

    // -- 선택 해체 사전 선택 결과 --
    /// <summary>chosen 모드 해체 시 플레이어가 미리 선택한 카드. HandleDismantle이 읽고 비운다.</summary>
    public List<CardData> PendingDismantleTargets { get; } = new List<CardData>();

    public DismantleVfxQueue DismantleVfxQueue { get; } = new DismantleVfxQueue();

    private readonly List<CardData> rebuiltCardsReturnedToHand = new List<CardData>();

    /// <summary>오픈 액세스 사용 전 플레이어가 선택한 카드 종류 필터.</summary>
    public string PendingOpenAccessFilter { get; private set; }

    // -- 이벤트 --

    /// <summary>상태 변경 시 UI 갱신용 이벤트</summary>
    public event Action OnStateChanged;

    /// <summary>전투 종료 (승: true, 패: false)</summary>
    public event Action<bool> OnBattleEnd;

    /// <summary>전투 로그</summary>
    public event Action<string> OnBattleLog;

    /// <summary>도박 판정 결과 (연출용). BattleInitializer가 구독해 GambleResultVfx로 전달한다.</summary>
    public event Action<GambleResultInfo> OnGambleResult;

    private bool battleEnded;
    public bool IsBattleEnded => battleEnded;

    public DeckController DeckCtrl { get; private set; }
    public CostCalculator CostCalc { get; private set; }
    public ProtocolResolver ProtocolResolver { get; private set; }
    private CardPlayProcessor cardPlayProcessor;

    private BattleJudge judge;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ===================================================
    //  초기화
    // ===================================================

    /// <summary>
    /// 전투를 초기화한다. BattleInitializer에서 호출.
    /// </summary>
    public void InitBattle(List<CardData> playerCards, int playerHp, int playerEnergy,
                           int enemyHp, int enemyEnergy)
    {
        if (Instance == null)
            Instance = this;

        battleEnded = false;
        PendingDismantleTargets.Clear();
        DismantleVfxQueue.Clear();
        rebuiltCardsReturnedToHand.Clear();

        // EntityState 생성
        Player = EntityState.Create(playerHp, playerEnergy);
        Enemy = EntityState.Create(enemyHp, enemyEnergy);
        Player.opponent = Enemy;
        Enemy.opponent = Player;

        // 덱 세팅
        Player.drawPile.AddRange(playerCards);
        ShuffleDeck(Player.drawPile);

        // EffectInterpreter 생성 및 핸들러 등록
        Interpreter = new EffectInterpreter();
        Interpreter.RegisterAllCoreHandlers();

        DeckCtrl = new DeckController(this);
        CostCalc = new CostCalculator(this);
        ProtocolResolver = new ProtocolResolver(this);
        cardPlayProcessor = new CardPlayProcessor(this);
        judge = new BattleJudge(this);

        Log($"전투 시작! 플레이어 HP:{playerHp} / 적 HP:{enemyHp}");
    }

    /// <summary>
    /// 적 덱을 설정한다. BattleInitializer에서 TempDeckFactory 결과를 전달할 때 사용.
    /// </summary>
    public void SetEnemyDeck(List<CardData> cards)
    {
        Enemy.drawPile.Clear();
        Enemy.drawPile.AddRange(cards);
        ShuffleDeck(Enemy.drawPile);
        Log($"적 덱 설정: {cards.Count}장");
    }

    /// <summary>
    /// 전투 시작 시 초기 드로우 (멀리건 전).
    /// </summary>
    public int InitialDraw(int count)
    {
        return DrawCards(Player, count);
    }

    /// <summary>
    /// 멀리건 처리 (블랙리스트 로직: 새 카드 먼저 뽑고, 반환 카드를 drawPile에 넣은 뒤 셔플).
    /// </summary>
    public bool TryMulligan(List<int> indices)
    {
        if (indices == null || indices.Count == 0)
            return true;

        var hand = Player.hand;
        var drawPile = Player.drawPile;

        // 인덱스 유효성 검증
        foreach (int idx in indices)
        {
            if (idx < 0 || idx >= hand.Count)
                return false;
        }

        // 중복 제거 + 내림차순 정렬 (인덱스 밀림 방지)
        var uniqueSet = new HashSet<int>(indices);
        var sorted = new List<int>(uniqueSet);
        sorted.Sort();
        sorted.Reverse();

        var removed = new List<CardData>();
        foreach (int idx in sorted)
        {
            removed.Add(hand[idx]);
            hand.RemoveAt(idx);
        }

        // 블랙리스트: 새 카드를 먼저 뽑음
        int drawCount = Mathf.Min(removed.Count, drawPile.Count);
        for (int i = 0; i < drawCount; i++)
        {
            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
        }

        // 반환된 카드를 drawPile에 넣고 셔플
        drawPile.AddRange(removed);
        ShuffleDeck(drawPile);

        Log($"멀리건: {removed.Count}장 교체 완료");
        NotifyStateChanged();
        return true;
    }

    // ===================================================
    //  카드 사용
    // ===================================================

    /// <summary>
    /// dynamicCost 조건을 평가해 실제 적용 코스트를 반환한다.
    /// </summary>
    public int GetEffectiveCost(CardData card, EntityState caster)
        => CostCalc.GetEffectiveCost(card, caster);

    /// <summary>
    /// 플레이어가 해당 손패 인덱스의 카드를 사용할 수 있는지 확인한다.
    /// </summary>
    public bool CanPlayCard(int handIndex)
        => CostCalc.CanPlayCard(handIndex, Player);

    /// <summary>
    /// 해당 엔티티가 손패 인덱스의 카드를 사용할 수 있는지 확인한다.
    /// </summary>
    public bool CanPlayCard(int handIndex, EntityState caster)
        => CostCalc.CanPlayCard(handIndex, caster);

    /// <summary>
    /// 플레이어가 카드를 사용한다.
    /// </summary>
    public void PlayCard(int handIndex)
    {
        PlayCard(handIndex, Player, Enemy);
    }

    /// <summary>
    /// 카드를 사용한다. CardPlayProcessor에 13단계 파이프라인을 위임.
    /// </summary>
    public void PlayCard(int handIndex, EntityState caster, EntityState target)
    {
        cardPlayProcessor.Execute(handIndex, caster, target);
    }

    public void SetPendingOpenAccessFilter(string filter)
    {
        PendingOpenAccessFilter = filter;
    }

    public string ConsumePendingOpenAccessFilter()
    {
        string filter = PendingOpenAccessFilter;
        PendingOpenAccessFilter = null;
        return filter;
    }

    // ===================================================
    //  데미지 / 드로우 / 해체 - 공용 메서드
    // ===================================================

    /// <summary>
    /// 데미지를 적용한다 (실드 우선 소모).
    /// </summary>
    public void ApplyDamage(EntityState target, int damage, EntityState attacker,
                            bool bypassShield = false, bool triggerRetaliation = true)
    {
        if (damage <= 0) return;

        // 무적 체크
        if (target.Turn.invincibleThisTurn) return;

        // 회피 체크: 다음 피해 인스턴스 1회를 무효화한다.
        if (target.Turn.evadeNextHits > 0)
        {
            target.Turn.evadeNextHits--;
            string evadeTargetName = target == Player ? "플레이어" : "적";
            BattleLogger.Log(BattleLogType.Effect, $"{evadeTargetName} 회피: 피해 {damage} 무효화");
            return;
        }

        // 데미지 감소 배율 체크
        if (target.Turn.damageReductionThisTurn > 0f)
        {
            damage = Mathf.RoundToInt(damage * (1f - target.Turn.damageReductionThisTurn));
            if (damage <= 0) return;
        }

        // 긴급 회피 패시브 체크 (BASE_014): 손패에 있으면 피해 반감 후 자동 해체
        var emergencyDodge = target.hand.Find(c => c.id == "BASE_014");
        if (emergencyDodge != null)
        {
            damage = Mathf.Max(1, damage / 2);
            target.hand.Remove(emergencyDodge);
            BattleLogger.Log(BattleLogType.Effect, $"긴급 회피 발동: 피해 반감 → {damage}. {BattleLogger.CardRef(emergencyDodge)} 자동 해체");
            DismantleCard(target, emergencyDodge, DismantleVfxSource.Hand);
        }

        int shieldAbsorb = 0;
        if (!bypassShield)
        {
            shieldAbsorb = Mathf.Min(target.shield, damage);
            target.shield -= shieldAbsorb;
        }
        int hpLoss = damage - shieldAbsorb;
        int hpBefore = target.hp;
        target.hp = Mathf.Max(0, target.hp - hpLoss);

        if (attacker != null)
            attacker.Turn.totalDamageThisTurn += damage;

        // 데미지 로그
        string targetName = target == Player ? "플레이어" : "적";
        string shieldInfo = shieldAbsorb > 0 ? $", 실드 흡수 {shieldAbsorb}" : "";
        string bypassInfo = bypassShield ? " [관통]" : "";
        BattleLogger.Log(BattleLogType.Damage,
            $"{targetName}에게 {damage} 데미지{bypassInfo} (HP {hpBefore}→{target.hp}{shieldInfo})");


        if (triggerRetaliation &&
            attacker != null &&
            attacker != target &&
            !target.IsDead &&
            target.Turn.retaliateOnHitDamage > 0)
        {
            int retaliationDamage = Mathf.Max(1,
                target.Turn.retaliateOnHitDamage + target.strength - target.weakness);
            target.Turn.retaliateOnHitDamage = 0;
            BattleLogger.Log(BattleLogType.Effect,
                $"{targetName} 반격: 공격자에게 {retaliationDamage} 피해");
            ApplyDamage(attacker, retaliationDamage, target, bypassShield: false, triggerRetaliation: false);
        }

        CheckBattleEnd();
    }

    /// <summary>
    /// 카드를 N장 드로우한다 (drawPile 뒤에서부터 = 상단).
    /// </summary>
    public int DrawCards(EntityState entity, int count)
    {
        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            if (entity.drawPile.Count == 0) break;
            if (entity.hand.Count >= 10) break;

            var card = entity.drawPile[0];
            entity.drawPile.RemoveAt(0);
            entity.hand.Add(card);
            entity.lastDrawnCard = card;
            drawn++;
        }
        return drawn;
    }

    /// <summary>
    /// 카드를 해체한다. 추출 트리거, 에너지 보너스, 카운터 증가, 코어 트리거 자동 처리.
    /// </summary>
    public void DismantleCard(EntityState entity, CardData card, DismantleVfxSource source = DismantleVfxSource.Unknown)
    {
        CommitDismantle(entity, card, source);
    }

    public void CommitDismantle(
        EntityState entity,
        CardData card,
        DismantleVfxSource source = DismantleVfxSource.Unknown,
        bool notify = true)
    {
        BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(card)}");
        RecordDismantleVfx(entity, card, source);

        // 추출 효과 (extractEffects)
        if (card.extractEffects != null && card.extractEffects.Count > 0)
        {
            var ctx = new EffectContext
            {
                Caster = entity,
                Target = entity.opponent,
                Card = card,
                Engine = this,
            };
            Interpreter.ExecuteAll(card.extractEffects, ctx);
        }

        // 추출 에너지 보너스 체크 (DM_025: 최대 횟수 체크)
        if (entity.Turn.extractEnergyBonusActive && entity.extractEnergyAmount > 0
            && entity.Turn.extractEnergyRemainingCount > 0)
        {
            entity.energy += entity.extractEnergyAmount;
            entity.Turn.extractEnergyRemainingCount--;
        }

        // 해체 카운터 증가
        entity.Turn.dismantledThisTurn++;
        entity.dismantledThisBattle++;

        // 재구축 or voidPile
        ApplyRebuildOrVoid(entity, card);

        // 코어 트리거
        CheckCoreTriggers("dismantle", entity, card);

        if (notify)
            NotifyStateChanged();
    }

    public void RecordDismantleVfx(EntityState entity, CardData card, DismantleVfxSource source)
    {
        DismantleVfxQueue.Enqueue(entity, card, source);
    }

    /// <summary>
    /// 카드를 해체/사용 후 처리한다.
    /// rebuildCount > 0이면 패로 복귀(재구축), 아니면 voidPile로 이동.
    /// HandleDismantle과 DismantleCard 양쪽에서 호출한다.
    /// </summary>
    public void ApplyRebuildOrVoid(EntityState entity, CardData card)
    {
        if (card.rebuildCount > 0)
        {
            card.rebuildCount--;
            entity.hand.Add(card);
            if (entity == Player)
                rebuiltCardsReturnedToHand.Add(card);
            entity.totalRebuildsThisBattle++;
            BattleLogger.Log(BattleLogType.Info,
                $"{BattleLogger.CardRef(card)} 재구축 ({card.rebuildCount}회 남음)");

            // rebuildAccum 키워드 카드(DM_021 등): 재구축마다 누적
            foreach (var handCard in entity.hand)
                if (handCard.HasKeyword("rebuildAccum"))
                    handCard.accumulationCount++;

            CheckCoreTriggers("rebuild", entity, card);
        }
        else
        {
            entity.voidPile.Add(card);

            // 플레이어 카드가 보이드로 이동 = 소모 처리
            if (entity == Player)
                BattleSceneData.ConsumedCardIds.Add(card.id);
        }
    }

    public List<CardData> ConsumeRebuiltCardsReturnedToHand()
    {
        var result = new List<CardData>(rebuiltCardsReturnedToHand);
        rebuiltCardsReturnedToHand.Clear();
        return result;
    }

    // ===================================================
    //  프로토콜 판정
    // ===================================================

    /// <summary>
    /// 프로토콜 조건을 판정하고 바이패스를 소모한다.
    /// PlayCard 흐름 전용. 조회 목적이라면 ProtocolResolver.Evaluate()를 직접 사용할 것.
    /// </summary>
    public bool CheckProtocolCondition(CardData card, EntityState caster)
        => ProtocolResolver.CheckAndConsume(card, caster);

    // ===================================================
    //  코어 카드 (6단계 CoreManager 도입 전 임시 구현)
    // ===================================================

    /// <summary>
    /// 특정 트리거에 해당하는 코어 효과를 발동한다.
    /// </summary>
    public void CheckCoreTriggers(string trigger, EntityState entity, CardData playedCard = null)
    {
        if (entity.activeCores == null || entity.activeCores.Count == 0) return;

        // CoreManager가 있으면 위임, 없으면 기본 effects[] 실행
        if (CoreManager.Instance != null)
        {
            CoreManager.Instance.HandleTrigger(trigger, entity, playedCard);
            return;
        }

        // Fallback: 기본 effects[] 실행
        foreach (var core in entity.activeCores)
        {
            if (core.coreEffect == null) continue;
            if (core.coreEffect.trigger != trigger) continue;
            if (core.coreEffect.effects == null) continue;

            var ctx = new EffectContext
            {
                Caster = entity,
                Target = entity.opponent,
                Card = core,
                Engine = this,
            };
            Interpreter.ExecuteAll(core.coreEffect.effects, ctx);
        }
    }

    /// <summary>
    /// turnStart/turnEnd 트리거의 코어 효과를 적용한다.
    /// </summary>
    public void ApplyCoreEffects(string trigger, EntityState entity)
    {
        CheckCoreTriggers(trigger, entity);
    }

    // ===================================================
    //  승패 판정
    // ===================================================

    /// <summary>
    /// 승패를 판정한다. PlayCard, ApplyDamage 후 호출.
    /// </summary>
    public void CheckBattleEnd()
    {
        var result = judge.Evaluate();
        if (result == BattleResult.None) return;

        battleEnded = true;
        bool won = result == BattleResult.Victory;
        if (won)
            CollectTemporaryCardsForBattleResult();

        string resultMessage = won ? "전투 승리!" : "전투 패배...";
        Log(resultMessage);
        BattleLogger.Log(BattleLogType.Info, resultMessage);

        if (BattleSceneData.IsTutorial && TutorialManager.Instance != null)
            TutorialManager.Instance.NotifyCondition(TutorialCondition.BattleWon);

        OnBattleEnd?.Invoke(won);
    }

    public void Surrender()
    {
        if (battleEnded || IsTrainingMode)
            return;

        battleEnded = true;

        const string resultMessage = "전투 항복...";
        Log(resultMessage);
        BattleLogger.Log(BattleLogType.Info, resultMessage);

        OnBattleEnd?.Invoke(false);
    }

    private void CollectTemporaryCardsForBattleResult()
    {
        if (Player == null) return;

        AddTemporaryCardsFromZone(Player.drawPile);
        AddTemporaryCardsFromZone(Player.hand);
        AddTemporaryCardsFromZone(Player.rollbackStoredHand);
    }

    private static void AddTemporaryCardsFromZone(List<CardData> cards)
    {
        if (cards == null) return;

        foreach (var card in cards)
        {
            if (card == null || string.IsNullOrEmpty(card.id)) continue;
            if (!card.HasKeyword(TemporaryKeyword)) continue;

            BattleSceneData.ConsumedCardIds.Add(card.id);
        }
    }

    // ===================================================
    //  유틸리티
    // ===================================================

    /// <summary>
    /// 도박 결과 이벤트를 CoreManager에 전달한다.
    /// EffectInterpreter.ResolveGamble에서 호출된다.
    /// </summary>
    public void NotifyGambleResult(EntityState entity, bool success, CardData card,
        float chancePct = 0f, int luck = 0, GamblePhase phase = GamblePhase.FirstRoll)
    {
        CoreManager.Instance?.OnGambleResult(entity, success, card);
        OnGambleResult?.Invoke(new GambleResultInfo(success, chancePct, luck, phase, entity == Player));
    }

    /// <summary>
    /// 주사위 결과를 연출 계층에만 통지한다(성공/실패가 아닌 눈금 표시).
    /// 도박 코어 트리거(CoreManager.OnGambleResult)는 거치지 않는다.
    /// </summary>
    public void NotifyDiceResult(EntityState entity, int rollValue, CardData card)
    {
        OnGambleResult?.Invoke(GambleResultInfo.Dice(rollValue, entity == Player));
    }

    /// <summary>
    /// 바이러스 소모 이벤트를 CoreManager에 전달한다.
    /// EffectInterpreter.HandleConsumeVirus에서 호출된다.
    /// </summary>
    public void NotifyVirusConsumed(EntityState entity, int amount)
    {
        entity.Turn.virusConsumedThisTurn += amount;
        CoreManager.Instance?.OnVirusConsumed(entity, amount);
    }

    /// <summary>
    /// 바이러스 부여 이벤트를 CoreManager에 전달한다.
    /// EffectInterpreter.HandleModifyStat에서 바이러스가 증가할 때 호출된다.
    /// </summary>
    public void NotifyVirusApplied(EntityState caster, EntityState target, int amount)
    {
        target.Turn.virusAppliedThisTurn += amount;
        CoreManager.Instance?.OnVirusApplied(caster, target, amount);
    }

    /// <summary>
    /// Fisher-Yates 셔플.
    /// </summary>
    public void ShuffleDeck(List<CardData> deck) => BattleUtils.Shuffle(deck);

    /// <summary>
    /// 배틀 로그를 기록한다. CardPlayProcessor 등 분리된 서브시스템에서도 호출 가능.
    /// </summary>
    public void Log(string msg)
    {
        Debug.Log($"[BattleEngine] {msg}");
        OnBattleLog?.Invoke(msg);
    }

    /// <summary>
    /// 상태 변경 이벤트를 발송한다.
    /// </summary>
    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
