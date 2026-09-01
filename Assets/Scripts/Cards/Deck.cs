using System;
using System.Collections.Generic;

/// <summary>
/// 덱의 3개 영역(DrawPile, Hand, Void)을 관리하는 데이터 클래스.
/// </summary>
public class Deck
{
    public const int MinDeckSize = 10;
    public const int HandSize = 5;
    public const int MaxHandSize = 10;

    private readonly List<CardData> drawPile = new List<CardData>();
    private readonly List<CardData> hand = new List<CardData>();
    private readonly List<CardData> voidPile = new List<CardData>();

    private readonly Random rng;

    public IReadOnlyList<CardData> DrawPile => drawPile;
    public IReadOnlyList<CardData> Hand => hand;
    public IReadOnlyList<CardData> VoidPile => voidPile;

    public Deck(IEnumerable<CardData> cards, Random rng = null)
    {
        this.rng = rng ?? new Random();

        drawPile.AddRange(cards);

        if (drawPile.Count < MinDeckSize)
            throw new ArgumentException($"덱은 최소 {MinDeckSize}장이어야 합니다. 현재: {drawPile.Count}장");

        Shuffle();
    }

    /// <summary>
    /// Fisher-Yates 셔플
    /// </summary>
    public void Shuffle()
    {
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = drawPile[i];
            drawPile[i] = drawPile[j];
            drawPile[j] = temp;
        }
    }

    /// <summary>
    /// 드로우 페이즈: 손패가 HandSize(5)장이 될 때까지 drawPile에서 카드를 뽑는다.
    /// drawPile이 부족하면 가능한 만큼만 뽑는다.
    /// </summary>
    /// <returns>이번 페이즈에서 실제로 뽑은 카드 수</returns>
    public int DrawPhase()
    {
        int needed = Math.Max(0, HandSize - hand.Count);
        // 최대 손패 제한
        needed = Math.Min(needed, MaxHandSize - hand.Count);
        int actual = Math.Min(needed, drawPile.Count);

        for (int i = 0; i < actual; i++)
        {
            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
        }

        return actual;
    }

    /// <summary>
    /// 멀리건: 선택된 인덱스의 카드를 교체한다. 블랙리스트 로직 적용.
    /// 새 카드를 먼저 뽑고, 반환 카드를 drawPile에 넣은 뒤 셔플한다.
    /// </summary>
    /// <param name="indices">교체할 손패 인덱스 목록 (내림차순 정렬하여 제거)</param>
    /// <returns>성공 여부</returns>
    public bool Mulligan(List<int> indices)
    {
        if (indices == null || indices.Count == 0)
            return true; // 교체 없이 패스

        // 인덱스 유효성 검증
        foreach (int idx in indices)
        {
            if (idx < 0 || idx >= hand.Count)
                return false;
        }

        // 교체할 카드를 임시로 제거 (내림차순 정렬하여 인덱스 밀림 방지)
        // 중복 인덱스 제거 후 내림차순 정렬
        var uniqueSet = new HashSet<int>(indices);
        var sortedIndices = new List<int>(uniqueSet);
        sortedIndices.Sort();
        sortedIndices.Reverse();

        var removedCards = new List<CardData>();
        foreach (int idx in sortedIndices)
        {
            removedCards.Add(hand[idx]);
            hand.RemoveAt(idx);
        }

        // 블랙리스트 로직: 먼저 새 카드를 뽑는다
        int drawCount = Math.Min(removedCards.Count, drawPile.Count);
        for (int i = 0; i < drawCount; i++)
        {
            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
        }

        // 반환된 카드를 drawPile에 넣고 셔플
        drawPile.AddRange(removedCards);
        Shuffle();

        return true;
    }

    /// <summary>
    /// 카드 사용 후 소멸(Void)로 이동.
    /// </summary>
    public bool UseCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count)
            return false;

        var card = hand[handIndex];
        hand.RemoveAt(handIndex);
        voidPile.Add(card);
        return true;
    }

    /// <summary>핸드 index 위치의 카드를 해체(Void)하고 반환한다.</summary>
    public CardData DismantleAt(int handIndex)
    {
        if (handIndex < 0 || handIndex >= hand.Count) return null;
        var card = hand[handIndex];
        hand.RemoveAt(handIndex);
        voidPile.Add(card);
        return card;
    }

    /// <summary>핸드에서 무작위 1장을 해체하고 반환한다.</summary>
    public CardData DismantleRandom()
    {
        if (hand.Count == 0) return null;
        int idx = rng.Next(hand.Count);
        return DismantleAt(idx);
    }

    /// <summary>카드를 핸드에 추가한다 (MaxHandSize 초과 시 무시).</summary>
    public void AddToHand(CardData card)
    {
        if (hand.Count < MaxHandSize)
            hand.Add(card);
    }

    /// <summary>카드를 드로우파일에 추가한다.</summary>
    public void AddToDrawPile(CardData card)
    {
        drawPile.Add(card);
    }

    /// <summary>지정 장수만큼 드로우한다 (MaxHandSize 제한 적용).</summary>
    public int DrawCards(int count)
    {
        int needed = Math.Min(count, MaxHandSize - hand.Count);
        int actual = Math.Min(needed, drawPile.Count);
        for (int i = 0; i < actual; i++)
        {
            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
        }
        return actual;
    }
}
