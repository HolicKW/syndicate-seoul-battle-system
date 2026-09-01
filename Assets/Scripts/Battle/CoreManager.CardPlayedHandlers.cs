using UnityEngine;

/// <summary>
/// CardPlayed / Dismantle / Rebuild 트리거 핸들러 (18종)
/// CardPlayed:  madGearBothDamage / shieldOnOverclock / energyOnCardCount /
///              drawOnNetworkThreshold / doubleThirdNetwork / damageAndShieldOnProtocol /
///              doubleFirstSkill / rouletteMaster / energyOnFirstLuck / drawOnLuckyChance
/// Dismantle:   damageOnDismantle / drawOnDismantle / energyAndDrawOnFirstDismantle
/// Rebuild:     healAndCostReductionOnRebuild / damageOnRebuild
/// VirusConsumed (no-op 등록): bioWeaponVault / immuneSystemTakeover / absoluteCarrierEnergy
/// </summary>
public partial class CoreManager
{
    private void RegisterCardPlayedHandlers()
    {
        // -- CardPlayed --

        // 카드 사용마다 양측에 오버클럭 스택만큼 피해
        Custom("madGearBothDamage", ctx =>
        {
            int dmg = ctx.Entity.overclockStacks;
            if (dmg <= 0) return;
            ctx.Engine.ApplyDamage(ctx.Entity,   dmg, ctx.Opponent);
            ctx.Engine.ApplyDamage(ctx.Opponent, dmg, ctx.Entity);
        });

        // 오버클럭 카드 사용 시 실드 획득
        Custom("shieldOnOverclock", ctx =>
        {
            if (ctx.PlayedCard != null && ctx.PlayedCard.HasKeyword("overclock"))
                ctx.Entity.shield += Mathf.Max(5, (int)ctx.Val);
        });

        // N번째 카드 사용마다 에너지 +1
        Custom("energyOnCardCount", ctx =>
        {
            int n = Mathf.Max(1, (int)ctx.Val);
            if (ctx.Entity.Turn.cardsPlayedThisTurn % n == 0)
                ctx.Entity.energy++;
        });

        // 카드 사용마다 적에게 고정 피해 (힘/약화/증폭 무시)
        Custom("fullCommitDamage", ctx =>
        {
            int damage = ctx.Core.coreEffect.damage > 0
                ? ctx.Core.coreEffect.damage
                : Mathf.Max(1, (int)ctx.Val);
            ctx.Engine.ApplyDamage(ctx.Opponent, damage, ctx.Entity);
        });

        // 네트워크 카드 3장마다 드로우 1
        Custom("drawOnNetworkThreshold", ctx =>
        {
            if (ctx.PlayedCard == null || !ctx.PlayedCard.HasKeyword("network")) return;
            if (ctx.Entity.Turn.networkCardsPlayedThisTurn > 0 &&
                ctx.Entity.Turn.networkCardsPlayedThisTurn % 3 == 0)
                ctx.Engine.DrawCards(ctx.Entity, 1);
        });

        // 세 번째 카드 피해 2배는 효과 실행 전에 CardPlayProcessor에서 적용한다.
        Custom("doubleThirdNetwork", ctx => { });

        // 프로토콜 발동 시 실드 + 적 피해
        Custom("damageAndShieldOnProtocol", ctx =>
        {
            int damage = ctx.Core.coreEffect.damage > 0
                ? ctx.Core.coreEffect.damage
                : Mathf.Max(5, (int)ctx.Val);
            int shield = ctx.Core.coreEffect.shield > 0
                ? ctx.Core.coreEffect.shield
                : damage;

            ctx.Entity.shield += shield;
            ctx.Engine.ApplyDamage(ctx.Opponent, damage, ctx.Entity);
        });

        // 이번 턴 첫 스킬 카드 효과 1회 추가 발동
        Custom("doubleFirstSkill", ctx =>
        {
            if (ctx.PlayedCard == null) return;
            if (ctx.PlayedCard.type != CardType.Skill) return;
            if (ctx.Entity.Turn.skillsPlayedThisTurn != 1) return;
            if (ctx.PlayedCard.effects == null || ctx.PlayedCard.effects.Count == 0) return;

            var effCtx = new EffectContext
            {
                Caster = ctx.Entity,
                Target = ctx.Opponent,
                Card   = ctx.PlayedCard,
                Engine = ctx.Engine,
            };
            ctx.Engine.Interpreter.ExecuteAll(ctx.PlayedCard.effects, effCtx);
        });

        // 도박 결과는 OnGambleResult에서 처리 - 트리거에서 do-nothing
        Custom("rouletteMaster", ctx => { });

        // 이 턴 처음 행운 발동 시 행운 스택 +1, 에너지 +1 (1회)
        Custom("energyOnFirstLuck", ctx =>
        {
            if (ctx.Entity.Turn.luckyThisTurn <= 0) return;
            if (ctx.Entity.Turn.luckyEnergyGainedThisTurn) return;
            ctx.Entity.Turn.luckyEnergyGainedThisTurn = true;
            ctx.Entity.luck++;
            ctx.Entity.energy++;
        });

        // 도박 성공 결과는 OnGambleResult에서 처리 - 트리거에서 do-nothing
        Custom("drawOnLuckyChance", ctx => { });

        // -- Dismantle --

        // 카드 해체마다 적에게 val 피해
        Custom("damageOnDismantle", ctx =>
        {
            int dmg = Mathf.Max(1, (int)ctx.Val);
            ctx.Engine.ApplyDamage(ctx.Opponent, dmg, ctx.Entity);
        });

        // 총 해체 횟수 threshold마다 드로우 1
        Custom("drawOnDismantle", ctx =>
        {
            int th = ctx.Core.coreEffect.threshold > 0
                ? ctx.Core.coreEffect.threshold
                : (ctx.Val > 0 ? (int)ctx.Val : 1);
            if (th > 0 && ctx.Entity.dismantledThisBattle % th == 0)
                ctx.Engine.DrawCards(ctx.Entity, 1);
        });

        // 이번 턴 첫 해체 시 에너지 + 드로우
        Custom("energyAndDrawOnFirstDismantle", ctx =>
        {
            if (ctx.Entity.Turn.dismantledThisTurn != 1) return;
            ctx.Entity.energy += ctx.Core.coreEffect.energy > 0 ? ctx.Core.coreEffect.energy : 1;
            ctx.Engine.DrawCards(ctx.Entity, ctx.Core.coreEffect.draw > 0 ? ctx.Core.coreEffect.draw : 1);
        });

        // -- Rebuild --

        // 재구축 카드 복귀 시 체력 회복 + 코스트 감소
        Custom("healAndCostReductionOnRebuild", ctx =>
        {
            int healAmt = ctx.Core.coreEffect.heal > 0 ? ctx.Core.coreEffect.heal : 20;
            ctx.Entity.hp = Mathf.Min(ctx.Entity.hp + healAmt, ctx.Entity.maxHp);
            if (ctx.PlayedCard != null && ctx.Core.coreEffect.costReduction > 0)
                ctx.PlayedCard.cost = Mathf.Max(0, ctx.PlayedCard.cost - ctx.Core.coreEffect.costReduction);
        });

        // 재구축 발동 시 총 재구축 횟수 x multiplier만큼 피해
        Custom("damageOnRebuild", ctx =>
        {
            float mult = ctx.Core.coreEffect.multiplier > 0 ? ctx.Core.coreEffect.multiplier : ctx.Val;
            int   dmg  = Mathf.RoundToInt(ctx.Entity.totalRebuildsThisBattle * mult);
            if (dmg > 0) ctx.Engine.ApplyDamage(ctx.Opponent, dmg, ctx.Entity);
        });

        // -- VirusConsumed 처리 no-op (OnVirusConsumed에서 별도 처리) --
        Custom("bioWeaponVault",        ctx => { });
        Custom("immuneSystemTakeover",  ctx => { });
        Custom("absoluteCarrierEnergy", ctx => { });
    }
}
