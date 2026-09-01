using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BattleEngineRetaliationTests
{
    private GameObject engineObject;
    private BattleEngine engine;

    [SetUp]
    public void SetUp()
    {
        Cleanup();

        engineObject = new GameObject("BattleEngine_Retaliation_Test");
        engine = engineObject.AddComponent<BattleEngine>();
        engine.InitBattle(new List<CardData>(), 80, 3, 50, 3);
    }

    [TearDown]
    public void TearDown()
    {
        Cleanup();
    }

    [Test]
    public void ApplyDamage_RetaliateOnHit_AppliesOwnerStrengthAndWeaknessThroughNormalDefense()
    {
        engine.Player.strength = 5;
        engine.Player.weakness = 2;
        engine.Player.Turn.retaliateOnHitDamage = 10;
        engine.Enemy.shield = 3;

        engine.ApplyDamage(engine.Player, 5, engine.Enemy);

        Assert.AreEqual(75, engine.Player.hp);
        Assert.AreEqual(0, engine.Enemy.shield);
        Assert.AreEqual(40, engine.Enemy.hp);
        Assert.AreEqual(0, engine.Player.Turn.retaliateOnHitDamage);
    }

    [Test]
    public void ApplyDamage_RetaliationDamage_DoesNotTriggerRetaliationChain()
    {
        engine.Player.Turn.retaliateOnHitDamage = 10;
        engine.Enemy.Turn.retaliateOnHitDamage = 10;

        engine.ApplyDamage(engine.Player, 5, engine.Enemy);

        Assert.AreEqual(75, engine.Player.hp);
        Assert.AreEqual(40, engine.Enemy.hp);
        Assert.AreEqual(10, engine.Enemy.Turn.retaliateOnHitDamage);
    }

    [Test]
    public void ApplyDamage_EvadeNextHit_PreventsOneDamageInstance()
    {
        engine.Player.Turn.evadeNextHits = 1;

        engine.ApplyDamage(engine.Player, 5, engine.Enemy);
        engine.ApplyDamage(engine.Player, 5, engine.Enemy);

        Assert.AreEqual(75, engine.Player.hp);
        Assert.AreEqual(0, engine.Player.Turn.evadeNextHits);
    }

    [Test]
    public void ApplyDamage_EmergencyDodgeInHand_HalvesEnemyDamageAndDismantlesCard()
    {
        var emergencyDodge = new CardData { id = "BASE_014", cardName = "Emergency Dodge" };
        engine.Player.hand.Add(emergencyDodge);

        engine.ApplyDamage(engine.Player, 10, engine.Enemy);

        Assert.AreEqual(75, engine.Player.hp);
        CollectionAssert.DoesNotContain(engine.Player.hand, emergencyDodge);
        CollectionAssert.Contains(engine.Player.voidPile, emergencyDodge);
        Assert.AreEqual(1, engine.Player.Turn.dismantledThisTurn);
        Assert.AreEqual(1, engine.DismantleVfxQueue.Pending.Count);
        Assert.AreSame(emergencyDodge, engine.DismantleVfxQueue.Pending[0].Card);
        Assert.AreEqual(DismantleVfxSource.Hand, engine.DismantleVfxQueue.Pending[0].Source);
    }

    [Test]
    public void CheckBattleEnd_Victory_ConsumesTemporaryCardsLeftInDeckAndHand()
    {
        var handTemporary = new CardData { id = "TEMP_HAND", keywords = new List<string> { "일시적" } };
        var deckTemporary = new CardData { id = "TEMP_DECK", keywords = new List<string> { "일시적" } };
        var normal = new CardData { id = "NORMAL_CARD", keywords = new List<string>() };

        engine.Player.hand.Add(handTemporary);
        engine.Player.drawPile.Add(deckTemporary);
        engine.Player.hand.Add(normal);
        engine.Enemy.hp = 0;

        engine.CheckBattleEnd();

        CollectionAssert.Contains(BattleSceneData.ConsumedCardIds, "TEMP_HAND");
        CollectionAssert.Contains(BattleSceneData.ConsumedCardIds, "TEMP_DECK");
        CollectionAssert.DoesNotContain(BattleSceneData.ConsumedCardIds, "NORMAL_CARD");
    }

    [Test]
    public void CheckBattleEnd_Defeat_DoesNotConsumeTemporaryCardsLeftInDeckAndHand()
    {
        engine.Player.hand.Add(new CardData { id = "TEMP_HAND", keywords = new List<string> { "일시적" } });
        engine.Player.drawPile.Add(new CardData { id = "TEMP_DECK", keywords = new List<string> { "일시적" } });
        engine.Player.hp = 0;

        engine.CheckBattleEnd();

        CollectionAssert.DoesNotContain(BattleSceneData.ConsumedCardIds, "TEMP_HAND");
        CollectionAssert.DoesNotContain(BattleSceneData.ConsumedCardIds, "TEMP_DECK");
    }

    [Test]
    public void PlayCard_NextProtocolEffectRepeat_ReplaysNextProtocolEffectsOnce()
    {
        var setup = new CardData
        {
            id = "SETUP_REPEAT",
            cardName = "Setup Repeat",
            type = CardType.Skill,
            cost = 0,
            effects = new List<CardEffect>
            {
                new CardEffect { type = "nextProtocolEffectRepeat", value = 1 }
            }
        };
        var protocol = new CardData
        {
            id = "PROTOCOL_CARD",
            cardName = "Protocol Card",
            type = CardType.Skill,
            cost = 0,
            protocolCondition = "any",
            protocolEffects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 4 }
            },
            effects = new List<CardEffect>
            {
                new CardEffect { type = "shield", value = 1 }
            }
        };

        engine.Player.hand.Add(setup);
        engine.Player.hand.Add(protocol);

        engine.PlayCard(0);
        engine.PlayCard(0);

        Assert.AreEqual(9, engine.Player.shield);
        Assert.AreEqual(0, engine.Player.nextProtocolEffectRepeat);
    }

    [Test]
    public void PlayCard_DoubleThirdNetwork_DoublesThirdCardsDamage()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "DOUBLE_THIRD_POWER",
            type = CardType.Core,
            coreEffect = new CoreEffect { coreType = "doubleThirdNetwork", trigger = "cardPlayed" }
        });

        engine.Player.hand.Add(new CardData { id = "FIRST", type = CardType.Skill, cost = 0 });
        engine.Player.hand.Add(new CardData { id = "SECOND", type = CardType.Skill, cost = 0 });
        engine.Player.hand.Add(new CardData
        {
            id = "THIRD_ATTACK",
            type = CardType.Attack,
            cost = 0,
            effects = new List<CardEffect> { new CardEffect { type = "damage", value = 10 } }
        });

        engine.PlayCard(0);
        engine.PlayCard(0);
        engine.PlayCard(0);

        Assert.AreEqual(30, engine.Enemy.hp);
        Assert.AreEqual(0f, engine.Player.Turn.nextDamageMultiplier);
    }

    [Test]
    public void PlayCard_DoubleThirdNetwork_DoesNotCarryWhenThirdCardHasNoDamage()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "DOUBLE_THIRD_POWER",
            type = CardType.Core,
            coreEffect = new CoreEffect { coreType = "doubleThirdNetwork", trigger = "cardPlayed" }
        });

        engine.Player.hand.Add(new CardData { id = "FIRST", type = CardType.Skill, cost = 0 });
        engine.Player.hand.Add(new CardData { id = "SECOND", type = CardType.Skill, cost = 0 });
        engine.Player.hand.Add(new CardData { id = "THIRD_SKILL", type = CardType.Skill, cost = 0 });
        engine.Player.hand.Add(new CardData
        {
            id = "FOURTH_ATTACK",
            type = CardType.Attack,
            cost = 0,
            effects = new List<CardEffect> { new CardEffect { type = "damage", value = 10 } }
        });

        engine.PlayCard(0);
        engine.PlayCard(0);
        engine.PlayCard(0);
        engine.PlayCard(0);

        Assert.AreEqual(40, engine.Enemy.hp);
        Assert.AreEqual(0f, engine.Player.Turn.nextDamageMultiplier);
    }

    [Test]
    public void ThreadSync_UsesCommonDismantleCommitAndRecordsVfx()
    {
        var high = new CardData { id = "HIGH_COST", cardName = "High Cost", cost = 3 };
        var low = new CardData { id = "LOW_COST", cardName = "Low Cost", cost = 1 };
        var middle = new CardData { id = "MIDDLE_COST", cardName = "Middle Cost", cost = 2 };

        engine.Player.networkStacks = 2;
        engine.Player.hand.Add(high);
        engine.Player.hand.Add(low);
        engine.Player.hand.Add(middle);

        engine.Interpreter.Execute(new EffectContext
        {
            Caster = engine.Player,
            Target = engine.Enemy,
            Engine = engine,
            Effect = new CardEffect { type = "threadSync" },
        });

        Assert.AreEqual(1, engine.Player.hand.Count);
        Assert.AreSame(middle, engine.Player.hand[0]);
        Assert.AreEqual(2, engine.Player.voidPile.Count);
        Assert.AreSame(high, engine.Player.voidPile[0]);
        Assert.AreSame(low, engine.Player.voidPile[1]);
        Assert.AreEqual(2, engine.Player.Turn.dismantledThisTurn);
        Assert.AreEqual(2, engine.Player.dismantledThisBattle);
        Assert.AreEqual(42, engine.Enemy.hp);

        Assert.AreEqual(2, engine.DismantleVfxQueue.Pending.Count);
        Assert.AreSame(high, engine.DismantleVfxQueue.Pending[0].Card);
        Assert.AreEqual(DismantleVfxSource.Hand, engine.DismantleVfxQueue.Pending[0].Source);
        Assert.AreSame(low, engine.DismantleVfxQueue.Pending[1].Card);
        Assert.AreEqual(DismantleVfxSource.Hand, engine.DismantleVfxQueue.Pending[1].Source);
    }

    [Test]
    public void PlayCard_DynamicRebuild_DecrementsAndRecordsReturnedCard()
    {
        var rebuildCard = new CardData
        {
            id = "DYNAMIC_REBUILD",
            cardName = "Dynamic Rebuild",
            type = CardType.Skill,
            cost = 0,
            rebuildCount = 0,
            rebuildScaling = new ScalingData
            {
                source = "networkStacks",
                multiplier = 1f
            }
        };

        engine.Player.networkStacks = 2;
        engine.Player.hand.Add(rebuildCard);

        engine.PlayCard(0);
        var returnedCards = engine.ConsumeRebuiltCardsReturnedToHand();

        Assert.AreEqual(1, rebuildCard.rebuildCount);
        Assert.AreEqual(1, engine.Player.hand.Count);
        Assert.AreSame(rebuildCard, engine.Player.hand[0]);
        Assert.AreEqual(1, engine.Player.totalRebuildsThisBattle);
        Assert.AreEqual(1, returnedCards.Count);
        Assert.AreSame(rebuildCard, returnedCards[0]);
    }

    private void Cleanup()
    {
        BattleSceneData.Clear();

        if (BattleEngine.Instance != null)
            Object.DestroyImmediate(BattleEngine.Instance.gameObject);

        if (engineObject != null)
            Object.DestroyImmediate(engineObject);

        engineObject = null;
        engine = null;
    }
}
