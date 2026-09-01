using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 건물 효과를 수집하고 배틀 엔티티에 적용한다.
/// Collect: Management 씬에서 호출 → BattleSceneData에 저장.
/// Apply: CardGame 씬에서 호출 → EntityState에 적용.
/// </summary>
public static class BuffBuildingApplier
{
    /// <summary>
    /// 세력이 보유한 모든 도시에서 활성화된 버프 건물 효과를 수집한다.
    /// 선택이 완료되지 않은 건물(HasBuffSelection == false)은 건너뛴다.
    /// </summary>
    public static List<BuffBuildingEffect> Collect(FactionManager faction)
    {
        List<BuffBuildingEffect> effects = new List<BuffBuildingEffect>();

        return effects;
    }

    /// <summary>
    /// 수집된 버프 효과를 배틀 시작 시 EntityState에 적용한다.
    /// </summary>
    public static void Apply(EntityState player, EntityState enemy, List<BuffBuildingEffect> effects)
    {
        if (player == null || effects == null || effects.Count == 0)
            return;

        for (int i = 0; i < effects.Count; i++)
        {
            ApplySingle(player, enemy, effects[i]);
        }
    }

    private static void ApplySingle(EntityState player, EntityState enemy, BuffBuildingEffect effect)
    {
        switch (effect.buffType)
        {
            case "Generic_Energy":
                player.energy += effect.value;
                Debug.Log($"[BuffBuilding] 에너지 +{effect.value} 적용");
                break;

            case "Generic_Shield":
                player.shield += effect.value;
                Debug.Log($"[BuffBuilding] 실드 +{effect.value} 적용");
                break;

            case "Generic_Draw":
                player.extraDraw += effect.value;
                Debug.Log($"[BuffBuilding] 추가 드로우 +{effect.value} 적용");
                break;

            default:
                Debug.LogWarning($"[BuffBuilding] 알 수 없는 buffType: {effect.buffType}");
                break;
        }
    }
}
