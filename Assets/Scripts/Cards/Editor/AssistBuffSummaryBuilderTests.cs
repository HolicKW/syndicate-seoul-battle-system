using System.Collections.Generic;
using NUnit.Framework;

public class AssistBuffSummaryBuilderTests
{
    [Test]
    public void BuildGroup_Survival_SummarizesEffectsAndTopSources()
    {
        var effects = new List<BattleSupportEffect>
        {
            new BattleSupportEffect
            {
                effectType = BattleSupportEffectType.MaxHpBonus,
                sourceBuildingName = "재생 코어",
                value = 40
            },
            new BattleSupportEffect
            {
                effectType = BattleSupportEffectType.MaxHpBonus,
                sourceBuildingName = "재생 코어",
                value = 40
            },
            new BattleSupportEffect
            {
                effectType = BattleSupportEffectType.FirstTurnShield,
                sourceBuildingName = "실드 관제소 IV",
                value = 45
            },
            new BattleSupportEffect
            {
                effectType = BattleSupportEffectType.InsertRansomware,
                sourceBuildingName = "해킹 센터 IV",
                value = 6
            }
        };

        AssistBuffGroupSummary summary = AssistBuffSummaryBuilder.BuildGroup(AssistBuffGroup.Survival, effects);
        string tooltip = summary.BuildTooltip();

        Assert.IsTrue(summary.HasActiveEffects);
        Assert.AreEqual(3, summary.ActiveSourceCount);
        StringAssert.Contains("최대 체력 +80", tooltip);
        StringAssert.Contains("시작 실드 +45", tooltip);
        StringAssert.Contains("지원 건물 3개 활성", tooltip);
        StringAssert.Contains("재생 코어 x2: +80", tooltip);
        Assert.IsFalse(tooltip.Contains("해킹 센터 IV"));
    }

    [Test]
    public void BuildGroup_Resource_ReturnsInactiveTooltipWhenNoEffectsExist()
    {
        AssistBuffGroupSummary summary = AssistBuffSummaryBuilder.BuildGroup(
            AssistBuffGroup.Resource,
            new List<BattleSupportEffect>());

        Assert.IsFalse(summary.HasActiveEffects);
        Assert.AreEqual("현재 활성화된 에너지/드로우 지원이 없습니다.", summary.BuildTooltip());
    }
}
