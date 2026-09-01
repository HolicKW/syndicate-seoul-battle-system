using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 덱 카운트 UI 갱신을 담당하는 매니저.
/// BattleEngine 도입 후 덱 조작은 BattleEngine이 전담하며,
/// DeckManager는 EntityState를 읽어 UI를 동기화하는 역할만 한다.
/// </summary>
public class DeckManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text deckCountText;
    [SerializeField] private DeckDroneCounterUI deckCounterUI;

    [Header("Inspector Debug (View Only)")]
    [SerializeField] private List<string> debugDrawPile = new List<string>();
    [SerializeField] private List<string> debugHand = new List<string>();
    [SerializeField] private List<string> debugVoidPile = new List<string>();

    /// <summary>
    /// BattleEngine의 EntityState에서 덱 카운트를 읽어 UI를 갱신한다.
    /// BattleEngine 도입 후 덱 조작은 BattleEngine이 담당하므로,
    /// DeckManager는 UI 갱신 전용으로 축소되었다.
    /// </summary>
    public void SyncFromEngine(EntityState playerState)
    {
        if (playerState == null) return;

        SetDeckCount(playerState.drawPile.Count);

#if UNITY_EDITOR
        SyncDebugListFromState(debugDrawPile, playerState.drawPile);
        SyncDebugListFromState(debugHand, playerState.hand);
        SyncDebugListFromState(debugVoidPile, playerState.voidPile);
#endif
    }

#if UNITY_EDITOR
    private void SyncDebugListFromState(List<string> debugList, System.Collections.Generic.List<CardData> pile)
    {
        debugList.Clear();
        foreach (var card in pile)
            debugList.Add(card.cardName);
    }
#endif

    void Start()
    {
        ResolveDeckCounterUI();

        if (deckCountText == null && deckCounterUI == null)
            Debug.LogWarning("[DeckManager] deckCountText 또는 deckCounterUI가 Inspector에서 연결되지 않았습니다.");
    }

    private void SetDeckCount(int count)
    {
        ResolveDeckCounterUI();

        if (deckCounterUI != null)
            deckCounterUI.SetCount(count);
        else if (deckCountText != null)
            deckCountText.text = count.ToString();
    }

    private void ResolveDeckCounterUI()
    {
        if (deckCounterUI != null)
            return;

        if (deckCountText != null)
            deckCounterUI = deckCountText.GetComponentInParent<DeckDroneCounterUI>();

        if (deckCounterUI == null)
            deckCounterUI = GetComponentInChildren<DeckDroneCounterUI>(true);
        if (deckCounterUI == null)
            deckCounterUI = FindFirstObjectByType<DeckDroneCounterUI>(FindObjectsInactive.Include);
    }
}
