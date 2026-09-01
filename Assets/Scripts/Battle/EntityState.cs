using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 1턴 내에서만 유효한 카운터/플래그 집합.
/// EntityState.Turn 필드로 보관하며, 턴 종료 시 <c>Turn = default</c> 한 줄로 전체 초기화된다.
/// 새 카운터 추가 시 이 struct에만 추가하면 리셋이 자동 보장된다.
/// </summary>
public struct TurnCounters
{
    // -- 행동 카운터 --
    public int selfDamageThisTurn;
    public int cardsPlayedThisTurn;
    public int skillsPlayedThisTurn;
    public int totalDamageThisTurn;
    public int networkCardsPlayedThisTurn;
    public int dismantledThisTurn;

    // -- 추출 --
    public bool extractEnergyBonusActive;
    public int  extractEnergyRemainingCount;

    // -- 도박/운 --
    public int  luckyThisTurn;
    public int  unluckyThisTurn;
    public int  gambleSuccessThisTurn;
    public bool luckyEnergyGainedThisTurn;
    public int  gambleBonusChance;
    public int  buffInsuranceValue;
    public int  buffInsuranceLuckValue;
    public bool retryOnUnluckFlag;
    public int  softenNextUnluckPenalty;
    public bool lastGambleUnluckSoftened;
    public bool luckyDayDebtActive;
    public int  luckyDayLuckPerSuccess;
    public int  luckyDayHpLossPerSuccess;
    public int  luckyDaySuccessCount;

    // -- 데미지 수정 --
    public int   nextAttackBonus;
    public float nextDamageMultiplier;
    public bool  armorPierceNextAttack;
    public float damageReductionThisTurn;
    public bool  invincibleThisTurn;
    public int   evadeNextHits;
    public bool  preventNextSelfDamage;
    public int   retaliateOnHitDamage;

    // -- 바이러스 추적 --
    public int virusConsumedThisTurn;
    public int virusAppliedThisTurn;
    public int virusOnCardPlayedThisTurn;

    // -- 최근 HP 변화 --
    public int lastHpLoss;
}

/// <summary>
/// 지연 실행 액션. 특정 타이밍(턴 종료/다음 턴 시작)에 실행될 이펙트를 보관한다.
/// </summary>
[System.Serializable]
public class DeferredAction
{
    /// <summary>"turnEnd" 또는 "nextTurnStart"</summary>
    public string timing;

    /// <summary>실행할 이펙트 데이터</summary>
    public CardEffect effect;

    /// <summary>이펙트를 등록한 카드 (returnToHandZeroCost 등에서 카드 참조가 필요한 경우)</summary>
    public CardData card;

    /// <summary>시전자 (이펙트를 등록한 측)</summary>
    public EntityState caster;

    /// <summary>대상 (상대방)</summary>
    public EntityState target;
}

/// <summary>
/// 전투 중 플레이어/적 한 쪽의 모든 상태를 보관하는 순수 데이터 클래스.
/// JS engine.js의 makePlayer() 필드를 C#으로 매핑.
/// MonoBehaviour가 아니므로 new EntityState()로 생성한다.
/// </summary>
public class EntityState
{
    // -- 기본 스탯 --
    public int hp;
    public int maxHp;
    public int shield;
    public int energy;
    public int baseEnergy = 3;

    // -- 버프/디버프 --
    public int strength;        // 힘: 공격 데미지 증가
    public int weakness;        // 약화: 적 공격 데미지 감소
    public int virus;           // 바이러스 스택
    public int corrosion;       // 부식: 매 턴 실드 감소

    // -- 오버클럭/오버플로우 --
    public int overclockStacks;
    public int overclockMax = 4;
    public bool overclockUnlimited;     // OC_037 '모든 제한 해제'
    public int fixedOverclockStacks;    // OC_038 '영구 기관 코어': 0이면 비활성, >0이면 자해/오버플로우 스케일 계산 시 해당 값으로 고정
    public int overflowNext;            // 다음 턴 에너지 감소
    public int energyDrainNext;         // 다음 턴 추가 에너지 감소
    public int virusOnCardPlayedNextTurn; // 다음 자기 턴 동안 카드 사용 시 자신에게 부여될 바이러스

    // -- 도박 (러시안 룰렛 팩) --
    public int luck;
    public int luckyThisBattle;         // 전투 누적 행운 횟수
    public int unluckyThisBattle;       // 전투 누적 불운 횟수
    public int consecutiveLuck;
    public bool ignoreUnluck;
    public bool doubleLuck;
    // 이번 턴 도박 관련 카운터는 Turn struct에 있음 (gambleBonusChance, luckyThisTurn, 등)

    // -- 턴 카운터 (턴 종료 시 Turn = default 로 일괄 초기화) --
    public TurnCounters Turn;

    // -- 네트워크 팩 추적 --
    public int networkStacks;
    public int networkCardsPlayedThisBattle;

    // -- 해체/재구축 추적 --
    public int dismantledThisBattle;
    public int totalRebuildsThisBattle;

    // -- 덱 영역 --
    public List<CardData> drawPile = new List<CardData>();
    public List<CardData> hand = new List<CardData>();
    public List<CardData> voidPile = new List<CardData>();
    [FormerlySerializedAs("active" + "Powers")]
    public List<CardData> activeCores = new List<CardData>();
    public List<CardData> rollbackStoredHand = new List<CardData>();

    // -- 드로우 설정 --
    public int baseDraw = 5;
    public int extraDraw;
    public int rebuildBonuses;

    // -- 프로토콜/네트워크 플래그 --
    public CardData lastPlayedCard;
    public int protocolBypassCount;
    public int nextProtocolShieldBonus;  // 다음 프로토콜 발동 시 추가 실드
    public int nextProtocolEffectRepeat; // 다음 프로토콜 발동 시 protocolEffects 추가 실행 횟수
    public bool doubleNetworkStacks;
    public bool energyCarryOver;

    // -- 해체/추출 --
    public int extractEnergyAmount;         // 추출당 획득 에너지량 (활성/횟수는 Turn struct)

    // -- 코어 카드 보너스 --
    public int networkDmgBonus;         // permanentNetworkBuff: 네트워크 데미지 고정 보너스
    public int networkShieldBonus;      // permanentNetworkBuff: 네트워크 실드 고정 보너스

    // -- 기타 플래그 --
    public bool forceEndTurn;
    public int skipNextDrawCount;   // BattleInitializer 선행 드로우 후 이펙트 드로우 중복 방지
    public int turnEndHpPenalty;            // 턴 종료 시 잃을 HP (gambleDebt 등)
    public CardData lastDrawnCard;          // 가장 마지막에 드로우된 카드 (adjustCostLastDrawn용)
    // 데미지 수정 플래그는 Turn struct에 있음 (preventNextSelfDamage, damageReductionThisTurn, 등)

    // -- 지연 실행 큐 --
    /// <summary>
    /// 특정 타이밍에 실행될 지연 액션 큐.
    /// "turnEnd": 이번 턴 종료 시, "nextTurnStart": 다음 턴 시작 시 실행.
    /// ResetTurnCounters에서 초기화하지 않음 - 전투 내 지속.
    /// </summary>
    public List<DeferredAction> deferredActions = new List<DeferredAction>();

    // -- 참조 --
    /// <summary>
    /// 상대방 EntityState 참조 (상호 접근용).
    /// </summary>
    public EntityState opponent;

    // -- 팩토리 메서드 --

    /// <summary>
    /// 기본값으로 초기화된 EntityState를 생성한다.
    /// </summary>
    public static EntityState Create(int startHp, int startEnergy = 3)
    {
        return new EntityState
        {
            hp = startHp,
            maxHp = startHp,
            shield = 0,
            energy = startEnergy,
            baseEnergy = startEnergy,
        };
    }

    // -- 유틸리티 --

    /// <summary>
    /// 턴 종료 시 1회성 턴 카운터를 초기화한다.
    /// TurnCounters struct이므로 default 대입 한 줄로 모든 필드가 초기화된다.
    /// 새 카운터 추가 시 TurnCounters struct에만 추가하면 자동으로 리셋된다.
    /// </summary>
    public void ResetTurnCounters() => Turn = default;

    /// <summary>
    /// 현재 유효 에너지 (overflowNext 적용 전 기준값).
    /// </summary>
    public int TurnStartEnergy => energyCarryOver ? energy : baseEnergy;

    public bool IsDead => hp <= 0;

    // -- 스탯 수정 메서드 --
    // 직접 필드 변경 대신 이 메서드를 사용하면 범위 보장이 자동으로 된다.
    // 기존 직접 접근 코드는 그대로 유지되며, 신규 코드에서 점진적으로 교체한다.

    /// <summary>
    /// HP를 amount만큼 회복한다. maxHp를 초과하지 않는다.
    /// </summary>
    public void Heal(int amount)
    {
        hp = Mathf.Min(hp + amount, maxHp);
    }

    /// <summary>
    /// 실드를 amount만큼 증가시킨다.
    /// </summary>
    public void AddShield(int amount)
    {
        shield += amount;
    }

    /// <summary>
    /// 실드를 amount만큼 감소시킨다. 0 미만으로 내려가지 않는다.
    /// </summary>
    public void ReduceShield(int amount)
    {
        shield = Mathf.Max(0, shield - amount);
    }

    /// <summary>
    /// 실드·무적 판정 없이 HP를 직접 amount만큼 감소시킨다. 0 미만으로 내려가지 않는다.
    /// 자해, 턴 종료 페널티, bankruptcy 등 "순수 HP 차감"에 사용한다.
    /// 일반 전투 데미지는 BattleEngine.ApplyDamage()를 사용할 것.
    /// </summary>
    public void TakeDamageRaw(int amount)
    {
        hp = Mathf.Max(0, hp - amount);
    }
}
