using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public static class KeywordTooltipBuilder
{
    private const string TemporaryKeyword = "일시적";
    private static readonly Dictionary<string, string> KeywordLabels = new Dictionary<string, string>
    {
        { "overclock", "오버클럭" },
        { "overload", "과부하" },
        { "network", "네트워크" },
        { "protocol", "프로토콜" },
        { "dismantle", "해체" },
        { "extract", "추출" },
        { "rebuild", "재구축" },
        { "accumulation", "축적" },
        { "gamble", "도박" },
        { "virus", "바이러스" },
        { "ransomware", "랜섬웨어" },
        { "corrosion", "부식" },
        { "overdrive", "폭주" },
        { "overflow", "오버플로우" },
        { "strength", "힘" },
        { "weakness", "약화" },
        { "반격", "반격" },
        { TemporaryKeyword, TemporaryKeyword },
    };

    public static string BuildSection(CardData card)
    {
        if (card?.keywords == null || card.keywords.Count == 0)
            return string.Empty;

        var lines = new List<string>();
        foreach (string keyword in card.keywords)
        {
            string line = BuildLine(card, keyword);
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }

        if (lines.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("키워드");
        foreach (string line in lines)
            sb.AppendLine($"- {line}");

        return sb.ToString().TrimEnd();
    }

    public static string BuildLine(CardData card, string keyword)
    {
        return keyword switch
        {
            "overclock" => "오버클럭: 사용할 때 현재 오버클럭 스택에 따라 일부 효과가 강화됩니다. 사용 후 오버클럭 스택이 1 증가합니다.",
            "overload" => $"과부하({FormatNumber(GetOverloadValue(card))}): 오버클럭 스택을 {FormatNumber(GetOverloadValue(card))}만큼 보유해야만 사용할 수 있습니다.",
            "network" => "네트워크: 사용할 때 네트워크 스택이 1 증가합니다. 많은 네트워크 카드는 네트워크 스택에 비례해 강해집니다.",
            "protocol" => "프로토콜: 이전에 사용한 카드 조건을 만족하면 추가 효과가 발동합니다.",
            "dismantle" => "해체: 카드를 버리고, 추출 및 해체 관련 효과를 발동합니다.",
            "extract" => "추출: 이 카드가 해체될 때 별도의 추출 효과가 발동합니다.",
            "rebuild" => $"재구축({FormatNumber(card?.rebuildCount ?? 0)}): 사용 후 바로 소모되지 않고 손패로 돌아와 {FormatNumber(card?.rebuildCount ?? 0)}번 더 사용할 수 있습니다.",
            "accumulation" => $"축적({FormatNumber(card?.accumulationTarget ?? 0)}): 이 카드가 손패에 있을 때 다른 카드를 {FormatNumber(card?.accumulationTarget ?? 0)}회 이상 사용하면 강화됩니다.",
            "gamble" => $"도박({FormatPercent(GetGambleChance(card))}): {FormatPercent(GetGambleChance(card))} 확률로 행운 효과가, 실패 시 불운 효과가 발동합니다.",
            "virus" => BuildStackKeywordLine("바이러스", GetEffectValues(card, "virus"), "대상에게 쌓이는 감염 스택입니다. 일부 카드는 바이러스를 소모하거나 바이러스 수치에 비례해 강해집니다."),
            "ransomware" => "랜섬웨어: 턴 시작 시 손패에 있으면 피해를 받습니다. 발동할 때마다 다음 피해량이 5 증가하며, 1코스트로 사용하면 제거됩니다.",
            "corrosion" => BuildStackKeywordLine("부식", GetEffectValues(card, "corrosion"), "대상의 턴 시작 시 실드를 깎고, 발동 후 1 감소합니다."),
            "overdrive" => BuildOverdriveLine(card),
            "overflow" => "오버플로우: 다음 턴 시작 시 이 수치만큼 에너지가 감소하고, 발동 후 0으로 초기화됩니다.",
            "strength" => BuildStackKeywordLine("힘", GetEffectValues(card, "strength"), "내가 주는 공격 피해가 이 수치만큼 증가합니다."),
            "weakness" => BuildStackKeywordLine("약화", GetEffectValues(card, "weakness"), "대상이 주는 공격 피해가 이 수치만큼 감소하고, 턴 종료 시 1씩 감소합니다."),
            "반격" => BuildStackKeywordLine("반격", GetEffectValues(card, "retaliateOnHit"), "다음에 피해를 받으면 공격자에게 힘과 약화가 적용되는 반격 피해를 줍니다."),
            TemporaryKeyword => "일시적: 생성형 카드입니다. 승리 시 덱이나 손패에 남아 있어도 운영턴의 카드 인벤토리로 돌아오지 않습니다.",
            _ => string.Empty,
        };
    }

    public static string BuildLinkedDescription(CardData card, string description)
    {
        if (string.IsNullOrEmpty(description))
            return description ?? string.Empty;

        string result = description;
        foreach (var pair in KeywordLabels)
        {
            string keyword = pair.Key;
            string label = pair.Value;
            if (string.IsNullOrEmpty(BuildLine(card, keyword)))
                continue;

            result = Regex.Replace(
                result,
                Regex.Escape(label),
                match => $"<link=\"{keyword}\"><b>{match.Value}</b></link>");
        }

        return result;
    }

    private static float GetOverloadValue(CardData card)
    {
        return card != null && card.overclockThreshold > 0 ? card.overclockThreshold : 0f;
    }

    private static float GetGambleChance(CardData card)
    {
        CardEffect effect = FindEffect(card?.effects, IsGambleEffect);
        float chance = effect != null && effect.chance > 0f ? effect.chance : 0.5f;
        if (chance > 1f) chance /= 100f;

        // 전투 중에는 행운·보너스 확률을 반영한 실제 판정 확률을 표시한다.
        // (ResolveGamble과 동일한 공식: base + luck*1% + gambleBonusChance*1%)
        var player = BattleEngine.Instance?.Player;
        if (player != null)
            chance += player.luck * 0.01f + player.Turn.gambleBonusChance * 0.01f;

        return Math.Max(0f, Math.Min(1f, chance));
    }

    private static string GetOverdriveValue(CardData card)
    {
        if (card == null)
            return "전부";

        CardEffect effect = FindEffect(card.effects, e => e.type == "consumeVirus" || e.type == "meltToxin");
        if (effect == null)
            return card.virusThreshold > 0 ? FormatNumber(card.virusThreshold) : "전부";
        if (effect.countAll || effect.mode == "all")
            return "전부";
        if (effect.threshold > 0)
            return FormatNumber(effect.threshold);
        if (effect.value > 0)
            return FormatNumber(effect.value);
        if (card.virusThreshold > 0)
            return FormatNumber(card.virusThreshold);
        return "전부";
    }

    private static string BuildOverdriveLine(CardData card)
    {
        string value = GetOverdriveValue(card);
        return $"폭주({value})";
    }

    private static List<float> GetEffectValues(CardData card, string effectType)
    {
        var values = new List<float>();
        CollectEffectValues(card?.effects, effectType, values);
        CollectEffectValues(card?.extractEffects, effectType, values);
        CollectEffectValues(card?.protocolEffects, effectType, values);
        CollectEffectValues(card?.accumulationEffects, effectType, values);
        if (card?.coreEffect?.effects != null)
            CollectEffectValues(card.coreEffect.effects, effectType, values);
        return values;
    }

    private static void CollectEffectValues(List<CardEffect> effects, string effectType, List<float> values)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect == null) continue;

            if (MatchesEffectType(effect, effectType))
            {
                if (effect.value > 0)
                    values.Add(effect.value);
            }

            CollectEffectValues(effect.effects, effectType, values);
            CollectEffectValues(effect.thenEffects, effectType, values);
            CollectEffectValues(effect.elseEffects, effectType, values);
        }
    }

    private static bool MatchesEffectType(CardEffect effect, string effectType)
    {
        if (effect.type == effectType)
            return true;

        if (effectType == "virus")
            return effect.type == "modifyStat" && effect.stat == "virus";
        if (effectType == "corrosion")
            return effect.type == "modifyStat" && effect.stat == "corrosion";

        return false;
    }

    private static CardEffect FindEffect(List<CardEffect> effects, Predicate<CardEffect> predicate)
    {
        if (effects == null) return null;

        foreach (var effect in effects)
        {
            if (effect == null) continue;
            if (predicate(effect)) return effect;

            var nested = FindEffect(effect.effects, predicate)
                ?? FindEffect(effect.thenEffects, predicate)
                ?? FindEffect(effect.elseEffects, predicate);
            if (nested != null) return nested;
        }

        return null;
    }

    private static bool IsGambleEffect(CardEffect effect)
    {
        return effect.type == "gamble"
            || effect.type == "russianRoulette"
            || effect.type == "fullHouseHit"
            || effect.type == "coinFlipHeads";
    }

    private static string FormatPercent(float chance)
    {
        return FormatNumber(chance * 100f) + "%";
    }

    private static string FormatValues(List<float> values)
    {
        if (values == null || values.Count == 0)
            return "N";

        var unique = new List<string>();
        foreach (float value in values)
        {
            string formatted = FormatNumber(value);
            if (!unique.Contains(formatted))
                unique.Add(formatted);
        }

        return string.Join("/", unique);
    }

    private static string BuildStackKeywordLine(string label, List<float> values, string description)
    {
        string title = values == null || values.Count == 0
            ? label
            : $"{label}({FormatValues(values)})";

        return $"{title}: {description}";
    }

    private static string FormatNumber(float value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.001f)
            return Math.Round(value).ToString(CultureInfo.InvariantCulture);

        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
