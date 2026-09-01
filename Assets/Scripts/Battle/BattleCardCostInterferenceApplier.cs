using System.Collections.Generic;
using UnityEngine;

public static class BattleCardCostInterferenceApplier
{
    public static int ApplyEnemyCardCostUp(List<CardData> targetDeck, IReadOnlyList<BattleSupportEffect> supportEffects)
    {
        if (targetDeck == null || targetDeck.Count == 0 || supportEffects == null || supportEffects.Count == 0)
            return 0;

        int affectedCardCount = 0;
        for (int i = 0; i < supportEffects.Count; i++)
        {
            BattleSupportEffect effect = supportEffects[i];
            if (effect == null || effect.effectType != BattleSupportEffectType.EnemyCardCostUp)
                continue;

            affectedCardCount += Mathf.Max(0, effect.value);
        }

        if (affectedCardCount <= 0)
            return 0;

        List<CardData> candidates = new List<CardData>();
        for (int i = 0; i < targetDeck.Count; i++)
        {
            CardData card = targetDeck[i];
            if (card == null || card.id == BattleDeckInfectionApplier.RansomwareCardId)
                continue;

            candidates.Add(card);
        }

        int actualCount = Mathf.Min(affectedCardCount, candidates.Count);
        for (int i = 0; i < actualCount; i++)
        {
            int index = Random.Range(i, candidates.Count);
            CardData selected = candidates[index];
            candidates[index] = candidates[i];
            candidates[i] = selected;

            selected.cost = Mathf.Max(0, selected.cost + 1);
        }

        if (actualCount > 0)
            Debug.Log($"[BattleCardCostInterference] 적 덱 카드 {actualCount}장 비용 +1");

        return actualCount;
    }
}
