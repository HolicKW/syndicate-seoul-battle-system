using UnityEngine;

/// <summary>
/// Tier 3: 바이오닉 감염 핸들러 (3종)
/// consumeVirus / damageByDebuff / meltToxin
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterBiohazardHandlers()
    {
        Register("consumeVirus",   HandleConsumeVirus);
        Register("damageByDebuff", HandleDamageByDebuff);
        Register("meltToxin",      HandleMeltToxin);
        Register("cleanseAndReflectDebuffs", HandleCleanseAndReflectDebuffs);
    }

    // -- 25. consumeVirus --
    private void HandleConsumeVirus(EffectContext ctx)
    {
        var eff = ctx.Effect;
        var target = ctx.Target;
        var caster = ctx.Caster;

        if (eff.threshold > 0 && target.virus < eff.threshold) return;

        int consumed;
        if (eff.countAll || eff.mode == "all")
            consumed = target.virus;
        else if (eff.threshold > 0)
            consumed = eff.threshold;
        else
            consumed = Mathf.Min(Mathf.RoundToInt(eff.value), target.virus);

        if (consumed <= 0) return;
        target.virus -= consumed;

        string action = eff.action ?? "";
        if (string.IsNullOrEmpty(action) && eff.ratio > 0)
            action = "damage";

        switch (action)
        {
            case "":
                break;
            case "damage":
            {
                int dmg = Mathf.RoundToInt(consumed * (eff.ratio > 0 ? eff.ratio : 1f));
                if (dmg > 0) ApplyDamage(target, dmg, caster);
                break;
            }
            case "shield":
            {
                int shieldAmt = Mathf.RoundToInt(consumed * (eff.ratio > 0 ? eff.ratio : 1f));
                caster.shield += shieldAmt;
                break;
            }
            case "shieldAndHealByChunk":
            {
                int shieldAmt = Mathf.RoundToInt(consumed * (eff.ratio > 0 ? eff.ratio : 1f));
                int chunkSize = Mathf.Max(1, eff.minValue > 0 ? eff.minValue : 5);
                int healPerChunk = Mathf.Max(1, Mathf.RoundToInt(eff.bonusMultiplier > 0 ? eff.bonusMultiplier : 1f));
                int healAmt = (consumed / chunkSize) * healPerChunk;

                caster.shield += shieldAmt;
                if (healAmt > 0)
                    caster.Heal(healAmt);
                break;
            }
            case "heal":
            {
                int healAmt = Mathf.RoundToInt(consumed * (eff.ratio > 0 ? eff.ratio : 1f));
                caster.hp = Mathf.Min(caster.hp + healAmt, caster.maxHp);
                break;
            }
            case "strength":
            {
                int strAmt = Mathf.RoundToInt(consumed * (eff.ratio > 0 ? eff.ratio : 1f));
                caster.strength += strAmt;
                break;
            }
            case "draw":
                ctx.Engine?.DrawCards(caster, Mathf.RoundToInt(eff.value));
                break;
            case "energy":
                caster.energy += Mathf.RoundToInt(eff.value);
                break;
        }

        if (eff.effects != null)
            ExecuteAll(eff.effects, ctx);

        if (ctx.Engine != null)
            ctx.Engine.NotifyVirusConsumed(caster, consumed);
    }

    // -- 26. damageByDebuff --
    private void HandleDamageByDebuff(EffectContext ctx)
    {
        var eff = ctx.Effect;
        string stat = eff.stat ?? "virus";
        float multiplier = eff.value > 0 ? eff.value : 1f;

        int stacks = stat == "corrosion" ? ctx.Target.corrosion : ctx.Target.virus;
        int dmg = Mathf.RoundToInt(stacks * multiplier);
        if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);
    }

    // -- 27. meltToxin --
    private void HandleMeltToxin(EffectContext ctx)
    {
        var target = ctx.Target;
        int n = ctx.Effect.threshold > 0 ? ctx.Effect.threshold : 6;

        if (target.virus < n) return;

        target.virus -= n;

        if (ctx.Effect.value > 0)
        {
            float dmg = ctx.Effect.value;
            if (!ctx.StrengthApplied)
            {
                dmg += ctx.Caster.strength;
                dmg -= ctx.Caster.weakness;
                ctx.StrengthApplied = true;
            }
            ApplyDamage(target, Mathf.Max(1, Mathf.RoundToInt(dmg)), ctx.Caster);
        }

        if (ctx.Effect.effects != null)
            ExecuteAll(ctx.Effect.effects, ctx);
    }

    private void HandleCleanseAndReflectDebuffs(EffectContext ctx)
    {
        var caster = ctx.Caster;
        var target = ctx.Target;

        int weakness = Mathf.Max(0, caster.weakness);
        int virus = Mathf.Max(0, caster.virus);
        int corrosion = Mathf.Max(0, caster.corrosion);

        if (weakness <= 0 && virus <= 0 && corrosion <= 0) return;

        caster.weakness = 0;
        caster.virus = 0;
        caster.corrosion = 0;

        target.weakness += weakness;
        target.virus += virus;
        target.corrosion += corrosion;

        if (virus > 0 && ctx.Engine != null)
            ctx.Engine.NotifyVirusApplied(caster, target, virus);
    }
}
