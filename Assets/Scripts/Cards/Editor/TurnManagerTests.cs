using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class TurnManagerTests
{
    private GameObject engineObject;
    private GameObject turnManagerObject;
    private BattleEngine engine;
    private TurnManager turnManager;

    [SetUp]
    public void SetUp()
    {
        CleanupSingletons();
        BattleSceneData.Clear();

        engineObject = new GameObject("BattleEngine_Test");
        engine = engineObject.AddComponent<BattleEngine>();
        engine.InitBattle(new List<CardData>(), 80, 3, 50, 3);

        turnManagerObject = new GameObject("TurnManager_Test");
        turnManager = turnManagerObject.AddComponent<TurnManager>();
    }

    [TearDown]
    public void TearDown()
    {
        CleanupSingletons();
    }

    [Test]
    public void StartTurn_DoesNotDecayWeakness()
    {
        engine.Enemy.weakness = 1;

        turnManager.StartTurn(engine.Enemy);

        Assert.AreEqual(1, engine.Enemy.weakness);
    }

    [Test]
    public void StartTurn_DecaysOverclockButKeepsMinimumOne()
    {
        engine.Player.overclockStacks = 2;

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(1, engine.Player.overclockStacks);

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(1, engine.Player.overclockStacks);
    }

    [Test]
    public void StartTurn_DoesNotDecayOverclockWhenUnlimited()
    {
        engine.Player.overclockStacks = 3;
        engine.Player.overclockUnlimited = true;

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(3, engine.Player.overclockStacks);
    }

    [Test]
    public void EndTurn_DecaysWeakness()
    {
        engine.Enemy.weakness = 2;

        turnManager.EndTurn(engine.Enemy);

        Assert.AreEqual(1, engine.Enemy.weakness);
    }

    [Test]
    public void EndTurn_ExtraDebuffReduction_DecaysAdditionalWeakness()
    {
        engine.Enemy.weakness = 3;
        engine.Enemy.activeCores.Add(new CardData
        {
            id = "POWER_EXTRA_DEBUFF_REDUCTION",
            coreEffect = new CoreEffect
            {
                coreType = "extraDebuffReduction",
                value = 1,
            },
        });

        turnManager.EndTurn(engine.Enemy);

        Assert.AreEqual(1, engine.Enemy.weakness);
    }

    [Test]
    public void EndTurn_RestoresRollbackHandAfterCleaningTemporarySwap()
    {
        var originalA = new CardData { id = "ORIGINAL_A", cardName = "Original A" };
        var originalB = new CardData { id = "ORIGINAL_B", cardName = "Original B" };
        var temporary = new CardData { id = "TEMP_A", cardName = "Temp A", isTemporary = true };

        engine.Player.rollbackStoredHand.Add(originalA);
        engine.Player.rollbackStoredHand.Add(originalB);
        engine.Player.hand.Add(temporary);

        turnManager.EndTurn(engine.Player);

        Assert.AreEqual(2, engine.Player.hand.Count);
        Assert.AreSame(originalA, engine.Player.hand[0]);
        Assert.AreSame(originalB, engine.Player.hand[1]);
        Assert.AreEqual(1, engine.Player.voidPile.Count);
        Assert.AreSame(temporary, engine.Player.voidPile[0]);
        Assert.AreEqual(0, engine.Player.rollbackStoredHand.Count);
    }

    [Test]
    public void StartTurn_TracksActualDrawCount()
    {
        engine.Enemy.hand.Clear();
        engine.Enemy.drawPile.Clear();
        engine.Enemy.drawPile.Add(new CardData { id = "TEST_DRAW_1", cardName = "Draw 1" });
        engine.Enemy.drawPile.Add(new CardData { id = "TEST_DRAW_2", cardName = "Draw 2" });

        turnManager.StartTurn(engine.Enemy);

        Assert.AreEqual(2, turnManager.LastStartTurnDrawCount);
        Assert.AreEqual(2, engine.Enemy.hand.Count);
    }

    [Test]
    public void StartTurn_ActivatesPendingVirusOnCardPlayed()
    {
        engine.Enemy.virusOnCardPlayedNextTurn = 1;

        turnManager.StartTurn(engine.Enemy);

        Assert.AreEqual(1, engine.Enemy.Turn.virusOnCardPlayedThisTurn);
        Assert.AreEqual(0, engine.Enemy.virusOnCardPlayedNextTurn);
    }

    [Test]
    public void PlayCard_VirusOnCardPlayed_AppliesEachCardDuringTurn()
    {
        engine.Enemy.hand.Clear();
        engine.Enemy.drawPile.Clear();
        engine.Enemy.hand.Add(new CardData { id = "ENEMY_CARD_1", cardName = "Enemy Card 1", type = CardType.Skill, cost = 0 });
        engine.Enemy.hand.Add(new CardData { id = "ENEMY_CARD_2", cardName = "Enemy Card 2", type = CardType.Skill, cost = 0 });
        engine.Enemy.virusOnCardPlayedNextTurn = 1;

        turnManager.StartTurn(engine.Enemy);
        engine.PlayCard(0, engine.Enemy, engine.Player);
        engine.PlayCard(0, engine.Enemy, engine.Player);

        Assert.AreEqual(2, engine.Enemy.virus);
    }

    [Test]
    public void StartTurn_RansomwareInHand_DamagesShieldFirstAndIncreasesNextDamage()
    {
        var ransomware = new CardData
        {
            id = "STATUS_RANSOMWARE",
            cardName = "랜섬웨어",
            type = CardType.Skill,
            cost = 1,
            keywords = new List<string> { "ransomware" },
        };

        engine.Player.shield = 7;
        engine.Player.hand.Add(ransomware);

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(80, engine.Player.hp);
        Assert.AreEqual(2, engine.Player.shield);
        Assert.AreEqual(10, ransomware.ransomwareDamage);

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(72, engine.Player.hp);
        Assert.AreEqual(0, engine.Player.shield);
        Assert.AreEqual(15, ransomware.ransomwareDamage);
    }

    [Test]
    public void StartTurn_RansomwareDrawnAtTurnStart_DamagesImmediately()
    {
        var ransomware = new CardData
        {
            id = "STATUS_RANSOMWARE",
            cardName = "랜섬웨어",
            type = CardType.Skill,
            cost = 1,
            keywords = new List<string> { "ransomware" },
        };

        engine.Player.drawPile.Add(ransomware);

        turnManager.StartTurn(engine.Player);

        Assert.AreEqual(75, engine.Player.hp);
        Assert.AreSame(ransomware, engine.Player.hand[0]);
        Assert.AreEqual(10, ransomware.ransomwareDamage);
    }

    [Test]
    public void PlayCard_Ransomware_DisappearsInsteadOfEnteringVoidPile()
    {
        var ransomware = new CardData
        {
            id = "STATUS_RANSOMWARE",
            cardName = "랜섬웨어",
            type = CardType.Skill,
            cost = 1,
            keywords = new List<string> { "ransomware" },
        };

        engine.Player.energy = 3;
        engine.Player.hand.Add(ransomware);

        engine.PlayCard(0, engine.Player, engine.Enemy);

        Assert.AreEqual(2, engine.Player.energy);
        Assert.AreEqual(0, engine.Player.hand.Count);
        Assert.AreEqual(0, engine.Player.voidPile.Count);
        CollectionAssert.DoesNotContain(BattleSceneData.ConsumedCardIds, "STATUS_RANSOMWARE");
    }

    private void CleanupSingletons()
    {
        if (TurnManager.Instance != null)
            UnityEngine.Object.DestroyImmediate(TurnManager.Instance.gameObject);
        if (BattleEngine.Instance != null)
            UnityEngine.Object.DestroyImmediate(BattleEngine.Instance.gameObject);

        if (turnManagerObject != null)
            UnityEngine.Object.DestroyImmediate(turnManagerObject);
        if (engineObject != null)
            UnityEngine.Object.DestroyImmediate(engineObject);

        turnManagerObject = null;
        engineObject = null;
        turnManager = null;
        engine = null;
    }
}
