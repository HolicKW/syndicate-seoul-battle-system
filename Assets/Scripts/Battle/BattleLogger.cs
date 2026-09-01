using System;
using System.Collections.Generic;

/// <summary>
/// 전투 로그 엔트리 타입.
/// </summary>
public enum BattleLogType
{
    Info,           // 일반 정보 (카드 사용, 턴 시작 등)
    Effect,         // 이펙트 실행
    Damage,         // 데미지 적용
    StateChange,    // 상태 변화 (버프/디버프)
    Warning,        // 경고 (미등록 핸들러, 조건 미충족 등)
    Gamble,         // 도박 성공/실패
}

/// <summary>
/// 전투 로그를 발생시킨 주체.
/// </summary>
public enum BattleLogActor
{
    Neutral,
    Player,
    Enemy,
}

/// <summary>
/// 전투 로그 한 줄.
/// </summary>
public class BattleLogEntry
{
    public BattleLogType type;
    public BattleLogActor actor;
    public string message;
    public float timestamp;

    public BattleLogEntry(BattleLogType type, string message, BattleLogActor actor = BattleLogActor.Neutral)
    {
        this.type = type;
        this.actor = actor;
        this.message = message;
        this.timestamp = UnityEngine.Time.time;
    }
}

/// <summary>
/// 전투 중 발생하는 이벤트를 기록하는 정적 로거.
/// BattleLogUI가 OnLogAdded를 구독하여 실시간 표시.
/// </summary>
public static class BattleLogger
{
    public static readonly List<BattleLogEntry> Entries = new();
    public static event Action<BattleLogEntry> OnLogAdded;

    /// <summary>로깅 활성화 여부.</summary>
    public static bool Enabled;

    private static readonly Stack<BattleLogActor> ActorStack = new();

    public static void Log(BattleLogType type, string message)
    {
        Log(type, message, CurrentActor);
    }

    public static void Log(BattleLogType type, string message, BattleLogActor actor)
    {
        if (!Enabled) return;
        var entry = new BattleLogEntry(type, message, actor);
        Entries.Add(entry);
        OnLogAdded?.Invoke(entry);
    }

    public static IDisposable ActorScope(BattleLogActor actor)
    {
        ActorStack.Push(actor);
        return new ActorScopeHandle();
    }

    public static void Clear()
    {
        Entries.Clear();
        ActorStack.Clear();
    }

    /// <summary>
    /// 로그 메시지 안에 카드 링크 마커를 삽입한다.
    /// BattleLogUI에서 파싱하여 밑줄+호버 프리뷰로 렌더링된다.
    /// 사용: $"해체: {BattleLogger.CardRef(card)}"
    /// </summary>
    public static string CardRef(CardData card)
    {
        if (card == null) return "?";
        // §card:id§name§ 형식으로 마킹
        return $"\u00A7card:{card.id}\u00A7{card.cardName}\u00A7";
    }

    private static BattleLogActor CurrentActor =>
        ActorStack.Count > 0 ? ActorStack.Peek() : BattleLogActor.Neutral;

    private sealed class ActorScopeHandle : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (ActorStack.Count > 0)
                ActorStack.Pop();
        }
    }
}

/// <summary>
/// EntityState의 주요 필드 스냅샷. 이펙트 전/후 비교에 사용.
/// </summary>
public struct StateSnapshot
{
    public int hp, maxHp, shield, energy;
    public int strength, weakness, virus, corrosion;
    public int overclockStacks, networkStacks, luck, overflowNext;
    public int protocolBypassCount, nextProtocolEffectRepeat;

    public static StateSnapshot Capture(EntityState s)
    {
        return new StateSnapshot
        {
            hp = s.hp, maxHp = s.maxHp, shield = s.shield, energy = s.energy,
            strength = s.strength, weakness = s.weakness,
            virus = s.virus, corrosion = s.corrosion,
            overclockStacks = s.overclockStacks, networkStacks = s.networkStacks,
            luck = s.luck, overflowNext = s.overflowNext,
            protocolBypassCount = s.protocolBypassCount,
            nextProtocolEffectRepeat = s.nextProtocolEffectRepeat,
        };
    }

    /// <summary>
    /// 변경된 필드를 "HP 100→95, 실드 0→5" 형식의 문자열로 반환.
    /// 변경 없으면 null.
    /// </summary>
    public string Diff(StateSnapshot after)
    {
        var changes = new List<string>(4);
        // HP는 ApplyDamage / 회복 / 자해 로그에서 별도 출력 - 여기서 중복 제거
        if (maxHp != after.maxHp)                 changes.Add($"최대HP {maxHp}→{after.maxHp}");
        if (shield != after.shield)               changes.Add($"실드 {shield}→{after.shield}");
        if (energy != after.energy)               changes.Add($"에너지 {energy}→{after.energy}");
        if (strength != after.strength)           changes.Add($"힘 {strength}→{after.strength}");
        if (weakness != after.weakness)           changes.Add($"약화 {weakness}→{after.weakness}");
        if (virus != after.virus)                 changes.Add($"바이러스 {virus}→{after.virus}");
        if (corrosion != after.corrosion)         changes.Add($"부식 {corrosion}→{after.corrosion}");
        if (overclockStacks != after.overclockStacks) changes.Add($"오버클럭 {overclockStacks}→{after.overclockStacks}");
        if (networkStacks != after.networkStacks) changes.Add($"네트워크 {networkStacks}→{after.networkStacks}");
        if (luck != after.luck)                   changes.Add($"행운 {luck}→{after.luck}");
        if (overflowNext != after.overflowNext)   changes.Add($"오버플로우 {overflowNext}→{after.overflowNext}");
        if (protocolBypassCount != after.protocolBypassCount) changes.Add($"프로토콜바이패스 {protocolBypassCount}→{after.protocolBypassCount}");
        if (nextProtocolEffectRepeat != after.nextProtocolEffectRepeat) changes.Add($"프로토콜반복 {nextProtocolEffectRepeat}→{after.nextProtocolEffectRepeat}");
        return changes.Count > 0 ? string.Join(", ", changes) : null;
    }
}
