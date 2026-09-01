using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// EffectInterpreter 핸들러 테스트.
/// BattleEngine 없이 폴백 경로로 실행하는 유닛 테스트.
/// </summary>
[TestFixture]
public class EffectHandlerTests
{
    private EffectInterpreter interpreter;
    private EntityState player;
    private EntityState enemy;

    [SetUp]
    public void SetUp()
    {
        interpreter = new EffectInterpreter();
        interpreter.RegisterAllCoreHandlers();

        player = EntityState.Create(80, 3);
        enemy = EntityState.Create(50, 3);
        player.opponent = enemy;
        enemy.opponent = player;
    }

    private EffectContext MakeCtx(CardEffect effect, CardData card = null)
    {
        return new EffectContext
        {
            Caster = player,
            Target = enemy,
            Effect = effect,
            Card = card,
        };
    }

    // ===================================================
    //  래퍼 핸들러 테스트
    // ===================================================

    [Test]
    public void Alias_Strength_AddsToSelf()
    {
        var ctx = MakeCtx(new CardEffect { type = "strength", value = 3 });
        interpreter.Execute(ctx);
        Assert.AreEqual(3, player.strength);
    }

    [Test]
    public void Alias_Weakness_AddsToEnemy()
    {
        var ctx = MakeCtx(new CardEffect { type = "weakness", value = 2 });
        interpreter.Execute(ctx);
        Assert.AreEqual(2, enemy.weakness);
    }

    [Test]
    public void Alias_Virus_AddsToEnemy()
    {
        var ctx = MakeCtx(new CardEffect { type = "virus", value = 5 });
        interpreter.Execute(ctx);
        Assert.AreEqual(5, enemy.virus);
    }

    [Test]
    public void Alias_Virus_ScalesWithTargetHandCount()
    {
        enemy.hand.Add(new CardData { id = "E1" });
        enemy.hand.Add(new CardData { id = "E2" });
        enemy.hand.Add(new CardData { id = "E3" });

        var ctx = MakeCtx(new CardEffect
        {
            type = "virus",
            value = 0,
            scaling = new ScalingData { source = "targetHandCount", multiplier = 2 },
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(6, enemy.virus);
    }

    [Test]
    public void Alias_VirusOnCardPlayedNextTurn_AddsPendingToEnemy()
    {
        var ctx = MakeCtx(new CardEffect { type = "virusOnCardPlayedNextTurn", value = 1 });
        interpreter.Execute(ctx);

        Assert.AreEqual(1, enemy.virusOnCardPlayedNextTurn);
    }

    [Test]
    public void Biohazard_CleanseAndReflectDebuffs_MovesDebuffsToEnemy()
    {
        player.weakness = 2;
        player.virus = 5;
        player.corrosion = 3;
        enemy.weakness = 1;
        enemy.virus = 4;
        enemy.corrosion = 2;

        var ctx = MakeCtx(new CardEffect { type = "cleanseAndReflectDebuffs" });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, player.weakness);
        Assert.AreEqual(0, player.virus);
        Assert.AreEqual(0, player.corrosion);
        Assert.AreEqual(3, enemy.weakness);
        Assert.AreEqual(9, enemy.virus);
        Assert.AreEqual(5, enemy.corrosion);
    }

    [Test]
    public void Biohazard_ConsumeVirus_ShieldAndHealByChunk_UsesConsumedAmount()
    {
        player.hp = 40;
        enemy.virus = 12;

        var ctx = MakeCtx(new CardEffect
        {
            type = "consumeVirus",
            value = 15,
            action = "shieldAndHealByChunk",
            ratio = 5,
            minValue = 5,
            bonusMultiplier = 4,
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, enemy.virus);
        Assert.AreEqual(60, player.shield);
        Assert.AreEqual(48, player.hp);
    }

    [Test]
    public void Alias_CasinoRoyalFieldReset_SuccessHealsOnlyCaster()
    {
        player.hp = 40;
        player.shield = 10;
        player.weakness = 2;
        player.virus = 3;
        player.corrosion = 4;
        enemy.hp = 20;
        enemy.shield = 15;
        enemy.weakness = 1;
        enemy.virus = 5;
        enemy.corrosion = 6;

        var ctx = MakeCtx(new CardEffect
        {
            type = "casinoRoyalFieldReset",
            chance = 100,
            value = 0.5f,
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, player.shield);
        Assert.AreEqual(0, player.weakness);
        Assert.AreEqual(0, player.virus);
        Assert.AreEqual(0, player.corrosion);
        Assert.AreEqual(80, player.hp);
        Assert.AreEqual(0, enemy.shield);
        Assert.AreEqual(0, enemy.weakness);
        Assert.AreEqual(0, enemy.virus);
        Assert.AreEqual(0, enemy.corrosion);
        Assert.AreEqual(20, enemy.hp);
    }

    [Test]
    public void Alias_CasinoRoyalFieldReset_FailureHealsBoth()
    {
        UnityEngine.Random.InitState(1);
        player.hp = 40;
        enemy.hp = 20;

        var ctx = MakeCtx(new CardEffect
        {
            type = "casinoRoyalFieldReset",
            chance = float.Epsilon,
            value = 0.5f,
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(80, player.hp);
        Assert.AreEqual(45, enemy.hp);
    }

    [Test]
    public void Alias_NextGambleBonusChance_DoesNotStackAndIsConsumed()
    {
        interpreter.Execute(MakeCtx(new CardEffect { type = "nextGambleBonusChance", value = 10 }));
        interpreter.Execute(MakeCtx(new CardEffect { type = "nextGambleBonusChance", value = 10 }));

        Assert.AreEqual(10, player.Turn.gambleBonusChance);

        interpreter.Execute(MakeCtx(new CardEffect
        {
            type = "gamble",
            chance = 100,
            thenEffects = new List<CardEffect>(),
        }));

        Assert.AreEqual(0, player.Turn.gambleBonusChance);
    }

    [Test]
    public void Gamble_BankruptcyReroll_PenalizesOnlyAfterRerollFailure()
    {
        UnityEngine.Random.InitState(1);
        player.hp = 80;
        player.activeCores.Add(new CardData
        {
            id = "TEST_BANKRUPTCY_REROLL",
            coreEffect = new CoreEffect
            {
                coreType = "bankruptcyReroll",
                value = 0.25f,
            },
        });

        interpreter.Execute(MakeCtx(new CardEffect
        {
            type = "gamble",
            chance = float.Epsilon,
        }));

        Assert.AreEqual(60, player.hp);
        Assert.AreEqual(1, player.Turn.unluckyThisTurn);
    }

    [Test]
    public void Gamble_LuckyDayDebt_AddsLuckOnSuccessAndTracksDebt()
    {
        interpreter.Execute(MakeCtx(new CardEffect
        {
            type = "luckyDayDebt",
            value = 2,
            bonusMultiplier = 7,
        }));

        interpreter.Execute(MakeCtx(new CardEffect
        {
            type = "gamble",
            chance = 100,
        }));

        Assert.AreEqual(2, player.luck);
        Assert.AreEqual(1, player.Turn.gambleSuccessThisTurn);
        Assert.AreEqual(1, player.Turn.luckyDaySuccessCount);
        Assert.AreEqual(7, player.Turn.luckyDayHpLossPerSuccess);
    }

    [Test]
    public void Alias_DiscardAll_DiscardsEntireHand()
    {
        player.hand.Add(new CardData { id = "C1" });
        player.hand.Add(new CardData { id = "C2" });
        player.hand.Add(new CardData { id = "C3" });

        var ctx = MakeCtx(new CardEffect { type = "discardAll" });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, player.hand.Count);
        Assert.AreEqual(3, player.voidPile.Count);
    }

    [Test]
    public void Alias_ScaledDamage_UseScaling()
    {
        player.networkStacks = 5;
        var ctx = MakeCtx(new CardEffect
        {
            type = "scaledDamage",
            value = 2,
            scaling = new ScalingData { source = "networkStacks", multiplier = 2 },
        });
        interpreter.Execute(ctx);

        // scaledEffect: value + networkStacks * multiplier = 2 + 5*2 = 12
        // enemy started at 50hp, no shield
        Assert.Less(enemy.hp, 50);
    }

    [Test]
    public void Alias_DoubleLuck_MultipliesLuck()
    {
        player.luck = 3;
        var ctx = MakeCtx(new CardEffect { type = "doubleLuck" });
        interpreter.Execute(ctx);
        Assert.AreEqual(6, player.luck);
    }

    [Test]
    public void Alias_RetaliateOnHit_SetsNextRetaliationDamage()
    {
        var ctx = MakeCtx(new CardEffect { type = "retaliateOnHit", value = 10 });
        interpreter.Execute(ctx);
        Assert.AreEqual(10, player.Turn.retaliateOnHitDamage);
    }

    [Test]
    public void Alias_EvadeNextHit_AddsEvadeStack()
    {
        var ctx = MakeCtx(new CardEffect { type = "evadeNextHit", value = 1 });
        interpreter.Execute(ctx);
        Assert.AreEqual(1, player.Turn.evadeNextHits);
    }

    [Test]
    public void Network_NextProtocolEffectRepeat_AddsRepeatCount()
    {
        var ctx = MakeCtx(new CardEffect { type = "nextProtocolEffectRepeat", value = 1 });
        interpreter.Execute(ctx);
        Assert.AreEqual(1, player.nextProtocolEffectRepeat);
    }

    [Test]
    public void Alias_EnergyFromShieldChunk_ConsumesOnlyFullChunks()
    {
        player.shield = 27;
        player.energy = 1;

        var ctx = MakeCtx(new CardEffect { type = "energyFromShieldChunk", value = 10 });
        interpreter.Execute(ctx);

        Assert.AreEqual(7, player.shield);
        Assert.AreEqual(3, player.energy);
    }

    [Test]
    public void Alias_DismantleAllAndVoidRecall_SwapsHandWithRecentVoidCards()
    {
        var handA = new CardData { id = "HAND_A", cardName = "Hand A" };
        var handB = new CardData { id = "HAND_B", cardName = "Hand B" };
        var voidA = new CardData { id = "VOID_A", cardName = "Void A" };
        var voidB = new CardData { id = "VOID_B", cardName = "Void B" };

        player.hand.Add(handA);
        player.hand.Add(handB);
        player.voidPile.Add(voidA);
        player.voidPile.Add(voidB);

        var ctx = MakeCtx(new CardEffect { type = "dismantleAllAndVoidRecall", recallCount = 5 });
        interpreter.Execute(ctx);

        Assert.AreEqual(2, player.rollbackStoredHand.Count);
        Assert.AreSame(handA, player.rollbackStoredHand[0]);
        Assert.AreSame(handB, player.rollbackStoredHand[1]);
        Assert.AreEqual(2, player.hand.Count);
        Assert.AreSame(voidB, player.hand[0]);
        Assert.AreSame(voidA, player.hand[1]);
        Assert.IsTrue(player.hand[0].isTemporary);
        Assert.IsTrue(player.hand[1].isTemporary);
        Assert.AreEqual(0, player.voidPile.Count);
    }

    [Test]
    public void Alias_ExtractTopOrDraw_DismantlesExtractAndDrawsOthers()
    {
        var extractCard = new CardData
        {
            id = "EXTRACT_A",
            cardName = "Extract A",
            extractEffects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 7 }
            }
        };
        var normalCard = new CardData { id = "NORMAL_A", cardName = "Normal A" };

        player.drawPile.Add(extractCard);
        player.drawPile.Add(normalCard);

        var ctx = MakeCtx(new CardEffect { type = "extractTopOrDraw", value = 2 });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, player.drawPile.Count);
        Assert.AreEqual(1, player.hand.Count);
        Assert.AreSame(normalCard, player.hand[0]);
        Assert.AreEqual(1, player.voidPile.Count);
        Assert.AreSame(extractCard, player.voidPile[0]);
        Assert.AreEqual(7, player.shield);
        Assert.AreEqual(1, player.Turn.dismantledThisTurn);
        Assert.AreEqual(1, player.dismantledThisBattle);
    }

    [Test]
    public void Alias_CloneHandCard_CopiesNetworkCardAsTemporary()
    {
        var source = new CardData
        {
            id = "NETWORK_A",
            cardName = "Network A",
            keywords = new List<string> { "network" },
            cost = 2
        };
        player.hand.Add(source);

        var ctx = MakeCtx(new CardEffect
        {
            type = "cloneHandCard",
            value = 1,
            filter = "network"
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(2, player.hand.Count);
        var clone = player.hand[1];
        Assert.AreNotSame(source, clone);
        Assert.AreEqual("NETWORK_A_TEMP_CLONE", clone.id);
        Assert.AreEqual(2, clone.cost);
        Assert.IsTrue(clone.isTemporary);
        CollectionAssert.Contains(clone.keywords, "일시적");
    }

    [Test]
    public void MasterOverride_RemoteExecutesHighestAttackOrSkillAndReturnsItToHand()
    {
        var lowSkill = new CardData
        {
            id = "LOW_SKILL",
            cardName = "Low Skill",
            type = CardType.Skill,
            cost = 1,
            effects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 3 }
            }
        };
        var ignoredCore = new CardData
        {
            id = "IGNORED_POWER",
            cardName = "Ignored Core",
            type = CardType.Core,
            cost = 9
        };
        var remote = new CardData
        {
            id = "REMOTE_ATTACK",
            cardName = "Remote Attack",
            type = CardType.Attack,
            cost = 4,
            protocolCondition = "any",
            protocolEffects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 5 }
            },
            effects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 7 }
            }
        };

        player.drawPile.Add(lowSkill);
        player.drawPile.Add(ignoredCore);
        player.drawPile.Add(remote);

        var source = new CardData { id = "NW_035", cardName = "Master Override" };
        var ctx = MakeCtx(new CardEffect { type = "masterOverride" }, source);
        interpreter.Execute(ctx);

        Assert.AreEqual(12, player.shield);
        Assert.IsFalse(player.drawPile.Contains(remote));
        Assert.IsTrue(player.hand.Contains(remote));
        Assert.IsTrue(player.drawPile.Contains(ignoredCore));
        Assert.AreSame(remote, player.lastPlayedCard);
        Assert.AreEqual(1, player.Turn.cardsPlayedThisTurn);
    }

    // ===================================================
    //  지연 실행 테스트
    // ===================================================

    [Test]
    public void Deferred_EnergyNextTurn_EnqueuesAction()
    {
        var ctx = MakeCtx(new CardEffect { type = "energyNextTurn", value = 2 });
        interpreter.Execute(ctx);

        Assert.AreEqual(1, player.deferredActions.Count);
        Assert.AreEqual("nextTurnStart", player.deferredActions[0].timing);
        Assert.AreEqual("energy", player.deferredActions[0].effect.type);
    }

    [Test]
    public void Deferred_SelfDamageAtTurnEnd_EnqueuesAction()
    {
        var ctx = MakeCtx(new CardEffect { type = "selfDamageAtTurnEnd", value = 5 });
        interpreter.Execute(ctx);

        Assert.AreEqual(1, player.deferredActions.Count);
        Assert.AreEqual("turnEnd", player.deferredActions[0].timing);
        Assert.AreEqual("selfDamage", player.deferredActions[0].effect.type);
    }

    [Test]
    public void Deferred_AddEndOfTurnDiscard_EnqueuesDiscard()
    {
        var ctx = MakeCtx(new CardEffect { type = "addEndOfTurnDiscard", value = 2, mode = "random" });
        interpreter.Execute(ctx);

        Assert.AreEqual(1, player.deferredActions.Count);
        Assert.AreEqual("turnEnd", player.deferredActions[0].timing);
        Assert.AreEqual("discard", player.deferredActions[0].effect.type);
        Assert.AreEqual("random", player.deferredActions[0].effect.mode);
    }

    // ===================================================
    //  신규 핸들러 테스트
    // ===================================================

    [Test]
    public void New_EnergyDrain_ReducesTargetEnergyNextTurn()
    {
        enemy.energy = 3;
        player.energy = 1;

        var ctx = MakeCtx(new CardEffect { type = "energyDrain", value = 2 });
        interpreter.Execute(ctx);

        Assert.AreEqual(3, enemy.energy);
        Assert.AreEqual(1, player.energy);
        Assert.AreEqual(2, enemy.energyDrainNext);
    }

    [Test]
    public void New_Invincible_SetsFlag()
    {
        var ctx = MakeCtx(new CardEffect { type = "invincible" });
        interpreter.Execute(ctx);
        Assert.IsTrue(player.Turn.invincibleThisTurn);
    }

    [Test]
    public void New_DamageReduction_SetsFraction()
    {
        var ctx = MakeCtx(new CardEffect { type = "damageReduction", value = 50 });
        interpreter.Execute(ctx);
        Assert.AreEqual(0.5f, player.Turn.damageReductionThisTurn, 0.01f);
    }

    [Test]
    public void New_VirusByDebuff_CalculatesCorrectly()
    {
        enemy.weakness = 3;
        enemy.corrosion = 2;

        var ctx = MakeCtx(new CardEffect { type = "virusByDebuff", value = 2 });
        interpreter.Execute(ctx);

        // (3 + 2) * 2 = 10
        Assert.AreEqual(10, enemy.virus);
    }

    [Test]
    public void New_ShieldByHpPercent_GrantsShield()
    {
        player.maxHp = 100;
        var ctx = MakeCtx(new CardEffect { type = "shieldByHpPercent", value = 25 });
        interpreter.Execute(ctx);
        Assert.AreEqual(25, player.shield);
    }

    [Test]
    public void New_SetHpScale_ClampToMaxHp()
    {
        player.hp = 40;
        player.maxHp = 80;

        var ctx = MakeCtx(new CardEffect { type = "setHpScale", value = 3.0f });
        interpreter.Execute(ctx);

        // 40 * 3 = 120, clamped to 80
        Assert.AreEqual(80, player.hp);
    }

    [Test]
    public void New_SetHpScaleStoreLoss_StoresActualHpLoss()
    {
        player.hp = 81;
        player.maxHp = 100;

        var ctx = MakeCtx(new CardEffect { type = "setHpScaleStoreLoss", value = 0.5f });
        interpreter.Execute(ctx);

        Assert.AreEqual(40, player.hp);
        Assert.AreEqual(41, player.Turn.lastHpLoss);
    }

    [Test]
    public void New_ShieldByLastHpLoss_GrantsStoredLossAndClearsIt()
    {
        player.Turn.lastHpLoss = 41;

        var ctx = MakeCtx(new CardEffect { type = "shieldByLastHpLoss" });
        interpreter.Execute(ctx);

        Assert.AreEqual(41, player.shield);
        Assert.AreEqual(0, player.Turn.lastHpLoss);
    }

    [Test]
    public void New_DrawPerEnergy_DrawsCorrectAmount()
    {
        player.energy = 3;
        for (int i = 0; i < 10; i++)
            player.drawPile.Add(new CardData { id = $"D{i}" });

        var ctx = MakeCtx(new CardEffect { type = "drawPerEnergy", value = 1 });
        interpreter.Execute(ctx);

        Assert.AreEqual(3, player.hand.Count);
    }

    // ===================================================
    //  엣지 케이스 테스트
    // ===================================================

    [Test]
    public void Edge_SearchDeck_StopsAtHandLimit()
    {
        // 패를 9장으로 채움
        for (int i = 0; i < 9; i++)
            player.hand.Add(new CardData { id = $"H{i}" });
        // 덱에 5장
        for (int i = 0; i < 5; i++)
            player.drawPile.Add(new CardData { id = $"D{i}", cost = i + 1 });

        var ctx = MakeCtx(new CardEffect { type = "searchByType", value = 3 });
        interpreter.Execute(ctx);

        // 9장 + 최대 1장 = 10장까지만
        Assert.LessOrEqual(player.hand.Count, 10);
    }

    [Test]
    public void Edge_SearchDeck_CanFindVirusKeywordCards()
    {
        player.drawPile.Add(new CardData { id = "NO_MATCH", keywords = new List<string> { "corrosion" } });
        player.drawPile.Add(new CardData { id = "MATCH", keywords = new List<string> { "virus" } });

        var ctx = MakeCtx(new CardEffect { type = "searchDeck", value = 1, filter = "virus" });
        interpreter.Execute(ctx);

        Assert.AreEqual(1, player.hand.Count);
        Assert.AreEqual("MATCH", player.hand[0].id);
    }

    [Test]
    public void Edge_ReturnToHand_StopsAtHandLimit()
    {
        for (int i = 0; i < 10; i++)
            player.hand.Add(new CardData { id = $"H{i}" });

        var card = new CardData { id = "RETURN", cost = 3 };
        var ctx = MakeCtx(new CardEffect { type = "returnToHandZeroCost" }, card);
        interpreter.Execute(ctx);

        // 이미 10장이므로 추가되지 않음
        Assert.AreEqual(10, player.hand.Count);
        Assert.IsFalse(player.hand.Contains(card));
    }

    [Test]
    public void Special_GambleDeathCross_SuccessDealsHpDiffTimesValue()
    {
        player.hp = 80;
        enemy.hp = 50;

        var ctx = MakeCtx(new CardEffect
        {
            type = "gambleDeathCross",
            chance = 100,
            value = 2,
            ratio = 0.5f,
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(0, enemy.hp);
        Assert.AreEqual(80, player.hp);
    }

    [Test]
    public void Special_GambleDeathCross_FailureDealsNoEnemyDamageAndSelfDamageByRatio()
    {
        UnityEngine.Random.InitState(1);
        player.hp = 80;
        enemy.hp = 50;

        var ctx = MakeCtx(new CardEffect
        {
            type = "gambleDeathCross",
            chance = 0.0001f,
            value = 2,
            ratio = 0.5f,
        });
        interpreter.Execute(ctx);

        Assert.AreEqual(50, enemy.hp);
        Assert.AreEqual(65, player.hp);
    }

    [Test]
    public void Edge_RecursionDepth_LimitsAtTen()
    {
        // 자기 자신을 재귀 호출하는 이펙트 (conditional → thenEffects → conditional...)
        var recursiveEffect = new CardEffect
        {
            type = "conditional",
            condition = new ConditionData { type = "hpAbove", value = 0 },
        };
        recursiveEffect.thenEffects = new List<CardEffect> { recursiveEffect };

        var ctx = MakeCtx(recursiveEffect);
        // 재귀 깊이 10에서 중단되어 StackOverflow 발생하지 않음
        Assert.DoesNotThrow(() => interpreter.Execute(ctx));
    }

    [Test]
    public void Edge_InvincibleBlocksDamage()
    {
        enemy.hp = 50;
        enemy.Turn.invincibleThisTurn = true;

        var ctx = MakeCtx(new CardEffect { type = "damage", value = 100 });
        interpreter.Execute(ctx);

        Assert.AreEqual(50, enemy.hp);
    }

    [Test]
    public void Edge_DamageReductionReducesDamage()
    {
        enemy.hp = 50;
        enemy.Turn.damageReductionThisTurn = 0.5f;

        var ctx = MakeCtx(new CardEffect { type = "damage", value = 20 });
        interpreter.Execute(ctx);

        // 20 * (1 - 0.5) = 10 damage → 50 - 10 = 40
        Assert.AreEqual(40, enemy.hp);
    }

    [Test]
    public void AllNewHandlersRegistered()
    {
        // 모든 래퍼/신규 핸들러가 등록되어 있는지 확인
        var requiredTypes = new[]
        {
            // 2단계 래퍼 40종
            "strength", "weakness", "virus", "corrosion", "virusOnCardPlayedNextTurn", "selfWeakness",
            "overflow", "overflowReduce", "overclockReduce", "overclockReset",
            "maxHpReduce", "doubleLuck", "loseAllLuck", "doubleVirus",
            "addLuck", "luckyDayDebt", "addExtraDraw", "removeOverclockLimit",
            "scaledDamage", "scaledDraw", "scaledShield", "scaledHeal", "scaledSelfDamage",
            "scaledSelfDamagePercent", "scaledSelfWeakness", "scaledMaxHpReduce",
            "healPercent", "endTurn", "discardAll", "discardRandom",
            "selfDamageByTargetShield", "shieldBreakAllAndDamage",
            "casinoRoyalFieldReset", "retryGambleOnUnluck",
            "dismantleAllAndScaleDraw", "dismantleAllAndScaleShield",
            "dismantleForEnergy", "dismantleNetworkAndDraw", "dismantleTop",
            "extractTopOrDraw",
            "diceRollLuck", "costReduceAttackCards", "searchByType", "cloneHandCard",
            "revealAndTakeHighestCost",
            // 3단계 14종
            "selfDamagePercent", "energyFromShield", "energyFromShieldChunk", "energyToHeal",
            "damageIfHandEmpty", "damageIfConsecutiveLuck",
            "damagePlusByLuckyThisBattle", "damagePlusByUnluckyThisBattle",
            "damageByLuckStack", "nextGambleBonusChance",
            "zeroCostHandCard", "costReductionRandom",
            "restoreMaxEnergy", "discardTopAndGainShieldByCost",
            // 4단계 18종
            "damageMaxPercent", "damageIfUnluckyThisTurn",
            "energyDrain", "drainEnergy", "consumeAllEnergy",
            "drawPerEnergy", "drawAndZeroCost",
            "diceRollCombo", "gambleDeathCross",
            "shieldByHpPercent", "shieldByLastHpLoss", "damageReduction", "invincible", "evadeNextHit", "nextAttackBonus", "retaliateOnHit",
            "conditionalAdd", "virusByDebuff", "costReductionPerOverclock",
            "setHpScale", "setHpScaleStoreLoss", "setEnemyHpScale",
            "cleanseAndReflectDebuffs",
            // 1단계 지연 6종
            "energyNextTurn", "nextTurnShield", "nextTurnSelfDamage",
            "returnToHandNextTurn", "addEndOfTurnDiscard", "selfDamageAtTurnEnd",
            // 누락 보완 2종
            "dismantleAllAndVoidRecall", "preventSelfDamageIfOverclock",
            "nextProtocolEffectRepeat",
        };

        var missing = new List<string>();
        foreach (var t in requiredTypes)
        {
            if (!interpreter.HasHandler(t))
                missing.Add(t);
        }

        Assert.IsEmpty(missing, $"미등록 핸들러: {string.Join(", ", missing)}");
    }
}
