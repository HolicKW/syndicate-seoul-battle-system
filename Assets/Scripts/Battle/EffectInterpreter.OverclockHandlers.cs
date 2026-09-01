using UnityEngine;

/// <summary>
/// Tier 3: 오버클럭 코어 핸들러 (4종)
/// overclockGain / overclockConsume / overload / preventSelfDamage
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterOverclockHandlers()
    {
        Register("overclockGain",     HandleOverclockGain);
        Register("overclockConsume",  HandleOverclockConsume);
        Register("overload",       HandleOverload);
        Register("preventSelfDamage", HandlePreventSelfDamage);
    }

    // -- 16. overclockGain --
    private void HandleOverclockGain(EffectContext ctx)
    {
        var caster = ctx.Caster;
        int gain = Mathf.RoundToInt(ctx.Effect.value);

        if (caster.overclockUnlimited)
            caster.overclockStacks += gain;
        else
            caster.overclockStacks = Mathf.Min(caster.overclockStacks + gain, caster.overclockMax);
    }

    // -- 17. overclockConsume --
    private void HandleOverclockConsume(EffectContext ctx)
    {
        var caster = ctx.Caster;
        var eff = ctx.Effect;

        int consumed = caster.overclockStacks;
        caster.overclockStacks = 0;
        ctx.OverclockConsumed = consumed;

        if (consumed <= 0) return;

        if (!string.IsNullOrEmpty(eff.action))
        {
            string action = eff.action;
            float multiplier = eff.value > 0 ? eff.value : 1f;
            int amount = Mathf.RoundToInt(consumed * multiplier);

            switch (action)
            {
                case "damage":
                    if (amount > 0) ApplyDamage(ctx.Target, amount, caster);
                    break;
                case "shield":
                    caster.shield += amount;
                    break;
                case "heal":
                    caster.hp = Mathf.Min(caster.hp + amount, caster.maxHp);
                    break;
            }
        }

        if (eff.effects != null)
            ExecuteAll(eff.effects, ctx);
    }

    // -- 18. overload --
    private void HandleOverload(EffectContext ctx)
    {
        bool met = ctx.Caster.overclockStacks >= Mathf.RoundToInt(ctx.Effect.value);
        var effects = met ? ctx.Effect.thenEffects : ctx.Effect.elseEffects;
        if (effects != null) ExecuteAll(effects, ctx);
    }

    // -- 19. preventSelfDamage --
    private void HandlePreventSelfDamage(EffectContext ctx)
    {
        ctx.Caster.Turn.preventNextSelfDamage = true;
    }
}
