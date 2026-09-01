using System.Collections.Generic;
using UnityEngine;

public static class CardEvaluator
{
    public struct CardEvaluation
    {
        public float damage;
        public float shield;
        public float heal;
        public float draw;
        public float statusBuff;
        public float statusDebuff;
        public float energy;
        public float discard;
        public float coreLongTerm;
        public float unknownValue;
    }

    private readonly struct EvalContext
    {
        public readonly CardData Card;
        public readonly EntityState Self;
        public readonly EntityState Opponent;

        public EvalContext(CardData card, EntityState self, EntityState opponent)
        {
            Card = card;
            Self = self;
            Opponent = opponent;
        }

        public int ProjectedEnergyAfterCost => Mathf.Max(0, (Self?.energy ?? 0) - (Card?.cost ?? 0));
        public float OverclockScale => Card != null && Card.HasKeyword("overclock")
            ? 1f + Self.overclockStacks * 0.1f
            : 1f;
    }

    public static CardEvaluation Evaluate(CardData card, EntityState self, EntityState opponent)
    {
        if (card == null || self == null || opponent == null)
            return default;

        EvalContext ctx = new EvalContext(card, self, opponent);
        CardEvaluation eval = EvaluateList(card.effects, ctx);

        if (card.coreEffect != null || card.type == CardType.Core)
            eval.coreLongTerm += Mathf.Max(0, card.tier) * 8f;

        return eval;
    }

    private static CardEvaluation EvaluateList(List<CardEffect> effects, EvalContext ctx)
    {
        CardEvaluation total = default;
        if (effects == null) return total;

        foreach (CardEffect effect in effects)
            total = Add(total, EvaluateEffect(effect, ctx));

        return total;
    }

    private static CardEvaluation EvaluateEffect(CardEffect effect, EvalContext ctx)
    {
        if (effect == null || string.IsNullOrEmpty(effect.type))
            return Unknown();

        CardEvaluation eval;
        switch (effect.type)
        {
            case "damage":
                eval = DamageEval(ResolveOcValue(effect, ctx), effect, ctx, true);
                break;
            case "fixedDamage":
                eval = DamageEval(effect.value, effect, ctx, false);
                break;
            case "scaledDamage":
                eval = DamageEval(ResolveScaledValue(effect, ctx, null), effect, ctx, true);
                break;
            case "damageByLuckStack":
                eval = DamageEval(ResolveScaledValue(effect, ctx, "luck"), effect, ctx, true);
                break;
            case "damagePlusByLuckyThisBattle":
                eval = DamageEval(ResolveScaledValue(effect, ctx, "luckyThisBattle"), effect, ctx, true);
                break;
            case "damagePlusByUnluckyThisBattle":
                eval = DamageEval(ResolveScaledValue(effect, ctx, "unluckyThisBattle"), effect, ctx, true);
                break;
            case "damageByDebuff":
                eval = DamageEval(GetDebuffDamage(effect, ctx), effect, ctx, false);
                break;
            case "damageMaxPercent":
                eval = DamageEval(GetPercentDamage(effect, ctx), effect, ctx, false);
                break;
            case "lostHpDamageByOverclock":
                eval = DamageEval(GetLostHpDamageByOverclock(effect, ctx), effect, ctx, true);
                break;
            case "damageIfHandEmpty":
                eval = HandCount(ctx.Self.hand) == 0 ? DamageEval(effect.value, effect, ctx, false) : default;
                break;
            case "damageIfConsecutiveLuck":
                eval = ctx.Self.consecutiveLuck > 0
                    ? DamageEval(effect.value * ctx.Self.consecutiveLuck, effect, ctx, false)
                    : default;
                break;
            case "damageIfUnluckyThisTurn":
                eval = ctx.Self.Turn.unluckyThisTurn > 0 ? DamageEval(effect.value, effect, ctx, false) : default;
                break;
            case "shield":
                eval = MakeShield(effect.value);
                break;
            case "scaledShield":
                eval = MakeShield(ResolveScaledValue(effect, ctx, null));
                break;
            case "nextTurnShield":
                eval = MakeShield(ResolveOcValue(effect, ctx));
                break;
            case "shieldByHpPercent":
                eval = MakeShield(ctx.Self.maxHp * (effect.value / 100f));
                break;
            case "shieldByLastHpLoss":
                eval = MakeShield(ctx.Self.Turn.lastHpLoss);
                break;
            case "shieldBreak":
            case "shieldBreakAllAndDamage":
                eval = effect.mode == "allAndDamage"
                    ? DamageEval(ctx.Opponent.shield * GetFraction(effect), effect, ctx, false)
                    : default;
                break;
            case "stealShield":
                eval = MakeShield(GetStealShieldValue(effect, ctx));
                break;
            case "heal":
                eval = MakeHeal(GetHealValue(effect, ctx));
                break;
            case "scaledHeal":
                eval = MakeHeal(ResolveScaledValue(effect, ctx, null));
                break;
            case "healPercent":
                eval = MakeHeal(ctx.Self.maxHp * (effect.value / 100f));
                break;
            case "draw":
                eval = MakeDraw(ResolveOcValue(effect, ctx));
                break;
            case "scaledDraw":
                eval = MakeDraw(ResolveScaledValue(effect, ctx, null));
                break;
            case "drawPerEnergy":
                eval = MakeDraw(ctx.ProjectedEnergyAfterCost * Mathf.Max(1, Mathf.RoundToInt(effect.value)));
                break;
            case "drawAndZeroCost":
            case "reshuffleAndDraw":
            case "searchDeck":
            case "searchByType":
            case "revealAndTakeHighestCost":
                eval = MakeDraw(Mathf.Max(1, Mathf.RoundToInt(effect.value)));
                break;
            case "cloneHandCard":
                eval = Add(MakeDraw(Mathf.Max(1, Mathf.RoundToInt(effect.value))), MakeStatusBuff(4f));
                break;
            case "energy":
            case "energyNextTurn":
                eval = MakeEnergy(ResolveOcValue(effect, ctx));
                break;
            case "energyDrain":
            case "drainEnergy":
                eval = MakeStatusDebuff(Mathf.Max(0, Mathf.RoundToInt(effect.value)));
                break;
            case "modifyStat":
                eval = EvaluateModifyStat(effect, ctx, effect.stat, effect.target, effect.mode, GetModifyValue(effect, ctx, effect.stat, effect.target));
                break;
            case "strength":
                eval = EvaluateModifyStat(effect, ctx, "strength", "self", null, effect.value);
                break;
            case "tempStrength":
                eval = EvaluateModifyStat(effect, ctx, "strength", "self", null, effect.value);
                break;
            case "weakness":
                eval = EvaluateModifyStat(effect, ctx, "weakness", "enemy", null, effect.value);
                break;
            case "virus":
                eval = EvaluateModifyStat(effect, ctx, "virus", "enemy", null, effect.value);
                break;
            case "virusOnCardPlayedNextTurn":
                eval = MakeStatusDebuff(Mathf.Max(1f, effect.value) * 2f);
                break;
            case "corrosion":
                eval = EvaluateModifyStat(effect, ctx, "corrosion", string.IsNullOrEmpty(effect.target) ? "enemy" : effect.target, null, effect.value);
                break;
            case "selfWeakness":
                eval = EvaluateModifyStat(effect, ctx, "weakness", "self", null, effect.value);
                break;
            case "overflow":
                eval = EvaluateModifyStat(effect, ctx, "overflowNext", "self", null, GetModifyValue(effect, ctx, "overflowNext", "self"));
                break;
            case "overflowReduce":
                eval = EvaluateModifyStat(effect, ctx, "overflowNext", "self", null, -Mathf.Abs(effect.value));
                break;
            case "overclockReduce":
                eval = EvaluateModifyStat(effect, ctx, "overclockStacks", "self", null, -Mathf.Abs(effect.value));
                break;
            case "overclockReset":
                eval = EvaluateModifyStat(effect, ctx, "overclockStacks", "self", "set", 0f);
                break;
            case "doubleLuck":
                eval = EvaluateModifyStat(effect, ctx, "luck", "self", "multiply", 2f);
                break;
            case "loseAllLuck":
                eval = EvaluateModifyStat(effect, ctx, "luck", "self", "set", 0f);
                break;
            case "doubleVirus":
                eval = EvaluateModifyStat(effect, ctx, "virus", "enemy", "multiply", 2f);
                break;
            case "addLuck":
                eval = EvaluateModifyStat(effect, ctx, "luck", "self", null, effect.value);
                break;
            case "luckyDayDebt":
                eval = Add(MakeStatusBuff(Mathf.Max(1f, effect.value) * 2f),
                    MakeDiscard(-Mathf.Max(1f, effect.bonusMultiplier)));
                break;
            case "addExtraDraw":
                eval = EvaluateModifyStat(effect, ctx, "extraDraw", "self", null, effect.value);
                break;
            case "removeOverclockLimit":
                eval = EvaluateModifyStat(effect, ctx, "overclockUnlimited", "self", "set", 1f);
                break;
            case "nextGambleBonusChance":
            case "gambleChanceBuff":
                eval = EvaluateModifyStat(effect, ctx, "gambleBonusChance", "self", null, effect.value);
                break;
            case "nextAttackBonus":
                eval = MakeStatusBuff(effect.value);
                break;
            case "retaliateOnHit":
                eval = MakeStatusBuff(effect.value * 0.8f);
                break;
            case "damageReduction":
                eval = MakeStatusBuff(effect.value * 0.2f);
                break;
            case "evadeNextHit":
                eval = MakeStatusBuff(Mathf.Max(1f, effect.value) * 12f);
                break;
            case "invincible":
            case "armorPierce":
            case "retryOnUnluck":
            case "preventSelfDamage":
                eval = MakeStatusBuff(2f);
                break;
            case "softenNextUnluck":
                eval = MakeStatusBuff(8f + Mathf.Max(1f, effect.value));
                break;
            case "preventSelfDamageIfOverclock":
                eval = ctx.Self.overclockStacks >= (effect.threshold > 0 ? effect.threshold : 1)
                    ? MakeStatusBuff(2f)
                    : default;
                break;
            case "protocolBypass":
                eval = MakeStatusBuff(Mathf.Max(1, effect.value));
                break;
            case "nextProtocolEffectRepeat":
                eval = MakeStatusBuff(Mathf.Max(1f, effect.value) * 12f);
                break;
            case "nextProtocolShieldBonus":
            case "buffInsurance":
                eval = MakeStatusBuff(Mathf.Max(1f, effect.value * 0.5f));
                break;
            case "buffInsuranceLuck":
                eval = MakeStatusBuff(Mathf.Max(1f, effect.value * 0.5f) + Mathf.Max(0f, effect.bonusMultiplier * 4f));
                break;
            case "energyFromShield":
            case "energyFromShieldChunk":
            case "energyToHeal":
            case "convertResource":
                eval = EvaluateConvertResource(effect, ctx);
                break;
            case "conditional":
            case "conditionalAdd":
            case "overload":
            case "conditionalByDismantledType":
                eval = Average(EvaluateList(effect.thenEffects, ctx), EvaluateList(effect.elseEffects, ctx));
                break;
            case "gamble":
                eval = WeightedBranches(effect, ctx, GetChance(effect, ctx));
                break;
            case "russianRoulette":
                eval = Add(
                    Scale(EvaluateList(effect.thenEffects, ctx), GetChance(effect, ctx) * Mathf.Max(6, effect.EffectiveHits)),
                    Scale(EvaluateList(effect.elseEffects, ctx), 1f - GetChance(effect, ctx)));
                break;
            case "fullHouseHit":
                eval = Add(
                    Scale(EvaluateList(effect.thenEffects, ctx), GetChance(effect, ctx) * effect.EffectiveHits),
                    Scale(EvaluateList(effect.elseEffects, ctx), (1f - GetChance(effect, ctx)) * effect.EffectiveHits));
                break;
            case "coinFlipHeads":
                eval = Add(MakeShield((ctx.Self.maxHp - ctx.Self.hp) * 0.5f), WeightedBranches(effect, ctx, 0.5f));
                break;
            case "setHpScaleStoreLoss":
                eval = MakeDiscard(-(ctx.Self.hp * (1f - Mathf.Clamp01(effect.value))));
                break;
            case "diceRoll":
                eval = effect.effects != null && effect.effects.Count > 0
                    ? Scale(EvaluateList(effect.effects, ctx), 1f / effect.effects.Count)
                    : Average(EvaluateList(effect.thenEffects, ctx), EvaluateList(effect.elseEffects, ctx));
                break;
            case "diceRollCombo":
                eval = effect.effects != null && effect.effects.Count > 0
                    ? Scale(EvaluateList(effect.effects, ctx), 1f / effect.effects.Count)
                    : Average(EvaluateList(effect.thenEffects, ctx), EvaluateList(effect.elseEffects, ctx));
                break;
            case "diceRollLuck":
                eval = MakeStatusBuff(3.5f);
                break;
            case "diceRollShieldDamage":
                eval = Add(Add(MakeShield(3.5f), DamageEval(3.5f, effect, ctx, false)), MakeStatusBuff(Mathf.Max(0f, effect.value) / 6f));
                break;
            case "gambleDeathCross":
            {
                float chance = GetChance(effect, ctx);
                float hpDiff = Mathf.Abs(ctx.Self.hp - ctx.Opponent.hp);
                float successMult = effect.value > 0f ? effect.value : 1f;
                var successEval = Add(
                    DamageEval(hpDiff * successMult, effect, ctx, false),
                    EvaluateList(effect.thenEffects, ctx));
                var failEval = Add(
                    effect.ratio > 0f ? MakeDiscard(-(hpDiff * effect.ratio)) : default,
                    EvaluateList(effect.elseEffects, ctx));
                eval = Add(Scale(successEval, chance), Scale(failEval, 1f - chance));
                break;
            }
            case "consumeVirus":
                eval = EvaluateConsumeVirus(effect, ctx);
                break;
            case "cleanseAndReflectDebuffs":
                eval = EvaluateCleanseAndReflectDebuffs(ctx);
                break;
            case "virusByDebuff":
                eval = MakeStatusDebuff((ctx.Opponent.weakness + ctx.Opponent.corrosion) * Mathf.Max(1f, effect.value));
                break;
            case "overclockGain":
                eval = MakeStatusBuff(effect.value);
                break;
            case "overclockConsume":
                eval = EvaluateOverclockConsume(effect, ctx);
                break;
            case "selfDamage":
            case "selfDamagePercent":
            case "nextTurnSelfDamage":
            case "selfDamageAtTurnEnd":
            case "selfDamageByTargetShield":
                eval = MakeDiscard(-GetSelfDamagePenalty(effect, ctx));
                break;
            case "discard":
            case "discardAll":
            case "discardRandom":
            case "addEndOfTurnDiscard":
                eval = MakeDiscard(-GetDiscardCount(effect, ctx));
                break;
            case "dismantle":
            case "dismantleAllAndScaleDraw":
            case "dismantleAllAndScaleShield":
            case "dismantleForEnergy":
            case "dismantleNetworkAndDraw":
            case "dismantleTop":
                eval = EvaluateDismantle(effect, ctx);
                break;
            case "extractTopOrDraw":
                eval = EvaluateExtractTopOrDraw(effect, ctx);
                break;
            case "threadSync":
                eval = EvaluateThreadSync(effect, ctx);
                break;
            case "masterOverride":
                eval = EvaluateMasterOverride(effect, ctx);
                break;
            case "extract":
                eval = Add(EvaluateList(ctx.Card?.extractEffects, ctx), ctx.Self.Turn.extractEnergyBonusActive ? MakeEnergy(ctx.Self.extractEnergyAmount) : default);
                break;
            case "randomBuff":
                eval = Add(Add(MakeStatusBuff(effect.value / 3f), MakeEnergy(effect.value / 3f)), MakeDraw(effect.value / 3f));
                break;
            default:
                eval = Add(Add(EvaluateList(effect.effects, ctx), Average(EvaluateList(effect.thenEffects, ctx), EvaluateList(effect.elseEffects, ctx))), Unknown());
                break;
        }

        return ApplyChance(eval, effect, ctx);
    }

    private static CardEvaluation EvaluateModifyStat(CardEffect effect, EvalContext ctx, string stat, string target, string mode, float rawValue)
    {
        if (string.IsNullOrEmpty(stat))
            return Unknown();

        EntityState entity = target == "self" ? ctx.Self : ctx.Opponent;
        int current = GetStatValue(entity, stat);
        int next = GetNextStatValue(stat, current, mode, rawValue);
        int delta = next - current;

        switch (stat)
        {
            case "shield": return target == "self" ? MakeShield(delta) : MakeStatusDebuff(Mathf.Max(0, -delta) * 0.5f);
            case "energy":
            case "baseEnergy": return target == "self" ? MakeEnergy(delta) : MakeStatusDebuff(Mathf.Max(0, -delta));
            case "hp": return target == "self" ? MakeHeal(Mathf.Max(0, delta)) : MakeDamageDirect(Mathf.Max(0, -delta));
            case "strength":
            case "luck":
            case "extraDraw":
            case "overclockStacks":
            case "networkStacks":
            case "gambleBonusChance":
            case "protocolBypassCount":
            case "nextProtocolEffectRepeat":
            case "nextProtocolShieldBonus":
            case "consecutiveLuck":
            case "energyCarryOver":
            case "doubleNetworkStacks":
            case "overclockUnlimited":
                return target == "self" ? MakeStatusBuff(delta) : MakeStatusDebuff(-delta);
            case "weakness":
            case "virus":
            case "corrosion":
            case "overflowNext":
                return target == "self" ? MakeStatusBuff(-delta) : MakeStatusDebuff(delta);
            case "maxHp":
                return target == "self" ? MakeStatusBuff(delta) : MakeStatusDebuff(-delta);
            default:
                return Unknown();
        }
    }

    private static CardEvaluation EvaluateConvertResource(CardEffect effect, EvalContext ctx)
    {
        string from = effect.from;
        string to = effect.to;

        if (effect.type == "energyFromShield")
        {
            from = "shield";
            to = "energy";
        }
        else if (effect.type == "energyFromShieldChunk")
        {
            int shieldAmount = ctx.Self.shield;
            int chunkSize = Mathf.Max(1, Mathf.RoundToInt(effect.value));
            int chunks = shieldAmount / chunkSize;
            return Add(MakeShield(-(chunks * chunkSize)), MakeEnergy(chunks));
        }
        else if (effect.type == "energyToHeal")
        {
            from = "energy";
            to = "heal";
        }

        int sourceAmount = GetStatValue(ctx.Self, from);
        float ratio = effect.ratio > 0 ? effect.ratio : 1f;
        int converted = Mathf.RoundToInt(sourceAmount * ratio);
        CardEvaluation eval = default;

        if (from == "shield") eval.shield -= sourceAmount;
        if (from == "energy") eval.energy -= sourceAmount;
        if (from == "strength") eval.statusBuff -= sourceAmount;

        switch (to)
        {
            case "energy": eval.energy += converted; break;
            case "heal": eval.heal += converted; break;
            case "draw": eval.draw += converted; break;
            case "shield": eval.shield += converted; break;
            default: eval.unknownValue += 1f; break;
        }

        return eval;
    }

    private static CardEvaluation EvaluateConsumeVirus(CardEffect effect, EvalContext ctx)
    {
        int consumed = effect.countAll || effect.mode == "all"
            ? ctx.Opponent.virus
            : effect.threshold > 0 ? Mathf.Min(effect.threshold, ctx.Opponent.virus) : Mathf.Min(Mathf.RoundToInt(effect.value), ctx.Opponent.virus);

        if (consumed <= 0) return default;

        float ratio = effect.ratio > 0 ? effect.ratio : 1f;
        CardEvaluation eval = MakeStatusDebuff(-consumed);
        string action = effect.action ?? "";
        switch (action)
        {
            case "": break;
            case "shield": eval.shield += consumed * ratio; break;
            case "shieldAndHealByChunk":
            {
                int chunkSize = Mathf.Max(1, effect.minValue > 0 ? effect.minValue : 5);
                float healPerChunk = effect.bonusMultiplier > 0 ? effect.bonusMultiplier : 1f;
                eval.shield += consumed * ratio;
                eval.heal += (consumed / chunkSize) * healPerChunk;
                break;
            }
            case "heal": eval.heal += consumed * ratio; break;
            case "strength": eval.statusBuff += consumed * ratio; break;
            case "draw": eval.draw += effect.value > 0 ? Mathf.RoundToInt(effect.value) : consumed; break;
            case "energy": eval.energy += effect.value > 0 ? Mathf.RoundToInt(effect.value) : consumed; break;
            default: eval.damage += SimulateDamage(consumed * ratio, effect, ctx, false); break;
        }

        return Add(eval, EvaluateList(effect.effects, ctx));
    }

    private static CardEvaluation EvaluateCleanseAndReflectDebuffs(EvalContext ctx)
    {
        float reflected = Mathf.Max(0, ctx.Self.weakness)
            + Mathf.Max(0, ctx.Self.virus)
            + Mathf.Max(0, ctx.Self.corrosion);

        return Add(MakeStatusBuff(reflected), MakeStatusDebuff(reflected));
    }

    private static CardEvaluation EvaluateOverclockConsume(CardEffect effect, EvalContext ctx)
    {
        int consumed = ctx.Self.overclockStacks;
        if (consumed <= 0) return default;

        CardEvaluation eval = MakeStatusBuff(-consumed);
        if (string.IsNullOrEmpty(effect.action))
            return Add(eval, EvaluateList(effect.effects, ctx));

        float amount = consumed * (effect.value > 0 ? effect.value : 1f);
        switch (effect.action)
        {
            case "shield": eval.shield += amount; break;
            case "heal": eval.heal += amount; break;
            default: eval.damage += SimulateDamage(amount, effect, ctx, false); break;
        }

        return Add(eval, EvaluateList(effect.effects, ctx));
    }

    private static CardEvaluation EvaluateDismantle(CardEffect effect, EvalContext ctx)
    {
        bool fromDeck = effect.source == "deck" || effect.type == "dismantleTop";
        int count = effect.countAll
            ? (fromDeck ? HandCount(ctx.Self.drawPile) : HandCount(ctx.Self.hand))
            : Mathf.Max(1, Mathf.RoundToInt(effect.value));

        CardEvaluation eval = MakeDiscard(-(fromDeck ? count * 0.25f : count * 0.5f));
        switch (effect.bonus)
        {
            case "drawEqual": eval.draw += count; break;
            case "shieldScale": eval.shield += count * (effect.bonusMultiplier > 0 ? effect.bonusMultiplier : 10f); break;
            case "energyByCost": eval.energy += EstimatePileCost(fromDeck ? ctx.Self.drawPile : ctx.Self.hand, count); eval.draw += 1f; break;
        }

        if (effect.type == "dismantleAllAndScaleDraw") eval.draw += count;
        if (effect.type == "dismantleAllAndScaleShield") eval.shield += count * (effect.bonusMultiplier > 0 ? effect.bonusMultiplier : 10f);
        if (effect.type == "dismantleForEnergy") eval.energy += EstimatePileCost(ctx.Self.hand, count);
        return eval;
    }

    private static CardEvaluation EvaluateExtractTopOrDraw(CardEffect effect, EvalContext ctx)
    {
        List<CardData> drawPile = ctx.Self.drawPile;
        if (drawPile == null || drawPile.Count == 0) return default;

        int count = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(effect.value)), drawPile.Count);
        int handSlots = Mathf.Max(0, 10 - HandCount(ctx.Self.hand));
        CardEvaluation eval = default;

        for (int i = 0; i < count; i++)
        {
            CardData card = drawPile[i];
            bool hasExtract = card.extractEffects != null && card.extractEffects.Count > 0;

            if (hasExtract)
            {
                eval = Add(eval, MakeDiscard(-0.25f));
                var extractCtx = new EvalContext(card, ctx.Self, ctx.Opponent);
                eval = Add(eval, EvaluateList(card.extractEffects, extractCtx));
                if (ctx.Self.Turn.extractEnergyBonusActive && ctx.Self.extractEnergyAmount > 0
                    && ctx.Self.Turn.extractEnergyRemainingCount > 0)
                    eval = Add(eval, MakeEnergy(ctx.Self.extractEnergyAmount));
            }
            else
            {
                if (handSlots <= 0)
                    break;

                eval = Add(eval, MakeDraw(1f));
                handSlots--;
            }
        }

        return eval;
    }

    private static CardEvaluation EvaluateThreadSync(CardEffect effect, EvalContext ctx)
    {
        List<CardData> hand = ctx.Self.hand;
        if (HandCount(hand) == 0) return default;

        int minCost = hand[0].cost;
        int maxCost = hand[0].cost;
        for (int i = 1; i < hand.Count; i++)
        {
            minCost = Mathf.Min(minCost, hand[i].cost);
            maxCost = Mathf.Max(maxCost, hand[i].cost);
        }

        int removed = hand.Count == 1 ? 1 : 2;
        float multiplier = effect.value > 0 ? effect.value : ctx.Self.networkStacks;
        float damage = (hand.Count == 1 ? maxCost : minCost + maxCost) * multiplier;
        return Add(MakeDiscard(-removed * 0.5f), DamageEval(damage, effect, ctx, false));
    }

    private static CardEvaluation EvaluateMasterOverride(CardEffect effect, EvalContext ctx)
    {
        CardData card = GetHighestCostAttackOrSkill(ctx.Self.drawPile);
        if (card == null) return default;

        if (card.effects != null && card.effects.Exists(e => e != null && e.type == "masterOverride"))
            return Add(MakeDraw(1f), Unknown());

        var remoteCtx = new EvalContext(card, ctx.Self, ctx.Opponent);
        bool protocolTriggered = ShouldProtocolTrigger(card, ctx.Self);
        CardEvaluation eval = protocolTriggered
            ? EvaluateList(card.protocolEffects, remoteCtx)
            : default;

        if (!(protocolTriggered && card.protocolOverride))
            eval = Add(eval, EvaluateList(card.effects, remoteCtx));

        eval = Add(eval, MakeDraw(1f));

        if (card.HasKeyword("network"))
            eval.statusBuff += card.cost;
        if (card.HasKeyword("overclock"))
            eval.statusBuff += card.cost * 0.5f;

        return eval;
    }

    private static CardEvaluation WeightedBranches(CardEffect effect, EvalContext ctx, float chance)
        => Add(Scale(EvaluateList(effect.thenEffects, ctx), chance), Scale(EvaluateList(effect.elseEffects, ctx), 1f - chance));

    private static CardEvaluation DamageEval(float value, CardEffect effect, EvalContext ctx, bool attackScaled)
        => MakeDamageDirect(SimulateDamage(value, effect, ctx, attackScaled));

    private static float SimulateDamage(float value, CardEffect effect, EvalContext ctx, bool includeAttackStats)
    {
        if (value <= 0f || ctx.Opponent.Turn.invincibleThisTurn) return 0f;

        float perHitValue = value;
        if (includeAttackStats)
            perHitValue += ctx.Self.strength - ctx.Self.weakness;

        int perHit = Mathf.RoundToInt(perHitValue);
        if (perHit <= 0) return 0f;

        if (ctx.Opponent.Turn.damageReductionThisTurn > 0f)
        {
            perHit = Mathf.RoundToInt(perHit * (1f - ctx.Opponent.Turn.damageReductionThisTurn));
            if (perHit <= 0) return 0f;
        }

        bool bypassShield = effect != null && (effect.bypassShield || ctx.Self.Turn.armorPierceNextAttack);
        int hits = effect != null ? effect.EffectiveHits : 1;
        int shield = bypassShield ? 0 : Mathf.Max(0, ctx.Opponent.shield);
        int total = 0;

        for (int i = 0; i < hits; i++)
        {
            int absorbed = bypassShield ? 0 : Mathf.Min(shield, perHit);
            shield -= absorbed;
            total += perHit - absorbed;
        }

        return total;
    }

    private static float ResolveScaledValue(CardEffect effect, EvalContext ctx, string fallbackSource)
    {
        string source = effect.scaling != null && !string.IsNullOrEmpty(effect.scaling.source)
            ? effect.scaling.source
            : fallbackSource;
        float multiplier = effect.scaling != null ? effect.scaling.multiplier : 1f;
        float sourceValue = effect.scaling != null
            ? ResolveEffectScalingSource(effect, ctx, null, null)
            : ResolveScalingSource(source, ctx);
        float value = effect.value + sourceValue * multiplier;

        if (effect.scaling != null && effect.scaling.max > 0)
            value = Mathf.Min(value, effect.scaling.max);

        if (effect.scaling != null && effect.scaling.min > 0)
            value = Mathf.Max(value, effect.scaling.min);

        return value;
    }

    private static float ResolveOcValue(CardEffect effect, EvalContext ctx)
        => effect.overclockScale ? effect.value * ctx.OverclockScale : effect.value;

    private static float GetModifyValue(CardEffect effect, EvalContext ctx, string stat, string target)
    {
        if (!string.IsNullOrEmpty(effect.source))
            return ResolveScalingSource(effect.source, ctx);

        if (effect.scaling == null)
            return effect.value;

        float value = effect.value + ResolveEffectScalingSource(effect, ctx, stat, target) * effect.scaling.multiplier;
        if (effect.scaling.max > 0)
            value = Mathf.Min(value, effect.scaling.max);
        if (effect.scaling.min > 0)
            value = Mathf.Max(value, effect.scaling.min);
        return value;
    }

    private static float ResolveScalingSource(string source, EvalContext ctx)
    {
        if (string.IsNullOrEmpty(source)) return 0f;

        switch (source)
        {
            case "networkStacks": return ctx.Self.networkStacks;
            case "networkStacksWithSelf": return ctx.Self.networkStacks + (ctx.Self.doubleNetworkStacks ? 2 : 1);
            case "overclockStacks": return ctx.Self.overclockStacks;
            case "selfDamageThisTurn": return ctx.Self.Turn.selfDamageThisTurn;
            case "totalDamageThisTurn": return ctx.Self.Turn.totalDamageThisTurn;
            case "dismantledThisTurn": return ctx.Self.Turn.dismantledThisTurn;
            case "dismantledThisBattle": return ctx.Self.dismantledThisBattle;
            case "targetVirus": return ctx.Opponent.virus;
            case "targetHandCount": return HandCount(ctx.Opponent.hand);
            case "corrosion": return ctx.Opponent.corrosion;
            case "luck": return ctx.Self.luck;
            case "luckyThisTurn": return ctx.Self.Turn.luckyThisTurn;
            case "unluckyThisTurn": return ctx.Self.Turn.unluckyThisTurn;
            case "hp": return ctx.Self.hp;
            case "maxHp": return ctx.Self.maxHp;
            case "enemyMaxHp": return ctx.Opponent.maxHp;
            case "overflowStacks": return ctx.Self.overflowNext;
            case "rebuildCount": return ctx.Self.totalRebuildsThisBattle;
            case "hpDifference": return Mathf.Abs(ctx.Self.hp - ctx.Opponent.hp);
            case "baseEnergy": return ctx.Self.baseEnergy;
            case "nonBaseCardsInHand": return CountNonBaseCards(ctx.Self.hand);
            case "luckyThisBattle": return ctx.Self.luckyThisBattle;
            case "unluckyThisBattle": return ctx.Self.unluckyThisBattle;
            case "gambleSuccessThisTurn": return ctx.Self.Turn.gambleSuccessThisTurn;
            case "targetLostHp": return ctx.Opponent.maxHp - ctx.Opponent.hp;
            case "cardAccumCount": return ctx.Card != null ? ctx.Card.accumulationCount : 0;
            default: return 0f;
        }
    }

    private static float ResolveEffectScalingSource(CardEffect effect, EvalContext ctx, string stat, string target)
    {
        if (effect?.scaling == null) return 0f;
        if (UsesFixedDemeritScaling(effect, stat, target) &&
            effect.scaling.source == "overclockStacks" &&
            ctx.Self.fixedOverclockStacks > 0)
            return ctx.Self.fixedOverclockStacks;

        return ResolveScalingSource(effect.scaling.source, ctx);
    }

    private static bool UsesFixedDemeritScaling(CardEffect effect, string stat, string target)
    {
        if (effect == null) return false;
        if (effect.type == "scaledSelfDamage" || effect.type == "scaledSelfDamagePercent")
            return true;

        return stat == "overflowNext" && target == "self";
    }

    private static int GetStatValue(EntityState entity, string stat)
    {
        if (entity == null || string.IsNullOrEmpty(stat)) return 0;

        switch (stat)
        {
            case "hp": return entity.hp;
            case "maxHp": return entity.maxHp;
            case "shield": return entity.shield;
            case "energy": return entity.energy;
            case "baseEnergy": return entity.baseEnergy;
            case "strength": return entity.strength;
            case "weakness": return entity.weakness;
            case "virus": return entity.virus;
            case "corrosion": return entity.corrosion;
            case "overclockStacks": return entity.overclockStacks;
            case "overflowNext": return entity.overflowNext;
            case "luck": return entity.luck;
            case "gambleBonusChance": return entity.Turn.gambleBonusChance;
            case "consecutiveLuck": return entity.consecutiveLuck;
            case "networkStacks": return entity.networkStacks;
            case "extraDraw": return entity.extraDraw;
            case "protocolBypassCount": return entity.protocolBypassCount;
            case "nextProtocolEffectRepeat": return entity.nextProtocolEffectRepeat;
            case "nextProtocolShieldBonus": return entity.nextProtocolShieldBonus;
            case "doubleNetworkStacks": return entity.doubleNetworkStacks ? 1 : 0;
            case "energyCarryOver": return entity.energyCarryOver ? 1 : 0;
            case "overclockUnlimited": return entity.overclockUnlimited ? 1 : 0;
            default: return 0;
        }
    }

    private static int GetNextStatValue(string stat, int current, string mode, float value)
    {
        int next = mode == "set"
            ? Mathf.RoundToInt(value)
            : mode == "multiply" ? Mathf.RoundToInt(current * value) : current + Mathf.RoundToInt(value);

        switch (stat)
        {
            case "maxHp": return Mathf.Max(1, next);
            case "shield":
            case "weakness":
            case "virus":
            case "corrosion":
            case "overclockStacks":
            case "overflowNext":
            case "luck":
            case "networkStacks":
                return Mathf.Max(0, next);
            case "doubleNetworkStacks":
            case "energyCarryOver":
            case "overclockUnlimited":
                return next != 0 ? 1 : 0;
            default:
                return next;
        }
    }

    private static float GetChance(CardEffect effect, EvalContext ctx)
    {
        float chance = effect.chance > 0f ? effect.chance : 0.5f;
        if (chance > 1f) chance /= 100f;
        return Mathf.Clamp01(chance + ctx.Self.luck * 0.01f + ctx.Self.Turn.gambleBonusChance * 0.01f);
    }

    private static CardEvaluation ApplyChance(CardEvaluation eval, CardEffect effect, EvalContext ctx)
        => effect.chance > 0f ? Scale(eval, GetChance(effect, ctx)) : eval;

    private static float GetHealValue(CardEffect effect, EvalContext ctx)
        => effect.mode == "percent" ? ctx.Self.maxHp * (effect.value / 100f) : effect.value;

    private static float GetPercentDamage(CardEffect effect, EvalContext ctx)
    {
        float value = ctx.Opponent.maxHp * (effect.value / 100f);
        if (effect.scaling != null && effect.scaling.max > 0)
            value = Mathf.Min(value, effect.scaling.max);
        if (effect.scaling != null && effect.scaling.min > 0)
            value = Mathf.Max(value, effect.scaling.min);
        return value;
    }

    private static float GetLostHpDamageByOverclock(CardEffect effect, EvalContext ctx)
    {
        float rate = effect.value;
        if (effect.scaling != null)
            rate += ResolveScalingSource(effect.scaling.source, ctx) * effect.scaling.multiplier;

        float value = Mathf.Max(0, ctx.Opponent.maxHp - ctx.Opponent.hp) * rate;
        if (effect.scaling != null && effect.scaling.max > 0)
            value = Mathf.Min(value, effect.scaling.max);
        if (effect.scaling != null && effect.scaling.min > 0)
            value = Mathf.Max(value, effect.scaling.min);
        return value;
    }

    private static float GetDebuffDamage(CardEffect effect, EvalContext ctx)
    {
        int stacks = effect.stat == "corrosion" ? ctx.Opponent.corrosion : ctx.Opponent.virus;
        return stacks * (effect.value > 0f ? effect.value : 1f);
    }

    private static float GetStealShieldValue(CardEffect effect, EvalContext ctx)
    {
        if (effect.mode == "all") return ctx.Opponent.shield;
        if (effect.mode == "half") return ctx.Opponent.shield / 2f;
        return Mathf.Min(Mathf.RoundToInt(effect.value), ctx.Opponent.shield);
    }

    private static int GetDiscardCount(CardEffect effect, EvalContext ctx)
    {
        int handCount = HandCount(ctx.Self.hand);
        if (effect.countAll || effect.type == "discardAll") return handCount;
        return Mathf.Min(Mathf.Max(1, Mathf.RoundToInt(effect.value)), handCount);
    }

    private static float GetSelfDamagePenalty(CardEffect effect, EvalContext ctx)
    {
        if (effect.mode == "targetShield" || effect.type == "selfDamageByTargetShield")
            return ctx.Opponent.shield;
        if (effect.mode == "percent" || effect.type == "selfDamagePercent")
            return ctx.Self.maxHp * (effect.value / 100f);
        return ResolveOcValue(effect, ctx);
    }

    private static float GetFraction(CardEffect effect) => effect.fraction > 0f ? effect.fraction : 0.5f;
    private static int HandCount(List<CardData> cards) => cards != null ? cards.Count : 0;

    private static float EstimatePileCost(List<CardData> pile, int count)
    {
        if (pile == null || pile.Count == 0 || count <= 0) return 0f;
        int sample = Mathf.Min(count, pile.Count);
        int total = 0;
        for (int i = 0; i < sample; i++) total += pile[i].cost;
        return total;
    }

    private static CardData GetHighestCostAttackOrSkill(List<CardData> pile)
    {
        if (pile == null || pile.Count == 0) return null;

        CardData best = null;
        for (int i = 0; i < pile.Count; i++)
        {
            CardData card = pile[i];
            if (card == null) continue;
            if (card.type != CardType.Attack && card.type != CardType.Skill) continue;
            if (best == null || card.cost > best.cost)
                best = card;
        }

        return best;
    }

    private static bool ShouldProtocolTrigger(CardData card, EntityState caster)
    {
        if (card == null || caster == null || string.IsNullOrEmpty(card.protocolCondition))
            return false;

        if (caster.protocolBypassCount > 0) return true;
        if (card.protocolCondition == "any") return true;
        if (card.protocolCondition == "luckMin") return caster.luck >= card.protocolConditionValue;

        CardData lastCard = caster.lastPlayedCard;
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

    private static int CountNonBaseCards(List<CardData> hand)
    {
        if (hand == null) return 0;
        int count = 0;
        for (int i = 0; i < hand.Count; i++)
            if (hand[i].pack != "base")
                count++;
        return count;
    }

    private static CardEvaluation Add(CardEvaluation a, CardEvaluation b)
    {
        a.damage += b.damage; a.shield += b.shield; a.heal += b.heal; a.draw += b.draw;
        a.statusBuff += b.statusBuff; a.statusDebuff += b.statusDebuff; a.energy += b.energy;
        a.discard += b.discard; a.coreLongTerm += b.coreLongTerm; a.unknownValue += b.unknownValue;
        return a;
    }

    private static CardEvaluation Scale(CardEvaluation eval, float factor)
    {
        eval.damage *= factor; eval.shield *= factor; eval.heal *= factor; eval.draw *= factor;
        eval.statusBuff *= factor; eval.statusDebuff *= factor; eval.energy *= factor;
        eval.discard *= factor; eval.coreLongTerm *= factor; eval.unknownValue *= factor;
        return eval;
    }

    private static CardEvaluation Average(CardEvaluation a, CardEvaluation b) => Scale(Add(a, b), 0.5f);
    private static CardEvaluation Unknown() => new CardEvaluation { unknownValue = 1f };
    private static CardEvaluation MakeDamageDirect(float value) => new CardEvaluation { damage = value };
    private static CardEvaluation MakeShield(float value) => new CardEvaluation { shield = value };
    private static CardEvaluation MakeHeal(float value) => new CardEvaluation { heal = value };
    private static CardEvaluation MakeDraw(float value) => new CardEvaluation { draw = value };
    private static CardEvaluation MakeEnergy(float value) => new CardEvaluation { energy = value };
    private static CardEvaluation MakeDiscard(float value) => new CardEvaluation { discard = value };
    private static CardEvaluation MakeStatusBuff(float value) => new CardEvaluation { statusBuff = value };
    private static CardEvaluation MakeStatusDebuff(float value) => new CardEvaluation { statusDebuff = value };
}
