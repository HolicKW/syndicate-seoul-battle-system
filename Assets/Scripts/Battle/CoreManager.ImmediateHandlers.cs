using UnityEngine;

/// <summary>
/// 즉시 효과 핸들러 (10종)
/// coreType: extraDraw / extraEnergy / rebuildBonus / permanentNetworkBuff /
///            doubleNetworkStacks / extraRebuildCount / energyCarryOver /
///            removeOverclockLimit / fixedDemerit / bankruptcy
/// </summary>
public partial class CoreManager
{
    private void RegisterImmediateHandlers()
    {
        // 드로우
        Immediate("extraDraw", ctx =>
            ctx.Entity.extraDraw += Mathf.Max(1, (int)ctx.Val));

        // 에너지
        Immediate("extraEnergy", ctx =>
            ctx.Entity.baseEnergy += Mathf.Max(1, (int)ctx.Val));

        // 재구축 보너스
        Immediate("rebuildBonus", ctx =>
            ctx.Entity.rebuildBonuses += Mathf.Max(1, (int)ctx.Val));

        // 네트워크 버프
        Immediate("permanentNetworkBuff", ctx =>
        {
            int bonus = Mathf.Max(1, (int)ctx.Val);
            ctx.Entity.networkDmgBonus    += bonus;
            ctx.Entity.networkShieldBonus += bonus;
        });

        // 네트워크 스택 2배
        Immediate("doubleNetworkStacks", ctx =>
            ctx.Entity.doubleNetworkStacks = true);

        // 재구축 횟수 보너스
        Immediate("extraRebuildCount", ctx =>
        {
            int bonus = Mathf.Max(1, (int)ctx.Val);
            foreach (var c in ctx.Entity.drawPile)
                if (c.rebuildCount > 0 || c.HasKeyword("rebuild")) c.rebuildCount += bonus;
            foreach (var c in ctx.Entity.hand)
                if (c.rebuildCount > 0 || c.HasKeyword("rebuild")) c.rebuildCount += bonus;
        });

        // 에너지 이월
        Immediate("energyCarryOver", ctx =>
            ctx.Entity.energyCarryOver = true);

        // 오버클럭 제한 해제
        Immediate("removeOverclockLimit", ctx =>
            ctx.Entity.overclockUnlimited = true);

        // 고정 오버클럭 페널티
        Immediate("fixedDemerit", ctx =>
            ctx.Entity.fixedOverclockStacks = Mathf.Max(1, (int)ctx.Val));

        // 파산 - activeCores 등록만으로 작동 (BattleEngine.CanPlayCard에서 처리)
        Immediate("bankruptcy", ctx => { });
    }

    /// <summary>immediateCoreHandlers 등록 단축 메서드</summary>
    private void Immediate(string coreType, System.Action<CoreContext> handler)
        => immediateCoreHandlers[coreType] = handler;
}
