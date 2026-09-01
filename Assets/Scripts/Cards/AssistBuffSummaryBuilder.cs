using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum AssistBuffGroup
{
    Survival,
    Offense,
    Resource
}

public sealed class AssistBuffEffectSummary
{
    public BattleSupportEffectType EffectType { get; }
    public string Label { get; }
    public int TotalValue { get; private set; }

    public AssistBuffEffectSummary(BattleSupportEffectType effectType, string label)
    {
        EffectType = effectType;
        Label = label;
    }

    public void Add(int value)
    {
        TotalValue += Math.Max(0, value);
    }
}

public sealed class AssistBuffSourceSummary
{
    public string BuildingName { get; }
    public int Count { get; private set; }
    public int TotalValue { get; private set; }

    public AssistBuffSourceSummary(string buildingName)
    {
        BuildingName = string.IsNullOrWhiteSpace(buildingName) ? "지원 건물" : buildingName.Trim();
    }

    public void Add(int value)
    {
        Count++;
        TotalValue += Math.Max(0, value);
    }
}

public sealed class AssistBuffGroupSummary
{
    public AssistBuffGroup Group { get; }
    public string Title { get; }
    public string EmptyMessage { get; }
    public List<AssistBuffEffectSummary> Effects { get; } = new List<AssistBuffEffectSummary>();
    public List<AssistBuffSourceSummary> Sources { get; } = new List<AssistBuffSourceSummary>();

    public bool HasActiveEffects => Effects.Any(effect => effect.TotalValue > 0);
    public int ActiveSourceCount => Sources.Sum(source => source.Count);

    public AssistBuffGroupSummary(AssistBuffGroup group, string title, string emptyMessage)
    {
        Group = group;
        Title = title;
        EmptyMessage = emptyMessage;
    }

    public string BuildTooltip()
    {
        if (!HasActiveEffects)
            return EmptyMessage;

        var builder = new StringBuilder();
        for (int i = 0; i < Effects.Count; i++)
        {
            AssistBuffEffectSummary effect = Effects[i];
            if (effect.TotalValue <= 0)
                continue;

            builder.Append(effect.Label).Append(" +").Append(effect.TotalValue).AppendLine();
        }

        builder.Append("지원 건물 ").Append(ActiveSourceCount).AppendLine("개 활성");
        builder.AppendLine();

        List<AssistBuffSourceSummary> orderedSources = Sources
            .Where(source => source.TotalValue > 0)
            .OrderByDescending(source => source.TotalValue)
            .ThenBy(source => source.BuildingName, StringComparer.Ordinal)
            .ToList();

        int visibleCount = Math.Min(3, orderedSources.Count);
        for (int i = 0; i < visibleCount; i++)
        {
            AssistBuffSourceSummary source = orderedSources[i];
            builder.Append("- ")
                .Append(source.BuildingName)
                .Append(" x")
                .Append(source.Count)
                .Append(": +")
                .Append(source.TotalValue)
                .AppendLine();
        }

        if (orderedSources.Count > visibleCount)
        {
            int otherCount = 0;
            int otherTotal = 0;
            for (int i = visibleCount; i < orderedSources.Count; i++)
            {
                otherCount += orderedSources[i].Count;
                otherTotal += orderedSources[i].TotalValue;
            }

            builder.Append("- 기타 x")
                .Append(otherCount)
                .Append(": +")
                .Append(otherTotal)
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}

public static class AssistBuffSummaryBuilder
{
    public static AssistBuffGroupSummary BuildGroup(
        AssistBuffGroup group,
        IReadOnlyList<BattleSupportEffect> supportEffects)
    {
        AssistBuffGroupSummary summary = CreateEmptyGroup(group);
        if (supportEffects == null || supportEffects.Count == 0)
            return summary;

        Dictionary<string, AssistBuffEffectSummary> effectsByLabel = new Dictionary<string, AssistBuffEffectSummary>(StringComparer.Ordinal);
        Dictionary<string, AssistBuffSourceSummary> sourcesByName = new Dictionary<string, AssistBuffSourceSummary>(StringComparer.Ordinal);

        for (int i = 0; i < supportEffects.Count; i++)
        {
            BattleSupportEffect supportEffect = supportEffects[i];
            if (supportEffect == null || supportEffect.value <= 0)
                continue;

            if (!TryGetDescriptor(supportEffect.effectType, out AssistBuffGroup effectGroup, out string label))
                continue;

            if (effectGroup != group)
                continue;

            if (!effectsByLabel.TryGetValue(label, out AssistBuffEffectSummary effectSummary))
            {
                effectSummary = new AssistBuffEffectSummary(supportEffect.effectType, label);
                effectsByLabel.Add(label, effectSummary);
                summary.Effects.Add(effectSummary);
            }

            effectSummary.Add(supportEffect.value);

            string buildingName = string.IsNullOrWhiteSpace(supportEffect.sourceBuildingName)
                ? supportEffect.sourceBuildingId
                : supportEffect.sourceBuildingName;
            if (string.IsNullOrWhiteSpace(buildingName))
                buildingName = "지원 건물";

            if (!sourcesByName.TryGetValue(buildingName, out AssistBuffSourceSummary sourceSummary))
            {
                sourceSummary = new AssistBuffSourceSummary(buildingName);
                sourcesByName.Add(buildingName, sourceSummary);
                summary.Sources.Add(sourceSummary);
            }

            sourceSummary.Add(supportEffect.value);
        }

        return summary;
    }

    public static bool TryGetDescriptor(
        BattleSupportEffectType effectType,
        out AssistBuffGroup group,
        out string label)
    {
        switch (effectType)
        {
            case BattleSupportEffectType.MaxHpBonus:
                group = AssistBuffGroup.Survival;
                label = "최대 체력";
                return true;
            case BattleSupportEffectType.FirstTurnShield:
                group = AssistBuffGroup.Survival;
                label = "시작 실드";
                return true;
            case BattleSupportEffectType.EnemyCardCostUp:
                group = AssistBuffGroup.Offense;
                label = "적 카드 비용 증가 대상";
                return true;
            case BattleSupportEffectType.InsertRansomware:
                group = AssistBuffGroup.Offense;
                label = "랜섬웨어 삽입";
                return true;
            default:
                group = AssistBuffGroup.Resource;
                label = string.Empty;
                return false;
        }
    }

    private static AssistBuffGroupSummary CreateEmptyGroup(AssistBuffGroup group)
    {
        switch (group)
        {
            case AssistBuffGroup.Survival:
                return new AssistBuffGroupSummary(group, "생존 지원", "현재 활성화된 체력/방어 지원이 없습니다.");
            case AssistBuffGroup.Offense:
                return new AssistBuffGroupSummary(group, "공세 지원", "현재 활성화된 공격 지원이 없습니다.");
            default:
                return new AssistBuffGroupSummary(group, "전술 자원", "현재 활성화된 에너지/드로우 지원이 없습니다.");
        }
    }
}
