using System.Collections.Generic;
using UnityEngine;

public static class BattleDeckInfectionApplier
{
    public const string RansomwareCardId = "STATUS_RANSOMWARE";

    public static int InsertRansomwareIntoDeck(List<CardData> targetDeck, IReadOnlyList<BattleSupportEffect> supportEffects)
    {
        if (targetDeck == null || supportEffects == null || supportEffects.Count == 0)
            return 0;

        int totalCount = 0;
        for (int i = 0; i < supportEffects.Count; i++)
        {
            BattleSupportEffect effect = supportEffects[i];
            if (effect == null)
                continue;

            if (effect.effectType != BattleSupportEffectType.InsertRansomware)
                continue;

            totalCount += Mathf.Max(0, effect.value);
        }

        if (totalCount <= 0)
            return 0;

        CardData ransomware = CardDatabase.Instance.GetById(RansomwareCardId);
        if (ransomware == null)
        {
            Debug.LogWarning($"[BattleDeckInfection] Card '{RansomwareCardId}' not found.");
            return 0;
        }

        for (int i = 0; i < totalCount; i++)
            targetDeck.Add(ransomware.Clone());

        BattleUtils.Shuffle(targetDeck);
        Debug.Log($"[BattleDeckInfection] {RansomwareCardId} {totalCount}장 주입");
        return totalCount;
    }
}
