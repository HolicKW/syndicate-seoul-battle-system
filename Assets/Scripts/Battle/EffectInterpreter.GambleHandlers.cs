using UnityEngine;

/// <summary>
/// Tier 3: 러시안 룰렛 핸들러 (8종)
/// gamble / russianRoulette / diceRoll / fullHouseHit / buffInsurance /
/// gambleChanceBuff / retryOnUnluck / coinFlipHeads
/// </summary>
public partial class EffectInterpreter
{
    private void RegisterGambleHandlers()
    {
        Register("gamble",           HandleGamble);
        Register("russianRoulette",  HandleRussianRoulette);
        Register("diceRoll",         HandleDiceRoll);
        Register("fullHouseHit",     HandleFullHouseHit);
        Register("buffInsurance",    HandleBuffInsurance);
        Register("buffInsuranceLuck", HandleBuffInsurance);
        Register("gambleChanceBuff", HandleGambleChanceBuff);
        Register("retryOnUnluck",    HandleRetryOnUnluck);
        Register("coinFlipHeads",    HandleCoinFlipHeads);
    }

    // -- 28. gamble --
    private void HandleGamble(EffectContext ctx)
    {
        var eff = ctx.Effect;
        float baseChance = eff.chance > 0 ? eff.chance : 0.5f;

        bool success = ResolveGamble(ctx, baseChance);
        var effects = success ? eff.thenEffects : ctx.Caster.Turn.lastGambleUnluckSoftened ? null : eff.elseEffects;
        if (effects != null) ExecuteAll(effects, ctx);
    }

    // -- 29. russianRoulette --
    private void HandleRussianRoulette(EffectContext ctx)
    {
        var eff = ctx.Effect;
        int maxRounds = eff.EffectiveHits > 1 ? eff.EffectiveHits : 6;
        float baseChance = eff.chance > 0 ? eff.chance : 0.5f;

        for (int round = 0; round < maxRounds; round++)
        {
            bool survived = ResolveGamble(ctx, baseChance);
            if (survived)
            {
                if (eff.thenEffects != null) ExecuteAll(eff.thenEffects, ctx);
            }
            else
            {
                if (!ctx.Caster.Turn.lastGambleUnluckSoftened && eff.elseEffects != null) ExecuteAll(eff.elseEffects, ctx);
                break;
            }
        }
    }

    // -- 30. diceRoll --
    private void HandleDiceRoll(EffectContext ctx)
    {
        var caster = ctx.Caster;
        var eff = ctx.Effect;

        int roll = UnityEngine.Random.Range(1, 7);

        if (eff.mode == "luck" && caster.luck > 0)
            roll = Mathf.Clamp(roll + Mathf.Min(caster.luck, 3), 1, 6);

        // 주사위는 성공/실패가 아니므로 굴린 눈금을 그대로 연출에 통지한다.
        ctx.Engine?.NotifyDiceResult(caster, roll, ctx.Card);

        if (eff.effects != null && eff.effects.Count > 0)
        {
            int idx = Mathf.Clamp(roll - 1, 0, eff.effects.Count - 1);
            var savedEffect = ctx.Effect;
            ctx.Effect = eff.effects[idx];
            Execute(ctx);
            ctx.Effect = savedEffect;
        }
        else
        {
            bool high = roll >= 4;
            var effects = high ? eff.thenEffects : eff.elseEffects;
            if (high) RegisterGambleSuccess(caster);
            else if (!caster.ignoreUnluck)
            {
                caster.Turn.unluckyThisTurn++;
                caster.unluckyThisBattle++;
                if (caster.Turn.buffInsuranceValue > 0)
                    caster.shield += caster.Turn.buffInsuranceValue;
                if (caster.Turn.buffInsuranceLuckValue > 0)
                    caster.luck += caster.Turn.buffInsuranceLuckValue;
            }

            if (effects != null) ExecuteAll(effects, ctx);
        }
    }

    // -- 31. fullHouseHit --
    private void HandleFullHouseHit(EffectContext ctx)
    {
        var eff = ctx.Effect;
        int hits = eff.EffectiveHits;
        float baseChance = eff.chance > 0 ? eff.chance : 0.5f;

        for (int i = 0; i < hits; i++)
        {
            bool success = ResolveGamble(ctx, baseChance);
            var effects = success ? eff.thenEffects : ctx.Caster.Turn.lastGambleUnluckSoftened ? null : eff.elseEffects;
            if (effects != null) ExecuteAll(effects, ctx);
        }
    }

    // -- 32. buffInsurance --
    private void HandleBuffInsurance(EffectContext ctx)
    {
        ctx.Caster.Turn.buffInsuranceValue += Mathf.RoundToInt(ctx.Effect.value);
        ctx.Caster.Turn.buffInsuranceLuckValue += Mathf.RoundToInt(ctx.Effect.bonusMultiplier);
    }

    // -- 33. gambleChanceBuff --
    private void HandleGambleChanceBuff(EffectContext ctx)
    {
        ctx.Caster.Turn.gambleBonusChance += Mathf.RoundToInt(ctx.Effect.value);
    }

    // -- 34. retryOnUnluck --
    private void HandleRetryOnUnluck(EffectContext ctx)
    {
        ctx.Caster.Turn.retryOnUnluckFlag = true;
    }

    // -- 35. coinFlipHeads --
    private void HandleCoinFlipHeads(EffectContext ctx)
    {
        var eff = ctx.Effect;
        bool heads = ResolveGamble(ctx, 0.5f);

        if (heads)
        {
            int lostHp = ctx.Caster.maxHp - ctx.Caster.hp;
            if (lostHp > 0) ctx.Caster.shield += lostHp;
            if (eff.thenEffects != null) ExecuteAll(eff.thenEffects, ctx);
        }
        else
        {
            if (!ctx.Caster.Turn.lastGambleUnluckSoftened && eff.elseEffects != null) ExecuteAll(eff.elseEffects, ctx);
        }
    }
}
