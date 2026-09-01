using UnityEngine;

/// <summary>
/// PlayCard 13단계 흐름을 관리하는 프로세서.
/// BattleEngine에서 분리되어 카드 사용의 전체 파이프라인을 담당한다.
///
/// 단계 순서 (변경 시 주의):
///   1.  코스트 차감 + cardsPlayedThisTurn++
///   2.  오버클럭 스케일 계산 (스택 증가는 효과 실행 후)
///   3.  네트워크 카운트 증가 (스택 증가는 효과 실행 후)
///   4.  패의 다른 축적 카드 카운트 증가
///   5.  핸드에서 제거
///   6.  EffectContext 생성
///   6-A. 프로토콜 사전 판정 + 프로토콜 이펙트 실행
///   6-B. 메인 이펙트 실행
///   7.  오버클럭/네트워크 스택 증가 (효과 실행 후)
///   8.  축적 조건 → accumulationEffects 실행
///   9.  CheckCoreTriggers("cardPlayed")
///   10. lastPlayedCard 갱신
///   11. 코어 카드 → activeCores 등록 + 즉시 효과
///   12. 재구축 or 보이드
///   13. 승패 체크 + 상태 알림
/// </summary>
public class CardPlayProcessor
{
    private const string RansomwareKeyword = "ransomware";

    private readonly BattleEngine engine;

    public CardPlayProcessor(BattleEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>
    /// 카드를 사용한다. CanPlayCard 검증 후 13단계 파이프라인을 실행한다.
    /// </summary>
    public void Execute(int handIndex, EntityState caster, EntityState target)
    {
        if (!engine.CanPlayCard(handIndex, caster)) return;

        var card = caster.hand[handIndex];
        int effectiveCost = engine.GetEffectiveCost(card, caster);
        var lastPlayedBeforeThisCard = caster.lastPlayedCard;
        string casterName = caster == engine.Player ? "플레이어" : "적";
        BattleLogActor actor = caster == engine.Player ? BattleLogActor.Player : BattleLogActor.Enemy;
        using var logActorScope = BattleLogger.ActorScope(actor);

        engine.Log($"'{card.cardName}' 사용 (코스트: {effectiveCost})");
        BattleLogger.Log(BattleLogType.Info,
            $"-- {casterName}: {BattleLogger.CardRef(card)} 사용 (코스트 {effectiveCost}, 에너지 {caster.energy}) --");

        // -- 1. 코스트 차감 + cardsPlayedThisTurn++ --
        PayCost(caster, effectiveCost);
        caster.Turn.cardsPlayedThisTurn++;
        if (card.type == CardType.Skill) caster.Turn.skillsPlayedThisTurn++;
        ApplyVirusOnCardPlayed(caster);

        // -- 2. 오버클럭 카드 → 효과 실행 전 스케일만 계산 (스택 증가는 효과 실행 후) --
        float overclockScale = 0f;
        bool isOverclockCard = card.HasKeyword("overclock");
        if (isOverclockCard)
        {
            overclockScale = 1f + caster.overclockStacks * 0.1f;
        }

        // -- 3. 네트워크 카드 → 카운트만 증가 (스택은 효과 실행 후) --
        bool isNetworkCard = card.HasKeyword("network");
        if (isNetworkCard)
        {
            caster.Turn.networkCardsPlayedThisTurn++;
            caster.networkCardsPlayedThisBattle++;
        }

        // -- 4. 패의 다른 축적 카드 카운트 증가 (사용한 카드 자신은 제외) --
        for (int j = 0; j < caster.hand.Count; j++)
        {
            if (j != handIndex && caster.hand[j].accumulationTarget > 0)
                caster.hand[j].accumulationCount++;
        }

        // -- 5. 핸드에서 제거 --
        caster.hand.RemoveAt(handIndex);

        // -- 6. EffectContext 생성 --
        var ctx = new EffectContext
        {
            Caster = caster,
            Target = target,
            Card = card,
            OverclockScale = overclockScale,
            CardDamageMultiplier = ShouldDoubleThirdCardDamage(caster) ? 2f : 0f,
            OpenAccessFilter = engine.ConsumePendingOpenAccessFilter(),
            Engine = engine,
        };

        // -- 6-A. 프로토콜 조건 사전 판정 (메인 이펙트 실행 전) --
        bool protocolTriggered = false;
        if (!string.IsNullOrEmpty(card.protocolCondition) && card.protocolEffects != null)
        {
            protocolTriggered = engine.ProtocolResolver.CheckAndConsume(card, caster);
            if (protocolTriggered)
            {
                engine.Log($"프로토콜 발동: {card.cardName}");
                engine.Interpreter.ExecuteAll(card.protocolEffects, ctx);
                int protocolRepeats = caster.nextProtocolEffectRepeat;
                if (protocolRepeats > 0)
                {
                    caster.nextProtocolEffectRepeat = 0;
                    for (int i = 0; i < protocolRepeats; i++)
                    {
                        engine.Log($"프로토콜 추가 발동: {card.cardName}");
                        engine.Interpreter.ExecuteAll(card.protocolEffects, ctx);
                    }
                }
                // 다음 프로토콜 보너스 실드 소모
                if (caster.nextProtocolShieldBonus > 0)
                {
                    caster.shield += caster.nextProtocolShieldBonus;
                    engine.Log($"프로토콜 보너스 실드 +{caster.nextProtocolShieldBonus}");
                    caster.nextProtocolShieldBonus = 0;
                }
            }
        }

        // -- 6-B. 메인 이펙트 실행 (protocolOverride 시 프로토콜이 발동된 경우 건너뜀) --
        if (card.effects != null && card.effects.Count > 0 && !(protocolTriggered && card.protocolOverride))
            engine.Interpreter.ExecuteAll(card.effects, ctx);

        // -- 7. 효과 실행 후 스택 증가 --
        if (isOverclockCard)
        {
            if (caster.overclockUnlimited || caster.overclockStacks < caster.overclockMax)
                caster.overclockStacks++;
        }
        if (isNetworkCard)
        {
            int gain = caster.doubleNetworkStacks ? 2 : 1;
            caster.networkStacks += gain;
        }

        // -- 8. 축적 조건 달성 → accumulationEffects 실행 --
        if (card.accumulationTarget > 0 &&
            card.accumulationCount >= card.accumulationTarget &&
            card.accumulationEffects != null && card.accumulationEffects.Count > 0)
        {
            engine.Log($"'{card.cardName}' 축적 조건 달성 ({card.accumulationCount}/{card.accumulationTarget})");
            var accCtx = new EffectContext
            {
                Caster = caster,
                Target = target,
                Card = card,
                Engine = engine,
            };
            engine.Interpreter.ExecuteAll(card.accumulationEffects, accCtx);
            card.accumulationCount = 0; // 리셋
        }

        // -- 9. CheckCoreTriggers("cardPlayed") --
        engine.CheckCoreTriggers("cardPlayed", caster, card);

        // -- 10. lastPlayedCard 갱신 --
        if (caster.lastPlayedCard == lastPlayedBeforeThisCard)
            caster.lastPlayedCard = card;

        // -- 11. 코어 카드 → activeCores에 추가 --
        if (card.type == CardType.Core)
        {
            bool alreadyActive = false;
            foreach (var p in caster.activeCores)
            {
                if (p.id == card.id) { alreadyActive = true; break; }
            }
            if (!alreadyActive)
            {
                caster.activeCores.Add(card);
                engine.Log($"코어 '{card.cardName}' 활성화");
                CoreManager.Instance?.ApplyImmediateCore(card, caster);

                // 플레이어 코어 카드 사용 = 소모 처리 (voidPile을 거치지 않으므로 여기서 기록)
                if (caster == engine.Player)
                    BattleSceneData.ConsumedCardIds.Add(card.id);
            }
        }
        // -- 12. 재구축 처리 or 보이드 이동 --
        else
        {
            bool returnedToHand = caster.hand.Contains(card);
            if (!returnedToHand && IsRansomwareCard(card))
            {
                BattleLogger.Log(BattleLogType.Effect,
                    $"{BattleLogger.CardRef(card)} 제거");
            }
            else if (!returnedToHand)
            {
                if (card.rebuildScaling != null && !card.rebuildScalingApplied)
                {
                    card.rebuildScalingApplied = true;
                    card.rebuildCount = engine.Interpreter.ComputeRebuildCount(card, caster, target);
                    engine.Log($"'{card.cardName}' 재구축 횟수 동적 설정: {card.rebuildCount}회");
                }

                engine.ApplyRebuildOrVoid(caster, card);
            }
        }

        // -- 13. 승패 체크 + 상태 알림 --
        engine.CheckBattleEnd();
        engine.NotifyStateChanged();
    }

    private static bool IsRansomwareCard(CardData card)
    {
        return card != null && card.HasKeyword(RansomwareKeyword);
    }

    /// <summary>
    /// 코스트를 지불한다. 에너지가 충분하면 차감, 부족하면 bankruptcy로 HP 소모.
    /// </summary>
    private void PayCost(EntityState caster, int effectiveCost)
    {
        if (caster.energy >= effectiveCost)
        {
            caster.energy -= effectiveCost;
        }
        else
        {
            var bankruptcyCore = CostCalculator.FindBankruptcyCore(caster);
            int deficit = effectiveCost - caster.energy;
            caster.energy = 0;
            int rate = bankruptcyCore != null ? CostCalculator.GetBankruptcyCoreRate(bankruptcyCore) : 10;
            int hpCost = deficit * rate;
            caster.hp -= hpCost;
            engine.Log($"bankruptcy 발동: 에너지 부족 {deficit} x {rate} → HP {hpCost} 소모");
        }
    }

    private static bool ShouldDoubleThirdCardDamage(EntityState caster)
    {
        if (caster == null || caster.Turn.cardsPlayedThisTurn != 3)
            return false;

        foreach (var core in caster.activeCores)
        {
            if (core?.coreEffect?.coreType == "doubleThirdNetwork")
                return true;
        }

        return false;
    }

    private void ApplyVirusOnCardPlayed(EntityState caster)
    {
        int amount = caster.Turn.virusOnCardPlayedThisTurn;
        if (amount <= 0) return;

        caster.virus += amount;
        engine.NotifyVirusApplied(caster.opponent ?? caster, caster, amount);
        BattleLogger.Log(BattleLogType.Effect,
            $"생체 신호 교란: 카드 사용으로 바이러스({amount}) 부여");
    }

}
