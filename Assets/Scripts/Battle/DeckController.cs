using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EntityState의 덱 영역(drawPile/hand/voidPile)에 게임 규칙을 얹는 레이어.
/// BattleEngine이 보유하며, 해체/드로우/덱 조작의 고수준 API를 제공한다.
/// </summary>
public class DeckController
{
    private readonly BattleEngine engine;

    public DeckController(BattleEngine engine)
    {
        this.engine = engine;
    }

    // -- 해체 --

    /// <summary>
    /// 핸드 인덱스의 카드를 해체한다.
    /// 추출 트리거, 에너지 보너스, 카운터 증가, 코어 트리거를 자동 처리한다.
    /// </summary>
    public void DismantleFromHand(EntityState entity, int handIndex)
    {
        if (handIndex < 0 || handIndex >= entity.hand.Count) return;
        var card = entity.hand[handIndex];
        entity.hand.RemoveAt(handIndex);
        engine.DismantleCard(entity, card, DismantleVfxSource.Hand);
    }

    /// <summary>
    /// 특정 카드 인스턴스를 핸드에서 해체한다.
    /// </summary>
    public void DismantleCard(EntityState entity, CardData card)
    {
        entity.hand.Remove(card);
        engine.DismantleCard(entity, card, DismantleVfxSource.Hand);
    }

    /// <summary>
    /// 드로우파일 상단에서 count장 해체한다.
    /// </summary>
    public void DismantleFromDeck(EntityState entity, int count)
    {
        int actual = Mathf.Min(count, entity.drawPile.Count);
        for (int i = 0; i < actual; i++)
        {
            var card = entity.drawPile[0];
            entity.drawPile.RemoveAt(0);
            engine.DismantleCard(entity, card, DismantleVfxSource.Deck);
        }
    }

    // -- 핸드 조작 --

    /// <summary>
    /// 카드를 핸드에 추가한다 (최대 10장 제한).
    /// </summary>
    public bool AddToHand(EntityState entity, CardData card)
    {
        if (entity.hand.Count >= 10) return false;
        entity.hand.Add(card);
        return true;
    }

    // -- 드로우파일 조작 --

    /// <summary>
    /// 카드를 드로우파일 상단에 추가한다.
    /// </summary>
    public void AddToTopOfDeck(EntityState entity, CardData card)
    {
        entity.drawPile.Insert(0, card);
    }

    /// <summary>
    /// 카드를 드로우파일 하단에 추가한다.
    /// </summary>
    public void AddToBottomOfDeck(EntityState entity, CardData card)
    {
        entity.drawPile.Add(card);
    }

    /// <summary>
    /// 드로우파일을 셔플한다.
    /// </summary>
    public void ShuffleDeck(EntityState entity)
    {
        engine.ShuffleDeck(entity.drawPile);
    }

    // -- 드로우 --

    /// <summary>
    /// count장 드로우한다.
    /// </summary>
    public int DrawCards(EntityState entity, int count)
    {
        return engine.DrawCards(entity, count);
    }

    // -- Void 조작 --

    /// <summary>
    /// Void에서 인덱스 위치의 카드를 핸드로 복귀한다.
    /// </summary>
    public bool RecallFromVoid(EntityState entity, int voidIndex)
    {
        if (voidIndex < 0 || voidIndex >= entity.voidPile.Count) return false;
        if (entity.hand.Count >= 10) return false;

        var card = entity.voidPile[voidIndex];
        entity.voidPile.RemoveAt(voidIndex);
        entity.hand.Add(card);
        return true;
    }
}
