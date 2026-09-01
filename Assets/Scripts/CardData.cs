using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine.Serialization;

// -- Enums --

[JsonConverter(typeof(StringEnumConverter))]
public enum CardType { Attack, Skill, Core }

[JsonConverter(typeof(StringEnumConverter))]
public enum CardRarity { Common, Rare, Epic, Unique, Legendary }

[JsonConverter(typeof(StringEnumConverter))]
public enum CardTarget { SingleEnemy, Self, Every }

// -- 이펙트 데이터 구조 --

[Serializable]
public class ScalingData
{
    public string source;       // "networkStacks", "selfDamageThisTurn", "virus" 등
    public float multiplier;
    public int max;
    public int min;             // 0이면 하한 없음
}

[Serializable]
public class ConditionData
{
    public string type;         // "hpBelow", "overclockMin", "hasKeyword" 등
    public float value;
}

[Serializable]
public class DynamicCostData
{
    public ConditionData condition; // 조건 충족 시 cost로 대체
    public int cost;
}

[Serializable]
public class ScaledCostData
{
    public string source;           // "overclockStacks" 등 스케일링 소스
    public int reductionPerUnit;    // 소스 1단위당 코스트 감소량
    public int min;                 // 최솟값 (기본 0)
}

[Serializable]
public class CoreEffect
{
    public string trigger;      // "turnStart", "turnEnd", "cardPlayed", "dismantle", "virusConsumed", "virusApplied", "onDeath", "immediate"
    [FormerlySerializedAs("power" + "Type")]
    public string coreType;    // custom handler 식별자 (null이면 effects[] 사용)
    public float value;         // custom handler용 파라미터
    public float chance;        // 도박/확률형 core handler용 확률
    public float energyCost;    // overclockPerTurn 등: 발동 시 소모할 에너지
    public int maxBonusStack;   // absoluteCarrier 등: 보조 수치 파라미터 (바이러스 부여량 등)
    public int energy;          // DM_038: 첫 해체 시 에너지 획득량
    public int draw;            // DM_038: 첫 해체 시 드로우 수
    public int damage;          // NW_038: 프로토콜 발동 시 고정 피해량
    public int heal;            // DM_039: 재구축 시 회복량
    public int costReduction;   // DM_039: 재구축 시 카드 코스트 감소량
    public float multiplier;    // DM_040: 재구축 횟수xmultiplier 피해
    public int threshold;       // DM_029: N장째마다 트리거
    public int shield;          // BASE_033: 매 턴 시작 실드 획득량
    public List<CardEffect> effects;
}

/// <summary>
/// 카드 이펙트 - 중첩/조건/스케일링을 지원하는 트리 구조.
/// type 필드로 범용 핸들러를 선택하고, 나머지 필드는 파라미터로 사용한다.
/// </summary>
[Serializable]
public class CardEffect
{
    public string type;             // "damage", "shield", "modifyStat", "conditional", "draw" 등
    public float value;
    public int hits;                // 다단히트 (0 또는 미지정이면 1회)
    public float chance;            // 도박 확률 (0이면 미사용)
    public bool overclockScale;     // 오버클럭 스택 스케일링 적용 여부
    public string target;           // "self" | "enemy" (미지정이면 카드 기본 타겟)
    public string stat;             // modifyStat용: "strength", "weakness", "virus" 등

    // -- 범용 핸들러 공용 필드 --
    public string mode;             // modifyStat: "add"/"set"/"multiply", selfDamage: "targetShield", shieldBreak: "allAndDamage" 등
    public string source;           // modifyStat: 값을 읽어올 소스 (예: "baseEnergy"), dismantle: "hand"/"deck"
    public string action;           // scaledEffect: 실행할 행동 ("damage","shield","heal","draw","selfDamage","energy","modifyStat")

    // -- 자원 변환 (convertResource) --
    public string from;             // 변환 소스 ("shield", "energy")
    public string to;               // 변환 대상 ("energy", "heal", "draw")
    public float ratio;             // 변환 비율

    // -- 해체 (dismantle) --
    public string bonus;            // "drawEqual", "shieldScale", "energyByCost"
    public float bonusMultiplier;   // bonus 배율
    public string filter;           // "network", "attack", "skill", "core"
    public bool countAll;           // count를 소스 전체로 할 때 true
    public int drawBeforeSelect;    // chosen 모드: 선택 UI 전에 먼저 드로우할 장수
    public int minValue;            // chosen 모드: 최소 선택 장수 (0 또는 미지정 시 value와 동일 - 정확히 N장 강제)

    // -- 기타 --
    public float fraction;          // shieldBreak allAndDamage의 피해 배율
    public bool gain;               // stealShield: 훔친 실드를 얻을지 (기본 true → JSON 미지정 시 false이므로 코드에서 보정)
    public string costMode;         // searchDeck: "zeroCost" 등
    public string timing;           // energy: "nextTurn", discard: "turnEnd"
    public bool bypassShield;       // damage: 실드 무시 여부 (armorPierce 또는 명시적 관통)

    // -- 특수 이펙트 파라미터 --
    public int threshold;           // preventSelfDamageIfOverclock: 오버클럭 최소 스택
    public int recallCount;         // dismantleAllAndVoidRecall: voidPile에서 복구할 카드 수

    public ScalingData scaling;
    public ConditionData condition;

    public List<CardEffect> thenEffects;    // 조건 성공 시
    public List<CardEffect> elseEffects;    // 조건 실패 시
    public List<CardEffect> effects;        // 서브이펙트 (복합 이펙트용)

    /// <summary>
    /// 유효 히트 수 (0이면 1로 처리).
    /// </summary>
    public int EffectiveHits => hits > 0 ? hits : 1;

    public CardEffect Clone()
    {
        return new CardEffect
        {
            type = type, value = value, hits = hits, chance = chance,
            overclockScale = overclockScale, target = target, stat = stat,
            mode = mode, source = source, action = action,
            from = from, to = to, ratio = ratio,
            bonus = bonus, bonusMultiplier = bonusMultiplier, filter = filter, countAll = countAll,
            drawBeforeSelect = drawBeforeSelect, minValue = minValue,
            fraction = fraction, gain = gain, costMode = costMode, timing = timing, bypassShield = bypassShield,
            threshold = threshold, recallCount = recallCount,
            scaling = scaling == null ? null : new ScalingData { source = scaling.source, multiplier = scaling.multiplier, max = scaling.max, min = scaling.min },
            condition = condition == null ? null : new ConditionData { type = condition.type, value = condition.value },
            thenEffects = thenEffects?.ConvertAll(e => e.Clone()),
            elseEffects = elseEffects?.ConvertAll(e => e.Clone()),
            effects = effects?.ConvertAll(e => e.Clone()),
        };
    }
}

// -- 카드 데이터 --

[Serializable]
public class CardData
{
    public string id;
    public string cardName;
    public string pack;             // "base", "IP", "overclock", "dismantle", "network", "biohazard", "russian_roulette"
    public CardType type;
    public CardRarity rarity;
    public int tier;                // 1~5
    public int cost;
    public DynamicCostData dynamicCost; // 조건부 코스트 (null이면 고정 cost 사용)
    public ScaledCostData scaledCost;   // 스케일링 코스트 (null이면 미사용)
    public List<string> keywords;   // ["overclock", "network", "rebuild", ...]
    public List<CardEffect> effects;
    public string description;
    public string artworkPath;

    // 코어 카드 전용
    [FormerlySerializedAs("power" + "Effect")]
    public CoreEffect coreEffect;

    // 재구축 전용
    public int rebuildCount;
    public ScalingData rebuildScaling;               // 동적 스케일링 (null이면 rebuildCount 고정)
    [NonSerialized] public bool rebuildScalingApplied; // 동적 스케일링 최초 1회 적용 여부

    // 축적 전용
    public int accumulationTarget;
    public List<CardEffect> accumulationEffects;
    [System.NonSerialized] public int accumulationCount;   // 런타임 축적 카운터 (JSON 비직렬화)

    // 프로토콜 전용
    public string protocolCondition;
    public float protocolConditionValue;    // luckMin 등 수치 조건에 사용
    public List<CardEffect> protocolEffects;
    public bool protocolOverride;           // true이면 프로토콜 발동 시 기본 effects 실행 건너뜀 (대체형)

    // 추출 전용 (패에서 버려질 때 발동)
    public List<CardEffect> extractEffects;

    // 런타임 전용: 임시 카드 표시 (DM_034 시스템 롤백 - 턴 종료 시 소멸)
    [NonSerialized] public bool isTemporary;

    // 런타임 전용: 랜섬웨어 카드의 현재 턴 시작 피해량.
    [NonSerialized] public int ransomwareDamage;

    // 오버클럭 스택 소모 카드: 스택 1 이상 보유 시에만 사용 가능
    public bool requiresOverclock;

    // 폭주(n) 카드: 적 바이러스가 n 이상일 때만 사용 가능
    public int virusThreshold;

    // 과부하(n) 카드: 오버플로우 스택이 n 이상일 때만 사용 가능
    public int overflowMin;

    // 과부하(n) 카드: 오버클럭 스택이 n 이상일 때만 사용 가능
    public int overclockThreshold;

    /// <summary>
    /// description의 {0}, {1}, ... 플레이스홀더를 effects의 value로 치환하여 반환한다.
    /// 예: "적에게 {0} 데미지를 입힌다." → "적에게 6 데미지를 입힌다."
    /// </summary>
    public string GetFormattedDescription()
    {
        return ReplaceStaticDamagePlaceholders(GetIndexedFormattedDescription());
    }

    private string GetIndexedFormattedDescription()
    {
        if (string.IsNullOrEmpty(description))
            return "";

        if (effects == null || effects.Count == 0)
            return description;

        string result = description;
        for (int i = 0; i < effects.Count; i++)
        {
            // value가 정수면 정수로, 아니면 소수로 표시
            float v = effects[i].value;
            string valueStr = (v == (int)v) ? ((int)v).ToString() : v.ToString("0.#");
            result = result.Replace("{" + i + "}", valueStr);
        }

        return result;
    }

    private static string ReplaceStaticDamagePlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        string result = Regex.Replace(text, @"\{d:(\d+)\}\+\(이번 전투에서 해체한 카드 수[xx]\d+\)", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"\{d:(\d+)\}", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"\{dhp:(\d+)\}", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"(\d+)\+\(스택[xx]\d+\)", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"(\d+)-\(스택[xx]\d+\)", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"(\d+)%\+\(스택[xx]\d+%\)", m => $"{m.Groups[1].Value}%");
        result = Regex.Replace(result, @"(\d+)\+\(오버클럭 스택[xx]\d+\)", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"(\d+)-\(오버클럭 스택[xx]\d+\)", m => m.Groups[1].Value);
        result = Regex.Replace(result, @"(\d+)%\+\(오버클럭 스택[xx]\d+%\)", m => $"{m.Groups[1].Value}%");
        return result;
    }

    /// <summary>
    /// description의 스케일링 패턴을 caster의 현재 스탯으로 치환하여 반환한다.
    /// 예: "6+(스택x6)" → overclockStacks=3이면 "24"
    /// </summary>
    public string GetFormattedDescription(EntityState caster)
    {
        string result = GetIndexedFormattedDescription();
        if (caster == null) return ReplaceStaticDamagePlaceholders(result);

        int strWeakDelta = caster.strength - caster.weakness;

        // {d:N}+(스택xM)처럼 공격 보정값과 오버클럭 스택값이 합쳐진 표현
        result = Regex.Replace(result, @"\{d:(\d+)\}\+\(스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return Math.Max(0, b + strWeakDelta + caster.overclockStacks * mult).ToString();
        });

        // N%+(스택xM%)처럼 퍼센트 기반 추가 피해 표현
        result = Regex.Replace(result, @"(\d+)%\+\(스택[xx](\d+)%\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return $"{b + caster.overclockStacks * mult}%";
        });

        // N-(스택xM)처럼 오버클럭 스택이 늘수록 감소하는 표현
        result = Regex.Replace(result, @"(\d+)-\(스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return Math.Max(1, b - caster.overclockStacks * mult).ToString();
        });

        // 기존 장문 표기도 혹시 남아 있으면 같은 방식으로 처리
        result = Regex.Replace(result, @"\{d:(\d+)\}\+\(오버클럭 스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return Math.Max(0, b + strWeakDelta + caster.overclockStacks * mult).ToString();
        });

        result = Regex.Replace(result, @"(\d+)%\+\(오버클럭 스택[xx](\d+)%\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return $"{b + caster.overclockStacks * mult}%";
        });

        result = Regex.Replace(result, @"(\d+)-\(오버클럭 스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return Math.Max(1, b - caster.overclockStacks * mult).ToString();
        });

        // N+(오버클럭 스택xM)
        result = Regex.Replace(result, @"(\d+)\+\(오버클럭 스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return (b + caster.overclockStacks * mult).ToString();
        });

        // standalone (오버클럭 스택xM)
        result = Regex.Replace(result, @"\(오버클럭 스택[xx](\d+)\)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.overclockStacks * mult).ToString();
        });

        // 1. base+(스택xmult)  →  base + overclockStacks * mult  (x 또는 x 모두 허용)
        result = Regex.Replace(result, @"(\d+)\+\(스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return (b + caster.overclockStacks * mult).ToString();
        });

        // 2. (N+스택)  →  N + overclockStacks
        result = Regex.Replace(result, @"\((\d+)\+스택\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            return (b + caster.overclockStacks).ToString();
        });

        // 3. standalone (스택xmult)
        result = Regex.Replace(result, @"\(스택[xx](\d+)\)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.overclockStacks * mult).ToString();
        });

        // 4. base+(네트워크 스택xmult)  →  base + networkStacks * mult
        result = Regex.Replace(result, @"(\d+)\+\(네트워크 스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return (b + caster.networkStacks * mult).ToString();
        });

        // 5. standalone (네트워크 스택xmult)
        result = Regex.Replace(result, @"\(네트워크 스택[xx](\d+)\)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.networkStacks * mult).ToString();
        });

        // 6. base+(오버플로우 스택xmult) / base+(내 오버플로우 스택xmult)
        result = Regex.Replace(result, @"(\d+)\+\((?:내 )?오버플로우 스택[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return (b + caster.overflowNext * mult).ToString();
        });

        // 7. standalone (오버플로우 스택xmult) / (내 오버플로우 스택xmult)
        result = Regex.Replace(result, @"\((?:내 )?오버플로우 스택[xx](\d+)\)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.overflowNext * mult).ToString();
        });

        // 8. 행운 스택xmult
        result = Regex.Replace(result, @"행운 스택[xx](\d+)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.luck * mult).ToString();
        });

        // 9. (N+힘)xM  →  (N + strength) * M
        result = Regex.Replace(result, @"\((\d+)\+힘\)[xx](\d+)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult = int.Parse(m.Groups[2].Value);
            return ((b + caster.strength) * mult).ToString();
        });

        // 10. (N+행운 횟수xM1+불운 횟수xM2)  →  N + luckyThisBattle*M1 + unluckyThisBattle*M2
        result = Regex.Replace(result, @"\((\d+)\+행운 횟수[xx](\d+)\+불운 횟수[xx](\d+)\)", m =>
        {
            int b = int.Parse(m.Groups[1].Value);
            int mult1 = int.Parse(m.Groups[2].Value);
            int mult2 = int.Parse(m.Groups[3].Value);
            return (b + caster.luckyThisBattle * mult1 + caster.unluckyThisBattle * mult2).ToString();
        });

        // 11. standalone 행운 횟수xN  →  luckyThisBattle * N
        result = Regex.Replace(result, @"행운 횟수[xx](\d+)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.luckyThisBattle * mult).ToString();
        });

        // 12. standalone 불운 횟수xN  →  unluckyThisBattle * N
        result = Regex.Replace(result, @"불운 횟수[xx](\d+)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return (caster.unluckyThisBattle * mult).ToString();
        });

        // 13. (해체xN) - dismantledThisBattle * N을 현재 값으로 표시
        result = Regex.Replace(result, @"\(해체[xx](\d+)\)", m =>
        {
            int mult = int.Parse(m.Groups[1].Value);
            return $"(현재 {caster.dismantledThisBattle * mult})";
        });

        result = ReplaceDismantledThisBattlePlaceholders(result, caster, strWeakDelta);
        result = ReplaceCardAccumulatedDamagePlaceholders(result, caster, strWeakDelta);

        // {dhp:MAX} - 시전자가 잃은 체력 비율에 비례한 현재 피해 (1~MAX)
        result = Regex.Replace(result, @"\{dhp:(\d+)\}", m =>
        {
            int max = int.Parse(m.Groups[1].Value);
            if (caster.maxHp <= 0) return max.ToString();

            float lostFraction = (float)(caster.maxHp - caster.hp) / caster.maxHp;
            int damage = Math.Max(1, Math.Min(max, MathfRoundToInt(max * lostFraction)));
            return damage.ToString();
        });

        // 14. {d:N} - 데미지 값에 힘/약화 적용
        result = Regex.Replace(result, @"\{d:(\d+)\}", m =>
        {
            int baseVal = int.Parse(m.Groups[1].Value);
            int final_ = Math.Max(0, baseVal + strWeakDelta);
            return final_.ToString();
        });

        // 15. 재구축(N) - 현재 남은 재구축 횟수로 치환, 0이면 텍스트(+뒤 공백) 삭제
        if (rebuildCount > 0)
            result = Regex.Replace(result, @"\[?재구축\(\d+\)\]?", $"재구축({rebuildCount})");
        else
            result = Regex.Replace(result, @"\[?재구축\(\d+\)\]?\s*", "");

        // 16. 도박:N% - 행운/보너스 확률을 반영한 실제 판정 확률로 치환
        //     (ResolveGamble과 동일: base + luck*1% + gambleBonusChance*1%, 0~100 클램프)
        result = Regex.Replace(result, @"도박:(\d+)%", m =>
        {
            int baseChance = int.Parse(m.Groups[1].Value);
            int dyn = Math.Max(0, Math.Min(100, baseChance + caster.luck + caster.Turn.gambleBonusChance));
            return $"도박:{dyn}%";
        });

        return result;
    }

    private string ReplaceCardAccumulatedDamagePlaceholders(string text, EntityState caster, int strWeakDelta)
    {
        if (string.IsNullOrEmpty(text) || effects == null)
            return text;

        string result = text;
        foreach (var effect in EnumerateEffects(effects))
        {
            if (effect?.scaling == null || effect.scaling.source != "cardAccumCount")
                continue;

            if (effect.type != "damage" && effect.type != "scaledDamage")
                continue;

            int baseValue = MathfRoundToInt(effect.value);
            string placeholder = "{d:" + baseValue + "}";
            int index = result.IndexOf(placeholder, StringComparison.Ordinal);
            if (index < 0)
                continue;

            float computed = effect.value + accumulationCount * effect.scaling.multiplier;
            if (effect.scaling.max > 0 && computed > effect.scaling.max)
                computed = effect.scaling.max;
            if (effect.scaling.min > 0 && computed < effect.scaling.min)
                computed = effect.scaling.min;

            int finalValue = Math.Max(0, MathfRoundToInt(computed) + strWeakDelta);
            result = result.Substring(0, index) + finalValue + result.Substring(index + placeholder.Length);
        }

        return result;
    }

    private string ReplaceDismantledThisBattlePlaceholders(string text, EntityState caster, int strWeakDelta)
    {
        if (string.IsNullOrEmpty(text) || effects == null)
            return text;

        string result = text;
        foreach (var effect in EnumerateEffects(effects))
        {
            if (effect?.scaling == null || effect.scaling.source != "dismantledThisBattle")
                continue;

            int multiplier = MathfRoundToInt(effect.scaling.multiplier);
            if (multiplier == 0)
                continue;

            float computed = effect.value + caster.dismantledThisBattle * effect.scaling.multiplier;
            if (effect.scaling.max > 0 && computed > effect.scaling.max)
                computed = effect.scaling.max;
            if (effect.scaling.min > 0 && computed < effect.scaling.min)
                computed = effect.scaling.min;

            int finalValue = MathfRoundToInt(computed);

            if (effect.type == "damage" || effect.type == "scaledDamage")
            {
                int baseValue = MathfRoundToInt(effect.value);
                string pattern = @"\{d:" + baseValue + @"\}\+\(이번 전투에서 해체한 카드 수[xx]" + multiplier + @"\)";
                result = Regex.Replace(result, pattern, Math.Max(0, finalValue + strWeakDelta).ToString());
            }
            else if (effect.type == "shield" || effect.type == "scaledShield")
            {
                string pattern = @"이번 전투에서 해체한 카드 수[xx]" + multiplier;
                result = Regex.Replace(result, pattern, finalValue.ToString());
            }
        }

        return result;
    }

    private static IEnumerable<CardEffect> EnumerateEffects(IEnumerable<CardEffect> source)
    {
        if (source == null)
            yield break;

        foreach (var effect in source)
        {
            if (effect == null)
                continue;

            yield return effect;

            foreach (var nested in EnumerateEffects(effect.thenEffects))
                yield return nested;
            foreach (var nested in EnumerateEffects(effect.elseEffects))
                yield return nested;
            foreach (var nested in EnumerateEffects(effect.effects))
                yield return nested;
        }
    }

    private static int MathfRoundToInt(float value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 특정 키워드를 가지고 있는지 확인.
    /// </summary>
    public bool HasKeyword(string keyword)
    {
        return keywords != null && keywords.Contains(keyword);
    }

    /// <summary>
    /// 깊은 복사본을 반환한다. 재구축/축적 시 카드별 런타임 상태 분리에 필요.
    /// </summary>
    public CardData Clone()
    {
        return new CardData
        {
            id = id,
            cardName = cardName,
            pack = pack,
            type = type,
            rarity = rarity,
            tier = tier,
            cost = cost,
            keywords = keywords != null ? new List<string>(keywords) : null,
            effects = effects?.ConvertAll(e => e.Clone()),
            description = description,
            artworkPath = artworkPath,
            coreEffect = coreEffect == null ? null : new CoreEffect
            {
                trigger = coreEffect.trigger,
                coreType = coreEffect.coreType,
                value = coreEffect.value,
                energyCost = coreEffect.energyCost,
                maxBonusStack = coreEffect.maxBonusStack,
                energy = coreEffect.energy,
                draw = coreEffect.draw,
                damage = coreEffect.damage,
                heal = coreEffect.heal,
                costReduction = coreEffect.costReduction,
                multiplier = coreEffect.multiplier,
                threshold = coreEffect.threshold,
                effects = coreEffect.effects?.ConvertAll(e => e.Clone()),
            },
            rebuildCount = rebuildCount,
            rebuildScaling = rebuildScaling,
            accumulationTarget = accumulationTarget,
            accumulationEffects = accumulationEffects?.ConvertAll(e => e.Clone()),
            protocolCondition = protocolCondition,
            protocolConditionValue = protocolConditionValue,
            protocolEffects = protocolEffects?.ConvertAll(e => e.Clone()),
            protocolOverride = protocolOverride,
            extractEffects = extractEffects?.ConvertAll(e => e.Clone()),
            isTemporary = isTemporary,
            ransomwareDamage = ransomwareDamage,
            requiresOverclock = requiresOverclock,
            virusThreshold = virusThreshold,
            overflowMin = overflowMin,
            overclockThreshold = overclockThreshold,
            dynamicCost = dynamicCost == null ? null : new DynamicCostData
            {
                condition = dynamicCost.condition,
                cost = dynamicCost.cost,
            },
            scaledCost = scaledCost == null ? null : new ScaledCostData
            {
                source = scaledCost.source,
                reductionPerUnit = scaledCost.reductionPerUnit,
                min = scaledCost.min,
            },
        };
    }
}
