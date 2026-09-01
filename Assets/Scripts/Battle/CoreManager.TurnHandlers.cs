using UnityEngine;

/// <summary>
/// TurnStart / TurnEnd 트리거 핸들러 (11종)
/// TurnStart: virusFarmDamage / autoVirus / autoCorrosion / overclockPerTurn /
///            absoluteCarrier / randomBuffStart / strengthPerLuck / berserkMode
/// TurnEnd:   healOnHighOverclock / extraDebuffReduction /
///            strengthOnManyCards / damageOnSelfDamage
/// </summary>
public partial class CoreManager
{
    private void RegisterTurnHandlers()
    {
        // -- TurnStart --

        // 적 바이러스 x val% 고정 데미지
        Custom("virusFarmDamage", ctx =>
        {
            int dmg = Mathf.RoundToInt(ctx.Opponent.virus * (ctx.Val > 0 ? ctx.Val : 0.25f));
            if (dmg > 0) ctx.Engine.ApplyDamage(ctx.Opponent, dmg, ctx.Entity);
        });

        // 매 턴 적에게 바이러스 부여
        Custom("autoVirus", ctx =>
        {
            int amount = Mathf.Max(1, (int)ctx.Val);
            ctx.Opponent.virus += amount;
            ctx.Engine.NotifyVirusApplied(ctx.Entity, ctx.Opponent, amount);
        });

        // 매 턴 적에게 부식 부여
        Custom("autoCorrosion", ctx =>
            ctx.Opponent.corrosion += Mathf.Max(1, (int)ctx.Val));

        // 에너지 소모 후 오버클럭 스택 획득
        Custom("overclockPerTurn", ctx =>
        {
            int cost = ctx.Core.coreEffect.energyCost > 0 ? (int)ctx.Core.coreEffect.energyCost : 1;
            if (ctx.Entity.energy < cost) return;
            ctx.Entity.energy -= cost;
            int gain = Mathf.Max(1, (int)ctx.Val);
            if (ctx.Entity.overclockUnlimited || ctx.Entity.overclockStacks < ctx.Entity.overclockMax)
                ctx.Entity.overclockStacks += gain;
        });

        // 매 턴 바이러스 + 부식 동시 부여
        Custom("absoluteCarrier", ctx =>
        {
            int virusAmt = ctx.Core.coreEffect.maxBonusStack > 0
                ? ctx.Core.coreEffect.maxBonusStack
                : Mathf.Max(1, (int)ctx.Val);
            ctx.Opponent.virus     += virusAmt;
            ctx.Opponent.corrosion += Mathf.Max(1, (int)ctx.Val);
        });

        // 무작위 버프: CoreEffect 값이 없으면 힘+5 / 에너지+2 / 드로우+3
        Custom("randomBuffStart", ctx =>
        {
            float baseChance = ctx.Core.coreEffect.chance;
            if (baseChance > 0f)
            {
                if (baseChance > 1f) baseChance /= 100f;
                float chance = Mathf.Clamp01(baseChance
                    + ctx.Entity.luck * 0.01f
                    + ctx.Entity.Turn.gambleBonusChance * 0.01f);
                ctx.Entity.Turn.gambleBonusChance = 0;

                bool success = UnityEngine.Random.value < chance;
                if (!success && TryGetActiveCore(ctx.Entity, "bankruptcyReroll", out var bankruptcyCore))
                {
                    success = UnityEngine.Random.value < chance;
                    if (!success)
                        ApplyBankruptcyRerollPenalty(ctx.Entity, bankruptcyCore);
                }

                if (success)
                {
                    ctx.Entity.Turn.luckyThisTurn++;
                    ctx.Entity.luckyThisBattle++;
                    ctx.Entity.consecutiveLuck++;
                    ctx.Entity.Turn.gambleSuccessThisTurn++;
                    if (ctx.Entity.Turn.luckyDayDebtActive)
                    {
                        int luckGain = Mathf.Max(1, ctx.Entity.Turn.luckyDayLuckPerSuccess);
                        ctx.Entity.luck += luckGain;
                        ctx.Entity.Turn.luckyDaySuccessCount++;
                    }
                    ctx.Engine.NotifyGambleResult(ctx.Entity, true, ctx.Core);
                }
                else
                {
                    if (!ctx.Entity.ignoreUnluck)
                    {
                        ctx.Entity.Turn.unluckyThisTurn++;
                        ctx.Entity.unluckyThisBattle++;
                        ctx.Entity.consecutiveLuck = 0;
                    }
                    ctx.Engine.NotifyGambleResult(ctx.Entity, false, ctx.Core);
                    return;
                }
            }

            int strengthGain = ctx.Core.coreEffect.value > 0 ? Mathf.RoundToInt(ctx.Core.coreEffect.value) : 5;
            int energyGain = ctx.Core.coreEffect.energy > 0 ? ctx.Core.coreEffect.energy : 2;
            int drawGain = ctx.Core.coreEffect.draw > 0 ? ctx.Core.coreEffect.draw : 3;

            switch (UnityEngine.Random.Range(0, 3))
            {
                case 0: ctx.Entity.strength += strengthGain; break;
                case 1: ctx.Entity.energy   += energyGain; break;
                case 2: ctx.Engine.DrawCards(ctx.Entity, drawGain); break;
            }
        });

        // 매 턴 시작 시 행운 스택만큼 힘 획득
        Custom("strengthPerLuck", ctx =>
            ctx.Entity.strength += ctx.Entity.luck);

        // HP <= threshold% 일 때 매 턴 힘/에너지/드로우 획득 (아드레날린 펌프 리메이크)
        Custom("berserkMode", ctx =>
        {
            int threshold = ctx.Core.coreEffect.threshold > 0 ? ctx.Core.coreEffect.threshold : 30;
            if (ctx.Entity.maxHp <= 0) return;
            if (ctx.Entity.hp > ctx.Entity.maxHp * threshold / 100f) return;

            int strengthGain = ctx.Core.coreEffect.value > 0 ? Mathf.RoundToInt(ctx.Core.coreEffect.value) : 10;
            int energyGain   = ctx.Core.coreEffect.energy > 0 ? ctx.Core.coreEffect.energy : 2;
            int drawGain     = ctx.Core.coreEffect.draw > 0 ? ctx.Core.coreEffect.draw : 2;

            ctx.Entity.strength += strengthGain;
            ctx.Entity.energy   += energyGain;
            if (drawGain > 0) ctx.Engine.DrawCards(ctx.Entity, drawGain);
        });

        // -- TurnEnd --

        // 오버클럭 ≥ 3 시 체력 회복
        Custom("healOnHighOverclock", ctx =>
        {
            if (ctx.Entity.overclockStacks < 3) return;
            int amt = Mathf.Max(5, (int)ctx.Val);
            ctx.Entity.hp = Mathf.Min(ctx.Entity.hp + amt, ctx.Entity.maxHp);
        });

        // 매 턴 실드 획득
        Custom("extraDebuffReduction", ctx =>
        {
            int gain = ctx.Core.coreEffect.shield > 0 ? ctx.Core.coreEffect.shield : 15;
            ctx.Entity.shield += gain;
        });

        // threshold장 사용할 때마다 힘 획득
        Custom("strengthOnManyCards", ctx =>
        {
            int th = ctx.Core.coreEffect.threshold > 0 ? ctx.Core.coreEffect.threshold : 3;
            if (ctx.Entity.Turn.cardsPlayedThisTurn > 0 &&
                ctx.Entity.Turn.cardsPlayedThisTurn % th == 0)
                ctx.Entity.strength += Mathf.Max(1, (int)ctx.Val);
        });

        // 이번 턴 자해가 있으면 적에게 그 양만큼 피해
        Custom("damageOnSelfDamage", ctx =>
        {
            if (ctx.Entity.Turn.selfDamageThisTurn <= 0) return;
            float ratio = ctx.Val > 0 ? ctx.Val : 1f;
            int   dmg   = Mathf.RoundToInt(ctx.Entity.Turn.selfDamageThisTurn * ratio);
            if (dmg > 0) ctx.Engine.ApplyDamage(ctx.Opponent, dmg, ctx.Entity);
        });
    }

    /// <summary>customCoreHandlers 등록 단축 메서드</summary>
    private void Custom(string coreType, System.Action<CoreContext> handler)
        => customCoreHandlers[coreType] = handler;

    private static bool TryGetActiveCore(EntityState entity, string coreType, out CardData core)
    {
        core = null;
        if (entity?.activeCores == null) return false;
        foreach (var activeCore in entity.activeCores)
        {
            if (activeCore?.coreEffect?.coreType != coreType) continue;
            core = activeCore;
            return true;
        }
        return false;
    }

    private static void ApplyBankruptcyRerollPenalty(EntityState entity, CardData core)
    {
        float penaltyRate = core.coreEffect.value > 0f ? core.coreEffect.value : 0.25f;
        if (penaltyRate > 1f) penaltyRate /= 100f;

        int hpBefore = entity.hp;
        int amount = Mathf.RoundToInt(entity.hp * Mathf.Clamp01(penaltyRate));
        entity.TakeDamageRaw(amount);
        entity.Turn.selfDamageThisTurn += hpBefore - entity.hp;
    }
}
