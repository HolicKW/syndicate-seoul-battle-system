using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CoreManagerTests
{
    private GameObject engineObject;
    private BattleEngine engine;
    private CoreManager coreManager;

    [SetUp]
    public void SetUp()
    {
        Cleanup();

        engineObject = new GameObject("CoreManager_Test");
        engine = engineObject.AddComponent<BattleEngine>();
        coreManager = engineObject.AddComponent<CoreManager>();

        engine.InitBattle(new System.Collections.Generic.List<CardData>(), 80, 3, 50, 3);
        coreManager.Init(engine);
    }

    [TearDown]
    public void TearDown()
    {
        Cleanup();
    }

    [Test]
    public void StrengthOnManyCards_TriggersEveryThresholdCardsPlayed()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_STRENGTH_ON_MANY_CARDS",
            cardName = "Strength On Many Cards",
            coreEffect = new CoreEffect
            {
                coreType = "strengthOnManyCards",
                trigger = "cardPlayed",
                threshold = 3,
                value = 2
            }
        });

        for (int played = 1; played <= 6; played++)
        {
            engine.Player.Turn.cardsPlayedThisTurn = played;
            engine.CheckCoreTriggers("cardPlayed", engine.Player);
        }

        Assert.AreEqual(4, engine.Player.strength);
    }

    [Test]
    public void AutoVirus_NotifiesVirusAppliedPowers()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_AUTO_VIRUS",
            cardName = "Auto Virus",
            coreEffect = new CoreEffect
            {
                coreType = "autoVirus",
                trigger = "turnStart",
                value = 4,
            },
        });
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_VIRAL_LAB",
            cardName = "Viral Lab",
            coreEffect = new CoreEffect
            {
                coreType = "viralLab",
                trigger = "virusApplied",
            },
        });

        engine.ApplyCoreEffects("turnStart", engine.Player);

        Assert.AreEqual(5, engine.Enemy.virus);
        Assert.AreEqual(4, engine.Enemy.Turn.virusAppliedThisTurn);
    }

    [Test]
    public void DrawOnLuckyChance_DoesNotTriggerFromCardPlayedTurnCounter()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_DRAW_ON_LUCKY_CHANCE",
            cardName = "Draw On Lucky Chance",
            coreEffect = new CoreEffect
            {
                coreType = "drawOnLuckyChance",
                trigger = "cardPlayed",
                value = 100,
            },
        });
        engine.Player.Turn.luckyThisTurn = 1;
        engine.Player.drawPile.Add(new CardData { id = "DRAW_1" });

        engine.CheckCoreTriggers("cardPlayed", engine.Player);

        Assert.AreEqual(0, engine.Player.hand.Count);
        Assert.AreEqual(1, engine.Player.drawPile.Count);
    }

    [Test]
    public void DrawOnLuckyChance_TriggersOnlyOnSuccessfulGambleResult()
    {
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_DRAW_ON_LUCKY_CHANCE",
            cardName = "Draw On Lucky Chance",
            coreEffect = new CoreEffect
            {
                coreType = "drawOnLuckyChance",
                trigger = "cardPlayed",
                value = 100,
            },
        });
        engine.Player.drawPile.Add(new CardData { id = "DRAW_1" });
        engine.Player.drawPile.Add(new CardData { id = "DRAW_2" });

        engine.NotifyGambleResult(engine.Player, false, new CardData { id = "GAMBLE", cost = 1 });
        Assert.AreEqual(0, engine.Player.hand.Count);

        engine.NotifyGambleResult(engine.Player, true, new CardData { id = "GAMBLE", cost = 1 });
        Assert.AreEqual(1, engine.Player.hand.Count);
        Assert.AreEqual(1, engine.Player.drawPile.Count);
    }

    [Test]
    public void RandomBuffStart_WithChance_TriggersGambleSuccessAndBuff()
    {
        for (int i = 0; i < 5; i++)
            engine.Player.drawPile.Add(new CardData { id = $"DRAW_{i}" });
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_RANDOM_BUFF_START",
            cardName = "Random Buff Start",
            coreEffect = new CoreEffect
            {
                coreType = "randomBuffStart",
                trigger = "turnStart",
                chance = 100,
                value = 10,
                energy = 3,
                draw = 3,
            },
        });

        engine.ApplyCoreEffects("turnStart", engine.Player);

        Assert.AreEqual(1, engine.Player.Turn.gambleSuccessThisTurn);
        Assert.AreEqual(1, engine.Player.Turn.luckyThisTurn);
        bool gainedStrength = engine.Player.strength == 10;
        bool gainedEnergy = engine.Player.energy == 6;
        bool drewCards = engine.Player.hand.Count == 3;
        Assert.IsTrue(gainedStrength || gainedEnergy || drewCards);
    }

    [Test]
    public void RandomBuffStart_WithChance_FailureDoesNotBuff()
    {
        UnityEngine.Random.InitState(1);
        engine.Player.activeCores.Add(new CardData
        {
            id = "TEST_RANDOM_BUFF_START",
            cardName = "Random Buff Start",
            coreEffect = new CoreEffect
            {
                coreType = "randomBuffStart",
                trigger = "turnStart",
                chance = 0.0001f,
                value = 10,
                energy = 3,
                draw = 3,
            },
        });

        engine.ApplyCoreEffects("turnStart", engine.Player);

        Assert.AreEqual(0, engine.Player.Turn.gambleSuccessThisTurn);
        Assert.AreEqual(1, engine.Player.Turn.unluckyThisTurn);
        Assert.AreEqual(0, engine.Player.strength);
        Assert.AreEqual(3, engine.Player.energy);
        Assert.AreEqual(0, engine.Player.hand.Count);
    }

    private void Cleanup()
    {
        if (CoreManager.Instance != null)
            Object.DestroyImmediate(CoreManager.Instance);

        if (BattleEngine.Instance != null)
            Object.DestroyImmediate(BattleEngine.Instance.gameObject);

        if (engineObject != null)
            Object.DestroyImmediate(engineObject);

        engineObject = null;
        engine = null;
        coreManager = null;
    }
}
