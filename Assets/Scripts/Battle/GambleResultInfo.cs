/// <summary>
/// 도박 판정의 진행 단계. 연출 분기에 사용된다.
/// </summary>
public enum GamblePhase
{
    /// <summary>일반 첫 판정.</summary>
    FirstRoll,
    /// <summary>재시도 / 파산 재판정 등 재굴림이 발생한 판정.</summary>
    Reroll,
    /// <summary>주사위 깎기 등으로 실패했지만 불운 페널티가 무효화된 판정.</summary>
    Softened,
}

/// <summary>
/// 도박 결과 1건을 연출 계층으로 전달하기 위한 불변 값.
/// BattleEngine.OnGambleResult 이벤트의 페이로드.
/// </summary>
public readonly struct GambleResultInfo
{
    public readonly bool Success;
    public readonly float ChancePct;
    public readonly int Luck;
    public readonly GamblePhase Phase;
    public readonly bool IsPlayer;

    /// <summary>주사위 출목(1~6). 0보다 크면 주사위 모드(성공/실패 대신 눈금 표시).</summary>
    public readonly int DiceValue;

    /// <summary>성공/실패가 아닌 주사위 결과 연출인지.</summary>
    public bool IsDice => DiceValue > 0;

    public GambleResultInfo(bool success, float chancePct, int luck, GamblePhase phase, bool isPlayer)
    {
        Success = success;
        ChancePct = chancePct;
        Luck = luck;
        Phase = phase;
        IsPlayer = isPlayer;
        DiceValue = 0;
    }

    private GambleResultInfo(int diceValue, bool isPlayer)
    {
        Success = false;
        ChancePct = 0f;
        Luck = 0;
        Phase = GamblePhase.FirstRoll;
        IsPlayer = isPlayer;
        DiceValue = diceValue;
    }

    /// <summary>주사위 결과(눈금 표시) 연출 정보를 만든다.</summary>
    public static GambleResultInfo Dice(int rollValue, bool isPlayer) =>
        new GambleResultInfo(rollValue, isPlayer);
}
