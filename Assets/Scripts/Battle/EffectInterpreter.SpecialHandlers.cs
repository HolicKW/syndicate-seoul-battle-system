using UnityEngine;

/// <summary>
/// Tier 4: 특수/유틸리티 핸들러 + 4단계 신규 핸들러 (17종)
/// forceTurnEnd / returnToHandZeroCost / reshuffleAndDraw / adjustCostLastDrawn /
/// randomBuff / casinoFieldReset / gambleDebt / discardTopAndShield /
/// dismantleVoidRecall / conditionalByDismantledType /
/// energyDrain / consumeAllEnergy / drawPerEnergy / drawAndZeroCost /
/// diceRollCombo / gambleDeathCross / conditionalAdd
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterSpecialHandlers()
    {
        Register("forceTurnEnd",                  HandleForceTurnEnd);
        Register("returnToHandZeroCost",          HandleReturnToHandZeroCost);
        Register("reshuffleAndDraw",              HandleReshuffleAndDraw);
        Register("adjustCostLastDrawn",           HandleAdjustCostLastDrawn);
        Register("randomBuff",                    HandleRandomBuff);
        Register("casinoFieldReset",              HandleCasinoFieldReset);
        Register("gambleDebt",                    HandleGambleDebt);
        Register("discardTopAndShield",           HandleDiscardTopAndShield);
        Register("dismantleVoidRecall",           HandleDismantleVoidRecall);
        Register("conditionalByDismantledType",   HandleConditionalByDismantledType);
        Register("energyDrain",                   HandleEnergyDrain);
        Register("drainEnergy",                   HandleEnergyDrain);
        Register("consumeAllEnergy",              HandleConsumeAllEnergy);
        Register("drawPerEnergy",                 HandleDrawPerEnergy);
        Register("drawAndZeroCost",               HandleDrawAndZeroCost);
        Register("diceRollCombo",                 HandleDiceRollCombo);
        Register("gambleDeathCross",              HandleGambleDeathCross);
        Register("conditionalAdd",                HandleConditionalAdd);
        Register("broadcastDebuffs",              HandleBroadcastDebuffs);
        Register("drawBothSides",                 HandleDrawBothSides);
        Register("kernelPanicDamage",             HandleKernelPanicDamage);
        Register("openAccessDraw",                HandleOpenAccessDraw);
    }

    // -- 40. forceTurnEnd --
    private void HandleForceTurnEnd(EffectContext ctx)
    {
        ctx.Caster.forceEndTurn = true;
    }

    // -- 41. returnToHandZeroCost --
    private void HandleReturnToHandZeroCost(EffectContext ctx)
    {
        if (ctx.Card == null) return;
        if (ctx.Caster.hand.Count >= 10) return;
        ctx.Card.cost = 0;
        if (!ctx.Caster.hand.Contains(ctx.Card))
            ctx.Caster.hand.Add(ctx.Card);
    }

    // -- 42. reshuffleAndDraw --
    private void HandleReshuffleAndDraw(EffectContext ctx)
    {
        var caster = ctx.Caster;

        foreach (var card in caster.hand)
            caster.drawPile.Add(card);
        caster.hand.Clear();

        ShuffleDeck(caster.drawPile);

        int drawCount = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        for (int i = 0; i < drawCount; i++)
            DrawCard(caster);
    }

    // -- 43. adjustCostLastDrawn --
    private void HandleAdjustCostLastDrawn(EffectContext ctx)
    {
        var card = ctx.Caster.lastDrawnCard;
        if (card == null || !ctx.Caster.hand.Contains(card)) return;

        var eff = ctx.Effect;
        if (eff.mode == "set")
            card.cost = Mathf.Max(0, Mathf.RoundToInt(eff.value));
        else
            card.cost = Mathf.Max(0, card.cost + Mathf.RoundToInt(eff.value));
    }

    // -- 44. randomBuff --
    private void HandleRandomBuff(EffectContext ctx)
    {
        var caster = ctx.Caster;
        int amount = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        int roll = UnityEngine.Random.Range(0, 3);

        switch (roll)
        {
            case 0: caster.strength += amount; break;
            case 1: caster.energy += amount; break;
            case 2:
                for (int i = 0; i < amount; i++) DrawCard(caster);
                break;
        }
    }

    // -- 45. casinoFieldReset --
    private void HandleCasinoFieldReset(EffectContext ctx)
    {
        foreach (var entity in new[] { ctx.Caster, ctx.Target })
        {
            entity.weakness = 0;
            entity.virus = 0;
            entity.corrosion = 0;
            entity.shield = 0;
        }

        float healRatio = ctx.Effect.value > 0f ? ctx.Effect.value : 0.5f;
        if (ctx.Effect.chance > 0f)
        {
            bool success = ResolveGamble(ctx, ctx.Effect.chance);
            HealByMaxHpRatio(ctx.Caster, healRatio);
            if (!success && !ctx.Caster.Turn.lastGambleUnluckSoftened)
                HealByMaxHpRatio(ctx.Target, healRatio);
            return;
        }

        HealByMaxHpRatio(ctx.Caster, healRatio);
        HealByMaxHpRatio(ctx.Target, healRatio);
    }

    private static void HealByMaxHpRatio(EntityState entity, float ratio)
    {
        int amount = Mathf.RoundToInt(entity.maxHp * ratio);
        entity.hp = Mathf.Min(entity.hp + amount, entity.maxHp);
    }

    // -- 46. gambleDebt --
    private void HandleGambleDebt(EffectContext ctx)
    {
        var caster = ctx.Caster;
        var eff = ctx.Effect;

        int debtTotal = 0;
        foreach (var card in caster.hand)
        {
            if (card.pack == "russian_roulette" || card.HasKeyword("gamble"))
            {
                debtTotal += card.cost;
                card.cost = 0;
            }
        }

        int penalty = eff.value > 0 ? Mathf.RoundToInt(eff.value) : debtTotal;
        caster.turnEndHpPenalty += penalty;
    }

    // -- 47. discardTopAndShield --
    private void HandleDiscardTopAndShield(EffectContext ctx)
    {
        var caster = ctx.Caster;
        if (caster.drawPile.Count == 0) return;

        var top = caster.drawPile[0];
        caster.drawPile.RemoveAt(0);
        BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(top)}");
        caster.voidPile.Add(top);

        float multiplier = ctx.Effect.value > 0 ? ctx.Effect.value : 1f;
        int luckMult = Mathf.Max(1, caster.luck);
        int shield = Mathf.RoundToInt(top.cost * luckMult * multiplier);
        if (shield > 0) caster.shield += shield;
    }

    // -- 48. dismantleVoidRecall --
    private void HandleDismantleVoidRecall(EffectContext ctx)
    {
        var caster = ctx.Caster;
        int prevVoidCount = caster.voidPile.Count;

        var handCopy = new System.Collections.Generic.List<CardData>(caster.hand);
        caster.hand.Clear();

        int dismantledCount = handCopy.Count;
        foreach (var card in handCopy)
        {
            BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(card)}");
            caster.voidPile.Add(card);
        }

        caster.Turn.dismantledThisTurn += dismantledCount;
        caster.dismantledThisBattle += dismantledCount;

        if (prevVoidCount > 0)
        {
            int recallIdx = prevVoidCount - 1;
            if (recallIdx >= 0 && recallIdx < caster.voidPile.Count)
            {
                var recalled = caster.voidPile[recallIdx];
                caster.voidPile.RemoveAt(recallIdx);
                caster.hand.Add(recalled);
            }
        }
    }

    // -- 49. conditionalByDismantledType --
    private void HandleConditionalByDismantledType(EffectContext ctx)
    {
        var voidPile = ctx.Caster.voidPile;
        if (voidPile.Count == 0) return;

        var last = voidPile[voidPile.Count - 1];
        var eff = ctx.Effect;

        bool typeMatch;
        if (string.IsNullOrEmpty(eff.filter))
            typeMatch = true;
        else
            typeMatch = MatchesFilter(last, eff.filter);

        var effects = typeMatch ? eff.thenEffects : eff.elseEffects;
        if (effects != null) ExecuteAll(effects, ctx);
    }

    // -- 50. energyDrain / drainEnergy --
    private void HandleEnergyDrain(EffectContext ctx)
    {
        int amount = Mathf.Max(0, Mathf.RoundToInt(ctx.Effect.value));
        ctx.Target.energyDrainNext += amount;
    }

    // -- 51. consumeAllEnergy --
    private void HandleConsumeAllEnergy(EffectContext ctx)
    {
        int consumed = ctx.Caster.energy;
        ctx.Caster.energy = 0;
        ctx.EnergyConsumed = consumed;

        if (consumed <= 0) return;

        var eff = ctx.Effect;
        if (eff.effects != null)
        {
            foreach (var sub in eff.effects)
                sub.value = consumed * (sub.value > 0 ? sub.value : 1);
            ExecuteAll(eff.effects, ctx);
        }
        else if (eff.thenEffects != null)
        {
            ExecuteAll(eff.thenEffects, ctx);
        }
    }

    // -- 52. drawPerEnergy --
    private void HandleDrawPerEnergy(EffectContext ctx)
    {
        int multiplier = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        int drawCount = ctx.Caster.energy * multiplier;
        for (int i = 0; i < drawCount; i++)
            DrawCard(ctx.Caster);
    }

    // -- 53. drawAndZeroCost --
    private void HandleDrawAndZeroCost(EffectContext ctx)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        int handBefore = ctx.Caster.hand.Count;

        for (int i = 0; i < count; i++)
            DrawCard(ctx.Caster);

        for (int i = handBefore; i < ctx.Caster.hand.Count; i++)
        {
            var drawnCard = ctx.Caster.hand[i];
            int originalCost = drawnCard.cost;
            drawnCard.cost = 0;
            ctx.Caster.deferredActions.Add(new DeferredAction
            {
                timing = "turnEnd",
                effect = new CardEffect { type = "restoreCardCost", value = originalCost },
                caster = ctx.Caster,
                target = ctx.Target,
                card = drawnCard,
            });
        }
    }

    // -- 54. diceRollCombo --
    private void HandleDiceRollCombo(EffectContext ctx)
    {
        var eff = ctx.Effect;
        int die1 = UnityEngine.Random.Range(1, 7);
        int die2 = UnityEngine.Random.Range(1, 7);
        int sum = die1 + die2;

        Debug.Log($"[EffectInterpreter] diceRollCombo: {die1} + {die2} = {sum}");

        if (die1 == die2)
        {
            if (eff.thenEffects != null)
            {
                ExecuteAll(eff.thenEffects, ctx);
            }
            else
            {
                var drawEff = new CardEffect { type = "draw", value = die1 };
                ctx.Effect = drawEff;
                Execute(ctx);
                ctx.Effect = eff;
            }
        }
        else
        {
            if (eff.effects != null && sum - 2 >= 0 && sum - 2 < eff.effects.Count)
            {
                var sub = eff.effects[sum - 2];
                ctx.Effect = sub;
                Execute(ctx);
                ctx.Effect = eff;
            }
            else if (eff.elseEffects != null)
            {
                ExecuteAll(eff.elseEffects, ctx);
            }
            else
            {
                var dmgEff = new CardEffect { type = "damage", value = sum };
                ctx.Effect = dmgEff;
                Execute(ctx);
                ctx.Effect = eff;
            }
        }
    }

    // -- 55. gambleDeathCross --
    private void HandleGambleDeathCross(EffectContext ctx)
    {
        var eff = ctx.Effect;
        float baseChance = eff.chance > 0 ? eff.chance : 0.5f;
        int hpDiff = Mathf.Abs(ctx.Caster.hp - ctx.Target.hp);

        bool success = ResolveGamble(ctx, baseChance);
        if (success)
        {
            float mult = eff.value > 0 ? eff.value : 1f;
            int dmg = Mathf.RoundToInt(hpDiff * mult);
            if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);

            if (eff.thenEffects != null) ExecuteAll(eff.thenEffects, ctx);
        }
        else
        {
            if (ctx.Caster.Turn.lastGambleUnluckSoftened)
                return;

            if (eff.ratio > 0f)
            {
                int selfDamage = Mathf.RoundToInt(hpDiff * eff.ratio);
                ApplySelfDamage(ctx.Caster, selfDamage);
            }
            if (eff.elseEffects != null) ExecuteAll(eff.elseEffects, ctx);
        }
    }

    // -- 56. conditionalAdd --
    private void HandleConditionalAdd(EffectContext ctx)
    {
        var eff = ctx.Effect;
        bool condMet = eff.condition == null || EvaluateCondition(eff.condition, ctx);

        if (condMet)
        {
            if (eff.thenEffects != null) ExecuteAll(eff.thenEffects, ctx);
        }
        else
        {
            if (eff.elseEffects != null) ExecuteAll(eff.elseEffects, ctx);
        }
    }

    // -- IP. 브로드캐스트 --
    private void HandleBroadcastDebuffs(EffectContext ctx)
    {
        int weakness = Mathf.Max(0, ctx.Caster.weakness);
        int virus = Mathf.Max(0, ctx.Caster.virus);
        int corrosion = Mathf.Max(0, ctx.Caster.corrosion);
        if (weakness <= 0 && virus <= 0 && corrosion <= 0) return;

        ctx.Target.weakness += weakness;
        ctx.Target.virus += virus;
        ctx.Target.corrosion += corrosion;

        if (virus > 0 && ctx.Engine != null)
            ctx.Engine.NotifyVirusApplied(ctx.Caster, ctx.Target, virus);
    }

    // -- IP. 핸드셰이크 --
    private void HandleDrawBothSides(EffectContext ctx)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(OcVal(ctx)));
        var engine = ctx.Engine ?? BattleEngine.Instance;
        if (engine != null)
        {
            engine.DrawCards(ctx.Caster, count);
            engine.DrawCards(ctx.Target, count);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            DrawCard(ctx.Caster);
            DrawCard(ctx.Target);
        }
    }

    // -- IP. 커널 패닉 --
    private void HandleKernelPanicDamage(EffectContext ctx)
    {
        int maxDamage = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        if (ctx.Caster.maxHp <= 0) return;

        float lostFraction = (float)(ctx.Caster.maxHp - ctx.Caster.hp) / ctx.Caster.maxHp;
        int damage = Mathf.Clamp(Mathf.RoundToInt(maxDamage * lostFraction), 1, maxDamage);
        ApplyAttackDamage(ctx, damage);
    }

    // -- IP. 오픈 액세스 --
    private void HandleOpenAccessDraw(EffectContext ctx)
    {
        string filter = ctx.Effect.filter;
        if (string.IsNullOrEmpty(filter))
            filter = ctx.OpenAccessFilter;
        if (string.IsNullOrEmpty(filter))
            filter = MostCommonTypeFilter(ctx.Caster.drawPile);

        int handBefore = ctx.Caster.hand.Count;
        DrawCard(ctx.Caster);

        if (ctx.Caster.hand.Count <= handBefore) return;

        var drawn = ctx.Caster.hand[ctx.Caster.hand.Count - 1];
        if (drawn == null || !MatchesFilter(drawn, filter)) return;

        var savedEffect = ctx.Effect;
        ctx.Effect = new CardEffect { type = "searchDeck", value = 1, filter = filter };
        HandleSearchDeck(ctx);
        ctx.Effect = savedEffect;
    }

    private static string MostCommonTypeFilter(System.Collections.Generic.List<CardData> pile)
    {
        int attacks = 0;
        int skills = 0;
        int cores = 0;

        if (pile != null)
        {
            foreach (var card in pile)
            {
                if (card == null) continue;
                if (card.type == CardType.Attack) attacks++;
                else if (card.type == CardType.Skill) skills++;
                else if (card.type == CardType.Core) cores++;
            }
        }

        if (attacks >= skills && attacks >= cores) return "attack";
        return skills >= cores ? "skill" : "core";
    }
}
