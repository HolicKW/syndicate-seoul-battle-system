using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 운영턴 구현 전까지 적(및 테스트용 플레이어) 임시 덱을 생성한다.
/// CardDatabase에서 카드를 읽어 전략별 비율로 섞어 반환.
/// </summary>
public static class TempDeckFactory
{
    /// <summary>
    /// 균형형 적 덱을 생성한다 (공격 60%, 스킬 40%, 티어 1~2).
    /// </summary>
    public static List<CardData> CreateEnemyDeck(int deckSize = 20)
    {
        return CreateDeck(deckSize, attackRatio: 0.6f, maxTier: 2);
    }

    /// <summary>
    /// 전략별 적 덱을 생성한다.
    /// </summary>
    public static List<CardData> CreateEnemyDeck(AIStrategy strategy, int deckSize = 20)
    {
        switch (strategy)
        {
            case AIStrategy.Aggressive: return CreateDeck(deckSize, attackRatio: 0.8f, maxTier: 2);
            case AIStrategy.Defensive:  return CreateDeck(deckSize, attackRatio: 0.3f, maxTier: 2);
            default:                    return CreateDeck(deckSize, attackRatio: 0.6f, maxTier: 2);
        }
    }

    // -----------------------------------------

    private static List<CardData> CreateDeck(int deckSize, float attackRatio, int maxTier)
    {
        var db = CardDatabase.Instance;
        if (db == null)
        {
            Debug.LogWarning("[TempDeckFactory] CardDatabase 없음. 빈 덱 반환.");
            return new List<CardData>();
        }

        var all = db.GetAll();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning("[TempDeckFactory] CardDatabase 비어있음. 빈 덱 반환.");
            return new List<CardData>();
        }

        var attacks = new List<CardData>();
        var skills  = new List<CardData>();

        foreach (var card in all)
        {
            if (card.type == CardType.Core) continue;
            if (card.tier > maxTier) continue;

            if (card.type == CardType.Attack) attacks.Add(card);
            else                              skills.Add(card);
        }

        var result = new List<CardData>();

        int attackCount = Mathf.RoundToInt(deckSize * attackRatio);
        int skillCount  = deckSize - attackCount;

        AddRandom(result, attacks, attackCount);
        AddRandom(result, skills, skillCount);

        // 부족하면 공격으로 채움
        while (result.Count < deckSize && attacks.Count > 0)
            AddRandom(result, attacks, 1);

        // 각 카드를 독립 인스턴스로 복제
        var cloned = new List<CardData>(result.Count);
        foreach (var c in result)
            cloned.Add(c.Clone());

        Shuffle(cloned);

        Debug.Log($"[TempDeckFactory] 적 덱 생성 완료: {cloned.Count}장");
        return cloned;
    }

    private static void AddRandom(List<CardData> dest, List<CardData> source, int count)
    {
        if (source.Count == 0) return;
        for (int i = 0; i < count; i++)
            dest.Add(source[Random.Range(0, source.Count)]);
    }

    private static void Shuffle(List<CardData> list) => BattleUtils.Shuffle(list);
}
