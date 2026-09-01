using UnityEngine;

/// <summary>
/// 래퍼/별칭 핸들러 등록 (40종+)
/// cards.json의 구타입명 → 기존 범용 핸들러로 위임
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterAliasHandlers()
    {
        // -- modifyStat 래퍼 (16종) --
        Register("strength",       ctx => { ctx.Effect.stat = "strength";        ctx.Effect.target = "self";  HandleModifyStat(ctx); });
        Register("tempStrength",   ctx => {
            int amount = Mathf.RoundToInt(ctx.Effect.value);
            ctx.Caster.strength += amount;
            EnqueueDeferred(ctx, "turnEnd", new CardEffect { type = "modifyStat", stat = "strength", target = "self", value = -amount });
        });
        Register("weakness",       ctx => { ctx.Effect.stat = "weakness";        ctx.Effect.target = "enemy"; HandleModifyStat(ctx); });
        Register("virus",          ctx => { ctx.Effect.stat = "virus";           ctx.Effect.target = "enemy"; HandleModifyStat(ctx); });
        Register("corrosion",      ctx => { ctx.Effect.stat = "corrosion"; if (string.IsNullOrEmpty(ctx.Effect.target)) ctx.Effect.target = "enemy"; HandleModifyStat(ctx); });
        Register("virusOnCardPlayedNextTurn", ctx => {
            int amount = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
            ctx.Target.virusOnCardPlayedNextTurn += amount;
        });
        Register("selfWeakness",   ctx => { ctx.Effect.stat = "weakness";        ctx.Effect.target = "self";  HandleModifyStat(ctx); });
        Register("overflow",       ctx => { ctx.Effect.stat = "overflowNext";    ctx.Effect.target = "self";  HandleModifyStat(ctx); });
        Register("overflowReduce", ctx => { ctx.Effect.stat = "overflowNext";    ctx.Effect.target = "self"; ctx.Effect.value = -ctx.Effect.value; HandleModifyStat(ctx); });
        Register("overclockReduce", ctx => {
            int required = Mathf.RoundToInt(Mathf.Abs(ctx.Effect.value));
            if (ctx.Caster.overclockStacks < required)
            {
                ctx.AbortRemainingEffects = true;
                return;
            }
            ctx.Effect.stat   = "overclockStacks";
            ctx.Effect.target = "self";
            ctx.Effect.value  = -required;
            HandleModifyStat(ctx);
        });
        Register("overclockReset",       ctx => { ctx.Effect.stat = "overclockStacks"; ctx.Effect.target = "self"; ctx.Effect.mode = "set"; ctx.Effect.value = 0; HandleModifyStat(ctx); });
        Register("maxHpReduce",          ctx => { ctx.Effect.stat = "maxHp"; ctx.Effect.target = "self"; ctx.Effect.value = -Mathf.Abs(ctx.Effect.value); HandleModifyStat(ctx); });
        Register("doubleLuck",           ctx => { ctx.Effect.stat = "luck"; ctx.Effect.target = "self"; ctx.Effect.mode = "multiply"; ctx.Effect.value = 2; HandleModifyStat(ctx); });
        Register("loseAllLuck",          ctx => { ctx.Effect.stat = "luck"; ctx.Effect.target = "self"; ctx.Effect.mode = "set"; ctx.Effect.value = 0; HandleModifyStat(ctx); });
        Register("doubleVirus",          ctx => { ctx.Effect.stat = "virus"; ctx.Effect.target = "enemy"; ctx.Effect.mode = "multiply"; ctx.Effect.value = 2; HandleModifyStat(ctx); });
        Register("addLuck",              ctx => { ctx.Effect.stat = "luck";      ctx.Effect.target = "self"; HandleModifyStat(ctx); });
        Register("addExtraDraw",         ctx => { ctx.Effect.stat = "extraDraw"; ctx.Effect.target = "self"; HandleModifyStat(ctx); });
        Register("removeOverclockLimit", ctx => { ctx.Effect.stat = "overclockUnlimited"; ctx.Effect.target = "self"; ctx.Effect.mode = "set"; ctx.Effect.value = 1; HandleModifyStat(ctx); });

        // -- scaledEffect 래퍼 (8종) --
        Register("scaledDamage",            ctx => { ctx.Effect.action = "damage";     HandleScaledEffect(ctx); });
        Register("scaledDraw",              ctx => { ctx.Effect.action = "draw";       HandleScaledEffect(ctx); });
        Register("scaledShield",            ctx => { ctx.Effect.action = "shield";     HandleScaledEffect(ctx); });
        Register("scaledHeal",              ctx => { ctx.Effect.action = "heal";       HandleScaledEffect(ctx); });
        Register("scaledSelfDamage",        ctx => { ctx.Effect.action = "selfDamage"; HandleScaledEffect(ctx); });
        Register("scaledSelfDamagePercent", ctx => { ctx.Effect.action = "selfDamage"; HandleScaledEffect(ctx); });
        Register("scaledSelfWeakness",      ctx => { ctx.Effect.action = "modifyStat"; ctx.Effect.stat = "weakness"; ctx.Effect.target = "self"; HandleScaledEffect(ctx); });
        Register("scaledMaxHpReduce",       ctx => { ctx.Effect.action = "modifyStat"; ctx.Effect.stat = "maxHp"; HandleScaledEffect(ctx); });

        // -- 이름 불일치 직접 매핑 (8종) --
        Register("healPercent",               ctx => { ctx.Effect.mode = "percent";       HandleHeal(ctx); });
        Register("endTurn",                   ctx => HandleForceTurnEnd(ctx));
        Register("discardAll",                ctx => { ctx.Effect.countAll = true;         HandleDiscard(ctx); });
        Register("discardRandom",             ctx => { ctx.Effect.mode = "random";         HandleDiscard(ctx); });
        Register("selfDamageByTargetShield",  ctx => { ctx.Effect.mode = "targetShield";   HandleSelfDamage(ctx); });
        Register("shieldBreakAllAndDamage",   ctx => { ctx.Effect.mode = "allAndDamage";   HandleShieldBreak(ctx); });
        Register("casinoRoyalFieldReset",     ctx => HandleCasinoFieldReset(ctx));
        Register("retryGambleOnUnluck",       ctx => HandleRetryOnUnluck(ctx));
        Register("softenNextUnluck", ctx => {
            ctx.Caster.Turn.softenNextUnluckPenalty = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        });
        Register("luckyDayDebt", ctx => {
            ctx.Caster.Turn.luckyDayDebtActive = true;
            ctx.Caster.Turn.luckyDayLuckPerSuccess = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
            ctx.Caster.Turn.luckyDayHpLossPerSuccess = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.bonusMultiplier));
            ctx.Caster.Turn.luckyDaySuccessCount = 0;
        });

        // -- dismantle 래퍼 (6종) --
        Register("dismantleAllAndScaleDraw",   ctx => { ctx.Effect.countAll = true; ctx.Effect.bonus = "drawEqual";   HandleDismantle(ctx); });
        Register("dismantleAllAndScaleShield", ctx => { ctx.Effect.countAll = true; ctx.Effect.bonus = "shieldScale"; HandleDismantle(ctx); });
        Register("dismantleForEnergy",         ctx => { ctx.Effect.bonus = "energyByCost"; HandleDismantle(ctx); });
        Register("dismantleNetworkAndDraw",    ctx => { ctx.Effect.filter = "network"; ctx.Effect.countAll = true; ctx.Effect.bonus = "drawEqual"; HandleDismantle(ctx); });
        Register("dismantleTop",               ctx => { ctx.Effect.source = "deck"; HandleDismantle(ctx); });
        Register("extractTopOrDraw",           ctx => HandleExtractTopOrDraw(ctx));

        // -- 기타 단순 래퍼 (4종) --
        Register("diceRollLuck",              ctx => { int roll = UnityEngine.Random.Range(1, 7); ctx.Caster.luck += roll; });
        Register("costReduceAttackCards",     ctx => { ctx.Effect.filter = "attack"; HandleCostReduceHandCards(ctx); });
        Register("searchByType",              ctx => HandleSearchDeck(ctx));
        Register("revealAndTakeHighestCost",  ctx => { ctx.Effect.filter = "highestCost"; HandleSearchDeck(ctx); });
        Register("cloneHandCard",             ctx => HandleCloneHandCard(ctx));

        // -- 러시안 룰렛 추가 (1종) --
        Register("diceRollShieldDamage", ctx => {
            int roll = UnityEngine.Random.Range(1, 7);
            ctx.Caster.shield += roll;
            ApplyDamage(ctx.Target, roll, ctx.Caster);
            int luckBonus = Mathf.RoundToInt(ctx.Effect.value);
            if (roll == 6 && luckBonus > 0)
                ctx.Caster.luck += luckBonus;
        });

        // -- selfDamage 확장 래퍼 (1종) --
        Register("selfDamagePercent", ctx => { ctx.Effect.mode = "percent"; HandleSelfDamage(ctx); });

        // -- convertResource 래퍼 (3종) --
        Register("energyFromShield", ctx => { ctx.Effect.from = "shield"; ctx.Effect.to = "energy"; HandleConvertResource(ctx); });
        Register("energyToHeal",     ctx => { ctx.Effect.from = "energy"; ctx.Effect.to = "heal";   HandleConvertResource(ctx); });
        Register("energyFromShieldChunk", ctx => {
            int chunkSize = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
            int chunks = ctx.Caster.shield / chunkSize;
            if (chunks <= 0) return;

            ctx.Caster.shield -= chunks * chunkSize;
            ctx.Caster.energy += chunks;
        });

        // -- conditional 데미지 래퍼 (3종) --
        Register("damageIfHandEmpty", ctx => {
            if (ctx.Caster.hand.Count == 0)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
                ApplyDamage(ctx.Target, dmg, ctx.Caster);
            }
        });
        Register("damageIfConsecutiveLuck", ctx => {
            if (ctx.Caster.consecutiveLuck >= 1)
            {
                int dmg = Mathf.RoundToInt(ctx.Effect.value * ctx.Caster.consecutiveLuck);
                if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);
            }
        });
        Register("damagePlusByLuckyThisBattle", ctx => {
            ctx.Effect.action = "damage";
            if (ctx.Effect.scaling == null)
                ctx.Effect.scaling = new ScalingData { source = "luckyThisBattle", multiplier = 1 };
            HandleScaledEffect(ctx);
        });

        // -- scaledEffect / modifyStat 래퍼 (3종) --
        Register("damagePlusByUnluckyThisBattle", ctx => {
            ctx.Effect.action = "damage";
            if (ctx.Effect.scaling == null)
                ctx.Effect.scaling = new ScalingData { source = "unluckyThisBattle", multiplier = 1 };
            HandleScaledEffect(ctx);
        });
        Register("damageByLuckStack", ctx => {
            ctx.Effect.action = "damage";
            if (ctx.Effect.scaling == null)
                ctx.Effect.scaling = new ScalingData { source = "luck", multiplier = 1 };
            HandleScaledEffect(ctx);
        });
        Register("nextGambleBonusChance", ctx => {
            int amount = Mathf.Max(0, Mathf.RoundToInt(ctx.Effect.value));
            ctx.Caster.Turn.gambleBonusChance = Mathf.Max(ctx.Caster.Turn.gambleBonusChance, amount);
        });

        // -- costReduce 래퍼 (2종) --
        Register("zeroCostHandCard",    ctx => { ctx.Effect.mode = "set"; ctx.Effect.value = 0; HandleCostReduceHandCards(ctx); });
        Register("costReductionRandom", ctx => { ctx.Effect.mode = "random"; HandleCostReduceHandCards(ctx); });

        // -- 기타 래퍼 (2종) --
        Register("restoreMaxEnergy",              ctx => { ctx.Effect.stat = "energy"; ctx.Effect.target = "self"; ctx.Effect.source = "baseEnergy"; ctx.Effect.mode = "set"; HandleModifyStat(ctx); });
        Register("discardTopAndGainShieldByCost", ctx => HandleDiscardTopAndShield(ctx));

        // -- 데미지 변형 (3종) --
        Register("damageMaxPercent", ctx => {
            int dmg = Mathf.RoundToInt(ctx.Target.maxHp * (ctx.Effect.value / 100f));
            if (ctx.Effect.scaling != null && ctx.Effect.scaling.max > 0)
                dmg = Mathf.Min(dmg, ctx.Effect.scaling.max);
            if (ctx.Effect.scaling != null && ctx.Effect.scaling.min > 0)
                dmg = Mathf.Max(dmg, ctx.Effect.scaling.min);
            if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);
        });
        Register("lostHpDamageByOverclock", ctx => {
            int lostHp = Mathf.Max(0, ctx.Target.maxHp - ctx.Target.hp);
            float rate = ctx.Effect.value;
            if (ctx.Effect.scaling != null)
                rate += GetScalingSourceValue(ctx.Effect.scaling.source, ctx) * ctx.Effect.scaling.multiplier;

            int dmg = Mathf.RoundToInt(lostHp * rate);
            if (ctx.Effect.scaling != null && ctx.Effect.scaling.max > 0)
                dmg = Mathf.Min(dmg, ctx.Effect.scaling.max);
            if (ctx.Effect.scaling != null && ctx.Effect.scaling.min > 0)
                dmg = Mathf.Max(dmg, ctx.Effect.scaling.min);
            if (dmg > 0) ApplyAttackDamage(ctx, dmg);
        });
        Register("damageIfUnluckyThisTurn", ctx => {
            if (ctx.Caster.Turn.unluckyThisTurn > 0)
            {
                int dmg = Mathf.RoundToInt(ctx.Effect.value);
                if (dmg > 0) ApplyDamage(ctx.Target, dmg, ctx.Caster);
            }
        });

        // -- 에너지/자원 래퍼 (1종) --
        Register("restoreCardCost", ctx => {
            if (ctx.Card != null)
                ctx.Card.cost = Mathf.RoundToInt(ctx.Effect.value);
        });

        // -- 방어/상태 (6종) --
        Register("shieldByHpPercent", ctx => {
            int shield = Mathf.RoundToInt(ctx.Caster.maxHp * (ctx.Effect.value / 100f));
            if (shield > 0) ctx.Caster.shield += shield;
        });
        Register("shieldByLastHpLoss", ctx => {
            int shield = Mathf.Max(0, ctx.Caster.Turn.lastHpLoss);
            if (shield > 0) ctx.Caster.shield += shield;
            ctx.Caster.Turn.lastHpLoss = 0;
        });
        Register("damageReduction", ctx => {
            ctx.Caster.Turn.damageReductionThisTurn = ctx.Effect.value / 100f;
        });
        Register("invincible", ctx => {
            ctx.Caster.Turn.invincibleThisTurn = true;
        });
        Register("evadeNextHit", ctx => {
            int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
            ctx.Caster.Turn.evadeNextHits += count;
        });
        Register("nextAttackBonus", ctx => {
            ctx.Caster.Turn.nextAttackBonus += Mathf.RoundToInt(ctx.Effect.value);
        });
        Register("retaliateOnHit", ctx => {
            int damage = Mathf.RoundToInt(ctx.Effect.value);
            if (damage > 0) ctx.Caster.Turn.retaliateOnHitDamage += damage;
        });

        // -- 감염 (1종) --
        Register("virusByDebuff", ctx => {
            int debuffs = ctx.Target.weakness + ctx.Target.corrosion;
            int virusGain = Mathf.RoundToInt(debuffs * ctx.Effect.value);
            if (virusGain > 0) ctx.Target.virus += virusGain;
        });

        // -- 오버클럭 (1종) --
        Register("costReductionPerOverclock", ctx => {
            int reduction = ctx.Caster.overclockStacks * Mathf.RoundToInt(ctx.Effect.value);
            if (reduction <= 0) return;
            foreach (var card in ctx.Caster.hand)
                card.cost = Mathf.Max(0, card.cost - reduction);
        });

        // -- HP 스케일 (2종) --
        Register("setHpScale", ctx => {
            ctx.Caster.hp = Mathf.RoundToInt(ctx.Caster.hp * ctx.Effect.value);
            ctx.Caster.hp = Mathf.Clamp(ctx.Caster.hp, 0, ctx.Caster.maxHp);
        });
        Register("setHpScaleStoreLoss", ctx => {
            int before = ctx.Caster.hp;
            ctx.Caster.hp = Mathf.RoundToInt(ctx.Caster.hp * ctx.Effect.value);
            ctx.Caster.hp = Mathf.Clamp(ctx.Caster.hp, 0, ctx.Caster.maxHp);
            ctx.Caster.Turn.lastHpLoss = Mathf.Max(0, before - ctx.Caster.hp);
        });
        Register("setEnemyHpScale", ctx => {
            ctx.Target.hp = Mathf.RoundToInt(ctx.Target.hp * ctx.Effect.value);
            ctx.Target.hp = Mathf.Clamp(ctx.Target.hp, 0, ctx.Target.maxHp);
        });

        // -- 누락 보완 (2종) --

        // dismantleAllAndVoidRecall: 현재 손패를 보관하고, 최근 소멸 카드로 임시 교체
        Register("dismantleAllAndVoidRecall", ctx => {
            var caster = ctx.Caster;
            if (caster.rollbackStoredHand == null)
                caster.rollbackStoredHand = new System.Collections.Generic.List<CardData>();

            var handCopy = new System.Collections.Generic.List<CardData>(caster.hand);
            caster.rollbackStoredHand.AddRange(handCopy);
            caster.hand.Clear();

            int recall = ctx.Effect.recallCount > 0 ? ctx.Effect.recallCount : 1;
            for (int i = 0; i < recall && caster.voidPile.Count > 0 && caster.hand.Count < 10; i++)
            {
                int idx = caster.voidPile.Count - 1;
                var recalled = caster.voidPile[idx];
                caster.voidPile.RemoveAt(idx);
                recalled.isTemporary = true;
                caster.hand.Add(recalled);
            }
        });

        // preventSelfDamageIfOverclock: OC 스택이 threshold 이상이면 자해 면제 플래그 설정
        Register("preventSelfDamageIfOverclock", ctx => {
            int threshold = ctx.Effect.threshold > 0 ? ctx.Effect.threshold : 1;
            if (ctx.Caster.overclockStacks >= threshold)
                ctx.Caster.Turn.preventNextSelfDamage = true;
        });
    }
}
