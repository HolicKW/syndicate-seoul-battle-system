using UnityEngine;

/// <summary>
/// 프로토콜 조건 판정 및 바이패스 소모 처리.
/// BattleEngine에서 분리된 프로토콜 전용 클래스.
///
/// 핵심 설계 원칙:
///   - CheckAndConsume : PlayCard 흐름에서 호출. 바이패스가 있으면 소모하고 발동.
///   - Evaluate        : 소모 없이 조건만 판정. 미래의 UI preview 등에 사용.
///
/// 주의: CheckAndConsume은 바이패스 소모 사이드이펙트가 있으므로
///       판정 목적으로만 쓰이는 호출에는 반드시 Evaluate를 사용할 것.
/// </summary>
public class ProtocolResolver
{
    private readonly BattleEngine engine;

    public ProtocolResolver(BattleEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>
    /// 프로토콜 조건을 판정하고 바이패스가 있으면 1회 소모한다.
    /// PlayCard 흐름의 6-A 단계에서만 호출해야 한다.
    /// </summary>
    public bool CheckAndConsume(CardData card, EntityState caster)
    {
        if (string.IsNullOrEmpty(card.protocolCondition)) return false;

        // 바이패스 소모 (조건과 무관하게 강제 발동)
        if (caster.protocolBypassCount > 0)
        {
            caster.protocolBypassCount--;
            Debug.Log($"[ProtocolResolver] 바이패스 소모: {card.cardName} → 남은 바이패스 {caster.protocolBypassCount}");
            BattleLogger.Log(BattleLogType.Info,
                $"[프로토콜 바이패스 소모] {card.cardName} → 남은 바이패스: {caster.protocolBypassCount}");
            return true;
        }

        return Evaluate(card, caster);
    }

    /// <summary>
    /// 바이패스를 소모하지 않고 조건만 판정한다.
    /// UI preview, AI 판단 등 순수 조회 목적에 사용한다.
    /// </summary>
    public bool Evaluate(CardData card, EntityState caster)
    {
        if (string.IsNullOrEmpty(card.protocolCondition)) return false;

        // 이전 카드와 무관한 조건
        if (card.protocolCondition == "any") return true;
        if (card.protocolCondition == "luckMin") return caster.luck >= card.protocolConditionValue;

        var lastCard = caster.lastPlayedCard;
        if (lastCard == null) return false;

        return card.protocolCondition switch
        {
            "sameType"      => lastCard.type == card.type,
            "differentType" => lastCard.type != card.type,
            "attack"        => lastCard.type == CardType.Attack,
            "skill"         => lastCard.type == CardType.Skill,
            "network"       => lastCard.HasKeyword("network"),
            "costHigher"    => lastCard.cost > card.cost,
            "costLower"     => lastCard.cost < card.cost,
            "zeroCost"      => lastCard.cost == 0,
            _               => false,
        };
    }
}
