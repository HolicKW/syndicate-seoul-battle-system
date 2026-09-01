using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 시작 시 플레이어 덱을 공급하는 전략 인터페이스.
/// 새 덱 소스(랜덤 덱, 도전 모드 등)를 추가할 때 이 인터페이스를 구현하고
/// DeckProviderResolver의 체인에 추가하기만 하면 된다.
/// </summary>
public interface IDeckProvider
{
    /// <summary>이 공급자가 현재 상황에서 덱을 제공할 수 있는지 반환한다.</summary>
    bool CanProvide();

    /// <summary>덱 카드 목록을 빌드해 반환한다.</summary>
    List<CardData> Build();
}

// -----------------------------------------------------------------------------
//  구현체
// -----------------------------------------------------------------------------

/// <summary>
/// Management 씬에서 전달된 BattleSceneData를 사용하는 공급자.
/// MinDeckSize 검증 포함.
/// </summary>
public class BattleSceneDataDeckProvider : IDeckProvider
{
    public bool CanProvide() => BattleSceneData.PlayerDeckIds != null;

    public List<CardData> Build()
    {
        var ids = BattleSceneData.PlayerDeckIds;
        if (ids.Count >= Deck.MinDeckSize)
        {
            var deck = StarterDeckFactory.BuildFromIds(ids);
            Debug.Log($"[DeckProvider] 사용자 덱 연동 성공: {deck.Count}장 (최소 {Deck.MinDeckSize}장 충족)");
            return deck;
        }

        Debug.LogWarning($"[DeckProvider] 사용자 덱 카드 수가 부족합니다. ({ids.Count}장 / 최소 {Deck.MinDeckSize}장). 기본 스타터 덱으로 전환합니다.");
        return StarterDeckFactory.BuildDefault();
    }
}

/// <summary>
/// Inspector의 PlayerDeck 참조를 사용하는 공급자.
/// </summary>
public class PlayerDeckProvider : IDeckProvider
{
    private readonly PlayerDeck playerDeck;

    public PlayerDeckProvider(PlayerDeck playerDeck)
    {
        this.playerDeck = playerDeck;
    }

    public bool CanProvide() => playerDeck != null && playerDeck.TotalCardCount() > 0;

    public List<CardData> Build()
    {
        var deck = playerDeck.BuildDeckCards();
        Debug.Log($"[DeckProvider] 인스펙터/기본 덱 사용: {deck.Count}장");
        return deck;
    }
}

/// <summary>
/// 항상 사용 가능한 기본 스타터 덱 공급자. 체인의 마지막 폴백.
/// </summary>
public class DefaultDeckProvider : IDeckProvider
{
    public bool CanProvide() => true;

    public List<CardData> Build()
    {
        Debug.LogWarning("[DeckProvider] 사용자 덱 정보가 없습니다(Null). 기본 스타터 덱을 사용합니다.");
        return StarterDeckFactory.BuildDefault();
    }
}

// -----------------------------------------------------------------------------
//  리졸버
// -----------------------------------------------------------------------------

/// <summary>
/// 우선순위 체인에서 첫 번째로 CanProvide()가 참인 공급자를 선택한다.
/// 우선순위: BattleSceneData → PlayerDeck → Default
/// </summary>
public static class DeckProviderResolver
{
    public static IDeckProvider Resolve(PlayerDeck playerDeck)
    {
        IDeckProvider[] chain =
        {
            new BattleSceneDataDeckProvider(),
            new PlayerDeckProvider(playerDeck),
            new DefaultDeckProvider(),
        };

        foreach (var provider in chain)
            if (provider.CanProvide()) return provider;

        return new DefaultDeckProvider(); // 도달 불가 (DefaultDeckProvider는 항상 true)
    }
}
