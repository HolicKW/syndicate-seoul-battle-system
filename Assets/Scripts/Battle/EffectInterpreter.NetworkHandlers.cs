using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tier 3: 네트워크 프로토콜 핸들러 (4종)
/// protocolBypass / nextProtocolShieldBonus / nextProtocolEffectRepeat / armorPierce / costReduceHandCards
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterNetworkHandlers()
    {
        Register("protocolBypass",          HandleProtocolBypass);
        Register("nextProtocolShieldBonus", HandleNextProtocolShieldBonus);
        Register("nextProtocolEffectRepeat", HandleNextProtocolEffectRepeat);
        Register("armorPierce",             HandleArmorPierce);
        Register("costReduceHandCards",     HandleCostReduceHandCards);
    }

    // -- 36. protocolBypass --
    private void HandleProtocolBypass(EffectContext ctx)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        ctx.Caster.protocolBypassCount += count;
        BattleLogger.Log(BattleLogType.Info, $"[프로토콜 바이패스 충전] +{count} → 총 {ctx.Caster.protocolBypassCount}회");
    }

    // -- 37. nextProtocolShieldBonus --
    private void HandleNextProtocolShieldBonus(EffectContext ctx)
    {
        int amount = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        ctx.Caster.nextProtocolShieldBonus += amount;
    }

    // -- 38. nextProtocolEffectRepeat --
    private void HandleNextProtocolEffectRepeat(EffectContext ctx)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(ctx.Effect.value));
        ctx.Caster.nextProtocolEffectRepeat += count;
    }

    // -- 39. armorPierce --
    private void HandleArmorPierce(EffectContext ctx)
    {
        ctx.Caster.Turn.armorPierceNextAttack = true;
    }

    // -- 40. costReduceHandCards --
    private void HandleCostReduceHandCards(EffectContext ctx)
    {
        var hand = ctx.Caster.hand;
        var eff = ctx.Effect;
        int reduction = Mathf.RoundToInt(eff.value);

        if (eff.mode == "random" && hand.Count > 0)
        {
            var candidates = new List<CardData>();
            foreach (var c in hand)
            {
                if (string.IsNullOrEmpty(eff.filter) || MatchesFilter(c, eff.filter))
                    candidates.Add(c);
            }
            int pickCount = Mathf.Min(2, candidates.Count);
            for (int i = 0; i < pickCount; i++)
            {
                int idx = UnityEngine.Random.Range(i, candidates.Count);
                var pick = candidates[idx];
                candidates[idx] = candidates[i];
                candidates[i] = pick;
                pick.cost = Mathf.Max(0, pick.cost - reduction);
            }
            return;
        }

        foreach (var card in hand)
        {
            bool matches = string.IsNullOrEmpty(eff.filter) || MatchesFilter(card, eff.filter);
            if (!matches) continue;

            if (eff.mode == "set")
                card.cost = Mathf.Max(0, reduction);
            else
                card.cost = Mathf.Max(0, card.cost - reduction);
        }
    }
}
