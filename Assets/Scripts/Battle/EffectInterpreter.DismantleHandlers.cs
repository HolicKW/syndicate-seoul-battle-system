using UnityEngine;

/// <summary>
/// Tier 3: 해체/재구축 핸들러 (5종)
/// extract / rebuild / threadSync / masterOverride / setupExtractEnergy
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterDismantleHandlers()
    {
        Register("extract",            HandleExtract);
        Register("rebuild",            HandleRebuild);
        Register("threadSync",         HandleThreadSync);
        Register("masterOverride",     HandleMasterOverride);
        Register("setupExtractEnergy", HandleSetupExtractEnergy);
    }

    // -- 20. extract --
    private void HandleExtract(EffectContext ctx)
    {
        if (ctx.Card?.extractEffects != null && ctx.Card.extractEffects.Count > 0)
            ExecuteAll(ctx.Card.extractEffects, ctx);

        if (ctx.Caster.Turn.extractEnergyBonusActive && ctx.Caster.extractEnergyAmount > 0
            && ctx.Caster.Turn.extractEnergyRemainingCount > 0)
        {
            ctx.Caster.energy += ctx.Caster.extractEnergyAmount;
            ctx.Caster.Turn.extractEnergyRemainingCount--;
        }
    }

    // -- 21. rebuild --
    private void HandleRebuild(EffectContext ctx) { }

    // -- 22. threadSync --
    private void HandleThreadSync(EffectContext ctx)
    {
        var caster = ctx.Caster;
        var hand = caster.hand;
        if (hand.Count == 0) return;

        var targets = new System.Collections.Generic.List<CardData>();
        int totalCost;

        if (hand.Count == 1)
        {
            var only = hand[0];
            targets.Add(only);
            totalCost = only.cost;
        }
        else
        {
            int maxIdx = 0, minIdx = 0;
            for (int i = 1; i < hand.Count; i++)
            {
                if (hand[i].cost > hand[maxIdx].cost) maxIdx = i;
                if (hand[i].cost < hand[minIdx].cost) minIdx = i;
            }

            if (maxIdx == minIdx)
            {
                var card = hand[maxIdx];
                targets.Add(card);
                totalCost = card.cost * 2;
            }
            else
            {
                var high = hand[maxIdx];
                var low = hand[minIdx];
                targets.Add(high);
                targets.Add(low);
                totalCost = high.cost + low.cost;
            }
        }

        for (int i = 0; i < targets.Count; i++)
            hand.Remove(targets[i]);

        if (ctx.Engine != null)
        {
            for (int i = 0; i < targets.Count; i++)
                ctx.Engine.CommitDismantle(caster, targets[i], DismantleVfxSource.Hand, notify: false);
        }
        else
        {
            for (int i = 0; i < targets.Count; i++)
            {
                BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(targets[i])}");
                caster.voidPile.Add(targets[i]);
            }

            caster.Turn.dismantledThisTurn += targets.Count;
            caster.dismantledThisBattle += targets.Count;
        }

        float multiplier = ctx.Effect.value > 0 ? ctx.Effect.value : caster.networkStacks;
        int damage = Mathf.RoundToInt(totalCost * multiplier);
        if (damage > 0) ApplyDamage(ctx.Target, damage, caster);

        ctx.Engine?.NotifyStateChanged();
    }

    // -- 23. masterOverride --
    private void HandleMasterOverride(EffectContext ctx)
    {
        var caster = ctx.Caster;
        if (caster == null || caster.drawPile == null || caster.drawPile.Count == 0)
            return;

        int targetIdx = FindHighestCostAttackOrSkill(caster.drawPile);
        if (targetIdx < 0)
            return;

        var card = caster.drawPile[targetIdx];
        caster.drawPile.RemoveAt(targetIdx);

        BattleLogger.Log(BattleLogType.Effect, $"원격 발동: {BattleLogger.CardRef(card)}");
        ExecuteRemoteCard(ctx, card);

        if (!caster.hand.Contains(card))
        {
            caster.hand.Add(card);
            caster.lastDrawnCard = card;
        }
    }

    private static int FindHighestCostAttackOrSkill(System.Collections.Generic.List<CardData> cards)
    {
        int bestIdx = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            if (card.type != CardType.Attack && card.type != CardType.Skill) continue;
            if (bestIdx < 0 || card.cost > cards[bestIdx].cost)
                bestIdx = i;
        }

        return bestIdx;
    }

    private void ExecuteRemoteCard(EffectContext parentCtx, CardData card)
    {
        var caster = parentCtx.Caster;
        var target = parentCtx.Target;
        if (caster == null || target == null || card == null)
            return;

        caster.Turn.cardsPlayedThisTurn++;
        if (card.type == CardType.Skill)
            caster.Turn.skillsPlayedThisTurn++;
        ApplyVirusOnRemoteCardPlayed(parentCtx, caster);

        foreach (var handCard in caster.hand)
        {
            if (handCard.accumulationTarget > 0)
                handCard.accumulationCount++;
        }

        float overclockScale = 0f;
        if (card.HasKeyword("overclock"))
            overclockScale = 1f + caster.overclockStacks * 0.1f;

        bool isNetworkCard = card.HasKeyword("network");
        if (isNetworkCard)
        {
            caster.Turn.networkCardsPlayedThisTurn++;
            caster.networkCardsPlayedThisBattle++;
        }

        if (parentCtx.Card != null)
            caster.lastPlayedCard = parentCtx.Card;

        var remoteCtx = new EffectContext
        {
            Caster = caster,
            Target = target,
            Card = card,
            OverclockScale = overclockScale,
            CardDamageMultiplier = ShouldDoubleThirdRemoteCardDamage(caster) ? 2f : 0f,
            Engine = parentCtx.Engine,
        };

        bool protocolTriggered = false;
        if (!string.IsNullOrEmpty(card.protocolCondition) && card.protocolEffects != null)
        {
            protocolTriggered = parentCtx.Engine != null
                ? parentCtx.Engine.ProtocolResolver.CheckAndConsume(card, caster)
                : EvaluateProtocolFallback(card, caster);

            if (protocolTriggered)
            {
                ExecuteAll(card.protocolEffects, remoteCtx);
                int protocolRepeats = caster.nextProtocolEffectRepeat;
                if (protocolRepeats > 0)
                {
                    caster.nextProtocolEffectRepeat = 0;
                    for (int i = 0; i < protocolRepeats; i++)
                        ExecuteAll(card.protocolEffects, remoteCtx);
                }
                if (caster.nextProtocolShieldBonus > 0)
                {
                    caster.shield += caster.nextProtocolShieldBonus;
                    caster.nextProtocolShieldBonus = 0;
                }
            }
        }

        if (card.effects != null && card.effects.Count > 0 && !(protocolTriggered && card.protocolOverride))
            ExecuteAll(card.effects, remoteCtx);

        if (card.HasKeyword("overclock"))
        {
            if (caster.overclockUnlimited || caster.overclockStacks < caster.overclockMax)
                caster.overclockStacks++;
        }

        if (isNetworkCard)
        {
            int gain = caster.doubleNetworkStacks ? 2 : 1;
            caster.networkStacks += gain;
        }

        if (card.accumulationTarget > 0 &&
            card.accumulationCount >= card.accumulationTarget &&
            card.accumulationEffects != null && card.accumulationEffects.Count > 0)
        {
            ExecuteAll(card.accumulationEffects, remoteCtx);
            card.accumulationCount = 0;
        }

        parentCtx.Engine?.CheckCoreTriggers("cardPlayed", caster, card);
        caster.lastPlayedCard = card;
        parentCtx.Engine?.CheckBattleEnd();
        parentCtx.Engine?.NotifyStateChanged();
    }

    private static bool ShouldDoubleThirdRemoteCardDamage(EntityState caster)
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

    private static void ApplyVirusOnRemoteCardPlayed(EffectContext parentCtx, EntityState caster)
    {
        int amount = caster.Turn.virusOnCardPlayedThisTurn;
        if (amount <= 0) return;

        caster.virus += amount;
        parentCtx.Engine?.NotifyVirusApplied(caster.opponent ?? caster, caster, amount);
    }

    private static bool EvaluateProtocolFallback(CardData card, EntityState caster)
    {
        if (card.protocolCondition == "any") return true;
        if (card.protocolCondition == "luckMin") return caster.luck >= card.protocolConditionValue;

        var lastCard = caster.lastPlayedCard;
        if (lastCard == null) return false;

        return card.protocolCondition switch
        {
            "sameType"      => lastCard.type == card.type,
            "differentType" => lastCard.type != card.type,
            "attack"        => lastCard.type == CardType.Attack,
            "skill"         => lastCard.type == CardType.Skill,
            "network"       => lastCard.HasKeyword("network"),
            "costHigher"    => lastCard.cost > card.cost,
            "costLower"     => lastCard.cost < card.cost,
            "zeroCost"      => lastCard.cost == 0,
            _               => false,
        };
    }

    // -- 24. setupExtractEnergy --
    private void HandleSetupExtractEnergy(EffectContext ctx)
    {
        int maxCount = Mathf.RoundToInt(ctx.Effect.value);
        if (!ctx.Caster.Turn.extractEnergyBonusActive)
        {
            ctx.Caster.Turn.extractEnergyBonusActive = true;
            ctx.Caster.extractEnergyAmount = 1;
        }
        ctx.Caster.Turn.extractEnergyRemainingCount = Mathf.Max(
            ctx.Caster.Turn.extractEnergyRemainingCount, maxCount);
    }
}
