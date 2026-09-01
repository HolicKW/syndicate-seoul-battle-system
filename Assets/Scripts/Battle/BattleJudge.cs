using UnityEngine;

/// <summary>
/// 전투 승패 판정 및 부활 처리.
/// BattleEngine에서 분리된 순수 판정 클래스.
/// 상태 변경(battleEnded, OnBattleEnd 호출)은 BattleEngine이 담당하고,
/// 이 클래스는 "어떤 결과인가"만 반환한다.
/// </summary>
public class BattleJudge
{
    private readonly BattleEngine engine;

    public BattleJudge(BattleEngine engine)
    {
        this.engine = engine;
    }

    /// <summary>
    /// 현재 전투 상태를 판정한다.
    /// Victory / Defeat이 반환되면 BattleEngine이 종료 처리를 수행한다.
    /// 이미 종료됐거나 훈련 모드면 None을 반환한다.
    /// </summary>
    public BattleResult Evaluate()
    {
        if (engine.IsBattleEnded) return BattleResult.None;
        if (engine.IsTrainingMode) return BattleResult.None;

        if (engine.Enemy.IsDead && !TryRevive(engine.Enemy))
            return BattleResult.Victory;

        if (engine.Player.IsDead && !TryRevive(engine.Player))
            return BattleResult.Defeat;

        return BattleResult.None;
    }

    /// <summary>
    /// onDeath 트리거 코어가 있으면 최대 HP의 30%로 부활시킨다.
    /// 부활에 성공하면 true, 코어가 없으면 false.
    /// </summary>
    private bool TryRevive(EntityState entity)
    {
        for (int i = entity.activeCores.Count - 1; i >= 0; i--)
        {
            var core = entity.activeCores[i];
            if (core.coreEffect?.trigger == "onDeath")
            {
                entity.hp = Mathf.Max(1, Mathf.RoundToInt(entity.maxHp * 0.3f));
                entity.activeCores.RemoveAt(i); // 1회용
                Debug.Log($"[BattleJudge] 부활! HP {entity.hp}");
                BattleLogger.Log(BattleLogType.Info, $"부활! HP {entity.hp}");
                return true;
            }
        }
        return false;
    }
}

/// <summary>BattleJudge.Evaluate() 반환값.</summary>
public enum BattleResult
{
    None,    // 전투 계속
    Victory, // 적 사망
    Defeat,  // 플레이어 사망
}
