using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방어자(적) AI의 카드 인벤토리 풀로부터 전투용 덱을 구성한다.
///
/// 구성 규칙(플레이어 덱 규칙과 동일하게 맞춤):
///  - 덱 크기: 풀 크기에 비례해 적당히 산정(풀 × PoolUsageRatio, 최소 Deck.MinDeckSize, 상한 없음)
///  - 전략 반영: 공격형/방어형에 따라 Attack/Skill 비율 조정
///  - 선별: 보유 수량 존중(비복원 추출) + 티어 가중 무작위
///  - Core 카드: 종류(ID)별 최대 1장(플레이어 덱 규칙과 동일). 같은 Core 2장 이상 불가, 서로 다른 Core는 각각 1장씩 가능.
///  - 부족분: 기본 저티어 카드로 보충
///
/// 입력 inventoryCardIds 는 보유 수량만큼 같은 ID가 중복 포함된 "펼쳐진 풀"이다.
/// (예: A를 2장 보유하면 "A","A" 로 들어온다.) 비복원 추출이 곧 수량 한도가 된다.
/// </summary>
public static class InventoryDeckBuilder
{
    public const float PoolUsageRatio = 0.7f;

    /// <summary>
    /// 인벤토리 풀로 덱을 구성해 반환한다. 구성 불가 시 빈 리스트를 반환하며,
    /// 호출 측(BattleInitializer)이 임시 덱으로 폴백한다.
    /// 반환 카드들은 독립 인스턴스(Clone)이며 셔플된 상태다.
    /// </summary>
    public static List<CardData> Build(List<string> inventoryCardIds, AIStrategy strategy)
    {
        var db = CardDatabase.Instance;
        if (db == null || inventoryCardIds == null || inventoryCardIds.Count == 0)
            return new List<CardData>();

        // 1) 풀 구성: ID → CardData. Attack/Skill 등은 보유 수량(중복) 유지.
        //    Core(파워) 카드는 플레이어 규칙과 동일하게 "종류(ID)별 최대 1장"이므로 ID당 1장으로 중복 제거한다.
        //    (서로 다른 Core는 각각 1장씩 후보가 되고, 같은 Core 2장 이상은 후보에서 제외된다.)
        var attacks = new List<CardData>();
        var others = new List<CardData>();
        var seenCoreIds = new HashSet<string>();
        foreach (var id in inventoryCardIds)
        {
            var card = db.GetById(id);
            if (card == null) continue;

            if (card.type == CardType.Attack)
            {
                attacks.Add(card);
            }
            else if (card.type == CardType.Core)
            {
                if (seenCoreIds.Add(card.id))   // 같은 Core ID는 한 번만 후보로
                    others.Add(card);
            }
            else
            {
                others.Add(card);
            }
        }

        int poolSize = attacks.Count + others.Count;
        if (poolSize == 0)
            return new List<CardData>();

        // 2) 덱 크기 산정: 풀에 비례해 적당히. 최소 Deck.MinDeckSize, 상한 없음(플레이어와 동일).
        int target = Mathf.Max(Deck.MinDeckSize, Mathf.RoundToInt(poolSize * PoolUsageRatio));

        // 3) 전략별 공격 카드 비율로 슬롯 배분
        float attackRatio = AttackRatioFor(strategy);
        int attackTarget = Mathf.RoundToInt(target * attackRatio);
        int otherTarget = target - attackTarget;

        // 4) 가중 무작위 선별 (비복원 = 보유 수량 / Core 종류별 1장 한도 내)
        var deck = new List<CardData>(target);
        WeightedTake(deck, attacks, attackTarget);
        WeightedTake(deck, others, otherTarget);

        // 한쪽 버킷이 부족했다면 다른 버킷에서 남는 만큼 보충
        int remaining = target - deck.Count;
        if (remaining > 0) WeightedTake(deck, attacks, remaining);
        remaining = target - deck.Count;
        if (remaining > 0) WeightedTake(deck, others, remaining);

        // 5) 풀이 target보다 작아 여전히 부족하면 기본 저티어 카드로 보충
        if (deck.Count < target)
            PadWithBasics(deck, target, db);

        // 6) 독립 인스턴스 복제 + 셔플
        var cloned = new List<CardData>(deck.Count);
        foreach (var c in deck)
            cloned.Add(c.Clone());
        BattleUtils.Shuffle(cloned);

        Debug.Log($"[InventoryDeckBuilder] 인벤토리 덱 구성: 풀 {poolSize}장 → 덱 {cloned.Count}장 " +
                  $"(strategy={strategy}, 목표 {target}, atk비율≈{attackRatio:0.0})");
        return cloned;
    }

    // -----------------------------------------------------------------

    private static float AttackRatioFor(AIStrategy strategy)
    {
        switch (strategy)
        {
            case AIStrategy.Aggressive: return 0.8f;
            case AIStrategy.Defensive:  return 0.3f;
            default:                    return 0.6f; // Balanced / Random
        }
    }

    /// <summary>
    /// source에서 가중 무작위로 count장을 비복원 추출해 dest에 추가한다.
    /// 추출한 카드는 source에서 제거되므로 보유 수량을 초과하지 않는다.
    /// </summary>
    private static void WeightedTake(List<CardData> dest, List<CardData> source, int count)
    {
        for (int i = 0; i < count && source.Count > 0; i++)
        {
            int idx = WeightedPickIndex(source);
            dest.Add(source[idx]);
            source.RemoveAt(idx);
        }
    }

    private static int WeightedPickIndex(List<CardData> source)
    {
        float total = 0f;
        for (int i = 0; i < source.Count; i++)
            total += CardWeight(source[i]);

        float r = Random.Range(0f, total);
        for (int i = 0; i < source.Count; i++)
        {
            r -= CardWeight(source[i]);
            if (r <= 0f) return i;
        }
        return source.Count - 1;
    }

    // 높은 티어·희귀도 카드일수록 약간 더 자주 채택되도록 가중치 부여.
    private static float CardWeight(CardData card)
    {
        return Mathf.Max(1, card.tier) + RarityWeight(card.rarity);
    }

    private static float RarityWeight(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Rare:      return 1f;
            case CardRarity.Epic:      return 2f;
            case CardRarity.Unique:    return 3f;
            case CardRarity.Legendary: return 4f;
            default:                   return 0f; // Common
        }
    }

    /// <summary>
    /// 풀이 목표 장수보다 작을 때 기본 저티어(≤2) 비-Core 카드로 부족분을 채운다.
    /// </summary>
    private static void PadWithBasics(List<CardData> deck, int target, CardDatabase db)
    {
        var basics = new List<CardData>();
        foreach (var card in db.GetAll())
        {
            if (card.type == CardType.Core) continue;
            if (card.tier <= 2) basics.Add(card);
        }

        if (basics.Count == 0)
            return;

        while (deck.Count < target)
            deck.Add(basics[Random.Range(0, basics.Count)]);
    }
}
