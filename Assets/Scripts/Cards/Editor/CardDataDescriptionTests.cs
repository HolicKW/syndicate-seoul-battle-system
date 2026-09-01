using NUnit.Framework;

[TestFixture]
public class CardDataDescriptionTests
{
    [Test]
    public void GetFormattedDescription_WithoutCaster_UsesBaseDamageValue()
    {
        var card = new CardData
        {
            description = "Deal {d:6} damage.",
        };

        Assert.AreEqual("Deal 6 damage.", card.GetFormattedDescription());
    }

    [Test]
    public void GetFormattedDescription_WithCaster_AppliesStrengthAndWeakness()
    {
        var card = new CardData
        {
            description = "Deal {d:6} damage.",
        };
        var caster = EntityState.Create(30, 3);
        caster.strength = 3;
        caster.weakness = 1;

        Assert.AreEqual("Deal 8 damage.", card.GetFormattedDescription(caster));
    }

    [Test]
    public void GetFormattedDescription_HeavyFinisher_ResolvesOverclockDamageText()
    {
        var card = new CardData
        {
            description = "[오버클럭] 적에게 {d:20}+(스택x20)의 피해를 줍니다. 적이 잃은 체력의 6%+(스택x3%)만큼 추가 피해를 줍니다(최대 250).",
        };
        var caster = EntityState.Create(80, 3);
        caster.overclockStacks = 2;

        Assert.AreEqual(
            "[오버클럭] 적에게 60의 피해를 줍니다. 적이 잃은 체력의 12%만큼 추가 피해를 줍니다(최대 250).",
            card.GetFormattedDescription(caster));
    }

    [Test]
    public void GetFormattedDescription_LifeShorteningBeam_ResolvesWeaknessReductionText()
    {
        var card = new CardData
        {
            description = "[오버클럭] 14+(스택x12)의 피해를 줍니다. 자신에게 약화 5-(스택x1)를 부여합니다(최소 1).",
        };
        var caster = EntityState.Create(80, 3);
        caster.overclockStacks = 2;

        Assert.AreEqual(
            "[오버클럭] 38의 피해를 줍니다. 자신에게 약화 3를 부여합니다(최소 1).",
            card.GetFormattedDescription(caster));
    }
    [Test]
    public void GetFormattedDescription_CardAccumCount_ResolvesCardLocalDamage()
    {
        var card = new CardData
        {
            description = "Deal {d:28} damage.",
            effects = new System.Collections.Generic.List<CardEffect>
            {
                new CardEffect
                {
                    type = "scaledDamage",
                    value = 28,
                    scaling = new ScalingData
                    {
                        source = "cardAccumCount",
                        multiplier = 8,
                    },
                },
            },
            accumulationCount = 3,
        };
        var caster = EntityState.Create(80, 3);
        caster.strength = 2;

        Assert.AreEqual("Deal 54 damage.", card.GetFormattedDescription(caster));
    }

    [Test]
    public void GetFormattedDescription_DismantledThisBattle_ResolvesDamageValue()
    {
        var card = new CardData
        {
            description = "적에게 {d:10}+(이번 전투에서 해체한 카드 수x8)의 피해를 줍니다.",
            effects = new System.Collections.Generic.List<CardEffect>
            {
                new CardEffect
                {
                    type = "scaledDamage",
                    value = 10,
                    scaling = new ScalingData
                    {
                        source = "dismantledThisBattle",
                        multiplier = 8,
                    },
                },
            },
        };
        var caster = EntityState.Create(80, 3);
        caster.dismantledThisBattle = 2;
        caster.strength = 3;
        caster.weakness = 1;

        Assert.AreEqual("적에게 28의 피해를 줍니다.", card.GetFormattedDescription(caster));
    }

    [Test]
    public void GetFormattedDescription_WithoutCaster_DismantledThisBattleUsesBaseDamage()
    {
        var card = new CardData
        {
            description = "적에게 {d:10}+(이번 전투에서 해체한 카드 수x8)의 피해를 줍니다.",
        };

        Assert.AreEqual("적에게 10의 피해를 줍니다.", card.GetFormattedDescription());
    }
}
