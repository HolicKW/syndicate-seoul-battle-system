using UnityEngine;

/// <summary>
/// 카드 사용 가능 여부 판단 및 유효 코스트 계산.
/// BattleEngine에서 분리된 코스트 관련 로직을 담당한다.
/// EnemyAI, UI 등 외부에서 BattleEngine 없이도 코스트를 조회할 수 있도록 독립 클래스로 분리.
/// </summary>
public class CostCalculator
{
    private readonly BattleEngine engine;

    public CostCalculator(BattleEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>
    /// dynamicCost / scaledCost 조건을 평가해 실제 적용 코스트를 반환한다.
    /// 조건 미충족 또는 데이터 없으면 card.cost를 그대로 반환.
    /// </summary>
    public int GetEffectiveCost(CardData card, EntityState caster)
    {
        if (caster == null) return card.cost;

        // scaledCost 우선: 소스 값에 따라 코스트 감소
        if (card.scaledCost != null)
        {
            int sourceVal = GetScaledCostSourceValue(card.scaledCost.source, caster);
            int reduced = card.cost - sourceVal * card.scaledCost.reductionPerUnit;
            return Mathf.Max(card.scaledCost.min, reduced);
        }

        // dynamicCost: 조건 충족 시 고정 코스트로 대체
        if (card.dynamicCost != null && card.dynamicCost.condition != null && caster.opponent != null)
        {
            var ctx = new EffectContext { Caster = caster, Target = caster.opponent, Engine = engine };
            return EffectInterpreter.EvaluateCondition(card.dynamicCost.condition, ctx)
                ? card.dynamicCost.cost
                : card.cost;
        }

        return card.cost;
    }

    /// <summary>
    /// 해당 엔티티가 손패 인덱스의 카드를 사용할 수 있는지 확인한다.
    /// </summary>
    public bool CanPlayCard(int handIndex, EntityState caster)
    {
        if (engine.IsBattleEnded) return false;
        if (caster == null) return false;
        if (handIndex < 0 || handIndex >= caster.hand.Count) return false;
        if (caster.forceEndTurn) return false;

        var card = caster.hand[handIndex];

        // 오버클럭 스택 소모 카드: 스택이 없으면 사용 불가
        if (card.requiresOverclock && caster.overclockStacks <= 0) return false;

        // 과부하(n) 카드: 오버클럭 스택이 n 미만이면 사용 불가
        if (card.overclockThreshold > 0 && caster.overclockStacks < card.overclockThreshold) return false;

        // 과부하(n) 카드: 오버플로우 스택이 n 미만이면 사용 불가
        if (card.overflowMin > 0 && caster.overflowNext < card.overflowMin) return false;

        // 폭주(n) 카드: 적 바이러스가 n 미만이면 사용 불가
        if (card.virusThreshold > 0)
        {
            var foe = caster.opponent;
            if (foe == null || foe.virus < card.virusThreshold) return false;
        }

        int effectiveCost = GetEffectiveCost(card, caster);
        if (caster.energy >= effectiveCost) return true;

        // bankruptcy 코어: 에너지 부족해도 HP 소모로 사용 가능
        var bankruptcyCore = FindBankruptcyCore(caster);
        if (bankruptcyCore != null)
        {
            int deficit = effectiveCost - caster.energy;
            int rate = GetBankruptcyCoreRate(bankruptcyCore);
            int hpCost = deficit * rate;
            return caster.hp > hpCost; // HP가 부족하면 사용 불가 (자살 방지)
        }
        return false;
    }

    /// <summary>
    /// bankruptcy 코어 카드를 찾아 반환한다. 없으면 null.
    /// </summary>
    public static CardData FindBankruptcyCore(EntityState entity)
    {
        if (entity.activeCores == null) return null;
        foreach (var core in entity.activeCores)
        {
            if (core.coreEffect?.coreType == "bankruptcy") return core;
        }
        return null;
    }

    /// <summary>
    /// bankruptcy의 에너지 1당 HP 소모량을 반환한다. (카드 value 기본 10)
    /// </summary>
    public static int GetBankruptcyCoreRate(CardData bankruptcyCore)
    {
        float val = bankruptcyCore.coreEffect.value;
        return val > 0 ? (int)val : 10;
    }

    private static int GetScaledCostSourceValue(string source, EntityState caster)
    {
        return source switch
        {
            "overclockStacks" => caster.overclockStacks,
            "strength"        => caster.strength,
            "shield"          => caster.shield,
            "energy"          => caster.energy,
            _                 => 0,
        };
    }
}
