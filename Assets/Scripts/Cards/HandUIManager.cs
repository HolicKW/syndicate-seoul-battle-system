using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 손패 카드 UI 인스턴스 생성/제거 및 레이아웃을 관리한다.
/// 멀리건/전투 페이즈에 따라 MulliganUI 또는 HandFanLayout에 배치를 위임한다.
/// </summary>
public class HandUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform handArea;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private float cardScale = 1.5f;

    private HandFanLayout fanLayout;
    private HandHoverManager hoverManager;
    private MulliganUI mulliganUI;

    private readonly List<HandCardUI> handCardUIs = new List<HandCardUI>();

    /// <summary>
    /// 현재 손패에 표시된 카드 UI 목록 (읽기 전용).
    /// </summary>
    public IReadOnlyList<HandCardUI> Cards => handCardUIs;

    /// <summary>
    /// 초기화: handArea에 FanLayout/HoverManager 세팅.
    /// </summary>
    public void Init(Transform handAreaRef, GameObject cardPrefabRef, float scale, MulliganUI mulliganUIRef)
    {
        handArea = handAreaRef;
        cardPrefab = cardPrefabRef;
        cardScale = scale;
        mulliganUI = mulliganUIRef;

        // 기존 HorizontalLayoutGroup 제거
        var hlg = handArea.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg != null)
            Destroy(hlg);

        fanLayout = handArea.GetComponent<HandFanLayout>();
        if (fanLayout == null)
            fanLayout = handArea.gameObject.AddComponent<HandFanLayout>();

        // BattleManagerRoot에서 HoverManager 참조, 없으면 handArea에 생성
        if (BattleManagerRoot.Instance != null)
            hoverManager = BattleManagerRoot.Instance.HoverManager;
        if (hoverManager == null)
            hoverManager = handArea.GetComponent<HandHoverManager>();
        if (hoverManager == null)
            hoverManager = handArea.gameObject.AddComponent<HandHoverManager>();

        hoverManager.SetHandArea(handArea.GetComponent<RectTransform>());
    }

    /// <summary>
    /// 손패 UI를 갱신한다: 기존 카드 제거 → 새 카드 생성 → 레이아웃 배치.
    /// </summary>
    /// <param name="hand">현재 손패 카드 목록</param>
    /// <param name="isMulliganPhase">멀리건 페이즈 여부</param>
    /// <param name="onCardUsed">카드 사용 시 콜백</param>
    public void RefreshHandUI(IReadOnlyList<CardData> hand, bool isMulliganPhase, Action<HandCardUI> onCardUsed, EntityState caster = null)
    {
        ClearCards();

        if (hand == null) return;

        // 카드 UI 생성
        for (int i = 0; i < hand.Count; i++)
        {
            var cardGO = Instantiate(cardPrefab, handArea);

            var cardUI = cardGO.GetComponent<HandCardUI>();
            if (cardUI == null)
                cardUI = cardGO.AddComponent<HandCardUI>();

            cardUI.Setup(hand[i], i, caster);
            cardUI.SetClickCallback(onCardUsed);
            cardUI.SetupMulligan(isMulliganPhase);

            cardGO.transform.localScale = Vector3.one * cardScale;
            cardGO.SetActive(true);
            handCardUIs.Add(cardUI);
        }

        // 레이아웃 배치
        if (isMulliganPhase)
        {
            mulliganUI.Show(handCardUIs, cardScale, handArea);
            SaveOriginalTransforms();
            RegisterHoverCards();
        }
        else
        {
            ArrangeBattleCards();
        }

        Debug.Log($"[HandUIManager] HandArea에 {hand.Count}장 카드 표시");
    }

    public void RefreshHandUIKeepingExisting(
        IReadOnlyList<CardData> hand,
        IReadOnlyList<CardData> newlyDrawnCards,
        Action<HandCardUI> onCardUsed,
        EntityState caster = null)
    {
        if (hand == null)
        {
            ClearCards();
            return;
        }

        var previousCards = new List<HandCardUI>(handCardUIs);
        var nextCards = new List<HandCardUI>(hand.Count);

        for (int i = 0; i < hand.Count; i++)
        {
            bool isNewlyDrawn = ContainsCardReference(newlyDrawnCards, hand[i]);
            HandCardUI cardUI = isNewlyDrawn ? null : TakeMatchingCardUI(previousCards, hand[i]);
            if (cardUI == null)
                cardUI = CreateCardUI(hand[i], i, onCardUsed, caster);
            else
                ConfigureExistingCardUI(cardUI, hand[i], i, onCardUsed, caster);

            if (isNewlyDrawn)
            {
                var group = cardUI.GetComponent<CanvasGroup>();
                if (group == null)
                    group = cardUI.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            nextCards.Add(cardUI);
        }

        foreach (var staleCard in previousCards)
        {
            if (staleCard != null)
            {
                staleCard.gameObject.SetActive(false);
                Destroy(staleCard.gameObject);
            }
        }

        handCardUIs.Clear();
        handCardUIs.AddRange(nextCards);
        ArrangeBattleCards();

        int drawnCount = newlyDrawnCards != null ? newlyDrawnCards.Count : 0;
        Debug.Log($"[HandUIManager] HandArea에 {hand.Count}장 카드 표시 (신규 {drawnCount}장)");
    }

    private HandCardUI CreateCardUI(CardData card, int index, Action<HandCardUI> onCardUsed, EntityState caster)
    {
        var cardGO = Instantiate(cardPrefab, handArea);

        var cardUI = cardGO.GetComponent<HandCardUI>();
        if (cardUI == null)
            cardUI = cardGO.AddComponent<HandCardUI>();

        ConfigureExistingCardUI(cardUI, card, index, onCardUsed, caster);
        cardGO.SetActive(true);
        return cardUI;
    }

    private void ConfigureExistingCardUI(HandCardUI cardUI, CardData card, int index, Action<HandCardUI> onCardUsed, EntityState caster)
    {
        if (cardUI == null)
            return;

        cardUI.transform.SetParent(handArea, false);
        cardUI.Setup(card, index, caster);
        cardUI.SetClickCallback(onCardUsed);
        cardUI.SetupMulligan(false);
        cardUI.gameObject.SetActive(true);
        cardUI.transform.localScale = Vector3.one * cardScale;
    }

    private static HandCardUI TakeMatchingCardUI(List<HandCardUI> candidates, CardData card)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            var cardUI = candidates[i];
            if (cardUI == null)
                continue;

            if (ReferenceEquals(cardUI.CardData, card))
            {
                candidates.RemoveAt(i);
                return cardUI;
            }
        }

        return null;
    }

    private static bool ContainsCardReference(IReadOnlyList<CardData> cards, CardData target)
    {
        if (cards == null || target == null)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (ReferenceEquals(cards[i], target))
                return true;
        }

        return false;
    }

    private void ArrangeBattleCards()
    {
        if (mulliganUI != null)
            mulliganUI.Hide();

        foreach (var cardUI in handCardUIs)
        {
            if (cardUI == null)
                continue;

            cardUI.transform.SetParent(handArea, false);
            var rt = cardUI.RT;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one * cardScale;
        }

        if (fanLayout != null)
            fanLayout.ArrangeCards();

        SaveOriginalTransforms();
        RegisterHoverCards();
    }

    private void SaveOriginalTransforms()
    {
        foreach (var cardUI in handCardUIs)
            cardUI?.SaveOriginalTransform();
    }

    private void RegisterHoverCards()
    {
        if (hoverManager != null)
            hoverManager.SetCards(handCardUIs);
    }

    private void ClearCards()
    {
        foreach (var cardUI in handCardUIs)
        {
            if (cardUI != null)
            {
                cardUI.gameObject.SetActive(false);
                Destroy(cardUI.gameObject);
            }
        }
        handCardUIs.Clear();
    }
}
