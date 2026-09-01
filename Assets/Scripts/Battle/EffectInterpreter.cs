using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트 핸들러 레지스트리 + 실행기.
/// type 문자열 → Action&lt;EffectContext&gt; 매핑으로 모든 카드 이펙트를 처리한다.
///
/// 핸들러 구현은 partial class 파일로 분리:
///   EffectInterpreter.CoreHandlers.cs    - Tier 1: damage/shield/modifyStat 등
///   EffectInterpreter.BasicHandlers.cs   - Tier 2: heal/draw/energy 등
///   EffectInterpreter.OverclockHandlers.cs
///   EffectInterpreter.DismantleHandlers.cs
///   EffectInterpreter.BiohazardHandlers.cs
///   EffectInterpreter.GambleHandlers.cs
///   EffectInterpreter.NetworkHandlers.cs
///   EffectInterpreter.SpecialHandlers.cs
///   EffectInterpreter.DeferredHandlers.cs
///   EffectInterpreter.AliasHandlers.cs
/// </summary>
public partial class EffectInterpreter
{
    private readonly Dictionary<string, Action<EffectContext>> handlers = new();

    public void Register(string type, Action<EffectContext> handler)
    {
        handlers[type] = handler;
    }

    public void Execute(EffectContext ctx)
    {
        if (ctx.Effect == null || string.IsNullOrEmpty(ctx.Effect.type))
        {
            Debug.LogWarning("[EffectInterpreter] Effect가 null이거나 type이 비어있습니다.");
            return;
        }

        if (!handlers.TryGetValue(ctx.Effect.type, out var handler))
        {
            Debug.LogWarning($"[EffectInterpreter] 미등록 핸들러: {ctx.Effect.type}");
            BattleLogger.Log(BattleLogType.Warning, $"미등록 핸들러: {ctx.Effect.type}");
            return;
        }

        try
        {
            handler(ctx);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EffectInterpreter] 핸들러 '{ctx.Effect.type}' 실행 중 예외 발생: {ex}");
            BattleLogger.Log(BattleLogType.Warning, $"핸들러 '{ctx.Effect.type}' 예외: {ex.Message}");
        }
    }

    /// <summary>
    /// 이펙트 리스트를 순서대로 실행한다. 각 이펙트마다 ctx.Effect를 교체하며 실행.
    /// </summary>
    public void ExecuteAll(List<CardEffect> effects, EffectContext ctx)
    {
        if (effects == null) return;

        ctx.Depth++;
        if (ctx.Depth > 10)
        {
            Debug.LogWarning("[EffectInterpreter] 재귀 깊이 초과 (최대 10). 순환 참조 의심.");
            ctx.Depth--;
            return;
        }

        try
        {
            foreach (var effect in effects)
            {
                if (ctx.Engine != null && ctx.Engine.IsBattleEnded) break;

                ctx.Effect = effect;
                Execute(ctx);

                if (ctx.AbortRemainingEffects)
                {
                    ctx.AbortRemainingEffects = false;
                    break;
                }
            }
        }
        finally
        {
            ctx.Depth--;
        }
    }

    public bool HasHandler(string type) => handlers.ContainsKey(type);

    // ===================================================
    //  핸들러 일괄 등록
    // ===================================================

    /// <summary>
    /// 모든 코어 핸들러를 등록한다. 생성 직후 1회 호출.
    /// </summary>
    public void RegisterAllCoreHandlers()
    {
        RegisterCoreHandlers();       // Tier 1
        RegisterBasicHandlers();      // Tier 2
        RegisterOverclockHandlers();  // Tier 3
        RegisterDismantleHandlers();  // Tier 3
        RegisterBiohazardHandlers();  // Tier 3
        RegisterGambleHandlers();     // Tier 3
        RegisterNetworkHandlers();    // Tier 3
        RegisterSpecialHandlers();    // Tier 4 + 신규
        RegisterDeferredHandlers();   // 지연 실행
        RegisterAliasHandlers();      // 래퍼/별칭
    }

    // ===================================================
    //  공유 헬퍼 메서드
    // ===================================================

    private static void EnqueueDeferred(EffectContext ctx, string timing, CardEffect effect)
    {
        ctx.Caster.deferredActions.Add(new DeferredAction
        {
            timing = timing,
            effect = effect,
            caster = ctx.Caster,
            target = ctx.Target,
            card = ctx.Card,
        });
    }

    private static EntityState ResolveTarget(EffectContext ctx)
    {
        return ctx.Effect.target == "self" ? ctx.Caster : ctx.Target;
    }

    private static float OcVal(EffectContext ctx)
    {
        if (ctx.Effect.overclockScale && ctx.OverclockScale > 0)
            return ctx.Effect.value * ctx.OverclockScale;
        return ctx.Effect.value;
    }

    private void ApplyAttackDamage(EffectContext ctx, float baseDmg, int hits = 1)
    {
        float dmg = baseDmg;

        if (!ctx.StrengthApplied)
        {
            dmg += ctx.Caster.strength;
            dmg -= ctx.Caster.weakness;
            ctx.StrengthApplied = true;
        }

        if (ctx.Caster.Turn.nextAttackBonus > 0)
        {
            dmg += ctx.Caster.Turn.nextAttackBonus;
            ctx.Caster.Turn.nextAttackBonus = 0;
        }

        if (ctx.Caster.Turn.nextDamageMultiplier > 0f)
        {
            dmg *= ctx.Caster.Turn.nextDamageMultiplier;
            ctx.Caster.Turn.nextDamageMultiplier = 0f;
        }

        if (ctx.CardDamageMultiplier > 0f)
            dmg *= ctx.CardDamageMultiplier;

        int finalDmg = Mathf.Max(1, Mathf.RoundToInt(dmg));

        bool pierce = ctx.Effect.bypassShield || ctx.Caster.Turn.armorPierceNextAttack;
        if (ctx.Caster.Turn.armorPierceNextAttack) ctx.Caster.Turn.armorPierceNextAttack = false;

        for (int i = 0; i < hits; i++)
            ApplyDamage(ctx.Target, finalDmg, ctx.Caster, pierce);
    }

    private static void ApplySelfDamage(EntityState caster, int amount)
    {
        if (amount <= 0) return;

        if (caster.Turn.preventNextSelfDamage)
        {
            caster.Turn.preventNextSelfDamage = false;
            BattleLogger.Log(BattleLogType.Info, "자해 면제");
            return;
        }

        int shieldAbsorb = Mathf.Min(caster.shield, amount);
        caster.shield -= shieldAbsorb;
        int hpLoss = amount - shieldAbsorb;
        int hpBefore = caster.hp;
        caster.hp = Mathf.Max(0, caster.hp - hpLoss);
        caster.Turn.selfDamageThisTurn += hpLoss;

        string who = BattleEngine.Instance?.Player == caster ? "플레이어" : "적";
        string shieldInfo = shieldAbsorb > 0 ? $", 실드 흡수 {shieldAbsorb}" : "";
        BattleLogger.Log(BattleLogType.Damage, $"{who} 자해: {amount} (HP {hpBefore}→{caster.hp}{shieldInfo})");
    }

    private static void ApplyDamage(EntityState target, int damage, EntityState attacker, bool bypassShield = false)
    {
        if (damage <= 0) return;

        var engine = BattleEngine.Instance;
        if (engine != null)
        {
            engine.ApplyDamage(target, damage, attacker, bypassShield);
            return;
        }

        if (target.Turn.invincibleThisTurn) return;
        if (target.Turn.evadeNextHits > 0)
        {
            target.Turn.evadeNextHits--;
            return;
        }

        if (target.Turn.damageReductionThisTurn > 0f)
        {
            damage = Mathf.RoundToInt(damage * (1f - target.Turn.damageReductionThisTurn));
            if (damage <= 0) return;
        }

        int shieldAbsorb = 0;
        if (!bypassShield)
        {
            shieldAbsorb = Mathf.Min(target.shield, damage);
            target.shield -= shieldAbsorb;
        }

        target.hp = Mathf.Max(0, target.hp - (damage - shieldAbsorb));
        if (attacker != null)
            attacker.Turn.totalDamageThisTurn += damage;
    }

    private static void DrawCard(EntityState entity)
    {
        var engine = BattleEngine.Instance;
        if (engine != null)
        {
            engine.DrawCards(entity, 1);
            return;
        }

        if (entity.drawPile.Count == 0) return;
        if (entity.hand.Count >= 10) return;

        var card = entity.drawPile[0];
        entity.drawPile.RemoveAt(0);
        entity.hand.Add(card);
        entity.lastDrawnCard = card;
    }

    private static float GetScalingSourceValue(string source, EffectContext ctx)
    {
        var caster = ctx.Caster;
        var target = ctx.Target;

        return source switch
        {
            "networkStacks"            => caster.networkStacks,
            "networkStacksWithSelf"    => caster.networkStacks + (caster.doubleNetworkStacks ? 2 : 1),
            "overclockStacks"          => caster.overclockStacks,
            "selfDamageThisTurn"       => caster.Turn.selfDamageThisTurn,
            "totalDamageThisTurn"      => caster.Turn.totalDamageThisTurn,
            "dismantledThisTurn"       => caster.Turn.dismantledThisTurn,
            "dismantledThisBattle"     => caster.dismantledThisBattle,
            "targetVirus"              => target.virus,
            "targetHandCount"          => target.hand != null ? target.hand.Count : 0,
            "corrosion"                => target.corrosion,
            "luck"                     => caster.luck,
            "luckyThisTurn"            => caster.Turn.luckyThisTurn,
            "unluckyThisTurn"          => caster.Turn.unluckyThisTurn,
            "hp"                       => caster.hp,
            "maxHp"                    => caster.maxHp,
            "enemyMaxHp"               => target.maxHp,
            "overflowStacks"           => caster.overflowNext,
            "rebuildCount"             => caster.totalRebuildsThisBattle,
            "hpDifference"             => Mathf.Abs(caster.hp - target.hp),
            "overclockConsumed"        => ctx.OverclockConsumed,
            "energyConsumed"           => ctx.EnergyConsumed,
            "baseEnergy"               => caster.baseEnergy,
            "nonBaseCardsInHand"       => CountNonBaseCards(caster),
            "luckyThisBattle"          => caster.luckyThisBattle,
            "unluckyThisBattle"        => caster.unluckyThisBattle,
            "gambleSuccessThisTurn"    => caster.Turn.gambleSuccessThisTurn,
            "targetLostHp"             => target.maxHp - target.hp,
            "cardAccumCount"           => ctx.Card != null ? ctx.Card.accumulationCount : 0,
            _ => LogAndReturnZero($"[EffectInterpreter] 미등록 scaling source: {source}"),
        };
    }

    private static float GetEffectScalingSourceValue(CardEffect effect, EffectContext ctx)
    {
        if (effect?.scaling == null) return 0f;
        if (UsesFixedDemeritScaling(effect) &&
            effect.scaling.source == "overclockStacks" &&
            ctx.Caster.fixedOverclockStacks > 0)
            return ctx.Caster.fixedOverclockStacks;

        return GetScalingSourceValue(effect.scaling.source, ctx);
    }

    private static bool UsesFixedDemeritScaling(CardEffect effect)
    {
        if (effect == null) return false;
        if (effect.action == "selfDamage") return true;
        return effect.stat == "overflowNext" && effect.target == "self";
    }

    private static float LogAndReturnZero(string message)
    {
        Debug.LogWarning(message);
        return 0f;
    }

    private static bool LogConditionWarning(string condType)
    {
        Debug.LogWarning($"[EffectInterpreter] 미등록 condition type: {condType}");
        return false;
    }

    private static int CountNonBaseCards(EntityState entity)
    {
        int count = 0;
        foreach (var card in entity.hand)
        {
            if (card.pack != "base") count++;
        }
        return count;
    }

    private static void ModifyEntityStat(EntityState entity, string stat, float value, string mode, EffectContext ctx)
    {
        int current = GetStatValue(entity, stat);

        int newValue;
        if (mode == "set")
            newValue = Mathf.RoundToInt(value);
        else if (mode == "multiply")
            newValue = Mathf.RoundToInt(current * value);
        else
            newValue = current + Mathf.RoundToInt(value);

        SetStatValue(entity, stat, newValue);

        if (stat == "maxHp" && entity.hp > entity.maxHp)
            entity.hp = entity.maxHp;
        if (stat == "hp")
            entity.hp = Mathf.Clamp(entity.hp, 0, entity.maxHp);
    }

    private static int GetStatValue(EntityState e, string stat)
    {
        return stat switch
        {
            "hp"                    => e.hp,
            "maxHp"                 => e.maxHp,
            "shield"                => e.shield,
            "energy"                => e.energy,
            "baseEnergy"            => e.baseEnergy,
            "strength"              => e.strength,
            "weakness"              => e.weakness,
            "virus"                 => e.virus,
            "corrosion"             => e.corrosion,
            "overclockStacks"       => e.overclockStacks,
            "overflowNext"          => e.overflowNext,
            "luck"                  => e.luck,
            "gambleBonusChance"     => e.Turn.gambleBonusChance,
            "consecutiveLuck"       => e.consecutiveLuck,
            "networkStacks"         => e.networkStacks,
            "extraDraw"             => e.extraDraw,
            "rebuildBonuses"        => e.rebuildBonuses,
            "doubleNetworkStacks"   => e.doubleNetworkStacks ? 1 : 0,
            "energyCarryOver"       => e.energyCarryOver ? 1 : 0,
            "overclockUnlimited"    => e.overclockUnlimited ? 1 : 0,
            _ => 0,
        };
    }

    private static void SetStatValue(EntityState e, string stat, int v)
    {
        switch (stat)
        {
            case "hp":                  e.hp = v; break;
            case "maxHp":               e.maxHp = Mathf.Max(1, v); break;
            case "shield":              e.shield = Mathf.Max(0, v); break;
            case "energy":              e.energy = v; break;
            case "baseEnergy":          e.baseEnergy = v; break;
            case "strength":            e.strength = v; break;
            case "weakness":            e.weakness = Mathf.Max(0, v); break;
            case "virus":               e.virus = Mathf.Max(0, v); break;
            case "corrosion":           e.corrosion = Mathf.Max(0, v); break;
            case "overclockStacks":     e.overclockStacks = Mathf.Max(0, v); break;
            case "overflowNext":        e.overflowNext = Mathf.Max(0, v); break;
            case "luck":                e.luck = Mathf.Max(0, v); break;
            case "gambleBonusChance":   e.Turn.gambleBonusChance = v; break;
            case "consecutiveLuck":     e.consecutiveLuck = v; break;
            case "networkStacks":       e.networkStacks = Mathf.Max(0, v); break;
            case "extraDraw":           e.extraDraw = v; break;
            case "rebuildBonuses":      e.rebuildBonuses = v; break;
            case "doubleNetworkStacks": e.doubleNetworkStacks = (v != 0); break;
            case "energyCarryOver":     e.energyCarryOver = (v != 0); break;
            case "overclockUnlimited":  e.overclockUnlimited = (v != 0); break;
            default:
                Debug.LogWarning($"[EffectInterpreter] ModifyStat 미지원 stat: {stat}");
                break;
        }
    }

    private static bool MatchesFilter(CardData card, string filter)
    {
        if (filter == "network")     return card.HasKeyword("network");
        if (filter == "overclock")   return card.HasKeyword("overclock");
        if (filter == "extract")     return card.HasKeyword("extract");
        if (filter == "virus")       return card.HasKeyword("virus");
        if (filter == "attack")      return card.type == CardType.Attack;
        if (filter == "skill")       return card.type == CardType.Skill;
        if (filter == "core")       return card.type == CardType.Core;
        if (filter == "coreOrSkill") return card.type == CardType.Core || card.type == CardType.Skill;
        return false;
    }

    private static void ShuffleDeck(List<CardData> deck) => BattleUtils.Shuffle(deck);

    private bool ResolveGamble(EffectContext ctx, float baseChance)
    {
        var caster = ctx.Caster;
        caster.Turn.lastGambleUnluckSoftened = false;
        if (baseChance > 1f) baseChance /= 100f;
        float chance = Mathf.Clamp01(baseChance
            + caster.luck * 0.01f
            + caster.Turn.gambleBonusChance * 0.01f);
        caster.Turn.gambleBonusChance = 0;

        bool success = UnityEngine.Random.value < chance;
        bool rerolled = false;

        if (success)
        {
            RegisterGambleSuccess(caster);
        }
        else if (caster.Turn.retryOnUnluckFlag)
        {
            caster.Turn.retryOnUnluckFlag = false;
            rerolled = true;
            success = UnityEngine.Random.value < chance;
            if (success)
            {
                RegisterGambleSuccess(caster);
            }
            BattleLogger.Log(BattleLogType.Gamble,
                $"  [재시도] 도박 {(success ? "★ 성공" : "X 실패")} (확률 {chance * 100f:F0}%)");
        }

        if (!success && HasActiveCore(caster, "bankruptcyReroll", out var bankruptcyCore))
        {
            rerolled = true;
            success = UnityEngine.Random.value < chance;
            if (success)
            {
                RegisterGambleSuccess(caster);
            }
            else
            {
                float penaltyRate = bankruptcyCore.coreEffect.value > 0f
                    ? bankruptcyCore.coreEffect.value
                    : 0.25f;
                if (penaltyRate > 1f) penaltyRate /= 100f;
                int selfDamage = Mathf.RoundToInt(caster.hp * Mathf.Clamp01(penaltyRate));
                int hpBefore = caster.hp;
                caster.TakeDamageRaw(selfDamage);
                caster.Turn.selfDamageThisTurn += hpBefore - caster.hp;
            }

            BattleLogger.Log(BattleLogType.Gamble,
                $"  [파산 선고] 도박 재판정 {(success ? "★ 성공" : "X 실패")} (확률 {chance * 100f:F0}%)");
        }

        if (!success && !caster.ignoreUnluck)
        {
            caster.Turn.unluckyThisTurn++;
            caster.unluckyThisBattle++;
            caster.consecutiveLuck = 0;
            if (caster.Turn.buffInsuranceValue > 0)
                caster.shield += caster.Turn.buffInsuranceValue;
            if (caster.Turn.buffInsuranceLuckValue > 0)
                caster.luck += caster.Turn.buffInsuranceLuckValue;
        }

        if (!success && caster.Turn.softenNextUnluckPenalty > 0)
        {
            int penalty = caster.Turn.softenNextUnluckPenalty;
            caster.Turn.softenNextUnluckPenalty = 0;
            caster.luck = Mathf.Max(0, caster.luck - penalty);
            caster.Turn.lastGambleUnluckSoftened = true;
            BattleLogger.Log(BattleLogType.Gamble,
                $"  [주사위 깎기] 불운 효과를 무시하고 행운 {penalty}을 잃습니다.");
        }

        string cardName = ctx.Card?.cardName ?? "?";
        BattleLogger.Log(BattleLogType.Gamble,
            $"  [{cardName}] 도박 {(success ? "★ 성공" : "X 실패")} (확률 {chance * 100f:F0}%, 행운 {caster.luck})");

        GamblePhase phase = rerolled
            ? GamblePhase.Reroll
            : caster.Turn.lastGambleUnluckSoftened ? GamblePhase.Softened : GamblePhase.FirstRoll;
        ctx.Engine?.NotifyGambleResult(caster, success, ctx.Card, chance * 100f, caster.luck, phase);

        return success;
    }

    private static void RegisterGambleSuccess(EntityState caster)
    {
        caster.Turn.luckyThisTurn++;
        caster.luckyThisBattle++;
        caster.consecutiveLuck++;
        caster.Turn.gambleSuccessThisTurn++;

        if (!caster.Turn.luckyDayDebtActive) return;

        int luckGain = Mathf.Max(1, caster.Turn.luckyDayLuckPerSuccess);
        caster.luck += luckGain;
        caster.Turn.luckyDaySuccessCount++;
    }

    private static bool HasActiveCore(EntityState entity, string coreType, out CardData core)
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

    /// <summary>
    /// 조건 평가 (condition.type 전체 목록 지원).
    /// </summary>
    public static bool EvaluateCondition(ConditionData cond, EffectContext ctx)
    {
        var caster = ctx.Caster;
        var target = ctx.Target;
        float v = cond.value;

        return cond.type switch
        {
            "hpBelow"                   => caster.hp <= caster.maxHp * (v / 100f),
            "hpAbove"                   => caster.hp >= caster.maxHp * (v / 100f),
            "targetHpBelow"             => target.hp <= target.maxHp * (v / 100f),
            "overclockMin"              => caster.overclockStacks >= (int)v,
            "virusMin" or "virusTarget" => target.virus >= (int)v,
            "virusAppliedThisTurnMin"   => target.Turn.virusAppliedThisTurn >= (int)v,
            "corrosionMin"              => target.corrosion >= (int)v,
            "shieldZero"                => target.shield == 0,
            "cardsPlayedThisTurn"       => caster.Turn.cardsPlayedThisTurn >= (int)v,
            "networkCardsPlayed"        => caster.Turn.networkCardsPlayedThisTurn >= (int)v,
            "dismantledThisTurn"        => caster.Turn.dismantledThisTurn >= (int)v,
            "dismantledThisBattle"      => caster.dismantledThisBattle >= (int)v,
            "handEmpty"                 => caster.hand.Count == 0,
            "enemyHasWeakness"          => target.weakness > 0,
            "luckMin"                   => caster.luck >= (int)v,
            "consecutiveLuckMin"        => caster.consecutiveLuck >= (int)v,
            "nonBaseCardsInHand"        => CountNonBaseCards(caster) >= (int)v,
            "gambleSuccessThisTurn"     => caster.Turn.gambleSuccessThisTurn >= (int)v,
            "corrosionTarget"           => target.corrosion > 0,
            "accumulationMet"           => ctx.Card != null && ctx.Card.accumulationCount >= ctx.Card.accumulationTarget,
            "accumulation"              => ctx.Card != null && ctx.Card.accumulationCount >= (int)v,
            _ => LogConditionWarning(cond.type),
        };
    }

    /// <summary>
    /// rebuildScaling이 있으면 동적으로 재구축 횟수를 계산하여 반환.
    /// </summary>
    public int ComputeRebuildCount(CardData card, EntityState caster, EntityState target)
    {
        var scaling = card.rebuildScaling;
        if (scaling == null || string.IsNullOrEmpty(scaling.source))
            return card.rebuildCount;

        var ctx = new EffectContext { Caster = caster, Target = target };
        float sv = GetScalingSourceValue(scaling.source, ctx);
        int count = card.rebuildCount + Mathf.RoundToInt(sv * scaling.multiplier);
        if (scaling.max > 0) count = Mathf.Min(count, scaling.max);
        if (scaling.min > 0) count = Mathf.Max(count, scaling.min);
        return Mathf.Max(0, count);
    }
}
