using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 보유한 카드 목록(덱 구성)을 관리한다.
/// CardDatabase(도감)와 분리하여, 어떤 카드를 몇 장 가지고 있는지만 추적한다.
/// </summary>
public class PlayerDeck : MonoBehaviour
{
    /// <summary>
    /// 덱에 포함된 카드 한 항목 (ID + 장수)
    /// </summary>
    [Serializable]
    public class DeckEntry
    {
        public string cardId;
        public int count;

        public DeckEntry(string cardId, int count)
        {
            this.cardId = cardId;
            this.count = count;
        }
    }

    [SerializeField] private List<DeckEntry> entries = new List<DeckEntry>();

    /// <summary>
    /// 현재 덱 구성 목록 (읽기 전용)
    /// </summary>
    public IReadOnlyList<DeckEntry> Entries => entries;

    /// <summary>
    /// 덱에 카드 추가 (이미 있으면 장수 증가)
    /// </summary>
    public void AddCard(string cardId, int amount = 1)
    {
        var existing = entries.Find(e => e.cardId == cardId);
        if (existing != null)
        {
            existing.count += amount;
        }
        else
        {
            entries.Add(new DeckEntry(cardId, amount));
        }
    }

    /// <summary>
    /// 덱에서 카드 제거 (장수 감소, 0이 되면 항목 제거)
    /// </summary>
    public bool RemoveCard(string cardId, int amount = 1)
    {
        var existing = entries.Find(e => e.cardId == cardId);
        if (existing == null) return false;

        existing.count -= amount;
        if (existing.count <= 0)
            entries.Remove(existing);

        return true;
    }

    /// <summary>
    /// 덱 구성을 CardData 리스트로 변환 (전투 시작 시 사용).
    /// CardDatabase에서 ID로 실제 데이터를 조회하여 count만큼 추가한다.
    /// </summary>
    public List<CardData> BuildDeckCards()
    {
        var result = new List<CardData>();

        foreach (var entry in entries)
        {
            var cardData = CardDatabase.Instance.GetById(entry.cardId);
            if (cardData == null)
            {
                Debug.LogWarning($"[PlayerDeck] ID '{entry.cardId}'에 해당하는 카드를 찾을 수 없습니다.");
                continue;
            }

            for (int i = 0; i < entry.count; i++)
                result.Add(cardData.Clone());
        }

        return result;
    }

    /// <summary>
    /// 덱의 총 카드 수
    /// </summary>
    public int TotalCardCount()
    {
        int total = 0;
        foreach (var entry in entries)
            total += entry.count;
        return total;
    }
}
