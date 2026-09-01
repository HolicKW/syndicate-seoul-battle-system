using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tier 1: 슈퍼 범용 엔진 핸들러 (7종)
/// damage / shield / modifyStat / scaledEffect / conditional / selfDamage / dismantle
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterCoreHandlers()
    {
        Register("damage",      HandleDamage);
        Register("fixedDamage", HandleFixedDamage);
        Register("shield",      HandleShield);
        Register("modifyStat",  HandleModifyStat);
        Register("scaledEffect",HandleScaledEffect);
        Register("conditional", HandleConditional);
        Register("selfDamage",  HandleSelfDamage);
        Register("dismantle",   HandleDismantle);
    }

    // -- 1. damage --
    private void HandleDamage(EffectContext ctx)
    {
        ApplyAttackDamage(ctx, OcVal(ctx), ctx.Effect.EffectiveHits);
    }

    // -- 1b. fixedDamage (고정 피해: 힘/약화 보정을 무시한다) --
    private void HandleFixedDamage(EffectContext ctx)
    {
        var target = ResolveTarget(ctx);
        int amount = Mathf.RoundToInt(ctx.Effect.value);
        if (amount <= 0) return;
        ApplyDamage(target, amount, ctx.Caster, ctx.Effect.bypassShield);
    }

    // -- 2. shield --
    private void HandleShield(EffectContext ctx)
    {
        int val = Mathf.RoundToInt(ctx.Effect.value);
        ctx.Caster.shield += val;
    }

    // -- 3. modifyStat --
    private void HandleModifyStat(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var entity = ResolveTarget(ctx);
        string stat = eff.stat;
        string mode = eff.mode ?? "add";
        float val = eff.value;

        if (!string.IsNullOrEmpty(eff.source))
            val = GetScalingSourceValue(eff.source, ctx);
        else if (eff.scaling != null)
        {
            float sourceVal = GetEffectScalingSourceValue(eff, ctx);
            val = eff.value + sourceVal * eff.scaling.multiplier;

            if (eff.scaling.max > 0 && val > eff.scaling.max)
                val = eff.scaling.max;

            if (eff.scaling.min > 0 && val < eff.scaling.min)
                val = eff.scaling.min;
        }

        int virusBefore = (stat == "virus") ? entity.virus : 0;

        ModifyEntityStat(entity, stat, val, mode, ctx);

        if (stat == "virus" && mode != "set" && mode != "multiply")
        {
            int added = entity.virus - virusBefore;
            if (added > 0 && ctx.Engine != null)
                ctx.Engine.NotifyVirusApplied(ctx.Caster, entity, added);
        }
    }

    // -- 4. scaledEffect --
    private void HandleScaledEffect(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var scaling = eff.scaling;
        if (scaling == null)
        {
            Debug.LogWarning("[EffectInterpreter] scaledEffect에 scaling 데이터가 없습니다.");
            return;
        }

        float sourceVal = GetEffectScalingSourceValue(eff, ctx);
        float computed = eff.value + sourceVal * scaling.multiplier;

        if (scaling.max > 0 && computed > scaling.max)
            computed = scaling.max;

        if (scaling.min > 0 && computed < scaling.min)
            computed = scaling.min;

        int finalVal = Mathf.RoundToInt(computed);
        if (finalVal <= 0 && eff.action != "modifyStat") return;

        string action = eff.action;
        if (string.IsNullOrEmpty(action)) action = "damage";

        float origValue = eff.value;
        bool origOcScale = eff.overclockScale;
        string origMode = eff.mode;
        string origSource = eff.source;

        eff.value = finalVal;
        eff.overclockScale = false;
        eff.mode = null;
        eff.source = null;

        try
        {
            switch (action)
            {
                case "damage":     HandleDamage(ctx);    break;
                case "shield":     HandleShield(ctx);    break;
                case "heal":       HandleHeal(ctx);      break;
                case "draw":       HandleDraw(ctx);      break;
                case "selfDamage": HandleSelfDamage(ctx); break;
                case "energy":     HandleEnergy(ctx);    break;
                case "modifyStat":
                    eff.mode = origMode;
                    HandleModifyStat(ctx);
                    break;
            }
        }
        finally
        {
            eff.value = origValue;
            eff.overclockScale = origOcScale;
            eff.mode = origMode;
            eff.source = origSource;
        }
    }

    // -- 5. conditional --
    private void HandleConditional(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var cond = eff.condition;
        if (cond == null) return;

        bool condMet = EvaluateCondition(cond, ctx);

        var effects = condMet ? eff.thenEffects : eff.elseEffects;
        if (effects != null)
            ExecuteAll(effects, ctx);
    }

    // -- 6. selfDamage --
    private void HandleSelfDamage(EffectContext ctx)
    {
        var eff = ctx.Effect;

        int amount;
        if (eff.mode == "targetShield")
            amount = ctx.Target.shield;
        else if (eff.mode == "percent")
            amount = Mathf.RoundToInt(ctx.Caster.maxHp * (eff.value / 100f));
        else
            amount = Mathf.RoundToInt(OcVal(ctx));

        ApplySelfDamage(ctx.Caster, amount);
    }

    // -- 7. dismantle --
    private void HandleDismantle(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var caster = ctx.Caster;

        List<CardData> sourceList;
        string source = eff.source ?? "hand";
        if (source == "deck")
            sourceList = caster.drawPile;
        else
            sourceList = caster.hand;

        int count;
        if (eff.countAll)
            count = sourceList.Count;
        else
            count = Mathf.Max(1, Mathf.RoundToInt(eff.value));

        if (count <= 0 || sourceList.Count == 0) return;

        string filter = eff.filter;
        var dismantled = new List<CardData>();
        int actualCount = Mathf.Min(count, sourceList.Count);

        if (eff.mode == "chosen")
        {
            var pending = ctx.Engine?.PendingDismantleTargets;
            if (pending != null)
                actualCount = Mathf.Min(actualCount, pending.Count);
        }

        for (int i = 0; i < actualCount && sourceList.Count > 0; i++)
        {
            CardData card = null;

            if (source == "deck")
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    for (int j = 0; j < sourceList.Count; j++)
                    {
                        if (MatchesFilter(sourceList[j], filter))
                        {
                            card = sourceList[j];
                            sourceList.RemoveAt(j);
                            break;
                        }
                    }
                }
                else
                {
                    card = sourceList[0];
                    sourceList.RemoveAt(0);
                }
            }
            else // hand
            {
                if (!string.IsNullOrEmpty(filter))
                {
                    for (int j = 0; j < sourceList.Count; j++)
                    {
                        if (MatchesFilter(sourceList[j], filter))
                        {
                            card = sourceList[j];
                            sourceList.RemoveAt(j);
                            break;
                        }
                    }
                }
                else if (eff.mode == "random" || source == "deck")
                {
                    int idx = UnityEngine.Random.Range(0, sourceList.Count);
                    card = sourceList[idx];
                    sourceList.RemoveAt(idx);
                }
                else if (eff.mode == "chosen")
                {
                    var pending = ctx.Engine?.PendingDismantleTargets;
                    if (pending != null && pending.Count > 0)
                    {
                        card = pending[0];
                        pending.RemoveAt(0);
                        if (sourceList.Contains(card))
                            sourceList.Remove(card);
                        else
                            card = null;
                    }
                    else
                    {
                        int idx = UnityEngine.Random.Range(0, sourceList.Count);
                        card = sourceList[idx];
                        sourceList.RemoveAt(idx);
                        Debug.LogWarning("[EffectInterpreter] chosen 모드인데 PendingDismantleTargets가 비어있어 랜덤 처리");
                    }
                }
                else
                {
                    int idx = UnityEngine.Random.Range(0, sourceList.Count);
                    card = sourceList[idx];
                    sourceList.RemoveAt(idx);
                }
            }

            if (card == null) break;

            BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(card)}");
            ctx.Engine?.RecordDismantleVfx(caster, card, ToDismantleVfxSource(source));
            if (ctx.Engine != null)
                ctx.Engine.ApplyRebuildOrVoid(caster, card);
            else
                caster.voidPile.Add(card);
            dismantled.Add(card);
        }

        int dismantledCount = dismantled.Count;
        caster.Turn.dismantledThisTurn += dismantledCount;
        caster.dismantledThisBattle += dismantledCount;

        string bonus = eff.bonus;
        if (!string.IsNullOrEmpty(bonus) && dismantledCount > 0)
        {
            switch (bonus)
            {
                case "drawEqual":
                    for (int i = 0; i < dismantledCount; i++)
                        DrawCard(caster);
                    break;
                case "shieldScale":
                    float mult = eff.bonusMultiplier > 0 ? eff.bonusMultiplier : 10;
                    caster.shield += Mathf.RoundToInt(dismantledCount * mult);
                    break;
                case "energyByCost":
                    foreach (var card in dismantled)
                        caster.energy += card.cost;
                    DrawCard(caster);
                    break;
            }
        }

        foreach (var card in dismantled)
        {
            if (card.extractEffects != null && card.extractEffects.Count > 0)
            {
                Debug.Log($"[EffectInterpreter] 추출 효과 발동: {card.cardName} (효과 수: {card.extractEffects.Count})");
                BattleLogger.Log(BattleLogType.Effect, $"추출 효과 발동: {BattleLogger.CardRef(card)}");
                var prevCard = ctx.Card;
                ctx.Card = card;
                ExecuteAll(card.extractEffects, ctx);
                ctx.Card = prevCard;
            }

            if (caster.Turn.extractEnergyBonusActive && caster.extractEnergyAmount > 0
                && caster.Turn.extractEnergyRemainingCount > 0)
            {
                caster.energy += caster.extractEnergyAmount;
                caster.Turn.extractEnergyRemainingCount--;
            }
        }

        if (ctx.Engine != null)
        {
            foreach (var card in dismantled)
                ctx.Engine.CheckCoreTriggers("dismantle", caster, card);
        }
    }

    private static DismantleVfxSource ToDismantleVfxSource(string source)
    {
        return source == "deck" ? DismantleVfxSource.Deck : DismantleVfxSource.Hand;
    }

    // -- 8. extractTopOrDraw --
    private void HandleExtractTopOrDraw(EffectContext ctx)
    {
        var caster = ctx.Caster;
        if (caster == null || caster.drawPile == null || caster.drawPile.Count == 0)
            return;

        int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        for (int i = 0; i < count && caster.drawPile.Count > 0; i++)
        {
            var card = caster.drawPile[0];
            bool hasExtract = card.extractEffects != null && card.extractEffects.Count > 0;

            if (!hasExtract && caster.hand.Count >= 10)
                break;

            caster.drawPile.RemoveAt(0);

            if (hasExtract)
            {
                if (ctx.Engine != null)
                {
                    ctx.Engine.DismantleCard(caster, card, DismantleVfxSource.Deck);
                }
                else
                {
                    BattleLogger.Log(BattleLogType.Effect, $"해체: {BattleLogger.CardRef(card)}");
                    var prevCard = ctx.Card;
                    ctx.Card = card;
                    ExecuteAll(card.extractEffects, ctx);
                    ctx.Card = prevCard;

                    if (caster.Turn.extractEnergyBonusActive && caster.extractEnergyAmount > 0
                        && caster.Turn.extractEnergyRemainingCount > 0)
                    {
                        caster.energy += caster.extractEnergyAmount;
                        caster.Turn.extractEnergyRemainingCount--;
                    }

                    caster.Turn.dismantledThisTurn++;
                    caster.dismantledThisBattle++;
                    caster.voidPile.Add(card);
                }
            }
            else
            {
                caster.hand.Add(card);
                caster.lastDrawnCard = card;
            }
        }
    }

    // -- 9. cloneHandCard --
    private void HandleCloneHandCard(EffectContext ctx)
    {
        var caster = ctx.Caster;
        if (caster == null || caster.hand == null || caster.hand.Count == 0)
            return;

        int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        string filter = ctx.Effect.filter;

        for (int i = 0; i < count && caster.hand.Count < 10; i++)
        {
            CardData source = null;
            if (ctx.Effect.mode == "chosen")
            {
                var pending = ctx.Engine?.PendingDismantleTargets;
                if (pending != null && pending.Count > 0)
                {
                    source = pending[0];
                    pending.RemoveAt(0);
                    if (!caster.hand.Contains(source) || !MatchesCloneFilter(source, filter))
                        source = null;
                }
            }

            if (source == null)
            {
                var candidates = new List<CardData>();
                foreach (var card in caster.hand)
                {
                    if (card == ctx.Card) continue;
                    if (!MatchesCloneFilter(card, filter)) continue;
                    candidates.Add(card);
                }

                if (candidates.Count > 0)
                    source = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            if (source == null)
                break;

            var clone = source.Clone();
            clone.id = $"{source.id}_TEMP_CLONE";
            clone.isTemporary = true;
            if (clone.keywords == null)
                clone.keywords = new List<string>();
            if (!clone.keywords.Contains("일시적"))
                clone.keywords.Add("일시적");

            caster.hand.Add(clone);
            caster.lastDrawnCard = clone;
            BattleLogger.Log(BattleLogType.Effect, $"카드 복사: {BattleLogger.CardRef(source)}");
        }
    }

    private static bool MatchesCloneFilter(CardData card, string filter)
    {
        if (card == null) return false;
        if (string.IsNullOrEmpty(filter)) return true;
        return MatchesFilter(card, filter);
    }
}
