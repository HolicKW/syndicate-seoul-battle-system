/// <summary>
/// 이펙트 핸들러에 전달되는 실행 컨텍스트.
/// 시전자/대상 상태, 카드/이펙트 데이터, 엔진 참조를 묶는다.
/// </summary>
public class EffectContext
{
    /// <summary>시전자 (카드를 사용한 측)</summary>
    public EntityState Caster;

    /// <summary>대상 (상대방)</summary>
    public EntityState Target;

    /// <summary>사용된 카드 전체 데이터</summary>
    public CardData Card;

    /// <summary>현재 실행 중인 개별 이펙트</summary>
    public CardEffect Effect;

    /// <summary>
    /// 오버클럭 스케일 배율.
    /// 오버클럭 카드 사용 시 overclockStacks 기반으로 미리 계산되어 전달된다.
    /// 0이면 미사용.
    /// </summary>
    public float OverclockScale;

    /// <summary>
    /// overclockConsume 시 소모된 스택 수 (후속 이펙트에서 참조).
    /// </summary>
    public int OverclockConsumed;

    /// <summary>
    /// consumeAllEnergy 시 소모된 에너지 (후속 이펙트에서 참조).
    /// </summary>
    public int EnergyConsumed;

    /// <summary>
    /// 오픈 액세스 사용 전 플레이어가 선택한 카드 종류 필터.
    /// </summary>
    public string OpenAccessFilter;

    /// <summary>
    /// 이번 카드에서 힘/약화 보정이 이미 적용되었는지 (다단히트 시 중복 적용 방지).
    /// </summary>
    public bool StrengthApplied;

    /// <summary>
    /// 현재 카드가 입히는 공격 피해 배율. 0이면 미사용.
    /// </summary>
    public float CardDamageMultiplier;

    /// <summary>
    /// BattleEngine 참조. 효과 핸들러에서 엔진 기능(드로우, 해체 등)에 접근할 때 사용.
    /// </summary>
    public BattleEngine Engine;

    /// <summary>
    /// 현재 이펙트 실행 재귀 깊이. conditional 순환 참조 방지용.
    /// ExecuteAll 진입 시 증가, 최대 10까지 허용.
    /// </summary>
    public int Depth;

    /// <summary>
    /// true로 설정하면 ExecuteAll이 나머지 이펙트 실행을 즉시 중단한다.
    /// overclockReduce 등 "소모 실패 시 후속 효과 취소" 핸들러에서 사용.
    /// ExecuteAll 내부에서 break 후 자동으로 false로 리셋된다.
    /// </summary>
    public bool AbortRemainingEffects;
}
