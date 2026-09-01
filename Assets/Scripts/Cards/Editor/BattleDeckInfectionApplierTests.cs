using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class BattleDeckInfectionApplierTests
{
    [SetUp]
    public void SetUp()
    {
        CardDatabase.ResetInstance();
    }

    [TearDown]
    public void TearDown()
    {
        CardDatabase.ResetInstance();
    }

    [Test]
    public void InsertRansomwareIntoDeck_AddsSpecialCardClones()
    {
        var deck = new List<CardData>
        {
            new CardData { id = "TEST_A", cardName = "Test A" },
            new CardData { id = "TEST_B", cardName = "Test B" }
        };
        var effects = new List<BattleSupportEffect>
        {
            new BattleSupportEffect { effectType = BattleSupportEffectType.MaxHpBonus, value = 99 },
            new BattleSupportEffect { effectType = BattleSupportEffectType.InsertRansomware, value = 2 }
        };

        int added = BattleDeckInfectionApplier.InsertRansomwareIntoDeck(deck, effects);
        CardData template = CardDatabase.Instance.GetById(BattleDeckInfectionApplier.RansomwareCardId);

        Assert.AreEqual(2, added);
        Assert.AreEqual(4, deck.Count);
        Assert.AreEqual(2, deck.Count(card => card != null && card.id == BattleDeckInfectionApplier.RansomwareCardId));
        Assert.IsTrue(deck.Where(card => card != null && card.id == BattleDeckInfectionApplier.RansomwareCardId)
            .All(card => !ReferenceEquals(card, template)));
    }
}
