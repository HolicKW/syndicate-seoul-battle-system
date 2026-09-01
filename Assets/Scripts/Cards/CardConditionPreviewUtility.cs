using System.Collections.Generic;

/// <summary>
/// Read-only condition preview used by hand-card UI.
/// </summary>
public static class CardConditionPreviewUtility
{
    public static bool HasSatisfiedCondition(CardData card, EntityState caster, BattleEngine engine = null)
    {
        if (card == null || caster == null)
            return false;

        EntityState target = caster.opponent;
        EffectContext ctx = new EffectContext
        {
            Caster = caster,
            Target = target,
            Card = card,
            Engine = engine,
        };

        if (HasSatisfiedPlayRequirement(card, caster, target))
            return true;

        if (card.dynamicCost?.condition != null && target != null &&
            EffectInterpreter.EvaluateCondition(card.dynamicCost.condition, ctx))
        {
            return true;
        }

        if (HasSatisfiedProtocol(card, caster, engine))
            return true;

        if (card.accumulationTarget > 0 &&
            card.accumulationCount >= card.accumulationTarget &&
            card.accumulationEffects != null &&
            card.accumulationEffects.Count > 0)
        {
            return true;
        }

        return HasSatisfiedEffectCondition(card.effects, ctx);
    }

    private static bool HasSatisfiedPlayRequirement(CardData card, EntityState caster, EntityState target)
    {
        if (card.requiresOverclock && caster.overclockStacks > 0)
            return true;

        if (card.overclockThreshold > 0 && caster.overclockStacks >= card.overclockThreshold)
            return true;

        if (card.overflowMin > 0 && caster.overflowNext >= card.overflowMin)
            return true;

        if (card.virusThreshold > 0 && target != null && target.virus >= card.virusThreshold)
            return true;

        return false;
    }

    private static bool HasSatisfiedProtocol(CardData card, EntityState caster, BattleEngine engine)
    {
        if (string.IsNullOrEmpty(card.protocolCondition) ||
            card.protocolEffects == null ||
            card.protocolEffects.Count == 0)
        {
            return false;
        }

        if (caster.protocolBypassCount > 0)
            return true;

        ProtocolResolver resolver = engine?.ProtocolResolver;
        if (resolver == null && BattleEngine.Instance != null)
            resolver = BattleEngine.Instance.ProtocolResolver;

        return resolver != null && resolver.Evaluate(card, caster);
    }

    private static bool HasSatisfiedEffectCondition(IReadOnlyList<CardEffect> effects, EffectContext ctx)
    {
        if (effects == null)
            return false;

        for (int i = 0; i < effects.Count; i++)
        {
            CardEffect effect = effects[i];
            if (effect == null)
                continue;

            if (IsConditionBranchEffect(effect) &&
                effect.condition != null &&
                ctx.Target != null &&
                EffectInterpreter.EvaluateCondition(effect.condition, ctx))
            {
                return true;
            }

            if (HasSatisfiedEffectCondition(effect.effects, ctx) ||
                HasSatisfiedEffectCondition(effect.thenEffects, ctx) ||
                HasSatisfiedEffectCondition(effect.elseEffects, ctx))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsConditionBranchEffect(CardEffect effect)
    {
        return effect.type == "conditional" || effect.type == "conditionalAdd";
    }
}
