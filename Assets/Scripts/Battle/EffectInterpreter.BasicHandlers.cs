using UnityEngine;

/// <summary>
/// Tier 2: 기본 행동 핸들러 (8종)
/// heal / draw / energy / shieldBreak / convertResource / stealShield / discard / searchDeck
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterBasicHandlers()
    {
        Register("heal",            HandleHeal);
        Register("draw",            HandleDraw);
        Register("energy",          HandleEnergy);
        Register("shieldBreak",     HandleShieldBreak);
        Register("convertResource", HandleConvertResource);
        Register("stealShield",     HandleStealShield);
        Register("discard",         HandleDiscard);
        Register("searchDeck",      HandleSearchDeck);
    }

    // -- 8. heal --
    private void HandleHeal(EffectContext ctx)
    {
        var eff = ctx.Effect;
        int amount;

        if (eff.mode == "percent")
            amount = Mathf.RoundToInt(ctx.Caster.maxHp * (eff.value / 100f));
        else
            amount = Mathf.RoundToInt(eff.value);

        int hpBefore = ctx.Caster.hp;
        ctx.Caster.hp = Mathf.Min(ctx.Caster.hp + amount, ctx.Caster.maxHp);
        int actual = ctx.Caster.hp - hpBefore;
        if (actual > 0)
        {
            string who = ctx.Caster == BattleEngine.Instance?.Player ? "플레이어" : "적";
            BattleLogger.Log(BattleLogType.Info, $"{who} 회복: +{actual} HP ({hpBefore}→{ctx.Caster.hp})");
        }
    }

    // -- 9. draw --
    private void HandleDraw(EffectContext ctx)
    {
        int count = Mathf.RoundToInt(OcVal(ctx));

        if (ctx.Caster.skipNextDrawCount > 0)
        {
            int skip = Mathf.Min(count, ctx.Caster.skipNextDrawCount);
            ctx.Caster.skipNextDrawCount -= skip;
            count -= skip;
        }

        for (int i = 0; i < count; i++)
            DrawCard(ctx.Caster);
    }

    // -- 10. energy --
    private void HandleEnergy(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(OcVal(ctx));

        if (ctx.Effect.timing == "nextTurn")
            EnqueueDeferred(ctx, "nextTurnStart", new CardEffect { type = "energy", value = amount });
        else
            ctx.Caster.energy += amount;
    }

    // -- 11. shieldBreak --
    private void HandleShieldBreak(EffectContext ctx)
    {
        var eff = ctx.Effect;

        if (eff.mode == "allAndDamage")
        {
            int destroyed = ctx.Target.shield;
            ctx.Target.shield = 0;
            float fraction = eff.fraction;
            int dmg = Mathf.RoundToInt(destroyed * fraction);
            if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);
        }
        else
        {
            int amount = Mathf.RoundToInt(eff.value);
            ctx.Target.shield = Mathf.Max(0, ctx.Target.shield - amount);
        }
    }

    // -- 12. convertResource --
    private void HandleConvertResource(EffectContext ctx)
    {
        var eff = ctx.Effect;
        string from = eff.from;
        string to = eff.to;
        float ratio = eff.ratio > 0 ? eff.ratio : 1f;

        int sourceAmount = GetStatValue(ctx.Caster, from);
        if (sourceAmount <= 0) return;

        int converted = Mathf.RoundToInt(sourceAmount * ratio);
        SetStatValue(ctx.Caster, from, 0);

        switch (to)
        {
            case "energy":
                ctx.Caster.energy += converted;
                break;
            case "heal":
                ctx.Caster.hp = Mathf.Min(ctx.Caster.hp + converted, ctx.Caster.maxHp);
                break;
            case "draw":
                for (int i = 0; i < converted; i++)
                    DrawCard(ctx.Caster);
                break;
            case "shield":
                ctx.Caster.shield += converted;
                break;
            default:
                ModifyEntityStat(ctx.Caster, to, converted, "add", ctx);
                break;
        }
    }

    // -- 13. stealShield --
    private void HandleStealShield(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var target = ctx.Target;
        var caster = ctx.Caster;

        int stolen;
        if (eff.mode == "all")
            stolen = target.shield;
        else if (eff.mode == "half")
            stolen = target.shield / 2;
        else
            stolen = Mathf.Min(Mathf.RoundToInt(eff.value), target.shield);

        target.shield -= stolen;

        bool shouldGain = eff.mode != "half" || eff.gain;
        if (shouldGain)
            caster.shield += stolen;
    }

    // -- 14. discard --
    private void HandleDiscard(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var hand = ctx.Caster.hand;

        if (eff.timing == "turnEnd")
        {
            EnqueueDeferred(ctx, "turnEnd", eff.Clone());
            return;
        }

        if (hand.Count == 0) return;

        int count;
        if (eff.countAll)
            count = hand.Count;
        else
            count = Mathf.Min(Mathf.RoundToInt(eff.value), hand.Count);

        for (int i = 0; i < count && hand.Count > 0; i++)
        {
            int idx;
            if (eff.mode == "random")
                idx = UnityEngine.Random.Range(0, hand.Count);
            else
                idx = hand.Count - 1;

            var card = hand[idx];
            hand.RemoveAt(idx);
            ctx.Caster.voidPile.Add(card);
        }
    }

    // -- 15. searchDeck --
    private void HandleSearchDeck(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var pile = ctx.Caster.drawPile;
        if (pile.Count == 0) return;

        int maxFind = Mathf.Max(1, Mathf.RoundToInt(eff.value));
        string filter = eff.filter;
        int found = 0;

        if (filter == "highestCost")
        {
            int revealCount = Mathf.Min(maxFind, pile.Count);
            if (revealCount == 0) return;

            var revealed = new System.Collections.Generic.List<CardData>();
            for (int i = 0; i < revealCount; i++)
            {
                int rIdx = UnityEngine.Random.Range(0, pile.Count);
                revealed.Add(pile[rIdx]);
                pile.RemoveAt(rIdx);
            }

            int highestIdx = 0;
            for (int i = 1; i < revealed.Count; i++)
            {
                if (revealed[i].cost > revealed[highestIdx].cost) highestIdx = i;
            }

            var chosen = revealed[highestIdx];
            revealed.RemoveAt(highestIdx);

            chosen.cost = 0;
            if (ctx.Caster.hand.Count < 10)
                ctx.Caster.hand.Add(chosen);
            else
                ctx.Caster.voidPile.Add(chosen);

            foreach (var rCard in revealed)
                pile.Insert(0, rCard);
        }
        else
        {
            for (int i = pile.Count - 1; i >= 0 && found < maxFind && ctx.Caster.hand.Count < 10; i--)
            {
                if (MatchesFilter(pile[i], filter))
                {
                    var card = pile[i];
                    pile.RemoveAt(i);

                    if (eff.costMode == "zeroCost")
                        card.cost = 0;
                    else if (eff.costMode == "reduce")
                        card.cost = Mathf.Max(0, card.cost - 1);

                    ctx.Caster.hand.Add(card);
                    found++;
                }
            }
        }
    }
}
