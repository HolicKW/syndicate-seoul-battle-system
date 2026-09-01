using System.Collections.Generic;

/// <summary>
/// 전투 시스템 공통 유틸리티.
/// MonoBehaviour 의존 없는 순수 정적 메서드 모음.
/// </summary>
public static class BattleUtils
{
    /// <summary>
    /// Fisher-Yates 셔플 (in-place). BattleEngine, EffectInterpreter, TempDeckFactory 공통 사용.
    /// </summary>
    public static void Shuffle(List<CardData> deck)
    {
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }
}
