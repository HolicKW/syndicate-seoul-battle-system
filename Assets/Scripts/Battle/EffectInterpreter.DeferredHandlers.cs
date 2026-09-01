using UnityEngine;

/// <summary>
/// 지연 실행 핸들러 (6종)
/// energyNextTurn / nextTurnShield / nextTurnSelfDamage /
/// returnToHandNextTurn / addEndOfTurnDiscard / selfDamageAtTurnEnd
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterDeferredHandlers()
    {
        Register("energyNextTurn",       HandleEnergyNextTurn);
        Register("nextTurnShield",       HandleNextTurnShield);
        Register("nextTurnSelfDamage",   HandleNextTurnSelfDamage);
        Register("returnToHandNextTurn", HandleReturnToHandNextTurn);
        Register("addEndOfTurnDiscard",  HandleAddEndOfTurnDiscard);
        Register("selfDamageAtTurnEnd",  HandleSelfDamageAtTurnEnd);
    }

    // -- 57. energyNextTurn --
    private void HandleEnergyNextTurn(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(OcVal(ctx));
        EnqueueDeferred(ctx, "nextTurnStart", new CardEffect { type = "energy", value = amount });
    }

    // -- 58. nextTurnShield --
    private void HandleNextTurnShield(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(OcVal(ctx));
        EnqueueDeferred(ctx, "nextTurnStart", new CardEffect { type = "shield", value = amount });
    }

    // -- 59. nextTurnSelfDamage --
    private void HandleNextTurnSelfDamage(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(OcVal(ctx));
        EnqueueDeferred(ctx, "nextTurnStart", new CardEffect { type = "selfDamage", value = amount });
    }

    // -- 60. returnToHandNextTurn --
    private void HandleReturnToHandNextTurn(EffectContext ctx)
    {
        EnqueueDeferred(ctx, "nextTurnStart", new CardEffect { type = "returnToHandZeroCost" });
    }

    // -- 61. addEndOfTurnDiscard --
    private void HandleAddEndOfTurnDiscard(EffectContext ctx)
    {
        var eff = ctx.Effect;
        EnqueueDeferred(ctx, "turnEnd", new CardEffect
        {
            type = "discard",
            value = eff.value,
            mode = eff.mode,
            countAll = eff.countAll,
        });
    }

    // -- 62. selfDamageAtTurnEnd --
    private void HandleSelfDamageAtTurnEnd(EffectContext ctx)
    {
        int amount = Mathf.RoundToInt(OcVal(ctx));
        EnqueueDeferred(ctx, "turnEnd", new CardEffect { type = "selfDamage", value = amount });
    }
}
